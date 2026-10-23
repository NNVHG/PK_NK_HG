import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/PatientDetailView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'post-treatment-addendum-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function fixture(role = 'DENTIST', userId = 5) {
  const calls = [];
  const notes = [{ id: 1, visitId: 10, createdByUserId: 5, createdByFullName: 'Bs A', createdByRoleName: 'Nha sĩ', note: 'Vết thương ổn', createdAt: '2026-10-15T10:00:00Z' }];
  let failAdd = false, addDeferred = null, readDeferred = null, forcedReply = null, cleanup;
  const auth = { role, user: { userId } };

  const imports = {
    vue: {
      defineComponent: x => x, ref: value => ({ value }), reactive: obj => obj,
      computed: fn => ({ get value() { return fn(); } }), onMounted() {}, onUnmounted(cb) { cleanup = cb; },
    },
    'vue-router': { useRoute: () => ({ params: { id: '1' } }) },
    axios: { default: { isAxiosError: () => false }, isAxiosError: () => false },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/components/SafetyAlertBanner.vue': {},
    '@/services/patients': {
      patientsService: {
        getById: async () => ({ patientId: 1, fullName: 'Nguyễn Văn B', dateOfBirth: '1990-01-01', gender: 'Male', phone: '0901234567' }),
        getTimeline: async () => ({ items: [], page: 1, totalPages: 1 }),
        getLatestMedicalHistory: async () => null,
        getMedicalHistoryArchive: async () => ({ items: [] }),
        getVitalSigns: async () => ({ items: [] }),
        getOpenVisits: async () => [],
      },
    },
    '@/components/PatientCitizenId.vue': { default: {} },
    '@/components/DentalImageAttachments.vue': { default: {} },
    '@/components/PatientPrescriptionHistory.vue': { default: {} },
    '@/services/visits': {
      visitsService: {
        getPostTreatmentNotes: async (visitId) => { calls.push(['getNotes', visitId]); if (readDeferred) await readDeferred.promise; return [...notes]; },
        addPostTreatmentNote: async (visitId, payload) => {
          calls.push(['addNote', visitId, payload]);
          if (addDeferred) await addDeferred.promise;
          if (failAdd) throw new Error('API failure');
          return forcedReply ?? { id: 2, visitId, createdByUserId: userId, createdByFullName: 'Bs A', createdByRoleName: 'Nha sĩ', note: payload.note, createdAt: '2026-10-16T12:00:00Z', requestId: payload.requestId };
        },
      },
    },
  };

  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => {
    if (!(name in imports)) throw new Error('Unexpected import: ' + name);
    return imports[name];
  }, module, module.exports);

  const c = module.exports.default.setup({}, { expose() {} });
  c.timeline.value = [{ visitId: 10, status: 'Completed', dentistId: 5 }];
  return {
    c, calls, notes, auth, cleanup: () => cleanup(),
    recover: () => { failAdd = false; forcedReply = null; },
    reply: value => { forcedReply = value; },
    deferRead: () => { let resolve; const promise = new Promise(r => { resolve = r; }); readDeferred = { promise, resolve }; return resolve; },
    setFailAdd: () => { failAdd = true; },
    deferAdd: () => {
      let resolve;
      const promise = new Promise(r => { resolve = r; });
      addDeferred = { promise, resolve };
      return resolve;
    },
  };
}

test('canAddPostTreatmentNote allows only Admin or assigned Dentist on Completed visit', () => {
  const fDentist = fixture('DENTIST', 5);
  const completedVisit = { visitId: 10, status: 'Completed', dentistId: 5 };
  const inProgressVisit = { visitId: 10, status: 'InProgress', dentistId: 5 };
  const otherDentistVisit = { visitId: 10, status: 'Completed', dentistId: 99 };

  assert.equal(fDentist.c.canAddPostTreatmentNote(completedVisit), true);
  assert.equal(fDentist.c.canAddPostTreatmentNote(inProgressVisit), false);
  assert.equal(fDentist.c.canAddPostTreatmentNote(otherDentistVisit), false);

  const fAdmin = fixture('ADMIN', 1);
  assert.equal(fAdmin.c.canAddPostTreatmentNote(completedVisit), true);
  assert.equal(fAdmin.c.canAddPostTreatmentNote(otherDentistVisit), true);
  assert.equal(fAdmin.c.canAddPostTreatmentNote(inProgressVisit), false);

  for (const role of ['RECEPTIONIST', 'ASSISTANT', 'PATIENT']) {
    const fOther = fixture(role, 5);
    assert.equal(fOther.c.canAddPostTreatmentNote(completedVisit), false);
  }
});

test('togglePostTreatmentNotes expands and loads notes on demand', async () => {
  const f = fixture();
  assert.equal(f.c.postTreatmentExpandedMap.value[10], undefined);

  await f.c.togglePostTreatmentNotes(10);
  assert.equal(f.c.postTreatmentExpandedMap.value[10], true);
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0][0], 'getNotes');
  assert.equal(f.c.postTreatmentNotesMap.value[10].length, 1);

  await f.c.togglePostTreatmentNotes(10);
  assert.equal(f.c.postTreatmentExpandedMap.value[10], false);
  await f.c.togglePostTreatmentNotes(10);
  assert.equal(f.c.postTreatmentExpandedMap.value[10], true);
  assert.equal(f.calls.length, 1);
});

