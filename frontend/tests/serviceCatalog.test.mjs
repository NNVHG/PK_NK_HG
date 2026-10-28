import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const options = { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } };
const apiModule = { exports: {} };
new Function('require', 'module', 'exports', ts.transpileModule(await readFile(new URL('../src/services/serviceCatalog.ts', import.meta.url), 'utf8'), options).outputText)(() => ({ apiClient: {} }), apiModule, apiModule.exports);
const helpers = apiModule.exports;
const { clinicDateTimeInput, clinicInputToIso, validCatalogPrice, validCatalogEffective } = helpers;
test('catalog uses Vietnam clock and converts local input to explicit UTC regardless of machine timezone', () => {
  assert.equal(clinicDateTimeInput(new Date('2026-10-15T17:01:00Z')), '2026-10-16T00:01');
  assert.equal(clinicInputToIso('2026-10-16T00:00'), '2026-10-15T17:00:00.000Z');
  assert.equal(clinicInputToIso('2026-02-30T12:00'), null);
  assert.equal(clinicInputToIso('bad'), null);
});
test('effective date permits today in Vietnam and blocks backdating or dates before the latest schedule', () => {
  const now = new Date('2026-10-15T17:01:00Z');
  assert.equal(validCatalogEffective('2026-10-16T00:00', null, now), true);
  assert.equal(validCatalogEffective('2026-10-15T23:59', null, now), false);
  assert.equal(validCatalogEffective('2026-10-16T00:00', '2026-10-16T10:00:00+07:00', now), false);
});
test('service price accepts zero and whole VND, rejects fractions, negatives and unsafe JS money', () => {
  assert.equal(validCatalogPrice('0'), true); assert.equal(validCatalogPrice('100001'), true);
  for (const value of ['1.5', '-1', '', '1e6', String(Number.MAX_SAFE_INTEGER + 1)]) assert.equal(validCatalogPrice(value), false);
});
const { descriptor } = parse(await readFile(new URL('../src/views/ServiceCatalogView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'catalog-test' }).content, options).outputText;
const future = () => clinicDateTimeInput(new Date(Date.now() + 2 * 86400000));
function fixture(role = 'ADMIN') {
  let cleanup, fail = false, pending;
  let record = { dentalServiceId: 12, code: 'SYN', name: 'Synthetic service', description: null, durationMinutes: 30, isActive: true,
    currentPrice: 100001, currentPriceEffectiveFrom: new Date().toISOString(), priceHistory: [{ servicePriceId: 1, amount: 100001, effectiveFrom: new Date().toISOString(), createdByUserId: 7 }] };
  const pageResult = { value: null }, detail = { value: null }, calls = { list: [], detail: [], create: [], update: [], prices: [], active: [] };
  const store = { replacePage: data => { pageResult.value = data; }, selectDetail: data => { detail.value = data; }, reset: () => { pageResult.value = null; detail.value = null; } };
  const api = {
    list: async (...args) => { calls.list.push(args); return { items: [record], totalCount: 1, totalPages: 1, page: args[0] }; },
    detail: async id => { calls.detail.push(id); return structuredClone(record); },
    create: async body => { calls.create.push(body); if (fail) throw { response: { data: { errors: [{ message: 'Mã đã tồn tại' }] } } }; if (pending) await pending; record = { ...record, ...body }; return structuredClone(record); },
    update: async (id, body) => { calls.update.push([id, body]); record = { ...record, ...body }; return structuredClone(record); },
    addPrice: async (...args) => { calls.prices.push(args); record.priceHistory.unshift({ servicePriceId: 2, amount: args[1], effectiveFrom: args[2], createdByUserId: 7 }); return record.priceHistory[0]; },
    setActive: async (id, active) => { calls.active.push([id, active]); record.isActive = active; return structuredClone(record); },
  };
  const module = { exports: {} };
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch() {}, onMounted() {}, onUnmounted: cb => { cleanup = cb; } },
    pinia: { storeToRefs: () => ({ pageResult, detail }) }, axios: { isAxiosError: e => !!e.response },
    '@/stores/auth': { useAuthStore: () => ({ role }) }, '@/stores/serviceCatalog': { useServiceCatalogStore: () => store },
    '@/services/serviceCatalog': { ...helpers, serviceCatalogApi: api },
  };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  const createReady = () => { c.startCreate(); c.form.value = { code: 'NEW', name: ' Synthetic new ', description: ' Description ', durationMinutes: '45', initialPrice: '250001', effectiveFrom: future() }; c.serviceConfirmed.value = true; };
  return { c, calls, pageResult, detail, createReady, fail: () => { fail = true; }, cleanup: () => cleanup(),
    defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; } };
}
test('catalog reads a bounded searchable page with explicit active filter', async () => {
  const f = fixture(); f.c.keyword.value = ' SYN '; f.c.activeFilter.value = 'false'; await f.c.load(1);
  assert.deepEqual(f.calls.list, [[1, 'SYN', false]]); assert.equal(f.pageResult.value.items.length, 1);
});
test('invalid keyword, filter or page does not read catalog', async () => {
  const f = fixture(); f.c.keyword.value = 'x'.repeat(101); await f.c.load();
  f.c.keyword.value = ''; f.c.activeFilter.value = 'bad'; await f.c.load(); f.c.activeFilter.value = 'all'; await f.c.load(0);
  assert.equal(f.calls.list.length, 0);
});
test('create sends confirmed whole VND and explicit Vietnam effective instant', async () => {
  const f = fixture(); f.createReady(); const instant = clinicInputToIso(f.c.form.value.effectiveFrom); await f.c.saveService();
  assert.deepEqual(f.calls.create, [{ code: 'NEW', name: 'Synthetic new', description: 'Description', durationMinutes: 45, initialPrice: 250001, effectiveFrom: instant }]);
  assert.equal(f.detail.value.code, 'NEW'); assert.equal(f.c.serviceConfirmed.value, false);
});
test('missing confirmation, blank name, overlong code and fractional price cannot mutate', async () => {
  const f = fixture(); f.createReady(); f.c.serviceConfirmed.value = false; await f.c.saveService();
  f.c.serviceConfirmed.value = true; f.c.form.value.initialPrice = '1.5'; await f.c.saveService();
  f.c.form.value.initialPrice = '10'; f.c.form.value.code = 'X'.repeat(51); await f.c.saveService();
  f.c.form.value.code = 'S'; f.c.form.value.name = ' '; await f.c.saveService(); assert.equal(f.calls.create.length, 0);
});
test('all non-admin roles cannot read or submit the management screen', async () => {
  for (const role of ['RECEPTIONIST', 'PATIENT', 'DENTIST', 'ASSISTANT']) {
    const f = fixture(role); await f.c.load(); f.createReady(); f.c.mode.value = 'create'; await f.c.saveService();
    assert.equal(f.calls.list.length, 0); assert.equal(f.calls.create.length, 0);
  }
});
test('metadata update never submits a replacement code or overwrites prices', async () => {
  const f = fixture(); await f.c.select(12); f.c.form.value.code = 'CHANGED'; f.c.form.value.name = ' Edited '; f.c.serviceConfirmed.value = true;
  await f.c.saveService(); assert.deepEqual(f.calls.update, [[12, { name: 'Edited', description: null, durationMinutes: 30 }]]);
  assert.equal(f.calls.prices.length, 0); assert.equal(f.detail.value.code, 'SYN');
});
test('new price is appended only after confirmation and reloads actual history', async () => {
  const f = fixture(); await f.c.select(12); f.c.priceForm.value = { amount: '200001', effectiveFrom: future() };
  await f.c.addPrice(); assert.equal(f.calls.prices.length, 0); f.c.priceConfirmed.value = true; await f.c.addPrice();
  assert.equal(f.calls.prices[0][1], 200001); assert.equal(f.detail.value.priceHistory.length, 2); assert.equal(f.detail.value.priceHistory[1].amount, 100001);
});
test('new price cannot precede a scheduled price or yesterday', async () => {
  const f = fixture(); await f.c.select(12); f.detail.value.priceHistory[0].effectiveFrom = new Date(Date.now() + 4 * 86400000).toISOString();
  f.c.priceForm.value = { amount: '200001', effectiveFrom: future() }; f.c.priceConfirmed.value = true; await f.c.addPrice();
  f.c.priceForm.value.effectiveFrom = clinicDateTimeInput(new Date(Date.now() - 86400000)); await f.c.addPrice(); assert.equal(f.calls.prices.length, 0);
});
test('deactivation requires separate confirmation and keeps record history', async () => {
  const f = fixture(); await f.c.select(12); await f.c.toggleActive(); assert.equal(f.calls.active.length, 0);
  f.c.statusConfirmed.value = true; await f.c.toggleActive(); assert.deepEqual(f.calls.active, [[12, false]]); assert.equal(f.detail.value.priceHistory.length, 1);
});
test('double submit issues one create request', async () => {
  const f = fixture(); f.createReady(); const done = f.defer(); const first = f.c.saveService(); await f.c.saveService(); assert.equal(f.calls.create.length, 1); done(); await first;
});
test('server error retains form and requires a read before another mutation', async () => {
  const f = fixture(); f.createReady(); f.fail(); await f.c.saveService();
  assert.equal(f.c.error.value, 'Mã đã tồn tại'); assert.equal(f.c.form.value.code, 'NEW'); assert.equal(f.c.needsReload.value, true);
  f.c.serviceConfirmed.value = true; await f.c.saveService(); assert.equal(f.calls.create.length, 1);
});
test('unmount ignores late create and clears local draft and store', async () => {
  const f = fixture(); f.createReady(); const done = f.defer(); const first = f.c.saveService(); f.cleanup(); done(); await first;
  assert.equal(f.detail.value, null); assert.equal(f.pageResult.value, null); assert.equal(f.c.form.value.code, ''); assert.equal(f.calls.detail.length, 0);
});
test('management route and navigation are limited to Admin', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
  assert.match(router, /path: '\/admin\/services'[\s\S]*?roles: \['ADMIN'\]/);
  const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8'); assert.match(app, /v-if="isAdmin" to="\/management\/admin\/services"/);
});
