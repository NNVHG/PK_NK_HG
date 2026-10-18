import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/ClinicalDiagnosisView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'selected-start-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const row = { queueEntryId: 7, visitId: 8, patientId: 1, queueNumber: 1, status: 1, dentistId: null, patientName: 'Synthetic', patientCode: 'TEST' };
const visit = { visitId: 8, patientId: 1, status: 'Created', dentistId: null, isLocked: false, diagnosis: null, clinicalNotes: null };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'DENTIST', failure = false, dentists = [{ dentistId: 5, dentistName: 'Synthetic dentist' }], pending, readFailureAfter = Infinity } = {}) {
  let mount, cleanup; const writes = [], diagnoses = [], reads = [], doctorReads = [];
  const state = { row: { ...row }, visit: { ...visit } };
  const imports = {
    '@/components/DiagnosisAutocomplete.vue': { default: {} },
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { default: { isAxiosError: e => !!e.response } },
    '@/stores/auth': { useAuthStore: () => ({ role, user: { userId: 5 } }) },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => { doctorReads.push(true); if (dentists instanceof Error) throw dentists; return dentists; } } },
    '@/services/queue': { queueService: {
      getToday: async () => ({ items: [state.row], page: 1, totalPages: 1 }),
      getEntry: async () => state.row,
      startConsultation: async (...args) => { writes.push(args); if (failure) throw { response: { data: { message: 'Synthetic409' } } }; if (pending) await pending;
        state.row = { ...state.row, status: 2, dentistId: args[1] }; state.visit = { ...state.visit, status: 'InProgress', dentistId: args[1] }; return state.row; },
    } },
    '@/services/visits': { visitsService: {
      getById: async () => { reads.push(true); if (reads.length > readFailureAfter) throw new Error('Synthetic read error'); return state.visit; },
      updateDiagnosis: async (id, body) => { diagnoses.push([id, body]); state.visit = { ...state.visit, ...body }; return state.visit; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} }); return { c, state, writes, diagnoses, reads, doctorReads, mount: () => mount(), cleanup: () => cleanup() };
}
test('dentist selected start uses self then permits saving actual diagnosis', async () => {
  const f = fixture(); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation();
  assert.deepEqual(f.writes, [[7, 5]]); assert.equal(f.c.canEdit.value, true); assert.equal(f.doctorReads.length, 0);
  f.c.diagnosis.value = 'Synthetic diagnosis'; await f.c.saveDiagnosis(); assert.equal(f.diagnoses.length, 1); assert.equal(f.c.visit.value.diagnosis, 'Synthetic diagnosis');
});
test('Admin must select a loaded active dentist', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); assert.equal(f.writes.length, 0);
  f.c.startDentistId.value = 5; await f.c.startConsultation(); assert.deepEqual(f.writes, [[7, 5]]);
});
test('failed picker or unknown selection cannot start', async () => {
  for (const dentists of [new Error('Synthetic'), []]) { const f = fixture({ role: 'ADMIN', dentists }); await f.mount(); await f.c.selectEntry(f.state.row); f.c.startDentistId.value = 5; await f.c.startConsultation(); assert.equal(f.writes.length, 0); }
});
test('denied roles cannot start or edit', async () => {
  for (const role of ['RECEPTIONIST', 'ASSISTANT', 'PATIENT']) { const f = fixture({ role }); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); assert.equal(f.writes.length, 0); assert.equal(f.c.canStart.value, false); }
});
test('another dentists assigned entry or locked visit cannot start', async () => {
  for (const blocked of ['FOREIGN', 'LOCKED']) { const f = fixture(); if (blocked === 'FOREIGN') f.state.row.dentistId = 6; else f.state.visit.isLocked = true;
    await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); assert.equal(f.writes.length, 0); }
});
test('double click sends one selected start', async () => {
  const d = deferred(), f = fixture({ pending: d.promise }); await f.mount(); await f.c.selectEntry(f.state.row);
  const first = f.c.startConsultation(); await f.c.startConsultation(); assert.equal(f.writes.length, 1); d.resolve(); await first;
});
test('conflict requires reloading both queue and Visit without auto retry', async () => {
  const f = fixture({ failure: true }); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); await f.c.startConsultation();
  assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true);
  f.state.row = { ...f.state.row, status: 2 }; f.state.visit = { ...f.state.visit, status: 'InProgress', dentistId: 5 };
  await f.c.reloadVisit(); assert.equal(f.c.selected.value.status, 2); assert.equal(f.c.canStart.value, false); assert.equal(f.writes.length, 1);
});
test('successful start with failed refresh does not unlock writes', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation();
  assert.equal(f.c.needsReview.value, true); assert.equal(f.c.visit.value, null); await f.c.startConsultation(); assert.equal(f.writes.length, 1);
});
test('unmount ignores pending start and clears clinical information', async () => {
  const d = deferred(), f = fixture({ pending: d.promise }); await f.mount(); await f.c.selectEntry(f.state.row); const first = f.c.startConsultation(); f.cleanup(); d.resolve(); await first;
  assert.equal(f.c.visit.value, null); assert.equal(f.c.selected.value, null); assert.equal(f.c.diagnosis.value, ''); assert.equal(f.reads.length, 1);
});
test('dirty diagnosis blocks switching patient or queue reload', async () => {
  const f = fixture(); await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); f.c.diagnosis.value = 'Unsaved';
  await f.c.selectEntry({ ...row, queueEntryId: 99 }); await f.c.loadQueue(); assert.equal(f.c.selected.value.queueEntryId, 7);
});

test('Calling stays uneditable until explicit Dentist/Admin clinical start', async () => {
  for (const role of ['DENTIST', 'ADMIN']) {
    const f = fixture({ role }); f.state.row.status = 6; f.state.row.dentistId = 5;
    await f.mount(); await f.c.selectEntry(f.state.row); assert.equal(f.c.canEdit.value, false); assert.equal(f.c.canStart.value, true);
    assert.equal(f.writes.length, 0); await f.c.startConsultation(); assert.deepEqual(f.writes, [[7, 5]]); assert.equal(f.c.canEdit.value, true);
  }
});
test('Assistant cannot start an invited Calling entry', async () => {
  const f = fixture({ role: 'ASSISTANT' }); f.state.row.status = 6; f.state.row.dentistId = 5;
  await f.mount(); await f.c.selectEntry(f.state.row); await f.c.startConsultation(); assert.equal(f.writes.length, 0); assert.equal(f.c.canEdit.value, false);
});


test('clinical screen shows actual destination and links to ready workspace before physical start', async () => {
  const source = await readFile(new URL('../src/views/ClinicalDiagnosisView.vue', import.meta.url), 'utf8');
  assert.ok(source.includes("selected.roomName || 'Chưa có phòng'"));
  assert.ok(source.includes("selected.chairNumber || 'Chưa có ghế'"));
  assert.ok(source.includes('to="/staff/workspace"'));
  assert.ok(source.includes('Bắt đầu khám sẽ chiếm ghế'));
});
