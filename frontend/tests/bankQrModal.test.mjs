import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const helperSource = await readFile(new URL('../src/services/bankQrSession.ts', import.meta.url), 'utf8');
const helperJs = ts.transpileModule(helperSource, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const helper = await import(`data:text/javascript;base64,${Buffer.from(helperJs).toString('base64')}`);
const sfc = await readFile(new URL('../src/components/InvoiceBankTransfer.vue', import.meta.url), 'utf8');
const { descriptor } = parse(sfc);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'ttl-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, esModuleInterop: true },
}).outputText;

// Exercise the real component script with isolated API/reactivity/clock seams.
// These are script integration tests, not a browser rendering or real payment test.
async function fixture() {
  let now = 0, next = 0, stateCalls = 0, infoCalls = 0, simulationCalls = 0, focusCalls = 0, cleanup;
  let remaining = 100000, deferredState;
  const timers = new Map(), events = [];
  const schedule = callback => { timers.set(++next, callback); return next; };
  const cancel = id => timers.delete(id);
  const info = () => ({ invoiceId: 1, invoiceCode: 'INV-20261011-0001', remainingAmount: remaining,
    content: 'PKNK INV-20261011-0001', qrUrl: null, simulationEnabled: true });
  const api = {
    info: async () => { infoCalls++; return info(); },
    state: async () => { stateCalls++; return deferredState ?? { remainingAmount: remaining }; },
    simulate: async () => { simulationCalls++; return {}; },
  };
  const vue = { defineComponent: value => value, ref: value => ({ value }),
    computed: getter => ({ get value() { return getter(); } }),
    watch: (getter, callback, options) => { if (options?.immediate) callback(getter()); },
    onUnmounted: callback => { cleanup = callback; } };
  const imports = {
    vue, axios: { isAxiosError: () => false },
    '@/services/bankTransfers': { bankTransfersApi: api },
    '@/stores/bankTransfers': { useBankTransfersStore: () => ({ pending: {}, put() {}, clear() {} }) },
    '@/stores/auth': { useAuthStore: () => ({ user: { userId: 1 } }) },
    '@/services/bankQrDisplay': { MOD_BIL_bankQrDisplay: () => 'offline-demo' },
    '@/services/bankQrSession': { ...helper, MOD_BIL_createQrSession: (onTick, onExpire) =>
      helper.MOD_BIL_createQrSession(onTick, onExpire, { now: () => now, schedule, cancel }) },
    '@/assets/images/vietqr-demo-fallback.svg': 'test-demo.svg',
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', 'setInterval', 'clearInterval', 'document', compiled)(
    name => { assert.ok(name in imports, `Unknown import ${name}`); return imports[name]; }, module, module.exports,
    schedule, cancel, { querySelector: () => ({ focus: () => { focusCalls++; } }) });
  const component = module.exports.default.setup({ invoiceId: 1 }, { expose() {}, emit: (...args) => events.push(args) });
  component.dialog.value = { open: false, showModal() { this.open = true; }, close() { this.open = false; } };
  await Promise.resolve(); await Promise.resolve();
  return { component, timers, events, advance: milliseconds => { now += milliseconds; },
    tick: async () => { for (const callback of [...timers.values()]) await callback(); },
    cleanup: () => cleanup(), setRemaining: value => { remaining = value; },
    deferState: () => { let resolve; deferredState = new Promise(done => { resolve = done; }); return resolve; },
    get stateCalls() { return stateCalls; }, get infoCalls() { return infoCalls; },
    get simulationCalls() { return simulationCalls; }, get focusCalls() { return focusCalls; } };
}
test('closed modal has no timer; opening starts countdown and polling', async () => {
  const f = await fixture(); assert.equal(f.timers.size, 0); await f.component.openModal();
  assert.equal(f.component.countdown.value, '15:00'); assert.equal(f.timers.size, 2); assert.equal(f.stateCalls, 1); f.cleanup();
});
test('expiry stops polling and guards manual check, retry and simulation', async () => {
  const f = await fixture(); await f.component.openModal(); f.advance(900000); await f.tick();
  assert.equal(f.component.expired.value, true); assert.equal(f.component.watching.value, false); assert.equal(f.timers.size, 0);
  const calls = f.stateCalls; f.component.confirmed.value = true;
  await f.component.checkNow(); await f.component.startWatching(); await f.component.simulate();
  assert.equal(f.stateCalls, calls); assert.equal(f.simulationCalls, 0); f.cleanup();
});
test('recreate reloads balance and starts a fresh session', async () => {
  const f = await fixture(); await f.component.openModal(); f.advance(900000); await f.tick();
  const oldCalls = f.infoCalls; f.setRemaining(50000); await f.component.load();
  assert.equal(f.infoCalls, oldCalls + 1); assert.equal(f.component.info.value.remainingAmount, 50000);
  assert.equal(f.component.expired.value, false); assert.equal(f.component.countdown.value, '15:00'); assert.equal(f.timers.size, 2); f.cleanup();
});
test('close, escape cleanup and unmount cancel timers; other method focuses cash', async () => {
  const f = await fixture(); await f.component.openModal(); f.component.chooseOtherMethod();
  assert.equal(f.timers.size, 0); assert.equal(f.focusCalls, 1); assert.equal(f.component.dialog.value.open, false);
  await f.component.openModal(); f.component.endSession(); assert.equal(f.timers.size, 0);
  await f.component.openModal(); f.cleanup(); assert.equal(f.timers.size, 0);
});
test('payment state arriving after expiry does not revive or close expired session', async () => {
  const f = await fixture(); await f.component.openModal(); const resolve = f.deferState();
  const inFlight = f.component.checkNow(); f.advance(900000); await f.tick();
  resolve({ remainingAmount: 0 }); await inFlight;
  assert.equal(f.component.expired.value, true); assert.equal(f.timers.size, 0); assert.equal(f.events.length, 0); f.cleanup();
});
