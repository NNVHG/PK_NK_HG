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
    '@/services/invoicePortal': { invoicePortalApi: {
      profiles: async () => { calls.profiles++; if (profileFailure) throw new Error('profile'); return profilesPending ? await profilesPending : [{ patientId: 1, fullName: 'Synthetic A', patientCode: 'BN000001', dateOfBirth: '2000-01-02', isActive: true, phone: 'private' }, { patientId: 2, fullName: 'Synthetic B', patientCode: 'BN000002', dateOfBirth: '1950-12-31', isActive: false }]; },
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
  assert.equal(f.c.profiles.value[0].phone, undefined); await f.choose(2); assert.deepEqual(f.calls.lists[0], [2, 1, {}]);
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
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.loadInvoices(2); assert.deepEqual(f.calls.lists.at(-1), [1, 2, {}]);
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


test('inactive owned family profile is selectable for invoice items and payment history', async () => {
  const f = fixture(); await f.mount();
  assert.equal(f.c.profiles.value[1].isActive, false); assert.equal(f.c.profiles.value[1].dateOfBirth, '1950-12-31');
  assert.equal(f.c.birth('1950-12-31'), '31/12/1950'); assert.equal(f.calls.lists.length, 0);
  await f.choose(2); await f.c.selectInvoice(20);
  assert.deepEqual(f.calls.lists, [[2, 1, {}]]); assert.deepEqual(f.calls.items, [[2, 20, 1]]); assert.deepEqual(f.calls.payments, [[2, 20, 1]]);
});
test('billing picker retains exactly five fields and never chooses first profile automatically', async () => {
  const f = fixture(); await f.mount(); assert.equal(f.c.patientId.value, 0);
  assert.deepEqual(Object.keys(f.c.profiles.value[0]).sort(), ['patientId', 'patientCode', 'fullName', 'dateOfBirth', 'isActive'].sort());
  assert.equal(f.calls.items.length + f.calls.payments.length + f.calls.lists.length, 0);
});
test('empty billing profiles keep financial controls invalid', async () => {
  const f = fixture(); f.profilesPending(Promise.resolve([])); await f.mount(); await f.choose(1);
  await f.c.loadItems(); await f.c.loadPayments(); assert.equal(f.c.validProfile.value, false);
  assert.equal(f.calls.lists.length + f.calls.items.length + f.calls.payments.length, 0);
});
test('picker reload removes selected inactive financial data and ignores older profile result', async () => {
  const f = fixture(); await f.mount(); await f.choose(2); await f.c.selectInvoice(20);
  let resolve; f.profilesPending(new Promise(done => { resolve = done; })); const old = f.c.loadProfiles();
  assert.deepEqual(f.c.invoices.value, []); assert.deepEqual(f.c.items.value, []); assert.deepEqual(f.c.payments.value, []);
  assert.equal(f.c.selected.value, null); assert.equal(f.c.patientId.value, 0);
  f.profilesPending(Promise.resolve([{ patientId: 3, patientCode: 'BN000003', fullName: 'Synthetic C', dateOfBirth: '2001-01-01', isActive: false }]));
  await f.c.loadProfiles(); resolve([{ patientId: 99, patientCode: 'BN000099', fullName: 'Stale', dateOfBirth: '2000-01-01', isActive: true }]); await old;
  assert.equal(f.c.profiles.value.length, 1); assert.equal(f.c.profiles.value[0].patientId, 3); assert.equal(f.c.patientId.value, 0);
});
test('profile no longer owned after refresh cannot be queried using old selector', async () => {
  const f = fixture(); await f.mount(); await f.choose(2);
  f.profilesPending(Promise.resolve([{ patientId: 1, patientCode: 'BN000001', fullName: 'Synthetic A', dateOfBirth: '2000-01-02', isActive: true }]));
  await f.c.loadProfiles(); await f.choose(2); assert.equal(f.calls.lists.length, 1); assert.equal(f.c.validProfile.value, false);
});
test('billing profile API has no client supplied owner scope', async () => {
  const source = await readFile(new URL('../src/services/invoicePortal.ts', import.meta.url), 'utf8');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const requests = [], rows = [{ patientId: 2, isActive: false }];
  const module = { exports: {} };
  new Function('require', 'module', 'exports', js)(() => ({ apiClient: { get: async (...args) => { requests.push(args); return { data: rows }; } } }), module, module.exports);
  assert.equal(await module.exports.invoicePortalApi.profiles(), rows); assert.deepEqual(requests, [['/portal/invoices/profiles']]);
});


test('filters reset page and clear selected items payments before new list', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); await f.c.loadInvoices(2);
  f.c.status.value = '2'; f.c.fromDate.value = '2026-10-14'; f.c.toDate.value = '2026-10-14'; await f.c.searchInvoices();
  assert.deepEqual(f.calls.lists.at(-1), [1, 1, { status: 2, fromDate: '2026-10-14', toDate: '2026-10-14' }]);
  assert.equal(f.c.invoicePage.value, 1); assert.equal(f.c.selected.value, null); assert.deepEqual(f.c.items.value, []); assert.deepEqual(f.c.payments.value, []);
});
test('pagination and reload retain applied filters while draft changes await submit', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); f.c.status.value = '0'; f.c.fromDate.value = '2024-02-29'; await f.c.searchInvoices();
  f.c.status.value = '4'; f.c.fromDate.value = '2026-10-15'; await f.c.loadInvoices(2); await f.c.loadInvoices(1);
  assert.deepEqual(f.calls.lists.at(-2), [1, 2, { status: 0, fromDate: '2024-02-29' }]); assert.deepEqual(f.calls.lists.at(-1), [1, 1, { status: 0, fromDate: '2024-02-29' }]);
});
test('reset filters drops status including zero and both dates', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); f.c.status.value = '0'; f.c.toDate.value = '2026-10-14'; await f.c.searchInvoices(); await f.c.resetInvoiceFilters();
  assert.deepEqual(f.calls.lists.at(-1), [1, 1, {}]); assert.deepEqual(f.c.applied.value, {}); assert.equal(f.c.status.value, ''); assert.equal(f.c.toDate.value, '');
});
test('invalid status calendar order and date overflow never request financial list', async () => {
  for (const [status, from, to] of [['5', '', ''], ['-1', '', ''], ['', '2026-02-29', ''], ['', '0001-01-01', ''], ['', '', '9999-12-31'], ['', '2026-10-15', '2026-10-14'], ['', 'bad', ''], ['', '0000-01-01', '']]) {
    const f = fixture(); await f.mount(); await f.choose(1); await f.c.selectInvoice(10); const count = f.calls.lists.length;
    f.c.status.value = status; f.c.fromDate.value = from; f.c.toDate.value = to; await f.c.searchInvoices();
    assert.equal(f.calls.lists.length, count); assert.ok(f.c.invoiceError.value); assert.equal(f.c.selected.value, null); assert.deepEqual(f.c.invoices.value, []);
  }
});
test('profile change resets applied and draft filters then scopes empty query to new inactive profile', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); f.c.status.value = '3'; f.c.toDate.value = '2026-10-14'; await f.c.searchInvoices(); await f.choose(2);
  assert.deepEqual(f.calls.lists.at(-1), [2, 1, {}]); assert.deepEqual(f.c.applied.value, {}); assert.equal(f.c.status.value, ''); assert.equal(f.c.toDate.value, '');
});
test('filter search errors preserve draft/applied for retry but clear rows', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); f.c.fromDate.value = '2026-10-14'; f.listFail(true); await f.c.searchInvoices();
  assert.equal(f.c.fromDate.value, '2026-10-14'); assert.deepEqual(f.c.invoices.value, []); assert.ok(f.c.invoiceError.value);
  f.listFail(false); await f.c.loadInvoices(); assert.deepEqual(f.calls.lists.at(-1), [1, 1, { fromDate: '2026-10-14' }]);
});
test('pending filtered list prevents duplicate submits reset and ignores response after identity change', async () => {
  const f = fixture(); await f.mount(); await f.choose(1); let finish; f.listPending(new Promise(resolve => { finish = resolve; }));
  f.c.status.value = '2'; const pending = f.c.searchInvoices(); const count = f.calls.lists.length; await f.c.searchInvoices(); await f.c.resetInvoiceFilters();
  assert.equal(f.calls.lists.length, count); f.auth.user.userId = 8; await f.identity();
  finish({ items: [row(99)], page: 1, totalCount: 1, totalPages: 1 }); await pending;
  assert.deepEqual(f.c.invoices.value, []); assert.deepEqual(f.c.applied.value, {}); assert.equal(f.c.status.value, '');
});
test('invalid profile nonpatient or unmounted filters cannot query and all private filter state clears', async () => {
  const f = fixture(); await f.mount(); f.c.status.value = '3'; await f.c.searchInvoices(); await f.c.resetInvoiceFilters(); assert.equal(f.calls.lists.length, 0);
  await f.choose(1); f.c.toDate.value = '2026-10-14'; await f.c.searchInvoices(); f.cleanup();
  assert.deepEqual(f.c.applied.value, {}); assert.equal(f.c.toDate.value, ''); await f.c.searchInvoices(); assert.equal(f.calls.lists.length, 2);
  const other = fixture('RECEPTIONIST'); await other.mount(); other.c.status.value = '3'; await other.c.searchInvoices(); assert.equal(other.calls.lists.length, 0);
});
test('portal list transport preserves zero-status filters and prevents owner/page spoof through filter extras', async () => {
  const source = await readFile(new URL('../src/services/invoicePortal.ts', import.meta.url), 'utf8');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const calls = [], module = { exports: {} };
  new Function('require', 'module', 'exports', js)(() => ({ apiClient: { get: async (...args) => { calls.push(args); return { data: {} }; } } }), module, module.exports);
  await module.exports.invoicePortalApi.list(23, 2, { status: 0, fromDate: '2026-10-14', toDate: '2026-10-14', patientId: 999, userId: 8, page: 99 });
  assert.deepEqual(calls, [['/portal/invoices', { params: { patientId: 23, page: 2, pageSize: 20, status: 0, fromDate: '2026-10-14', toDate: '2026-10-14' } }]]);
});
