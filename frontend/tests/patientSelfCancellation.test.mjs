import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/PatientAppointmentCancellation.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'patient-self-cancel-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const eligible = { appointmentId: 7, canCancel: true, message: null, reasonMaxLength: 487 };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ reads = [eligible], result, failure, id = 7 } = {}) {
  let cleanup; const getCalls = [], writes = [], events = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/appointments': { appointmentsService: {
      selfCancellationEligibility: async value => { getCalls.push(value); const response = reads.shift() ?? eligible; if (response instanceof Error) throw response; return response; },
      cancel: async (...args) => { writes.push(args); if (failure) throw { response: { data: { message: 'Synthetic deadline rejection' } } }; return result ?? { status: 'Cancelled' }; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({ appointmentId: id, patientName: 'Synthetic member', date: '2099-01-01', time: '08:00:00' }, { expose() {}, emit: (...args) => events.push(args) });
  c.dialog.value = { open: false, showModal() { this.open = true; }, close() { this.open = false; } };
  return { c, getCalls, writes, events, cleanup: () => cleanup() };
}
test('no eligibility requests until user opens dialog', async () => {
  const f = fixture(); assert.equal(f.getCalls.length, 0); await f.c.open(); assert.deepEqual(f.getCalls, [7]); assert.equal(f.c.dialog.value.open, true); assert.equal(f.c.confirmed.value, false);
});
test('back or escape keeps appointment unchanged and clears draft', async () => {
  const f = fixture(); await f.c.open(); f.c.reason.value = 'Synthetic reason'; f.c.close(); await f.c.cancel();
  assert.equal(f.writes.length, 0); assert.equal(f.c.reason.value, ''); assert.equal(f.c.eligibility.value, null);
});
test('explicit confirmation sends one trimmed cancellation and emits refresh', async () => {
  const f = fixture(); await f.c.open(); await f.c.cancel(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; f.c.reason.value = '  Synthetic reason  '; await f.c.cancel();
  assert.deepEqual(f.writes, [[7, 'Synthetic reason']]); assert.deepEqual(f.events, [['cancelled']]); assert.equal(f.c.dialog.value.open, false);
});
test('server-denied eligibility never allows cancellation', async () => {
  const f = fixture({ reads: [{ ...eligible, canCancel: false, message: 'Synthetic near deadline' }] }); await f.c.open(); f.c.confirmed.value = true; await f.c.cancel(); assert.equal(f.writes.length, 0);
});
test('oversized reason rejected and optional blank reason accepted at zero budget', async () => {
  const f = fixture({ reads: [{ ...eligible, reasonMaxLength: 0 }] }); await f.c.open(); f.c.confirmed.value = true; f.c.reason.value = 'x'; await f.c.cancel(); assert.equal(f.writes.length, 0);
  f.c.reason.value = ''; await f.c.cancel(); assert.deepEqual(f.writes, [[7, null]]);
});
test('duplicate click and close during write cannot issue another request', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.c.open(); f.c.confirmed.value = true; const first = f.c.cancel(); await f.c.cancel(); f.c.close();
  assert.equal(f.writes.length, 1); assert.equal(f.c.dialog.value.open, true); d.resolve({ status: 'Cancelled' }); await first;
});
test('write error rereads eligibility without automatically retrying mutation', async () => {
  const f = fixture({ failure: true, reads: [eligible, { ...eligible, canCancel: false, message: 'Synthetic deadline passed' }] }); await f.c.open(); f.c.confirmed.value = true; f.c.reason.value = 'Synthetic draft'; await f.c.cancel();
  assert.equal(f.writes.length, 1); assert.equal(f.getCalls.length, 2); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.eligibility.value.canCancel, false); assert.equal(f.c.reason.value, 'Synthetic draft');
});
test('failed eligibility disables confirmation', async () => {
  const f = fixture({ reads: [new Error('Synthetic fault')] }); await f.c.open(); f.c.confirmed.value = true; await f.c.cancel(); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value);
});
test('unmount ignores late eligibility response and closes dialog', async () => {
  const d = deferred(), f = fixture({ reads: [d.promise] }); const first = f.c.open(); f.cleanup(); d.resolve(eligible); await first;
  assert.equal(f.c.eligibility.value, null); assert.equal(f.c.dialog.value.open, false);
});
test('unmount ignores late cancellation success', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.c.open(); f.c.confirmed.value = true; const first = f.c.cancel(); f.cleanup(); d.resolve({ status: 'Cancelled' }); await first;
  assert.equal(f.events.length, 0); assert.equal(f.c.reason.value, '');
});
test('invalid appointment ID cannot request eligibility', async () => {
  const f = fixture({ id: 0 }); await f.c.open(); assert.equal(f.getCalls.length, 0);
});
