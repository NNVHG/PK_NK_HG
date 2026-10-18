import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
import QRCode from 'qrcode';
import png from 'pngjs';
const helperSource = await readFile(new URL('../src/services/prescriptionQr.ts', import.meta.url), 'utf8');
const helper = { exports: {} };
new Function('require', 'module', 'exports', ts.transpileModule(helperSource, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText)(name => name === 'qrcode' ? { default: QRCode } : { apiClient: {} }, helper, helper.exports);
const path = '/portal/prescriptions/RX-20261017-0001';
test('real PNG encodes exact deployment URL and has four-module quiet zone', async () => {
 const result = await helper.exports.MOD_RX_lookupQr(path, 'https://clinic.example');
 assert.equal(result.url, 'https://clinic.example' + path);
 const data = png.PNG.sync.read(Buffer.from(result.image.split(',')[1], 'base64'));
 const matrix = QRCode.create(result.url, { errorCorrectionLevel: 'M' }).modules;
 assert.equal(data.width, (matrix.size + 8) * 6); assert.equal(data.height, data.width);
 function pixel(x, y) { return data.data[(y * data.width + x) * 4]; }
 assert.equal(pixel(0, 0), 255); assert.equal(pixel(23, 23), 255);
 for (let y = 0; y < matrix.size; y++) for (let x = 0; x < matrix.size; x++) assert.equal(pixel((x + 4) * 6 + 3, (y + 4) * 6 + 3), matrix.data[y * matrix.size + x] ? 0 : 255);
 assert.doesNotMatch(result.url, /token|citizen|987654/);
});
test('invalid paths, credentials, schemes and extra query parameters never render QR', async () => {
 for (const [p, origin] of [[path + '?token=abc', 'https://clinic.example'], [path + '\n', 'https://clinic.example'], ['/patients/1', 'https://clinic.example'], [path, 'javascript:alert(1)'], [path, 'https://user:pass@clinic.example'], [path, 'https://clinic.example/path'], [path, 'https://clinic.example?cccd=1']]) await assert.rejects(helper.exports.MOD_RX_lookupQr(p, origin));
});
const source = await readFile(new URL('../src/components/PrescriptionQr.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'lookup-qr-test' }).content, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
function deferred() { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; }
function fixture(role = 'DENTIST') {
 const auth = { role, user: { userId: 3 } }, props = { visitId: 7, prescriptionVersion: 1 }, calls = [], watchers = [], controls = {}; let cleanup;
 const module = { exports: {} };
 new Function('require', 'module', 'exports', 'window', compiled)(name => name === 'vue' ? { defineComponent: c => c, ref: value => ({ value }), computed: get => ({ get value() { return get(); } }), watch: (get, cb) => watchers.push(cb), onUnmounted: cb => { cleanup = cb; } } : name === '@/stores/auth' ? { useAuthStore: () => auth } : {
  prescriptionQrService: { link: async id => { calls.push(['link', id]); if (controls.fail) throw Error('Synthetic'); return controls.link ?? { prescriptionCode: 'RX-20261017-0001', version: 1, relativePath: path }; } },
  MOD_RX_lookupQr: async (...args) => { calls.push(['render', ...args]); return { url: 'https://clinic.example' + path, image: 'data:image/png;base64,TEST' }; }
 }, module, module.exports, { location: { origin: 'https://clinic.example' } });
 return { c: module.exports.default.setup(props, { expose() {} }), controls, auth, props, calls, watchers, cleanup: () => cleanup() };
}
test('explicit request loads authorized current saved code then renders at actual origin', async () => {
 const f = fixture(); assert.equal(f.calls.length, 0); await f.c.generate(); assert.equal(f.c.code.value, 'RX-20261017-0001'); assert.deepEqual(f.calls, [['link', 7], ['render', path, 'https://clinic.example']]);
});
test('stale version or mismatching link never renders', async () => {
 for (const patch of [{ version: 2 }, { relativePath: '/patients/7' }]) { const f = fixture(); f.controls.link = { prescriptionCode: 'RX-20261017-0001', version: 1, relativePath: path, ...patch }; await f.c.generate(); assert.equal(f.c.qr.value, null); assert.equal(f.calls.length, 1); }
});
test('server failure clears old QR instead of showing stale image', async () => {
 const f = fixture(); await f.c.generate(); f.controls.fail = true; await f.c.generate(); assert.equal(f.c.qr.value, null); assert.match(f.c.error.value, /Không tạo/);
});
test('duplicate clicks issue one request and late response ignored after auth change', async () => {
 const f = fixture(), d = deferred(); f.controls.link = d.promise; const loading = f.c.generate(); await f.c.generate(); assert.equal(f.calls.length, 1); f.auth.role = ''; f.watchers[0](); d.resolve({ prescriptionCode: 'RX-20261017-0001', version: 1, relativePath: path }); await loading; assert.equal(f.c.qr.value, null); assert.equal(f.calls.length, 1);
});
test('order version change clears image, unmount ignores late response', async () => {
 const f = fixture(); await f.c.generate(); f.props.prescriptionVersion = 2; f.watchers[0](); assert.equal(f.c.qr.value, null);
 const g = fixture(), d = deferred(); g.controls.link = d.promise; const loading = g.c.generate(); g.cleanup(); d.resolve({ prescriptionCode: 'RX-20261017-0001', version: 1, relativePath: path }); await loading; assert.equal(g.c.qr.value, null);
});
test('unknown role and invalid visit never query link', async () => {
 const f = fixture('UNKNOWN'); await f.c.generate(); assert.equal(f.calls.length, 0); const g = fixture(); g.props.visitId = 0; await g.c.generate(); assert.equal(g.calls.length, 0);
});
test('integrated QR uses saved order and approved local library, no external QR service', async () => {
 const parent = await readFile(new URL('../src/views/PrescriptionView.vue', import.meta.url), 'utf8'); assert.match(parent, /<PrescriptionQr v-if="context\.prescription && !context\.prescription\.isCancelled"/); assert.match(helperSource, /QRCode\.toDataURL/); assert.doesNotMatch(helperSource, /https:\/\/|citizenId|previewToken|X-Prescription-View-Token/);
});
