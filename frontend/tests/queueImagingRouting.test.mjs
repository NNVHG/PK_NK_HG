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
const row = { queueEntryId: 7, status: 2, dentistId: 5, dentistName: 'Synthetic dentist', queueNumber: 1,
  patientName: 'Synthetic patient', patientCode: 'TEST', queueDate: '2026-10-16' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'DENTIST', selected = row, fail = false, result, readFailureAfter = Infinity } = {}) {
  let cleanup, mount; const reads = [], writes = [], calls = []; const auth = { role, user: { userId: 5 } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => [{ dentistId: 5, dentistName: 'Synthetic dentist' }] } },
    '@/services/queue': { queueService: {
      getToday: async () => { reads.push(true); if (reads.length > readFailureAfter) throw new Error('Read failure'); return { items: [selected], page: 1, totalCount: 1, hasNextPage: false }; },
      routeImaging: async (...args) => { writes.push(args); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...selected, status: args[1] }; },
      callNext: async () => { calls.push(true); return row; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, calls, auth, mount: () => mount(), cleanup: () => cleanup() };
}
test('opening and closing imaging does not write and clears indication', async () => {
  const f = fixture(); await f.mount(); f.c.openImaging(row); assert.equal(f.c.imagingRow.value.queueEntryId, 7);
  f.c.imagingReason.value = 'draft'; f.c.closeImaging(); assert.equal(f.writes.length, 0); assert.equal(f.c.imagingRow.value, null); assert.equal(f.c.imagingReason.value, '');
});
test('Dentist/Admin must confirm and record an indication before routing', async () => {
  for (const role of ['DENTIST', 'ADMIN']) {
    const f = fixture({ role }); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = '  Synthetic indication  ';
    await f.c.handleImaging(); assert.equal(f.writes.length, 0); f.c.imagingConfirmed.value = true; await f.c.handleImaging();
    assert.deepEqual(f.writes, [[7, 5, 'Synthetic indication']]); assert.equal(f.reads.length, 2); assert.equal(f.c.imagingRow.value, null); assert.ok(f.c.actionMsg.value);
  }
});
test('Assistant/Admin can complete imaging back to priority waiting', async () => {
  for (const role of ['ASSISTANT', 'ADMIN']) {
    const selected = { ...row, status: 5 }; const f = fixture({ role, selected }); await f.mount(); f.c.openImaging(selected);
    f.c.imagingConfirmed.value = true; await f.c.handleImaging(); assert.deepEqual(f.writes, [[7, 1, null]]); assert.match(f.c.actionMsg.value, /nha sĩ phụ trách/);
  }
});
test('each transition rejects unpermitted roles and foreign dentist', async () => {
  for (const [status, roles] of [[2, ['ASSISTANT', 'RECEPTIONIST', 'PATIENT']], [5, ['DENTIST', 'RECEPTIONIST', 'PATIENT']]]) {
    for (const role of roles) { const selected = { ...row, status }; const f = fixture({ role, selected }); await f.mount(); f.c.openImaging(selected); assert.equal(f.c.imagingRow.value, null); }
  }
  const selected = { ...row, dentistId: 6 }; const f = fixture({ selected }); await f.mount(); f.c.openImaging(selected); assert.equal(f.c.imagingRow.value, null);
});
test('invalid row, status, dentist or unknown entry cannot open routing', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount();
  for (const invalid of [{ ...row, status: 1 }, { ...row, queueEntryId: 0 }, { ...row, queueEntryId: 99 }, { ...row, dentistId: null }]) f.c.openImaging(invalid);
  assert.equal(f.c.imagingRow.value, null);
});
test('blank indication and oversized reason cannot write', async () => {
  for (const reason of [' ', 'x'.repeat(501)]) {
    const f = fixture(); await f.mount(); f.c.openImaging(row); f.c.imagingConfirmed.value = true; f.c.imagingReason.value = reason;
    await f.c.handleImaging(); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value);
  }
});
test('boundary500 indication accepted', async () => {
  const f = fixture(); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = 'x'.repeat(500); f.c.imagingConfirmed.value = true;
  await f.c.handleImaging(); assert.equal(f.writes[0][2].length, 500);
});
test('double click cannot write twice, close or call next while routing', async () => {
  const d = deferred(), f = fixture({ role: 'ADMIN', result: d.promise }); await f.mount(); f.c.callDentistId.value = 5;
  f.c.openImaging(row); f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true;
  const pending = f.c.handleImaging(); await f.c.handleImaging(); await f.c.handleCallNext(); f.c.closeImaging(); await f.c.load();
  assert.equal(f.writes.length, 1); assert.equal(f.calls.length, 0); assert.equal(f.reads.length, 1); assert.ok(f.c.imagingRow.value);
  d.resolve({ ...row, status: 5 }); await pending;
});
test('open routing prevents unrelated cancellation and call-next', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); f.c.callDentistId.value = 5; f.c.openImaging(row);
  f.c.openCancellation({ ...row, status: 1 }); await f.c.handleCallNext(); assert.equal(f.c.cancelRow.value, null); assert.equal(f.calls.length, 0);
});
test('failure requires reload and a new confirmation, with no automatic retry', async () => {
  const f = fixture({ fail: true }); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true;
  await f.c.handleImaging(); await f.c.handleImaging(); assert.equal(f.writes.length, 1); assert.equal(f.c.imagingConfirmed.value, false);
  assert.equal(f.c.needsReview.value, true); assert.equal(f.c.imagingReason.value, 'Synthetic indication'); assert.equal(f.c.error.value, 'Synthetic409');
  await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.c.imagingRow.value, null);
});
test('malformed success never claims routing complete', async () => {
  const f = fixture({ result: { ...row, dentistId: 6, status: 5 } }); await f.mount(); f.c.openImaging(row);
  f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true; await f.c.handleImaging();
  assert.equal(f.c.actionMsg.value, ''); assert.equal(f.c.needsReview.value, true);
});
test('successful routing followed by read failure blocks additional writes', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true;
  await f.c.handleImaging(); f.c.openImaging(row); assert.equal(f.c.imagingRow.value, null); assert.equal(f.c.needsReview.value, true); assert.ok(f.c.actionMsg.value);
});
test('unmount clears indication and ignores late routing response without reload', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true;
  const pending = f.c.handleImaging(); f.cleanup(); d.resolve({ ...row, status: 5 }); await pending;
  assert.equal(f.c.imagingRow.value, null); assert.equal(f.c.imagingReason.value, ''); assert.equal(f.c.actionMsg.value, ''); assert.equal(f.reads.length, 1);
});
test('role change before confirmation prevents sending a request', async () => {
  const f = fixture(); await f.mount(); f.c.openImaging(row); f.c.imagingReason.value = 'Synthetic indication'; f.c.imagingConfirmed.value = true; f.auth.role = 'PATIENT';
  await f.c.handleImaging(); assert.equal(f.writes.length, 0);
});
test('API uses protected status endpoint without dentist reassignment', async () => {
  const api = await readFile(new URL('../src/services/queue.ts', import.meta.url), 'utf8');
  assert.match(api, /async routeImaging[\s\S]*?apiClient\.put[\s\S]*?\{ newStatus, reason \}/);
  assert.match(source, /imagingConfirmed/); assert.match(source, /Hoàn tất chụp → chờ nha sĩ/);
});
