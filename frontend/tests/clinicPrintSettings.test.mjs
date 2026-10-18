import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse,compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source=await readFile(new URL('../src/views/ClinicPrintSettingsView.vue',import.meta.url),'utf8');
const compiled=ts.transpileModule(compileScript(parse(source).descriptor,{id:'clinic-print-settings'}).content,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
const initial=()=>({address:null,phone:null,revision:'A'.repeat(64)});
function fixture(role='ADMIN'){
 const auth={role,user:{userId:3}},calls=[],controls={},watchers=[];let cleanup;const module={exports:{}};
 const api={read:async()=>{calls.push(['read']);if(controls.failRead)throw Error('Synthetic');return controls.read??initial();},save:async(...a)=>{calls.push(['save',...a]);if(controls.failSave)throw Error('Synthetic');return controls.save??{address:a[0],phone:a[1],revision:'B'.repeat(64)};}};
 new Function('require','module','exports',compiled)(name=>name==='vue'?{defineComponent:c=>c,ref:value=>({value}),computed:get=>({get value(){return get();}}),watch:(get,cb)=>watchers.push(cb),onMounted(){},onUnmounted:cb=>cleanup=cb}:name==='@/stores/auth'?{useAuthStore:()=>auth}:{clinicPrintSettingsService:api},module,module.exports);
 return{c:module.exports.default.setup({}, {expose(){}}),auth,calls,controls,watchers,cleanup:()=>cleanup()};
}
test('missing header starts blank and needs explicit valid confirmation',async()=>{const f=fixture();await f.c.load();assert.equal(f.c.address.value,'');f.c.address.value='Synthetic address';f.c.phone.value='0990000773';await f.c.save();assert.equal(f.calls.length,1);f.c.confirmed.value=true;await f.c.save();assert.deepEqual(f.calls[1],['save','Synthetic address','0990000773','A'.repeat(64)]);assert.equal(f.c.revision.value,'B'.repeat(64));});
for(const role of ['DENTIST','ASSISTANT','RECEPTIONIST','PATIENT',''])test(role+' cannot manage print header',async()=>{const f=fixture(role);await f.c.load();f.c.confirmed.value=true;await f.c.save();assert.equal(f.calls.length,0);});
test('invalid address phone and oversized text never sent',async()=>{for(const pair of [['','0990000773'],['bad\naddress','0990000773'],['x'.repeat(501),'0990000773'],['Synthetic','short']]){const f=fixture();await f.c.load();f.c.address.value=pair[0];f.c.phone.value=pair[1];f.c.confirmed.value=true;await f.c.save();assert.equal(f.calls.filter(a=>a[0]==='save').length,0);}});
test('unknown save outcome blocks resend until explicit read',async()=>{const f=fixture();await f.c.load();f.c.address.value='Synthetic';f.c.phone.value='0990000773';f.c.confirmed.value=true;f.controls.failSave=true;await f.c.save();assert.equal(f.c.needsReview.value,true);f.c.confirmed.value=true;await f.c.save();assert.equal(f.calls.filter(a=>a[0]==='save').length,1);await f.c.load();assert.equal(f.c.needsReview.value,false);});
test('malformed server revision and saved values cannot claim success',async()=>{const f=fixture();await f.c.load();f.c.address.value='Synthetic';f.c.phone.value='0990000773';f.c.confirmed.value=true;f.controls.save={address:'Wrong',phone:'0990000773',revision:'bad'};await f.c.save();assert.equal(f.c.message.value,'');assert.equal(f.c.needsReview.value,true);});
test('role change and unmount erase header editor state',async()=>{const f=fixture();await f.c.load();f.auth.role='';f.watchers[0]();assert.equal(f.c.loaded.value,'');assert.equal(f.c.address.value,'');const g=fixture();await g.c.load();g.cleanup();assert.equal(g.c.revision.value,'');});
test('Staff title field wired to optional metadata, Admin route only; no invented default',async()=>{const staff=await readFile(new URL('../src/views/StaffView.vue',import.meta.url),'utf8'),router=await readFile(new URL('../src/router/index.ts',import.meta.url),'utf8');assert.match(staff,/v-model="form.academicTitle"/);assert.match(staff,/academicTitle: form.academicTitle.trim\(\) \|\| null/);assert.match(router,/clinic-print-settings.*roles: \['ADMIN'\]/);assert.doesNotMatch(source,/localStorage|sessionStorage|console\./);});
