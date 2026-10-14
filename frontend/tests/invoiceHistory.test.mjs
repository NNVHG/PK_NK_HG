import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/components/InvoiceHistoryPanel.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'invoice-history-test' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
const row = { invoiceId: 9, invoiceCode: 'INV-9', status: 2, patientCode: 'BN000123' };
function fixture(role = 'ADMIN') {
  let cleanup, pending, error; const reads = [], emits = []; const props = { disabled: false };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onUnmounted: cb => { cleanup = cb; } },
    '@/stores/auth': { useAuthStore: () => ({ role }) },
    '@/services/invoiceHistory': { invoiceHistoryApi: { get: async query => { reads.push(query); if (error) throw error; return pending ? await pending : { items: [row], page: query.page, pageSize: 20, totalPages: 3, totalCount: 41 }; } } }
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup(props, { expose() {}, emit: (...args) => emits.push(args) });
  return { c, reads, emits, props, cleanup: () => cleanup(), pending: p => { pending = p; }, error: e => { error = e; } };
}
test('list is on demand, default all query has bounded size', async () => {
  const f = fixture(); assert.equal(f.reads.length, 0); await f.c.search(); assert.deepEqual(f.reads[0], { page: 1, pageSize: 20 }); assert.equal(f.c.totalCount.value, 41);
});
test('canonical patient and status forwarded, edits do not change pagination filter', async () => {
  const f = fixture('RECEPTIONIST'); f.c.patientCode.value = ' bn000123 '; f.c.status.value = '2'; await f.c.search();
  f.c.patientCode.value = 'BN000999'; f.c.status.value = '3'; await f.c.loadPage(2);
  assert.deepEqual(f.reads.at(-1), { patientCode: 'BN000123', status: 2, page: 2, pageSize: 20 });
  await f.c.search(); assert.equal(f.reads.at(-1).page, 1); assert.equal(f.reads.at(-1).patientCode, 'BN000999');
});
test('invalid code clears old data and never broadens query', async () => {
  for (const code of ['BN1', 'BN000000', 'BN0000001', 'BN2147483648', 'BN00000A', 'OTHER', 'BN１２３４５６']) {
    const f = fixture(); await f.c.search(); f.c.patientCode.value = code; await f.c.search(); assert.equal(f.reads.length, 1); assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value);
  }
});
test('canonical large code accepted and invalid status rejected', async () => {
  const f = fixture(); f.c.patientCode.value = 'BN2147483647'; await f.c.search(); assert.equal(f.reads[0].patientCode, 'BN2147483647');
  f.c.status.value = '5'; await f.c.search(); assert.equal(f.reads.length, 1);
});
test('denied roles never read or emit even with forged rows', async () => {
  for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT']) { const f = fixture(role); await f.c.search(); f.c.loaded.value = true; f.c.rows.value = [row]; f.c.selectInvoice(row); assert.equal(f.reads.length, 0); assert.deepEqual(f.emits, []); }
});
test('disabled during payment cannot read or select another invoice', async () => {
  const f = fixture(); await f.c.search(); f.props.disabled = true; await f.c.search(); await f.c.loadPage(2); f.c.selectInvoice(row);
  assert.equal(f.reads.length, 1); assert.deepEqual(f.emits, []);
});
test('selection emits exact code only for a loaded row without writes', async () => {
  const f = fixture(); await f.c.search(); f.c.selectInvoice({ ...row, invoiceCode: 'OLD' }); f.c.selectInvoice({ ...row, invoiceId: 1 }); f.c.selectInvoice(row);
  assert.deepEqual(f.emits, [['selected', 'INV-9']]); assert.equal(f.reads.length, 1);
});
test('invalid pagination or beyond last page never queries', async () => {
  const f = fixture(); await f.c.loadPage(2); assert.equal(f.reads.length, 0); await f.c.search();
  for (const page of [0, -1, 1.5, 1000001, 4]) await f.c.loadPage(page); assert.equal(f.reads.length, 1);
});
test('empty result stays honest with count zero', async () => {
  const f = fixture(); f.pending(Promise.resolve({ items: [], page: 1, totalPages: 0, totalCount: 0 })); await f.c.search();
  assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.loaded.value, true); assert.equal(f.c.totalCount.value, 0);
});
test('failed read clears rows and stale selection, search can recover', async () => {
  const f = fixture(); await f.c.search(); f.error(new Error('network')); await f.c.loadPage(2); f.c.selectInvoice(row);
  assert.deepEqual(f.c.rows.value, []); assert.deepEqual(f.emits, []); assert.ok(f.c.error.value); f.error(null); await f.c.search(); assert.equal(f.c.error.value, '');
});
test('duplicate submit blocked and unmount discards late private list', async () => {
  const f = fixture(); let resolve; f.pending(new Promise(done => { resolve = done; }));
  const first = f.c.search(); await f.c.search(); f.c.selectInvoice(row); assert.equal(f.reads.length, 1);
  f.cleanup(); resolve({ items: [row], page: 1, totalPages: 1, totalCount: 1 }); await first;
  assert.deepEqual(f.c.rows.value, []); assert.equal(f.c.loaded.value, false); assert.equal(f.c.patientCode.value, ''); assert.deepEqual(f.emits, []);
});

