import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/InvoicesView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'invoice-lookup' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
const row = { invoiceId: 42, invoiceCode: 'INV-1', status: 1, totalAmount: 100, paidAmount: 0, remainingAmount: 100, patientName: 'Synthetic', patientCode: 'BN000001' };
function fixture(role = 'ADMIN') {
  let cleanup, error, response = row, pending; const reads = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => !!e.response }, '@/stores/auth': { useAuthStore: () => ({ role }) },
    '@/components/InvoiceCashPayment.vue': {}, '@/components/PaymentReceiptModal.vue': {},
    '@/services/invoiceLookup': { invoiceLookupApi: { get: async code => { reads.push(code); if (error) throw error; return pending ? await pending : { ...response }; } } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, cleanup: () => cleanup(), error: e => { error = e; }, row: r => { response = r; }, pending: p => { pending = p; },
    lookup: async () => { c.code.value = ' inv-1 '; await c.lookup(); } };
}
test('no request at mount; normalizes and retrieves exact invoice', async () => {
  const f = fixture(); assert.equal(f.reads.length, 0); await f.lookup(); assert.deepEqual(f.reads, ['INV-1']); assert.equal(f.c.invoice.value.invoiceId, 42);
});
test('invalid codes clear old patient result before any API call', async () => {
  for (const code of ['', 'INV_1', 'INV 1', 'HÓA-1', 'a'.repeat(51)]) { const f = fixture(); await f.lookup(); f.c.code.value = code; await f.c.lookup();
    assert.equal(f.reads.length, 1); assert.equal(f.c.invoice.value, null); assert.ok(f.c.error.value); }
});
test('code50 accepted', async () => { const f = fixture(); f.c.code.value = 'a'.repeat(50); await f.c.lookup(); assert.equal(f.reads[0].length, 50); });
test('unknown or failed lookup clears data and reports error', async () => {
  for (const error of [{ response: { status: 404 } }, new Error('network')]) { const f = fixture(); await f.lookup(); f.error(error); await f.lookup(); assert.equal(f.c.invoice.value, null); assert.ok(f.c.error.value); }
});
test('roles enforce no reads or payment opening', async () => {
  for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT']) { const f = fixture(role); await f.lookup(); f.c.invoice.value = row; f.c.confirmed.value = true; f.c.openPayment(); assert.equal(f.reads.length, 0); assert.equal(f.c.paymentOpen.value, false); }
});
test('confirmation required and cancelled/draft blocked; paid opens history', async () => {
  for (const status of [0, 1, 2, 3, 4]) { const f = fixture(); f.row({ ...row, status }); await f.lookup(); f.c.openPayment(); assert.equal(f.c.paymentOpen.value, false);
    f.c.confirmed.value = true; f.c.openPayment(); assert.equal(f.c.paymentOpen.value, [1, 2, 3].includes(status)); }
});
test('payment mode locks lookup and refreshes immutable original code', async () => {
  const f = fixture('RECEPTIONIST'); await f.lookup(); f.c.confirmed.value = true; f.c.openPayment(); f.c.code.value = 'OTHER'; await f.c.lookup();
  assert.equal(f.reads.length, 1); f.row({ ...row, paidAmount: 40, remainingAmount: 60, status: 2 });
  await f.c.onPaid({ id: 9, invoiceId: 42, amount: 40 }); assert.deepEqual(f.reads, ['INV-1', 'INV-1']); assert.equal(f.c.invoice.value.remainingAmount, 60); assert.equal(f.c.lastPayment.value.id, 9);
});
test('refresh failure or changed identity blocks additional new cash amount', async () => {
  for (const changed of [true, false]) { const f = fixture(); await f.lookup(); f.c.confirmed.value = true; f.c.openPayment();
    if (changed) f.row({ ...row, invoiceId: 99 }); else f.error(new Error('network'));
    await f.c.refresh(); assert.equal(f.c.invoice.value.invoiceId, 42); assert.equal(f.c.reviewRequired.value, true); assert.ok(f.c.error.value);
    f.error(null); f.row(row); await f.c.refresh(); assert.equal(f.c.reviewRequired.value, false); }
});
test('foreign payment cannot update receipt or trigger refresh', async () => {
  const f = fixture(); await f.lookup(); await f.c.onPaid({ invoiceId: 99 }); assert.equal(f.reads.length, 1); assert.equal(f.c.lastPayment.value, null);
});
test('duplicate read blocked and unmount ignores late private data', async () => {
  const f = fixture(); let resolve; f.pending(new Promise(done => { resolve = done; }));
  const first = f.lookup(); await f.c.lookup(); assert.equal(f.reads.length, 1); f.cleanup(); resolve(row); await first; assert.equal(f.c.invoice.value, null); assert.equal(f.c.code.value, '');
});

test('payment invalidates a stale manual refresh and reads debt again', async () => {
  const f = fixture(); await f.lookup(); f.c.confirmed.value = true; f.c.openPayment();
  let resolve; f.pending(new Promise(done => { resolve = done; }));
  const old = f.c.refresh(); f.pending(null); f.row({ ...row, remainingAmount: 60, paidAmount: 40 });
  await f.c.onPaid({ invoiceId: 42, id: 9, amount: 40 }); resolve(row); await old;
  assert.equal(f.c.invoice.value.remainingAmount, 60); assert.equal(f.c.lastPayment.value.id, 9); assert.equal(f.c.reviewRequired.value, false);
});

test('history selection re-reads exact invoice and is locked after opening cash', async () => {
  const f = fixture(); await f.c.selectFromHistory('INV-9'); assert.equal(f.reads[0], 'INV-9');
  f.c.confirmed.value = true; f.c.openPayment(); await f.c.selectFromHistory('OTHER'); assert.equal(f.reads.length, 1); assert.equal(f.c.code.value, 'INV-9');
});
