<template>
  <section class="recovery-card" aria-labelledby="recovery-title">
    <div class="recovery-brand">
      <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="44" height="44" />
      <span class="brand-badge">Nha Khoa Hoàng Gia</span>
    </div>
    <h1 id="recovery-title">Khôi phục mật khẩu</h1>
    <p class="recovery-desc">Bệnh nhân/người giám hộ nhập giấy tờ và số điện thoại đã đăng ký. Nhân viên dùng số điện thoại đăng ký. Mã xác minh sẽ được gửi qua SMS.</p>

    <!-- Step Indicator -->
    <nav class="step-progress" aria-label="Tiến trình khôi phục">
      <div class="step-item" :class="{ 'step-item--active': !store.challenge && !done, 'step-item--done': store.challenge || done }">
        <span class="step-num">1</span>
        <span class="step-label">Thông tin</span>
      </div>
      <div class="step-line" :class="{ 'step-line--filled': store.challenge || done }"></div>
      <div class="step-item" :class="{ 'step-item--active': store.challenge && !done, 'step-item--done': done }">
        <span class="step-num">2</span>
        <span class="step-label">Xác minh &amp; Mật khẩu</span>
      </div>
    </nav>

    <p v-if="message" class="notice" role="status">{{ message }}</p>
    <form v-if="!done" @submit.prevent="submit" novalidate :aria-busy="busy">
      <label for="recovery-account">Loại tài khoản</label>
      <select id="recovery-account" v-model="accountType" :disabled="busy || !!store.challenge" @change="changeDocument">
        <option value="patient">Bệnh nhân / người giám hộ</option>
        <option value="staff">Nhân viên phòng khám</option>
      </select>
      <label for="recovery-phone">Số điện thoại đã đăng ký</label>
      <input id="recovery-phone" ref="phoneInput" v-model="phone" type="tel" name="phone" inputmode="numeric"
        autocomplete="tel" maxlength="10" :disabled="busy || !!store.challenge" :aria-invalid="invalidField === 'phone'" aria-describedby="recovery-error" placeholder="0xxxxxxxxx" />
      <template v-if="accountType === 'patient'">
        <label for="recovery-document">Giấy tờ đăng nhập</label>
        <select id="recovery-document" v-model="documentType" :disabled="busy || !!store.challenge" @change="changeDocument">
          <option :value="1">CCCD (12 số)</option><option :value="2">Hộ chiếu nước ngoài</option>
        </select>
        <label for="recovery-citizen-id">{{ documentType === 2 ? 'Số hộ chiếu' : 'Số CCCD' }}</label>
        <input id="recovery-citizen-id" ref="citizenIdInput" v-model="citizenId" type="text" name="identity-document"
          :inputmode="documentType === 2 ? 'text' : 'numeric'" autocomplete="off" maxlength="12"
          :disabled="busy || !!store.challenge" :aria-invalid="invalidField === 'citizenId'" aria-describedby="recovery-error" :placeholder="documentType === 2 ? 'Nhập số hộ chiếu' : 'Nhập 12 số CCCD'" />
        <template v-if="documentType === 2">
          <label for="recovery-country">Mã quốc gia cấp hộ chiếu</label>
          <input id="recovery-country" ref="countryInput" v-model="nationalityCode" type="text" name="issuing-country"
            autocomplete="off" maxlength="3" :disabled="busy || !!store.challenge"
            :aria-invalid="invalidField === 'country'" aria-describedby="country-help recovery-error" placeholder="VN, US, JP..." />
          <p id="country-help" class="hint-text">Nhập mã 2–3 chữ cái đã đối soát tại quầy, ví dụ US.</p>
        </template>
      </template>
      <template v-if="store.challenge">
        <p class="hint-text">Mã gồm 6 chữ số, có hiệu lực 5 phút và tối đa 5 lần nhập sai. Có thể yêu cầu mã mới sau 60 giây.</p>
        <label for="recovery-code">Mã OTP từ SMS</label>
        <input id="recovery-code" ref="codeInput" v-model="code" name="code" type="text" inputmode="numeric" maxlength="6"
          autocomplete="one-time-code" :disabled="busy" :aria-invalid="invalidField === 'code'" aria-describedby="recovery-error" class="input-otp" placeholder="••••••" />
        <label for="recovery-password">Mật khẩu mới</label>
        <input id="recovery-password" ref="passwordInput" v-model="password" name="new-password" type="password"
          autocomplete="new-password" maxlength="72" :disabled="busy" :aria-invalid="invalidField === 'password'" aria-describedby="password-help recovery-error" placeholder="Tối thiểu 8 ký tự, gồm hoa, thường và số" />
        <p id="password-help" class="hint-text">Từ 8 ký tự, gồm chữ hoa, chữ thường và chữ số; tối đa 72 byte UTF-8.</p>
        <label for="recovery-confirm">Nhập lại mật khẩu mới</label>
        <input id="recovery-confirm" ref="confirmInput" v-model="confirmPassword" name="confirm-password" type="password"
          autocomplete="new-password" maxlength="72" :disabled="busy" :aria-invalid="invalidField === 'confirm'" aria-describedby="recovery-error" placeholder="Nhập lại mật khẩu mới" />
      </template>
      <p v-if="error" id="recovery-error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="busy" class="btn-primary">{{ busy ? 'Đang xử lý…' : store.challenge ? 'Đặt mật khẩu mới' : 'Nhận mã qua SMS' }}</button>
      <div v-if="store.challenge" class="actions">
        <button type="button" :disabled="busy" @click="requestCode">Gửi lại mã</button>
        <button type="button" :disabled="busy" @click="changeIdentity">Nhập thông tin khác</button>
      </div>
    </form>
    <footer class="recovery-nav">
      <RouterLink to="/login">Quay lại đăng nhập</RouterLink>
    </footer>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { passwordRecoveryService } from '@/services/passwordRecovery';
