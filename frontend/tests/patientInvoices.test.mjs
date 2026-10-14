import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/PatientInvoicesView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'portal-invoices' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
const row = id => ({ invoiceId: id, invoiceCode: 'INV-' + id, status: 2, totalAmount: 100, paidAmount: 40, remainingAmount: 60 });
function fixture(role = 'PATIENT') {
  let mount, cleanup, identity, profileFailure, listFailure, paymentFailure, itemFailure, itemPending, listPending, paymentPending, profilesPending;
  const auth = { role, user: { userId: 7 } }; const calls = { profiles: 0, lists: [], payments: [], items: [] };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; }, watch: (_get, cb) => { identity = cb; } },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/patients': { patientsService: { getMyProfiles: async () => { calls.profiles++; if (profileFailure) throw new Error('profile'); return profilesPending ? await profilesPending : [{ patientId: 1, fullName: 'Synthetic A', patientCode: 'BN000001', phone: 'private' }, { patientId: 2, fullName: 'Synthetic B', patientCode: 'BN000002' }]; } } },
    '@/services/invoicePortal': { invoicePortalApi: {
      list: async (...args) => { calls.lists.push(args); if (listFailure) throw new Error('list'); return listPending ? await listPending : { items: [row(args[0] * 10), row(args[0] * 10 + 1)], page: args[1], totalCount: 30, totalPages: 2 }; },
      items: async (...args) => { calls.items.push(args); if (itemFailure) throw new Error('items'); return itemPending ? await itemPending : { items: [{ itemId: 3, code: 'SVC', name: 'Snapshot', quantity: 2, unitPrice: 50, totalAmount: 100 }], page: args[2], totalCount: 21, totalPages: 2 }; },
      payments: async (...args) => { calls.payments.push(args); if (paymentFailure) throw new Error('payments'); return paymentPending ? await paymentPending : { items: [{ paymentId: 5, invoiceId: args[1], amount: 40, paymentMethod: 'Cash', isSimulation: false }], page: args[2], totalCount: 21, totalPages: 2 }; }
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, auth, calls, mount: () => mount(), cleanup: () => cleanup(), identity: () => identity(),
    choose: async id => { c.patientId.value = id; await c.changeProfile(); },
    profileFail: v => { profileFailure = v; }, listFail: v => { listFailure = v; }, payFail: v => { paymentFailure = v; },
    itemFail: v => { itemFailure = v; }, itemPending: p => { itemPending = p; },
    listPending: p => { listPending = p; }, payPending: p => { paymentPending = p; }, profilesPending: p => { profilesPending = p; } };
}
test('mount loads only owned profile picker; financial list requires explicit profile', async () => {
  const f = fixture(); await f.mount(); assert.equal(f.calls.profiles, 1); assert.equal(f.calls.lists.length, 0); assert.equal(f.c.patientId.value, 0);
  assert.equal(f.c.profiles.value[0].phone, undefined); await f.choose(2); assert.deepEqual(f.calls.lists[0], [2, 1]);
});
test('unknown or missing profile never sends a broad query', async () => {
  const f = fixture(); await f.mount(); await f.choose(0); await f.choose(999); assert.equal(f.calls.lists.length, 0);
});
test('nonpatient roles never read profiles or financial data', async () => {
  for (const role of ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT']) { const f = fixture(role); await f.mount(); await f.choose(1);
    assert.equal(f.calls.profiles, 0); assert.equal(f.calls.lists.length, 0); assert.equal(f.calls.payments.length, 0); }
});
test('profile failure does not permit financial reads and can recover', async () => {
  const f = fixture(); f.profileFail(true); await f.mount(); await f.choose(1); assert.equal(f.calls.lists.length, 0); assert.ok(f.c.profileError.value);
  f.profileFail(false); await f.c.loadProfiles(); await f.choose(1); assert.equal(f.calls.lists.length, 1);
});
test('pagination always includes explicit profile and guards invalid pages', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.loadInvoices(2); assert.deepEqual(f.calls.lists.at(-1), [1, 2]);
  for (const n of [0, -1, 1.5, 1000001, 3]) await f.c.loadInvoices(n); assert.equal(f.calls.lists.length, 2);
});
test('selecting only a current row reads own profile and exact invoice payments', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(999); assert.equal(f.calls.payments.length, 0);
  await f.c.selectInvoice(10); await f.c.loadPayments(2); assert.deepEqual(f.calls.payments, [[1, 10, 1], [1, 10, 2]]);
});
test('profile switch clears selected invoice and ignores late transaction response', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); let resolve; f.payPending(new Promise(done => { resolve = done; }));
  const old = f.c.selectInvoice(10); await f.choose(2); resolve({ items: [{ paymentId: 99 }], page: 1, totalCount: 1, totalPages: 1 }); await old;
  assert.equal(f.c.selected.value, null); assert.deepEqual(f.c.payments.value, []); assert.equal(f.c.invoices.value[0].invoiceId, 20);
});
test('changing selected invoice ignores older payment response', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); let resolve; f.payPending(new Promise(done => { resolve = done; }));
  const old = f.c.selectInvoice(10); f.payPending(null); await f.c.selectInvoice(11);
  resolve({ items: [{ paymentId: 99 }], page: 1, totalCount: 1, totalPages: 1 }); await old;
  assert.equal(f.c.selected.value.invoiceId, 11); assert.equal(f.c.payments.value[0].invoiceId, 11);
});
test('duplicate list or payment requests blocked', async () => {
  const f = fixture(); await f.mount(); let resolve; f.listPending(new Promise(done => { resolve = done; }));
  const first = f.choose(1); await f.c.loadInvoices(1); assert.equal(f.calls.lists.length, 1);
  resolve({ items: [row(10)], page: 1, totalPages: 1, totalCount: 1 }); await first;
  let resolvePay; f.payPending(new Promise(done => { resolvePay = done; })); const payment = f.c.selectInvoice(10); await f.c.selectInvoice(10);
  assert.equal(f.calls.payments.length, 1); resolvePay({ items: [], page: 1, totalPages: 0, totalCount: 0 }); await payment;
});
test('failed list clears private rows and payments before retry', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); f.listFail(true); await f.c.loadInvoices(1);
  assert.deepEqual(f.c.invoices.value, []); assert.deepEqual(f.c.payments.value, []); assert.equal(f.c.selected.value, null); assert.ok(f.c.invoiceError.value);
  f.listFail(false); await f.c.loadInvoices(1); assert.equal(f.c.invoiceError.value, '');
});
test('failed payment read clears older transactions and can retry', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); f.payFail(true); await f.c.loadPayments(1);
  assert.deepEqual(f.c.payments.value, []); assert.ok(f.c.paymentError.value); f.payFail(false); await f.c.loadPayments(1); assert.equal(f.c.paymentError.value, '');
});
test('identity change clears all financial data and requires choosing profile again', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); f.auth.user.userId = 8; await f.identity();
  assert.equal(f.c.patientId.value, 0); assert.deepEqual(f.c.invoices.value, []); assert.deepEqual(f.c.payments.value, []); assert.equal(f.c.selected.value, null);
});
test('unmount ignores late invoice and profile responses', async () => {
  const f = fixture(); await f.mount(); let resolve; f.listPending(new Promise(done => { resolve = done; })); const first = f.choose(1); f.cleanup();
  resolve({ items: [row(10)], page: 1, totalPages: 1, totalCount: 1 }); await first;
  assert.deepEqual(f.c.invoices.value, []); assert.deepEqual(f.c.profiles.value, []); assert.equal(f.c.patientId.value, 0);
  const other = fixture(); let finish; other.profilesPending(new Promise(done => { finish = done; })); const mounting = other.mount(); other.cleanup();
  finish([{ patientId: 1 }]); await mounting; assert.deepEqual(other.c.profiles.value, []);
});


