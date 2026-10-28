import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript } from '@vue/compiler-sfc';

const source = await readFile(new URL('../src/views/LoginView.vue', import.meta.url), 'utf8');

function fixture(login = async () => {}) {
  const calls = [];
  const auth = { role: 'PATIENT', login: async (...args) => { calls.push(['login', ...args]); await login(); } };
  const imports = {
    vue: { defineComponent: value => value, ref: value => ({ value }), computed: fn => ({ get value() { return fn(); } }) },
    '@/stores/auth': { useAuthStore: () => auth },
    'vue-router': { RouterLink: {}, useRouter: () => ({ push: target => calls.push(['route', target]) }), useRoute: () => ({ query: {} }) },
    '@/services/patientActivation': { stageActivationPrefill: value => calls.push(['prefill', value]) }
  };
  const js = ts.transpileModule(compileScript(parse(source).descriptor, { id: 'login-country-test' }).content,
    { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const module = { exports: {} };
  new Function('require', 'module', 'exports', js)(name => imports[name] ?? {}, module, module.exports);
  return { view: module.exports.default.setup({}, { expose() {} }), calls };
}

test('passport login requires a country chosen by the user before calling the API', async () => {
  const { view, calls } = fixture();
  view.loginMode.value = 'passport';
  view.phone.value = 'B1234567';
  view.password.value = 'Password123';
  assert.equal(view.nationalityCode.value, '');
  await view.handleSubmit();
  assert.deepEqual(calls, []);
  assert.match(view.errorMessage.value, /quốc gia cấp/);

  view.nationalityCode.value = ' us ';
  await view.handleSubmit();
  assert.deepEqual(calls[0], ['login', 'B1234567', 'Password123', 2, 'US']);
});

test('activation prefill for CCCD never supplies a passport country', async () => {
  const error = { response: { status: 401, data: { code: 'AUTH_ACTIVATION_REQUIRED' } } };
  const { view, calls } = fixture(async () => { throw error; });
  view.phone.value = '001200000001';
  view.password.value = 'Password123';
  await view.handleSubmit();
  assert.deepEqual(calls[1], ['prefill', { identifier: '001200000001', documentType: 'cccd', nationalityCode: '' }]);
});
