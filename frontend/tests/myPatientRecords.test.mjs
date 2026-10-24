import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/MyPatientRecordView.vue', import.meta.url), 'utf8');
const compiled = ts.transpileModule(compileScript(parse(source).descriptor, { id: 'my-records-test' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
async function fixture(role = 'PATIENT') {
  let cleanup, watcher, failure, deferred;
  const auth = { role, user: { userId: 22 } }, calls = [];
  let response = { patientId: 41, fullName: 'Synthetic patient', patientCode: 'BN000041', dateOfBirth: '2010-01-01' };
  const imports = { vue: { defineComponent: c => c, ref: value => ({ value }), watch: (_f, cb) => { watcher = cb; cb(); },
    onUnmounted: cb => { cleanup = cb; } }, '@/stores/auth': { useAuthStore: () => auth },
    '@/services/patients': { patientsService: { getMyPatient: async (...args) => { calls.push(args); if (deferred) return deferred;
      if (failure) throw failure; return response; } } } };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(n => imports[n], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} }); await Promise.resolve(); await Promise.resolve();
  return { c, auth, calls, reload: () => watcher(), cleanup: () => cleanup(), fail: () => { failure = Error('Synthetic'); },
    data: value => { response = value; }, defer: () => { let resolve; deferred = new Promise(done => { resolve = done; }); return resolve; } };
}
test('portal loads one personal profile from the current-user API without submitting an owner id', async () => {
  const f = await fixture(); assert.equal(f.c.patient.value.patientId, 41); assert.deepEqual(f.calls, [[]]);
  const service = await readFile(new URL('../src/services/patients.ts', import.meta.url), 'utf8');
  assert.match(service, /getMyPatient[\s\S]*?['"]\/patients\/me['"]/);
  assert.doesNotMatch(source, /getMyProfiles|v-for|gia đình/);
});
test('staff or logged-out users never request the patient portal list', async () => {
  for (const role of ['', 'ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT']) {
    const f = await fixture(role); assert.equal(f.calls.length, 0); assert.equal(f.c.patient.value, null);
  }
});
test('load failure clears previous profiles rather than showing stale health links', async () => {
  const f = await fixture(); f.fail(); await f.reload(); assert.equal(f.c.patient.value, null); assert.ok(f.c.error.value);
});
test('unmount ignores an outstanding profile response', async () => {
  const f = await fixture(); const resolve = f.defer(); const load = f.reload(); f.cleanup();
  resolve({ patientId: 42, fullName: 'Late' }); await load; assert.equal(f.c.patient.value, null);
});
test('role changes clear data before another account can see it', async () => {
  const f = await fixture(); f.auth.role = 'ADMIN'; await f.reload(); assert.equal(f.c.patient.value, null); assert.equal(f.calls.length, 1);
});
test('invalid profile identifiers fail closed', async () => {
  for (const data of [undefined, {}, [], { patientId: -1, fullName: 'Bad' }, { patientId: 1.2, fullName: 'Bad' }]) {
    const f = await fixture(); f.data(data); await f.reload(); assert.equal(f.c.patient.value, null); assert.ok(f.c.error.value);
  }
});
test('empty profile response is an honest empty state', async () => {
  const f = await fixture(); f.data(null); await f.reload(); assert.equal(f.c.patient.value, null); assert.equal(f.c.error.value, '');
  assert.match(source, /Chưa có hồ sơ đang hoạt động/);
});
test('portal links only to server-returned profile ids, with no create or edit action', () => {
  assert.match(source, /name: 'patient-detail', params: \{ id: patient.patientId \}/);
  assert.doesNotMatch(source, /createVisit|updatePatient|localStorage|console\.log/);
});
test('router and back link make current-user records reachable without opening the staff list', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
  const detail = await readFile(new URL('../src/views/PatientDetailView.vue', import.meta.url), 'utf8');
  assert.match(router, /name: 'my-patient-record',[\s\S]*?roles: \['PATIENT'\]/);
  assert.match(router, /name: 'my-patient-records',[\s\S]*?redirect: \{ name: 'my-patient-record' \}/);
  assert.match(router, /name: 'patient-detail',[\s\S]*?roles: \['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT'\]/);
  assert.match(detail, /authStore.role === 'PATIENT' \? 'my-patient-record' : 'patients'/);
  assert.match(detail, /canEdit = computed\(\(\) => \['ADMIN', 'RECEPTIONIST'\]/);
  assert.match(detail, /SafetyAlertBanner v-if="authStore.role !== 'PATIENT'"/);
});
