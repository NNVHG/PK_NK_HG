import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';

// Use the existing TypeScript compiler; no frontend runner/package dependency.
const source = await readFile(new URL('../src/services/bankQrDisplay.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { MOD_BIL_bankQrDisplay } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
for (const simulation of [false, true]) {
  for (const qrUrl of [null, 'https://img.vietqr.io/test.png']) {
    for (const failed of [false, true]) {
      test(`QR mode: simulation=${simulation}, configured=${!!qrUrl}, failed=${failed}`, () => {
        const expected = qrUrl && !failed ? 'online' : simulation ? 'offline-demo' : 'unavailable';
        assert.equal(MOD_BIL_bankQrDisplay(qrUrl, failed, simulation), expected);
      });
    }
  }
}
test('retry and next invoice restore live QR instead of retaining demo mode', () => {
  assert.equal(MOD_BIL_bankQrDisplay('https://img.vietqr.io/a.png', true, true), 'offline-demo');
  assert.equal(MOD_BIL_bankQrDisplay('https://img.vietqr.io/a.png', false, true), 'online');
  assert.equal(MOD_BIL_bankQrDisplay('https://img.vietqr.io/b.png', false, false), 'online');
});
