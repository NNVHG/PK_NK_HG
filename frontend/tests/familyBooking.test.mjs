import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/AppointmentsView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'family-booking' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const members = [{ patientId: 1, fullName: 'Synthetic adult', dateOfBirth: '1990-01-02', patientCode: 'BN1' },
  { patientId: 2, fullName: 'Synthetic child', dateOfBirth: '2020-03-04', patientCode: 'BN2' }];
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ profiles = members, booking, failure } = {}) {
  let mount, cleanup; const calls = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }),
      onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e.response) },
    '@/services/patients': { patientsService: { getMyProfiles: async () => profiles } },
    '@/services/appointments': { appointmentsService: { create: async payload => {
      calls.push(payload); if (failure) throw { response: { data: { message: 'Synthetic error' } } };
      return booking ?? { patientName: 'Synthetic child', appointmentDate: payload.appointmentDate, slotTime: payload.slotTime };
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  c.patientId.value = 2; c.appointmentDate.value = '2026-12-01'; c.slotTime.value = '08:00';
  return { c, calls, mount: () => mount(), cleanup: () => cleanup() };
}
test('loads both family members without automatically choosing one', async () => {
  const f = fixture(); f.c.patientId.value = 0; await f.mount();
  assert.deepEqual(f.c.profiles.value, members); assert.equal(f.c.patientId.value, 0);
  assert.equal(f.c.formatDate('2020-03-04'), '4/3/2020');
  assert.ok(descriptor.template.content.includes('formatDate(profile.dateOfBirth)'));
});
test('booking sends the explicitly selected family member', async () => {
  const f = fixture(); await f.mount(); f.c.notes.value = 'Synthetic reason'; await f.c.submit();
  assert.deepEqual(f.calls, [{ patientId: 2, appointmentDate: '2026-12-01', slotTime: '08:00:00', notes: 'Synthetic reason' }]);
  assert.equal(f.c.notes.value, ''); assert.equal(f.c.success.value.patientName, 'Synthetic child');
});
test('unknown or missing profile cannot be submitted', async () => {
  for (const id of [0, 3, 1.5]) { const f = fixture(); await f.mount(); f.c.patientId.value = id; await f.c.submit(); assert.equal(f.calls.length, 0); }
});
test('date, slot and note length validated before request', async () => {
  for (const [key, value] of [['appointmentDate', ''], ['slotTime', '12:00'], ['notes', 'x'.repeat(501)]]) {
    const f = fixture(); await f.mount(); f.c[key].value = value; await f.c.submit(); assert.equal(f.calls.length, 0);
  }
});
test('loading and duplicate submit are guarded', async () => {
  const d = deferred(), f = fixture({ booking: d.promise }); await f.c.submit(); assert.equal(f.calls.length, 0);
  await f.mount(); const first = f.c.submit(); await f.c.submit(); assert.equal(f.calls.length, 1); d.resolve({ patientName: 'Synthetic child' }); await first;
});
test('failure retains selected member and unsaved notes', async () => {
  const f = fixture({ failure: true }); await f.mount(); f.c.notes.value = 'Synthetic reason'; await f.c.submit();
  assert.equal(f.c.patientId.value, 2); assert.equal(f.c.notes.value, 'Synthetic reason'); assert.equal(f.c.errorMessage.value, 'Synthetic error');
});
test('unmount prevents late profile data restoring private state', async () => {
  const d = deferred(), f = fixture({ profiles: d.promise }); const loading = f.mount(); f.cleanup(); d.resolve(members); await loading;
  assert.deepEqual(f.c.profiles.value, []); assert.equal(f.c.patientId.value, 0);
});
test('unmount ignores late booking response and clears draft', async () => {
  const d = deferred(), f = fixture({ booking: d.promise }); await f.mount(); const first = f.c.submit(); f.cleanup(); d.resolve({ patientName: 'Synthetic child' }); await first;
  assert.equal(f.c.success.value, null); assert.equal(f.c.notes.value, ''); assert.deepEqual(f.c.profiles.value, []);
});