import { usePasswordRecoveryStore } from '@/stores/passwordRecovery';

const store = usePasswordRecoveryStore();
const accountType = ref<'patient' | 'staff'>('patient'), documentType = ref<1 | 2>(1), nationalityCode = ref('');
const countryInput = ref<HTMLInputElement | null>(null);
const phone = ref(''), citizenId = ref(''), code = ref(''), password = ref(''), confirmPassword = ref('');
const error = ref(''), message = ref(''), invalidField = ref(''), busy = ref(false), done = ref(false);
const phoneInput = ref<HTMLInputElement | null>(null), codeInput = ref<HTMLInputElement | null>(null);
const citizenIdInput = ref<HTMLInputElement | null>(null);
const passwordInput = ref<HTMLInputElement | null>(null), confirmInput = ref<HTMLInputElement | null>(null);
let active = true, requestVersion = 0, resendAt = 0;

function reject(field: string, text: string) {
  invalidField.value = field; error.value = text;
  ({ phone: phoneInput, citizenId: citizenIdInput, country: countryInput, code: codeInput, password: passwordInput, confirm: confirmInput }[field])?.value?.focus();
}
function identityArguments(): [string, 1 | 2 | undefined, string | undefined] {
  return accountType.value === 'patient'
    ? [citizenId.value, documentType.value, documentType.value === 2 ? nationalityCode.value : undefined]
    : ['', undefined, undefined];
}
function changeDocument() {
  citizenId.value = ''; nationalityCode.value = ''; error.value = ''; invalidField.value = '';
}
function clearSecrets() { code.value = ''; password.value = ''; confirmPassword.value = ''; }
async function requestCode() {
  if (busy.value || !active || done.value) return;
  error.value = ''; invalidField.value = '';
  if (!/^0[0-9]{9}$/.test(phone.value)) return reject('phone', 'Nhập số điện thoại gồm 10 chữ số, bắt đầu bằng 0.');
  if (accountType.value === 'patient') {
    citizenId.value = citizenId.value.trim().toUpperCase();
    nationalityCode.value = nationalityCode.value.trim().toUpperCase();
    if (documentType.value === 1 && !/^[0-9]{12}$/.test(citizenId.value)) return reject('citizenId', 'CCCD phải gồm đúng 12 chữ số.');
    if (documentType.value === 2 && !/^[A-Z0-9]{6,12}$/.test(citizenId.value)) return reject('citizenId', 'Hộ chiếu phải gồm 6–12 chữ cái hoặc chữ số.');
    if (documentType.value === 2 && !/^[A-Z]{2,3}$/.test(nationalityCode.value)) return reject('country', 'Nhập mã quốc gia cấp hộ chiếu gồm 2–3 chữ cái.');
  }
  if (Date.now() < resendAt) { error.value = 'Vui lòng chờ đủ 60 giây trước khi yêu cầu mã mới.'; return; }
  busy.value = true;
  const version = ++requestVersion;
  try {
    const response = await passwordRecoveryService.request(phone.value, ...identityArguments());
    if (!active || version !== requestVersion) return;
    store.set(response); clearSecrets(); resendAt = Date.now() + response.resendAfterSeconds * 1000;
    message.value = 'Nếu thông tin khớp tài khoản đang hoạt động, bạn sẽ nhận được mã SMS. Nếu chưa nhận được, kiểm tra giấy tờ và số điện thoại đăng ký hoặc liên hệ phòng khám.';
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
    await passwordRecoveryService.reset(store.challenge.challengeId, phone.value, code.value, password.value, ...identityArguments());
    if (!active || version !== requestVersion) return;
    store.clear(); phone.value = ''; citizenId.value = ''; nationalityCode.value = ''; clearSecrets(); done.value = true;
    message.value = 'Đã đặt mật khẩu mới. Vui lòng đăng nhập lại bằng mật khẩu mới.';
  } catch (err: any) {
    if (active && version === requestVersion) { clearSecrets(); error.value = err.response?.data?.message || 'Chưa xác định được kết quả. Hãy thử đăng nhập bằng mật khẩu mới hoặc yêu cầu mã khác.'; }
  } finally { if (active && version === requestVersion) busy.value = false; }
}
function changeIdentity() {
  if (busy.value) return;
  ++requestVersion; store.clear(); clearSecrets(); phone.value = ''; citizenId.value = ''; nationalityCode.value = ''; error.value = ''; message.value = ''; invalidField.value = '';
  // Keep the local resend deadline; changing the form must not bypass the server cooldown.
  nextTick(() => phoneInput.value?.focus());
}
onMounted(() => { store.clear(); phoneInput.value?.focus(); });
onUnmounted(() => { active = false; ++requestVersion; store.clear(); clearSecrets(); phone.value = ''; citizenId.value = ''; nationalityCode.value = ''; message.value = ''; });
</script>

