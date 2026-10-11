import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
const source = await readFile(new URL('../src/services/receiptBarcode.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const { MOD_BIL_receiptBarcode } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
for (const value of ['', 'abc', '<script>', 'INV*01', 'A'.repeat(51)]) {
  test(`reject invalid barcode payload ${value.slice(0, 10)}`, () => assert.equal(MOD_BIL_receiptBarcode(value), null));
}
test('invoice barcode includes start/stop, ten-module quiet zones and expected width', () => {
  const value = 'INV-20261011-0001', code = MOD_BIL_receiptBarcode(value);
  assert.equal(code.bars.length, (value.length + 2) * 5);
  assert.equal(code.width, 20 + (value.length + 2) * 12 + value.length + 1);
  assert.equal(code.bars[0].x, 10); const last = code.bars.at(-1);
  assert.equal(code.width - last.x - last.width, 10);
  assert.deepEqual(code.bars.slice(0, 5).map(bar => bar.width), code.bars.slice(-5).map(bar => bar.width));
});
test('known character A produces standard wide/narrow bar widths', () => {
  const code = MOD_BIL_receiptBarcode('A');
  assert.deepEqual(code.bars.slice(5, 10).map(bar => bar.width), [2, 1, 1, 1, 2]);
});
