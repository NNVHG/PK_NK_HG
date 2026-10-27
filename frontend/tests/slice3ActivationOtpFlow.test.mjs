import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('LoginView redirects to activation on AUTH_ACTIVATION_REQUIRED', async () => {
  const loginSrc = await readFile(new URL('../src/views/LoginView.vue', import.meta.url), 'utf8');

  // Kiểm tra bắt mã lỗi AUTH_ACTIVATION_REQUIRED và redirect
  assert.match(loginSrc, /AUTH_ACTIVATION_REQUIRED/);
  assert.match(loginSrc, /\/activate-account/);
  assert.match(loginSrc, /stageActivationPrefill/);
  assert.doesNotMatch(loginSrc, /query:\s*\{\s*identifier/);
});

test('PatientActivationView supports CCCD, SĐT and Passport without identity in URL', async () => {
  const actSrc = await readFile(new URL('../src/views/PatientActivationView.vue', import.meta.url), 'utf8');

  assert.match(actSrc, /loginMode/);
  assert.match(actSrc, /passport/);
  assert.match(actSrc, /nationalityCode/);
  assert.match(actSrc, /takeActivationPrefill\(\)/);
  assert.doesNotMatch(actSrc, /route\.query\.(identifier|country)/);
  assert.match(actSrc, /patientActivationService\.request\(val,\s*password\.value,\s*docType,\s*country\)/);
});

test('patientActivationService request passes documentType and nationalityCode', async () => {
  const svcSrc = await readFile(new URL('../src/services/patientActivation.ts', import.meta.url), 'utf8');

  assert.match(svcSrc, /documentType\?:/);
  assert.match(svcSrc, /nationalityCode\?:/);
  assert.match(svcSrc, /payload\.documentType = documentType/);
  assert.match(svcSrc, /payload\.nationalityCode = nationalityCode/);
});
