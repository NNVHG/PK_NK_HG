import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/components/PatientCitizenId.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'citizen-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
function deferred() { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; }
function fixture(role = 'RECEPTIONIST') {
 const auth = { role, user: { userId: 3 } }, props = { patientId: 7 }, calls = [], controls = {}, watchers = []; let cleanup;
 const api = {
  read: async id => { calls.push(['read', id]); if (controls.failRead) throw Error('Synthetic'); return controls.read ?? { patientId: 7, masked: null, version: 0 }; },
  save: async (...args) => { calls.push(['save', ...args]); if (controls.failSave) throw Error('Synthetic'); return controls.save ?? { patientId: 7, masked: '987******098', version: 1 }; }
 };
 const module = { exports: {} };
 new Function('require', 'module', 'exports', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch: (get, cb) => watchers.push(cb), onMounted() {}, onUnmounted: cb => { cleanup = cb; } } : name === '@/stores/auth' ? { useAuthStore: () => auth } : { patientCitizenIdService: api }, module, module.exports);
 return { c: module.exports.default.setup(props, { expose() {} }), auth, props, calls, controls, watchers, cleanup: () => cleanup() };
}
const writes = f => f.calls.filter(x => x[0] === 'save');
test('entry blank and masked-only context; explicit confirmation required to save', async () => {
 const f = fixture(); await f.c.load(); assert.equal(f.c.citizenId.value, ''); f.c.citizenId.value = '987654321098'; await f.c.save(); assert.equal(writes(f).length, 0); f.c.confirmed.value = true; await f.c.save(); assert.deepEqual(writes(f)[0], ['save', 7, '987654321098', 0]); assert.equal(f.c.citizenId.value, ''); assert.equal(f.c.status.value.masked, '987******098');
});
for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT']) test(role + ' can read masked information but cannot write', async () => {
 const f = fixture(role); await f.c.load(); f.c.citizenId.value = '987654321098'; f.c.confirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 0); assert.equal(f.c.canWrite.value, false);
});
test('invalid CCCD never sent', async () => {
 for (const value of ['', '123', '987654321098 ', 'a'.repeat(12)]) { const f = fixture(); await f.c.load(); f.c.citizenId.value = value; f.c.confirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 0); }
});
test('unknown save clears full CCCD and requires explicit reload before another request', async () => {
 const f = fixture(); await f.c.load(); f.controls.failSave = true; f.c.citizenId.value = '987654321098'; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.c.citizenId.value, ''); assert.equal(f.c.needsReview.value, true); f.c.citizenId.value = '987654321098'; f.c.confirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 1); await f.c.load(); assert.equal(f.c.needsReview.value, false);
});
test('double submit and reload blocked during save', async () => {
 const f = fixture(), d = deferred(); await f.c.load(); f.controls.save = d.promise; f.c.citizenId.value = '987654321098'; f.c.confirmed.value = true; const save = f.c.save(); await f.c.save(); await f.c.load(); assert.equal(writes(f).length, 1); assert.equal(f.calls.length, 2); d.resolve({ patientId: 7, masked: '987******098', version: 1 }); await save;
});
test('malformed save response cannot claim success', async () => {
 const f = fixture(); await f.c.load(); f.controls.save = { patientId: 8, masked: '987******098', version: 1 }; f.c.citizenId.value = '987654321098'; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.c.message.value, ''); assert.equal(f.c.needsReview.value, true);
});
test('full unmasked response rejected', async () => {
 const f = fixture(); f.controls.read = { patientId: 7, masked: '987654321098', version: 0 }; await f.c.load(); assert.equal(f.c.status.value, null); assert.equal(f.c.needsReview.value, true);
});
test('auth change and unmount erase input and ignore late responses', async () => {
 const f = fixture(), d = deferred(); f.controls.read = d.promise; const read = f.c.load(); f.c.citizenId.value = '987654321098'; f.auth.role = ''; f.watchers[0](); d.resolve({ patientId: 7, masked: null, version: 0 }); await read; assert.equal(f.c.citizenId.value, ''); assert.equal(f.c.status.value, null);
 const g = fixture(), e = deferred(); g.controls.read = e.promise; const loading = g.c.load(); g.cleanup(); e.resolve({ patientId: 7, masked: null, version: 0 }); await loading; assert.equal(g.c.status.value, null);
});
test('input uses no persistent client storage, URL, or logging', async () => {
 const service = await readFile(new URL('../src/services/patientCitizenId.ts', import.meta.url), 'utf8');
 assert.doesNotMatch(source + service, /localStorage|sessionStorage|console\.|params:.*citizenId/); assert.match(source, /type="password"/); assert.match(service, /put<CitizenIdStatus>/);
});
