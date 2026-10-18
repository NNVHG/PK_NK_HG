import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/PublicPrescriptionView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'public-rx-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const code = 'RX-20261017-0012';
const summary = { prescriptionCode: code, createdAt: '2026-10-17T01:00:00Z' };
const details = { ...summary, patientName: 'Synthetic patient', doctorName: 'Synthetic doctor', diagnosis: 'Synthetic clinical content', doctorAdvice: null, revisitDate: null, items: [] };
function deferred() { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; }
function fixture() {
 const route = { params: { code } }, calls = [], controls = {}, watchers = [], storage = new Map(), timers = []; let cleanup;
 const session = { token: 'synthetic-restricted-token', expiresAt: new Date(Date.now() + 15 * 60000).toISOString() };
 const api = {
  summary: async x => { calls.push(['summary', x]); return controls.summary ?? summary; },
  verify: async (...args) => { calls.push(['verify', ...args]); if (controls.fail) throw controls.fail; return controls.verify ?? session; },
  details: async (...args) => { calls.push(['details', ...args]); if (controls.failDetails) throw Error('Synthetic'); return controls.details ?? details; }
 };
 const module = { exports: {} };
 new Function('require', 'module', 'exports', 'sessionStorage', 'setTimeout', 'clearTimeout', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch: (get, cb) => watchers.push(cb), onMounted() {}, onUnmounted: cb => { cleanup = cb; } } : name === 'vue-router' ? { useRoute: () => route } : { publicPrescriptionsService: api }, module, module.exports, { getItem: k => storage.get(k) ?? null, setItem: (k, v) => storage.set(k, v), removeItem: k => storage.delete(k) }, cb => { timers.push(cb); return timers.length; }, () => {});
 return { c: module.exports.default.setup({}, { expose() {} }), route, calls, controls, watchers, storage, session, timers, cleanup: () => cleanup() };
}
test('scan route initially shows metadata and input only; no automatic private fetch', async () => {
 const f = fixture(); await f.c.load(); assert.equal(f.c.summary.value.prescriptionCode, code); assert.equal(f.c.details.value, null); assert.equal(f.c.citizenId.value, ''); assert.deepEqual(f.calls.map(x => x[0]), ['summary']);
});
test('correct CCCD verification uses body then separate restricted-token detail read, stores no CCCD', async () => {
 const f = fixture(); await f.c.load(); f.c.citizenId.value = '987654321098'; await f.c.verify(); assert.equal(f.c.details.value.patientName, details.patientName); assert.equal(f.c.citizenId.value, ''); assert.deepEqual(f.calls[1], ['verify', code, '987654321098']); assert.deepEqual(f.calls[2], ['details', code, f.session.token]); assert.doesNotMatch([...f.storage.values()].join(), /987654321098|Synthetic clinical/);
});
test('invalid CCCD blocks request', async () => {
 const f = fixture(); await f.c.load(); f.c.citizenId.value = '123'; await f.c.verify(); assert.equal(f.calls.length, 1);
});
test('failed challenge clears input and clinical data; never retries automatically', async () => {
 const f = fixture(); await f.c.load(); f.controls.fail = { response: { status: 403 } }; f.c.citizenId.value = '987654321098'; await f.c.verify(); assert.equal(f.c.citizenId.value, ''); assert.equal(f.c.details.value, null); assert.equal(f.storage.size, 0); assert.equal(f.calls.filter(x => x[0] === 'verify').length, 1);
});
test('rate lock displayed without exposing server error or identity', async () => {
 const f = fixture(); await f.c.load(); f.controls.fail = { response: { status: 429 } }; f.c.citizenId.value = '987654321098'; await f.c.verify(); assert.match(f.c.error.value, /giới hạn/); assert.doesNotMatch(f.c.error.value, /987654321098/);
});
test('double click sends one challenge and clears input while pending', async () => {
 const f = fixture(), d = deferred(); await f.c.load(); f.controls.verify = d.promise; f.c.citizenId.value = '987654321098'; const pending = f.c.verify(); await f.c.verify(); await f.c.load(); assert.equal(f.c.citizenId.value, ''); assert.equal(f.calls.filter(x => x[0] === 'verify').length, 1); d.resolve(f.session); await pending;
});
test('valid session survives reload with metadata then revalidated details, no CCCD reuse', async () => {
 const f = fixture(); f.storage.set('rx-view:' + code, JSON.stringify(f.session)); await f.c.load(); assert.deepEqual(f.calls.map(x => x[0]), ['summary', 'details']); assert.equal(f.c.details.value.prescriptionCode, code);
});
test('expired or malformed stored session never opens details', async () => {
 for (const value of ['invalid', JSON.stringify({ ...fixture().session, expiresAt: '2000-01-01T00:00:00Z' })]) { const f = fixture(); f.storage.set('rx-view:' + code, value); await f.c.load(); assert.equal(f.c.details.value, null); assert.equal(f.storage.size, 0); }
});
test('expiry timer erases private content and restricted session', async () => {
 const f = fixture(); await f.c.load(); f.c.citizenId.value = '987654321098'; await f.c.verify(); f.timers[0](); assert.equal(f.c.details.value, null); assert.equal(f.storage.size, 0); assert.match(f.c.error.value, /hết hạn/);
});
test('explicit close removes session and private contents', async () => {
 const f = fixture(); await f.c.load(); f.c.citizenId.value = '987654321098'; await f.c.verify(); f.c.close(); assert.equal(f.c.details.value, null); assert.equal(f.storage.size, 0);
});
test('wrong prescription response cannot expose details', async () => {
 const f = fixture(); await f.c.load(); f.controls.details = { ...details, prescriptionCode: 'RX-20261017-0013' }; f.c.citizenId.value = '987654321098'; await f.c.verify(); assert.equal(f.c.details.value, null); assert.equal(f.storage.size, 0);
});
test('unmount discards late challenge and clears clinical state', async () => {
 const f = fixture(), d = deferred(); await f.c.load(); f.controls.verify = d.promise; f.c.citizenId.value = '987654321098'; const pending = f.c.verify(); f.cleanup(); d.resolve(f.session); await pending; assert.equal(f.c.details.value, null); assert.equal(f.calls.filter(x => x[0] === 'details').length, 0); assert.equal(f.c.citizenId.value, '');
});
test('invalid route makes no network request', async () => {
 const f = fixture(); f.route.params.code = '../patients'; await f.c.load(); assert.equal(f.calls.length, 0);
});
test('public client has no account JWT or logout interceptor; header only for view token', async () => {
 const service = await readFile(new URL('../src/services/publicPrescriptions.ts', import.meta.url), 'utf8'); const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
 assert.match(service, /axios\.create/); assert.doesNotMatch(service, /import.*apiClient|Authorization:|interceptors|params:.*citizenId/); assert.match(service, /X-Prescription-View-Token/); assert.match(router, /public-prescription.*requiresAuth: false/); assert.doesNotMatch(source, /localStorage|console\./);
});
