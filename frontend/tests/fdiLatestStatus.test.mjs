import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';

const source = await readFile(new URL('../src/types/fdi.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const module = { exports: {} };
new Function('module', 'exports', compiled)(module, module.exports);
const { latestConditionBySurface, toothConditionColor, TOOTH_CONDITION_LEGEND } = module.exports;

test('latest whole-tooth status applies to surfaces until a newer surface status exists', () => {
  const states = latestConditionBySurface([
    { id: 1, toothNumber: 11, surface: null, conditionCode: 'MISSING', createdAt: '2026-01-01T00:00:00Z' },
    { id: 2, toothNumber: 11, surface: 'B', conditionCode: 'CARIES', createdAt: '2026-01-02T00:00:00Z' }
  ]);
  assert.equal(states.get(11).buccal, 'CARIES');
  assert.equal(states.get(11).lingual, 'MISSING');
  assert.equal(states.get(11).occlusal, 'MISSING');
});

test('multi-surface records update every included face and ID breaks timestamp ties', () => {
  const states = latestConditionBySurface([
    { id: 4, toothNumber: 16, surface: 'BL', conditionCode: 'CARIES', createdAt: '2026-01-01T00:00:00Z' },
    { id: 5, toothNumber: 16, surface: 'B', conditionCode: 'RESTORED', createdAt: '2026-01-01T00:00:00Z' }
  ]);
  assert.equal(states.get(16).buccal, 'RESTORED');
  assert.equal(states.get(16).lingual, 'CARIES');
});

test('DL-217 color legend maps supported and related condition codes', () => {
  assert.equal(toothConditionColor('NORMAL'), 'healthy');
  assert.equal(toothConditionColor('CARIES'), 'caries');
  assert.equal(toothConditionColor('RESTORED'), 'restored');
  assert.equal(toothConditionColor('MISSING'), 'missing');
  assert.equal(toothConditionColor('PULPITIS'), 'treatment');
  assert.equal(toothConditionColor('CROWN'), 'prosthetic');
  assert.equal(toothConditionColor('GINGIVITIS'), 'caries');
  assert.equal(TOOTH_CONDITION_LEGEND.length, 6);
});

test('FDI condition view uses patient-wide latest statuses and chart legend includes six states', async () => {
  const view = await readFile(new URL('../src/views/FdiConditionView.vue', import.meta.url), 'utf8');
  const chart = await readFile(new URL('../src/components/fdi/FdiDentalChart.vue', import.meta.url), 'utf8');
  const service = await readFile(new URL('../src/services/fdi.ts', import.meta.url), 'utf8');
  assert.match(view, /:latest-conditions="latestConditions"/);
  assert.match(view, /refreshLatestConditions\(details\.patientId, version, visitId\.value\)/);
  assert.match(chart, /TOOTH_CONDITION_LEGEND/);
  assert.match(chart, /isMissingTooth\(num\)/);
  assert.match(service, /teeth\/latest-conditions/);
});
