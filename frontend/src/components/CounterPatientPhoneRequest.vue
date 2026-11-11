<template>
  <section aria-labelledby="counter-phone-title">
    <h2 id="counter-phone-title">Yêu cầu đổi SĐT liên hệ tài khoản</h2>
    <p>Chỉ khởi tạo cho tài khoản gắn hồ sơ của lịch #{{ appointmentId }} vừa đặt. Chủ tài khoản tự đăng nhập bằng giấy tờ và mật khẩu hiện tại, mở Hồ sơ cá nhân để kiểm tra phạm vi và nhập mật khẩu lưu số mới. OTP được yêu cầu ở lần đăng nhập tiếp theo. Đề nghị tại quầy chưa làm thay đổi số.</p>
    <form @submit.prevent="submit">
      <label for="counter-phone-new">SĐT mới chủ tài khoản đề nghị</label>
      <input id="counter-phone-new" v-model="phone" name="newPhone" type="tel" inputmode="numeric" autocomplete="off" maxlength="10" :disabled="busy || sent || uncertain" required />
      <label><input v-model="confirmed" type="checkbox" :disabled="busy || sent || uncertain" /> Đã đối soát tài khoản liên kết với chủ tài khoản khi đặt lịch; đây chỉ là yêu cầu, chưa đổi số.</label>
      <button type="submit" :disabled="busy || sent || uncertain">{{ busy ? 'Đang khởi tạo…' : 'Khởi tạo yêu cầu cho chủ tài khoản' }}</button>
    </form>
    <p v-if="message" role="status">{{ message }}</p><p v-if="error" role="alert">{{ error }}</p>
  </section>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { counterPatientPhone } from '@/services/counterPatientPhone';
const props = defineProps<{ appointmentId: number }>();
const auth = useAuthStore(), actor = auth.user?.userId;
const phone = ref(''), confirmed = ref(false), busy = ref(false), sent = ref(false), uncertain = ref(false), error = ref(''), message = ref('');
let alive = true, revision = 0;
function reset() { revision++; phone.value = ''; confirmed.value = false; sent.value = false; uncertain.value = false; error.value = ''; message.value = ''; }
watch(() => props.appointmentId, reset);
watch(phone, () => { confirmed.value = false; });
watch(() => [auth.user?.userId, auth.role], () => { alive = false; reset(); });
onUnmounted(() => { alive = false; reset(); });
async function submit() {
  if (!alive || busy.value || sent.value || uncertain.value || auth.user?.userId !== actor || !['ADMIN','RECEPTIONIST'].includes(auth.role ?? '')) return;
  error.value = '';
  if (!Number.isInteger(props.appointmentId) || props.appointmentId <= 0 || phone.value.length !== 10 || !/^0[0-9]{9}$/.test(phone.value) || !confirmed.value) { error.value = 'Nhập SĐT mới đúng 10 chữ số bắt đầu bằng 0 và xác nhận đã đối soát.'; return; }
  busy.value = true; const ticket = revision;
  try {
    const result = await counterPatientPhone.create(props.appointmentId, phone.value);
    if (!alive || ticket !== revision || auth.user?.userId !== actor) return;
    if (!result || result.requiresOwnerConfirmation !== true || typeof result.requestId !== 'string' || !/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(result.requestId)) throw new Error('Unknown result');
    sent.value = true; phone.value = ''; confirmed.value = false;
    message.value = 'Đã khởi tạo. Hướng dẫn chủ tài khoản tự đăng nhập, kiểm tra phạm vi và nhập mật khẩu tại Hồ sơ cá nhân để lưu số mới; xác minh OTP khi đăng nhập lại. Nhân viên không nhận hoặc nhập OTP thay; chủ tài khoản tự xác nhận.';
  } catch (e: any) {
    if (!alive || ticket !== revision || auth.user?.userId !== actor) return;
    confirmed.value = false;
    if ([400,403,404,409,429].includes(e.response?.status)) error.value = e.response?.data?.message || 'Chưa tạo được yêu cầu; kiểm tra tài khoản liên kết và lịch hẹn.';
    else { uncertain.value = true; error.value = 'Chưa rõ kết quả. Chủ tài khoản đăng nhập kiểm tra yêu cầu hiện hành trước khi thử lại; lịch hẹn đã đặt vẫn giữ nguyên.'; }
  } finally { if (alive) busy.value = false; }
}
</script>
<style scoped>
section {
  margin-top: 1.25rem;
  padding: 1.25rem;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

h2 {
  color: #0f2d59;
  font-size: 1.2rem;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

form {
  display: grid;
  gap: 0.75rem;
  margin: 1rem 0;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  font-size: 0.925rem;
}

input:not([type="checkbox"]) {
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  vertical-align: middle;
  margin-right: 0.4rem;
}

button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #0f2d59;
  color: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  margin: 0.4rem 0;
}

button:hover:not(:disabled) {
  background: #1e3a8a;
}

button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

[role="alert"] {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 0.75rem 0;
}

[role="status"] {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 0.75rem 0;
}

@media (max-width: 640px) {
  button {
    width: 100%;
  }
}
</style>
