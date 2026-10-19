import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript } from '@vue/compiler-sfc';
function execute(source, imports) {
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const module = { exports: {} }; new Function('require', 'module', 'exports', js)(name => imports[name] ?? {}, module, module.exports); return module.exports;
}
const serviceSource = await readFile(new URL('../src/services/dentalImages.ts', import.meta.url), 'utf8');
const service = execute(serviceSource, { './api': { apiClient: {} } });
for (const [tooth, valid] of [[null,true],[11,true],[48,true],[51,true],[85,true],[19,false],[56,false],[90,false],[-1,false],[11.5,false],[NaN,false]]) {
  test(`image tooth ${tooth} uses FDI`, () => assert.equal(service.validImageTooth(tooth), valid));
}
test('image input rejects missing, overlarge and MIME/extension mismatch', () => {
  assert.ok(service.imageFileError(null)); assert.ok(service.imageFileError({ size: 10 * 1024 * 1024 + 1, name: 'x.png', type: 'image/png' }));
  assert.ok(service.imageFileError({ size: 100, name: 'x.jpg', type: 'image/png' })); assert.ok(service.imageFileError({ size: 100, name: 'x.svg', type: 'image/svg+xml' }));
  assert.equal(service.imageFileError({ size: 100, name: 'ảnh.PNG', type: 'image/png' }), '');
});
test('multipart request clears the API JSON content type and omits nullable ids', async () => {
  let call;
  const s = execute(serviceSource, { './api': { apiClient: { post: async (...args) => { call = args; return { data: { id: 1 } }; } } } });
  const file = new File([new Uint8Array(12)], 'x.png', { type: 'image/png' });
  await s.dentalImagesService.upload({ patientId: 5, visitId: null, toothNumber: null, category: 'Other' }, file);
  assert.equal(call[0], '/dental-images/upload'); assert.equal(call[2].headers['Content-Type'], undefined);
  assert.equal(call[1].get('patientId'), '5'); assert.equal(call[1].has('visitId'), false); assert.equal(call[1].has('toothNumber'), false);
});
const vueSource = await readFile(new URL('../src/components/DentalImageAttachments.vue', import.meta.url), 'utf8');
const compiled = compileScript(parse(vueSource).descriptor, { id: 'dental-image-test' }).content;
function deferred() { let resolve; const promise = new Promise(r => { resolve = r; }); return { resolve, promise }; }
function fixture(role = 'DENTIST') {
  const calls = [], hooks = {}, auth = { role }, controls = {};
  const store = { items: [], loading: false, hasMore: false, clear() { this.items = []; }, async load(...args) { calls.push(['list', ...args]); } };
  const api = { async targets() { return []; }, async upload(...args) { calls.push(['upload', ...args]); if (controls.error) throw controls.error; return controls.pending ?? { id: 1, originalFileName: 'x.png' }; }, async attach(...args) { calls.push(['attach', ...args]); return {}; } };
  const mod = execute(compiled, { vue: { defineComponent: x => x, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch: (_get, run) => { hooks.watch = run; }, onUnmounted: run => { hooks.unmount = run; } },
    '@/stores/auth': { useAuthStore: () => auth }, '@/stores/dentalImages': { useDentalImagesStore: () => store }, '@/services/dentalImages': { ...service, dentalImagesService: api } });
  const c = mod.default.setup({ patientId: 5 }, { expose() {} });
  c.file.value = { name: 'x.png', type: 'image/png', size: 100 };
  return { c, calls, hooks, controls, auth, store };
}
test('upload rejects invalid FDI without sending request', async () => { const f = fixture(); f.c.toothText.value = '19'; await f.c.upload(); assert.match(f.c.error.value, /FDI/); assert.equal(f.calls.length, 0); });
test('upload rejects overlong notes without sending request', async () => { const f = fixture(); f.c.notes.value = 'a'.repeat(501); await f.c.upload(); assert.match(f.c.error.value, /500/); assert.equal(f.calls.length,0); });
test('upload sends nullable visit and clears file after success', async () => { const f = fixture(); await f.c.upload(); assert.equal(f.calls[0][1].visitId, null); assert.equal(f.c.file.value, null); assert.match(f.c.success.value, /Đã lưu ảnh/); });
test('upload prevents double submit', async () => { const f = fixture(), d = deferred(); f.controls.pending = d.promise; const first = f.c.upload(); await f.c.upload(); assert.equal(f.calls.filter(x => x[0] === 'upload').length, 1); d.resolve({ id: 1, originalFileName: 'x.png' }); await first; });
test('departing component ignores late upload response', async () => { const f = fixture(), d = deferred(); f.controls.pending = d.promise; const pending = f.c.upload(); f.hooks.unmount(); d.resolve({ id: 99, originalFileName: 'other.png' }); await pending; assert.equal(f.c.success.value, ''); assert.equal(f.c.file.value, null); assert.equal(f.calls.filter(x => x[0] === 'list').length, 0); });
test('failed upload has explicit error and permits deliberate retry', async () => { const f = fixture(); f.controls.error = { response: { data: { message: 'Ca đã đóng' } } }; await f.c.upload(); assert.equal(f.c.error.value, 'Ca đã đóng'); assert.equal(f.c.busy.value, false); assert.notEqual(f.c.file.value, null); });
test('patient and receptionist cannot upload', async () => { for (const role of ['PATIENT', 'RECEPTIONIST']) { const f = fixture(role); await f.c.upload(); assert.equal(f.calls.length, 0); } });
test('assistant can upload but does not gain read/attach rights', async () => { const f = fixture('ASSISTANT'); assert.equal(f.c.canRead.value, false); assert.equal(f.c.canManage.value, false); await f.c.upload(); assert.equal(f.calls.filter(x => x[0] === 'upload').length, 1); assert.equal(f.calls.filter(x => x[0] === 'list').length, 0); });
test('attach uses selected visit and server version', async () => { const f = fixture(); f.c.attachTargets.value[1] = 8; await f.c.attach({ id: 1, version: 3, visitId: null }); assert.deepEqual(f.calls[0], ['attach', { id: 1, version: 3, visitId: null }, 8]); });
test('identity change clears selected file and prior patient data', () => { const f = fixture(); f.c.success.value = 'old patient'; f.hooks.watch(); assert.equal(f.c.file.value, null); assert.equal(f.c.success.value, ''); assert.deepEqual(f.store.items, []); });
test('comparison preserves selection order and limits selection to two',()=>{const f=fixture();const images=[1,2,3].map(id=>({id,patientId:5}));f.store.items=images;f.c.toggleComparison(images[1]);f.c.toggleComparison(images[0]);f.c.toggleComparison(images[2]);assert.deepEqual(f.c.comparisonIds.value,[2,1]);f.c.showComparison();assert.deepEqual(f.c.viewerImages.value.map(x=>x.id),[2,1]);});
test('identity change closes viewer and clears comparison',()=>{const f=fixture();f.c.showImage({id:1,patientId:5});assert.equal(f.c.viewerImages.value.length,1);f.hooks.watch();assert.deepEqual(f.c.viewerImages.value,[]);assert.deepEqual(f.c.comparisonIds.value,[]);});
test('wrong patient and upload-only role cannot open a viewer',()=>{const f=fixture();f.c.showImage({id:1,patientId:6});assert.equal(f.c.viewerImages.value.length,0);const assistant=fixture('ASSISTANT');assistant.c.showImage({id:1,patientId:5});assert.equal(assistant.c.viewerImages.value.length,0);});

const storeSource = await readFile(new URL('../src/stores/dentalImages.ts', import.meta.url), 'utf8');
function storeFixture() {
  const replies = [];
  const mod = execute(storeSource, { vue: { ref: value => ({ value }) }, pinia: { defineStore: (_id, setup) => setup }, '@/services/dentalImages': { dentalImagesService: { list: () => { const reply = deferred(); replies.push(reply); return reply.promise; } } } });
  return { store: mod.useDentalImagesStore(), replies };
}
test('store replacement refresh wins over an older pending read', async () => {
  const {store,replies} = storeFixture(); const old = store.load(5); const fresh = store.load(5);
  replies[1].resolve({items:[{id:2}],hasMore:false}); await fresh; replies[0].resolve({items:[{id:1}],hasMore:true}); await old;
  assert.deepEqual(store.items.value,[{id:2}]); assert.equal(store.hasMore.value,false);
});
test('store clear ignores a response from the previous patient', async () => {
  const {store,replies} = storeFixture(); const pending = store.load(5); store.clear(); replies[0].resolve({items:[{id:1}],hasMore:true}); await pending;
  assert.deepEqual(store.items.value,[]); assert.equal(store.loading.value,false);
});
