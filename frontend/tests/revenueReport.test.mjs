import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/RevenueReportView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'report-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
function data(q) {
  return { filter: { ...q, groupBy: 'Day' }, generatedAt: '2025-02-05T00:00:00Z', summary: { grossRevenue: 100, totalRefunded: 0, netRevenue: 100, totalRevenue: 100, totalInvoiced: 0, totalDiscount: 0, totalOutstanding: 900, temporaryCollections: 0, cancelledInvoiceCount: 0 }, dailyRevenue: [], revenueTimeline: [], byPaymentMethod: [], byCashier: [], topServices: [] };
}
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture(role = 'ADMIN') {
  const auth = { role, user: { userId: 1 } }, calls = [], downloads = [], revoked = [];
  let cleanup, authChanged, result, pendingExport, fail = false, printCount = 0, removed = 0;
  const api = { revenue: async q => { calls.push(q); if (fail) throw Error('Synthetic'); return result ? await result : data(q); }, exportRevenue: async q => { downloads.push(q); return pendingExport ? await pendingExport : { blob: new Blob(['test']), filename: 'BaoCao.csv' }; } };
  const document = { createElement: () => ({ remove() { removed++; }, click() { downloads.push('clicked'); } }), body: { appendChild() {} }, head: { appendChild() {} } };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', 'document', 'window', 'URL', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), reactive: x => x, computed: get => ({ get value() { return get(); } }), watch: (getter, cb) => { authChanged = cb; }, onMounted() {}, onUnmounted: cb => { cleanup = cb; } } : name === '@/stores/auth' ? { useAuthStore: () => auth } : { reportsService: api }, module, module.exports, document, { print() { printCount++; } }, { createObjectURL: () => 'blob:synthetic', revokeObjectURL: url => { revoked.push(url); } });
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, auth, calls, downloads, revoked, fail: () => { fail = true; }, result: v => { result = v; }, exportResult: v => { pendingExport = v; }, cleanup: () => cleanup(), authChanged: () => authChanged(), printed: () => printCount, removed: () => removed };
}
test('Admin loads period report and current debt separately; printing does not call another API', async () => {
  const f = fixture(); await f.c.load(); assert.equal(f.calls.length, 1); assert.equal(f.c.report.value.summary.totalOutstanding, 900); assert.equal(f.c.current.value, true);
  f.c.printReport(); assert.equal(f.printed(), 1); assert.equal(f.removed(), 1); assert.equal(f.calls.length, 1);
});
for (const role of ['RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT', '']) test(role + ' cannot query export or print report', async () => {
  const f = fixture(role); await f.c.load(); await f.c.exportCsv(); f.c.printReport(); assert.equal(f.calls.length, 0); assert.equal(f.downloads.length, 0); assert.equal(f.printed(), 0);
});
test('invalid dates/range/cashier and enum never call API', async () => {
  for (const patch of [{ fromDate: '2025-02-30', toDate: '2025-03-01' }, { fromDate: '2025-01-01', toDate: '2026-01-04' }, { fromDate: '2025-02-01', toDate: '2025-01-01' }, { groupBy: 'Year' }, { top: '1000' }, { sortBy: 'Name' }]) {
    const f = fixture(); Object.assign(f.c.form, patch); await f.c.load(); assert.equal(f.calls.length, 0); assert.ok(f.c.error.value);
  }
  for (const cashier of ['0', '-1', '1.5', '2147483648', 'NaN']) { const f = fixture(); f.c.cashier.value = cashier; await f.c.load(); assert.equal(f.calls.length, 0); }
});
test('approved366daydifference accepted and numeric cashier forwarded', async () => {
  const f = fixture(); Object.assign(f.c.form, { fromDate: '2024-01-01', toDate: '2025-01-01' }); f.c.cashier.value = '12'; await f.c.load(); assert.equal(f.calls[0].cashierId, 12); assert.ok(f.c.report.value);
});
test('editing filter disables export and print until explicit reload', async () => {
  const f = fixture(); await f.c.load(); f.c.form.top = '20'; await f.c.exportCsv(); f.c.printReport(); assert.equal(f.downloads.length, 0); assert.equal(f.printed(), 0); await f.c.load(); assert.equal(f.c.current.value, true);
});
test('duplicate load and export are blocked and blob URL is revoked', async () => {
  const f = fixture(), d = deferred(); f.result(d.promise); const load = f.c.load(); await f.c.load(); assert.equal(f.calls.length, 1); d.resolve(data(f.calls[0])); await load;
  const e = deferred(); f.exportResult(e.promise); const exportPending = f.c.exportCsv(); await f.c.exportCsv(); assert.equal(f.downloads.length, 1); e.resolve({ blob: new Blob(['test']), filename: 'report.csv' }); await exportPending;
  assert.equal(f.downloads.at(-1), 'clicked'); assert.deepEqual(f.revoked, ['blob:synthetic']);
});
test('late response after filter changes or unmount cannot restore report', async () => {
  const f = fixture(), d = deferred(); f.result(d.promise); const pending = f.c.load(); f.c.form.top = '20'; d.resolve(data(f.calls[0])); await pending; assert.equal(f.c.report.value, null);
  const g = fixture(), e = deferred(); g.result(e.promise); const other = g.c.load(); g.cleanup(); e.resolve(data(g.calls[0])); await other; assert.equal(g.c.report.value, null);
});
test('auth change clears financial data and ignores in-flight load/export', async () => {
  const f = fixture(), d = deferred(); f.result(d.promise); const pending = f.c.load(); f.auth.role = 'RECEPTIONIST'; f.authChanged(); d.resolve(data(f.calls[0])); await pending; assert.equal(f.c.report.value, null);
  const g = fixture(); await g.c.load(); const e = deferred(); g.exportResult(e.promise); const download = g.c.exportCsv(); g.auth.user.userId = 2; g.authChanged(); e.resolve({ blob: new Blob(['test']), filename: 'report.csv' }); await download; assert.equal(g.downloads.length, 1); assert.equal(g.c.report.value, null);
});
test('reload error and malformed response clear old totals instead of showing zeros', async () => {
  const f = fixture(); await f.c.load(); f.fail(); await f.c.load(); assert.equal(f.c.report.value, null); assert.ok(f.c.error.value);
  const g = fixture(); g.result({ summary: {} }); await g.c.load(); assert.equal(g.c.report.value, null); assert.ok(g.c.error.value);
});
test('quick filters are read-only until load and year range is bounded', () => {
  const f = fixture(); for (const value of ['today', 'week', 'month', 'quarter', 'year']) { f.c.quick(value); assert.equal(f.c.validate(), true); } assert.equal(f.calls.length, 0);
});
test('UI route/nav and debt/settlement explanations preserve decision scope', async () => {
  const routes = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8'); const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  assert.match(routes, /path: '\/admin\/reports\/revenue'.*roles: \['ADMIN'\]/); assert.match(app, /v-if="isAdmin" to="\/admin\/reports\/revenue"/);
  assert.match(source, /Tổng nợ hiện tại toàn phòng khám/); assert.match(source, /Không phải số dư nợ lịch sử cuối kỳ/); assert.match(source, /cơ cấu kỳ cũ được cập nhật/);
});
