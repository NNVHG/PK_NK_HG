import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/DentalChairsView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'chairs' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const row = { id: 7, chairNumber: 'GHE-01', roomName: 'Phòng 1', status: 1, statusText: 'Sẵn sàng', description: null, isActive: true, version: 1 };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { resolve, promise }; }
function fixture({ role = 'ADMIN', fail = false, readFailureAfter = Infinity, result, read } = {}) {
  let mount, cleanup; const reads = [], writes = []; const auth = { role };
  const store = { result: null, reset() { this.result = null; }, replace(value) { this.result = value; } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/dentalChairs': { useDentalChairsStore: () => store },
    '@/services/dentalChairs': { dentalChairsService: {
      getPage: async query => { reads.push(query); if (reads.length > readFailureAfter) throw new Error('Read failure'); return read ? read(query) : { items: [row], page: query.page, totalCount: 1, hasNextPage: false }; },
      create: async input => { writes.push(['create', input]); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...row, status: input.status }; },
      update: async (id, input) => { writes.push(['update', id, input]); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...row, id, status: input.status, version: 2 }; }
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, store, reads, writes, auth, mount: () => mount(), cleanup: () => cleanup() };
}
function create(f) { f.c.openCreate(); f.c.chairNumber.value = ' ghe-02 '; f.c.roomName.value = ' Phòng 2 '; f.c.confirmed.value = true; }
test('catalog uses bounded actual server list with keyword/status/active filters', async () => {
  const f = fixture(); f.c.keyword.value = ' Phòng '; f.c.status.value = 3; f.c.active.value = 'true'; await f.mount();
  assert.deepEqual(f.reads, [{ page: 1, pageSize: 20, keyword: 'Phòng', status: 3, isActive: true }]); assert.equal(f.store.result.items[0].id, 7);
});
test('invalid filters and pages never read', async () => {
  const f = fixture(); await f.c.load(0); await f.c.load(1000001); f.c.status.value = 5; await f.c.load(); f.c.status.value = 0; f.c.keyword.value = 'x'.repeat(101); await f.c.load(); assert.equal(f.reads.length, 0);
});
test('all non-admin roles cannot read or save catalog', async () => {
  for (const role of ['RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT']) { const f = fixture({ role }); await f.mount(); create(f); await f.c.save(); assert.equal(f.reads.length, 0); assert.equal(f.writes.length, 0); }
});
test('create needs confirmation and sends trimmed actual values', async () => {
  const f = fixture(); await f.mount(); create(f); f.c.confirmed.value = false; await f.c.save(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes[0][0], 'create'); assert.equal(f.writes[0][1].roomName, 'Phòng 2'); assert.equal(f.writes[0][1].chairNumber, 'ghe-02'); assert.equal(f.writes[0][1].description, null); assert.equal(f.c.formOpen.value, false); assert.equal(f.reads.length, 2);
});
test('back clears draft and never writes', async () => {
  const f = fixture(); await f.mount(); create(f); f.c.closeForm(); assert.equal(f.c.roomName.value, ''); assert.equal(f.c.confirmed.value, false); assert.equal(f.writes.length, 0);
});
test('blank, oversized or Occupied target rejected before API', async () => {
  for (const mutate of [c => { c.roomName.value = ' '; }, c => { c.chairNumber.value = 'x'.repeat(51); }, c => { c.roomName.value = 'x'.repeat(101); }, c => { c.description.value = 'x'.repeat(501); }, c => { c.formStatus.value = 2; }]) {
    const f = fixture(); await f.mount(); create(f); mutate(f.c); await f.c.save(); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value);
  }
});
test('edit retains immutable code and sends expected version, maintenance and inactive supported', async () => {
  for (const status of [1, 3, 4]) { const f = fixture(); await f.mount(); f.c.openEdit(row); f.c.formStatus.value = status; f.c.confirmed.value = true; await f.c.save();
    assert.equal(f.writes[0][0], 'update'); assert.equal(f.writes[0][1], 7); assert.equal(f.writes[0][2].expectedVersion, 1); assert.equal(f.writes[0][2].status, status); assert.ok(!('chairNumber' in f.writes[0][2])); }
});
test('occupied, unknown and stale list row cannot be edited', async () => {
  const f = fixture(); await f.mount(); for (const invalid of [{ ...row, status: 2 }, { ...row, id: 99 }, { ...row, version: 2 }]) f.c.openEdit(invalid); assert.equal(f.c.formOpen.value, false);
});
test('duplicate submit and close during write cannot send another mutation', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); create(f); const pending = f.c.save(); await f.c.save(); f.c.closeForm(); await f.c.load();
  assert.equal(f.writes.length, 1); assert.equal(f.c.formOpen.value, true); assert.equal(f.reads.length, 1); d.resolve(row); await pending;
});
test('server conflict keeps draft but requires reload and fresh confirmation', async () => {
  const f = fixture({ fail: true }); await f.mount(); create(f); await f.c.save(); await f.c.save(); assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.error.value, 'Synthetic409');
  await f.c.load(); assert.equal(f.c.formOpen.value, false); assert.equal(f.c.needsReview.value, false);
});
test('failed refresh after save keeps further mutations blocked', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); create(f); await f.c.save(); f.c.openCreate(); assert.equal(f.c.formOpen.value, false); assert.equal(f.c.needsReview.value, true);
});
test('late list response cannot overwrite newer query', async () => {
  const a = deferred(), b = deferred(); let n = 0; const f = fixture({ read: () => ++n === 1 ? a.promise : b.promise });
  const first = f.c.load(1), second = f.c.load(2); b.resolve({ items: [], page: 2, totalCount: 0 }); await second; a.resolve({ items: [row], page: 1 }); await first; assert.equal(f.store.result.page, 2);
});
test('unmount ignores late save and clears store and draft', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); create(f); const pending = f.c.save(); f.cleanup(); d.resolve(row); await pending; assert.equal(f.store.result, null); assert.equal(f.c.formOpen.value, false); assert.equal(f.c.message.value, ''); assert.equal(f.reads.length, 1);
});
test('Admin role loss before submitting prevents writes', async () => {
  const f = fixture(); await f.mount(); create(f); f.auth.role = 'ASSISTANT'; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('route and navigation restrict catalog to Admin', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  assert.match(router, /path: '\/admin\/chairs'[\s\S]*?roles: \['ADMIN'\]/); assert.match(app, /v-if="isAdmin" to="\/admin\/chairs"/);
});
