import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const view = await readFile(new URL('../src/views/MedicineStockView.vue', import.meta.url), 'utf8');
const notice = await readFile(new URL('../src/components/PrescriptionStockNotice.vue', import.meta.url), 'utf8');
const transpile = code => ts.transpileModule(code, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const compile = source => transpile(compileScript(parse(source).descriptor, { id: 'medicine-stock-test' }).content);
const compiled = compile(view), noticeCompiled = compile(notice);
const medicine = { id: 7, code: 'M-TEST', name: 'Synthetic medicine', unit: 'Viên', version: 1, isActive: true };
const detail = { medicineId: 7, code: medicine.code, name: medicine.name, unit: 'Viên', medicineVersion: 1, isActive: true, onHand: 20, receipts: [], hasMore: false };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { resolve, promise }; }
function fixture({ role = 'ADMIN', read, write, list, store: suppliedStore } = {}) {
  let mount, cleanup, changed; const reads = [], writes = [], lists = [], auth = { role, user: { userId: 99 } };
  const store = suppliedStore ?? { pending: null, start(actor, medicineId, request) { if (!this.pending) this.pending = { actor, medicineId, request: { ...request } }; }, reset() { this.pending = null; } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), nextTick: async () => {}, onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; }, watch: (_, cb) => { changed = cb; } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/medicineStock': { useMedicineStockStore: () => store },
    '@/services/medicines': { medicinesService: { list: async (...args) => { lists.push(args); return list ? list(...args) : { items: [medicine], page: args[1], hasNextPage: false }; } } },
    '@/services/medicineStock': { medicineStockService: {
      read: async (...args) => { reads.push(args); return read ? read(...args) : { ...detail }; },
      receive: async (id, input) => { writes.push({ id, input }); return write ? write(id, input) : { receipt: { id: 1, medicineId: id, quantity: input.quantity }, replayed: false }; }
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', 'crypto', compiled)(name => imports[name], module, module.exports, { randomUUID });
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, store, reads, writes, lists, auth, mount: () => mount(), cleanup: () => cleanup(), changed: () => changed() };
}
async function prepare(f) { await f.c.loadDetail(7); f.c.quantity.value = '5'; f.c.supplier.value = ' Supplier '; f.c.confirmed.value = true; }
test('stock entry uses actual medicine search and per-medicine stock', async () => {
  const f = fixture(); f.c.keyword.value = ' M '; await f.c.search(); await f.c.select(7);
  assert.deepEqual(f.lists, [['M', 1, true]]); assert.deepEqual(f.reads, [[7, 0]]); assert.equal(f.c.detail.value.onHand, 20);
});
test('non-admin roles never search/read/receive', async () => {
  for (const role of ['DENTIST', 'ASSISTANT', 'RECEPTIONIST', 'PATIENT', '']) { const f = fixture({ role }); await f.c.search(); await prepare(f); await f.c.receive(); assert.equal(f.lists.length + f.reads.length + f.writes.length, 0); }
});
test('receipt needs confirmation and sends positive integer with stable id', async () => {
  const f = fixture(); await prepare(f); f.c.confirmed.value = false; await f.c.receive(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; await f.c.receive(); const input = f.writes[0].input;
  assert.equal(input.quantity, 5); assert.equal(input.expectedMedicineVersion, 1); assert.equal(input.supplierName, 'Supplier'); assert.equal(input.notes, null); assert.equal(input.confirmed, true); assert.match(input.requestId, /^[0-9a-f-]{36}$/);
  assert.equal(f.store.pending, null); assert.equal(f.c.confirmed.value, false); assert.equal(f.reads.length, 2); assert.ok(f.c.message.value);
});
test('invalid quantities and text never create pending request', async () => {
  for (const quantity of ['0', '-1', '0.5', '1e3', '2147483648', '']) { const f = fixture(); await prepare(f); f.c.quantity.value = quantity; await f.c.receive(); assert.equal(f.writes.length, 0); assert.equal(f.store.pending, null); }
  for (const [field, value] of [['supplier', ' '], ['supplier', 'x'.repeat(201)], ['supplier', 'Bad\nName'], ['notes', 'x'.repeat(501)]]) { const f = fixture(); await prepare(f); f.c[field].value = value; await f.c.receive(); assert.equal(f.writes.length, 0); }
});
test('inactive and malformed stock detail cannot be used to receive', async () => {
  const off = fixture({ read: () => ({ ...detail, isActive: false }) }); await prepare(off); await off.c.receive(); assert.equal(off.writes.length, 0);
  for (const value of [{ ...detail, onHand: -1 }, { ...detail, medicineVersion: 0 }, { ...detail, medicineId: 8 }, null]) { const f = fixture({ read: () => value }); await f.c.loadDetail(7); assert.equal(f.c.detail.value, null); assert.ok(f.c.error.value); }
});
test('unknown result locks form and reuses exact pending request', async () => {
  let n = 0; const f = fixture({ write: (id, input) => { if (++n === 1) throw new Error('Network result unknown'); return { receipt: { id: 1, medicineId: id, quantity: input.quantity }, replayed: true }; } });
  await prepare(f); await f.c.receive(); const pending = f.store.pending;
  f.c.quantity.value = '999'; await f.c.receive(); await f.c.search(); await f.c.select(8); assert.equal(f.writes.length, 1); assert.equal(f.lists.length, 0);
  await f.c.retry(); assert.equal(f.writes.length, 2); assert.equal(f.writes[1].input.requestId, pending.request.requestId); assert.equal(f.writes[1].input.quantity, 5); assert.equal(f.store.pending, null); assert.match(f.c.message.value, /không cộng thêm/);
});
test('concurrent submit and pending reload cannot send a second receipt', async () => {
  const d = deferred(), f = fixture({ write: () => d.promise }); await prepare(f); const saving = f.c.receive(); await f.c.receive(); await f.c.retry(); await f.c.loadDetail(7);
  assert.equal(f.writes.length, 1); assert.equal(f.reads.length, 1); d.resolve({ receipt: { id: 1, medicineId: 7, quantity: 5 }, replayed: false }); await saving;
});
test('definitive stale rejection clears pending but requires new stock detail', async () => {
  const f = fixture({ write: () => { throw { response: { status: 409, data: { code: 'MED_STOCK_STALE', message: 'Synthetic stale' } } }; } }); await prepare(f); await f.c.receive();
  assert.equal(f.store.pending, null); assert.equal(f.c.detail.value, null); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.error.value, 'Synthetic stale'); await f.c.receive(); assert.equal(f.writes.length, 1);
});
test('generic conflict retains key because commit outcome needs reread/replay', async () => {
  const f = fixture({ write: () => { throw { response: { status: 409, data: { code: 'MED_STOCK_CONFLICT' } } }; } }); await prepare(f); await f.c.receive(); assert.ok(f.store.pending); await f.c.retry(); assert.equal(f.writes[0].input.requestId, f.writes[1].input.requestId);
});
test('malformed receipt success never clears pending or claims success', async () => {
  for (const receipt of [{ id: 0, medicineId: 7, quantity: 5 }, { id: 1, medicineId: 8, quantity: 5 }, { id: 1, medicineId: 7, quantity: 9 }]) {
    const f = fixture({ write: () => ({ receipt, replayed: false }) }); await prepare(f); await f.c.receive(); assert.ok(f.store.pending); assert.equal(f.c.message.value, '');
  }
});
test('unmount keeps unknown request in store and ignores late result', async () => {
  const d = deferred(), f = fixture({ write: () => d.promise }); await prepare(f); const pending = f.c.receive(); const key = f.store.pending.request.requestId; f.cleanup();
  d.resolve({ receipt: { id: 1, medicineId: 7, quantity: 5 }, replayed: false }); await pending; assert.equal(f.store.pending.request.requestId, key); assert.equal(f.c.detail.value, null); assert.equal(f.c.message.value, '');
});
test('role/identity switch clears private draft and pending key', async () => {
  const f = fixture({ write: () => { throw new Error('Unknown'); } }); await prepare(f); await f.c.receive(); f.auth.role = 'PATIENT'; f.changed(); assert.equal(f.store.pending, null); assert.equal(f.c.detail.value, null); assert.equal(f.c.supplier.value, ''); await f.c.retry(); assert.equal(f.writes.length, 1);
});
test('late stock detail cannot restore state after unmount', async () => {
  const d = deferred(), f = fixture({ read: () => d.promise }); const pending = f.c.loadDetail(7); f.cleanup(); d.resolve(detail); await pending; assert.equal(f.c.detail.value, null);
});
test('history cursor uses exact medicine and older id', async () => { const f = fixture(); await f.c.loadDetail(7, 99); assert.deepEqual(f.reads[0], [7, 99]); });
test('invalid search/page/selection/cursor never calls API', async () => {
  const f = fixture(); await f.c.search(0); await f.c.search(1000001); f.c.keyword.value = 'x'.repeat(201); await f.c.search(); await f.c.loadDetail(0); await f.c.loadDetail(7, -1); await f.c.select(99); assert.equal(f.lists.length + f.reads.length, 0);
});
test('changing receipt inputs resets confirmation in template', () => { assert.equal((view.match(/@input="confirmed = false"/g) ?? []).length, 3); assert.match(view, /v-model="confirmed"/); });
function noticeFixture({ role = 'DENTIST', read } = {}) {
  let cleanup, changed; const auth = { role, user: { userId: 9 } }, calls = [];
  const props = { visitId: 7, items: [{ medicineId: 1, quantity: 5, unit: 'Viên' }] };
  const imports = { vue: { defineComponent: c => c, ref: value => ({ value }), watch: (_, cb) => { changed = cb; }, onUnmounted: cb => { cleanup = cb; } }, '@/stores/auth': { useAuthStore: () => auth },
    '@/services/medicineStock': { medicineStockService: { availability: async (...args) => { calls.push(args); return read ? read(...args) : [{ medicineId: 1, code: 'M', name: 'Medicine', unit: 'Viên', onHand: 2, isActive: true }]; } } } };
  const module = { exports: {} }; new Function('require', 'module', 'exports', noticeCompiled)(name => imports[name], module, module.exports);
  return { c: module.exports.default.setup(props, { expose() {} }), props, auth, calls, cleanup: () => cleanup(), changed: () => changed() };
}
test('prescribing notice requests only distinct selected medicine ids', async () => { const f = noticeFixture(); await f.c.load(); assert.deepEqual(f.calls, [[7, [1]]]); assert.equal(f.c.byId(1).onHand, 2); });
test('notice never calls API for non-dentists or invalid targets', async () => {
  for (const role of ['ADMIN', 'ASSISTANT', 'PATIENT', 'RECEPTIONIST']) { const f = noticeFixture({ role }); await f.c.load(); assert.equal(f.calls.length, 0); }
  const f = noticeFixture(); f.props.visitId = 0; await f.c.load(); f.props.visitId = 7; f.props.items = []; await f.c.load(); assert.equal(f.calls.length, 0);
});
test('unknown stock is not replaced with a fake zero', async () => { const f = noticeFixture({ read: () => { throw new Error('Offline'); } }); await f.c.load(); assert.equal(f.c.byId(1), undefined); assert.match(f.c.error.value, /Có thể tiếp tục kê/); });
test('notice rejects out-of-scope or invalid stock rows', async () => {
  for (const rows of [[{ medicineId: 9, onHand: 0 }], [{ medicineId: 1, code: 'M', name: 'M', unit: 'Viên', onHand: -1 }]]) { const f = noticeFixture({ read: () => rows }); await f.c.load(); assert.ok(f.c.error.value); assert.equal(f.c.rows.value.length, 0); }
});
test('notice discards response after identity switch or unmount', async () => {
  const d = deferred(), f = noticeFixture({ read: () => d.promise }); const pending = f.c.load(); f.auth.user.userId = 100; d.resolve([]); await pending; assert.equal(f.c.rows.value.length, 0);
  const e = deferred(), g = noticeFixture({ read: () => e.promise }); const second = g.c.load(); g.cleanup(); e.resolve([]); await second; assert.equal(g.c.rows.value.length, 0);
});
test('short stock is a soft warning and quantity edits need no extra request', async () => {
  const f = noticeFixture(); await f.c.load(); f.props.items[0].quantity = 10; assert.equal(f.calls.length, 1); assert.ok(f.props.items[0].quantity > f.c.byId(1).onHand);
  assert.match(notice, /vẫn có thể lưu đơn/); assert.doesNotMatch(notice, /emit\(|\.receive\(|\.dispense\(/);
});
test('new route/menu remains Admin-only; draft composition includes stock notice', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'), app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8'), rx = await readFile(new URL('../src/views/PrescriptionView.vue', import.meta.url), 'utf8');
  assert.match(router, /path: '\/admin\/medicine-stock'[^\n]*roles: \['ADMIN'\]/); assert.match(app, /v-if="isAdmin" to="\/admin\/medicine-stock"/); assert.match(rx, /<PrescriptionStockNotice :visit-id="visitId"/);
});
test('typed stock API uses repeated medicineIds and no mutation from availability', async () => {
  const calls = [], module = { exports: {} }, code = transpile(await readFile(new URL('../src/services/medicineStock.ts', import.meta.url), 'utf8'));
  const api = { get: async (...a) => { calls.push(['GET', ...a]); return { data: [] }; }, post: async (...a) => { calls.push(['POST', ...a]); return { data: {} }; } };
  new Function('require', 'module', 'exports', code)(() => ({ apiClient: api }), module, module.exports);
  await module.exports.medicineStockService.availability(7, [1, 2]); assert.equal(calls[0][1], '/visits/7/prescription/stock'); assert.equal(calls[0][2].params.toString(), 'medicineIds=1&medicineIds=2');
  await module.exports.medicineStockService.receive(1, { requestId: 'stable' }); assert.equal(calls[1][1], '/medicine-stock/1/receipts');
});
test('handover validates stock DTO and exposes safe stock/legacy copy', async () => {
  const module = { exports: {} }, code = transpile(await readFile(new URL('../src/services/prescriptionHandover.ts', import.meta.url), 'utf8'));
  new Function('require', 'module', 'exports', code)(() => ({ apiClient: {} }), module, module.exports);
  const value = { needsDeduction: true, legacyHandover: false, canFulfill: false, lines: [{ medicineId: 1, code: 'M', name: 'Medicine', unit: 'Viên', quantity: 5, onHand: 2, unitMatches: true }] };
  assert.equal(module.exports.MOD_RX_validStock(value), true); assert.equal(module.exports.MOD_RX_validStock({ ...value, legacyHandover: true }), false); assert.equal(module.exports.MOD_RX_validStock({ ...value, lines: [{ ...value.lines[0], onHand: -1 }] }), false);
  const handover = await readFile(new URL('../src/components/PrescriptionHandover.vue', import.meta.url), 'utf8'); assert.match(handover, /cùng giao dịch/); assert.match(handover, /không xuất thuốc thêm/); assert.match(handover, /không tự hoàn tiền hoặc nhập lại kho/);
});
test('browser fixture stays in tests with valid module syntax and synthetic API', async () => {
  const html = await readFile(new URL('./browser-medicine-stock.html', import.meta.url), 'utf8');
  const script = html.match(/<script type="module">([\s\S]*?)<\/script>/)?.[1]; assert.ok(script);
  assert.equal(ts.createSourceFile('fixture.mjs', script, ts.ScriptTarget.Latest, true, ts.ScriptKind.JS).parseDiagnostics.length, 0);
  assert.match(html, /Fixture chỉ dùng trong tests/); assert.match(html, /apiClient.defaults.adapter/);
});