test('date query uses inclusive calendar strings and pagination preserves applied dates', async () => {
  const f = fixture(); f.c.fromDate.value = '2026-10-14'; f.c.toDate.value = '2026-10-14'; await f.c.search();
  assert.deepEqual(f.reads[0], { page: 1, pageSize: 20, fromDate: '2026-10-14', toDate: '2026-10-14' });
  f.c.fromDate.value = '2026-10-15'; f.c.toDate.value = '2026-10-16'; await f.c.loadPage(2);
  assert.equal(f.reads[1].fromDate, '2026-10-14'); assert.equal(f.reads[1].toDate, '2026-10-14');
  await f.c.search(); assert.equal(f.reads.at(-1).page, 1); assert.equal(f.reads.at(-1).fromDate, '2026-10-15');
});
test('dates intersect patient and state filters', async () => {
  const f = fixture(); f.c.patientCode.value = 'BN000123'; f.c.status.value = '2';
  f.c.fromDate.value = '2026-10-14'; f.c.toDate.value = '2026-10-16'; await f.c.search();
  assert.deepEqual(f.reads[0], { page: 1, pageSize: 20, patientCode: 'BN000123', status: 2, fromDate: '2026-10-14', toDate: '2026-10-16' });
});
test('invalid calendar, reversed or overflow dates clear old rows before request', async () => {
  for (const [from, to] of [['2026-10-15', '2026-10-14'], ['2026-02-29', ''], ['', '2026-04-31'], ['0001-01-01', ''], ['', '9999-12-31'], ['0000-01-01', ''], ['2026-1-1', '']]) {
    const f = fixture(); await f.c.search(); f.c.fromDate.value = from; f.c.toDate.value = to; await f.c.search();
    assert.equal(f.reads.length, 1); assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value);
  }
});
test('either date can be omitted and clearing dates removes query parameters', async () => {
  const f = fixture(); f.c.fromDate.value = '2026-10-14'; await f.c.search(); assert.equal(f.reads[0].toDate, undefined);
  f.c.fromDate.value = ''; f.c.toDate.value = '2026-10-14'; await f.c.search(); assert.equal(f.reads[1].fromDate, undefined);
  f.c.toDate.value = ''; await f.c.search(); assert.deepEqual(f.reads[2], { page: 1, pageSize: 20 });
});
test('leap and long date ranges stay valid for paginated invoice history', async () => {
  const f = fixture(); f.c.fromDate.value = '2024-02-29'; f.c.toDate.value = '2024-02-29'; await f.c.search();
  f.c.fromDate.value = '1970-01-01'; f.c.toDate.value = '2099-12-31'; await f.c.search(); assert.equal(f.reads.length, 2);
});
test('failed date read retains filter to retry; unmount clears dates', async () => {
  const f = fixture(); f.c.fromDate.value = '2026-10-14'; f.c.toDate.value = '2026-10-14'; f.error(new Error('network')); await f.c.search();
  assert.equal(f.c.fromDate.value, '2026-10-14'); f.error(null); await f.c.search(); assert.equal(f.c.error.value, '');
  f.cleanup(); assert.equal(f.c.fromDate.value, ''); assert.equal(f.c.toDate.value, '');
});