test('submitAddendum rejects empty note and note > 1000 characters', async () => {
  const f = fixture();
  f.c.addendumDraftMap.value[10] = '   ';
  await f.c.submitAddendum(10);
  assert.equal(f.calls.length, 0);
  assert.match(f.c.addendumErrorMap.value[10], /không được để trống/);

  f.c.addendumDraftMap.value[10] = 'x'.repeat(1001);
  await f.c.submitAddendum(10);
  assert.equal(f.calls.length, 0);
  assert.match(f.c.addendumErrorMap.value[10], /không được vượt quá 1000/);
});

test('submitAddendum successfully adds note, clears draft, and retains notes on error', async () => {
  const f = fixture();
  f.c.addendumDraftMap.value[10] = '  Bệnh nhân hồi phục tốt.  ';
  await f.c.submitAddendum(10);

  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0][0], 'addNote');
  assert.equal(f.calls[0][2].note, 'Bệnh nhân hồi phục tốt.');
  assert.ok(f.calls[0][2].requestId);
  assert.equal(f.c.addendumDraftMap.value[10], '');
  assert.equal(f.c.postTreatmentNotesMap.value[10].length, 1);
  assert.match(f.c.addendumSuccessMap.value[10], /thành công/);

  f.setFailAdd();
  f.c.addendumDraftMap.value[10] = 'Ghi chú thử nghiệm lỗi';
  await f.c.submitAddendum(10);
  assert.equal(f.c.addendumDraftMap.value[10], 'Ghi chú thử nghiệm lỗi');
  assert.ok(f.c.addendumErrorMap.value[10]);
});

test('submitAddendum blocks double submit while saving', async () => {
  const f = fixture();
  const resolve = f.deferAdd();
  f.c.addendumDraftMap.value[10] = 'Kiểm tra double submit';

  const firstCall = f.c.submitAddendum(10);
  await f.c.submitAddendum(10);
  assert.equal(f.calls.length, 1);

  resolve();
  await firstCall;
  assert.equal(f.calls.length, 1);
});


test('unknown dentist assignment, stale visit, or changed role never submits', async () => {
  const f = fixture(); assert.equal(f.c.canAddPostTreatmentNote({ status: 'Completed', dentistId: null }), false);
  f.c.addendumDraftMap.value[99] = 'Synthetic note'; await f.c.submitAddendum(99); assert.equal(f.calls.length, 0);
  f.c.addendumDraftMap.value[10] = 'Synthetic note'; f.auth.role = 'RECEPTIONIST'; await f.c.submitAddendum(10); assert.equal(f.calls.length, 0);
});
test('uncertain retry uses the same key and content', async () => {
  const f = fixture(); f.setFailAdd(); f.c.addendumDraftMap.value[10] = 'Synthetic note'; await f.c.submitAddendum(10);
  const first = f.calls[0][2]; f.c.addendumDraftMap.value[10] = 'Changed'; await f.c.submitAddendum(10); assert.equal(f.calls.length, 1);
  f.c.addendumDraftMap.value[10] = 'Synthetic note'; f.recover(); await f.c.submitAddendum(10);
  assert.deepEqual(f.calls[1][2], first); assert.equal(f.c.postTreatmentNotesMap.value[10].length, 1);
});
test('unmount clears clinical drafts and ignores late write response', async () => {
  const f = fixture(); const resolve = f.deferAdd(); f.c.addendumDraftMap.value[10] = 'Synthetic note'; const pending = f.c.submitAddendum(10);
  f.cleanup(); resolve(); await pending; assert.deepEqual(f.c.addendumDraftMap.value, {}); assert.deepEqual(f.c.postTreatmentNotesMap.value, {});
  assert.deepEqual(f.c.addendumSuccessMap.value, {});
});
test('manual reload reconciles an already committed request after lost response', async () => {
  const f = fixture(); f.setFailAdd(); f.c.addendumDraftMap.value[10] = 'Synthetic note'; await f.c.submitAddendum(10);
  f.notes.push({ id: 2, visitId: 10, createdByUserId: 5, note: 'Synthetic note', requestId: f.calls[0][2].requestId });
  await f.c.loadPostTreatmentNotes(10); assert.equal(f.c.addendumDraftMap.value[10], '');
  f.recover(); f.c.addendumDraftMap.value[10] = 'New note'; await f.c.submitAddendum(10);
  assert.notEqual(f.calls[2][2].requestId, f.calls[0][2].requestId);
});

test('read in progress prevents append and unmount ignores late clinical history', async () => {
  const f = fixture(); const resolve = f.deferRead(); const pending = f.c.loadPostTreatmentNotes(10);
  f.c.addendumDraftMap.value[10] = 'Synthetic note'; await f.c.submitAddendum(10); assert.equal(f.calls.length, 1);
  f.cleanup(); resolve(); await pending; assert.deepEqual(f.c.postTreatmentNotesMap.value, {});
});
test('mismatched response is not acknowledged and retry retains original key', async () => {
  const f = fixture(); f.reply({ id: 99, visitId: 88, createdByUserId: 5, note: 'Wrong', requestId: 'wrong' });
  f.c.addendumDraftMap.value[10] = 'Synthetic note'; await f.c.submitAddendum(10);
  assert.equal(f.c.addendumDraftMap.value[10], 'Synthetic note'); assert.equal(f.c.addendumSuccessMap.value[10], '');
  const first = f.calls[0][2]; f.recover(); await f.c.submitAddendum(10); assert.deepEqual(f.calls[1][2], first);
});
test('role changed during history read cannot expose the late response', async () => {
  const f = fixture(); const resolve = f.deferRead(); const pending = f.c.loadPostTreatmentNotes(10);
  f.auth.role = 'PATIENT'; resolve(); await pending; assert.deepEqual(f.c.postTreatmentNotesMap.value, {});
});
