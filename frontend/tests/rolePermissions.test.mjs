import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/RolePermissionsView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'permissions-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const data = () => ({ isReadOnly: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT'].map(code => ({ code, name: code })),
  permissions: [{ code: 'PatientManage', name: 'Create profile', module: 'MOD_PAT', allowedRoles: ['ADMIN', 'RECEPTIONIST'], conditions: 'Administrative data' },
    { code: 'ClinicalDiagnosisUpdate', name: 'Diagnosis', module: 'MOD_PAT', allowedRoles: ['ADMIN', 'DENTIST'], conditions: 'Responsible dentist and open visit' },
    { code: 'InvoiceDiscountWrite', name: 'Discount', module: 'MOD_BIL', allowedRoles: ['ADMIN', 'RECEPTIONIST'], conditions: 'Unpaid invoice only' }] });
function fixture(role = 'ADMIN') {
  const matrix = { value: null }, auth = { role }, calls = [];
  let cleanup, pending, fail = false, response = data();
  const imports = {
    vue: { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), onMounted() {}, onUnmounted: cb => { cleanup = cb; } },
    pinia: { storeToRefs: () => ({ matrix }) }, axios: { isAxiosError: e => !!e.response },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/stores/rolePermissions': { useRolePermissionsStore: () => ({ replace: value => { matrix.value = value; } }) },
    '@/services/rolePermissions': { rolePermissionsApi: { get: async () => { calls.push('GET'); if (fail) throw { response: { data: { message: 'Không đủ quyền' } } }; return pending ?? response; } } },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, matrix, auth, calls, cleanup: () => cleanup(), fail: () => { fail = true; }, recover: () => { fail = false; },
    respond: value => { response = value; }, defer: () => { let resolve; pending = new Promise(done => { resolve = done; }); return resolve; } };
}
test('Admin reads live roles and permissions, preserving ownership/state conditions', async () => {
  const f = fixture(); await f.c.load(); assert.equal(f.calls.length, 1); assert.equal(f.matrix.value.roles.length, 5);
  assert.equal(f.c.visiblePermissions.value[1].conditions, 'Responsible dentist and open visit'); assert.equal(f.matrix.value.isReadOnly, true);
});
test('module filter changes only displayed rows without another API request', async () => {
  const f = fixture(); await f.c.load(); assert.deepEqual(f.c.modules.value, ['MOD_BIL', 'MOD_PAT']);
  f.c.moduleFilter.value = 'MOD_BIL'; assert.equal(f.c.visiblePermissions.value.length, 1); assert.equal(f.calls.length, 1);
});
test('non-Admin roles cannot request or retain the matrix', async () => {
  for (const role of ['RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT', null]) {
    const f = fixture(role); f.matrix.value = data(); await f.c.load(); assert.equal(f.calls.length, 0); assert.equal(f.matrix.value, null);
  }
});
test('reload clears stale permissions while reading and double clicks cause one request', async () => {
  const f = fixture(); f.matrix.value = data(); const done = f.defer(); const first = f.c.load(); await f.c.load();
  assert.equal(f.matrix.value, null); assert.equal(f.calls.length, 1); done(data()); await first;
});
test('read failure clears old data, displays server error and supports a manual retry', async () => {
  const f = fixture(); f.matrix.value = data(); f.fail(); await f.c.load(); assert.equal(f.matrix.value, null); assert.equal(f.c.error.value, 'Không đủ quyền');
  f.recover(); await f.c.load(); assert.equal(f.matrix.value.roles.length, 5); assert.equal(f.c.error.value, '');
});
test('invalid or empty configuration is explicit, without fabricated permission rows', async () => {
  for (const response of [{ ...data(), isReadOnly: false }, { ...data(), roles: [] }, { ...data(), permissions: [] }]) {
    const f = fixture(); f.respond(response); await f.c.load(); assert.equal(f.matrix.value, null); assert.ok(f.c.error.value);
  }
});
test('late response after unmount cannot restore the matrix', async () => {
  const f = fixture(); const done = f.defer(); const first = f.c.load(); f.cleanup(); done(data()); await first; assert.equal(f.matrix.value, null);
});
test('Admin role loss before a response cannot expose permissions', async () => {
  const f = fixture(); const done = f.defer(); const first = f.c.load(); f.auth.role = 'PATIENT'; done(data()); await first; assert.equal(f.matrix.value, null);
});
test('page is read only, links to staff assignment and explains conditional permissions', async () => {
  assert.match(source, /to="\/admin\/staff"/); assert.match(source, /nha sĩ phụ trách/); assert.match(source, /scope="row"/);
  assert.doesNotMatch(source, /type="checkbox"|@submit|\.post\(|\.put\(|\.delete\(/);
  const api = await readFile(new URL('../src/services/rolePermissions.ts', import.meta.url), 'utf8');
  assert.match(api, /apiClient\.get/); assert.doesNotMatch(api, /apiClient\.(post|put|delete|patch)/);
});
test('route and navigation restrict management matrix to Admin', async () => {
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
  assert.match(router, /path: '\/admin\/role-permissions'[\s\S]*?roles: \['ADMIN'\]/);
  const app = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8'); assert.match(app, /v-if="isAdmin" to="\/admin\/role-permissions"/);
  const staff = await readFile(new URL('../src/views/StaffView.vue', import.meta.url), 'utf8');
  assert.match(staff, /aria-label="Quản lý tài khoản và quyền"/); assert.match(staff, /to="\/admin\/role-permissions"/);
});
