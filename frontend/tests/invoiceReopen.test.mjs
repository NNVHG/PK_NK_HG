import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/InvoiceDraftView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'reopen-invoice' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
const cause = (status, message) => ({ response: { status, data: { message } } });
function fixture(role = 'ADMIN') {
  let cleanup, visitError, invoiceError, historyError, unlockError, unlockPending, visitPending, invoicePending;
  let details = { visitId: 1, status: 'Completed', isLocked: true, dentistId: 9 };
  let data = { id: 5, invoiceCode: 'INV-5', status: 1, paidAmount: 0, remainingAmount: 100, totalAmount: 100, items: [] };
  const watchers = [], auth = { role, user: { userId: 7 } }, route = { params: { visitId: '1' } };
  const invoice = { value: null }, store = { replace: v => { invoice.value = v; } }, calls = { visits: [], invoices: [], history: [], unlocks: [] };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      onUnmounted: cb => { cleanup = cb; }, watch: (_value, cb) => { watchers.push(cb); } },
    'vue-router': { useRoute: () => route }, pinia: { storeToRefs: () => ({ invoice }) },
    axios: { isAxiosError: e => !!e?.response },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/invoiceDraft': { useInvoiceDraftStore: () => store },
    '@/services/visits': { visitsService: { getById: async id => { calls.visits.push(id); if (visitError) throw visitError; return visitPending ? await visitPending : { ...details }; } } },
    '@/services/invoiceDraft': { invoiceDraftApi: {
      get: async id => { calls.invoices.push(id); if (invoiceError) throw invoiceError; return invoicePending ? await invoicePending : { ...data }; },
      unlockHistory: async id => { calls.history.push(id); if (historyError) throw historyError; return []; },
      unlock: async (...args) => { calls.unlocks.push(args); if (unlockPending) await unlockPending; if (unlockError) throw unlockError;
        details = { ...details, status: 'Correcting', isLocked: false }; data = { ...data, id: 6, invoiceCode: 'INV-6', status: 0 }; },
      complete: async () => {}
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name] ?? {}, module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, auth, route, invoice, cleanup: () => cleanup(), identity: () => watchers[1](), routeChanged: () => watchers[0](),
    visitFail: e => { visitError = e; }, invoiceFail: e => { invoiceError = e; }, historyFail: e => { historyError = e; },
    unlockFail: e => { unlockError = e; }, unlockPending: p => { unlockPending = p; }, visitPending: p => { visitPending = p; },
    invoicePending: p => { invoicePending = p; }, data: v => { data = { ...data, ...v }; } };
}
test('reopen only available after complete fresh reads and requires trimmed reason', async () => {
  const f = fixture(); f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock(); assert.equal(f.calls.unlocks.length, 0);
  await f.c.load(); assert.equal(f.c.ready.value, true); assert.equal(f.c.canUnlock.value, true);
  f.c.unlockReason.value = '  Synthetic correction  '; await f.c.unlock();
  assert.deepEqual(f.calls.unlocks, [[1, 'Synthetic correction']]); assert.match(f.c.success.value, /INV-5/);
  assert.equal(f.c.visit.value.status, 'Correcting'); assert.equal(f.c.canUnlock.value, false);
});
test('nonadmin cannot reopen or read protected unlock history', async () => {
  for (const role of ['DENTIST', 'RECEPTIONIST', 'PATIENT', 'ASSISTANT']) {
    const f = fixture(role); await f.c.load(); f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock();
    assert.equal(f.calls.unlocks.length, 0); assert.equal(f.calls.history.length, 0);
  }
});
test('reason boundaries and paid invoice prevent writes', async () => {
  const f = fixture(); await f.c.load();
  for (const reason of ['', '  short  ', 'A'.repeat(501)]) { f.c.unlockReason.value = reason; await f.c.unlock(); }
  assert.equal(f.calls.unlocks.length, 0); f.data({ status: 2, paidAmount: 40 }); await f.c.load();
  f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock(); assert.equal(f.calls.unlocks.length, 0);
});
test('conflict response retains reason after successful refresh for explicit retry', async () => {
  const f = fixture(); await f.c.load(); f.unlockFail(cause(409, 'Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại.'));
  f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock();
  assert.match(f.c.error.value, /thao tác đồng thời/); assert.equal(f.c.unlockReason.value, 'Synthetic correction'); assert.equal(f.c.ready.value, true);
  assert.equal(f.c.visit.value.status, 'Completed'); f.unlockFail(null); await f.c.unlock(); assert.equal(f.calls.unlocks.length, 2);
});
test('failed refresh after unlock error clears invoice and requires fresh GET before retry', async () => {
  const f = fixture(); await f.c.load(); f.unlockFail(cause(409, 'Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại.')); f.invoiceFail(cause(500, 'Không tải được hóa đơn'));
  f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock();
  assert.equal(f.c.ready.value, false); assert.equal(f.invoice.value, null); assert.equal(f.c.visit.value, null);
  assert.match(f.c.error.value, /thao tác đồng thời/);
  await f.c.unlock(); assert.equal(f.calls.unlocks.length, 1);
  f.invoiceFail(null); await f.c.load(); assert.equal(f.c.ready.value, true);
});
test('history load failure clears financial snapshot and never unlocks stale visit', async () => {
  const f = fixture(); await f.c.load(); f.historyFail(cause(500, 'History failed')); await f.c.load();
  f.c.unlockReason.value = 'Synthetic correction'; await f.c.unlock();
  assert.equal(f.c.ready.value, false); assert.equal(f.invoice.value, null); assert.equal(f.calls.unlocks.length, 0);
});
test('duplicate unlock requests blocked while one is pending', async () => {
  const f = fixture(); await f.c.load(); let finish; f.unlockPending(new Promise(resolve => { finish = resolve; }));
  f.c.unlockReason.value = 'Synthetic correction'; const first = f.c.unlock(); await f.c.unlock();
  assert.equal(f.calls.unlocks.length, 1); finish(); await first;
});
test('route change prevents late successful unlock from refreshing a new visit', async () => {
  const f = fixture(); await f.c.load(); let finish; f.unlockPending(new Promise(resolve => { finish = resolve; }));
  f.c.unlockReason.value = 'Synthetic correction'; const old = f.c.unlock();
  f.route.params.visitId = '2'; await f.routeChanged(); const reads = f.calls.visits.length; finish(); await old;
  assert.equal(f.calls.visits.length, reads); assert.equal(f.c.success.value, ''); assert.equal(f.c.unlockReason.value, '');
});
test('identity changes invalidate in-flight invoice and clear private state', async () => {
  const f = fixture(); await f.c.load(); let finish; f.invoicePending(new Promise(resolve => { finish = resolve; }));
  const old = f.c.load(); await new Promise(resolve => setImmediate(resolve));
  f.invoicePending(null); f.auth.role = 'PATIENT'; f.auth.user.userId = 8; f.visitFail(cause(403, 'Denied')); await f.identity();
  finish({ id: 99, status: 1 }); await old;
  assert.equal(f.invoice.value, null); assert.equal(f.c.visit.value, null); assert.equal(f.c.ready.value, false);
});
test('unmount clears reason history and ignores late invoice response', async () => {
  const f = fixture(); await f.c.load(); let finish; f.invoicePending(new Promise(resolve => { finish = resolve; }));
  const old = f.c.load(); await new Promise(resolve => setImmediate(resolve)); f.c.unlockReason.value = 'Synthetic correction'; f.cleanup();
  finish({ id: 99, status: 1 }); await old; assert.equal(f.invoice.value, null); assert.equal(f.c.unlockReason.value, '');
  assert.equal(f.c.ready.value, false); assert.deepEqual(f.c.unlockHistory.value, []);
});
test('legacy no-invoice404 is a verified readable state, other errors block reopen', async () => {
  const f = fixture(); f.invoiceFail(cause(404, 'No invoice')); await f.c.load(); assert.equal(f.c.ready.value, true); assert.equal(f.c.canUnlock.value, true);
  f.invoiceFail(cause(403, 'Denied')); await f.c.load(); assert.equal(f.c.canUnlock.value, false); assert.equal(f.c.ready.value, false);
});