<style scoped>
.recovery-card {
  max-width: 480px;
  margin: 2.5rem auto;
  padding: 2rem 2.25rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 20px;
  box-shadow: 0 10px 25px -5px rgba(15, 45, 89, 0.08), 0 8px 10px -6px rgba(15, 45, 89, 0.04);
  color: #1e293b;
  font-family: inherit;
}

.recovery-brand {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.25rem;
}

.brand-badge {
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
}

.brand-logo {
  display: block;
}

h1 {
  color: #0f2d59;
  font-size: 1.45rem;
  font-weight: 700;
  text-align: center;
  margin: 0 0 0.5rem;
}

.recovery-desc {
  color: #64748b;
  font-size: 0.88rem;
  line-height: 1.5;
  text-align: center;
  margin: 0 0 1.25rem;
  overflow-wrap: anywhere;
}

/* Step Progress */
.step-progress {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 1.25rem 0 1.5rem;
  padding: 0.75rem 1.5rem;
  background: #f8fafc;
  border: 1px solid #f1f5f9;
  border-radius: 12px;
}

.step-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
}

.step-num {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 50%;
  background: #e2e8f0;
  color: #64748b;
  font-size: 0.78rem;
  font-weight: 700;
  transition: all 0.2s;
}

.step-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: #94a3b8;
}

.step-item--active .step-num {
  background: #0284c7;
  color: #ffffff;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.2);
}

.step-item--active .step-label {
  color: #0284c7;
}

.step-item--done .step-num {
  background: #0f2d59;
  color: #ffffff;
}

.step-item--done .step-label {
  color: #0f2d59;
}

.step-line {
  flex: 1;
  height: 2px;
  background: #e2e8f0;
  margin: 0 0.75rem -0.9rem;
  transition: background 0.2s;
}

.step-line--filled {
  background: #0284c7;
}

label {
  display: block;
  font-weight: 600;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.88rem;
}

input, select {
  box-sizing: border-box;
  width: 100%;
  min-height: 44px;
  padding: 0.65rem 0.95rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible,
select:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

.input-otp {
  font-size: 1.35rem !important;
  letter-spacing: 0.35em;
  text-align: center;
  font-weight: 700;
}

.hint-text {
  color: #64748b;
  font-size: 0.82rem;
  line-height: 1.4;
  margin: 0.25rem 0 0.5rem;
}

button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 46px;
  padding: 0.75rem 1rem;
  font-size: 0.95rem;
  font-weight: 700;
  border-radius: 10px;
  border: none;
  background: linear-gradient(135deg, #0284c7, #0f2d59);
  color: #ffffff;
  cursor: pointer;
  transition: opacity 0.15s, transform 0.15s;
  box-shadow: 0 4px 12px rgba(2, 132, 199, 0.2);
}

.btn-primary {
  width: 100%;
  margin: 1rem 0 0.5rem;
}

button:hover:not(:disabled) {
  opacity: 0.94;
  transform: translateY(-1px);
}

button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
  transform: none;
}

.actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin: 0.75rem 0;
}

.actions button {
  width: 100%;
  background: transparent;
  color: #0f2d59;
  border: 1px solid #cbd5e1;
  box-shadow: none;
}

.actions button:hover:not(:disabled) {
  background: #f1f5f9;
}

[role="alert"] {
  color: #991b1b;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-weight: 500;
  font-size: 0.88rem;
  margin: 0.75rem 0;
}

.notice {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-weight: 500;
  font-size: 0.88rem;
  margin: 0.75rem 0;
}

.recovery-nav {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid #f1f5f9;
  text-align: center;
  font-size: 0.88rem;
}

.recovery-nav a {
  color: #0284c7;
  text-decoration: none;
  font-weight: 600;
}

.recovery-nav a:hover {
  text-decoration: underline;
}

@media (max-width: 440px) {
  .recovery-card {
    margin: 1rem 0.75rem;
    padding: 1.5rem 1.15rem;
    border-radius: 16px;
  }
}
</style>
