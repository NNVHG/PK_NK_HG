import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript, compileTemplate } from '@vue/compiler-sfc';
function execute(source, imports) { const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText; const module = { exports: {} }; new Function('require', 'module', 'exports', js)(name => imports[name] ?? {}, module, module.exports); return module.exports; }
const sdkSource = await readFile(new URL('../src/services/schedulingSettings.ts', import.meta.url), 'utf8');
const storeSource = await readFile(new URL('../src/stores/schedulingSettings.ts', import.meta.url), 'utf8');
const composableSource = await readFile(new URL('../src/composables/useBookingSchedule.ts', import.meta.url), 'utf8');
const viewSource = await readFile(new URL('../src/views/SchedulingSettingsView.vue', import.meta.url), 'utf8');
const current = () => ({ workStartTime: '08:00', lunchStartTime: '12:00', lunchEndTime: '13:30', workEndTime: '20:00', slotDurationMinutes: 30, revision: 'A'.repeat(64), usingFallback: true });
const payload = () => ({ ...current(), expectedRevision: 'A'.repeat(64) });
test('schedule validator enforces minute syntax, order and at least one full slot', () => {
  const { validSchedule } = execute(sdkSource, { '@/services/api': {} }); assert.equal(validSchedule(payload()), true);
  for (const patch of [{ workStartTime: '8:00' }, { lunchStartTime: '07:00' }, { lunchEndTime: '12:00' }, { workEndTime: '24:00' }, { slotDurationMinutes: 0 }, { slotDurationMinutes: .5 }, { slotDurationMinutes: 1441 }, { slotDurationMinutes: 1000 }]) assert.equal(validSchedule({ ...payload(), ...patch }), false);
  assert.equal(validSchedule({ ...payload(), slotDurationMinutes: 60 }), true);
});
test('typed SDK only reads and writes the authorized config endpoint', async () => { const calls = []; const sdk = execute(sdkSource, { '@/services/api': { apiClient: { get: async (...a) => { calls.push(a); return { data: current() }; }, put: async (...a) => { calls.push(a); return { data: current() }; } } } }).schedulingSettingsService; await sdk.read(); await sdk.save(payload()); assert.deepEqual(calls.map(x => x[0]), ['/master/configs', '/master/configs']); assert.equal(calls[1][1].expectedRevision, 'A'.repeat(64)); });
function storeFixture() {
  const responses = [], calls = []; let identity;
  const api = { read: async () => responses.length ? await responses.shift()() : current(), save: async r => { calls.push(r); return responses.length ? await responses.shift()() : { ...current(), usingFallback: false, revision: 'B'.repeat(64) }; } };
  const mod = execute(storeSource, { pinia: { defineStore: (_id, fn) => fn }, vue: { ref: value => ({ value }), watch: (_get, fn) => { identity = fn; } }, axios: { isAxiosError: e => !!e.response }, '@/stores/auth': { useAuthStore: () => ({ user: { userId: 1 }, role: 'ADMIN' }) }, '@/services/schedulingSettings': { schedulingSettingsService: api } });
  return { s: mod.useSchedulingSettingsStore(), responses, calls, identity: () => identity() };
}
test('store reads fallback and saves with current revision', async () => { const { s, calls } = storeFixture(); await s.load(); assert.equal(s.current.value.usingFallback, true); await s.save(payload()); assert.equal(s.current.value.revision, 'B'.repeat(64)); assert.match(s.message.value, /Lịch đã hẹn được giữ nguyên/); assert.equal(calls.length, 1); });
test('stale admin revision locks save until explicit reload', async () => { const { s, responses, calls } = storeFixture(); await s.load(); responses.push(() => { throw { response: { status: 409 } }; }); await s.save(payload()); assert.equal(s.needsReload.value, true); await s.save(payload()); assert.equal(calls.length, 1); await s.load(); assert.equal(s.needsReload.value, false); });
test('unknown save result requires reread and never retries automatically', async () => { const { s, responses, calls } = storeFixture(); await s.load(); responses.push(() => { throw new Error('network'); }); await s.save(payload()); assert.equal(s.needsReload.value, true); assert.match(s.error.value, /Chưa xác định/); assert.equal(calls.length, 1); });
test('validation rejection permits form correction without a forced reload', async () => { const { s, responses } = storeFixture(); await s.load(); responses.push(() => { throw { response: { status: 400, data: { message: 'Invalid time' } } }; }); await s.save(payload()); assert.equal(s.needsReload.value, false); assert.equal(s.error.value, 'Invalid time'); });
test('double save is blocked while request is running', async () => { const { s, responses, calls } = storeFixture(); await s.load(); let resolve; responses.push(() => new Promise(r => { resolve = r; })); const first = s.save(payload()); await s.save(payload()); assert.equal(calls.length, 1); resolve(current()); await first; });
test('identity change clears raw config and discards late response', async () => { const { s, responses, identity } = storeFixture(); let resolve; responses.push(() => new Promise(r => { resolve = r; })); const work = s.load(); identity(); resolve(current()); await work; assert.equal(s.current.value, null); assert.equal(s.busy.value, false); });
function bookingFixture(auto = false) {
  const responses = [], calls = [], hooks = {};
  const mod = execute(composableSource, { vue: { ref: value => ({ value }), onMounted: fn => { hooks.mount = fn; }, onUnmounted: fn => { hooks.unmount = fn; } }, '@/services/appointments': { appointmentsService: { bookingSchedule: async () => { calls.push(true); return responses.length ? await responses.shift()() : { slots: ['08:00', '09:00'], slotDurationMinutes: 60, revision: 'A'.repeat(64) }; } } } });
  return { s: mod.useBookingSchedule(auto), responses, calls, hooks };
}
test('booking selector loads current server slots and duration', async () => { const { s, calls } = bookingFixture(); assert.deepEqual(s.slots.value, []); await s.load(); assert.deepEqual(s.slots.value, ['08:00', '09:00']); assert.equal(s.duration.value, 60); assert.equal(calls.length, 1); });
test('schedule fetch failure clears slots instead of inventing a local fallback', async () => { const { s, responses } = bookingFixture(); await s.load(); responses.push(() => { throw new Error('network'); }); await s.load(); assert.deepEqual(s.slots.value, []); assert.equal(s.revision.value, ''); assert.equal(s.duration.value, 0); assert.ok(s.error.value); });
for (const bad of [{ slots: ['24:00'], slotDurationMinutes: 30, revision: 'A'.repeat(64) }, { slots: [], slotDurationMinutes: 30, revision: 'A'.repeat(64) }, { slots: ['08:00'], slotDurationMinutes: .5, revision: 'A'.repeat(64) }, { slots: ['08:00'], slotDurationMinutes: 30, revision: '' }]) test(`malformed selector response is rejected ${JSON.stringify(bad)}`, async () => { const { s, responses } = bookingFixture(); responses.push(() => bad); await s.load(); assert.deepEqual(s.slots.value, []); assert.ok(s.error.value); });
test('closing selector before response ignores it and clears revision', async () => { const { s, responses, hooks } = bookingFixture(); let resolve; responses.push(() => new Promise(r => { resolve = r; })); const work = s.load(); hooks.unmount(); resolve({ slots: ['08:00'], slotDurationMinutes: 60, revision: 'A'.repeat(64) }); await work; assert.deepEqual(s.slots.value, []); assert.equal(s.revision.value, ''); });
test('newer schedule load wins over stale request', async () => { const { s, responses } = bookingFixture(); let resolve; responses.push(() => new Promise(r => { resolve = r; })); const old = s.load(); await s.load(); resolve({ slots: ['10:00'], slotDurationMinutes: 30, revision: 'B'.repeat(64) }); await old; assert.deepEqual(s.slots.value, ['08:00', '09:00']); });
test('admin template compiles and explains preservation of old reservations', () => { const d = parse(viewSource).descriptor; assert.equal(compileTemplate({ source: d.template.content, id: 'schedule', filename: 'SchedulingSettingsView.vue' }).errors.length, 0); assert.match(viewSource, /phần chỗ của lịch đã hẹn được giữ nguyên/); assert.match(viewSource, /step="1"/); assert.match(viewSource, /store.needsReload/); assert.match(viewSource, /Tôi đã kiểm tra giờ/); });
const compiled = compileScript(parse(viewSource).descriptor, { id: 'schedule' }).content;
test('admin form requires valid schedule and explicit confirmation', async () => {
  const calls = [], store = { current: current(), save: async p => calls.push(p), load: async () => {} };
  const mod = execute(compiled, { vue: { defineComponent: x => x, reactive: x => x, ref: value => ({ value }), watch: (_get, fn) => fn(store.current), onMounted() {}, onUnmounted() {} }, '@/stores/schedulingSettings': { useSchedulingSettingsStore: () => store }, '@/services/schedulingSettings': execute(sdkSource, { '@/services/api': {} }) });
  const v = mod.default.setup({}, { expose() {} }); await v.save(); assert.equal(calls.length, 0); v.confirmed.value = true; v.form.slotDurationMinutes = .5; await v.save(); assert.equal(calls.length, 0); v.form.slotDurationMinutes = 60; await v.save(); assert.equal(calls.length, 1); assert.equal(calls[0].expectedRevision, 'A'.repeat(64)); assert.equal(v.confirmed.value, false);
});
