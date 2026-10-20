import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript } from '@vue/compiler-sfc';
function execute(source, imports, globals = {}) {
  const js=ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText;
  const module={exports:{}};new Function('require','module','exports',...Object.keys(globals),js)(name=>imports[name]??{},module,module.exports,...Object.values(globals));return module.exports;
}
function deferred(){let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b;});return{promise,resolve,reject};}
const blobSource=await readFile(new URL('../src/composables/useDentalImageBlob.ts',import.meta.url),'utf8');
function blobFixture(){
  const hooks=[],calls=[],created=[],revoked=[],responses=[];let id=1;
  const api={file:async(image,signal)=>{calls.push({image,signal});return responses.shift()??new Blob([new Uint8Array(20)],{type:'image/png'});}};
  const mod=execute(blobSource,{vue:{ref:value=>({value}),onUnmounted:fn=>hooks.push(fn)},'@/services/dentalImages':{dentalImagesService:api}},{URL:{createObjectURL(blob){created.push(blob);return 'blob:test-'+created.length;},revokeObjectURL:url=>revoked.push(url)}});
  return{state:mod.useDentalImageBlob(()=>id),calls,created,revoked,responses,cleanup:()=>hooks.forEach(fn=>fn()),setId:value=>{id=value;}};
}
test('blob loader uses protected API and releases URL on unmount',async()=>{const f=blobFixture();await f.state.load();assert.equal(f.calls[0].image,1);assert.ok(f.calls[0].signal instanceof AbortSignal);assert.equal(f.state.src.value,'blob:test-1');f.cleanup();assert.deepEqual(f.revoked,['blob:test-1']);assert.equal(f.state.src.value,'');});
test('changing image cancels old request and ignores its late response',async()=>{const f=blobFixture(),old=deferred();f.responses.push(old.promise);const pending=f.state.load();f.setId(2);await f.state.load();old.resolve(new Blob([new Uint8Array(20)],{type:'image/png'}));await pending;assert.equal(f.calls[0].signal.aborted,true);assert.equal(f.created.length,1);assert.equal(f.state.src.value,'blob:test-1');});
test('closing before fetch completes never creates a blob URL',async()=>{const f=blobFixture(),d=deferred();f.responses.push(d.promise);const pending=f.state.load();f.cleanup();d.resolve(new Blob([new Uint8Array(20)],{type:'image/png'}));await pending;assert.equal(f.created.length,0);assert.equal(f.state.src.value,'');});
test('reload revokes the previous file before showing another',async()=>{const f=blobFixture();await f.state.load();await f.state.load();assert.deepEqual(f.revoked,['blob:test-1']);assert.equal(f.state.src.value,'blob:test-2');f.cleanup();});
for(const [type,size] of [['text/html',20],['application/json',20],['image/png',11],['image/png',10485761]])test(`invalid blob ${type}/${size} is rejected`,async()=>{const f=blobFixture();f.responses.push(new Blob([new Uint8Array(size)],{type}));await f.state.load();assert.ok(f.state.error.value);assert.equal(f.created.length,0);});
test('request failure displays a generic message without details',async()=>{const f=blobFixture();f.responses.push(Promise.reject(new Error('private server path')));await f.state.load();assert.match(f.state.error.value,/Không tải được ảnh/);assert.ok(!f.state.error.value.includes('private'));assert.equal(f.state.loading.value,false);});

