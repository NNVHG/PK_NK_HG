import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const sfc = await readFile(new URL('../src/components/InvoiceManualBankTransfer.vue', import.meta.url), 'utf8');
const compiled = ts.transpileModule(compileScript(parse(sfc).descriptor, { id: 'manual-bank-test' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
async function fixture({ role = 'RECEPTIONIST', pending, remaining = 100, status = 1 } = {}) {
  let watcher, cleanup, failure, deferred, stateFailure = false;
  const calls = [], events = [], auth = { role, user: { userId: 9 } }, props = { invoiceId: 1 };
  const store = { pending: pending ? { '9:1': pending } : {}, put(k, r) { this.pending[k] = r; }, clear(k) { delete this.pending[k]; } };
  const current = { id: 1, invoiceCode: 'INV-20261022-0001', remainingAmount: remaining, status, paidAmount: 0 };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      watch: (_get, cb, options) => { watcher = cb; if (options?.immediate) cb(); }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => !!e?.response },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/stores/manualBankTransfers': { useManualBankTransfersStore: () => store },
    '@/services/bankTransfers': { bankTransfersApi: {
      state: async () => { if (stateFailure) throw Error('Synthetic outage'); return { ...current }; },
      confirmManual: async (id, request) => { calls.push({ id, request }); if (deferred) return deferred; if (failure) throw failure;
        return { id: 77, invoiceId: id, amount: request.amount, source: 'Manual' }; }
    } }
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => { assert.ok(name in imports, name); return imports[name]; }, module, module.exports);
  const c = module.exports.default.setup(props, { expose() {}, emit: (...event) => events.push(event) });
  await Promise.resolve(); await Promise.resolve();
  return { c, props, auth, store, calls, events, current, cleanup: () => cleanup(), reload: () => watcher(),
    fail: value => { failure = value; }, failState: () => { stateFailure = true; },
    defer: () => { let resolve; deferred = new Promise(done => { resolve = done; }); return resolve; } };
}
function fill(f) { f.c.amount.value = 60; f.c.reference.value = 'BANK001'; f.c.confirmed.value = true; }

test('real confirmation includes explicit consent, current balance, bank reference and request id', async () => {
  const f = await fixture(); fill(f); await f.c.receive();
  assert.equal(f.calls.length, 1); assert.equal(f.calls[0].id, 1);
  assert.deepEqual({ ...f.calls[0].request, requestId: 'id' }, { requestId: 'id', amount: 60, transactionReference: 'BANK001', expectedRemainingAmount: 100, confirmed: true });
  assert.match(f.calls[0].request.requestId, /^[0-9a-f-]{36}$/); assert.equal(f.events[0][0], 'paid'); assert.equal(f.c.pending.value, undefined);
});
test('consent is mandatory even if input values are valid', async () => {
  const f = await fixture(); fill(f); f.c.confirmed.value = false; await f.c.receive(); assert.equal(f.calls.length, 0);
});
test('invalid amounts and references never reach the API', async () => {
  for (const amount of [0, -1, 0.5, '', Number.MAX_SAFE_INTEGER + 1]) {
    const f = await fixture(); fill(f); f.c.amount.value = amount; await f.c.receive(); assert.equal(f.calls.length, 0);
  }
  for (const reference of ['short', 'BANK 001', ' BANK001', 'x'.repeat(21)]) {
    const f = await fixture(); fill(f); f.c.reference.value = reference; await f.c.receive(); assert.equal(f.calls.length, 0);
  }
});
test('amount above remaining is shown as excess for review', async () => {
  const f = await fixture(); fill(f); f.c.amount.value = 120; assert.equal(f.c.excess.value, 20);
});
test('network ambiguity retains exactly the original request across retries', async () => {
  const f = await fixture(); fill(f); f.fail(Error('Synthetic network fault')); await f.c.receive();
  const original = f.c.pending.value; assert.equal(f.c.confirmed.value, false); assert.equal(f.events.length, 0);
  f.c.amount.value = 99; f.c.reference.value = 'DIFFERENT'; f.c.confirmed.value = true; f.fail(null); await f.c.receive();
  assert.deepEqual(f.calls[1].request, original); assert.match(f.c.error.value, /^$/);
});
test('concurrency conflict retains key, requires renewed consent and never auto retries', async () => {
  const f = await fixture(); fill(f); f.fail({ response: { status: 409, data: { code: 'BIL_006' } } });
  await f.c.receive(); await f.c.receive(); assert.equal(f.calls.length, 1); assert.ok(f.c.pending.value); assert.equal(f.c.confirmed.value, false);
});
test('stale balance clears rejected request, reloads amount and requires consent again', async () => {
  const f = await fixture(); fill(f); f.current.remainingAmount = 40;
  f.fail({ response: { status: 409, data: { code: 'BIL_064', message: 'Balance changed' } } }); await f.c.receive();
  assert.equal(f.c.pending.value, undefined); assert.equal(f.c.state.value.remainingAmount, 40); assert.equal(f.c.confirmed.value, false);
  assert.equal(f.c.busy.value, false); assert.equal(f.events.length, 0);
});
test('reference conflicts and definitive rejection release only the rejected request', async () => {
  for (const status of [400, 401, 403, 404, 409]) {
    const f = await fixture(); fill(f); f.fail({ response: { status, data: { code: 'BIL_065' } } }); await f.c.receive();
    assert.equal(f.c.pending.value, undefined); assert.equal(f.c.confirmed.value, false);
  }
});
test('double click is suppressed; a late response after unmount cannot update the view', async () => {
  const f = await fixture(); fill(f); const resolve = f.defer(); const first = f.c.receive(); await f.c.receive();
  assert.equal(f.calls.length, 1); f.cleanup(); resolve({ id: 77 }); await first; assert.equal(f.events.length, 0);
});
test('restored uncertain request can be checked after invoice has become Paid', async () => {
  const pending = { requestId: crypto.randomUUID(), amount: 100, transactionReference: 'BANK001', expectedRemainingAmount: 100, confirmed: true };
  const f = await fixture({ pending, remaining: 0, status: 3 }); assert.equal(f.c.payable.value, false); f.c.confirmed.value = true;
  await f.c.receive(); assert.deepEqual(f.calls[0].request, pending); assert.equal(f.events.length, 1);
});
test('new receipt cannot be submitted on a settled invoice or without loaded state', async () => {
  const f = await fixture({ remaining: 0, status: 3 }); fill(f); await f.c.receive(); assert.equal(f.calls.length, 0);
  const g = await fixture(); g.failState(); await g.reload(); fill(g); await g.c.receive(); assert.equal(g.calls.length, 0);
});
test('patient, dentist and assistant do not load or confirm financial state', async () => {
  for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT', '']) {
    const f = await fixture({ role }); fill(f); await f.c.receive(); assert.equal(f.c.state.value, null); assert.equal(f.calls.length, 0);
  }
});
test('account and invoice changes do not expose another request', async () => {
  const f = await fixture(); fill(f); f.fail(Error('Synthetic outage')); await f.c.receive();
  f.auth.user.userId = 10; await f.reload(); assert.equal(f.c.pending.value, undefined); assert.equal(f.c.reference.value, '');
  f.props.invoiceId = 2; await f.reload(); assert.equal(f.c.pending.value, undefined);
});
test('native labels, consent, amount/reference limits and manual independence from QR TTL', () => {
  assert.match(sfc, /for="manual-bank-amount"/); assert.match(sfc, /for="manual-bank-reference"/);
  assert.match(sfc, /type="checkbox" required/); assert.match(sfc, /step="1" required/);
  assert.match(sfc, /phân biệt chữ hoa\/thường/); assert.doesNotMatch(sfc, /qrSession|simulationEnabled|console\.log/);
});

test('manual store restores only valid requests and persists/releases retry state', async () => {
  const source = await readFile(new URL('../src/stores/manualBankTransfers.ts', import.meta.url), 'utf8');
  const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
  const valid = { requestId: crypto.randomUUID(), amount: 100, expectedRemainingAmount: 100, confirmed: true, transactionReference: 'BANK001' };
  let saved = JSON.stringify({ '9:1': valid, bad: { ...valid, amount: 1.2 }, consent: { ...valid, confirmed: false } });
  const module = { exports: {} };
  new Function('require', 'module', 'exports', 'sessionStorage', code)(n => n === 'pinia' ? { defineStore: (_n, setup) => setup } : { ref: value => ({ value }) },
    module, module.exports, { getItem: () => saved, setItem: (_k, v) => { saved = v; } });
  const store = module.exports.useManualBankTransfersStore(); assert.deepEqual(Object.keys(store.pending.value), ['9:1']);
  store.put('9:2', valid); assert.ok(JSON.parse(saved)['9:2']); store.clear('9:1'); assert.equal(JSON.parse(saved)['9:1'], undefined);
});
