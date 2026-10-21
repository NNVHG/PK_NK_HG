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
const dentist = { dentistId: 31, dentistName: 'Synthetic dentist' };
function fixture({ searchResult = { items: [patient] }, booking, failure, dentistResult = [dentist] } = {}) {
  let cleanup, mount; const calls = [], searches = [], dentistReads = [], failurePlan = Array.isArray(failure) ? [...failure] : failure ? [failure] : [];
  const imports = {
    '@/composables/useBookingSchedule': { useBookingSchedule: () => ({ slots: { value: ['08:00', '08:30', '09:00', '09:30', '10:00', '10:30', '11:00', '11:30', '13:30', '14:00', '14:30', '15:00', '15:30', '16:00'] }, revision: { value: 'A'.repeat(64) }, duration: { value: 30 }, loading: { value: false }, error: { value: '' }, load: async () => {} }) },
    vue: { defineComponent: c => c, ref: value => ({ value }), watch() {}, onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/patients': { patientsService: { search: async query => { searches.push(query); return searchResult; } } },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => { dentistReads.push(true); if (dentistResult instanceof Error) throw dentistResult; return dentistResult; }, create: async request => {
      calls.push(request); const issue = failurePlan.length ? failurePlan.shift() : null;
      if (issue) throw issue === true ? { response: { data: { message: 'Synthetic rejection' } } } : issue;
      return booking ?? { appointmentId: 9, patientName: patient.fullName };
    } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, searches, dentistReads, mount: () => mount(), cleanup: () => cleanup() };
}
async function ready(f) { await f.mount(); f.c.keyword.value = 'Synthetic'; await f.c.search(); f.c.choose(patient); f.c.day.value = '2099-01-01'; f.c.slot.value = '08:30'; f.c.confirmed.value = true; }
test('search uses API with bounded page and selecting resets confirmation', async () => {
  const f = fixture(); await ready(f); assert.deepEqual(f.searches, [{ keyword: 'Synthetic', page: 1, pageSize: 20 }]);
  f.c.choose(patient); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.selected.value.patientId, 71);
});
test('confirmed booking sends selected patient and normal slot without dentist assignment', async () => {
  const f = fixture(); await ready(f); f.c.notes.value = '  Synthetic reason  '; await f.c.book();
  assert.deepEqual(f.calls, [{ patientId: 71, appointmentDate: '2099-01-01', slotTime: '08:30:00', dentistId: null, notes: 'Synthetic reason', isEmergency: false, emergencyReason: null, confirmedDentistConflictAppointmentId: null, expectedScheduleRevision: 'A'.repeat(64) }]);
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
test('counter loads minimal active dentist picker and sends selected ID', async () => {
  const f = fixture(); await ready(f); assert.deepEqual(f.c.dentists.value, [dentist]); f.c.dentistId.value = 31; await f.c.book();
  assert.equal(f.calls[0].dentistId, 31); assert.equal(f.dentistReads.length, 1);
});
test('unknown, negative and noninteger dentist IDs cannot book', async () => {
  for (const id of [-1, 32, 1.5]) { const f = fixture(); await ready(f); f.c.dentistId.value = id; await f.c.book(); assert.equal(f.calls.length, 0); }
});
test('dentist reload resets confirmation and drops stale selection', async () => {
  const rows = [dentist], f = fixture({ dentistResult: rows }); await ready(f); f.c.dentistId.value = 31; rows.splice(0); await f.c.loadDentists();
  assert.equal(f.c.dentistId.value, 0); assert.equal(f.c.confirmed.value, false); assert.deepEqual(f.c.dentists.value, []);
});
test('dentist load failure is explicit and supports deliberately unassigned booking', async () => {
  const f = fixture({ dentistResult: new Error('Synthetic failure') }); await ready(f); assert.ok(f.c.dentistError.value); await f.c.book(); assert.equal(f.calls[0].dentistId, null);
});
test('booking waits for dentist load and unmount ignores late dentist response', async () => {
  const d = deferred(), f = fixture({ dentistResult: d.promise }); const load = f.mount(); f.c.keyword.value = 'Synthetic'; await f.c.search(); f.c.choose(patient);
  f.c.day.value = '2099-01-01'; f.c.slot.value = '08:30'; f.c.confirmed.value = true; await f.c.book(); assert.equal(f.calls.length, 0);
  f.cleanup(); d.resolve([dentist]); await load; assert.deepEqual(f.c.dentists.value, []); assert.equal(f.c.dentistId.value, 0);
});
test('dentist conflict or ambiguous network outcome keeps draft without automatic retry', async () => {
  const f = fixture({ failure: true }); await ready(f); f.c.dentistId.value = 31; f.c.notes.value = 'Synthetic draft'; await f.c.book(); await f.c.book();
  assert.equal(f.calls.length, 1); assert.equal(f.c.notes.value, 'Synthetic draft'); assert.equal(f.c.dentistId.value, 31); assert.equal(f.c.confirmed.value, false);
});
test('emergency free-time booking requires reason and confirms the exact dentist conflict before retry', async () => {
  const conflictResponse = { response: { data: { code: 'APP_011', message: 'Synthetic conflict', details: {
    conflictAppointmentId: 88, dentistName: 'Synthetic dentist', patientName: 'Booked patient', appointmentDate: '2099-01-01', slotTime: '17:15:00'
  } } } };
  const f = fixture({ failure: [conflictResponse] }); await ready(f);
  f.c.isEmergency.value = true; f.c.toggleEmergency(); f.c.slot.value = '17:15'; f.c.emergencyReason.value = '  Đau răng dữ dội  ';
  f.c.dentistId.value = 31; f.c.confirmed.value = true;
  await f.c.book();
  assert.equal(f.calls.length, 1); assert.equal(f.c.conflict.value.conflictAppointmentId, 88);
  assert.equal(f.c.overrideConflictConfirmed.value, false); assert.equal(f.c.confirmed.value, true);
  f.c.overrideConflictConfirmed.value = true; await f.c.book();
  assert.equal(f.calls.length, 2);
  assert.equal(f.calls[0].isEmergency, true); assert.equal(f.calls[0].slotTime, '17:15:00');
  assert.equal(f.calls[0].emergencyReason, 'Đau răng dữ dội');
  assert.equal(f.calls[0].confirmedDentistConflictAppointmentId, null);
  assert.equal(f.calls[1].confirmedDentistConflictAppointmentId, 88);
  assert.equal(f.c.success.value.appointmentId, 9);
});

test('emergency Pending conflict uses distinct request id and preserves acknowledgement across warnings', async()=>{
 const pending={response:{data:{code:'APP_011',details:{conflictAppointmentId:0,conflictRequestId:17,dentistName:'Synthetic dentist',patientName:'Khách đang chờ xác nhận',appointmentDate:'2099-01-01',slotTime:'08:00:00'}}}};
 const existing={response:{data:{code:'APP_011',details:{conflictAppointmentId:88,dentistName:'Synthetic dentist',patientName:'Synthetic existing',appointmentDate:'2099-01-01',slotTime:'08:00:00'}}}};
 const f=fixture({failure:[pending,existing]});await ready(f);f.c.isEmergency.value=true;f.c.toggleEmergency();f.c.slot.value='17:15';f.c.emergencyReason.value='Synthetic urgent issue';f.c.dentistId.value=31;f.c.confirmed.value=true;
 await f.c.book();f.c.overrideConflictConfirmed.value=true;await f.c.book();assert.equal(f.calls[1].confirmedDentistConflictRequestId,17);assert.equal(f.calls[1].confirmedDentistConflictAppointmentId,null);
 f.c.overrideConflictConfirmed.value=true;await f.c.book();assert.equal(f.calls[2].confirmedDentistConflictRequestId,17);assert.equal(f.calls[2].confirmedDentistConflictAppointmentId,88);assert.ok(f.c.success.value);
});
