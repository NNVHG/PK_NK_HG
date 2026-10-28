import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/MedicalSupplyCatalogView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'medical-supply-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText;

const item = {
  id: 7,
  code: 'VT-GANGTAY',
  name: 'Găng tay y tế',
  unit: 'Hộp',
  minimumStockThreshold: 10,
  description: 'Hộp 100 chiếc',
  isActive: true,
  version: 1,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: null,
  createdByUserId: 1,
  updatedByUserId: null
};

function deferred() {
  let resolve;
  const promise = new Promise(done => { resolve = done; });
  return { promise, resolve };
}

function fixture(role = 'ADMIN') {
  const auth = { role, user: { userId: 1 } }, calls = [], watchers = [];
  let cleanup, pending, pendingList, fail = false;
  const module = { exports: {} };
  const api = {
    list: async (...args) => {
      calls.push(['list', ...args]);
      return pendingList ? await pendingList : { items: [item], page: args[1], totalCount: 1, hasNextPage: false };
    },
    save: async (id, payload) => {
      calls.push(['save', id, payload]);
      if (fail) throw Error('Synthetic');
      return pending ? await pending : { ...item, ...payload, id: id ?? 8, version: id === null ? 1 : payload.expectedVersion + 1 };
    },
    deactivate: async (id, version) => {
      calls.push(['deactivate', id, version]);
      if (fail) throw Error('Synthetic');
      return pending ? await pending : { ...item, id, version: version + 1, isActive: false };
    }
  };
  new Function('require', 'module', 'exports', compiled)(
    name => name === 'vue'
      ? { defineComponent: c => c, ref: value => ({ value }), reactive: x => x, watch: (getter, cb) => { watchers.push(cb); }, onMounted() {}, onUnmounted: cb => { cleanup = cb; } }
      : name === '@/stores/auth'
        ? { useAuthStore: () => auth }
        : { medicalSuppliesService: api },
    module,
    module.exports
  );
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, auth, calls, watchers, fail: () => { fail = true; }, pending: p => { pending = p; }, list: p => { pendingList = p; }, cleanup: () => cleanup() };
}

test('Admin loads active supply catalog and can create and update items', async () => {
  const f = fixture();
  await f.c.load();
  assert.equal(f.calls[0][3], false);
  f.c.open();
  Object.assign(f.c.form, { code: 'VT-BONGGON', name: 'Bông gòn', unit: 'Gói', minimumStockThreshold: 5, description: 'Gói 500g', isActive: true });
  await f.c.save();
  const saveCall = f.calls.find(c => c[0] === 'save');
  assert.equal(saveCall[1], null);
  assert.equal(saveCall[2].code, 'VT-BONGGON');
  assert.equal(saveCall[2].minimumStockThreshold, 5);
  assert.equal(f.c.editor.value, false);
});

for (const role of ['RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT', '']) {
  test(role + ' cannot manage medical supply master view', async () => {
    const f = fixture(role);
    await f.c.load();
    f.c.open(item);
    await f.c.save();
    await f.c.deactivate();
    assert.equal(f.calls.length, 0);
  });
}

test('invalid code, unit, threshold or length blocks request', async () => {
  for (const patch of [
    { code: 'bad code' },
    { name: ' ' },
    { name: 'x'.repeat(201) },
    { unit: ' ' },
    { unit: 'u'.repeat(51) },
    { minimumStockThreshold: -1 },
    { minimumStockThreshold: 1.5 },
    { minimumStockThreshold: 1000001 },
    { description: 'd'.repeat(501) }
  ]) {
    const f = fixture();
    await f.c.load();
    f.c.open(item);
    Object.assign(f.c.form, patch);
    await f.c.save();
    assert.equal(f.calls.filter(c => c[0] === 'save').length, 0);
  }
});

test('deactivation preserves identity and uses expected version', async () => {
  const f = fixture();
  await f.c.load();
  f.c.open(item);
  await f.c.deactivate();
  assert.deepEqual(f.calls.find(c => c[0] === 'deactivate'), ['deactivate', 7, 1]);
});

test('uncertain write blocks another save until successful reload', async () => {
  const f = fixture();
  await f.c.load();
  f.c.open(item);
  f.fail();
  await f.c.save();
  await f.c.save();
  assert.equal(f.calls.filter(c => c[0] === 'save').length, 1);
  assert.equal(f.c.needsReview.value, true);
  await f.c.load();
  assert.equal(f.c.needsReview.value, false);
  assert.equal(f.c.editor.value, false);
});

test('double submit, load and close blocked during write', async () => {
  const f = fixture(), d = deferred();
  await f.c.load();
  f.c.open(item);
  f.pending(d.promise);
  const pending = f.c.save();
  await f.c.save();
  await f.c.load();
  f.c.close();
  assert.equal(f.c.editor.value, true);
  assert.equal(f.calls.length, 2);
  d.resolve({ ...item, version: 2 });
  await pending;
});

test('unknown ID and stale response cannot claim success', async () => {
  const f = fixture();
  await f.c.load();
  f.c.open({ ...item, id: 9 });
  assert.equal(f.c.editor.value, false);
  f.c.open(item);
  f.pending({ ...item, id: 9, version: 2 });
  await f.c.save();
  assert.equal(f.c.needsReview.value, true);
  assert.equal(f.c.message.value, '');
});

test('out of order reads and changed filters cannot restore old rows', async () => {
  const f = fixture(), a = deferred(), b = deferred();
  f.list(a.promise);
  const old = f.c.load();
  f.c.keyword.value = 'new';
  f.list(b.promise);
  const current = f.c.load();
  b.resolve({ items: [], page: 1, totalCount: 0, hasNextPage: false });
  await current;
  a.resolve({ items: [item], page: 1, totalCount: 1, hasNextPage: false });
  await old;
  assert.equal(f.c.items.value.length, 0);
});

test('auth change and unmount ignore late writes and clear supply form', async () => {
  const f = fixture(), d = deferred();
  await f.c.load();
  f.c.open(item);
  f.pending(d.promise);
  const pending = f.c.save();
  f.auth.role = 'DENTIST';
  f.watchers[0]();
  d.resolve({ ...item, version: 2 });
  await pending;
  assert.equal(f.c.items.value.length, 0);
  assert.equal(f.c.message.value, '');
  assert.equal(f.c.form.name, '');

  const g = fixture(), e = deferred();
  g.list(e.promise);
  const read = g.c.load();
  g.cleanup();
  e.resolve({ items: [item], page: 1, totalCount: 1, hasNextPage: false });
  await read;
  assert.equal(g.c.items.value.length, 0);
});

test('route and navigation only Admin and no UnitPrice or StockIssue controls', async () => {
  const route = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
  const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  assert.match(route, /path: '\/admin\/medical-supplies'.*roles: \['ADMIN'\]/);
  assert.match(app, /v-if="isAdmin" to="\/management\/admin\/medical-supplies"/);
  assert.doesNotMatch(source, /unitPrice|StockQuantity|StockIssue/);
});
