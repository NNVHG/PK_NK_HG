<template>
  <main class="activation">
    <div class="activation-brand">
      <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="44" height="44" />
      <span class="brand-badge">Nha Khoa Hoàng Gia</span>
    </div>
    <h1>Xác minh số điện thoại</h1>
    <p>Dành cho bệnh nhân mới sau khi Lễ tân đối soát tại quầy, nhân viên mới do Admin tạo và bệnh nhân cần xác minh lại sau khi đổi SĐT. Tài khoản cũ tiếp tục đăng nhập theo trạng thái xác minh hiện tại.</p>

    <!-- Step Indicator -->
    <nav class="step-progress" aria-label="Tiến trình kích hoạt">
      <div class="step-item" :class="{ 'step-item--active': !challenge && !grant && !done && !uncertain, 'step-item--done': challenge || grant || done }">
        <span class="step-num">1</span>
        <span class="step-label">Thông tin</span>
      </div>
      <div class="step-line" :class="{ 'step-line--filled': challenge || grant || done }"></div>
      <div class="step-item" :class="{ 'step-item--active': challenge && !grant && !uncertain, 'step-item--done': grant || done }">
        <span class="step-num">2</span>
        <span class="step-label">Mã OTP</span>
      </div>
      <div class="step-line" :class="{ 'step-line--filled': grant || done }"></div>
      <div class="step-item" :class="{ 'step-item--active': grant && !uncertain, 'step-item--done': done }">
        <span class="step-num">3</span>
        <span class="step-label">Mật khẩu</span>
      </div>
    </nav>

    <p role="status" aria-live="polite" v-if="message">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <form v-if="!challenge && !grant && !done && !uncertain" @submit.prevent="requestCode" novalidate>
      <div class="mode-selector">
        <label :class="{ 'mode-label--active': loginMode === 'cccd' }"><input type="radio" v-model="loginMode" value="cccd" :disabled="busy" /> CCCD (bệnh nhân) / SĐT (nhân viên)</label>
        <label :class="{ 'mode-label--active': loginMode === 'passport' }"><input type="radio" v-model="loginMode" value="passport" :disabled="busy" /> Hộ chiếu (bệnh nhân nước ngoài)</label>
      </div>
      <div v-if="loginMode === 'passport'" class="form-subgroup">
        <label>Quốc gia cấp hộ chiếu
          <input v-model="nationalityCode" type="text" maxlength="3" placeholder="VN, US, JP..." :disabled="busy" />
        </label>
      </div>
      <label>{{ loginMode === 'passport' ? 'Số hộ chiếu' : 'CCCD bệnh nhân hoặc SĐT nhân viên' }}
        <input v-model="phone" type="text" :inputmode="loginMode === 'passport' ? 'text' : 'numeric'" autocomplete="username" :maxlength="loginMode === 'passport' ? 12 : 12" :disabled="busy" placeholder="Nhập số CCCD 12 số hoặc SĐT" />
      </label>
      <label>Mật khẩu hiện tại<input v-model="password" type="password" autocomplete="current-password" maxlength="72" :disabled="busy" placeholder="Nhập mật khẩu hiện tại" /></label>
      <p class="hint-text">Nếu chưa đổi mật khẩu, dùng SĐT làm mật khẩu mặc định.</p>
      <button type="submit" :disabled="busy">Nhận mã kích hoạt</button>
    </form>
    <form v-if="challenge && !grant && !uncertain" @submit.prevent="verifyCode" novalidate>
      <label>Mã OTP kích hoạt<input v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy" class="input-otp" placeholder="••••••" /></label>
      <p class="hint-text">Mã có 6 số, hiệu lực 5 phút, tối đa 5 lần sai. Xin mã mới cách ít nhất 60 giây.</p>
      <div v-if="challenge.developmentCode" class="demo-code">
        <span>Mã hỗ trợ demo (Development): <strong>{{ challenge.developmentCode }}</strong></span>
        <button type="button" :disabled="busy" @click="code = challenge.developmentCode || ''">Dùng mã này</button>
      </div>
      <button type="submit" :disabled="busy">Xác minh OTP</button>
    </form>
    <form v-if="grant && !uncertain" @submit.prevent="complete" novalidate>
      <h2>{{ grant.mustChangePassword ? 'Đổi mật khẩu bắt buộc' : 'Chọn mật khẩu đăng nhập' }}</h2>
      <p class="hint-text">OTP đã được xác minh. Hoàn tất lựa chọn trong 5 phút.</p>
      <fieldset :disabled="busy">
        <legend>Lựa chọn mật khẩu</legend>
        <label><input v-model="choice" type="radio" value="new" name="password-choice" /> Đặt mật khẩu mới</label>
        <label v-if="!grant.mustChangePassword"><input v-model="choice" type="radio" value="default" name="password-choice" /> Bỏ qua, giữ mật khẩu hiện tại (mặc định là SĐT nếu chưa đổi)</label>
        <template v-if="choice === 'new'">
          <label>Mật khẩu mới<input v-model="newPassword" type="password" autocomplete="new-password" maxlength="72" placeholder="Tối thiểu 8 ký tự, gồm hoa, thường và số" /></label>
          <label>Nhập lại mật khẩu mới<input v-model="repeatPassword" type="password" autocomplete="new-password" maxlength="72" placeholder="Nhập lại mật khẩu mới" /></label>
          <p class="hint-text">Ít nhất 8 ký tự, có chữ hoa, chữ thường và số.</p>
        </template>
        <label><input v-model="confirmed" type="checkbox" /> Tôi xác nhận lựa chọn mật khẩu và hoàn tất kích hoạt</label>
      </fieldset>
      <button type="submit" :disabled="busy">Hoàn tất kích hoạt</button>
    </form>
    <p v-if="uncertain" class="hint-text">Kết quả chưa rõ. Thử đăng nhập trước; nếu tài khoản vẫn cần kích hoạt, xin mã mới. Không gửi lặp lại lựa chọn mật khẩu.</p>
    <button v-if="!done" type="button" :disabled="busy" class="btn-secondary" @click="restart">Nhập lại thông tin / xin mã mới</button>
    <footer class="activation-nav">
      <RouterLink to="/login">Đăng nhập</RouterLink>
      <span>·</span>
      <RouterLink to="/book-appointment">Gửi yêu cầu đặt lịch</RouterLink>
    </footer>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { patientActivationService, takeActivationPrefill, type ActivationChallenge, type ActivationGrant } from '@/services/patientActivation';

