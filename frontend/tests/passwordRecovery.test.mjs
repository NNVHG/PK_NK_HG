import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source = await readFile(new URL('../src/views/PasswordRecoveryView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'recovery-test' }).content,
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const challenge = { challengeId: '11111111-1111-4111-8111-111111111111', expiresInSeconds: 300, resendAfterSeconds: 60 };
function deferred() { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; }
function fixture() {
  const controls = { now: 100000 }, calls = [], hooks = {}; const store = { challenge: null, set(value) { this.challenge = value; }, clear() { this.challenge = null; } };
  const api = {
    request: async phone => { calls.push(['request', phone]); if (controls.requestError) throw controls.requestError; return controls.response ?? challenge; },
    reset: async (...args) => { calls.push(['reset', ...args]); if (controls.resetError) throw controls.resetError; return controls.reset; }
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', 'Date', compiled)(name => name === 'vue' ? {
    defineComponent: c => c, ref: value => ({ value }), nextTick: async cb => cb?.(), onMounted: cb => { hooks.mount = cb; }, onUnmounted: cb => { hooks.unmount = cb; }
  } : name === '@/stores/passwordRecovery' ? { usePasswordRecoveryStore: () => store } : name === '@/services/passwordRecovery' ? { passwordRecoveryService: api } : {}, module, module.exports, { now: () => controls.now });
  const c = module.exports.default.setup({}, { expose() {} }); hooks.mount();
  return { c, calls, controls, store, cleanup: hooks.unmount };
}
async function ready(f) { f.c.phone.value = '0999999998'; await f.c.submit(); f.c.code.value = '123456'; f.c.password.value = f.c.confirmPassword.value = 'Newpass1'; }

for (const password of ['Aa1234','Aa12345','Strong1\0x','Aa1'+'Đ'.repeat(35)]) test('recovery rejects policy-invalid password without sending OTP '+JSON.stringify(password),async()=>{
  const f=fixture();await ready(f);f.c.password.value=f.c.confirmPassword.value=password;await f.c.submit();assert.equal(f.calls.length,1);assert.equal(f.c.code.value,'123456');assert.match(f.c.error.value,/8 ký tự/);
});
test('request sends registered phone and only stores transient challenge metadata', async () => {
  const f = fixture(); f.c.phone.value = '0999999998'; await f.c.submit();
  assert.deepEqual(f.calls, [['request','0999999998']]); assert.deepEqual(f.store.challenge, challenge);
  assert.match(f.c.message.value, /Nếu số điện thoại/); assert.equal(f.c.code.value, '');
});
test('invalid phone, weak password and mismatched confirmation never write', async () => {
  const f = fixture(); f.c.phone.value = '1'; await f.c.submit(); assert.equal(f.calls.length, 0);
  await ready(f); f.c.password.value = 'weak'; await f.c.submit(); assert.equal(f.calls.length, 1);
  f.c.password.value = 'Newpass1'; f.c.confirmPassword.value = 'Different2'; await f.c.submit(); assert.equal(f.calls.length, 1);
});
test('local cooldown blocks resends until exactly sixty seconds', async () => {
  const f = fixture(); await ready(f); await f.c.requestCode(); assert.equal(f.calls.length, 1);
  f.controls.now += 60000; await f.c.requestCode(); assert.equal(f.calls.length, 2); assert.equal(f.c.password.value, '');
});
test('successful reset clears secrets and offers login without creating a session', async () => {
  const f = fixture(); await ready(f); await f.c.submit();
  assert.deepEqual(f.calls[1], ['reset',challenge.challengeId,'0999999998','123456','Newpass1']);
  assert.equal(f.c.done.value, true); assert.equal(f.store.challenge, null);
  for (const field of ['phone','code','password','confirmPassword']) assert.equal(f.c[field].value, '');
  await f.c.submit(); assert.equal(f.calls.length, 2);
});
test('double clicks never send duplicate request or reset', async () => {
  const f = fixture(), pending = deferred(); f.controls.response = pending.promise; f.c.phone.value = '0999999998';
  const first = f.c.submit(); await f.c.submit(); assert.equal(f.calls.length, 1); pending.resolve(challenge); await first;
  f.c.code.value = '123456'; f.c.password.value = f.c.confirmPassword.value = 'Newpass1';
  const next = deferred(); f.controls.reset = next.promise;
  const reset = f.c.submit(); await f.c.submit(); assert.equal(f.calls.length, 2); next.resolve(); await reset;
});
test('unavailable service is explicit and does not claim that SMS was sent', async () => {
  const f = fixture(); f.controls.requestError = { response: { data: { message: 'Khôi phục qua SMS chưa sẵn sàng.' } } };
  f.c.phone.value = '0999999998'; await f.c.submit(); assert.equal(f.store.challenge,null); assert.equal(f.c.message.value,''); assert.match(f.c.error.value,/chưa sẵn sàng/);
});
test('failed or unknown reset clears all authentication secrets and never retries automatically', async () => {
  const f = fixture(); await ready(f); f.controls.resetError = Error('Synthetic network fault'); await f.c.submit();
  assert.equal(f.c.done.value,false); assert.equal(f.calls.length,2);
  for (const field of ['code','password','confirmPassword']) assert.equal(f.c[field].value,'');
  assert.match(f.c.error.value,/Chưa xác định/);
});
test('unmount clears secrets and ignores late request/reset responses', async () => {
  const f = fixture(), pending = deferred(); f.controls.response = pending.promise; f.c.phone.value = '0999999998';
  const request = f.c.submit(); f.cleanup(); pending.resolve(challenge); await request;
  assert.equal(f.store.challenge,null); assert.equal(f.c.phone.value,''); assert.equal(f.c.message.value,'');
  const g = fixture(); await ready(g); const resetPending = deferred(); g.controls.reset = resetPending.promise;
  const reset = g.c.submit(); g.cleanup(); resetPending.resolve(); await reset; assert.equal(g.c.done.value,false); assert.equal(g.c.password.value,'');
});
test('changing phone clears previous challenge and inputs', async () => {
  const f = fixture(); await ready(f); f.c.changePhone(); assert.equal(f.store.challenge,null);
  for(const field of ['phone','code','password','confirmPassword']) assert.equal(f.c[field].value,'');
});
test('form labels, notices and public entry exist without persistent secret storage', async () => {
  const client = await readFile(new URL('../src/services/passwordRecovery.ts',import.meta.url),'utf8');
  const store = await readFile(new URL('../src/stores/passwordRecovery.ts',import.meta.url),'utf8');
  const router = await readFile(new URL('../src/router/index.ts',import.meta.url),'utf8');
  const login = await readFile(new URL('../src/views/LoginView.vue',import.meta.url),'utf8');
  assert.match(source,/autocomplete="one-time-code"/); assert.match(source,/role="alert"/); assert.match(source,/role="status"/);
  assert.doesNotMatch(source+client+store,/localStorage|sessionStorage|console\./);
  assert.match(router,/path: '\/forgot-password'.*guestOnly: true/); assert.match(login,/to="\/forgot-password"/);
});
