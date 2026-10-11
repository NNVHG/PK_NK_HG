import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/InvoiceCashPayment.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'cash-conflict-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
function fixture() {
  let reload, cleanup, failure = { response: { status: 409, data: { code: 'BIL_006', message: 'Synthetic conflict' } } }, wait;
  const calls = [], events = [], store = { pending: {}, put(key, value) { this.pending[key] = value; }, clear(key) { delete this.pending[key]; } };
  const props = { invoiceId: 1, remainingAmount: 100, status: 1 };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      watch: (_get, cb) => { reload = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/stores/auth': { useAuthStore: () => ({ user: { userId: 9 }, role: 'RECEPTIONIST' }) },
    '@/stores/cashPayments': { useCashPaymentsStore: () => store },
    '@/services/cashPayments': { cashPaymentsApi: { history: async () => [], receive: async (_id, request) => {
      calls.push(request); if (wait) return wait; if (failure) throw failure; return { id: 7, requestId: request.requestId };
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup(props, { expose() {}, emit: (...event) => events.push(event) });
  return { c, store, calls, events, reload: () => reload(), cleanup: () => cleanup(), success: () => { failure = null; },
    fail: value => { failure = value; }, defer: () => { let resolve; wait = new Promise(done => { resolve = done; }); return resolve; } };
}
async function ready(f) { await f.reload(); f.c.amount.value = 100; f.c.tendered.value = 120; f.c.confirmed.value = true; }
test('BIL_006 retains exact pending request and requires confirmation again', async () => {
  const f = fixture(); await ready(f); await f.c.receive();
  assert.equal(f.c.pending.value.requestId, f.calls[0].requestId); assert.equal(f.c.confirmed.value, false);
  assert.ok(f.c.error.value.includes('không nhận thêm tiền')); assert.equal(f.events.length, 0);
});
test('no automatic retry; explicit retry uses same amounts and request ID', async () => {
  const f = fixture(); await ready(f); await f.c.receive(); await f.c.receive(); assert.equal(f.calls.length, 1);
  f.success(); f.c.confirmed.value = true; await f.c.receive();
  assert.deepEqual(f.calls[0], f.calls[1]); assert.equal(f.c.pending.value, undefined); assert.equal(f.events.length, 1);
});
test('double click cannot create second payment request', async () => {
  const f = fixture(); await ready(f); const resolve = f.defer(); const first = f.c.receive(); await f.c.receive();
  assert.equal(f.calls.length, 1); resolve({ id: 7 }); await first;
});
test('unknown network result retains request for reconciliation', async () => {
  const f = fixture(); f.fail(new Error('Synthetic network fault')); await ready(f); await f.c.receive();
  assert.ok(f.c.pending.value); assert.equal(f.events.length, 0);
});
test('definitive validation or permission errors clear pending request', async () => {
  for (const status of [400, 403, 404]) { const f = fixture(); f.fail({ response: { status, data: { message: 'Synthetic rejection' } } });
    await ready(f); await f.c.receive(); assert.equal(f.c.pending.value, undefined); assert.equal(f.c.error.value, 'Synthetic rejection'); }
});
test('unmount ignores late successful response', async () => {
  const f = fixture(); await ready(f); const resolve = f.defer(); const first = f.c.receive(); f.cleanup(); resolve({ id: 7 }); await first;
  assert.equal(f.events.length, 0);
});
