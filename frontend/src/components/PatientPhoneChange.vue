<template>
  <section class="phone-change" aria-labelledby="phone-change-title">
    <h2 id="phone-change-title">Đổi số điện thoại cá nhân</h2>
    <p>Nhập mật khẩu hiện tại để lưu số mới và đăng xuất các phiên đang mở. Lần đăng nhập tiếp theo cần OTP gửi tới số mới. Mật khẩu được giữ nguyên; số liên hệ của trẻ được giám hộ không thay đổi.</p>
    <p>SĐT tài khoản: {{ auth.user?.phoneVerified === true ? 'Đã xác minh' : 'Chưa có xác nhận OTP' }}</p>
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
      <form @submit.prevent="confirm">
        <label for="phone-current-password">Mật khẩu hiện tại</label>
        <input id="phone-current-password" v-model="currentPassword" type="password" autocomplete="current-password" :disabled="busy || uncertain" required />
        <label><input v-model="scopeConfirmed" type="checkbox" :disabled="busy || uncertain" /> Tôi đã kiểm tra số mới, phạm vi cập nhật và đồng ý đăng nhập lại để xác minh OTP.</label>
        <button type="submit" :disabled="busy || !scopeConfirmed || !currentPassword || uncertain">Lưu số mới và đăng xuất</button>
      </form>
    </div>
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
import { patientPhoneChange, isPatientPhonePreview, type PatientPhonePreview } from '@/services/patientPhoneChange';
const auth = useAuthStore(), router = useRouter();
const owner = auth.user?.userId;
const newPhone = ref(''), currentPassword = ref(''), message = ref(''), error = ref('');
const preview = ref<PatientPhonePreview | null>(null);
const busy = ref(false), scopeConfirmed = ref(false), uncertain = ref(false);
let active = true, revision = 0;
const current = (version: number) => active && revision === version && auth.user?.userId === owner && auth.role === 'PATIENT';
function clearReview() { revision++; preview.value = null; currentPassword.value = ''; scopeConfirmed.value = false; message.value = ''; }
watch(newPhone, () => { clearReview(); error.value = ''; });
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
async function confirm() {
  if (busy.value || uncertain.value || !current(revision) || !preview.value || !scopeConfirmed.value) return;
  error.value = '';
  if (!currentPassword.value || currentPassword.value.includes('\0') || new TextEncoder().encode(currentPassword.value).length > 72) {
    error.value = 'Vui lòng nhập mật khẩu hiện tại hợp lệ.'; return;
  }
  busy.value = true;
  const version = revision, reviewed = preview.value;
  try {
    const data = await patientPhoneChange.change(reviewed.newPhone, reviewed.reviewStamp, currentPassword.value);
    if (!current(version)) return;
    if (data?.completed !== true || data.patientContactUpdated !== reviewed.contactWillUpdate) throw new Error('Unknown result');
    clearReview(); newPhone.value = '';
    auth.clearSession();
    await router.replace('/login?phoneChanged=1');
  } catch (e: any) {
    if (!current(version)) return;
    scopeConfirmed.value = false;
    if (e.response?.status === 409 || e.response?.status === 403) {
      clearReview(); error.value = problem(e, 'Phạm vi đã thay đổi. Vui lòng xem lại thông tin.');
    } else if ([400, 429].includes(e.response?.status) || e.response?.data?.code === 'PAT_PHONE_SMS_UNAVAILABLE') {
      error.value = problem(e, 'Không thể đổi số. Kiểm tra mật khẩu và xác nhận lại trước khi thử tiếp.');
    } else {
      uncertain.value = true;
      error.value = 'Kết quả chưa rõ. Đăng nhập lại để kiểm tra số hiện tại trước khi thực hiện yêu cầu khác.';
    }
  } finally { currentPassword.value = ''; if (active) busy.value = false; }
}
async function signInAgain() { clearReview(); newPhone.value = ''; auth.clearSession(); await router.replace('/login'); }
</script>
<style scoped>
.phone-change { padding: 1.5rem; margin: 1rem 0; border: 1px solid #cbd5e1; border-radius: .75rem; background: white; }
form { display: grid; gap: .8rem; margin: 1rem 0; max-width: 48rem; } input:not([type=checkbox]), button { padding: .6rem; font: inherit; }
.table-scroll { overflow-x: auto; } table { border-collapse: collapse; width: 100%; margin: 1rem 0; } th, td { padding: .6rem; border-bottom: 1px solid #cbd5e1; text-align: left; }
input:not([type=checkbox]) { min-width: 0; width: 100%; box-sizing: border-box; }
label { display: block; margin: .6rem 0; } [role=alert] { color: #9f1239; }
</style>
