import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/PatientAppointmentHistoryView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'patient-appointment-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const result = { items: [{ appointmentId: 7, patientId: 2 }], page: 1, totalCount: 21, hasNextPage: true };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ pages = [result] } = {}) {
  let mount, cleanup; const calls = [];
  const imports = {
    '@/components/PatientAppointmentCancellation.vue': {},
    '@/components/AppointmentRescheduling.vue': {},
    vue: { defineComponent: c => c, ref: value => ({ value }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/services/appointments': { appointmentsService: { getMine: async (...args) => { calls.push(args); const response = pages.shift() ?? result; if (response instanceof Error) throw response; return response; } } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, mount: () => mount(), cleanup: () => cleanup() };
}
test('mount loads personal appointments', async () => {
  const f = fixture(); await f.mount(); assert.deepEqual(f.calls, [[1, undefined]]); assert.deepEqual(f.c.rows.value, result.items);
});

test('successful self-reschedule refreshes current page and shows confirmation', async () => {
  const f = fixture(); await f.mount(); f.c.page.value = 2; await f.c.rescheduled();
  assert.deepEqual(f.calls[1], [2, undefined]); assert.ok(f.c.notice.value); f.cleanup(); assert.equal(f.c.notice.value, '');
});
test('selecting status sends only requested filters', async () => {
  const f = fixture(); await f.mount(); f.c.status.value = 'Cancelled'; await f.c.load(1); assert.deepEqual(f.calls[1], [1, 'Cancelled']);
});
test('pagination preserves current personal scope', async () => {
  const f = fixture(); await f.mount(); await f.c.load(2); assert.deepEqual(f.calls[1], [2, undefined]);
});
test('invalid status or page cannot be requested', async () => {
  const f = fixture(); await f.mount(); f.c.status.value = 'Unknown'; await f.c.load(); f.c.status.value = ''; await f.c.load(0); assert.equal(f.calls.length, 1);
});
test('failure clears old rows and retry recovers', async () => {
  const f = fixture({ pages: [result, new Error('Synthetic fault'), result] }); await f.mount(); await f.c.load(); assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value); await f.c.load(); assert.equal(f.c.error.value, '');
});
test('old response cannot replace new filter result', async () => {
  const d = deferred(), f = fixture({ pages: [d.promise, { ...result, items: [] }] }); const first = f.c.load(); f.c.status.value = 'Cancelled'; await f.c.load(); d.resolve(result); await first; assert.deepEqual(f.c.rows.value, []);
});
test('unmount clears private data and ignores both late API responses', async () => {
  const d = deferred(), f = fixture({ pages: [d.promise] }); const first = f.mount(); f.cleanup(); d.resolve(result); await first;
  assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.total.value, 0);
});
