<template>
  <section class="family-phone" aria-labelledby="family-phone-title">
    <h2 id="family-phone-title">Đổi SĐT tài khoản bệnh nhân</h2>
    <p>OTP được gửi tới số mới. Sau khi xác nhận, số đăng nhập và hồ sơ đang dùng chung số cũ sẽ được đổi cùng lúc. Mật khẩu được giữ nguyên; các phiên cũ sẽ bị thu hồi.</p>
    <PatientCounterPhoneSuggestion :disabled="busy || uncertain" @select="acceptCounterPhone" />
    <form @submit.prevent="loadPreview">
      <label for="family-new-phone">SĐT mới</label>
      <input id="family-new-phone" v-model="newPhone" type="tel" inputmode="numeric" autocomplete="tel" maxlength="10" :disabled="busy || uncertain" required />
      <button type="submit" :disabled="busy || uncertain">Xem lại danh sách hồ sơ</button>
    </form>
    <div v-if="preview">
      <h3>Phạm vi đồng bộ</h3>
      <p>Đăng nhập: <strong>{{ preview.currentPhone }}</strong> → <strong>{{ preview.newPhone }}</strong>. Có {{ affectedCount }} hồ sơ đổi liên hệ sang số mới.</p>
      <div class="table-scroll">
        <table v-if="preview.profiles.length">
          <caption>Toàn bộ hồ sơ thuộc tài khoản và tác động khi đổi số</caption>
          <thead><tr><th scope="col">Hồ sơ</th><th scope="col">Ngày sinh</th><th scope="col">Trạng thái</th><th scope="col">Số liên hệ</th></tr></thead>
          <tbody><tr v-for="profile in preview.profiles" :key="profile.patientId">
            <th scope="row">{{ profile.fullName }} (#{{ profile.patientId }})</th><td>{{ profile.dateOfBirth }}</td>
            <td>{{ profile.isActive ? 'Đang hoạt động' : 'Đã ngừng hoạt động' }}</td>
            <td>{{ profile.willUpdate ? 'Đổi sang số mới' : 'Giữ số liên hệ riêng' }}</td>
          </tr></tbody>
        </table>
      </div>
      <p v-if="!preview.profiles.length">Tài khoản chưa có hồ sơ liên kết. Chỉ số đăng nhập sẽ được đổi.</p>
      <label><input v-model="scopeConfirmed" type="checkbox" :disabled="busy || !!pending || uncertain" /> Tôi đã xem toàn bộ danh sách và đồng ý đồng bộ đúng các hồ sơ đánh dấu “Đổi sang số mới”, kể cả hồ sơ đã ngừng hoạt động.</label>
      <button type="button" :disabled="busy || !scopeConfirmed || !!pending || uncertain" @click="requestOtp">Gửi OTP tới số mới</button>
    </div>
    <form v-if="pending && !uncertain" @submit.prevent="confirm">
      <p>OTP có hạn 5 phút kể từ lúc gửi. Nếu cần đổi số hoặc danh sách, chọn “Xem lại danh sách hồ sơ” và xin mã mới.</p>
      <label for="family-phone-code">OTP gửi tới số mới</label>
      <input id="family-phone-code" v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy" required />
      <label><input v-model="finalConfirmed" type="checkbox" :disabled="busy" /> Tôi xác nhận số mới thuộc quyền sử dụng của mình và đồng ý đổi đăng nhập, đồng bộ đúng danh sách ở trên, rồi đăng nhập lại.</label>
      <button type="submit" :disabled="busy || !finalConfirmed">Xác minh và đổi số</button>
    </form>
    <p role="status">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <button v-if="uncertain" type="button" @click="signInAgain">Đăng nhập lại để kiểm tra kết quả</button>
  </section>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import PatientCounterPhoneSuggestion from './PatientCounterPhoneSuggestion.vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { patientPhoneChange, isPhoneScopePreview, type PhoneScopePreview, type PhoneChangePending } from '@/services/patientPhoneChange';
const auth = useAuthStore(), router = useRouter();
const owner = auth.user?.userId;
const newPhone = ref(''), code = ref(''), message = ref(''), error = ref('');
const preview = ref<PhoneScopePreview | null>(null), pending = ref<PhoneChangePending | null>(null);
const busy = ref(false), scopeConfirmed = ref(false), finalConfirmed = ref(false), uncertain = ref(false);
const affectedCount = computed(() => preview.value?.profiles.filter(p => p.willUpdate).length ?? 0);
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
    if (!isPhoneScopePreview(data, requested, auth.user?.phone)) throw new Error('Invalid preview');
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
    message.value = 'Đã gửi OTP. Số đăng nhập, liên hệ hồ sơ và các phiên cũ hiện vẫn được giữ nguyên.';
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
  const version = revision, count = affectedCount.value;
  try {
    const data = await patientPhoneChange.confirm(pending.value.challengeId, code.value, preview.value.reviewStamp);
    if (!current(version)) return;
    if (data?.completed !== true || data.updatedProfiles !== count) throw new Error('Unknown result');
    clearReview(); newPhone.value = '';
    auth.clearSession();
    await router.replace('/login?phoneChanged=1');
  } catch (e: any) {
    if (!current(version)) return;
    code.value = ''; finalConfirmed.value = false;
    if (e.response?.status === 409 || e.response?.status === 403) {
      clearReview(); error.value = problem(e, 'Phạm vi đã thay đổi. Vui lòng xem lại danh sách.');
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
.family-phone { padding: 1.5rem; margin: 1rem 0; border: 1px solid #cbd5e1; border-radius: .75rem; background: white; }
form { display: grid; gap: .8rem; margin: 1rem 0; max-width: 48rem; } input:not([type=checkbox]), button { padding: .6rem; font: inherit; }
.table-scroll { overflow-x: auto; } table { border-collapse: collapse; width: 100%; margin: 1rem 0; } th, td { padding: .6rem; border-bottom: 1px solid #cbd5e1; text-align: left; }
label { display: block; margin: .6rem 0; } [role=alert] { color: #9f1239; }
</style>
