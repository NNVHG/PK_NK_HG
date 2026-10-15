import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const options = { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } };
const utility = { exports: {} };
new Function('module', 'exports', ts.transpileModule(await readFile(new URL('../src/utils/invoiceDiscount.ts', import.meta.url), 'utf8'), options).outputText)(utility, utility.exports);
const { calculateInvoiceDiscount: calculate, validDiscountValue } = utility.exports;
const invoice = (service = 1000000, medicine = 200000) => ({ id: 7, status: 1, paidAmount: 0, discountAmount: 0,
  totalAmount: service + medicine, items: [
    { itemType: 'Service', quantity: 1, unitPrice: service, totalAmount: service },
    { itemType: 'Medicine', quantity: 1, unitPrice: medicine, totalAmount: medicine },
  ] });
test('percentage preview excludes medicines and rounds half dong away from zero', () => {
  assert.equal(calculate(invoice(105, 200000), 'Percentage', '10'), 11);
  assert.equal(calculate(invoice(101, 200000), 'Percentage', '10'), 10);
  assert.equal(calculate(invoice(999, 200000), 'Percentage', '12.5'), 125);
});
test('preview uses integer arithmetic across large safe amounts', () => {
  assert.equal(calculate(invoice(8000000000000005, 0), 'Percentage', '10'), 800000000000001);
  assert.equal(calculate(invoice(100006, 0), 'Percentage', '25'), 25002);
});
test('unsafe money, invalid snapshots, unknown line types and excessive discount have no preview', () => {
  assert.equal(calculate(invoice(Number.MAX_SAFE_INTEGER + 1, 0), 'Percentage', 25), null);
  const stale = invoice(); stale.items[0].totalAmount++;
  assert.equal(calculate(stale, 'Percentage', 25), null);
  const unknown = invoice(); unknown.items[0].itemType = 'Other';
  assert.equal(calculate(unknown, 'Percentage', 25), null);
  assert.equal(calculate(invoice(), 'FixedAmount', '1200001'), null);
});
test('ratio accepts 2 decimals but VND only accepts whole positive amounts', () => {
  for (const [type, value] of [['Percentage', '100.01'], ['Percentage', '2.333'], ['Percentage', '-1'], ['FixedAmount', '1.5'], ['FixedAmount', '0']])
    assert.equal(validDiscountValue(type, value), false);
  assert.equal(validDiscountValue('Percentage', '12.50'), true);
  assert.equal(calculate(invoice(), 'FixedAmount', '1200000'), 1200000);
});

