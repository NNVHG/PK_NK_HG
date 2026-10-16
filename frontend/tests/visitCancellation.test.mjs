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
const row = { queueEntryId: 7, status: 2, canCancelConsultation: true, dentistId: 5, dentistName: 'Synthetic dentist', queueNumber: 1,
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
      cancelConsultation: async (...args) => { writes.push(args); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...selected, status: 4 }; },
      callNext: async () => { calls.push(true); return row; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, calls, auth, mount: () => mount(), cleanup: () => cleanup() };
}
test('clinical cancellation requires confirmation and a nonblank reason', async () => {
  const f = fixture(); await f.mount(); f.c.openCancellation(row, true);
  await f.c.handleCancellation(); assert.equal(f.writes.length, 0);
  f.c.cancelConfirmed.value = true; f.c.cancelReason.value = '   '; await f.c.handleCancellation(); assert.equal(f.writes.length, 0);
  f.c.cancelReason.value = '  Synthetic reason  '; await f.c.handleCancellation();
  assert.deepEqual(f.writes, [[7, 'Synthetic reason']]); assert.equal(f.reads.length, 2); assert.equal(f.c.cancelRow.value, null);
});
test('all active phases allow owner or admin, never preclinical or foreign roles', async () => {
  for (const role of ['DENTIST', 'ADMIN', 'ASSISTANT', 'RECEPTIONIST', 'PATIENT']) {
    for (const status of [1, 2, 3, 4, 5, 6, 7]) {
      for (const eligible of [false, true]) {
        const selected = { ...row, status, canCancelConsultation: eligible }, f = fixture({ role, selected }); await f.mount();
        f.c.openCancellation(selected, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'Synthetic reason'; await f.c.handleCancellation();
        assert.equal(f.writes.length, ['DENTIST', 'ADMIN'].includes(role) && [1,2,5,6].includes(status) && eligible ? 1 : 0);
      }
    }
  }
  const f = fixture({ selected: { ...row, dentistId: 9 } }); await f.mount(); assert.equal(f.c.canCancelClinical({ ...row, dentistId: 9 }), false);
});
test('overlong reason, stale row and changed role cannot submit', async () => {
  const f = fixture(); await f.mount(); f.c.openCancellation({ ...row, queueEntryId: 99 }, true); assert.equal(f.c.cancelRow.value, null);
  f.c.openCancellation(row, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'x'.repeat(501); await f.c.handleCancellation(); assert.equal(f.writes.length, 0);
  f.c.cancelReason.value = 'x'.repeat(500); f.auth.role = 'ASSISTANT'; await f.c.handleCancellation(); assert.equal(f.writes.length, 0);
  f.auth.role = 'DENTIST'; await f.c.handleCancellation(); assert.equal(f.writes.length, 1);
});
test('double submit, reload and competing panels are blocked while cancelling', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.openCancellation(row, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'Synthetic reason';
  const pending = f.c.handleCancellation(); await f.c.handleCancellation(); await f.c.load(); await f.c.handleCallNext(); f.c.openImaging(row); f.c.closeCancellation();
  assert.equal(f.writes.length, 1); assert.equal(f.reads.length, 1); assert.equal(f.calls.length, 0); assert.equal(f.c.imagingRow.value, null); assert.ok(f.c.cancelRow.value);
  d.resolve({ ...row, status: 4 }); await pending; assert.equal(f.reads.length, 2);
});
test('failure or mismatched server response requires explicit reload without retry', async () => {
  for (const options of [{ fail: true }, { result: { ...row, status: 2 } }, { result: { ...row, status: 4, queueEntryId: 99 } }, { result: { ...row, status: 4, dentistId: 99 } }]) {
    const f = fixture(options); await f.mount(); f.c.openCancellation(row, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'Synthetic reason';
    await f.c.handleCancellation(); await f.c.handleCancellation(); assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.cancelConfirmed.value, false);
    await f.c.load(); assert.equal(f.c.cancelRow.value, null); assert.equal(f.c.needsReview.value, false);
  }
});
test('successful cancellation with failed reload stays blocked', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.openCancellation(row, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'Synthetic reason';
  await f.c.handleCancellation(); assert.equal(f.c.needsReview.value, true); assert.ok(f.c.actionMsg.value); assert.equal(f.writes.length, 1);
});
test('back and unmount clear reason and late responses cannot update state', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.openCancellation(row, true); f.c.cancelReason.value = 'Draft'; f.c.closeCancellation();
  assert.equal(f.c.cancelReason.value, ''); assert.equal(f.writes.length, 0);
  f.c.openCancellation(row, true); f.c.cancelConfirmed.value = true; f.c.cancelReason.value = 'Synthetic reason'; const pending = f.c.handleCancellation(); f.cleanup();
  d.resolve({ ...row, status: 4 }); await pending; assert.equal(f.c.actionMsg.value, ''); assert.equal(f.c.cancelRow.value, null); assert.equal(f.reads.length, 1);
});
test('UI explains Draft and preserves protected status endpoint', async () => {
  assert.match(source, /tự hủy hóa đơn Draft chưa thu/); assert.match(source, /:required="cancelClinical"/);
  const api = await readFile(new URL('../src/services/queue.ts', import.meta.url), 'utf8'); assert.match(api, /newStatus: 4, reason/);
});
test('active imaging return is never offered as preclinical cancellation', async () => {
  for (const role of ['ADMIN', 'RECEPTIONIST']) {
    const selected = { ...row, status: 1 }, f = fixture({ role, selected }); await f.mount(); f.c.openCancellation(selected);
    assert.equal(f.c.cancelRow.value, null); assert.equal(f.writes.length, 0);
  }
  const selected = { ...row, dentistId: 9 }, f = fixture({ selected }); await f.mount(); f.c.openCancellation({ ...row }, true); assert.equal(f.c.cancelRow.value, null);
});
