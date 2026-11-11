<template>
  <section aria-labelledby="staff-phone-title" class="phone-change">
    <h2 id="staff-phone-title">Yêu cầu đổi SĐT đăng nhập</h2>
    <p>{{ staffName }} phải đăng nhập bằng số cũ, mở Hồ sơ cá nhân và nhập OTP gửi tới số mới. Số cũ và các phiên vẫn dùng được trong lúc chờ.</p>
    <form @submit.prevent="submit">
      <label for="staff-new-phone">SĐT mới</label>
      <input id="staff-new-phone" ref="phoneInput" v-model="newPhone" type="tel" inputmode="numeric" autocomplete="off" maxlength="10" required :disabled="busy" />
      <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi đã đối chiếu số mới với nhân viên và yêu cầu gửi OTP.</label>
      <p v-if="error" role="alert">{{ error }}</p>
      <p role="status">{{ message }}</p>
      <button type="submit" :disabled="busy || !confirmed">{{ busy ? 'Đang gửi…' : 'Gửi OTP số mới' }}</button>
      <button type="button" :disabled="busy" @click="$emit('close')">Đóng</button>
    </form>
  </section>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { apiClient } from '@/services/api';
import { useAuthStore } from '@/stores/auth';
const props = defineProps<{ userId: number; staffName: string }>();
defineEmits<{ close: [] }>();
const auth = useAuthStore();
const newPhone = ref(''), confirmed = ref(false), busy = ref(false), error = ref(''), message = ref('');
const phoneInput = ref<HTMLInputElement | null>(null);
const returnFocus = typeof document !== 'undefined' && document.activeElement instanceof HTMLElement ? document.activeElement : null;
onMounted(() => phoneInput.value?.focus());
let active = true;
function clear() { newPhone.value = ''; confirmed.value = false; error.value = ''; message.value = ''; }
onUnmounted(() => { active = false; clear(); if (returnFocus?.isConnected) returnFocus.focus(); });
watch(() => [auth.user?.userId, auth.role], () => { active = false; clear(); });
watch(newPhone, () => { confirmed.value = false; message.value = ''; });
async function submit() {
  if (busy.value || !active || auth.role !== 'ADMIN' || !confirmed.value) return;
  error.value = ''; message.value = '';
  if (newPhone.value.length !== 10 || !/^0[0-9]{9}$/.test(newPhone.value)) { error.value = 'SĐT mới cần đúng 10 chữ số, bắt đầu bằng 0.'; return; }
  busy.value = true;
  try {
    const response = await apiClient.post(`/staff/${props.userId}/phone-change`, { newPhone: newPhone.value, confirmed: true });
    if (!active) return;
    if (!response.data?.challengeId || !response.data?.expiresAtUtc) throw new Error('Invalid response');
    message.value = 'Đã gửi OTP, có hạn 5 phút. Nhân viên mở Hồ sơ cá nhân để xác nhận. Số đăng nhập chưa đổi; mật khẩu được giữ nguyên.';
    confirmed.value = false;
  } catch (e: any) { if (active) error.value = e.response?.data?.message || 'Không gửi được yêu cầu. Kiểm tra lại trước khi gửi mới.'; }
  finally { if (active) busy.value = false; }
}
</script>
<style scoped>
.phone-change {
  padding: 1.5rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
  margin: 1.25rem 0;
}

h2 {
  color: #0f2d59;
  font-size: 1.25rem;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

form {
  display: grid;
  gap: 0.875rem;
  max-width: 42rem;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  font-size: 0.925rem;
}

input[type="tel"] {
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
