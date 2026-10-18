import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/PrescriptionView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'rx-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const medicine = { id: 9, version: 2, code: 'TEST', name: 'Synthetic medicine', activeIngredient: 'Test', strength: null, unit: 'Viên', unitPrice: '500', defaultUsageInstruction: null };
const initial = { visitId: 7, patientId: 12, dentistId: 3, visitStatus: 'InProgress', visitLocked: false, visitDiagnosis: 'Synthetic diagnosis', canEdit: true, invoiceId: null, invoiceCode: null, invoiceStatus: null, allergies: [], prescription: null };
const preview = { token: 'a'.repeat(64), allergyWarnings: [], duplicateIngredients: [], medicineTotal: '1000', servicesTotal: '10000', discountAmount: '0', finalAmount: '11000' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture(role = 'DENTIST') {
 const auth = { role, user: { userId: 3 } }, route = { params: { visitId: '7' } }, calls = [], watchers = [], controls = { context: structuredClone(initial), preview: structuredClone(preview), medicines: [structuredClone(medicine)], failSave: false, failRead: false };
 let cleanup;
 const api = {
  context: async id => { calls.push(['context', id]); if (controls.failRead) throw Error('Synthetic read failure'); return controls.read ?? structuredClone(controls.context); },
  medicines: async (...args) => { calls.push(['medicines', ...args]); return { items: controls.medicines, page: args[2], hasNextPage: false }; },
  latest: async (...args) => { calls.push(['latest', ...args]); return controls.repeat ?? { sourcePrescriptionId: 5, sourceCode: 'RX-TEST-0001', items: [{ medicineId: 9, quantity: 2, usageInstructions: 'Saved clinician instruction', treatmentDays: null, currentMedicine: medicine }] }; },
  preview: async (...args) => { calls.push(['preview', ...args]); return controls.previewPromise ?? structuredClone(controls.preview); },
  save: async (...args) => { calls.push(['save', ...args]); if (controls.failSave) throw Error('Synthetic uncertain save'); return controls.savePromise ?? { id: 4, visitId: 7, doctorId: 3, version: args[2].expectedVersion + 1 }; }
 };
 const module = { exports: {} };
 new Function('require', 'module', 'exports', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), reactive: x => x, computed: get => ({ get value() { return get(); } }), watch: (get, cb) => watchers.push(cb), onMounted() {}, onUnmounted: cb => { cleanup = cb; } } : name === 'vue-router' ? { useRoute: () => route } : name === '@/stores/auth' ? { useAuthStore: () => auth } : { prescriptionsService: api }, module, module.exports);
 return { c: module.exports.default.setup({}, { expose() {} }), auth, route, calls, controls, watchers, cleanup: () => cleanup() };
}
async function filled(f = fixture()) { await f.c.load(); await f.c.search(); f.c.add(f.controls.medicines[0]); Object.assign(f.c.draft.value[0], { quantity: '2', usageInstructions: 'Synthetic clinician instructions' }); return f; }
const writes = f => f.calls.filter(x => x[0] === 'save');
async function approve(f) { await f.c.previewOrder(); f.c.confirmed.value = true; }

