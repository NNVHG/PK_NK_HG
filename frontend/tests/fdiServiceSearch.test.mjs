import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/FdiServiceAssignmentView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'fdi-search-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const priced = { dentalServiceId: 1, code: 'TRAM', name: 'Trám', currentPrice: 120000 };
const unpriced = { dentalServiceId: 2, code: 'FUTURE', name: 'Chưa có giá', currentPrice: null };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'ADMIN' } = {}) {
  let cleanup, reload; const reads = [], writes = []; let failure = false, pending;
  const rows = { value: [] }, route = { params: { visitId: '1' } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      watch: (_value, cb) => { reload = cb; }, onUnmounted: cb => { cleanup = cb; } },
    pinia: { storeToRefs: () => ({ rows }) }, 'vue-router': { useRoute: () => route },
    axios: { isAxiosError: e => !!e.response },
    '@/components/fdi/FdiDentalChart.vue': {}, '@/types/fdi': { SURFACE_METADATA: [{ code: 'B' }], ADULT_TEETH: [11], CHILD_TEETH: [51] },
    '@/services/visits': { visitsService: { getById: async () => ({ visitId: Number(route.params.visitId), status: 'InProgress', isLocked: false, dentistId: 7 }) } },
    '@/services/visitServices': { visitServicesApi: {
      get: async () => [], catalog: async (...args) => { reads.push(args); if (failure) throw { response: { data: { message: 'Synthetic search failure' } } };
        if (pending) { const current = pending; pending = undefined; return await current; }
        return { items: args[1] === 'zzz' ? [] : [priced, unpriced], totalPages: 2 }; },
      assign: async (...args) => { writes.push(args); return [{ id: 4, ...args[1] }]; },
    } },
    '@/stores/auth': { useAuthStore: () => ({ role, user: { userId: 7 } }) },
    '@/stores/visitServices': { useVisitServicesStore: () => ({ replace: data => { rows.value = data; }, prepend: data => { rows.value = [...data, ...rows.value]; }, reset: () => { rows.value = []; } }) },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, rows, reads, writes, mount: () => reload(), cleanup: () => cleanup(), changeRoute: async () => { route.params.visitId = '2'; await reload(); },
    fail: value => { failure = value; }, pending: value => { pending = value; } };
}
test('search trims keyword resets page and clears stale service selection', async () => {
  const f = fixture(); await f.mount(); f.c.serviceId.value = 1; f.c.catalogPage.value = 2; f.c.keyword.value = '  Trám  '; await f.c.searchCatalog();
  assert.deepEqual(f.reads.at(-1), [1, 'Trám']); assert.equal(f.c.serviceId.value, null); assert.equal(f.c.appliedKeyword.value, 'Trám'); assert.equal(f.writes.length, 0);
});
test('pagination keeps applied keyword while unsent draft does not change results', async () => {
  const f = fixture(); await f.mount(); f.c.keyword.value = 'TRAM'; await f.c.searchCatalog(); f.c.keyword.value = 'new draft'; await f.c.loadCatalog(2);
  assert.deepEqual(f.reads.at(-1), [2, 'TRAM']);
});
test('clear search rereads first page without a filter', async () => {
  const f = fixture(); await f.mount(); f.c.keyword.value = 'TRAM'; await f.c.searchCatalog(); await f.c.clearSearch();
  assert.deepEqual(f.reads.at(-1), [1, '']); assert.equal(f.c.keyword.value, '');
});
test('overlong keyword and invalid page never send query', async () => {
  const f = fixture(); await f.mount(); const count = f.reads.length; f.c.keyword.value = 'x'.repeat(101); await f.c.searchCatalog();
  for (const page of [0, -1, 1.5, 1000001]) await f.c.loadCatalog(page);
  assert.equal(f.reads.length, count); assert.ok(f.c.searchError.value);
});
test('boundary keyword100 sends one read', async () => {
  const f = fixture(); await f.mount(); f.c.keyword.value = 'x'.repeat(100); await f.c.searchCatalog(); assert.equal(f.reads.at(-1)[1].length, 100);
});
test('readonly roles and unrelated dentist cannot search assignment catalog', async () => {
  for (const role of ['RECEPTIONIST', 'ASSISTANT', 'PATIENT']) { const f = fixture({ role }); await f.mount(); await f.c.searchCatalog(); assert.equal(f.reads.length, 0); }
  const f = fixture({ role: 'DENTIST' }); await f.mount(); f.c.visit.value.dentistId = 8; const count = f.reads.length; await f.c.searchCatalog(); assert.equal(f.reads.length, count);
});
test('failed read clears old options and prevents assigning stale selected ID', async () => {
  const f = fixture(); await f.mount(); f.fail(true); f.c.keyword.value = 'TRAM'; await f.c.searchCatalog(); f.c.serviceId.value = 1; f.c.general.value = true; await f.c.assign();
  assert.deepEqual(f.c.catalog.value, []); assert.equal(f.writes.length, 0); assert.ok(f.c.catalogError.value); f.fail(false); await f.c.searchCatalog(); assert.equal(f.c.catalogError.value, '');
});
test('no results stays empty and does not fabricate a service', async () => {
  const f = fixture(); await f.mount(); f.c.keyword.value = 'zzz'; await f.c.searchCatalog(); assert.deepEqual(f.c.catalog.value, []); assert.equal(f.c.catalogError.value, ''); assert.equal(f.writes.length, 0);
});
test('selected service from search assigns to preserved adult and child teeth', async () => {
  const f = fixture(); await f.mount(); f.c.selection.value = [{ toothNumber: 11 }, { toothNumber: 51 }]; f.c.surface.value = 'B'; f.c.keyword.value = 'TRAM'; await f.c.searchCatalog();
  f.c.serviceId.value = 1; await f.c.assign(); assert.deepEqual(f.writes, [[1, { serviceId: 1, toothNumbers: [11, 51], surface: 'B', quantity: 1 }]]); assert.equal(f.rows.value.length, 1);
});
test('unpriced service cannot be assigned', async () => {
  const f = fixture(); await f.mount(); f.c.serviceId.value = 2; f.c.general.value = true; await f.c.assign(); assert.equal(f.writes.length, 0);
});
test('busy search rejects another search and late result after route change', async () => {
  const f = fixture(); await f.mount(); const d = deferred(); f.pending(d.promise); f.c.keyword.value = 'TRAM'; const first = f.c.searchCatalog(); const count = f.reads.length;
  await f.c.searchCatalog(); assert.equal(f.reads.length, count); await f.changeRoute(); d.resolve({ items: [{ dentalServiceId: 99 }], totalPages: 9 }); await first;
  assert.equal(f.c.appliedKeyword.value, ''); assert.equal(f.c.catalog.value[0].dentalServiceId, 1);
});
test('unmount clears search and ignores late catalog results', async () => {
  const f = fixture(); await f.mount(); const d = deferred(); f.pending(d.promise); f.c.keyword.value = 'TRAM'; const first = f.c.searchCatalog(); f.cleanup(); d.resolve({ items: [priced], totalPages: 1 }); await first;
  assert.deepEqual(f.c.catalog.value, []); assert.equal(f.c.keyword.value, ''); assert.equal(f.c.visit.value, null);
});
