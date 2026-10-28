import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/MedicineCatalogView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'medicine-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const item = { id: 7, code: 'TEST', name: 'Synthetic drug', activeIngredient: 'Test', strength: null, unit: 'Viên', unitPrice: '1000', defaultUsageInstruction: null, isActive: true, version: 1, createdAt: '2025-01-01T00:00:00Z', updatedAt: null };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture(role = 'ADMIN') {
 const auth = { role, user: { userId: 1 } }, calls = [], watchers = []; let cleanup, pending, pendingList, fail = false;
 const module = { exports: {} };
 const api = {
  list: async (...args) => { calls.push(['list', ...args]); return pendingList ? await pendingList : { items: [item], page: args[1], totalCount: 1, hasNextPage: false }; },
  save: async (id, payload) => { calls.push(['save', id, payload]); if (fail) throw Error('Synthetic'); return pending ? await pending : { ...item, ...payload, id: id ?? 8, version: id === null ? 1 : payload.expectedVersion + 1 }; },
  deactivate: async (id, version) => { calls.push(['deactivate', id, version]); if (fail) throw Error('Synthetic'); return pending ? await pending : { ...item, id, version: version + 1, isActive: false }; }
 };
 new Function('require', 'module', 'exports', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), reactive: x => x, watch: (getter, cb) => { watchers.push(cb); }, onMounted() {}, onUnmounted: cb => { cleanup = cb; } } : name === '@/stores/auth' ? { useAuthStore: () => auth } : { medicinesService: api }, module, module.exports);
 const c = module.exports.default.setup({}, { expose() {} });
 return { c, auth, calls, watchers, fail: () => { fail = true; }, pending: p => { pending = p; }, list: p => { pendingList = p; }, cleanup: () => cleanup() };
}
test('Admin reads inactive seed by default and can create priced active medicine after confirmation', async () => {
 const f = fixture(); await f.c.load(); assert.equal(f.calls[0][3], true); f.c.open(); Object.assign(f.c.form, { code: 'NEW', name: 'New', unitPrice: '5000', isActive: true });
 await f.c.save(); assert.equal(f.calls.filter(c => c[0] === 'save').length, 0); f.c.confirmed.value = true; await f.c.save();
 const save = f.calls.find(c => c[0] === 'save'); assert.equal(save[1], null); assert.equal(save[2].unitPrice, '5000'); assert.equal(f.c.editor.value, false);
});
for (const role of ['RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT', '']) test(role + ' cannot read or manage medicine master', async () => {
 const f = fixture(role); await f.c.load(); f.c.open(item); f.c.confirmed.value = true; await f.c.save(); await f.c.deactivate(); assert.equal(f.calls.length, 0);
});
test('zero price allowed while inactive and blocked when activating', async () => {
 const f = fixture(); await f.c.load(); f.c.open(); Object.assign(f.c.form, { code: 'ZERO', name: 'Zero', unitPrice: '0', isActive: true }); f.c.confirmed.value = true; await f.c.save(); assert.equal(f.calls.filter(c => c[0] === 'save').length, 0);
 f.c.form.isActive = false; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.calls.find(c => c[0] === 'save')[2].isActive, false);
});
test('exact18digit price never passes through Number conversion', async () => {
 const f = fixture(); await f.c.load(); f.c.open(item); f.c.form.unitPrice = '999999999999999999'; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.calls.find(c => c[0] === 'save')[2].unitPrice, '999999999999999999'); assert.match(f.c.money('999999999999999999'), /999[.,]999[.,]999[.,]999[.,]999[.,]999/);
});
test('invalid money/unit/length blocks request', async () => {
 for (const patch of [{ unitPrice: '-1' }, { unitPrice: '1.5' }, { unitPrice: '1000000000000000000' }, { unitPrice: '1e5' }, { unit: 'Khác' }, { name: 'x'.repeat(201) }, { activeIngredient: 'x'.repeat(201) }, { strength: 'x'.repeat(51) }, { defaultUsageInstruction: 'x'.repeat(501) }]) {
  const f = fixture(); await f.c.load(); f.c.open(item); Object.assign(f.c.form, patch); f.c.confirmed.value = true; await f.c.save(); assert.equal(f.calls.filter(c => c[0] === 'save').length, 0);
 }
});
test('deactivation preserves identity and uses expected version after confirmation', async () => {
 const f = fixture(); await f.c.load(); f.c.open(item); await f.c.deactivate(); assert.equal(f.calls.filter(c => c[0] === 'deactivate').length, 0); f.c.confirmed.value = true; await f.c.deactivate(); assert.deepEqual(f.calls.find(c => c[0] === 'deactivate'), ['deactivate', 7, 1]);
});
test('uncertain write blocks another save until successful reload and resets confirmation', async () => {
 const f = fixture(); await f.c.load(); f.c.open(item); f.c.confirmed.value = true; f.fail(); await f.c.save(); await f.c.save(); assert.equal(f.calls.filter(c => c[0] === 'save').length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.confirmed.value, false); await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.c.editor.value, false);
});
test('double submit, load and close blocked during write', async () => {
 const f = fixture(), d = deferred(); await f.c.load(); f.c.open(item); f.pending(d.promise); f.c.confirmed.value = true; const pending = f.c.save(); await f.c.save(); await f.c.load(); f.c.close(); assert.equal(f.c.editor.value, true); assert.equal(f.calls.length, 2); d.resolve({ ...item, version: 2 }); await pending;
});
test('unknown ID and stale response cannot claim success', async () => {
 const f = fixture(); await f.c.load(); f.c.open({ ...item, id: 9 }); assert.equal(f.c.editor.value, false); f.c.open(item); f.pending({ ...item, id: 9, version: 2 }); f.c.confirmed.value = true; await f.c.save(); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.message.value, '');
});
test('changing form invalidates prior confirmation', async () => {
 const f = fixture(); await f.c.load(); f.c.open(item); f.c.confirmed.value = true; f.c.form.unitPrice = '2000'; f.watchers[1](); assert.equal(f.c.confirmed.value, false);
});
test('out of order reads and changed filters cannot restore old rows', async () => {
 const f = fixture(), a = deferred(), b = deferred(); f.list(a.promise); const old = f.c.load(); f.c.keyword.value = 'new'; f.list(b.promise); const current = f.c.load(); b.resolve({ items: [], page: 1, totalCount: 0, hasNextPage: false }); await current; a.resolve({ items: [item], page: 1, totalCount: 1, hasNextPage: false }); await old; assert.equal(f.c.items.value.length, 0);
});
test('auth change and unmount ignore late writes and clear drug form', async () => {
 const f = fixture(), d = deferred(); await f.c.load(); f.c.open(item); f.pending(d.promise); f.c.confirmed.value = true; const pending = f.c.save(); f.auth.role = 'DENTIST'; f.watchers[0](); d.resolve({ ...item, version: 2 }); await pending; assert.equal(f.c.items.value.length, 0); assert.equal(f.c.message.value, ''); assert.equal(f.c.form.name, '');
 const g = fixture(), e = deferred(); g.list(e.promise); const read = g.c.load(); g.cleanup(); e.resolve({ items: [item], page: 1, totalCount: 1, hasNextPage: false }); await read; assert.equal(g.c.items.value.length, 0);
});
test('route and navigation only Admin and no inventory controls', async () => {
 const route = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8'); assert.match(route, /path: '\/management\/admin\/medicines'.*roles: \['ADMIN'\]/); assert.match(app, /v-if="isAdmin" to="\/management\/admin\/medicines"/); assert.doesNotMatch(source, /StockQuantity|StockIssue|tồn kho/);
});
