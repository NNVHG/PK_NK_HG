import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/fdi/ToothHistoryDrawer.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'history-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
function fixture() {
  const calls = [], props = { patientId: 1 };
  let fail = false, pending, cleanup, change;
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), watch: (_get, cb) => { change = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e.response) }, '@/services/fdi': { FDI_CONDITIONS: { CARIES: 'Sâu răng' } },
    '@/services/toothHistory': { toothHistoryApi: { get: async (...args) => {
      calls.push(args); if (fail) throw { response: { data: { message: 'Không có quyền xem hồ sơ' } } };
      return pending ?? { items: [{ id: 1, toothNumber: args[1] }], page: args[2], totalPages: 3 };
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup(props, { expose() {} });
  c.dialog.value = { open: false, showModal() { this.open = true; }, close() { this.open = false; } };
  return { c, calls, props, cleanup: () => cleanup(), change: () => change(), fail: value => { fail = value; },
    defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; }, clearPending: () => { pending = null; } };
}
test('mount does not preload history and selected tooth opens on demand', async () => {
  const f = fixture(); assert.equal(f.calls.length, 0); await f.c.open(11);
  assert.deepEqual(f.calls, [[1, 11, 1]]); assert.equal(f.c.dialog.value.open, true); assert.equal(f.c.allJaw.value, false);
});
test('whole jaw toggle and pagination use correct scope', async () => {
  const f = fixture(); await f.c.open(51); f.c.allJaw.value = true; await f.c.load(1); await f.c.load(2);
  assert.deepEqual(f.calls.slice(1), [[1, null, 1], [1, null, 2]]); assert.equal(f.c.page.value, 2);
});
test('new tooth resets whole jaw and page', async () => {
  const f = fixture(); await f.c.open(11); f.c.allJaw.value = true; await f.c.load(2); await f.c.open(85);
  assert.deepEqual(f.calls.at(-1), [1, 85, 1]); assert.equal(f.c.allJaw.value, false);
});
test('errors clear old rows, show actual API error, and permit retry', async () => {
  const f = fixture(); await f.c.open(11); f.fail(true); await f.c.load(2);
  assert.deepEqual(f.c.items.value, []); assert.equal(f.c.error.value, 'Không có quyền xem hồ sơ'); assert.equal(f.c.loading.value, false);
  f.fail(false); await f.c.load(f.c.page.value); assert.equal(f.c.page.value, 2); assert.equal(f.c.error.value, '');
});
test('late response after close does not expose clinical rows', async () => {
  const f = fixture(); const resolve = f.defer(); const opening = f.c.open(11); f.c.close();
  resolve({ items: [{ id: 7 }], page: 1, totalPages: 1 }); await opening;
  assert.deepEqual(f.c.items.value, []); assert.equal(f.c.dialog.value.open, false);
});
test('older tooth response cannot replace a newly selected tooth', async () => {
  const f = fixture(); const resolve = f.defer(); const opening = f.c.open(11);
  f.clearPending(); await f.c.open(51); resolve({ items: [{ id: 7, toothNumber: 11 }], page: 1, totalPages: 1 }); await opening;
  assert.equal(f.c.items.value[0].toothNumber, 51);
});
test('patient change and unmount close drawer and clear rows', async () => {
  const f = fixture(); await f.c.open(11); f.props.patientId = 2; f.change(); assert.deepEqual(f.c.items.value, []); assert.equal(f.c.dialog.value.open, false);
  await f.c.open(11); f.cleanup(); assert.deepEqual(f.c.items.value, []); assert.equal(f.c.dialog.value.open, false);
});
test('invalid FDI, patient and page never issue requests', async () => {
  const f = fixture(); for (const tooth of [0, 19, 56, 86, 99, 11.5]) await f.c.open(tooth); assert.equal(f.calls.length, 0);
  f.props.patientId = 0; await f.c.open(11); assert.equal(f.calls.length, 0); f.props.patientId = 1;
  await f.c.open(11); await f.c.load(0); await f.c.load(1.5); assert.equal(f.calls.length, 1);
});
