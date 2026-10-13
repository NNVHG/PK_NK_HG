import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/AppointmentReminderPanel.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'reminder-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const page = { items: [{ id: 1, appointmentId: 7, messageContent: 'Synthetic message' }], page: 1, totalCount: 21, hasNextPage: true };
const scan = { eligibleCount: 2, createdCount: 1, alreadyRemindedCount: 1, windowStart: '2026-10-14T08:30:00', windowEnd: '2026-10-14T09:30:00' };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ reads = [page], result = scan, failure = false } = {}) {
  let cleanup; const gets = [], writes = [];
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), onUnmounted: cb => { cleanup = cb; } },
    axios: { isAxiosError: e => Boolean(e?.response) },
    '@/services/appointmentReminders': { appointmentRemindersService: {
      getLogs: async (...args) => { gets.push(args); const next = reads.shift() ?? page; if (next instanceof Error) throw next; return next; },
      simulate: async () => { writes.push(true); if (failure) throw { response: { data: { message: 'Synthetic conflict' } } }; return result; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} }); return { c, gets, writes, cleanup: () => cleanup() };
}
test('opening loads logs on demand without sending a reminder', async () => {
  const f = fixture(); assert.equal(f.gets.length, 0); await f.c.openPanel(); assert.deepEqual(f.c.rows.value, page.items); assert.equal(f.writes.length, 0);
});
test('explicit confirmation required and result refreshes logs without repeat writes', async () => {
  const f = fixture(); await f.c.openPanel(); await f.c.simulate(); assert.equal(f.writes.length, 0);
  f.c.confirmed.value = true; await f.c.simulate(); assert.deepEqual(f.c.scan.value, scan); assert.equal(f.c.confirmed.value, false); assert.equal(f.gets.length, 2); assert.equal(f.writes.length, 1);
});
test('empty scan remains an honest zero result', async () => {
  const zero = { ...scan, eligibleCount: 0, createdCount: 0, alreadyRemindedCount: 0 }; const f = fixture({ result: zero }); await f.c.openPanel(); f.c.confirmed.value = true; await f.c.simulate(); assert.equal(f.c.scan.value.createdCount, 0);
});
test('double submit and closing while writing cannot issue another scan', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.c.openPanel(); f.c.confirmed.value = true;
  const first = f.c.simulate(); await f.c.simulate(); f.c.closePanel(); assert.equal(f.writes.length, 1); assert.equal(f.c.opened.value, true); d.resolve(scan); await first;
});
test('write conflict rereads logs and requires confirmation without mutation retry', async () => {
  const f = fixture({ failure: true }); await f.c.openPanel(); f.c.confirmed.value = true; await f.c.simulate(); await f.c.simulate();
  assert.equal(f.writes.length, 1); assert.equal(f.gets.length, 2); assert.equal(f.c.writeError.value, 'Synthetic conflict'); assert.equal(f.c.confirmed.value, false);
});
test('string or numeric ID filters and pagination are bounded', async () => {
  const f = fixture(); await f.c.openPanel(); f.c.appointmentId.value = 7; await f.c.load(2); assert.deepEqual(f.gets[1], [2, 7]);
  f.c.appointmentId.value = '8'; await f.c.load(1); assert.deepEqual(f.gets[2], [1, 8]);
  for (const id of ['0', '-1', '1.5', 'abc', '2147483648']) { f.c.appointmentId.value = id; await f.c.load(1); }
  f.c.appointmentId.value = ''; await f.c.load(0); assert.equal(f.gets.length, 3);
});
test('close clears message data and ignores late log response', async () => {
  const d = deferred(), f = fixture({ reads: [d.promise] }); const first = f.c.openPanel(); f.c.closePanel(); d.resolve(page); await first;
  assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.opened.value, false); assert.equal(f.c.appointmentId.value, '');
});
test('new filter response wins over old private message response', async () => {
  const d = deferred(), f = fixture({ reads: [d.promise, { ...page, items: [] }] }); const first = f.c.openPanel(); f.c.appointmentId.value = '9'; await f.c.load(1); d.resolve(page); await first; assert.deepEqual(f.c.rows.value, []);
});
test('unmount ignores late simulation result and cannot re-fetch logs', async () => {
  const d = deferred(), f = fixture({ result: d.promise }); await f.c.openPanel(); f.c.confirmed.value = true; const first = f.c.simulate(); f.cleanup(); d.resolve(scan); await first;
  assert.equal(f.c.scan.value, null); assert.deepEqual(f.c.rows.value, []); assert.equal(f.gets.length, 1);
});
test('failed read clears messages and retry can restore list', async () => {
  const f = fixture({ reads: [page, new Error('Synthetic read error'), page] }); await f.c.openPanel(); await f.c.load(1);
  assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.readError.value); await f.c.load(1); assert.deepEqual(f.c.rows.value, page.items);
});
