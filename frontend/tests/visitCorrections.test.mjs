import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/InvoiceDraftView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'correction-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const original = { visitId: 7, patientId: 1, status: 'Correcting', dentistId: 2, isLocked: false,
  diagnosis: 'Original', clinicalNotes: null, startedAt: '2026-10-15T01:00:00Z', endedAt: '2026-10-15T02:00:00Z' };
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
function fixture(role = 'DENTIST', initial = original, overrides = {}) {
  let cleanup, current = { ...initial }, bill = { id: 10, invoiceCode: 'NEW-001', status: 0, paidAmount: 0, items: [] };
  const calls = [], auth = { role, user: { userId: 2 } }, route = { params: { visitId: '7' } };
  const ref = value => ({ value });
  const store = { invoice: ref(null), replace(value) { this.invoice.value = value; } };
  const imports = {
    vue: { defineComponent: x => x, ref, computed: fn => ({ get value() { return fn(); } }), watch() {}, onUnmounted(cb) { cleanup = cb; } },
    'vue-router': { useRoute: () => route }, pinia: { storeToRefs: s => ({ invoice: s.invoice }) },
    axios: { default: { isAxiosError: error => error?.isAxiosError === true } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/invoiceDraft': { useInvoiceDraftStore: () => store },
    '@/services/visits': { visitsService: {
      getById: async id => { calls.push(['get', id]); return overrides.get ? overrides.get(id) : { ...current }; },
      updateDiagnosis: async (id, payload) => { calls.push(['save', id, payload]);
        if (overrides.save) return overrides.save(id, payload);
        current = { ...current, diagnosis: payload.diagnosis, clinicalNotes: payload.clinicalNotes }; return { ...current }; },
    } },
    '@/services/invoiceDraft': { invoiceDraftApi: {
      get: async id => { calls.push(['invoice', id]); return { ...bill }; }, unlockHistory: async () => [],
      unlock: async (id, reason) => { calls.push(['unlock', id, reason]);
        if (overrides.unlock) return overrides.unlock(id, reason);
        current = { ...current, status: 'Correcting', isLocked: false }; bill = { ...bill, invoiceCode: 'NEW-002', status: 0 }; },
      complete: async id => { calls.push(['complete', id]);
        if (overrides.complete) return overrides.complete(id);
        current = { ...current, status: 'Completed', isLocked: true }; bill = { ...bill, status: 1 }; },
    } },
    '@/components/InvoiceRefundPanel.vue': {}, '@/components/InvoiceRefundProposalPanel.vue': {}, '@/components/InvoiceCashPayment.vue': {}, '@/components/InvoiceBankTransfer.vue': {}, '@/components/InvoiceManualBankTransfer.vue': {}, '@/components/InvoiceDiscountPanel.vue': {},
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => { if (!(name in imports)) throw new Error('Unexpected import ' + name); return imports[name]; }, module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, calls, auth, route, cleanup: () => cleanup?.(), writes: name => calls.filter(c => c[0] === name) };
}
test('manual and webhook receipt messages never claim a simulation', async () => {
  const f = fixture();
  for (const source of ['Manual', 'Webhook', 'Simulation']) {
    await f.c.onBankReceived({ id: 7, amount: 30, source, note: null });
    assert.equal(f.c.success.value.includes('MÔ PHỎNG'), source === 'Simulation');
  }
  f.cleanup();
});

test('correction loads outside the queue with explicit controls', async () => {
  const f = fixture(); await f.c.load();
  assert.equal(f.c.isCorrection.value, true); assert.equal(f.c.canCorrect.value, true); assert.equal(f.c.canComplete.value, true);
  assert.equal(f.c.correctionDiagnosis.value, 'Original'); assert.equal(f.c.canUnlock.value, false);
  assert.ok(source.includes('Đính chính hồ sơ ngoài ca khám')); assert.ok(source.includes('không chiếm ghế')); f.cleanup();
});
test('Admin opens correction and checks the actual refreshed mode', async () => {
  const f = fixture('ADMIN', { ...original, status: 'Completed', isLocked: true }); await f.c.load();
  f.c.unlockReason.value = '  Correction reason  '; await f.c.unlock();
  assert.equal(f.writes('unlock').length, 1); assert.equal(f.writes('unlock')[0][2], 'Correction reason');
  assert.equal(f.c.visit.value.status, 'Correcting'); assert.match(f.c.success.value, /mở đính chính/); f.cleanup();
});
for (const role of ['RECEPTIONIST', 'ASSISTANT', 'PATIENT']) test(role + ' cannot edit or finalize', async () => {
  const f = fixture(role); await f.c.load(); f.c.correctionDiagnosis.value = 'Changed'; f.c.confirmed.value = true;
  await f.c.saveCorrection(); await f.c.complete(); assert.equal(f.c.canCorrect.value, false); assert.equal(f.c.canComplete.value, false);
  assert.equal(f.writes('save').length + f.writes('complete').length, 0); f.cleanup();
});
test('unrelated dentist cannot edit or complete', async () => {
  const f = fixture('DENTIST', { ...original, dentistId: 3 }); await f.c.load(); f.c.correctionDiagnosis.value = 'Changed'; f.c.confirmed.value = true;
  await f.c.saveCorrection(); await f.c.complete(); assert.equal(f.c.canCorrect.value, false);
  assert.equal(f.writes('save').length + f.writes('complete').length, 0); f.cleanup();
});
test('unsaved diagnosis blocks completion and saving clears confirmation', async () => {
  const f = fixture(); await f.c.load(); f.c.confirmed.value = true; f.c.correctionDiagnosis.value = ' Changed ';
  assert.equal(f.c.correctionDirty.value, true); assert.equal(f.c.canComplete.value, false); await f.c.complete(); assert.equal(f.writes('complete').length, 0);
  await f.c.saveCorrection(); assert.equal(f.writes('save')[0][2].diagnosis, 'Changed'); assert.equal(f.c.confirmed.value, false);
  assert.equal(f.c.correctionDirty.value, false); assert.equal(f.c.canComplete.value, true); f.cleanup();
});
test('invalid diagnosis or note never writes', async () => {
  const f = fixture(); await f.c.load();
  for (const value of [' ', 'x'.repeat(1001)]) { f.c.correctionDiagnosis.value = value; await f.c.saveCorrection(); }
  f.c.correctionDiagnosis.value = 'Valid'; f.c.correctionNotes.value = 'x'.repeat(2001); await f.c.saveCorrection();
  assert.equal(f.writes('save').length, 0); assert.ok(f.c.error.value); f.cleanup();
});
test('double save and completion while saving send one write', async () => {
  const d = deferred(), f = fixture('DENTIST', original, { save: () => d.promise }); await f.c.load(); f.c.correctionDiagnosis.value = 'Changed';
  const pending = f.c.saveCorrection(); await f.c.saveCorrection(); f.c.confirmed.value = true; await f.c.complete();
  assert.equal(f.writes('save').length, 1); assert.equal(f.writes('complete').length, 0);
  d.resolve({ ...original, diagnosis: 'Changed' }); await pending; f.cleanup();
});
test('conflict requires explicit successful reload before another write', async () => {
  const f = fixture('DENTIST', original, { save: async () => { throw { isAxiosError: true, response: { data: { message: 'Reload conflict' } } }; } });
  await f.c.load(); f.c.correctionDiagnosis.value = 'Changed'; await f.c.saveCorrection();
  assert.equal(f.c.correctionNeedsReview.value, true); assert.equal(f.c.canCorrect.value, false);
  await f.c.saveCorrection(); f.c.confirmed.value = true; await f.c.complete();
  assert.equal(f.writes('save').length, 1); assert.equal(f.writes('complete').length, 0); assert.match(f.c.error.value, /Reload/);
  await f.c.load(); assert.equal(f.c.correctionNeedsReview.value, false); f.cleanup();
});
test('malformed save cannot claim success', async () => {
  const f = fixture('DENTIST', original, { save: async () => ({ ...original, visitId: 8 }) }); await f.c.load();
  f.c.correctionDiagnosis.value = 'Changed'; await f.c.saveCorrection(); assert.equal(f.c.correctionNeedsReview.value, true); assert.equal(f.c.success.value, ''); f.cleanup();
});
test('confirmation is required and completion relocks through existing API', async () => {
  const f = fixture(); await f.c.load(); await f.c.complete(); assert.equal(f.writes('complete').length, 0);
  f.c.confirmed.value = true; await f.c.complete(); assert.equal(f.writes('complete').length, 1);
  assert.equal(f.c.visit.value.status, 'Completed'); assert.equal(f.c.visit.value.isLocked, true); assert.match(f.c.success.value, /hoàn tất đính chính/); f.cleanup();
});
test('double completion sends once and ignores late response after unmount', async () => {
  const d = deferred(), f = fixture('DENTIST', original, { complete: () => d.promise }); await f.c.load(); f.c.confirmed.value = true;
  const pending = f.c.complete(); await f.c.complete(); assert.equal(f.writes('complete').length, 1);
  f.cleanup(); d.resolve(); await pending; assert.equal(f.c.visit.value, null); assert.equal(f.c.invoice.value, null); assert.equal(f.c.success.value, '');
});
test('late diagnosis after unmount cannot restore sensitive fields', async () => {
  const d = deferred(), f = fixture('DENTIST', original, { save: () => d.promise }); await f.c.load(); f.c.correctionDiagnosis.value = 'Changed';
  const pending = f.c.saveCorrection(); f.cleanup(); d.resolve({ ...original, diagnosis: 'Changed' }); await pending;
  assert.equal(f.c.visit.value, null); assert.equal(f.c.correctionDiagnosis.value, ''); assert.equal(f.c.correctionNotes.value, '');
});
test('FDI views expose correction mode while preserving ownership', async () => {
  for (const name of ['FdiConditionView', 'FdiServiceAssignmentView']) {
    const text = await readFile(new URL('../src/views/' + name + '.vue', import.meta.url), 'utf8');
    assert.ok(text.includes("['InProgress', 'Correcting'].includes")); assert.ok(text.includes('visit.value?.dentistId === auth.user?.userId'));
    assert.ok(text.includes('Trở lại để hoàn tất và khóa đính chính'));
  }
});
