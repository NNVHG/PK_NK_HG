<template>
  <section class="phone-change" aria-labelledby="phone-change-title">
    <h2 id="phone-change-title">Đổi số điện thoại cá nhân</h2>
    <p>OTP được gửi tới số mới. Sau khi xác nhận, số liên hệ của tài khoản và hồ sơ cá nhân được cập nhật nếu còn cùng số cũ. Mật khẩu được giữ nguyên; các phiên cũ sẽ bị thu hồi.</p>
    <PatientCounterPhoneSuggestion :disabled="busy || uncertain" @select="acceptCounterPhone" />
    <form @submit.prevent="loadPreview">
      <label for="new-phone">SĐT mới</label>
      <input id="new-phone" v-model="newPhone" type="tel" inputmode="numeric" autocomplete="tel" maxlength="10" :disabled="busy || uncertain" required />
      <button type="submit" :disabled="busy || uncertain">Xem thông tin thay đổi</button>
    </form>
    <div v-if="preview">
      <h3>Xác nhận thông tin</h3>
      <p>{{ preview.patientName }} · hồ sơ #{{ preview.patientId }}</p>
      <p>Số liên hệ tài khoản: <strong>{{ preview.currentPhone }}</strong> → <strong>{{ preview.newPhone }}</strong>.</p>
      <p>{{ preview.contactWillUpdate ? 'Số trên hồ sơ cá nhân cũng sẽ được cập nhật.' : 'Số liên hệ riêng trên hồ sơ được giữ nguyên.' }}</p>
      <label><input v-model="scopeConfirmed" type="checkbox" :disabled="busy || !!pending || uncertain" /> Tôi xác nhận số mới thuộc quyền sử dụng của mình.</label>
      <button type="button" :disabled="busy || !scopeConfirmed || !!pending || uncertain" @click="requestOtp">Gửi OTP tới số mới</button>
    </div>
    <form v-if="pending && !uncertain" @submit.prevent="confirm">
      <p>OTP có hạn 5 phút kể từ lúc gửi. Nếu cần đổi số, hãy xem lại thông tin và xin mã mới.</p>
      <label for="phone-code">OTP gửi tới số mới</label>
      <input id="phone-code" v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy" required />
      <label><input v-model="finalConfirmed" type="checkbox" :disabled="busy" /> Tôi đồng ý đổi số điện thoại cá nhân và đăng nhập lại.</label>
      <button type="submit" :disabled="busy || !finalConfirmed">Xác minh và đổi số</button>
    </form>
    <p role="status">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <button v-if="uncertain" type="button" @click="signInAgain">Đăng nhập lại để kiểm tra kết quả</button>
  </section>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import PatientCounterPhoneSuggestion from './PatientCounterPhoneSuggestion.vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { patientPhoneChange, isPatientPhonePreview, type PatientPhonePreview, type PhoneChangePending } from '@/services/patientPhoneChange';
