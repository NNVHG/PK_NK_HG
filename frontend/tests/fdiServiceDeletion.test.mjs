import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/FdiServiceAssignmentView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'fdi-delete-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
function fixture(role = 'ADMIN') {
  let calls = [], fail = false, pending, cleanup, reload;
  const rows = { value: [{ id: 2 }, { id: 3 }] }, route = { params: { visitId: '1' } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: getter => ({ get value() { return getter(); } }),
      watch: (_value, cb) => { reload = cb; }, onUnmounted: cb => { cleanup = cb; } },
    pinia: { storeToRefs: () => ({ rows }) }, 'vue-router': { useRoute: () => route },
    axios: { isAxiosError: error => Boolean(error.response) },
    '@/components/fdi/FdiDentalChart.vue': {}, '@/types/fdi': { SURFACE_METADATA: [], ADULT_TEETH: [], CHILD_TEETH: [] },
    '@/services/visits': { visitsService: { getById: async () => ({ status: 'Completed', isLocked: true }) } },
    '@/services/visitServices': { visitServicesApi: { remove: async (...args) => {
      calls.push(args); if (fail) throw { response: { data: { message: 'Đã phát sinh thanh toán' } } }; return pending;
    }, get: async () => [], catalog: async () => ({ items: [], totalPages: 1 }) } },
    '@/stores/auth': { useAuthStore: () => ({ role, user: { userId: 7 } }) },
    '@/stores/visitServices': { useVisitServicesStore: () => ({ replace: data => { rows.value = data; }, reset: () => { rows.value = []; } }) },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(key => imports[key], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  c.visit.value = { status: 'InProgress', isLocked: false, dentistId: 7 };
  return { c, rows, calls, fail: () => { fail = true; }, cleanup: () => cleanup(), reload: async () => { route.params.visitId = '4'; await reload(); },
    defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; } };
}
test('begin deletion needs explicit reason and confirmation', async () => {
  const f = fixture(); f.c.beginDelete(2); assert.equal(f.calls.length, 0);
  await f.c.remove(); assert.equal(f.calls.length, 0); assert.ok(f.c.error.value);
});
test('confirmed deletion removes only acknowledged row and trims reason', async () => {
  const f = fixture(); f.c.beginDelete(2); f.c.deleteReason.value = '  Điều chỉnh  '; await f.c.remove();
  assert.deepEqual(f.calls, [[1, 2, 'Điều chỉnh']]); assert.deepEqual(f.rows.value, [{ id: 3 }]); assert.ok(f.c.success.value); assert.equal(f.c.deleteId.value, null);
});
test('failed deletion preserves rows and displays API error', async () => {
  const f = fixture(); f.fail(); f.c.beginDelete(2); f.c.deleteReason.value = 'Lý do'; await f.c.remove();
  assert.equal(f.rows.value.length, 2); assert.equal(f.c.error.value, 'Đã phát sinh thanh toán'); assert.equal(f.c.success.value, ''); assert.equal(f.calls.length, 1);
});
test('double submit sends one request', async () => {
  const f = fixture(); f.c.beginDelete(2); f.c.deleteReason.value = 'Lý do'; const resolve = f.defer();
  const first = f.c.remove(); await f.c.remove(); assert.equal(f.calls.length, 1); resolve(); await first;
});
test('receptionist, unrelated dentist and locked visit cannot delete', async () => {
  for (const role of ['RECEPTIONIST', 'PATIENT', 'ASSISTANT']) { const f = fixture(role); f.c.beginDelete(2); assert.equal(f.c.deleteId.value, null); }
  const f = fixture('DENTIST'); f.c.visit.value.dentistId = 8; f.c.beginDelete(2); assert.equal(f.c.deleteId.value, null);
  const g = fixture(); g.c.visit.value.isLocked = true; g.c.beginDelete(2); assert.equal(g.c.deleteId.value, null);
});
test('overlong reason is rejected before network', async () => {
  const f = fixture(); f.c.beginDelete(2); f.c.deleteReason.value = 'x'.repeat(501); await f.c.remove(); assert.equal(f.calls.length, 0);
});
test('response after route change never removes new visit rows', async () => {
  const f = fixture(); f.c.beginDelete(2); f.c.deleteReason.value = 'Lý do'; const resolve = f.defer(); const request = f.c.remove();
  await f.reload(); f.rows.value = [{ id: 2, visitId: 4 }]; resolve(); await request;
  assert.equal(f.rows.value[0].visitId, 4); assert.equal(f.c.success.value, '');
});
test('response after unmount does not repopulate store', async () => {
  const f = fixture(); f.c.beginDelete(2); f.c.deleteReason.value = 'Lý do'; const resolve = f.defer(); const request = f.c.remove();
  f.cleanup(); resolve(); await request; assert.deepEqual(f.rows.value, []); assert.equal(f.c.success.value, '');
});
