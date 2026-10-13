import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/CounterBookingView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'counter-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const patient = { patientId: 71, fullName: 'Synthetic patient', dateOfBirth: '1990-01-01', patientCode: 'BN71' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ searchResult = { items: [patient] }, booking, failure } = {}) {
  let cleanup; const calls = [], searches = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/patients': { patientsService: { search: async query => { searches.push(query); return searchResult; } } },
    '@/services/appointments': { appointmentsService: { create: async request => {
      calls.push(request); if (failure) throw { response: { data: { message: 'Synthetic rejection' } } };
      return booking ?? { appointmentId: 9, patientName: patient.fullName };
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, searches, cleanup: () => cleanup() };
}
async function ready(f) { f.c.keyword.value = 'Synthetic'; await f.c.search(); f.c.choose(patient); f.c.day.value = '2099-01-01'; f.c.slot.value = '08:30'; f.c.confirmed.value = true; }
test('search uses API with bounded page and selecting resets confirmation', async () => {
  const f = fixture(); await ready(f); assert.deepEqual(f.searches, [{ keyword: 'Synthetic', page: 1, pageSize: 20 }]);
  f.c.choose(patient); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.selected.value.patientId, 71);
});
test('confirmed booking sends selected patient and normal slot without dentist assignment', async () => {
  const f = fixture(); await ready(f); f.c.notes.value = '  Synthetic reason  '; await f.c.book();
  assert.deepEqual(f.calls, [{ patientId: 71, appointmentDate: '2099-01-01', slotTime: '08:30:00', notes: 'Synthetic reason' }]);
  assert.equal(f.c.confirmed.value, false); assert.equal(f.c.success.value.appointmentId, 9);
});
test('unconfirmed, past date, wrong slot and long notes do not create appointments', async () => {
  for (const [key, value] of [['confirmed', false], ['day', '2000-01-01'], ['slot', '12:00'], ['notes', 'x'.repeat(501)]]) {
    const f = fixture(); await ready(f); f.c[key].value = value; await f.c.book(); assert.equal(f.calls.length, 0);
  }
});
test('double submit and repeat after success do not create extra appointments', async () => {
  const d = deferred(), f = fixture({ booking: d.promise }); await ready(f); const first = f.c.book(); await f.c.book();
  assert.equal(f.calls.length, 1); d.resolve({ appointmentId: 9 }); await first; f.c.confirmed.value = true; await f.c.book(); assert.equal(f.calls.length, 1);
});
test('server rejection preserves draft and requires confirmation again', async () => {
  const f = fixture({ failure: true }); await ready(f); f.c.notes.value = 'Synthetic reason'; await f.c.book();
  assert.equal(f.c.notes.value, 'Synthetic reason'); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.error.value, 'Synthetic rejection');
});
test('unmount ignores pending search and clears private profile state', async () => {
  const d = deferred(), f = fixture({ searchResult: d.promise }); f.c.keyword.value = 'Synthetic'; const request = f.c.search(); f.cleanup(); d.resolve({ items: [patient] }); await request;
  assert.deepEqual(f.c.patients.value, []); assert.equal(f.c.selected.value, null);
});
test('unmount ignores late booking success', async () => {
  const d = deferred(), f = fixture({ booking: d.promise }); await ready(f); const request = f.c.book(); f.cleanup(); d.resolve({ appointmentId: 9 }); await request;
  assert.equal(f.c.success.value, null); assert.equal(f.c.notes.value, '');
});
test('unknown profile cannot be selected and empty search does not call API', async () => {
  const f = fixture(); await f.c.search(); f.c.choose(patient); assert.equal(f.searches.length, 0); assert.equal(f.c.selected.value, null);
});
