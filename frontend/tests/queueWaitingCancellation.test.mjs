import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'cancel-waiting-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const row = { queueEntryId: 7, status: 1, queueNumber: 1, patientName: 'Synthetic patient', patientCode: 'TEST', queueDate: '2026-10-14' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'RECEPTIONIST', failure = false, result = row, readFailureAfter = Infinity } = {}) {
  let cleanup, mount; const reads = [], writes = [], calls = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => ({ role }) },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => [{ dentistId: 5, dentistName: 'Synthetic dentist' }] } },
    '@/services/queue': { queueService: {
      getToday: async () => { reads.push(true); if (reads.length > readFailureAfter) throw new Error('Synthetic read error'); return { items: [row], page: 1, totalCount: 1, hasNextPage: false }; },
      cancelWaiting: async (...args) => { writes.push(args); if (failure) throw { response: { data: { message: 'Synthetic409' } } }; return result; },
      callNext: async () => { calls.push(true); return row; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} }); return { c, reads, writes, calls, mount: () => mount(), cleanup: () => cleanup() };
}
test('opening and closing cancellation does not mutate queue', async () => {
  const f = fixture(); await f.mount(); f.c.openCancellation(row); assert.equal(f.c.cancelRow.value.queueEntryId, 7);
  f.c.cancelReason.value = 'draft'; f.c.closeCancellation(); assert.equal(f.writes.length, 0); assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.cancelReason.value, '');
});
test('confirmation required and successful cancellation reloads list', async () => {
  for (const role of ['ADMIN', 'RECEPTIONIST']) {
    const f = fixture({ role }); await f.mount(); f.c.openCancellation(row); await f.c.handleCancellation(); assert.equal(f.writes.length, 0);
    f.c.cancelReason.value = '  Synthetic reason  '; f.c.cancelConfirmed.value = true; await f.c.handleCancellation();
    assert.deepEqual(f.writes, [[7, 'Synthetic reason']]); assert.equal(f.reads.length, 2); assert.equal(f.c.cancelRow.value, null); assert.ok(f.c.actionMsg.value);
  }
});
test('Dentist Assistant Patient cannot open or submit cancellation', async () => {
  for (const role of ['DENTIST', 'ASSISTANT', 'PATIENT']) { const f = fixture({ role }); await f.mount(); f.c.openCancellation(row); f.c.cancelConfirmed.value = true; await f.c.handleCancellation(); assert.equal(f.c.canCancelWaiting.value, false); assert.equal(f.writes.length, 0); assert.equal(f.c.cancelRow.value, null); }
});
test('non-waiting unknown or invalid row never opens cancellation', async () => {
  const f = fixture(); await f.mount(); for (const invalid of [{ ...row, status: 2 }, { ...row, queueEntryId: 0 }, { ...row, queueEntryId: 99 }]) f.c.openCancellation(invalid);
  assert.equal(f.c.cancelRow.value, null); assert.equal(f.writes.length, 0);
});
test('optional blank reason becomes null and boundary500 accepted', async () => {
  for (const reason of ['  ', 'x'.repeat(500)]) { const f = fixture(); await f.mount(); f.c.openCancellation(row); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = reason; await f.c.handleCancellation(); assert.deepEqual(f.writes, [[7, reason.trim() || null]]); }
});
test('reason501 rejected before request', async () => {
  const f = fixture(); await f.mount(); f.c.openCancellation(row); f.c.cancelReason.value = 'x'.repeat(501); f.c.cancelConfirmed.value = true; await f.c.handleCancellation(); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value);
});
test('double submit blocks concurrent call-next and close during write', async () => {
  const d = deferred(), f = fixture({ role: 'ADMIN', result: d.promise }); await f.mount(); f.c.callDentistId.value = 5; f.c.openCancellation(row); f.c.cancelConfirmed.value = true;
  const pending = f.c.handleCancellation(); await f.c.handleCancellation(); await f.c.handleCallNext(); f.c.closeCancellation();
  assert.equal(f.writes.length, 1); assert.equal(f.calls.length, 0); assert.notEqual(f.c.cancelRow.value, null); d.resolve(row); await pending;
});
test('failed write retains reason, clears confirmation and requires successful reload', async () => {
  const f = fixture({ failure: true }); await f.mount(); f.c.openCancellation(row); f.c.cancelReason.value = 'Synthetic draft'; f.c.cancelConfirmed.value = true;
  await f.c.handleCancellation(); await f.c.handleCancellation(); assert.equal(f.writes.length, 1); assert.equal(f.c.cancelConfirmed.value, false); assert.equal(f.c.cancelReason.value, 'Synthetic draft'); assert.equal(f.c.needsReview.value, true);
  await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.c.cancelRow.value, null); assert.equal(f.writes.length, 1);
});
test('success followed by failed reload blocks more mutations', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.openCancellation(row); f.c.cancelConfirmed.value = true; await f.c.handleCancellation(); f.c.openCancellation(row);
  assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.needsReview.value, true); assert.ok(f.c.actionMsg.value); assert.ok(f.c.error.value);
});
test('unmount clears cancellation and ignores late write without reloading', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.openCancellation(row); f.c.cancelReason.value = 'Synthetic'; f.c.cancelConfirmed.value = true; const pending = f.c.handleCancellation(); f.cleanup(); d.resolve(row); await pending;
  assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.cancelReason.value, ''); assert.equal(f.c.actionMsg.value, ''); assert.equal(f.reads.length, 1);
});
test('list reload clears unsent cancellation draft', async () => {
  const f = fixture(); await f.mount(); f.c.openCancellation(row); f.c.cancelConfirmed.value = true; await f.c.load(); assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.cancelConfirmed.value, false); assert.equal(f.writes.length, 0);
});