async function script(path){const source=await readFile(new URL(path,import.meta.url),'utf8');return compileScript(parse(source).descriptor,{id:'image-viewer-test'}).content;}
const canvasSource=await script('../src/components/DentalImageCanvas.vue');
function canvasFixture(){
  let mounted,watched;const blob={src:{value:''},loading:{value:false},error:{value:''},load:async()=>{},release(){this.src.value='';}};
  const mod=execute(canvasSource,{vue:{defineComponent:x=>x,ref:value=>({value}),onMounted:fn=>{mounted=fn;},watch:(_get,fn)=>{watched=fn;}},'@/composables/useDentalImageBlob':{useDentalImageBlob:()=>({...blob,release:()=>{blob.src.value='';}})}});
  return{c:mod.default.setup({image:{id:1},label:'Ảnh'},{expose(){}}),mounted,watched};
}
test('canvas zoom stays within 50 to 500 percent and reset clears transforms',()=>{const{c}=canvasFixture();c.changeZoom(1000);assert.equal(c.zoom.value,500);c.changeZoom(-1000);assert.equal(c.zoom.value,50);c.angle.value=90;c.pan(30,-30);c.reset();assert.equal(c.zoom.value,100);assert.equal(c.angle.value,0);assert.equal(c.x.value,0);assert.equal(c.y.value,0);});
test('canvas wheel supports zoom but preserves browser ctrl zoom',()=>{const{c}=canvasFixture();let prevented=0;c.wheel({deltaY:-100,preventDefault(){prevented++;}});assert.equal(c.zoom.value,125);c.wheel({ctrlKey:true,deltaY:-100,preventDefault(){prevented++;}});assert.equal(c.zoom.value,125);assert.equal(prevented,1);});
test('keyboard arrows and plus/minus provide a drag alternative',()=>{const{c}=canvasFixture();let prevented=0;for(const key of ['ArrowRight','ArrowDown','+'])c.key({key,preventDefault(){prevented++;}});assert.equal(c.x.value,30);assert.equal(c.y.value,30);assert.equal(c.zoom.value,125);assert.equal(prevented,3);});
test('pointer drag requires zoom and stops on cancellation',()=>{const{c}=canvasFixture();let captured=0;const event={button:0,pointerId:3,clientX:10,clientY:20,preventDefault(){},currentTarget:{focus(){},setPointerCapture(){captured++;}}};c.startDrag(event);assert.equal(captured,0);c.zoom.value=200;c.startDrag(event);c.drag({pointerId:3,clientX:30,clientY:50});assert.equal(c.x.value,20);assert.equal(c.y.value,30);c.stopDrag();c.drag({pointerId:3,clientX:100,clientY:100});assert.equal(c.x.value,20);});
test('two canvases keep independent zoom, rotation and pan',()=>{const a=canvasFixture().c,b=canvasFixture().c;a.changeZoom(50);a.angle.value=90;a.pan(40,0);assert.equal(b.zoom.value,100);assert.equal(b.angle.value,0);assert.equal(b.x.value,0);});

const modalSource=await script('../src/components/DentalImageViewer.vue');
test('native modal opens and restores the connected trigger on unmount',async()=>{
  const hooks={};let shown=0,headingFocus=0,restored=0;class Element{isConnected=true;focus(){restored++;}}
  const trigger=new Element();const mod=execute(modalSource,{vue:{defineComponent:x=>x,ref:value=>({value}),onMounted:fn=>{hooks.mount=fn;},onUnmounted:fn=>{hooks.unmount=fn;},nextTick:async()=>{}}},{document:{activeElement:trigger},HTMLElement:Element});
  const c=mod.default.setup({images:[]},{expose(){},emit(){}});c.dialog.value={showModal(){shown++;}};c.heading.value={focus(){headingFocus++;}};await hooks.mount();hooks.unmount();assert.equal(shown,1);assert.equal(headingFocus,1);assert.equal(restored,1);
});

const thumbnailSource=await script('../src/components/DentalImageThumbnail.vue');
function thumbnailFixture(){
  const hooks={};let callback,loads=0,releases=0,disconnected=0;
  const state={src:{value:''},loading:{value:false},error:{value:''},async load(){loads++;},release(){releases++;}};
  class Observer{constructor(fn){callback=fn;}observe(){}disconnect(){disconnected++;}}
  const mod=execute(thumbnailSource,{vue:{defineComponent:x=>x,ref:value=>({value}),onMounted:fn=>{hooks.mount=fn;},onUnmounted:fn=>{hooks.unmount=fn;},watch(){}},'@/composables/useDentalImageBlob':{useDentalImageBlob:()=>state}},{IntersectionObserver:Observer});
  const c=mod.default.setup({image:{id:1}},{expose(){},emit(){}});c.host.value={};hooks.mount();
  return{state,notify:visible=>callback([{isIntersecting:visible}]),cleanup:hooks.unmount,get loads(){return loads;},get releases(){return releases;},get disconnected(){return disconnected;}};
}
test('thumbnail fetches on visibility and releases the hidden preview',()=>{const f=thumbnailFixture();assert.equal(f.loads,0);f.notify(true);assert.equal(f.loads,1);f.notify(false);assert.equal(f.releases,1);});
test('queued observer callbacks after unmount cannot restart a private image fetch',()=>{const f=thumbnailFixture();f.cleanup();f.notify(true);assert.equal(f.disconnected,1);assert.equal(f.loads,0);});