test('displayed invoice date matches Vietnam filter even when UTC date is previous day', () => {
  const f = fixture(); const text = f.c.formatDate('2026-10-13T17:00:00Z');
  assert.ok(text.includes('14/10/2026')); assert.ok(text.includes('00:00:00'));
});

test('name or phone keyword trimmed and pagination keeps applied value', async () => {
  const f = fixture(); f.c.keyword.value = '  Trần  '; await f.c.search(); assert.equal(f.reads[0].keyword, 'Trần');
  f.c.keyword.value = '0900'; await f.c.loadPage(2); assert.equal(f.reads[1].keyword, 'Trần');
  await f.c.search(); assert.equal(f.reads.at(-1).page, 1); assert.equal(f.reads.at(-1).keyword, '0900');
});
test('keyword combines with patient, status and Vietnam date filters', async () => {
  const f = fixture(); f.c.keyword.value = '0900'; f.c.patientCode.value = 'BN000123'; f.c.status.value = '2';
  f.c.fromDate.value = '2026-10-14'; f.c.toDate.value = '2026-10-14'; await f.c.search();
  assert.deepEqual(f.reads[0], { keyword: '0900', patientCode: 'BN000123', status: 2, fromDate: '2026-10-14', toDate: '2026-10-14', page: 1, pageSize: 20 });
});
test('keyword100 accepted; keyword101 clears old rows and sends no query', async () => {
  const f = fixture(); f.c.keyword.value = 'A'.repeat(100); await f.c.search(); assert.equal(f.reads[0].keyword.length, 100);
  f.c.keyword.value = 'A'.repeat(101); await f.c.search(); assert.equal(f.reads.length, 1); assert.deepEqual(f.c.rows.value, []); assert.ok(f.c.error.value);
});
test('blank or cleared keyword omits parameter and resets page', async () => {
  const f = fixture(); f.c.keyword.value = '0900'; await f.c.search(); await f.c.loadPage(2);
  f.c.keyword.value = '   '; await f.c.search(); assert.deepEqual(f.reads.at(-1), { page: 1, pageSize: 20 });
});
test('literal percent underscore and apostrophe sent unchanged', async () => {
  for (const keyword of ['100%_', "O'Neil"]) { const f = fixture(); f.c.keyword.value = keyword; await f.c.search(); assert.equal(f.reads[0].keyword, keyword); }
});
test('failed keyword search retains draft for retry and clears patient results', async () => {
  const f = fixture(); f.c.keyword.value = '0900'; await f.c.search(); f.error(new Error('network')); await f.c.search();
  assert.equal(f.c.keyword.value, '0900'); assert.deepEqual(f.c.rows.value, []); f.error(null); await f.c.search(); assert.equal(f.c.error.value, '');
});
test('unmount clears keyword and ignores late results', async () => {
  const f = fixture(); let resolve; f.pending(new Promise(done => { resolve = done; })); f.c.keyword.value = '0900'; const first = f.c.search();
  f.cleanup(); resolve({ items: [row], page: 1, totalPages: 1, totalCount: 1 }); await first; assert.equal(f.c.keyword.value, ''); assert.deepEqual(f.c.rows.value, []);
});
