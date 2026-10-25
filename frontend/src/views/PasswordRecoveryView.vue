<template>
  <section class="recovery-card" aria-labelledby="recovery-title">
    <div class="recovery-brand">
      <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="40" height="40" />
    </div>
    <h1 id="recovery-title">Khôi phục mật khẩu</h1>
    <p>Bệnh nhân nhập CCCD và số điện thoại đã đăng ký. Nhân viên chỉ cần số điện thoại. Mã xác minh được gửi qua SMS.</p>
    <p class="notice" role="status">{{ message }}</p>
    <form v-if="!done" @submit.prevent="submit" novalidate :aria-busy="busy">
      <label for="recovery-phone">Số điện thoại đã đăng ký</label>
      <input id="recovery-phone" ref="phoneInput" v-model="phone" type="tel" name="phone" inputmode="numeric"
        autocomplete="tel" maxlength="10" :disabled="busy || !!store.challenge" :aria-invalid="invalidField === 'phone'" aria-describedby="recovery-error" />
      <label for="recovery-citizen-id">CCCD của bệnh nhân (nhân viên để trống)</label>
      <input id="recovery-citizen-id" ref="citizenIdInput" v-model="citizenId" type="text" name="citizen-id" inputmode="numeric"
        autocomplete="off" maxlength="12" :disabled="busy || !!store.challenge" :aria-invalid="invalidField === 'citizenId'" aria-describedby="recovery-error" />
      <template v-if="store.challenge">
        <p>Mã gồm 6 chữ số, có hiệu lực 5 phút và tối đa 5 lần nhập sai. Có thể yêu cầu mã mới sau 60 giây.</p>
        <label for="recovery-code">Mã OTP từ SMS</label>
        <input id="recovery-code" ref="codeInput" v-model="code" name="code" type="text" inputmode="numeric" maxlength="6"
          autocomplete="one-time-code" :disabled="busy" :aria-invalid="invalidField === 'code'" aria-describedby="recovery-error" />
        <label for="recovery-password">Mật khẩu mới</label>
        <input id="recovery-password" ref="passwordInput" v-model="password" name="new-password" type="password"
          autocomplete="new-password" maxlength="72" :disabled="busy" :aria-invalid="invalidField === 'password'" aria-describedby="password-help recovery-error" />
        <p id="password-help">Từ 8 ký tự, gồm chữ hoa, chữ thường và chữ số; tối đa 72 byte UTF-8.</p>
        <label for="recovery-confirm">Nhập lại mật khẩu mới</label>
        <input id="recovery-confirm" ref="confirmInput" v-model="confirmPassword" name="confirm-password" type="password"
          autocomplete="new-password" maxlength="72" :disabled="busy" :aria-invalid="invalidField === 'confirm'" aria-describedby="recovery-error" />
      </template>
      <p id="recovery-error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="busy">{{ busy ? 'Đang xử lý…' : store.challenge ? 'Đặt mật khẩu mới' : 'Nhận mã qua SMS' }}</button>
      <div v-if="store.challenge" class="actions">
        <button type="button" :disabled="busy" @click="requestCode">Gửi lại mã</button>
        <button type="button" :disabled="busy" @click="changeIdentity">Nhập thông tin khác</button>
      </div>
    </form>
    <RouterLink to="/login">Quay lại đăng nhập</RouterLink>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { passwordRecoveryService } from '@/services/passwordRecovery';
import { usePasswordRecoveryStore } from '@/stores/passwordRecovery';

const store = usePasswordRecoveryStore();
const phone = ref(''), citizenId = ref(''), code = ref(''), password = ref(''), confirmPassword = ref('');
const error = ref(''), message = ref(''), invalidField = ref(''), busy = ref(false), done = ref(false);
const phoneInput = ref<HTMLInputElement | null>(null), codeInput = ref<HTMLInputElement | null>(null);
const citizenIdInput = ref<HTMLInputElement | null>(null);
const passwordInput = ref<HTMLInputElement | null>(null), confirmInput = ref<HTMLInputElement | null>(null);
let active = true, requestVersion = 0, resendAt = 0;

