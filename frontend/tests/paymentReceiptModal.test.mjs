import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/components/PaymentReceiptModal.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'receipt-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, esModuleInterop: true },
}).outputText;
function fixture() {
  let previews = 0, printRequests = 0, printed = 0, cleanup, onChange, fail = false, deferred;
  const classes = new Set(), listeners = new Map();
  const dto = number => ({ printCount: number, printNumber: Math.max(1, number), invoiceId: 1, paymentId: 2 });
  const api = { preview: async () => { previews++; return dto(printRequests); },
    print: async () => { printRequests++; if (fail) throw Error('test'); return deferred ?? dto(printRequests); } };
  const imports = {
    vue: { defineComponent: value => value, ref: value => ({ value }), computed: getter => ({ get value() { return getter(); } }), nextTick: async () => {},
      watch: (_getter, callback) => { onChange = callback; }, onUnmounted: callback => { cleanup = callback; } },
    '@/services/paymentReceipts': { paymentReceiptsApi: api },
    '@/services/receiptBarcode': { MOD_BIL_receiptBarcode: () => null },
  };
  const window = { print: () => { printed++; }, addEventListener: (name, callback) => listeners.set(name, callback),
    removeEventListener: name => listeners.delete(name) };
  const document = { body: { classList: { add: name => classes.add(name), remove: name => classes.delete(name) } } };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', 'window', 'document', compiled)(name => imports[name], module, module.exports, window, document);
  const component = module.exports.default.setup({ invoiceId: 1, paymentId: 2 }, { expose() {} });
  component.dialog.value = { open: false, showModal() { this.open = true; }, close() { this.open = false; } };
  return { component, classes, listeners, afterPrint: () => listeners.get('afterprint')?.(), cleanup: () => cleanup(), change: () => onChange(),
    failPrint: () => { fail = true; }, defer: () => { let resolve; deferred = new Promise(done => { resolve = done; }); return resolve; },
    get previews() { return previews; }, get printRequests() { return printRequests; }, get printed() { return printed; } };
}
test('preview does not call print endpoint or browser printer', async () => {
  const f = fixture(); await f.component.open(); assert.equal(f.previews, 1); assert.equal(f.printRequests, 0); assert.equal(f.printed, 0); f.cleanup();
});
test('print uses committed server ordinal and cleans CSS after browser print', async () => {
  const f = fixture(); await f.component.open(); await f.component.print();
  assert.equal(f.component.receipt.value.printNumber, 1); assert.equal(f.printRequests, 1); assert.equal(f.printed, 1);
  assert.ok(f.classes.has('receipt-printing')); f.afterPrint(); assert.equal(f.classes.size, 0); assert.equal(f.listeners.size, 0);
  await f.component.print(); assert.equal(f.component.receipt.value.printNumber, 2); f.cleanup();
});
test('unknown print result requires preview reload before another print', async () => {
  const f = fixture(); await f.component.open(); f.failPrint(); await f.component.print();
  assert.equal(f.component.receipt.value, null); assert.ok(f.component.error.value); assert.equal(f.printed, 0); assert.equal(f.classes.size, 0);
  await f.component.print(); assert.equal(f.printRequests, 1); f.cleanup();
});
test('double click does not send simultaneous print requests', async () => {
  const f = fixture(); await f.component.open(); const resolve = f.defer(); const pending = f.component.print();
  await f.component.print(); assert.equal(f.printRequests, 1); resolve({ printNumber: 1, printCount: 1 }); await pending; f.cleanup();
});
test('late print response after closing cannot open browser print', async () => {
  const f = fixture(); await f.component.open(); const resolve = f.defer(); const pending = f.component.print();
  f.component.close(); resolve({ printNumber: 1, printCount: 1 }); await pending;
  assert.equal(f.printed, 0); assert.equal(f.classes.size, 0); f.cleanup();
});
test('invoice/payment change closes modal and removes previous receipt', async () => {
  const f = fixture(); await f.component.open(); await f.component.print(); f.change();
  assert.equal(f.component.receipt.value, null); assert.equal(f.component.dialog.value.open, false); assert.equal(f.classes.size, 0); f.cleanup();
});
test('unmount removes print class and event listener', async () => {
  const f = fixture(); await f.component.open(); await f.component.print(); f.cleanup();
  assert.equal(f.classes.size, 0); assert.equal(f.listeners.size, 0);
});
