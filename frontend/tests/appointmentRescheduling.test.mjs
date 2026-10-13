import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/AppointmentRescheduling.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'reschedule-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const current = { appointmentId: 7, status: 'Scheduled', appointmentDate: '2099-01-01', slotTime: '08:00:00', dentistId: 30, rescheduleCount: 2 };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ reads = [current], result, failure, selfService = false, eligibilityReads = [] } = {}) {
  let cleanup; const getCalls = [], eligibilityCalls = [], writes = [], events = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/appointments': { appointmentsService: {
      selfReschedulingEligibility: async id => { eligibilityCalls.push(id); const response = eligibilityReads.shift() ?? { appointment: { ...current, rescheduleCount: 1 }, canReschedule: true, remainingChanges: 1, message: null }; if (response instanceof Error) throw response; return response; },
      getById: async id => { getCalls.push(id); const response = reads.shift() ?? current; if (response instanceof Error) throw response; return response; },
      reschedule: async (...args) => { writes.push(args); if (failure) throw { response: { data: { message: 'Synthetic conflict' } } }; return result ?? {}; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({ appointmentId: 7, patientName: 'Synthetic patient', selfService }, { expose() {}, emit: (...args) => events.push(args) });
  c.dialog.value = { open: false, showModal() { this.open = true; }, close() { this.open = false; } };
  return { c, writes, events, getCalls, eligibilityCalls, cleanup: () => cleanup() };
}
async function ready(f) { await f.c.open(); f.c.day.value = '2099-01-02'; f.c.time.value = '09:00'; f.c.reason.value = 'Synthetic shift'; f.c.confirmed.value = true; }
test('loads current appointment only when opened', async () => {
  const f = fixture(); assert.equal(f.getCalls.length, 0); await f.c.open(); assert.equal(f.c.day.value, current.appointmentDate); assert.equal(f.c.current.value.rescheduleCount, 2);
});
test('close clears form without moving appointment', async () => {
  const f = fixture(); await ready(f); f.c.close(); await f.c.save(); assert.equal(f.writes.length, 0); assert.equal(f.c.reason.value, '');
});
test('confirmation sends target and reason, preserving dentist through API contract', async () => {
  const f = fixture(); await ready(f); f.c.confirmed.value = false; await f.c.save(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; await f.c.save(); assert.deepEqual(f.writes, [[7, '2099-01-02', '09:00:00', 'Synthetic shift']]); assert.deepEqual(f.events, [['rescheduled']]);
});
test('invalid date, unchanged slot, lunch slot and missing/long reason rejected', async () => {
  for (const [key, value] of [['day', '2000-01-01'], ['time', '12:00'], ['reason', ' '], ['reason', 'x'.repeat(501)]]) {
    const f = fixture(); await ready(f); f.c[key].value = value; await f.c.save(); assert.equal(f.writes.length, 0);
  }
  const f = fixture(); await ready(f); f.c.day.value = current.appointmentDate; f.c.time.value = '08:00'; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('double submit and close during write cannot issue another move', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await ready(f); const first = f.c.save(); await f.c.save(); f.c.close(); assert.equal(f.writes.length, 1); assert.equal(f.c.dialog.value.open, true); d.resolve({}); await first;
});
test('conflict keeps draft, rereads state and requires confirmation again', async () => {
  const f = fixture({ failure: true }); await ready(f); await f.c.save(); assert.equal(f.c.reason.value, 'Synthetic shift'); assert.equal(f.c.confirmed.value, false); assert.equal(f.getCalls.length, 2); assert.equal(f.writes.length, 1);
});
test('closed or checked-in appointment cannot move', async () => {
  const f = fixture({ reads: [{ ...current, status: 'CheckedIn' }] }); await ready(f); await f.c.save(); assert.equal(f.writes.length, 0);
});
test('unmount ignores pending detail and move responses', async () => {
  const d = deferred(), f = fixture({ reads: [d.promise] }); const first = f.c.open(); f.cleanup(); d.resolve(current); await first; assert.equal(f.c.current.value, null);
  const p = deferred(), g = fixture({ result: p.promise }); await ready(g); const move = g.c.save(); g.cleanup(); p.resolve({}); await move; assert.equal(g.events.length, 0); assert.equal(g.c.reason.value, '');
});

test('portal opens current ownership-checked eligibility and remaining changes on demand', async () => {
  const f = fixture({ selfService: true }); assert.equal(f.eligibilityCalls.length, 0); await f.c.open();
  assert.deepEqual(f.eligibilityCalls, [7]); assert.equal(f.getCalls.length, 0); assert.equal(f.c.eligibility.value.remainingChanges, 1);
});
test('portal cannot submit when deadline or count limit is reached', async () => {
  for (const message of ['Synthetic deadline', 'Synthetic count limit']) {
    const f = fixture({ selfService: true, eligibilityReads: [{ appointment: current, canReschedule: false, message, remainingChanges: 0 }] });
    await ready(f); await f.c.save(); assert.equal(f.writes.length, 0); assert.equal(f.c.eligibility.value.message, message);
  }
});
test('portal sends only time and mandatory reason, never dentist or staff notes', async () => {
  const f = fixture({ selfService: true }); await ready(f); await f.c.save();
  assert.deepEqual(f.writes, [[7, '2099-01-02', '09:00:00', 'Synthetic shift']]); assert.deepEqual(f.events, [['rescheduled']]); assert.equal(f.c.eligibility.value, null);
});
test('portal close and unmount clear eligibility and unsaved form', async () => {
  const f = fixture({ selfService: true }); await ready(f); f.c.close(); assert.equal(f.c.eligibility.value, null); assert.equal(f.writes.length, 0);
  const d = deferred(), g = fixture({ selfService: true, eligibilityReads: [d.promise] }); const open = g.c.open(); g.cleanup();
  d.resolve({ appointment: current, canReschedule: true }); await open; assert.equal(g.c.current.value, null); assert.equal(g.c.eligibility.value, null); assert.equal(g.c.dialog.value.open, false);
});
test('portal rejection rechecks eligibility, preserves draft and requires confirmation again', async () => {
  const allowed = { appointment: current, canReschedule: true, remainingChanges: 1 };
  const blocked = { appointment: { ...current, rescheduleCount: 2 }, canReschedule: false, remainingChanges: 0, message: 'Synthetic limit' };
  const f = fixture({ selfService: true, failure: true, eligibilityReads: [allowed, blocked] }); await ready(f); await f.c.save();
  assert.equal(f.c.reason.value, 'Synthetic shift'); assert.equal(f.c.confirmed.value, false); assert.equal(f.eligibilityCalls.length, 2);
  f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes.length, 1); assert.equal(f.c.eligibility.value.canReschedule, false);
});
test('portal failed eligibility read prevents submission and clears prior private snapshot', async () => {
  const f = fixture({ selfService: true, eligibilityReads: [new Error('Synthetic 404')] }); await ready(f); await f.c.save();
  assert.equal(f.c.current.value, null); assert.equal(f.c.eligibility.value, null); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value);
});
test('portal double submit and close during write issue only one request', async () => {
  const d = deferred(), f = fixture({ selfService: true, result: d.promise }); await ready(f);
  const first = f.c.save(); await f.c.save(); f.c.close(); assert.equal(f.writes.length, 1); assert.equal(f.c.dialog.value.open, true); d.resolve({}); await first;
});
test('portal late write after unmount cannot emit or retain eligibility', async () => {
  const d = deferred(), f = fixture({ selfService: true, result: d.promise }); await ready(f); const first = f.c.save(); f.cleanup(); d.resolve({}); await first;
  assert.equal(f.events.length, 0); assert.equal(f.c.eligibility.value, null); assert.equal(f.c.reason.value, '');
});
