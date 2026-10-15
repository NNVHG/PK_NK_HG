import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'imaging-routing' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const row = { queueEntryId: 7, status: 6, canMarkAbsent: true, dentistId: 5, dentistName: 'Synthetic dentist', queueNumber: 1,
  patientName: 'Synthetic patient', patientCode: 'TEST', queueDate: '2026-10-16' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'DENTIST', selected = row, fail = false, result, readFailureAfter = Infinity } = {}) {
  let cleanup, mount; const reads = [], writes = [], calls = []; const auth = { role, user: { userId: 5 } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => [{ dentistId: 5, dentistName: 'Synthetic dentist' }] } },
    '@/services/queue': { queueService: {
      getCallingDentists: async () => [{ dentistId: 5, dentistName: 'Synthetic dentist' }],
      getToday: async () => { reads.push(true); if (reads.length > readFailureAfter) throw new Error('Read failure'); return { items: [selected], page: 1, totalCount: 1, hasNextPage: false }; },
      changeAbsence: async (...args) => { writes.push(args); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...selected, status: args[1] ? 1 : 7 }; },
      callNext: async () => { calls.push(true); return row; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, calls, auth, mount: () => mount(), cleanup: () => cleanup() };
}
test('open and back never mutate, confirmation required for skip and recall', async () => {
  for (const status of [6, 7]) {
    const selected = { ...row, status }, f = fixture({ role: 'RECEPTIONIST', selected }); await f.mount(); f.c.openAbsence(selected);
    await f.c.handleAbsence(); assert.equal(f.writes.length, 0); f.c.absenceReason.value = 'draft'; f.c.closeAbsence();
    assert.equal(f.c.absenceRow.value, null); assert.equal(f.c.absenceReason.value, '');
    f.c.openAbsence(selected); f.c.absenceConfirmed.value = true; f.c.absenceReason.value = '  Synthetic note  '; await f.c.handleAbsence();
    assert.deepEqual(f.writes, [[7, status === 7, 'Synthetic note']]); assert.equal(f.reads.length, 2); assert.equal(f.c.absenceRow.value, null);
  }
});
test('skip requires server eligibility and own dentist; recall only receptionist/admin', async () => {
  for (const [role, status, eligible, doctor] of [
    ['ASSISTANT', 6, true, 5], ['PATIENT', 6, true, 5], ['DENTIST', 6, true, 6], ['DENTIST', 6, false, 5],
    ['ADMIN', 6, false, 5], ['DENTIST', 7, true, 5], ['ASSISTANT', 7, true, 5], ['PATIENT', 7, true, 5], ['ADMIN', 1, true, 5],
  ]) {
    const selected = { ...row, status, canMarkAbsent: eligible, dentistId: doctor }, f = fixture({ role, selected }); await f.mount(); f.c.openAbsence(selected);
    f.c.absenceConfirmed.value = true; await f.c.handleAbsence(); assert.equal(f.writes.length, 0); assert.equal(f.c.absenceRow.value, null);
  }
});
test('admin and assigned dentist may skip an eligible invitation', async () => {
  for (const role of ['ADMIN', 'DENTIST']) { const f = fixture({ role }); await f.mount(); f.c.openAbsence(row); f.c.absenceConfirmed.value = true; await f.c.handleAbsence(); assert.equal(f.writes.length, 1); }
});
test('double click and concurrent panels cannot write or reload while changing absence', async () => {
  const d = deferred(), f = fixture({ role: 'ADMIN', result: d.promise }); await f.mount(); f.c.callDentistId.value = 5;
  f.c.openAbsence(row); f.c.absenceConfirmed.value = true; const pending = f.c.handleAbsence();
  await f.c.handleAbsence(); await f.c.handleCallNext(); await f.c.load(); f.c.closeAbsence(); f.c.openCancellation({ ...row, status: 1 }); f.c.openImaging({ ...row, status: 2 });
  assert.equal(f.writes.length, 1); assert.equal(f.calls.length, 0); assert.equal(f.reads.length, 1); assert.ok(f.c.absenceRow.value);
  assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.imagingRow.value, null); d.resolve({ ...row, status: 7 }); await pending;
});
test('open absence panel prevents call-next even before confirmation', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); f.c.callDentistId.value = 5; f.c.openAbsence(row); await f.c.handleCallNext(); assert.equal(f.calls.length, 0);
});
test('500 character note accepted and 501 rejected', async () => {
  for (const length of [500, 501]) { const f = fixture(); await f.mount(); f.c.openAbsence(row); f.c.absenceConfirmed.value = true; f.c.absenceReason.value = 'x'.repeat(length); await f.c.handleAbsence(); assert.equal(f.writes.length, length === 500 ? 1 : 0); }
});
test('failed or malformed write requires reload without automatic retry', async () => {
  for (const options of [{ fail: true }, { result: { ...row, status: 1 } }]) {
    const f = fixture(options); await f.mount(); f.c.openAbsence(row); f.c.absenceConfirmed.value = true;
    await f.c.handleAbsence(); await f.c.handleAbsence(); assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true);
    assert.equal(f.c.absenceConfirmed.value, false); assert.equal(f.c.actionMsg.value, ''); await f.c.load(); assert.equal(f.c.absenceRow.value, null); assert.equal(f.c.needsReview.value, false);
  }
});
test('successful write with read failure stays blocked', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.openAbsence(row); f.c.absenceConfirmed.value = true; await f.c.handleAbsence();
  assert.equal(f.c.needsReview.value, true); assert.ok(f.c.actionMsg.value); await f.c.handleAbsence(); assert.equal(f.writes.length, 1);
});
test('unmount clears drafts and ignores late response', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.openAbsence(row); f.c.absenceConfirmed.value = true; f.c.absenceReason.value = 'draft';
  const pending = f.c.handleAbsence(); f.cleanup(); d.resolve({ ...row, status: 7 }); await pending;
  assert.equal(f.c.absenceRow.value, null); assert.equal(f.c.absenceReason.value, ''); assert.equal(f.c.actionMsg.value, ''); assert.equal(f.reads.length, 1);
});
test('role change, invalid and stale rows cannot submit', async () => {
  const f = fixture(); await f.mount(); f.c.openAbsence({ ...row, queueEntryId: 99 }); assert.equal(f.c.absenceRow.value, null);
  f.c.openAbsence(row); f.c.absenceConfirmed.value = true; f.auth.role = 'ASSISTANT'; await f.c.handleAbsence(); assert.equal(f.writes.length, 0);
});
test('Skipped filter and protected dedicated endpoints are wired', async () => {
  const f = fixture(); f.c.status.value = 7; await f.c.load(); assert.equal(f.reads.length, 1);
  const api = await readFile(new URL('../src/services/queue.ts', import.meta.url), 'utf8');
  assert.match(api, /recall \? 'recall' : 'skip'/); assert.match(api, /confirmed: true, reason/); assert.match(source, /Vắng mặt/);
});