test('invoice selection loads scoped snapshot items with independent pagination', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); await f.c.loadItems(2);
  assert.deepEqual(f.calls.items, [[1, 10, 1], [1, 10, 2]]); assert.equal(f.c.items.value[0].totalAmount, 100);
  assert.equal(f.c.paymentPage.value, 1); assert.equal(f.c.itemPage.value, 2);
  for (const n of [0, -1, 1.5, 1000001, 3]) await f.c.loadItems(n); assert.equal(f.calls.items.length, 2);
});
test('unknown profile or invoice and nonpatient roles cannot read items', async () => {
  const f = fixture(); await f.mount(); await f.c.loadItems(); await f.choose(999); await f.c.selectInvoice(10); assert.equal(f.calls.items.length, 0);
  await f.choose(1); await f.c.selectInvoice(999); assert.equal(f.calls.items.length, 0);
  for (const role of ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT']) { const other = fixture(role); await other.mount(); await other.choose(1); await other.c.selectInvoice(10); assert.equal(other.calls.items.length, 0); }
});
test('item error clears snapshots and retries without losing payment history', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); f.itemFail(true); await f.c.loadItems();
  assert.deepEqual(f.c.items.value, []); assert.equal(f.c.itemTotal.value, 0); assert.ok(f.c.itemError.value); assert.equal(f.c.payments.value.length, 1);
  f.itemFail(false); await f.c.loadItems(); assert.equal(f.c.itemError.value, ''); assert.equal(f.c.items.value.length, 1);
});
test('item request duplicates and repeat same selection blocked', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); let finish; f.itemPending(new Promise(resolve => { finish = resolve; }));
  const loading = f.c.selectInvoice(10); await f.c.loadItems(); await f.c.selectInvoice(10); assert.equal(f.calls.items.length, 1);
  finish({ items: [], page: 1, totalCount: 0, totalPages: 0 }); await loading; assert.equal(f.c.itemsLoaded.value, true);
});
test('profile switch ignores late items including late errors', async () => {
  for (const fail of [false, true]) {
    const f = fixture(); await f.mount(); await f.choose(1); let finish, reject; f.itemPending(new Promise((resolve, no) => { finish = resolve; reject = no; }));
    const loading = f.c.selectInvoice(10); await f.choose(2);
    if (fail) reject(new Error('private old response')); else finish({ items: [{ itemId: 99 }], page: 1, totalCount: 1, totalPages: 1 });
    await loading; assert.deepEqual(f.c.items.value, []); assert.equal(f.c.itemError.value, ''); assert.equal(f.c.selected.value, null);
  }
});
test('different invoice selection ignores stale item page', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); let finish; f.itemPending(new Promise(resolve => { finish = resolve; }));
  const loading = f.c.selectInvoice(10); f.itemPending(null); await f.c.selectInvoice(11);
  finish({ items: [{ itemId: 99 }], page: 1, totalCount: 1, totalPages: 1 }); await loading;
  assert.equal(f.c.selected.value.invoiceId, 11); assert.equal(f.c.items.value[0].itemId, 3);
});
test('identity reload and unmount discard late item responses', async () => {
  for (const end of ['identity', 'unmount']) {
    const f = fixture(); await f.mount(); await f.choose(1); let finish; f.itemPending(new Promise(resolve => { finish = resolve; }));
    const loading = f.c.selectInvoice(10);
    if (end === 'identity') { f.auth.user.userId = 8; await f.identity(); } else f.cleanup();
    finish({ items: [{ itemId: 99 }], page: 1, totalCount: 1, totalPages: 1 }); await loading;
    assert.deepEqual(f.c.items.value, []); assert.equal(f.c.itemTotal.value, 0); assert.equal(f.c.itemsBusy.value, false);
  }
});
test('invoice reload clears all item state and only successful selection reloads it', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); await f.c.loadItems(2);
  f.listFail(true); await f.c.loadInvoices(); assert.deepEqual(f.c.items.value, []); assert.equal(f.c.itemPage.value, 1);
  assert.equal(f.c.itemsLoaded.value, false); assert.equal(f.c.selected.value, null);
  await f.c.loadItems(); assert.equal(f.calls.items.length, 2);
});
