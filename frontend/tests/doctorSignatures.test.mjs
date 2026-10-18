import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const source=await readFile(new URL('../src/views/DoctorSignatureView.vue',import.meta.url),'utf8');
const serviceSource=await readFile(new URL('../src/services/doctorSignatures.ts',import.meta.url),'utf8');
const compile=text=>ts.transpileModule(text,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
const compiled=compile(compileScript(parse(source).descriptor,{id:'signature-test'}).content);
const serviceModule={exports:{}};const httpCalls=[];
new Function('require','module','exports',compile(serviceSource))(()=>({apiClient:{get:async(...args)=>{httpCalls.push(['get',...args]);return{data:'read'};},post:async(...args)=>{httpCalls.push(['post',...args]);return{data:'saved'};}}}),serviceModule,serviceModule.exports);
const {MOD_AUTH_signatureFile,doctorSignaturesService}=serviceModule.exports;
const bytes=Uint8Array.from([137,80,78,71,13,10,26,10,0]);
const file=(type='image/png',size=bytes.length,content=bytes)=>({type,size,arrayBuffer:async()=>content.buffer.slice(content.byteOffset,content.byteOffset+content.byteLength)});
const image=(version=0,id=3)=>({doctorId:id,doctorName:'Synthetic doctor',version,signatureImageUrl:version?'/api/doctors/'+id+'/signature/file?version='+version:null,updatedAt:null});
const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return{promise,resolve};};
function fixture(role='DENTIST',id='3'){
 const auth={role,user:{userId:3}},route={params:{doctorId:id}},calls=[],controls={},watchers=[],revoked=[];let cleanup;
 const api={read:async n=>{calls.push(['read',n]);if(controls.failRead)throw Error('Synthetic');return controls.read??image();},file:async(...args)=>{calls.push(['file',...args]);return controls.file??new Blob([bytes],{type:'image/png'});},save:async(...args)=>{calls.push(['save',...args]);if(controls.failSave)throw Error('Synthetic');if(!controls.save)controls.read=image(1);return controls.save??image(1);}};
 const module={exports:{}};new Function('require','module','exports','URL',compiled)(name=>name==='vue'?{defineComponent:c=>c,ref:value=>({value}),computed:get=>({get value(){return get();}}),watch:(get,cb)=>watchers.push(cb),onMounted(){},onUnmounted:cb=>cleanup=cb}:name==='vue-router'?{useRoute:()=>route}:name==='@/stores/auth'?{useAuthStore:()=>auth}:{doctorSignaturesService:api,MOD_AUTH_signatureFile},module,module.exports,{createObjectURL:()=> 'blob:synthetic',revokeObjectURL:url=>revoked.push(url)});
 return{c:module.exports.default.setup({}, {expose(){}}),auth,route,calls,controls,watchers,revoked,cleanup:()=>cleanup()};
}
const select=async f=>{const input={files:[file()],value:'selected'};await f.c.selectFile({target:input});assert.equal(input.value,'');};
const writes=f=>f.calls.filter(x=>x[0]==='save');
test('file helper accepts PNG magic and raw base64 without data URL',async()=>{assert.equal(await MOD_AUTH_signatureFile(file()),Buffer.from(bytes).toString('base64'));});
for(const bad of [file('image/jpeg'),file('image/svg+xml'),file('image/png',0),file('image/png',262145),file('image/png',8),file('image/png',9,new Uint8Array(9))])test('invalid upload rejected locally '+bad.type+'/'+bad.size,async()=>{await assert.rejects(MOD_AUTH_signatureFile(bad));});
test('service uses authenticated client and binary explicit version; no raw URL navigation',async()=>{
 await doctorSignaturesService.read(3);await doctorSignaturesService.file(3,2);await doctorSignaturesService.save(3,2,'encoded');assert.deepEqual(httpCalls,[['get','/doctors/3/signature'],['get','/doctors/3/signature/file',{params:{version:2},responseType:'blob'}],['post','/doctors/3/signature',{expectedVersion:2,imageBase64:'encoded'}]]);
});
for(const role of ['RECEPTIONIST','ASSISTANT','PATIENT',''])test(role+' has no management requests',async()=>{const f=fixture(role);await f.c.load();await select(f);f.c.newImage.value='forged';f.c.confirmed.value=true;await f.c.save();assert.equal(f.calls.length,0);});
test('foreign dentist denied; Admin can manage foreign doctor',async()=>{const f=fixture('DENTIST','4');await f.c.load();assert.equal(f.calls.length,0);const g=fixture('ADMIN','4');g.controls.read=image(0,4);await g.c.load();assert.equal(g.c.status.value.doctorId,4);});
for(const id of ['0','-1','1.5','2147483648','3x'])test('invalid route '+id,async()=>{const f=fixture('ADMIN',id);await f.c.load();assert.equal(f.calls.length,0);});
test('initial no image is honest; confirmed owner save then reloads new image',async()=>{
 const f=fixture();await f.c.load();assert.equal(f.c.currentImage.value,'');assert.equal(f.c.status.value.version,0);await select(f);await f.c.save();assert.equal(writes(f).length,0);f.c.confirmed.value=true;await f.c.save();assert.deepEqual(writes(f)[0],['save',3,0,Buffer.from(bytes).toString('base64')]);assert.equal(f.c.newImage.value,'');assert.equal(f.c.currentImage.value,'blob:synthetic');assert.match(f.c.message.value,/Đã lưu/);
});
test('old image revoked on reload and unmount',async()=>{const f=fixture();f.controls.read=image(2);await f.c.load();await f.c.load();assert.equal(f.revoked.length,1);f.cleanup();assert.equal(f.revoked.length,2);assert.equal(f.c.status.value,null);});
test('unknown save clears image and blocks retry until explicit reload',async()=>{
 const f=fixture();await f.c.load();await select(f);f.c.confirmed.value=true;f.controls.failSave=true;await f.c.save();assert.equal(f.c.needsReview.value,true);assert.equal(f.c.newImage.value,'');assert.equal(f.c.status.value,null);f.c.newImage.value='again';f.c.confirmed.value=true;await f.c.save();assert.equal(writes(f).length,1);await f.c.load();assert.equal(f.c.needsReview.value,false);
});
test('double submit and reload blocked during pending upload; selected image cleared early',async()=>{
 const f=fixture(),d=deferred();await f.c.load();await select(f);f.c.confirmed.value=true;f.controls.save=d.promise;const saving=f.c.save();assert.equal(f.c.newImage.value,'');await f.c.save();await f.c.load();assert.equal(writes(f).length,1);d.resolve(image(1));await saving;
});
for(const bad of [{...image(1),doctorId:4},{...image(1),signatureImageUrl:'https://other/image.png'},image(3)])test('malformed saved status rejected '+JSON.stringify(bad),async()=>{
 const f=fixture();await f.c.load();await select(f);f.c.confirmed.value=true;f.controls.save=bad;await f.c.save();assert.equal(f.c.message.value,'');assert.equal(f.c.needsReview.value,true);
});
test('invalid binary MIME cannot become image preview',async()=>{const f=fixture();f.controls.read=image(1);f.controls.file=new Blob(['<svg/>'],{type:'image/svg+xml'});await f.c.load();assert.equal(f.c.status.value,null);assert.equal(f.c.currentImage.value,'');});
test('auth change and unmount ignore late replies and erase buffers',async()=>{
 const f=fixture(),d=deferred();f.controls.read=d.promise;const reading=f.c.load();f.auth.role='';f.watchers[0]();d.resolve(image(1));await reading;assert.equal(f.c.status.value,null);
 const g=fixture(),e=deferred();g.controls.read=image(1);g.controls.file=e.promise;const loading=g.c.load();await Promise.resolve();g.cleanup();e.resolve(new Blob([bytes],{type:'image/png'}));await loading;assert.equal(g.c.currentImage.value,'');
});
test('route change resets image before switching doctor',async()=>{const f=fixture('ADMIN');f.controls.read=image(1);await f.c.load();f.route.params.doctorId='4';f.controls.read=image(0,4);f.watchers[0]();assert.equal(f.c.currentImage.value,'');assert.equal(f.revoked.length,1);await Promise.resolve();assert.equal(f.c.status.value.doctorId,4);});
test('no image persistence or console logs; navigation only active dentists/own profile',async()=>{
 assert.doesNotMatch(source+serviceSource,/localStorage|sessionStorage|console\./);const profile=await readFile(new URL('../src/views/ProfileView.vue',import.meta.url),'utf8'),staff=await readFile(new URL('../src/views/StaffView.vue',import.meta.url),'utf8');assert.match(profile,/staff\/signatures/);assert.match(staff,/staff\/signatures/);assert.match(staff,/DENTIST/);
});