test('manual selection leaves quantity/duration blank and accepts only the loaded active catalog', async () => {
 const f = fixture(); await f.c.load(); f.c.add(medicine); assert.equal(f.c.draft.value.length, 0); await f.c.search(); f.c.add(medicine);
 assert.equal(f.c.draft.value[0].quantity, ''); assert.equal(f.c.draft.value[0].treatmentDays, ''); assert.equal(f.c.draft.value[0].usageInstructions, '');
 f.c.add(medicine); assert.equal(f.c.draft.value.length, 1); assert.equal(f.c.form.diagnosis, initial.visitDiagnosis);
});
test('configured instruction is copied without inventing quantity', async () => {
 const f = fixture(); f.controls.medicines[0].defaultUsageInstruction = 'Admin configured text'; await f.c.load(); await f.c.search(); f.c.add(medicine); assert.equal(f.c.draft.value[0].usageInstructions, 'Admin configured text'); assert.equal(f.c.draft.value[0].quantity, '');
});
test('preview and explicit confirmation required before atomic shared invoice save', async () => {
 const f = await filled(); f.c.confirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 0);
 await f.c.previewOrder(); await f.c.save(); assert.equal(writes(f).length, 0); f.c.confirmed.value = true; await f.c.save();
 assert.equal(writes(f).length, 1); assert.deepEqual(writes(f)[0].slice(1, 3), [7, true]); assert.equal(writes(f)[0][3].previewToken, preview.token); assert.match(f.c.message.value, /đồng bộ/);
});
for (const patch of [{ quantity: '' }, { quantity: '0' }, { quantity: '101' }, { quantity: '1.5' }, { quantity: '1e2' }, { treatmentDays: '0' }, { treatmentDays: '31' }, { usageInstructions: ' ' }, { usageInstructions: 'x'.repeat(501) }, { active: false }]) test('invalid line blocks preview: ' + JSON.stringify(patch).slice(0, 55), async () => {
 const f = await filled(); Object.assign(f.c.draft.value[0], patch); await f.c.previewOrder(); assert.equal(f.calls.filter(x => x[0] === 'preview').length, 0);
});
test('quantity100 and optional duration30 accepted, duplicate medicines and more than10 rejected', async () => {
 const f = await filled(); f.c.draft.value[0].quantity = '100'; f.c.draft.value[0].treatmentDays = '30'; assert.equal(f.c.valid(), true);
 f.c.draft.value.push({ ...f.c.draft.value[0] }); assert.equal(f.c.valid(), false);
 f.c.draft.value = Array.from({ length: 11 }, (_, i) => ({ ...f.c.draft.value[0], medicineId: i + 1 })); assert.equal(f.c.valid(), false);
});
test('invalid independent diagnosis, advice and date block preview', async () => {
 for (const patch of [{ diagnosis: '' }, { diagnosis: 'x'.repeat(501) }, { doctorAdvice: 'x'.repeat(1001) }, { revisitDate: '2026-02-30' }]) { const f = await filled(); Object.assign(f.c.form, patch); assert.equal(f.c.valid(), false); }
});
test('clinical change invalidates token even without a reactive watcher callback', async () => {
 const f = await filled(); await approve(f); f.c.draft.value[0].quantity = '3'; await f.c.save(); assert.equal(writes(f).length, 0);
 f.c.invalidate(); assert.equal(f.c.preview.value, null); assert.equal(f.c.confirmed.value, false);
});
test('allergy requires per-drug confirmation with reason5..500 and separate duplicate acknowledgment', async () => {
 const f = await filled(); f.controls.preview.allergyWarnings = [{ medicineId: 9, allergyIds: [1], allergyNames: ['Test'] }]; f.controls.preview.duplicateIngredients = ['Test']; await approve(f);
 await f.c.save(); assert.equal(writes(f).length, 0); Object.assign(f.c.approvals[9], { confirmed: true, reason: 'four' }); await f.c.save(); assert.equal(writes(f).length, 0);
 f.c.approvals[9].reason = 'Clinician reviewed'; await f.c.save(); assert.equal(writes(f).length, 0); f.c.duplicateConfirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 1); assert.equal(writes(f)[0][3].items[0].allergyOverrideReason, 'Clinician reviewed');
});
test('uncertain save retains draft and blocks all repeat writes until explicit reload', async () => {
 const f = await filled(); await approve(f); f.controls.failSave = true; await f.c.save(); f.c.confirmed.value = true; await f.c.save(); assert.equal(writes(f).length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.draft.value.length, 1); await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.c.draft.value.length, 0);
});
test('double submit, add, remove, repeat and reload blocked during write', async () => {
 const f = await filled(), d = deferred(); await approve(f); f.controls.savePromise = d.promise; const saving = f.c.save(); await f.c.save(); f.c.remove(0); await f.c.repeat(); await f.c.load(); assert.equal(writes(f).length, 1); assert.equal(f.c.draft.value.length, 1); d.resolve({ id: 4, visitId: 7, doctorId: 3, version: 1 }); await saving;
});
test('wrong write identity never claims success', async () => {
 const f = await filled(); await approve(f); f.controls.savePromise = { id: 4, visitId: 8, doctorId: 3, version: 1 }; await f.c.save(); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.message.value, '');
});
test('save success with failed verification read states saved but requires reload', async () => {
 const f = await filled(); await approve(f); f.controls.failRead = true; await f.c.save(); assert.match(f.c.message.value, /Đã lưu đơn nhưng/); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.context.value, null);
});
test('repeat uses actual saved instructions with current price, never overwrites a draft', async () => {
 const f = fixture(); await f.c.load(); await f.c.repeat(); assert.equal(f.c.draft.value[0].unitPrice, medicine.unitPrice); assert.equal(f.c.draft.value[0].usageInstructions, 'Saved clinician instruction'); assert.equal(f.c.preview.value, null); await f.c.repeat(); assert.equal(f.calls.filter(x => x[0] === 'latest').length, 1); assert.deepEqual(f.calls.find(x => x[0] === 'latest').slice(1), [12, 7]);
});
test('inactive repeat line must be removed or replaced before preview', async () => {
 const f = fixture(); f.controls.repeat = { sourceCode: 'RX-OLD', items: [{ medicineId: 9, quantity: 2, usageInstructions: 'Saved', treatmentDays: null, currentMedicine: null }] }; await f.c.load(); await f.c.repeat(); assert.equal(f.c.draft.value[0].active, false); assert.equal(f.c.valid(), false);
});
for (const role of ['ADMIN', 'RECEPTIONIST', 'ASSISTANT', 'PATIENT']) test(role + ' can read but cannot prescribe despite forged canEdit context', async () => {
 const f = fixture(role); await f.c.load(); await f.c.search(); await f.c.repeat(); await f.c.previewOrder(); await f.c.save(); assert.equal(f.c.editable.value, false); assert.deepEqual(f.calls.map(x => x[0]), ['context']);
});
test('foreign dentist and server-locked visit cannot enter editor', async () => {
 const f = fixture(); f.controls.context.dentistId = 4; await f.c.load(); assert.equal(f.c.editable.value, false); f.controls.context.dentistId = 3; f.controls.context.canEdit = false; await f.c.load(); assert.equal(f.c.editable.value, false);
});
test('invalid route makes no request', async () => {
 for (const value of ['bad', '0', '1e3', '2147483648']) { const f = fixture(); f.route.params.visitId = value; await f.c.load(); assert.equal(f.calls.length, 0); }
});
test('unmount clears clinical content and discards late responses', async () => {
 const f = fixture(), d = deferred(); f.controls.read = d.promise; const reading = f.c.load(); f.cleanup(); d.resolve(initial); await reading; assert.equal(f.c.context.value, null); assert.equal(f.c.form.diagnosis, '');
});
test('auth change clears draft and ignores late write', async () => {
 const f = await filled(), d = deferred(); await approve(f); f.controls.savePromise = d.promise; const writing = f.c.save(); f.auth.role = ''; f.watchers[0](); d.resolve({ id: 4, visitId: 7, doctorId: 3, version: 1 }); await writing; assert.equal(f.c.context.value, null); assert.equal(f.c.draft.value.length, 0); assert.equal(f.c.message.value, '');
});
test('stale preview cannot approve changed draft', async () => {
 const f = await filled(), d = deferred(); f.controls.previewPromise = d.promise; const loading = f.c.previewOrder(); f.c.form.diagnosis = 'Changed'; d.resolve(preview); await loading; assert.equal(f.c.preview.value, null);
});
test('money stays exact at18digits and overflow blocks clinical save', async () => {
 const f = await filled(); f.c.draft.value[0].unitPrice = '999999999999999999'; f.c.draft.value[0].quantity = '1'; assert.equal(f.c.valid(), true); assert.match(f.c.lineTotal(f.c.draft.value[0]), /999[.,]999[.,]999[.,]999[.,]999[.,]999/); f.c.draft.value[0].quantity = '2'; assert.equal(f.c.valid(), false);
});
test('existing prescription edit uses expected version and original price', async () => {
 const f = fixture(); f.controls.context.prescription = { id: 4, version: 2, diagnosis: 'Independent', doctorAdvice: null, revisitDate: null, items: [{ id: 20, medicineId: 9, medicineVersion: 2, medicineCode: 'OLD', medicineName: 'Old snapshot', activeIngredient: 'Test', strength: null, unit: 'Viên', unitPrice: '300', quantity: 2, usageInstructions: 'Saved', treatmentDays: null, medicineActive: true }] }; await f.c.load(); assert.equal(f.c.draft.value[0].unitPrice, '300'); await approve(f); await f.c.save(); assert.equal(writes(f)[0][2], false); assert.equal(writes(f)[0][3].expectedVersion, 2);
});
test('route requires auth, templates have no enabled control, service uses JWT apiClient and preview', async () => {
 const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const service = await readFile(new URL('../src/services/prescriptions.ts', import.meta.url), 'utf8');
 assert.match(router, /PrescriptionView/); assert.match(source, /Chưa có đơn mẫu đã được duyệt/); assert.doesNotMatch(source, /@click=".*[Tt]emplate/); assert.match(service, /apiClient/); assert.match(service, /prescription\/preview/);
});
