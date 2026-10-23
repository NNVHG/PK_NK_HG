import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/AppointmentDeskView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'desk-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const row = { appointmentId: 7, patientName: 'Synthetic patient', status: 'Scheduled', notes: 'Synthetic note', appointmentDate: '2026-10-20', slotTime: '09:00:00' };
const page = { items: [row], page: 1, totalCount: 1, hasNextPage: false };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ pages = [page], cancelResult, fail } = {}) {
  let mount, cleanup; const reads = [], writes = [];
  const imports = {
    '@/components/AppointmentRescheduling.vue': {},
    '@/components/AppointmentReminderPanel.vue': {},
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/appointments': { appointmentsService: { getForDesk: async (...args) => { reads.push(args); return pages.shift() ?? page; },
      cancel: async (...args) => { writes.push(args); if (fail) throw { response: { data: { message: 'Synthetic rejection' } } }; return cancelResult ?? { ...row, status: 'Cancelled' }; } } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, mount: () => mount(), cleanup: () => cleanup() };
}
async function ready(f) { await f.mount(); f.c.select(row); f.c.confirmed.value = true; }
test('desk loads selected date and scheduled filter', async () => {
  const f = fixture(); await f.mount(); assert.deepEqual(f.reads[0], [f.c.date.value, 1, 'Scheduled']); assert.deepEqual(f.c.rows.value, [row]);
});
test('desk presents same-dentist same-time appointments together without emergency behavior for legacy flags', async () => {
  const f = fixture();
  f.c.rows.value = [
    { ...row, appointmentId: 21, dentistId: 31, isEmergency: false },
    { ...row, appointmentId: 22, dentistId: 31, isEmergency: true, emergencyReason: 'Synthetic reason' },
    { ...row, appointmentId: 23, dentistId: 31, slotTime: '09:30:00', isEmergency: false }
  ];
  const groups = f.c.displayGroups.value;
  assert.equal(groups.length, 2);
  assert.deepEqual(groups[0].items.map(item => item.appointmentId), [21, 22]);
  assert.equal(groups[0].isEmergency, undefined);
  assert.equal(groups[1].isEmergency, undefined);
  assert.doesNotMatch(descriptor.template.content, /CẤP CỨU|emergency-badge/);
  assert.match(descriptor.template.content, /Ghi chú cần lưu ý/);
});
test('selecting and closing cancellation form never writes', async () => {
  const f = fixture(); await ready(f); f.c.close(); await f.c.cancel(); assert.equal(f.writes.length, 0); assert.equal(f.c.selected.value, null);
});
test('confirmation is required and successful cancel reloads list', async () => {
  const f = fixture(); await f.mount(); f.c.select(row); await f.c.cancel(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; f.c.reason.value = '  Synthetic reason  '; await f.c.cancel();
  assert.deepEqual(f.writes, [[7, 'Synthetic reason']]); assert.equal(f.reads.length, 2); assert.equal(f.c.selected.value, null); assert.equal(f.c.success.value, 'Đã hủy lịch #7.');
});
test('double click sends one cancellation', async () => {
  const d = deferred(), f = fixture({ cancelResult: d.promise }); await ready(f); const first = f.c.cancel(); await f.c.cancel();
  assert.equal(f.writes.length, 1); d.resolve({ status: 'Cancelled' }); await first;
});
test('server rejection retains draft and resets confirmation', async () => {
  const f = fixture({ fail: true }); await ready(f); f.c.reason.value = 'Synthetic'; await f.c.cancel();
  assert.equal(f.c.reason.value, 'Synthetic'); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.error.value, 'Synthetic rejection');
});
test('combined note limit rejected before request', async () => {
  const f = fixture(); await ready(f); f.c.reason.value = 'x'.repeat(f.c.reasonLimit.value + 1); await f.c.cancel(); assert.equal(f.writes.length, 0);
});
test('old list response cannot replace new filter results', async () => {
  const d = deferred(), f = fixture({ pages: [d.promise, { ...page, items: [] }] }); const first = f.c.load();
  f.c.status.value = 'Cancelled'; await f.c.load(); d.resolve(page); await first; assert.deepEqual(f.c.rows.value, []);
});
test('unmount clears personal data and ignores late cancellation response', async () => {
  const d = deferred(), f = fixture({ cancelResult: d.promise }); await ready(f); const first = f.c.cancel(); f.cleanup(); d.resolve({ status: 'Cancelled' }); await first;
  assert.equal(f.c.selected.value, null); assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.success.value, '');
});
