import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import ts from 'typescript';
import {parse,compileScript} from '@vue/compiler-sfc';
const source=await readFile(new URL('../src/views/PatientActivationView.vue',import.meta.url),'utf8');
const challenge={challengeId:'abcdef01-2345-6789-abcd-0123456789ab',expiresInSeconds:300,resendAfterSeconds:60};
const grant={completionToken:'A'.repeat(64),expiresInSeconds:300,mustChangePassword:true};
function fixture(routeQuery = {}){
 const hooks={watch:[]},auth={user:null,role:null},calls=[];
 let request=()=>challenge,verify=()=>grant,complete=()=>({completed:true});
 const imports={
  vue:{defineComponent:x=>x,ref:value=>({value}),watch:(_g,fn)=>hooks.watch.push(fn),onMounted:fn=>fn(),onUnmounted:fn=>hooks.unmount=fn},
  'vue-router':{useRoute:()=>({query:routeQuery}),useRouter:()=>({push:(target)=>calls.push(['route',target])}),RouterLink:{}},
  '@/stores/auth':{useAuthStore:()=>auth},
  '@/services/patientActivation':{patientActivationService:{
    request:async(...args)=>{calls.push(['request',...args]);return await request();},
    verify:async(...args)=>{calls.push(['verify',...args]);return await verify();},
    complete:async(...args)=>{calls.push(['complete',...args]);return await complete();}
  }}
 };
 const js=ts.transpileModule(compileScript(parse(source).descriptor,{id:'activation-test'}).content,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
 const module={exports:{}};new Function('require','module','exports',js)(n=>imports[n]??{},module,module.exports);
 const c=module.exports.default.setup({}, {expose(){}});
 return{c,hooks,auth,calls,setRequest:f=>request=f,setVerify:f=>verify=f,setComplete:f=>complete=f};
}
async function requested(){const f=fixture();f.c.phone.value='0967000000';f.c.password.value='0967000000';await f.c.requestCode();return f;}
async function verified(){const f=await requested();f.c.code.value='123456';await f.c.verifyCode();return f;}
test('request clears credentials and exposes only the generic next step',async()=>{const f=await requested();assert.deepEqual(f.calls,[['request','0967000000','0967000000',null,null]]);assert.equal(f.c.phone.value,'');assert.equal(f.c.password.value,'');assert.equal(f.c.challenge.value,challenge);assert.match(f.c.message.value,/Nếu thông tin đúng/);});
test('cannot skip OTP to complete activation',async()=>{const f=await requested();f.c.confirmed.value=true;await f.c.complete();assert.equal(f.calls.length,1);});
test('only six numeric OTP digits are submitted and cleared afterwards',async()=>{const f=await requested();f.c.code.value='12345x';await f.c.verifyCode();assert.equal(f.calls.length,1);f.c.code.value='123456';await f.c.verifyCode();assert.equal(f.c.code.value,'');assert.equal(f.c.challenge.value,null);assert.equal(f.c.grant.value,grant);});
test('keep default password requires explicit confirmation and sends no new password',async()=>{const f=await verified();f.c.choice.value='default';await f.c.complete();assert.equal(f.calls.length,2);f.c.confirmed.value=true;await f.c.complete();assert.deepEqual(f.calls[2],['complete',grant.completionToken,true,null]);assert.equal(f.c.done.value,true);assert.equal(f.c.grant.value,null);});
for(const value of ['Abc1234','abcdefgh','ABCDEFGH','Abcdefgh','abc12345','Aa1'+'Đ'.repeat(40),'Strong1\0x'])test('chosen password rejects '+JSON.stringify(value),async()=>{const f=await verified();f.c.newPassword.value=value;f.c.repeatPassword.value=value;f.c.confirmed.value=true;await f.c.complete();assert.equal(f.calls.length,2);});
test('chosen password must match and is cleared after completion',async()=>{const f=await verified();f.c.newPassword.value='Strong123';f.c.repeatPassword.value='Other123';f.c.confirmed.value=true;await f.c.complete();assert.equal(f.calls.length,2);f.c.repeatPassword.value='Strong123';await f.c.complete();assert.deepEqual(f.calls[2],['complete',grant.completionToken,false,'Strong123']);assert.equal(f.c.newPassword.value,'');assert.equal(f.c.repeatPassword.value,'');});
test('uncertain write discards proof and prevents repeated completion',async()=>{const f=await verified();f.setComplete(()=>{throw Error();});f.c.choice.value='default';f.c.confirmed.value=true;await f.c.complete();assert.equal(f.c.uncertain.value,true);assert.equal(f.c.grant.value,null);f.c.confirmed.value=true;await f.c.complete();assert.equal(f.calls.length,3);});
test('late request cannot survive identity change',async()=>{const f=fixture();let resolve;f.setRequest(()=>new Promise(r=>resolve=r));f.c.phone.value='0967000000';f.c.password.value='0967000000';const p=f.c.requestCode();f.auth.user={userId:7};f.hooks.watch[1]();resolve(challenge);await p;assert.equal(f.c.challenge.value,null);assert.equal(f.c.password.value,'');});
test('late verify never preserves a proof after unmount',async()=>{const f=await requested();let resolve;f.setVerify(()=>new Promise(r=>resolve=r));f.c.code.value='123456';const p=f.c.verifyCode();f.hooks.unmount();resolve(grant);await p;assert.equal(f.c.grant.value,null);assert.equal(f.c.code.value,'');});
test('changing password choice clears password drafts and confirmation',async()=>{const f=await verified();f.c.newPassword.value='Strong123';f.c.repeatPassword.value='Strong123';f.c.confirmed.value=true;f.hooks.watch[0]();assert.equal(f.c.newPassword.value,'');assert.equal(f.c.repeatPassword.value,'');assert.equal(f.c.confirmed.value,false);});
test('invalid challenge never advances and failed request clears password',async()=>{const f=fixture();f.setRequest(()=>({...challenge,challengeId:'-'.repeat(36)}));f.c.phone.value='0967000000';f.c.password.value='0967000000';await f.c.requestCode();assert.equal(f.c.challenge.value,null);assert.equal(f.c.password.value,'');assert.ok(f.c.error.value);});
test('accessible native forms and memory only secrets',()=>{assert.match(source,/<legend>/);assert.match(source,/autocomplete="one-time-code"/);assert.match(source,/role="status" aria-live="polite"/);assert.match(source,/role="alert"/);assert.ok(!/localStorage|sessionStorage|console\.log/.test(source));});
test('legacy registration route redirects to booking intake',async()=>{const router=await readFile(new URL('../src/router/index.ts',import.meta.url),'utf8');assert.match(router,/path: '\/register'[\s\S]{0,120}redirect: \{ name: 'guest-booking' \}/);const service=await readFile(new URL('../src/services/patientActivation.ts',import.meta.url),'utf8');assert.ok(!/localStorage|sessionStorage|console\./.test(service));assert.doesNotMatch(service, /\/auth\/activation\/(?:request|verify|complete)\?/);});
