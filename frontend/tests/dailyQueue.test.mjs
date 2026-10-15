import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'queue-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const result = { items: [{ queueEntryId: 1, patientName: 'Synthetic name', isPriority: true }], page: 1, totalCount: 21, hasNextPage: true };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture(responses = [result]) {
  let mount, cleanup; const calls = [];
  const imports = {
    vue: { defineComponent: c => c, computed: get => ({ get value() { return get(); } }), ref: value => ({ value }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => ({ role: 'RECEPTIONIST' }) },
    '@/services/appointments': { appointmentsService: {} },
    '@/services/queue': { queueService: { getToday: async (...args) => {
      calls.push(args); const response = responses.shift() ?? result; if (response instanceof Error) throw response; return response;
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, mount: () => mount(), cleanup: () => cleanup() };
}
test('mount loads waiting queue and preserves backend order', async () => {
  const f = fixture(); await f.mount(); assert.deepEqual(f.calls, [[1, 1]]); assert.deepEqual(f.c.rows.value, result.items); assert.equal(f.c.total.value, 21);
});
test('all status filter omits status and resets pagination', async () => {
  const f = fixture(); f.c.page.value = 3; f.c.status.value = 0; await f.c.load(1); assert.deepEqual(f.calls, [[1, undefined]]); assert.equal(f.c.page.value, 1);
});
test('pagination requests explicit page with selected filter', async () => {
  const f = fixture([{ ...result, page: 2, hasNextPage: false }]); f.c.status.value = 5; await f.c.load(2); assert.deepEqual(f.calls, [[2, 5]]); assert.equal(f.c.hasNext.value, false);
});
test('ordinary and appointment-priority both use A prefix, not emergency U', () => {
  const f = fixture(); assert.equal(f.c.numberLabel(1), 'A-001'); assert.equal(f.c.numberLabel(120), 'A-120');
});
test('error clears old rows and reload can recover', async () => {
  const f = fixture([result, new Error('Synthetic network error'), result]); await f.mount(); await f.c.load();
  assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value); assert.equal(f.c.total.value, 0); await f.c.load(); assert.equal(f.c.error.value, '');
});
test('older filter response cannot replace newer queue', async () => {
  const d = deferred(), f = fixture([d.promise, { ...result, items: [{ queueEntryId: 2 }] }]); const first = f.c.load();
  f.c.status.value = 2; await f.c.load(); d.resolve(result); await first; assert.deepEqual(f.c.rows.value, [{ queueEntryId: 2 }]);
});
test('unmount clears queue and ignores late response', async () => {
  const d = deferred(), f = fixture([d.promise]); const first = f.c.load(); f.cleanup(); d.resolve(result); await first;
  assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.updatedAt.value, '');
});
test('invalid page or status never calls API', async () => {
  const f = fixture(); await f.c.load(0); await f.c.load(1000001); f.c.status.value = 7; await f.c.load(); assert.equal(f.calls.length, 0);
});