const { descriptor } = parse(await readFile(new URL('../src/components/InvoiceDiscountPanel.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'discount-test' }).content, options).outputText;
function fixture(role = 'RECEPTIONIST') {
  const props = { invoice: invoice(), role }, calls = [], saves = [], emitted = [];
  let pending, fail = false, cleanup;
  const module = { exports: {} };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch() {}, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => !!e.response },
    '@/utils/invoiceDiscount': utility.exports,
    '@/services/invoiceDiscounts': { invoiceDiscountApi: {
      presets: async () => [{ id: 4, code: 'SYN', name: 'Synthetic', discountType: 'Percentage', value: 25, isActive: true }],
      apply: async (...args) => { calls.push(args); if (fail) throw { response: { data: { message: 'Hóa đơn đã thay đổi' } } }; return pending; },
      savePreset: async (...args) => { saves.push(args); return { id: 8, ...args[1] }; },
    } },
  };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup(props, { expose() {}, emit: (...args) => emitted.push(args) });
  const ready = async () => { await c.loadPresets(); c.presetId.value = '4'; c.reason.value = '  Synthetic reason  '; c.confirmed.value = true; };
  return { c, props, calls, saves, emitted, ready, cleanup: () => cleanup(), fail: () => { fail = true; },
    defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; } };
}
test('preset application sends exact confirmed financial state and trimmed reason', async () => {
  const f = fixture(); await f.ready(); assert.equal(f.c.preview.value, 250000); await f.c.apply();
  assert.deepEqual(f.calls, [[7, { mode: 'Preset', presetId: 4, discountType: null, value: null, reason: 'Synthetic reason',
    expectedTotalAmount: 1200000, expectedCurrentDiscountAmount: 0, expectedDiscountAmount: 250000 }]]);
  assert.ok(f.emitted.some(e => e[0] === 'applied')); assert.equal(f.c.needsReload.value, true);
});
test('missing confirmation, unknown preset and blank reason cannot issue writes', async () => {
  const f = fixture(); await f.ready(); f.c.confirmed.value = false; await f.c.apply();
  f.c.confirmed.value = true; f.c.presetId.value = '999'; await f.c.apply();
  f.c.presetId.value = '4'; f.c.reason.value = ' '; await f.c.apply(); assert.equal(f.calls.length, 0);
});
test('readonly roles and invoices with a payment cannot edit discounts', async () => {
  for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT']) { const f = fixture(role); await f.ready(); await f.c.apply(); assert.equal(f.calls.length, 0); }
  for (const status of [0, 2, 3, 4]) { const f = fixture(); await f.ready(); f.props.invoice.status = status; await f.c.apply(); assert.equal(f.calls.length, 0); }
  const f = fixture(); await f.ready(); f.props.invoice.paidAmount = 1; await f.c.apply(); assert.equal(f.calls.length, 0);
});
test('cashier cannot use custom discount or exceed either limit', async () => {
  const f = fixture(); await f.ready(); f.c.mode.value = 'Custom'; f.c.customValue.value = '10'; await f.c.apply(); assert.equal(f.calls.length, 0);
  f.c.mode.value = 'Preset'; f.c.presets.value[0].value = 25.01; await f.c.apply(); assert.equal(f.calls.length, 0);
  f.c.presets.value[0].value = 25; f.props.invoice = invoice(2000004, 0); await f.c.apply(); assert.equal(f.calls.length, 0);
});
test('Admin custom discount can exceed cashier limits with a reason', async () => {
  const f = fixture('ADMIN'); await f.ready(); f.c.mode.value = 'Custom'; f.c.customType.value = 'Percentage'; f.c.customValue.value = '75';
  await f.c.apply(); assert.equal(f.calls[0][1].value, 75); assert.equal(f.calls[0][1].expectedDiscountAmount, 750000);
});
test('duplicate submit and result without a refreshed invoice cannot issue a second mutation', async () => {
  const f = fixture(); await f.ready(); const done = f.defer(); const first = f.c.apply(); await f.c.apply();
  assert.equal(f.calls.length, 1); done(); await first; f.c.confirmed.value = true; await f.c.apply(); assert.equal(f.calls.length, 1);
});
test('failure retains reason and requires refreshing invoice before retry', async () => {
  const f = fixture(); await f.ready(); f.fail(); await f.c.apply();
  assert.equal(f.c.error.value, 'Hóa đơn đã thay đổi'); assert.equal(f.c.reason.value, '  Synthetic reason  ');
  f.c.confirmed.value = true; await f.c.apply(); assert.equal(f.calls.length, 1); assert.equal(f.c.needsReload.value, true);
});
test('late response after unmount cannot report success or repopulate private form state', async () => {
  const f = fixture(); await f.ready(); const done = f.defer(); const pending = f.c.apply(); f.cleanup(); done(); await pending;
  assert.equal(f.c.reason.value, ''); assert.deepEqual(f.c.presets.value, []); assert.ok(!f.emitted.some(e => e[0] === 'applied'));
});
test('Admin can create preset while cashier cannot save configuration', async () => {
  const f = fixture('ADMIN'); await f.ready(); f.c.editor.value = { code: 'TEST', name: ' Synthetic ', discountType: 'Percentage', value: '12.50', isActive: true };
  await f.c.savePreset(); assert.equal(f.saves.length, 1); assert.equal(f.saves[0][1].value, 12.5); assert.equal(f.saves[0][1].name, 'Synthetic');
  assert.equal(f.c.confirmed.value, false); assert.equal(f.c.presetId.value, '');
  const g = fixture(); await g.ready(); g.c.editor.value = { ...f.saves[0][1], value: '12.5' }; await g.c.savePreset(); assert.equal(g.saves.length, 0);
});
test('removing a discount requires confirmation and quotes zero discount', async () => {
  const f = fixture(); await f.ready(); f.props.invoice.discountAmount = 10000; f.c.mode.value = 'Remove'; await f.c.apply();
  assert.equal(f.calls[0][1].expectedCurrentDiscountAmount, 10000); assert.equal(f.calls[0][1].expectedDiscountAmount, 0);
});
