import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('LoginView supports CCCD and Passport with nationality and lockout message', async () => {
  const loginSrc = await readFile(new URL('../src/views/LoginView.vue', import.meta.url), 'utf8');

  // Kiểm tra selector CCCD / Hộ chiếu
  assert.match(loginSrc, /loginMode/);
  assert.match(loginSrc, /passport/);
  assert.match(loginSrc, /nationalityCode/);
  assert.match(loginSrc, /Quốc gia cấp hộ chiếu/);

  // Kiểm tra xử lý mã lỗi 423 (khóa tạm 15 phút)
  assert.match(loginSrc, /423/);
  assert.match(loginSrc, /Tài khoản tạm thời bị khóa 15 phút/);

  // Kiểm tra authStore.login truyền đúng documentType và nationalityCode
  assert.match(loginSrc, /authStore\.login\(phone\.value,\s*password\.value,\s*docType,\s*country\)/);
});

test('auth store login accepts documentType and nationalityCode', async () => {
  const authStoreSrc = await readFile(new URL('../src/stores/auth.ts', import.meta.url), 'utf8');

  assert.match(authStoreSrc, /documentType\?:/);
  assert.match(authStoreSrc, /nationalityCode\?:/);
  assert.match(authStoreSrc, /payload\.documentType = documentType/);
  assert.match(authStoreSrc, /payload\.nationalityCode = nationalityCode/);
});
