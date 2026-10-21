import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript } from '@vue/compiler-sfc';
const sources = await Promise.all(['StaffPhoneChangeRequest', 'StaffPhoneChangeConfirmation'].map(n => readFile(new URL(`../src/components/${n}.vue`, import.meta.url), 'utf8')));
function fixture(mode = 0) {
  const calls = [], watchers = [], unmount = [], control = {}, auth = { user: { userId: 7 }, role: 'ADMIN', clearSession() { calls.push(['clearSession']); } };
  const pending = { challengeId: 'challenge', newPhone: '0967000001', expiresAtUtc: new Date(Date.now() + 300000).toISOString() };
  const imports = { vue: { defineComponent: x => x, ref: value => ({ value }), onMounted() {}, onUnmounted: f => unmount.push(f), watch: (...a) => watchers.push(a) },
    'vue-router': { useRouter: () => ({ replace: async p => calls.push(['route', p]) }) }, '@/stores/auth': { useAuthStore: () => auth },
    '@/services/api': { apiClient: { get: async (...a) => { calls.push(['get', ...a]); if(control.getError)throw control.getError;return control.read ?? { data: { pending } }; }, post: async (...a) => { calls.push(['post', ...structuredClone(a)]); if(control.error)throw control.error;return control.write ?? { data: mode ? { completed: true } : pending }; } } } };
  const js = ts.transpileModule(compileScript(parse(sources[mode]).descriptor, { id: 'staff-phone' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const m = { exports: {} }; new Function('require','module','exports', js)(n => imports[n] ?? {}, m, m.exports);
  const c = m.exports.default.setup({ userId: 4, staffName: 'Synthetic staff' }, { expose() {}, emit() {} });
  return { c, calls, control, auth, pending, watchers, unmount: () => unmount.forEach(f=>f()) };
}
test('Admin requests purpose-bound OTP without writing profile or password', async () => {
  const f=fixture(); f.c.newPhone.value='0967000001';f.c.confirmed.value=true;await f.c.submit();
  assert.deepEqual(f.calls,[['post','/staff/4/phone-change',{newPhone:'0967000001',confirmed:true}]]);
  assert.match(f.c.message.value,/chưa đổi/);assert.equal(f.c.confirmed.value,false);
});
test('request requires consent and active Admin role', async()=>{const f=fixture();f.c.newPhone.value='0967000001';await f.c.submit();f.c.confirmed.value=true;f.auth.role='DENTIST';await f.c.submit();assert.equal(f.calls.length,0);});
for(const value of ['bad','0967000001\n','0１２３４５６７８９','09670000012'])test('malformed request phone rejected '+JSON.stringify(value),async()=>{const f=fixture();f.c.newPhone.value=value;f.c.confirmed.value=true;await f.c.submit();assert.equal(f.calls.length,0);assert.ok(f.c.error.value);});
test('request prevents double submit and ignores late result on unmount',async()=>{const f=fixture();let resolve;f.control.write=new Promise(r=>resolve=r);f.c.newPhone.value='0967000001';f.c.confirmed.value=true;const first=f.c.submit();await f.c.submit();assert.equal(f.calls.length,1);f.unmount();resolve({data:f.pending});await first;assert.equal(f.c.message.value,'');assert.equal(f.c.newPhone.value,'');});
test('request surfaces unavailable provider without claiming phone was changed',async()=>{const f=fixture();f.control.error={response:{data:{message:'SMS chưa cấu hình'}}};f.c.newPhone.value='0967000001';f.c.confirmed.value=true;await f.c.submit();assert.equal(f.c.message.value,'');assert.equal(f.c.error.value,'SMS chưa cấu hình');});
test('request cannot claim success from malformed reply',async()=>{const f=fixture();f.control.write={data:{}};f.c.newPhone.value='0967000001';f.c.confirmed.value=true;await f.c.submit();assert.ok(f.c.error.value);assert.equal(f.c.message.value,'');});
test('self loads only own pending request with no user identifier in URL',async()=>{const f=fixture(1);await f.c.load();assert.deepEqual(f.calls,[['get','/auth/staff-phone-change']]);assert.deepEqual(f.c.pending.value,f.pending);});
test('nonstaff cannot load self confirmation',async()=>{const f=fixture(1);f.auth.role='PATIENT';await f.c.load();assert.equal(f.calls.length,0);});
test('self confirms once, clears OTP and logs out to new-number instruction',async()=>{const f=fixture(1);await f.c.load();f.c.code.value='123456';f.c.confirmed.value=true;await f.c.confirm();assert.deepEqual(f.calls.slice(1),[['post','/auth/staff-phone-change/confirm',{challengeId:'challenge',code:'123456',confirmed:true}],['clearSession'],['route','/login?phoneChanged=1']]);assert.equal(f.c.code.value,'');assert.equal(f.c.pending.value,null);});
test('confirmation requires explicit consent',async()=>{const f=fixture(1);await f.c.load();f.c.code.value='123456';await f.c.confirm();assert.equal(f.calls.length,1);});
for(const value of ['bad','123456\n','１２３４５６'])test('malformed OTP never sent '+JSON.stringify(value),async()=>{const f=fixture(1);await f.c.load();f.c.code.value=value;f.c.confirmed.value=true;await f.c.confirm();assert.equal(f.calls.length,1);assert.ok(f.c.error.value);});
test('expired request cannot be confirmed',async()=>{const f=fixture(1);f.pending.expiresAtUtc=new Date(Date.now()-1).toISOString();await f.c.load();f.c.code.value='123456';f.c.confirmed.value=true;await f.c.confirm();assert.equal(f.calls.length,1);assert.equal(f.c.pending.value,null);assert.match(f.c.error.value,/hết hạn/);});
test('invalid reply retains session and clears entered OTP',async()=>{const f=fixture(1);await f.c.load();f.control.write={data:{completed:false}};f.c.code.value='123456';f.c.confirmed.value=true;await f.c.confirm();assert.equal(f.calls.length,2);assert.equal(f.c.code.value,'');assert.equal(f.c.confirmed.value,false);assert.ok(f.c.error.value);});
test('confirmation prevents double submit and ignores completion after unmount',async()=>{const f=fixture(1);await f.c.load();let resolve;f.control.write=new Promise(r=>resolve=r);f.c.code.value='123456';f.c.confirmed.value=true;const first=f.c.confirm();await f.c.confirm();assert.equal(f.calls.length,2);f.unmount();resolve({data:{completed:true}});await first;assert.equal(f.calls.length,2);assert.equal(f.c.code.value,'');});
test('late load cannot restore private pending metadata after unmount',async()=>{const f=fixture(1);let resolve;f.control.read=new Promise(r=>resolve=r);const first=f.c.load();f.unmount();resolve({data:{pending:f.pending}});await first;assert.equal(f.c.pending.value,null);});
test('no pending request is reported without exposing a different account',async()=>{const f=fixture(1);f.control.read={data:{pending:null}};await f.c.load();assert.match(f.c.message.value,/Chưa có/);});
test('malformed pending metadata is rejected',async()=>{const f=fixture(1);f.control.read={data:{pending:{challengeId:'x',newPhone:'bad',expiresAtUtc:'bad'}}};await f.c.load();assert.equal(f.c.pending.value,null);assert.ok(f.c.error.value);});
test('identity change clears personal metadata and disables stale component',async()=>{const f=fixture(1);await f.c.load();f.watchers[0][1]();assert.equal(f.c.pending.value,null);await f.c.load();assert.equal(f.calls.length,1);});
test('UI integrates verified staff action and stores no OTP or phone in browser persistence',async()=>{const staff=await readFile(new URL('../src/views/StaffView.vue',import.meta.url),'utf8'),profile=await readFile(new URL('../src/views/ProfileView.vue',import.meta.url),'utf8');assert.match(staff,/staff.hasVerifiedPhone && staff.isActive/);assert.match(staff,/:readonly="editingVerifiedPhone"/);assert.match(profile,/StaffPhoneChangeConfirmation/);for(const s of sources)assert.doesNotMatch(s,/localStorage|sessionStorage|console\.log/);});

const authSource=await readFile(new URL('../src/stores/auth.ts',import.meta.url),'utf8');
function sessionFixture(){const calls=[];const imports={pinia:{defineStore:(_n,setup)=>setup},vue:{ref:value=>({value}),computed:f=>({get value(){return f()}})},'@/services/api':{apiClient:{post:async()=>calls.push('post')},setAccessToken:v=>calls.push(['token',v])}};const js=ts.transpileModule(authSource,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;const m={exports:{}};new Function('require','module','exports','window',js)(n=>imports[n],m,m.exports,{addEventListener(){}});const auth=m.exports.useAuthStore();auth.token.value='synthetic';auth.user.value={userId:4};return{auth,calls};}
test('after server revocation local session clears without sending expired JWT to logout',()=>{const f=sessionFixture();f.auth.clearSession();assert.equal(f.auth.token.value,null);assert.equal(f.auth.user.value,null);assert.deepEqual(f.calls,[['token',null]]);});
test('ordinary logout still calls endpoint then clears local session',async()=>{const f=sessionFixture();await f.auth.logout();assert.deepEqual(f.calls,['post',['token',null]]);assert.equal(f.auth.token.value,null);});