const auth = useAuthStore();
const loginMode = ref<'cccd' | 'passport'>('cccd');
const nationalityCode = ref('');
const phone = ref(''), password = ref(''), code = ref(''), newPassword = ref(''), repeatPassword = ref('');
const choice = ref('new'), confirmed = ref(false), busy = ref(false), done = ref(false), uncertain = ref(false);
const challenge = ref<ActivationChallenge | null>(null), grant = ref<ActivationGrant | null>(null), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}`;
const alive = (t: number, owner: string) => !disposed && t === generation && owner === identity();

onMounted(() => {
  const prefill = takeActivationPrefill();
  if (!prefill) return;
  phone.value = prefill.identifier;
  loginMode.value = prefill.documentType;
  nationalityCode.value = prefill.nationalityCode;
});

function clear() { phone.value = ''; password.value = ''; nationalityCode.value = ''; code.value = ''; newPassword.value = ''; repeatPassword.value = ''; challenge.value = null; grant.value = null; confirmed.value = false; choice.value = 'new'; }
function restart() { if (disposed || busy.value) return; generation++; clear(); done.value = false; uncertain.value = false; error.value = ''; message.value = ''; }
function failure(cause: unknown, fallback: string) { const data = (cause as { response?: { data?: { message?: unknown } } })?.response?.data; return typeof data?.message === 'string' ? data.message : fallback; }
async function requestCode() {
  if (disposed || busy.value || challenge.value || grant.value || done.value || uncertain.value) return;

  const isPassport = loginMode.value === 'passport';
  const val = phone.value.trim();
  if (isPassport) {
    if (!/^[A-Za-z0-9]{6,12}$/.test(val) || !nationalityCode.value || !/^[A-Za-z]{2,3}$/.test(nationalityCode.value.trim())) {
      error.value = 'Nhập số hộ chiếu hợp lệ (6–12 ký tự) và mã quốc gia cấp (2–3 chữ cái).';
      return;
    }
  } else {
    if (!/^(0[0-9]{9}|[0-9]{12})$/.test(val)) {
      error.value = 'Nhập CCCD 12 số của bệnh nhân hoặc SĐT 10 số của nhân viên.';
      return;
    }
  }

  if (password.value.length < 6 || password.value.includes('\0') || new TextEncoder().encode(password.value).length > 72) {
    error.value = 'Mật khẩu hiện tại phải hợp lệ.';
    return;
  }

  const t = ++generation, owner = identity(); busy.value = true; error.value = ''; message.value = '';
  try {
    const docType = isPassport ? 2 : (val.length === 12 ? 1 : null);
    const country = isPassport ? nationalityCode.value.trim().toUpperCase() : null;
    const data = await patientActivationService.request(val, password.value, docType, country);
    if (!alive(t, owner)) return;
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(data.challengeId) || data.expiresInSeconds !== 300 || data.resendAfterSeconds !== 60 || data.developmentCode != null && !/^[0-9]{6}$/.test(data.developmentCode)) throw Error();
    challenge.value = data; phone.value = ''; message.value = data.developmentCode ? 'Mã OTP mô phỏng chỉ hiển thị trong Development.' : 'Nếu thông tin đúng và tài khoản đủ điều kiện, mã kích hoạt sẽ được gửi tới SĐT đăng ký.';
  } catch (cause) { if (alive(t, owner)) error.value = failure(cause, 'Chưa yêu cầu được mã kích hoạt. Thử lại sau hoặc liên hệ phòng khám.'); }
  finally { if (alive(t, owner)) { busy.value = false; password.value = ''; } }
}
async function verifyCode() {
  if (disposed || busy.value || !challenge.value || grant.value || uncertain.value || done.value) return;
  if (code.value.length !== 6 || !/^[0-9]{6}$/.test(code.value)) { error.value = 'Nhập mã OTP gồm đúng 6 số.'; return; }
  const t = generation, owner = identity(); busy.value = true; error.value = '';
  try {
    const data = await patientActivationService.verify(challenge.value.challengeId, code.value);
    if (!alive(t, owner)) return;
    if (!/^[A-F0-9]{64}$/.test(data.completionToken) || data.expiresInSeconds !== 300 || typeof data.mustChangePassword !== 'boolean') throw Error();
    grant.value = data; challenge.value = null; choice.value = data.mustChangePassword ? 'new' : 'default'; message.value = data.mustChangePassword ? 'Đã xác minh OTP. Bệnh nhân cần đổi mật khẩu trước khi đăng nhập.' : 'Đã xác minh OTP. Chọn đổi hoặc giữ mật khẩu để hoàn tất.';
  } catch (cause) { if (alive(t, owner)) error.value = failure(cause, 'Chưa xác minh được mã. Nếu mã đã dùng hoặc hết hạn, xin mã mới.'); }
  finally { if (alive(t, owner)) { busy.value = false; code.value = ''; } }
}
async function complete() {
  if (disposed || busy.value || !grant.value || uncertain.value || done.value) return;
  const keep = choice.value === 'default';
  if (!confirmed.value || !['new', 'default'].includes(choice.value) || !keep && (newPassword.value.length < 8 || !/[A-Z]/.test(newPassword.value) || !/[a-z]/.test(newPassword.value) || !/[0-9]/.test(newPassword.value) || newPassword.value.includes('\0') || new TextEncoder().encode(newPassword.value).length > 72 || newPassword.value !== repeatPassword.value)) { error.value = 'Xác nhận lựa chọn; mật khẩu mới phải đủ 8 ký tự, hoa/thường/số và nhập lại trùng khớp.'; return; }
  const t = generation, owner = identity(); busy.value = true; error.value = ''; confirmed.value = false;
  try {
    const data = await patientActivationService.complete(grant.value.completionToken, keep, keep ? null : newPassword.value);
    if (!alive(t, owner)) return;
    if (data.completed !== true) throw Error();
    clear(); done.value = true; message.value = 'Kích hoạt thành công. Đăng nhập bằng mật khẩu đã chọn; nếu bỏ qua thì dùng mật khẩu hiện tại.';
  } catch { if (alive(t, owner)) { clear(); uncertain.value = true; message.value = ''; error.value = 'Chưa xác minh được kết quả hoàn tất kích hoạt.'; } }
  finally { if (alive(t, owner)) { busy.value = false; newPassword.value = ''; repeatPassword.value = ''; } }
}
watch(choice, () => { newPassword.value = ''; repeatPassword.value = ''; confirmed.value = false; });
watch(identity, () => { generation++; busy.value = false; clear(); done.value = false; uncertain.value = false; message.value = ''; error.value = ''; });
onUnmounted(() => { disposed = true; generation++; clear(); message.value = ''; error.value = ''; });
</script>
<style scoped>
.activation {
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

.activation-brand {
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

h2 {
  color: #0f2d59;
  font-size: 1.15rem;
  font-weight: 700;
  margin: 1rem 0 0.5rem;
}

p {
  color: #475569;
  font-size: 0.925rem;
  line-height: 1.5;
  margin: 0.5rem 0;
}

/* Step Progress */
.step-progress {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 1.25rem 0 1.5rem;
  padding: 0.75rem 1rem;
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
  font-size: 0.72rem;
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
  margin: 0 0.5rem -0.9rem;
  transition: background 0.2s;
}

.step-line--filled {
  background: #0284c7;
}

.mode-selector {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  background: #f1f5f9;
  padding: 0.375rem;
  border-radius: 8px;
}

.mode-selector label {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  margin: 0;
  padding: 0.5rem 0.25rem;
  font-size: 0.85rem;
  font-weight: 500;
  color: #334155;
  cursor: pointer;
  border-radius: 6px;
  text-align: center;
}

.form-subgroup {
  margin-bottom: 0.75rem;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

input:not([type="checkbox"]):not([type="radio"]) {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.65rem 0.95rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible {
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

input[type="checkbox"],
input[type="radio"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  vertical-align: middle;
}

fieldset {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 0.85rem 1rem;
  margin: 1rem 0;
}

legend {
  font-weight: 700;
  color: #0f2d59;
  padding: 0 0.5rem;
  font-size: 0.88rem;
}

button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
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
  margin: 1rem 0 0.5rem;
  box-shadow: 0 4px 12px rgba(2, 132, 199, 0.2);
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

.btn-secondary {
  background: transparent !important;
  color: #0f2d59 !important;
  border: 1px solid #cbd5e1 !important;
  box-shadow: none !important;
}

.btn-secondary:hover:not(:disabled) {
  background: #f1f5f9 !important;
}

.demo-code {
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  padding: 0.85rem;
  border-radius: 10px;
  color: #1e40af;
  margin: 0.75rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-size: 0.88rem;
}

.demo-code button {
  width: auto;
  align-self: flex-start;
  min-height: 32px;
  margin: 0.25rem 0 0;
  padding: 0.25rem 0.75rem;
  font-size: 0.82rem;
  background: #dbeafe;
  color: #1e40af;
  border: 1px solid #bfdbfe;
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

[role="status"] {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-weight: 500;
  font-size: 0.88rem;
  margin: 0.75rem 0;
}

.activation-nav {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid #f1f5f9;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.85rem;
}

.activation-nav a {
  color: #0284c7;
  text-decoration: none;
  font-weight: 600;
}

.activation-nav a:hover {
  text-decoration: underline;
}

@media (max-width: 480px) {
  .activation {
    margin: 1rem 0.75rem;
    padding: 1.5rem 1.15rem;
    border-radius: 16px;
  }
}
</style>