function reject(field: string, text: string) {
  invalidField.value = field; error.value = text;
  ({ phone: phoneInput, citizenId: citizenIdInput, code: codeInput, password: passwordInput, confirm: confirmInput }[field])?.value?.focus();
}
function clearSecrets() { code.value = ''; password.value = ''; confirmPassword.value = ''; }
async function requestCode() {
  if (busy.value || !active || done.value) return;
  error.value = ''; invalidField.value = '';
  if (!/^0[0-9]{9}$/.test(phone.value)) return reject('phone', 'Nhập số điện thoại gồm 10 chữ số, bắt đầu bằng 0.');
  if (citizenId.value && !/^[0-9]{12}$/.test(citizenId.value)) return reject('citizenId', 'CCCD phải gồm đúng 12 chữ số.');
  if (Date.now() < resendAt) { error.value = 'Vui lòng chờ đủ 60 giây trước khi yêu cầu mã mới.'; return; }
  busy.value = true;
  const version = ++requestVersion;
  try {
    const response = await passwordRecoveryService.request(phone.value, citizenId.value);
    if (!active || version !== requestVersion) return;
    store.set(response); clearSecrets(); resendAt = Date.now() + response.resendAfterSeconds * 1000;
    message.value = 'Nếu số điện thoại thuộc tài khoản đang hoạt động, bạn sẽ nhận được mã SMS. Nếu chưa nhận được, kiểm tra số điện thoại hoặc liên hệ phòng khám.';
    await nextTick(); if (active) codeInput.value?.focus();
  } catch (err: any) {
    if (active && version === requestVersion) { clearSecrets(); error.value = err.response?.data?.message || 'Chưa thể yêu cầu mã. Vui lòng thử lại hoặc liên hệ phòng khám.'; }
  } finally { if (active && version === requestVersion) busy.value = false; }
}
async function submit() {
  if (busy.value || !active || done.value) return;
  if (!store.challenge) { await requestCode(); return; }
  error.value = ''; invalidField.value = '';
  if (!/^[0-9]{6}$/.test(code.value)) return reject('code', 'Nhập mã OTP gồm đúng 6 chữ số từ SMS.');
  if (password.value.length < 8 || password.value.includes('\0') || new TextEncoder().encode(password.value).length > 72 ||
      !/[A-Z]/.test(password.value) || !/[a-z]/.test(password.value) || !/[0-9]/.test(password.value))
    return reject('password', 'Mật khẩu cần từ 8 ký tự, gồm chữ hoa, chữ thường và chữ số; tối đa 72 byte UTF-8.');
  if (password.value !== confirmPassword.value) return reject('confirm', 'Mật khẩu nhập lại chưa khớp.');
  busy.value = true;
  const version = ++requestVersion;
  try {
    await passwordRecoveryService.reset(store.challenge.challengeId, phone.value, code.value, password.value, citizenId.value);
    if (!active || version !== requestVersion) return;
    store.clear(); phone.value = ''; citizenId.value = ''; clearSecrets(); done.value = true;
    message.value = 'Đã đặt mật khẩu mới. Vui lòng đăng nhập lại bằng mật khẩu mới.';
  } catch (err: any) {
    if (active && version === requestVersion) { clearSecrets(); error.value = err.response?.data?.message || 'Chưa xác định được kết quả. Hãy thử đăng nhập bằng mật khẩu mới hoặc yêu cầu mã khác.'; }
  } finally { if (active && version === requestVersion) busy.value = false; }
}
function changeIdentity() {
  if (busy.value) return;
  ++requestVersion; store.clear(); clearSecrets(); phone.value = ''; citizenId.value = ''; error.value = ''; message.value = ''; invalidField.value = '';
  // Keep the local resend deadline; changing the form must not bypass the server cooldown.
  nextTick(() => phoneInput.value?.focus());
}
onMounted(() => { store.clear(); phoneInput.value?.focus(); });
onUnmounted(() => { active = false; ++requestVersion; store.clear(); clearSecrets(); phone.value = ''; citizenId.value = ''; message.value = ''; });
</script>

<style scoped>
.recovery-card { max-width: 440px; margin: 2rem auto; padding: 1.5rem; background: white; border: 1px solid #d7dce1; border-radius: 8px; }
.recovery-brand { display: flex; justify-content: center; margin-bottom: 0.75rem; }
.brand-logo { display: block; }
label { display: block; margin: 1rem 0 .4rem; font-weight: 600; }
input { box-sizing: border-box; width: 100%; padding: .65rem; font-size: 1rem; border: 1px solid #7b8791; border-radius: 4px; }
button { min-height: 40px; padding: .6rem 1rem; cursor: pointer; }
button:disabled { cursor: wait; }
.actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .75rem 0; }
[role=alert] { color: #a51d20; }
.notice { color: #18552d; }
:focus-visible { outline: 2px solid #185eac; outline-offset: 2px; }
p { line-height: 1.5; }
</style>
