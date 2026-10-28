import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/StaffWorkspacesView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'workspaces' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const dentist = { userId: 5, fullName: 'Synthetic dentist', roleCode: 'DENTIST', isActive: true, version: 1,
  workingDentistId: 5, dentistName: 'Synthetic dentist', chairId: 1, chairNumber: 'GHE-01', roomName: 'Phòng 3', isLocationUsable: true, canChange: true };
const assistant = { ...dentist, userId: 6, fullName: 'Synthetic assistant', roleCode: 'ASSISTANT' };
const choices = { chairs: [
  { id: 1, chairNumber: 'GHE-01', roomName: 'Phòng 3', status: 1, isActive: true, claimedByUserId: 5 },
  { id: 2, chairNumber: 'GHE-02', roomName: 'Phòng 4', status: 1, isActive: true, claimedByUserId: null }],
  dentists: [{ dentistId: 5, dentistName: 'Synthetic dentist', chairId: 1, chairNumber: 'GHE-01', roomName: 'Phòng 3', assistantCount: 1, isReady: true },
    { dentistId: 7, dentistName: 'Synthetic dentist2', chairId: 2, chairNumber: 'GHE-02', roomName: 'Phòng 4', assistantCount: 0, isReady: true }] };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'DENTIST', snapshot, options = choices, fail = false, result, read, readFailureAfter = Infinity } = {}) {
  let mount, cleanup, tick, cleared = false; const reads = [], writes = [], targets = [];
  const auth = { role, user: { userId: role === 'ASSISTANT' ? 6 : role === 'ADMIN' ? 99 : 5 } };
  const current = snapshot ?? (role === 'ASSISTANT' ? assistant : dentist);
  const store = { snapshot: null, reset() { this.snapshot = null; }, replace(value) { this.snapshot = value; } };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted: cb => { mount = cb; }, onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/workspaces': { useWorkspacesStore: () => store },
    '@/services/workspaces': { workspacesService: {
      get: async id => { reads.push(id); if (reads.length > readFailureAfter) throw new Error('Read failure'); return read ? read(id) : { ...current, userId: id }; },
      options: async () => options,
      targets: async (...args) => { targets.push(args); return { items: [{ userId: 5, fullName: 'Synthetic dentist', roleCode: 'DENTIST', isActive: true }], page: args[0], totalCount: 1, hasNextPage: false }; },
      change: async (id, input) => { writes.push([id, input]); if (fail) throw { response: { data: { message: 'Synthetic409' } } }; return result ?? { ...current, userId: id, version: current.version + 1 }; }
    } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', 'setInterval', 'clearInterval', compiled)(name => imports[name], module, module.exports,
    callback => { tick = callback; return 42; }, () => { cleared = true; });
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, auth, store, reads, writes, targets, mount: () => mount(), cleanup: () => cleanup(), tick: async () => { tick(); await new Promise(done => setImmediate(done)); }, cleared: () => cleared };
}
test('Dentist loads self and confirms available chair, never another staff ID', async () => {
  const f = fixture(); await f.mount(); assert.deepEqual(f.reads, [5]); f.c.chairChoice.value = 2; f.c.markDirty(); await f.c.save(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; await f.c.save(); assert.deepEqual(f.writes, [[5, { chairId: 2, dentistId: null, expectedVersion: 1, confirmed: true }]]); assert.equal(f.reads.length, 2);
});
test('Assistant chooses dentist and sends no independently selected chair', async () => {
  const f = fixture({ role: 'ASSISTANT' }); await f.mount(); assert.equal(f.store.snapshot.roomName, 'Phòng 3'); f.c.dentistChoice.value = 7; f.c.markDirty(); f.c.chairChoice.value = 999; f.c.confirmed.value = true;
  await f.c.save(); assert.deepEqual(f.writes, [[6, { chairId: null, dentistId: 7, expectedVersion: 1, confirmed: true }]]);
});
test('Receptionist and Patient cannot load or change workspace', async () => {
  for (const role of ['RECEPTIONIST', 'PATIENT']) { const f = fixture({ role }); await f.mount(); await f.c.load(5); f.c.confirmed.value = true; await f.c.save(); assert.equal(f.reads.length, 0); assert.equal(f.writes.length, 0); }
});
test('non-admin cannot read another staff ID', async () => {
  const f = fixture(); await f.mount(); await f.c.load(7); assert.deepEqual(f.reads, [5]);
});
test('Admin loads bounded staff list then changes selected person using their snapshot', async () => {
  const f = fixture({ role: 'ADMIN' }); await f.mount(); assert.equal(f.reads.length, 0); assert.deepEqual(f.targets, [[1, undefined]]);
  await f.c.load(5); f.c.chairChoice.value = 2; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes[0][0], 5);
  await f.c.loadTargets(0); f.c.keyword.value = 'x'.repeat(101); await f.c.loadTargets(); assert.equal(f.targets.length, 1);
});
test('busy snapshot cannot change chair or unlink', async () => {
  const f = fixture({ snapshot: { ...dentist, canChange: false } }); await f.mount(); f.c.chairChoice.value = 0; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('maintenance inactive unknown and claimed chairs cannot be submitted', async () => {
  for (const patch of [{ status: 3 }, { isActive: false }, { claimedByUserId: 7 }]) {
    const f = fixture({ options: { ...choices, chairs: [choices.chairs[0], { ...choices.chairs[1], ...patch }] } }); await f.mount(); f.c.chairChoice.value = 2; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes.length, 0);
  }
  const f = fixture(); await f.mount(); f.c.chairChoice.value = 99; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('assistant can join a dentist who already has another assistant', async () => {
  const f = fixture({ role: 'ASSISTANT', options: { ...choices, dentists: [{ ...choices.dentists[0], assistantCount: 2 }, choices.dentists[1]] } });
  await f.mount(); f.c.dentistChoice.value = 5; f.c.confirmed.value = true; await f.c.save();
  assert.deepEqual(f.writes[0], [6, { chairId: null, dentistId: 5, expectedVersion: 1, confirmed: true }]);
});
test('unready dentist cannot be submitted', async () => {
  const f = fixture({ role: 'ASSISTANT', options: { ...choices, dentists: [choices.dentists[0], { ...choices.dentists[1], isReady: false }] } });
  await f.mount(); f.c.dentistChoice.value = 7; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('explicit empty choice clears relation without copying a room', async () => {
  for (const role of ['DENTIST', 'ASSISTANT']) { const f = fixture({ role }); await f.mount(); f.c.chairChoice.value = 0; f.c.dentistChoice.value = 0; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.writes[0][1].chairId, null); assert.equal(f.writes[0][1].dentistId, null); }
});
test('polling refreshes inherited room but never overwrites a dirty draft', async () => {
  let room = 'Phòng 3'; const f = fixture({ role: 'ASSISTANT', read: () => ({ ...assistant, roomName: room }) }); await f.mount(); room = 'Phòng 4'; await f.tick(); assert.equal(f.store.snapshot.roomName, 'Phòng 4');
  f.c.markDirty(); const count = f.reads.length; await f.tick(); assert.equal(f.reads.length, count);
});
test('duplicate write and polling during save cannot cause another mutation or read', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.chairChoice.value = 2; f.c.confirmed.value = true; const pending = f.c.save(); await f.c.save(); await f.tick(); await f.c.load(5); assert.equal(f.writes.length, 1); assert.equal(f.reads.length, 1);
  d.resolve({ ...dentist, version: 2 }); await pending;
});
test('failed mutation requires explicit reload and confirmation, no auto retry', async () => {
  const f = fixture({ fail: true }); await f.mount(); f.c.chairChoice.value = 2; f.c.confirmed.value = true; await f.c.save(); await f.c.save(); await f.tick(); assert.equal(f.writes.length, 1); assert.equal(f.c.needsReview.value, true); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.error.value, 'Synthetic409');
  await f.c.load(5); assert.equal(f.c.needsReview.value, false);
});
test('failed post-save refresh keeps writes blocked', async () => {
  const f = fixture({ readFailureAfter: 1 }); await f.mount(); f.c.chairChoice.value = 2; f.c.confirmed.value = true; await f.c.save(); assert.equal(f.c.needsReview.value, true); assert.equal(f.store.snapshot, null);
});
test('old response cannot replace another Admin target selection', async () => {
  const a = deferred(), b = deferred(); const f = fixture({ role: 'ADMIN', read: id => id === 5 ? a.promise : b.promise }); await f.mount();
  const first = f.c.load(5), second = f.c.load(7); b.resolve({ ...dentist, userId: 7 }); await second; a.resolve(dentist); await first; assert.equal(f.store.snapshot.userId, 7);
});
test('unmount clears timer, snapshot and drafts and ignores late save', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.mount(); f.c.chairChoice.value = 2; f.c.confirmed.value = true; const pending = f.c.save(); f.cleanup(); d.resolve({ ...dentist, version: 2 }); await pending;
  assert.equal(f.cleared(), true); assert.equal(f.store.snapshot, null); assert.equal(f.c.confirmed.value, false); assert.equal(f.c.message.value, ''); assert.equal(f.reads.length, 1);
});
test('unmount during initial read never starts a polling timer or restores snapshot', async () => {
  const d = deferred(), f = fixture({ read: () => d.promise }); const pending = f.mount(); f.cleanup(); d.resolve(dentist); await pending; assert.equal(f.store.snapshot, null); assert.equal(f.cleared(), false);
});
test('role or principal changes cannot use old snapshot to mutate', async () => {
  const f = fixture(); await f.mount(); f.c.confirmed.value = true; f.auth.user.userId = 7; await f.c.save(); assert.equal(f.writes.length, 0); f.auth.role = 'RECEPTIONIST'; await f.c.save(); assert.equal(f.writes.length, 0);
});
test('route allows only Admin Dentist Assistant and navigation is role guarded', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  assert.match(router, /StaffWorkspacesView[\s\S]*?roles: \['ADMIN', 'DENTIST', 'ASSISTANT'\]/); assert.match(app, /Vị trí nha sĩ/);
});
