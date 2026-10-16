import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
async function compile(path) { const {descriptor}=parse(await readFile(new URL(path,import.meta.url),'utf8')); return ts.transpileModule(compileScript(descriptor,{id:'catalog-test'}).content,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText; }
const autocomplete=await compile('../src/components/DiagnosisAutocomplete.vue'), catalog=await compile('../src/views/DiagnosisCatalogView.vue');
const item={id:7,code:'TEST',name:'Tên chuẩn',category:'Khác',isActive:true,version:1};
function deferred() { let resolve; const promise=new Promise(done=>{resolve=done;});return {promise,resolve}; }
function suggestions() {
 let cleanup, timer, result={items:[item]}, fail=false; const calls=[], emissions=[], props={modelValue:'',disabled:false};
 const module={exports:{}};
 new Function('require','module','exports','setTimeout','clearTimeout',autocomplete)(name=> name==='vue'?{defineComponent:c=>c,ref:value=>({value}),onUnmounted:cb=>{cleanup=cb;}}:{diagnosesService:{list:async q=>{calls.push(q);if(fail)throw Error('Synthetic');return await result;}}},module,module.exports,cb=>{timer=cb;return 1;},()=>{timer=null;});
 const c=module.exports.default.setup(props,{expose(){},emit:(e,value)=>{emissions.push(value);props.modelValue=value;}});
 return {c,calls,emissions,props,fire:()=>timer?.(),setResult:value=>{result=value;},fail:()=>{fail=true;},cleanup:()=>cleanup()};
}
test('suggestions start after one character, emit standard name, allow free text',async()=>{
 const f=suggestions();f.c.input({target:{value:'s'}});assert.deepEqual(f.emissions,['s']);await f.fire();assert.equal(f.c.items.value.length,1);f.c.choose(item);assert.deepEqual(f.emissions,['s','Tên chuẩn']);
 f.c.input({target:{value:'Chẩn đoán tự do'}});assert.equal(f.emissions.at(-1),'Chẩn đoán tự do');
});
test('stale queries and unmounted component cannot restore suggestions',async()=>{
 const d=deferred(),f=suggestions();f.setResult(d.promise);f.c.input({target:{value:'a'}});const pending=f.fire();f.c.input({target:{value:'b'}});d.resolve({items:[item]});await pending;assert.equal(f.c.items.value.length,0);
 const g=suggestions(),e=deferred();g.setResult(e.promise);g.c.input({target:{value:'a'}});const other=g.fire();g.cleanup();e.resolve({items:[item]});await other;assert.equal(g.c.items.value.length,0);
});
test('disabled, empty input, unknown choices and failures preserve free input',async()=>{
 const f=suggestions();f.props.disabled=true;f.c.input({target:{value:'a'}});assert.equal(f.emissions.length,0);f.props.disabled=false;f.c.choose(item);assert.equal(f.emissions.length,0);
 f.c.input({target:{value:''}});assert.equal(f.calls.length,0);f.fail();f.c.input({target:{value:'x'}});await f.fire();assert.ok(f.c.error.value);assert.equal(f.props.modelValue,'x');
});
function admin(role='ADMIN') {
 let cleanup, fail=false, pending;const calls=[],auth={role};const module={exports:{}};
 const api={list:async()=>({items:[item],page:1,totalCount:1,hasNextPage:false}),save:async(id,payload)=>{calls.push([id,payload]);if(fail)throw Error('Synthetic409');return pending?await pending:{...item,...payload,version:2};}};
 new Function('require','module','exports',catalog)(name=>name==='vue'?{defineComponent:c=>c,ref:value=>({value}),reactive:x=>x,onMounted(){},onUnmounted:cb=>{cleanup=cb;}}:name==='@/stores/auth'?{useAuthStore:()=>auth}:{diagnosesService:api},module,module.exports);
 const c=module.exports.default.setup({},{expose(){}});return {c,calls,auth,fail:()=>{fail=true;},wait:value=>{pending=value;},cleanup:()=>cleanup()};
}
test('admin can create update and deactivate with expected version',async()=>{
 const f=admin();await f.c.load();f.c.open(item);f.c.form.isActive=false;await f.c.save();assert.equal(f.calls[0][0],7);assert.equal(f.calls[0][1].expectedVersion,1);assert.equal(f.calls[0][1].isActive,false);
 f.c.open();Object.assign(f.c.form,{code:'NEW',name:'New',category:'Khác'});await f.c.save();assert.equal(f.calls[1][0],null);
});
test('non Admin, invalid and stale forms never write',async()=>{
 const f=admin('DENTIST');await f.c.load();f.c.open(item);await f.c.save();assert.equal(f.calls.length,0);
 const g=admin();await g.c.load();g.c.open({...item,id:99});assert.equal(g.c.editor.value,false);g.c.open();g.c.form.code='bad code';await g.c.save();assert.equal(g.calls.length,0);
});
test('uncertain writes require reload; double submit blocked; unmount ignores late result',async()=>{
 const f=admin();await f.c.load();f.c.open(item);f.fail();await f.c.save();await f.c.save();assert.equal(f.calls.length,1);assert.equal(f.c.needsReview.value,true);await f.c.load();assert.equal(f.c.editor.value,false);
 const g=admin(),d=deferred();await g.c.load();g.c.open(item);g.wait(d.promise);const pending=g.c.save();await g.c.save();assert.equal(g.calls.length,1);g.cleanup();d.resolve(item);await pending;assert.equal(g.c.message.value,'');
});
