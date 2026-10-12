import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/PatientAppointmentHistoryView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'patient-appointment-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const profiles = [{ patientId: 1, fullName: 'Synthetic adult', dateOfBirth: '1990-01-01' }, { patientId: 2, fullName: 'Synthetic child', dateOfBirth: '2020-01-01' }];
const result = { items: [{ appointmentId: 7, patientId: 2 }], page: 1, totalCount: 21, hasNextPage: true };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ pages = [result], profileResult = profiles } = {}) {
  let mount, cleanup; const calls = [];
  const imports = {
    '@/components/PatientAppointmentCancellation.vue': {},
    '@/components/AppointmentRescheduling.vue': {},
    vue: { defineComponent: c => c, ref: value => ({ value }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/services/appointments': { appointmentsService: { getMine: async (...args) => { calls.push(args); const response = pages.shift() ?? result; if (response instanceof Error) throw response; return response; } } },
    '@/services/patients': { patientsService: { getMyProfiles: async () => { if (profileResult instanceof Error) throw profileResult; return profileResult; } } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, mount: () => mount(), cleanup: () => cleanup() };
}
test('mount loads family profiles and all owned appointments', async () => {
  const f = fixture(); await f.mount(); assert.deepEqual(f.c.profiles.value, profiles); assert.deepEqual(f.calls, [[1, undefined, undefined]]); assert.deepEqual(f.c.rows.value, result.items);
});

test('successful self-reschedule refreshes current family page and shows confirmation', async () => {
  const f = fixture(); await f.mount(); f.c.patientId.value = 2; f.c.page.value = 2; await f.c.rescheduled();
  assert.deepEqual(f.calls[1], [2, 2, undefined]); assert.ok(f.c.notice.value); f.cleanup(); assert.equal(f.c.notice.value, '');
});
test('selecting member and status sends only requested filters', async () => {
  const f = fixture(); await f.mount(); f.c.patientId.value = 2; f.c.status.value = 'Cancelled'; await f.c.load(1); assert.deepEqual(f.calls[1], [1, 2, 'Cancelled']);
});
test('pagination preserves selected family profile', async () => {
  const f = fixture(); await f.mount(); f.c.patientId.value = 1; await f.c.load(2); assert.deepEqual(f.calls[1], [2, 1, undefined]);
});
test('unknown member, invalid status or page cannot be requested', async () => {
  const f = fixture(); await f.mount(); f.c.patientId.value = 3; await f.c.load(); f.c.patientId.value = 0; f.c.status.value = 'Unknown'; await f.c.load(); f.c.status.value = ''; await f.c.load(0); assert.equal(f.calls.length, 1);
});
test('failure clears old rows and retry recovers', async () => {
  const f = fixture({ pages: [result, new Error('Synthetic fault'), result] }); await f.mount(); await f.c.load(); assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value); await f.c.load(); assert.equal(f.c.error.value, '');
});
test('old response cannot replace new filter result', async () => {
  const d = deferred(), f = fixture({ pages: [d.promise, { ...result, items: [] }] }); const first = f.c.load(); f.c.status.value = 'Cancelled'; await f.c.load(); d.resolve(result); await first; assert.deepEqual(f.c.rows.value, []);
});
test('unmount clears private data and ignores both late API responses', async () => {
  const d = deferred(), p = deferred(), f = fixture({ pages: [d.promise], profileResult: p.promise }); const first = f.mount(); f.cleanup(); d.resolve(result); p.resolve(profiles); await first;
  assert.deepEqual(f.c.profiles.value, []); assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.total.value, 0);
});
test('profile picker failure keeps safe all-owned list available', async () => {
  const f = fixture({ profileResult: new Error('Synthetic profile fault') }); await f.mount(); assert.ok(f.c.profileError.value); assert.deepEqual(f.c.rows.value, result.items); assert.equal(f.c.error.value, '');
});