const auth = useAuthStore(), router = useRouter();
const owner = auth.user?.userId;
const newPhone = ref(''), code = ref(''), message = ref(''), error = ref('');
const preview = ref<PatientPhonePreview | null>(null), pending = ref<PhoneChangePending | null>(null);
const busy = ref(false), scopeConfirmed = ref(false), finalConfirmed = ref(false), uncertain = ref(false);
let active = true, revision = 0;
const current = (version: number) => active && revision === version && auth.user?.userId === owner && auth.role === 'PATIENT';
function clearReview() { revision++; preview.value = null; pending.value = null; code.value = ''; scopeConfirmed.value = false; finalConfirmed.value = false; message.value = ''; }
watch(newPhone, () => { clearReview(); error.value = ''; });
watch(code, () => { finalConfirmed.value = false; });
watch(() => [auth.user?.userId, auth.role], () => { active = false; clearReview(); newPhone.value = ''; error.value = ''; });
onUnmounted(() => { active = false; clearReview(); newPhone.value = ''; error.value = ''; });
function problem(e: any, fallback: string) { return e.response?.data?.message || fallback; }
async function acceptCounterPhone(phone: string) {
  if (busy.value || uncertain.value || !current(revision)) return;
  newPhone.value = phone;
  // Watch flushes before rendering; the normal preview path still requires fresh scope consent.
  await Promise.resolve();
  await loadPreview();
}
async function loadPreview() {
  if (busy.value || uncertain.value || !current(revision)) return;
  clearReview(); error.value = '';
  if (newPhone.value.length !== 10 || !/^0[0-9]{9}$/.test(newPhone.value) || newPhone.value === auth.user?.phone) { error.value = 'SĐT mới cần đúng 10 chữ số, bắt đầu bằng 0 và khác số hiện tại.'; return; }
  busy.value = true;
  const version = revision, requested = newPhone.value;
  try {
    const data = await patientPhoneChange.preview(requested);
    if (!current(version) || newPhone.value !== requested) return;
    if (!isPatientPhonePreview(data, requested, auth.user?.phone)) throw new Error('Invalid preview');
    preview.value = data;
  } catch (e: any) { if (current(version)) error.value = problem(e, 'Không tải được phạm vi thay đổi. Vui lòng xem lại trước khi tiếp tục.'); }
  finally { if (active) busy.value = false; }
}
async function requestOtp() {
  if (busy.value || uncertain.value || !current(revision) || !preview.value || !scopeConfirmed.value || pending.value) return;
  busy.value = true; error.value = ''; message.value = '';
  const version = revision, reviewed = preview.value;
  try {
    const data = await patientPhoneChange.request(reviewed.newPhone, reviewed.reviewStamp);
    if (!current(version)) return;
    if (!data?.challengeId || data.newPhone !== reviewed.newPhone || !Number.isFinite(Date.parse(data.expiresAtUtc))) throw new Error('Invalid challenge');
    pending.value = data;
    message.value = 'Đã gửi OTP. Số hiện tại và phiên đăng nhập vẫn được giữ nguyên trong lúc chờ xác nhận.';
  } catch (e: any) {
    if (!current(version)) return;
    if (e.response?.status === 409 || e.response?.status === 403) clearReview();
    scopeConfirmed.value = false;
    error.value = problem(e, 'Không xác định được lần gửi OTP. Xem lại danh sách và chờ ít nhất 60 giây trước khi xin mã mới.');
  } finally { if (active) busy.value = false; }
}
async function confirm() {
  if (busy.value || uncertain.value || !current(revision) || !preview.value || !pending.value || !scopeConfirmed.value || !finalConfirmed.value) return;
  error.value = '';
  if (code.value.length !== 6 || !/^[0-9]{6}$/.test(code.value)) { error.value = 'OTP cần đúng 6 chữ số.'; return; }
  if (Date.parse(pending.value.expiresAtUtc) <= Date.now()) { clearReview(); error.value = 'OTP đã hết hạn. Vui lòng xem lại danh sách và xin mã mới.'; return; }
  busy.value = true;
  const version = revision;
  try {
    const data = await patientPhoneChange.confirm(pending.value.challengeId, code.value, preview.value.reviewStamp);
    if (!current(version)) return;
    if (data?.completed !== true || data.patientContactUpdated !== preview.value.contactWillUpdate) throw new Error('Unknown result');
    clearReview(); newPhone.value = '';
    auth.clearSession();
    await router.replace('/login?phoneChanged=1');
  } catch (e: any) {
    if (!current(version)) return;
    code.value = ''; finalConfirmed.value = false;
    if (e.response?.status === 409 || e.response?.status === 403) {
      clearReview(); error.value = problem(e, 'Phạm vi đã thay đổi. Vui lòng xem lại thông tin.');
    } else if (e.response?.status === 400 || e.response?.status === 429) {
      error.value = problem(e, 'Mã không hợp lệ hoặc đã hết hạn. Xin mã mới khi cần.');
    } else {
      uncertain.value = true; pending.value = null;
      error.value = 'Kết quả chưa rõ. Đăng nhập lại để kiểm tra số hiện tại trước khi thực hiện yêu cầu khác.';
    }
  } finally { if (active) busy.value = false; }
}
async function signInAgain() { clearReview(); newPhone.value = ''; auth.clearSession(); await router.replace('/login'); }
</script>
<style scoped>
.phone-change { padding: 1.5rem; margin: 1rem 0; border: 1px solid #cbd5e1; border-radius: .75rem; background: white; }
form { display: grid; gap: .8rem; margin: 1rem 0; max-width: 48rem; } input:not([type=checkbox]), button { padding: .6rem; font: inherit; }
.table-scroll { overflow-x: auto; } table { border-collapse: collapse; width: 100%; margin: 1rem 0; } th, td { padding: .6rem; border-bottom: 1px solid #cbd5e1; text-align: left; }
label { display: block; margin: .6rem 0; } [role=alert] { color: #9f1239; }
</style>
