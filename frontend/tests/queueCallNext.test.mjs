import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'call-next-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const page = { items: [{ queueEntryId: 7 }], page: 1, totalCount: 1, hasNextPage: false };
const dentist = { dentistId: 5, dentistName: 'Synthetic dentist' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'ADMIN', dentists = [dentist], result = { queueNumber: 1, patientName: 'Synthetic patient', status: 6 }, failure = false, readFailureAfter = Infinity } = {}) {
  let cleanup, mount; const reads = [], writes = [], doctorReads = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => ({ role }) },
    '@/services/appointments': { appointmentsService: { getBookingDentists: async () => { doctorReads.push(true); if (dentists instanceof Error) throw dentists; return dentists; } } },
    '@/services/queue': { queueService: { getCallingDentists: async () => { doctorReads.push(true); if (dentists instanceof Error) throw dentists; return dentists; }, getToday: async (...args) => { reads.push(args); if (reads.length > readFailureAfter) throw new Error('Synthetic read failure'); return page; }, callNext: async payload => { writes.push(payload); if (failure) throw { response: { data: { message: 'Synthetic409' } } }; return result; } } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} }); return { c, reads, writes, doctorReads, mount: () => mount(), cleanup: () => cleanup() };
}
test('Admin must select loaded active dentist before calling', async () => {
  const f = fixture(); await f.mount(); await f.c.handleCallNext(); assert.equal(f.writes.length, 0);
  f.c.callDentistId.value = 5; await f.c.handleCallNext(); assert.deepEqual(f.writes, [{ dentistId: 5 }]); assert.equal(f.reads.length, 2);
});
test('Dentist calls self without overriding dentist through UI', async () => {
  const f = fixture({ role: 'DENTIST' }); await f.mount(); await f.c.handleCallNext(); assert.deepEqual(f.writes, [undefined]); assert.equal(f.doctorReads.length, 0);
});
test('Receptionist and Patient never submit call-next', async () => {
  for (const role of ['RECEPTIONIST', 'PATIENT']) { const f = fixture({ role }); await f.mount(); await f.c.handleCallNext(); assert.equal(f.writes.length, 0); assert.equal(f.c.canCallNext.value, false); }
});
test('unknown dentist or failed picker cannot call', async () => {
  const f = fixture(); await f.mount(); f.c.callDentistId.value = 99; await f.c.handleCallNext(); assert.equal(f.writes.length, 0);
  const g = fixture({ dentists: new Error('Synthetic picker failure') }); await g.mount(); g.c.callDentistId.value = 5; await g.c.handleCallNext(); assert.equal(g.writes.length, 0); assert.ok(g.c.dentistError.value);
});
test('double click sends only one call', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.callDentistId.value = 5; const first = f.c.handleCallNext(); await f.c.handleCallNext(); assert.equal(f.writes.length, 1); d.resolve({ queueNumber: 1, patientName: 'Synthetic', status: 6 }); await first;
});
test('conflict preserves chosen dentist and requires successful list reload without retry', async () => {
  const f = fixture({ failure: true }); await f.mount(); f.c.callDentistId.value = 5; await f.c.handleCallNext(); await f.c.handleCallNext();
  assert.equal(f.writes.length, 1); assert.equal(f.c.callDentistId.value, 5); assert.equal(f.c.needsReview.value, true);
  await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.writes.length, 1);
});
test('unmount ignores late call and clears patient announcement', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.callDentistId.value = 5; const first = f.c.handleCallNext(); f.cleanup(); d.resolve({ queueNumber: 1, patientName: 'Synthetic', status: 6 }); await first;
  assert.equal(f.c.actionMsg.value, ''); assert.equal(f.c.callDentistId.value, 0); assert.equal(f.reads.length, 1);
});
test('successful call with failed refresh blocks another call until reviewed', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.callDentistId.value = 5;
  await f.c.handleCallNext(); await f.c.handleCallNext(); assert.equal(f.writes.length, 1);
  assert.equal(f.c.needsReview.value, true); assert.ok(f.c.actionMsg.value); assert.ok(f.c.error.value);
});
test('failed list reload does not unlock a conflict for another write', async () => {
  const f = fixture({ failure: true, readFailureAfter: 1 }); await f.mount(); f.c.callDentistId.value = 5;
  await f.c.handleCallNext(); await f.c.load(); await f.c.handleCallNext();
  assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true);
});
test('unmount ignores late dentist picker response', async () => {
  const d = deferred(), f = fixture({ dentists: d.promise }); const first = f.mount(); f.cleanup(); d.resolve([dentist]); await first; assert.deepEqual(f.c.dentists.value, []);
});

test('Assistant selects calling dentist and can invite without a clinical start request', async () => {
  const f = fixture({ role: 'ASSISTANT' }); await f.mount(); await f.c.handleCallNext(); assert.equal(f.writes.length, 0);
  f.c.callDentistId.value = 5; await f.c.handleCallNext(); assert.deepEqual(f.writes, [{ dentistId: 5 }]);
  assert.match(f.c.actionMsg.value, /bắt đầu khám riêng/); assert.equal(f.c.canCallNext.value, true);
});
test('Calling status filter loads six without mutation', async () => {
  const f = fixture(); await f.mount(); f.c.status.value = 6; await f.c.load();
  assert.deepEqual(f.reads.at(-1), [1, 6]); assert.equal(f.writes.length, 0);
});
test('unexpected started response does not claim a separate invitation succeeded', async () => {
  const f = fixture({ result: { queueNumber: 1, patientName: 'Synthetic', status: 2 } }); await f.mount();
  f.c.callDentistId.value = 5; await f.c.handleCallNext(); assert.equal(f.c.actionMsg.value, ''); assert.equal(f.c.needsReview.value, true);
});


test('queue rows and dentist selector display destination room without a workspace write', async () => {
  const source = await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8');
  assert.ok(source.includes('{{ locationLabel(dentist) }}'));
  assert.ok(source.includes('{{ locationLabel(row) }}'));
  assert.ok(!source.includes('workspaces.change'));
});
