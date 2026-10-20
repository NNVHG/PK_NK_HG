import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/ChairStatusView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const transpile = text => ts.transpileModule(text, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const compiled = transpile(compileScript(descriptor, { id: 'chair-status-test' }).content);
const row = { id: 7, chairNumber: 'GHE-01', roomName: 'Phòng 1', status: 1, statusText: 'Sẵn sàng', isActive: true, version: 1, isAssigned: false, hasActiveVisit: false };
const page = (items = [row], n = 1) => ({ items, page: n, totalCount: items.length, hasNextPage: false });
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { resolve, promise }; }
function fixture({ role = 'ASSISTANT', read, write } = {}) {
  let mount, cleanup, changed; const reads = [], writes = [], auth = { role, user: { userId: 99 } };
  const store = { result: null, replace(value) { this.result = value; }, reset() { this.result = null; } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), nextTick: async () => {}, onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; }, watch: (_, cb) => { changed = cb; } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/chairStatus': { useChairStatusStore: () => store },
    '@/services/chairStatus': { chairStatusService: {
      getPage: async q => { reads.push(q); return read ? read(q, reads.length) : page([row], q.page); },
      change: async (id, input) => { writes.push({ id, input }); return write ? write(id, input) : { ...row, id, status: input.status, isActive: input.status !== 4, version: 2 }; }
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, store, reads, writes, auth, mount: () => mount(), cleanup: () => cleanup(), changed: () => changed() };
}
async function edit(f, status = 3) { await f.c.open(row); f.c.targetStatus.value = status; f.c.confirmed.value = true; }
test('operational list reads server paging/search/status and no catalog fields', async () => {
  const f = fixture(); f.c.keyword.value = ' GHE '; f.c.filterStatus.value = 3; await f.mount();
  assert.deepEqual(f.reads, [{ page: 1, pageSize: 20, keyword: 'GHE', status: 3 }]); assert.equal(f.store.result.items[0].id, 7);
});
test('both Admin and Assistant can confirm a status-only change', async () => {
  for (const role of ['ADMIN', 'ASSISTANT']) { const f = fixture({ role }); await f.mount(); await edit(f); await f.c.save();
    assert.deepEqual(f.writes, [{ id: 7, input: { status: 3, expectedVersion: 1, confirmed: true } }]); assert.equal(f.reads.length, 2); assert.equal(f.c.selected.value, null); assert.ok(f.c.message.value); }
});
test('other roles cannot read/open/change', async () => {
  for (const role of ['DENTIST', 'RECEPTIONIST', 'PATIENT', '']) { const f = fixture({ role }); await f.mount(); await edit(f); await f.c.save(); assert.equal(f.reads.length, 0); assert.equal(f.writes.length, 0); }
});
test('confirmation is required and cancel never mutates', async () => {
  const f = fixture(); await f.mount(); await f.c.open(row); f.c.targetStatus.value = 3; await f.c.save(); assert.equal(f.writes.length, 0);
  f.c.close(); assert.equal(f.c.selected.value, null); assert.equal(f.c.confirmed.value, false);
});
test('occupied, assigned and actual active visit each disable manual changes', async () => {
  for (const value of [{ ...row, status: 2 }, { ...row, isAssigned: true }, { ...row, hasActiveVisit: true }]) {
    const f = fixture({ read: () => page([value]) }); await f.mount(); assert.equal(f.c.canEdit(value), false); await f.c.open(value); assert.equal(f.c.selected.value, null); assert.equal(f.writes.length, 0);
  }
});
test('unknown/stale list rows cannot be opened', async () => {
  const f = fixture(); await f.mount(); await f.c.open({ ...row, id: 8 }); await f.c.open({ ...row, version: 2 }); assert.equal(f.c.selected.value, null);
});
test('invalid target and unchanged status never reach API', async () => {
  for (const status of [0, 1, 2, 5, -1]) { const f = fixture(); await f.mount(); await edit(f, status); await f.c.save(); assert.equal(f.writes.length, 0); assert.ok(f.c.error.value); }
});
test('can stop and restore an inactive chair without touching catalog fields', async () => {
  const f = fixture(); await f.mount(); await edit(f, 4); await f.c.save(); assert.equal(f.writes[0].input.status, 4);
  const off = { ...row, status: 4, isActive: false }; const g = fixture({ read: () => page([off]) }); await g.mount(); await g.c.open(off); g.c.targetStatus.value = 1; g.c.confirmed.value = true; await g.c.save(); assert.equal(g.writes[0].input.status, 1);
});
test('invalid page/filter/keyword never read', async () => {
  const f = fixture(); for (const n of [0, -1, 1.5, 1_000_001]) await f.c.load(n);
  f.c.keyword.value = 'x'.repeat(101); await f.c.load(); f.c.keyword.value = ''; f.c.filterStatus.value = 5; await f.c.load(); assert.equal(f.reads.length, 0);
});
test('empty server list remains empty', async () => { const f = fixture({ read: () => page([]) }); await f.mount(); assert.deepEqual(f.store.result.items, []); });
test('malformed rows and paging cannot unlock edits', async () => {
  for (const value of [page([{ ...row, version: 0 }]), page([{ ...row, isAssigned: undefined }]), { ...page(), page: 2 }, null]) {
    const f = fixture({ read: () => value }); await f.mount(); assert.equal(f.store.result, null); assert.ok(f.c.error.value);
  }
});
test('late read cannot overwrite a newer page', async () => {
  const a = deferred(), b = deferred(); const f = fixture({ read: (_, n) => n === 1 ? a.promise : b.promise });
  const first = f.c.load(), second = f.c.load(2); b.resolve(page([], 2)); await second; a.resolve(page()); await first; assert.equal(f.store.result.page, 2);
});
test('double submit, reload and close during save are blocked', async () => {
  const d = deferred(), f = fixture({ write: () => d.promise }); await f.mount(); await edit(f); const saving = f.c.save(); await f.c.save(); await f.c.load(); f.c.close();
  assert.equal(f.writes.length, 1); assert.equal(f.reads.length, 1); assert.ok(f.c.selected.value); d.resolve({ ...row, status: 3, version: 2 }); await saving;
});
test('conflict retains draft but requires successful reread and fresh confirmation', async () => {
  const f = fixture({ write: () => { throw { response: { data: { message: 'Synthetic assigned conflict' } } }; } }); await f.mount(); await edit(f); await f.c.save(); await f.c.save();
  assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.confirmed.value, false); assert.match(f.c.error.value, /assigned/);
  await f.c.load(); assert.equal(f.c.needsReview.value, false); assert.equal(f.c.selected.value, null);
});
test('unknown network result never automatically retries', async () => {
  const f = fixture({ write: () => { throw new Error('Unknown result'); } }); await f.mount(); await edit(f); await f.c.save(); f.c.confirmed.value = true; await f.c.save();
  assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true); assert.match(f.c.error.value, /Tải lại/);
});
test('failed refresh after success preserves review lock', async () => {
  const f = fixture({ read: (_, n) => { if (n > 1) throw new Error('Offline'); return page(); } }); await f.mount(); await edit(f); await f.c.save(); assert.equal(f.c.needsReview.value, true); assert.equal(f.store.result, null);
});
test('wrong save response cannot be reported as success', async () => {
  for (const value of [{ ...row, id: 8, status: 3, version: 2 }, { ...row, status: 3 }, { ...row, status: 3, version: 2, isAssigned: true }, { ...row, status: 4, isActive: true, version: 2 }]) {
    const f = fixture({ write: () => value }); await f.mount(); await edit(f, value.status); await f.c.save(); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.message.value, '');
  }
});
test('role or actor changes while reading discard previous response', async () => {
  const d = deferred(), f = fixture({ read: () => d.promise }); const pending = f.mount(); f.auth.user.userId = 100; d.resolve(page()); await pending; assert.equal(f.store.result, null);
});
test('late save after identity switch is ignored', async () => {
  const d = deferred(), f = fixture({ write: () => d.promise }); await f.mount(); await edit(f); const pending = f.c.save(); f.auth.role = 'PATIENT'; f.changed();
  d.resolve({ ...row, status: 3, version: 2 }); await pending; assert.equal(f.store.result, null); assert.equal(f.c.selected.value, null); assert.equal(f.c.message.value, '');
});
test('role loss before submit prevents writes', async () => { const f = fixture(); await f.mount(); await edit(f); f.auth.role = 'DENTIST'; await f.c.save(); assert.equal(f.writes.length, 0); });
test('unmount clears state and ignores late read', async () => {
  const d = deferred(), f = fixture({ read: () => d.promise }); const pending = f.mount(); f.cleanup(); d.resolve(page()); await pending; assert.equal(f.store.result, null); assert.equal(f.c.loading.value, false);
});
test('editor focus and cancel focus return use native elements', async () => {
  const f = fixture(); await f.mount(); let editor = 0, trigger = 0; f.c.editorHeading.value = { focus: () => editor++ };
  await f.c.open(row, { currentTarget: { focus: () => trigger++ } }); f.c.close(); assert.equal(editor, 1); assert.equal(trigger, 1);
});
test('route/menu restrict status management while catalog stays Admin-only', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  assert.match(router, /path: '\/staff\/chair-status'[^\n]*roles: \['ADMIN', 'ASSISTANT'\]/);
  assert.match(app, /v-if="\['ADMIN', 'ASSISTANT'\].includes\(authStore.role\)" to="\/staff\/chair-status"/);
  assert.match(router, /path: '\/admin\/chairs'[^\n]*roles: \['ADMIN'\]/);
  assert.match(source, /to="\/staff\/workspace"/); assert.match(source, /@change="confirmed = false"/);
});
test('service sends only status DTO to protected operational route', async () => {
  const calls = []; const imports = { '@/services/api': { apiClient: { get: async (...a) => { calls.push(['GET', ...a]); return { data: page() }; }, put: async (...a) => { calls.push(['PUT', ...a]); return { data: row }; } } } };
  const module = { exports: {} }; const code = transpile(await readFile(new URL('../src/services/chairStatus.ts', import.meta.url), 'utf8'));
  new Function('require', 'module', 'exports', code)(name => imports[name], module, module.exports);
  await module.exports.chairStatusService.getPage({ page: 1, pageSize: 20 }); await module.exports.chairStatusService.change(7, { status: 3, expectedVersion: 1, confirmed: true });
  assert.equal(calls[0][1], '/chair-status'); assert.equal(calls[1][1], '/chair-status/7');
});
