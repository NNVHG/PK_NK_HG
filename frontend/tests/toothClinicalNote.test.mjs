import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/FdiConditionView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'tooth-note-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
function fixture(role = 'DENTIST') {
  let pending, fail = false, cleanup, reload;
  const calls = [], rows = { value: [{ id: 1, note: 'Original note' }] }, route = { params: { visitId: '1' } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      watch: (_get, cb) => { reload = cb; }, onUnmounted: cb => { cleanup = cb; } },
    pinia: { storeToRefs: () => ({ rows }) }, 'vue-router': { useRoute: () => route },
    axios: { isAxiosError: e => Boolean(e.response) },
    '@/components/fdi/FdiDentalChart.vue': {}, '@/components/fdi/ToothHistoryDrawer.vue': {},
    '@/services/fdi': { FDI_CONDITIONS: { CARIES: 'Sâu răng' }, fdiService: { get: async () => [], add: async (...args) => {
      calls.push(args); if (fail) throw { response: { data: { message: 'Lần khám đã khóa' } } };
      return pending ?? { id: 2, note: args[4] };
    } } },
    '@/stores/fdi': { useFdiStore: () => ({ reset: () => { rows.value = []; }, replace: data => { rows.value = data; }, prepend: data => { rows.value = [data, ...rows.value]; } }) },
    '@/services/visits': { visitsService: { getById: async () => ({ visitId: Number(route.params.visitId), patientId: 1, status: 'InProgress', isLocked: false, dentistId: 7 }) } },
    '@/types/fdi': { SURFACE_METADATA: { occlusal: { code: 'O', nameVi: 'Mặt nhai' } }, ALL_SURFACES: ['occlusal'] },
    '@/stores/auth': { useAuthStore: () => ({ role, user: { userId: 7 } }) },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  c.visit.value = { visitId: 1, patientId: 1, status: 'InProgress', isLocked: false, dentistId: 7 };
  c.selection.value = [{ toothNumber: 11, surface: 'occlusal' }];
  return { c, calls, rows, fail: () => { fail = true; }, cleanup: () => cleanup(), reload: async () => { route.params.visitId = '4'; await reload(); },
    defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; } };
}
test('save sends trimmed multiline note for selected tooth and retains old record', async () => {
  const f = fixture(); f.c.note.value = '  Synthetic note\nSecond line  '; await f.c.save();
  assert.deepEqual(f.calls, [[1, 11, 'O', 'CARIES', 'Synthetic note\nSecond line']]);
  assert.equal(f.rows.value[0].note, 'Synthetic note\nSecond line'); assert.equal(f.rows.value[1].note, 'Original note'); assert.equal(f.c.note.value, '');
});
test('blank note is optional and sent as null', async () => {
  const f = fixture(); f.c.note.value = '   '; await f.c.save(); assert.equal(f.calls[0][4], null);
});
test('500 chars accepted; 501 rejected before API', async () => {
  const f = fixture(); f.c.note.value = 'x'.repeat(500); await f.c.save(); assert.equal(f.calls.length, 1);
  const g = fixture(); g.c.note.value = 'x'.repeat(501); await g.c.save(); assert.equal(g.calls.length, 0); assert.ok(g.c.error.value);
});
test('failure retains unsaved text and old records for user correction', async () => {
  const f = fixture(); f.fail(); f.c.note.value = 'Synthetic note'; await f.c.save();
  assert.equal(f.c.note.value, 'Synthetic note'); assert.equal(f.rows.value.length, 1); assert.equal(f.c.error.value, 'Lần khám đã khóa');
});
test('readonly roles, unrelated dentist, locked visit cannot save notes', async () => {
  for (const role of ['PATIENT', 'RECEPTIONIST', 'ASSISTANT']) { const f = fixture(role); await f.c.save(); assert.equal(f.calls.length, 0); }
  const f = fixture(); f.c.visit.value.dentistId = 8; await f.c.save(); assert.equal(f.calls.length, 0);
  const g = fixture('ADMIN'); g.c.visit.value.isLocked = true; await g.c.save(); assert.equal(g.calls.length, 0);
});
test('duplicate submit sends one request', async () => {
  const f = fixture(); f.c.note.value = 'Synthetic note'; const resolve = f.defer(); const first = f.c.save();
  await f.c.save(); assert.equal(f.calls.length, 1); resolve({ id: 2, note: 'Synthetic note' }); await first;
});
test('route change clears draft and old response cannot enter new store', async () => {
  const f = fixture(); f.c.note.value = 'Synthetic note'; const resolve = f.defer(); const first = f.c.save();
  await f.reload(); assert.equal(f.c.note.value, ''); f.rows.value = [{ id: 4 }];
  resolve({ id: 2, note: 'Synthetic note' }); await first; assert.deepEqual(f.rows.value, [{ id: 4 }]); assert.equal(f.c.success.value, '');
});
test('unmount ignores late clinical response', async () => {
  const f = fixture(); const resolve = f.defer(); const first = f.c.save(); f.cleanup(); resolve({ id: 2, note: 'Synthetic note' }); await first;
  assert.deepEqual(f.rows.value, []); assert.equal(f.c.success.value, '');
});
