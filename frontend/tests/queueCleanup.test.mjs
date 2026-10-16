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
      cleanupExpired: async (...args) => { writes.push(args); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { cancelledCount: 3, lastExpiredDate: '2026-10-15' }; },
      callNext: async () => { calls.push(true); return row; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, calls, auth, mount: () => mount(), cleanup: () => cleanup() };
}
test('cleanup needs explicit confirmation and back clears it without writes', async () => {
  const f = fixture({ role: 'RECEPTIONIST' }); await f.mount(); f.c.openCleanup(); await f.c.handleCleanup(); assert.equal(f.writes.length, 0);
  f.c.cleanupConfirmed.value = true; f.c.closeCleanup(); assert.equal(f.c.cleanupOpen.value, false); assert.equal(f.c.cleanupConfirmed.value, false);
});
test('Admin and Receptionist submit one confirmed cleanup then reload actual queue', async () => {
  for (const role of ['ADMIN', 'RECEPTIONIST']) { const f = fixture({ role }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true;
    await f.c.handleCleanup(); assert.deepEqual(f.writes, [[]]); assert.equal(f.reads.length, 2); assert.match(f.c.actionMsg.value, /3 lượt/); assert.equal(f.c.cleanupOpen.value, false); }
});
test('Dentist Assistant and Patient cannot trigger cleanup', async () => {
  for (const role of ['DENTIST', 'ASSISTANT', 'PATIENT']) { const f = fixture({ role }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true; await f.c.handleCleanup(); assert.equal(f.writes.length, 0); assert.equal(f.c.cleanupOpen.value, false); }
});
test('pending cleanup blocks reload, duplicate writes, close and unrelated actions', async () => {
  const d = deferred(), f = fixture({ role: 'ADMIN', result: d.promise }); await f.mount(); f.c.callDentistId.value = 5;
  f.c.openCleanup(); f.c.cleanupConfirmed.value = true; const pending = f.c.handleCleanup(); await f.c.handleCleanup(); await f.c.load(); await f.c.handleCallNext();
  f.c.closeCleanup(); f.c.openAbsence(row); f.c.openCancellation({ ...row, status: 1 }); f.c.openImaging({ ...row, status: 2 });
  assert.equal(f.writes.length, 1); assert.equal(f.calls.length, 0); assert.equal(f.reads.length, 1); assert.equal(f.c.cleanupOpen.value, true);
  assert.equal(f.c.absenceRow.value, null); assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.imagingRow.value, null);
  d.resolve({ cancelledCount: 1, lastExpiredDate: '2026-10-15' }); await pending;
});
test('opening cleanup prevents other panels even before submitting', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); f.c.openCleanup(); f.c.openAbsence(row); await f.c.handleCallNext();
  assert.equal(f.c.absenceRow.value, null); assert.equal(f.calls.length, 0);
});
test('cleanup does not open when another panel is selected', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); f.c.openAbsence(row); f.c.openCleanup(); assert.equal(f.c.cleanupOpen.value, false);
});
test('failure requires reload and a fresh confirmation, never retries', async () => {
  const f = fixture({ role: 'RECEPTIONIST', fail: true }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true;
  await f.c.handleCleanup(); await f.c.handleCleanup(); assert.equal(f.writes.length, 1); assert.equal(f.c.cleanupConfirmed.value, false);
  assert.equal(f.c.needsReview.value, true); assert.equal(f.c.error.value, 'Synthetic409'); await f.c.load(); assert.equal(f.c.cleanupOpen.value, false); assert.equal(f.c.needsReview.value, false);
});
test('zero count is honest, malformed result never claims success', async () => {
  for (const result of [{ cancelledCount: 0, lastExpiredDate: '2026-10-15' }, { cancelledCount: -1, lastExpiredDate: '2026-10-15' }]) {
    const f = fixture({ role: 'ADMIN', result }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true; await f.c.handleCleanup();
    assert.equal(Boolean(f.c.actionMsg.value), result.cancelledCount === 0);
  }
});
test('refresh failure after cleanup keeps writes blocked', async () => {
  const f = fixture({ role: 'ADMIN', readFailureAfter: 1 }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true; await f.c.handleCleanup();
  assert.equal(f.c.needsReview.value, true); f.c.openCleanup(); assert.equal(f.c.cleanupOpen.value, false);
});
test('unmount ignores late cleanup result and clears confirmation', async () => {
  const d = deferred(), f = fixture({ role: 'ADMIN', result: d.promise }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true;
  const pending = f.c.handleCleanup(); f.cleanup(); d.resolve({ cancelledCount: 2, lastExpiredDate: '2026-10-15' }); await pending;
  assert.equal(f.c.cleanupOpen.value, false); assert.equal(f.c.cleanupConfirmed.value, false); assert.equal(f.c.actionMsg.value, ''); assert.equal(f.reads.length, 1);
});
test('role change before confirmation prevents cleanup write', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); f.c.openCleanup(); f.c.cleanupConfirmed.value = true; f.auth.role = 'DENTIST'; await f.c.handleCleanup(); assert.equal(f.writes.length, 0);
});
test('cleanup UI confirmation remains enabled while its own panel is open and API sends no date', async () => {
  assert.match(source, /:disabled="writing \|\| loading \|\| needsReview \|\| !cleanupConfirmed"/);
  const api = await readFile(new URL('../src/services/queue.ts', import.meta.url), 'utf8'); assert.match(api, /post<QueueCleanupResponse>\('\/queue\/cleanup-expired', \{ confirmed: true \}\)/);
});
