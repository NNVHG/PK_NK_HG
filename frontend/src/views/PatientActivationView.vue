<template>
  <main class="activation">
    <h1>Kích hoạt tài khoản bệnh nhân</h1>
    <p>Dành cho tài khoản mới được tạo sau khi Lễ tân xác nhận lịch. Tài khoản đã có tiếp tục đăng nhập như trước.</p>
    <p role="status" aria-live="polite">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <form v-if="!challenge && !grant && !done && !uncertain" @submit.prevent="requestCode" novalidate>
      <label>Số điện thoại đăng ký<input v-model="phone" type="tel" inputmode="numeric" autocomplete="username" maxlength="10" :disabled="busy" /></label>
      <label>Mật khẩu hiện tại<input v-model="password" type="password" autocomplete="current-password" maxlength="72" :disabled="busy" /></label>
      <p>Nếu chưa đổi mật khẩu, dùng SĐT làm mật khẩu mặc định.</p>
      <button type="submit" :disabled="busy">Nhận mã kích hoạt</button>
    </form>
    <form v-if="challenge && !grant && !uncertain" @submit.prevent="verifyCode" novalidate>
      <label>Mã OTP kích hoạt<input v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy" /></label>
      <p>Mã có 6 số, hiệu lực 5 phút, tối đa 5 lần sai. Xin mã mới cách ít nhất 60 giây.</p>
      <button type="submit" :disabled="busy">Xác minh OTP</button>
    </form>
    <form v-if="grant && !uncertain" @submit.prevent="complete" novalidate>
      <h2>Chọn mật khẩu đăng nhập</h2>
      <p>OTP đã được xác minh. Hoàn tất lựa chọn trong 5 phút.</p>
      <fieldset :disabled="busy">
        <legend>Lựa chọn mật khẩu</legend>
        <label><input v-model="choice" type="radio" value="new" name="password-choice" /> Đặt mật khẩu mới</label>
        <label><input v-model="choice" type="radio" value="default" name="password-choice" /> Bỏ qua, giữ mật khẩu hiện tại (mặc định là SĐT nếu chưa đổi)</label>
        <template v-if="choice === 'new'">
          <label>Mật khẩu mới<input v-model="newPassword" type="password" autocomplete="new-password" maxlength="72" /></label>
          <label>Nhập lại mật khẩu mới<input v-model="repeatPassword" type="password" autocomplete="new-password" maxlength="72" /></label>
          <p>Ít nhất 8 ký tự, có chữ hoa, chữ thường và số.</p>
        </template>
        <label><input v-model="confirmed" type="checkbox" /> Tôi xác nhận lựa chọn mật khẩu và hoàn tất kích hoạt</label>
      </fieldset>
      <button type="submit" :disabled="busy">Hoàn tất kích hoạt</button>
    </form>
    <p v-if="uncertain">Kết quả chưa rõ. Thử đăng nhập trước; nếu tài khoản vẫn cần kích hoạt, xin mã mới. Không gửi lặp lại lựa chọn mật khẩu.</p>
    <button v-if="!done" type="button" :disabled="busy" @click="restart">Nhập lại thông tin / xin mã mới</button>
    <p><RouterLink to="/login">Đăng nhập</RouterLink></p>
    <p><RouterLink to="/book-appointment">Gửi yêu cầu đặt lịch</RouterLink></p>
  </main>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { patientActivationService, type ActivationChallenge, type ActivationGrant } from '@/services/patientActivation';

const auth = useAuthStore();
const phone = ref(''), password = ref(''), code = ref(''), newPassword = ref(''), repeatPassword = ref('');
const choice = ref('new'), confirmed = ref(false), busy = ref(false), done = ref(false), uncertain = ref(false);
const challenge = ref<ActivationChallenge | null>(null), grant = ref<ActivationGrant | null>(null), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}`;
const alive = (t: number, owner: string) => !disposed && t === generation && owner === identity();
function clear() { phone.value = ''; password.value = ''; code.value = ''; newPassword.value = ''; repeatPassword.value = ''; challenge.value = null; grant.value = null; confirmed.value = false; choice.value = 'new'; }
function restart() { if (disposed || busy.value) return; generation++; clear(); done.value = false; uncertain.value = false; error.value = ''; message.value = ''; }
function failure(cause: unknown, fallback: string) { const data = (cause as { response?: { data?: { message?: unknown } } })?.response?.data; return typeof data?.message === 'string' ? data.message : fallback; }
async function requestCode() {
  if (disposed || busy.value || challenge.value || grant.value || done.value || uncertain.value) return;
  if (!/^0[0-9]{9}$/.test(phone.value) || phone.value.length !== 10 || password.value.length < 6 || password.value.includes('\0') || new TextEncoder().encode(password.value).length > 72) { error.value = 'Kiểm tra SĐT 10 số và mật khẩu hiện tại.'; return; }
  const t = ++generation, owner = identity(); busy.value = true; error.value = ''; message.value = '';
  try {
    const data = await patientActivationService.request(phone.value, password.value);
    if (!alive(t, owner)) return;
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(data.challengeId) || data.expiresInSeconds !== 300 || data.resendAfterSeconds !== 60) throw Error();
    challenge.value = data; phone.value = ''; message.value = 'Nếu thông tin đúng và tài khoản đủ điều kiện, mã kích hoạt sẽ được gửi tới SĐT đăng ký.';
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
    if (!/^[A-F0-9]{64}$/.test(data.completionToken) || data.expiresInSeconds !== 300) throw Error();
    grant.value = data; challenge.value = null; message.value = 'Đã xác minh OTP. Chọn đổi hoặc giữ mật khẩu để hoàn tất.';
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
.activation { max-width: 38rem; margin: 2rem auto; padding: 1rem; } label { display: block; margin-block: 1rem; }
input:not([type=checkbox]):not([type=radio]) { display: block; width: 100%; box-sizing: border-box; }
input, button { font: inherit; padding: .6rem; } button { margin: .5rem .5rem .5rem 0; } [role=alert] { color: #9f1239; }
:focus-visible { outline: 3px solid #2563eb; outline-offset: 2px; }
</style>
