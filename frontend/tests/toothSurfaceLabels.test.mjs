import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/types/fdi.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const module = { exports: {} };
new Function('module', 'exports', compiled)(module, module.exports);
const { formatSurfaceCodes } = module.exports;

test('surface history shows Vietnamese names for standard FDI codes', () => {
  assert.equal(formatSurfaceCodes('BLMDO'), 'Mặt ngoài, Mặt trong, Mặt gần, Mặt xa, Mặt nhai');
  assert.equal(formatSurfaceCodes('MOD'), 'Mặt gần, Mặt nhai, Mặt xa');
  assert.equal(formatSurfaceCodes(null), 'Toàn răng');
});

test('unknown surface conventions remain visible without reinterpretation', () => {
  assert.equal(formatSurfaceCodes('P'), 'P');
  assert.equal(formatSurfaceCodes('M-P'), 'M-P');
});

test('history drawer renders decoded surface labels', async () => {
  const view = await readFile(new URL('../src/components/fdi/ToothHistoryDrawer.vue', import.meta.url), 'utf8');
  const { descriptor } = parse(view);
  compileScript(descriptor, { id: 'surface-label-test' });
  assert.match(view, /formatSurfaceCodes\(item\.surface\)/);
});
