import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
const source = await readFile(new URL('../src/services/checkInForm.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
const mod = { exports: {} }; new Function('module', 'exports', compiled)(mod, mod.exports);
const { validateCheckIn, checkInProblem } = mod.exports;
for (const notes of ['', '   ', 'x'.repeat(500)]) test('F_CHK_01 accepts optional notes length ' + notes.length, () => {
  assert.deepEqual(validateCheckIn('000000000101', '0900000091', notes), {});
});
test('F_CHK_01 rejects 501 character notes without changing identity validation', () => {
  const errors = validateCheckIn('000000000101', '0900000091', 'x'.repeat(501));
  assert.deepEqual(Object.keys(errors), ['notes']); assert.match(errors.notes, /500/);
});
test('F_CHK_01 missing required fields are not described as a notes error', () => {
  const errors = validateCheckIn('', '', ''); assert.deepEqual(Object.keys(errors), ['citizenId', 'phone']);
});
test('F_CHK_01 field details take priority over generic validation message', () => {
  const result = checkInProblem({ message: 'Dữ liệu đầu vào không hợp lệ', errors: [{ field: 'Phone', message: 'Số điện thoại sai.' }] });
  assert.equal(result.message, 'Số điện thoại sai.'); assert.equal(result.fields.phone, result.message);
});
test('F_CHK_01 supports ASP.NET validation dictionary and safe fallback', () => {
  assert.equal(checkInProblem({ errors: { Notes: ['Ghi chú quá dài.'] } }).fields.notes, 'Ghi chú quá dài.');
  assert.equal(checkInProblem({ errors: { NationalityCode: ['Thiếu quốc gia cấp.'] } }).fields.nationalityCode, 'Thiếu quốc gia cấp.');
  assert.ok(checkInProblem(null).message); assert.ok(checkInProblem({ errors: [null, 1] }).message);
});

test('F_CHK_01 accepts passport with issuing country while preserving CCCD rules', () => {
  assert.deepEqual(validateCheckIn('B1234567', '0900000091', '', 'passport', 'us'), {});
  assert.equal(validateCheckIn('B1234567', '0900000091', '', 'passport').nationalityCode?.includes('quốc gia'), true);
  assert.ok(validateCheckIn('B1234567', '0900000091', '', 'passport', 'U1').nationalityCode);
  assert.ok(validateCheckIn('123', '0900000091', '', 'passport', 'US').citizenId);
  assert.ok(validateCheckIn('B1234567', '0900000091', '').citizenId);
});
