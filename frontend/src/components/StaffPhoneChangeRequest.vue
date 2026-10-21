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
.phone-change { padding: 1.5rem; background: white; border: 1px solid #cbd5e1; border-radius: .75rem; margin: 1rem 0; }
form { display: grid; gap: .8rem; max-width: 42rem; } input[type=tel] { padding: .6rem; border: 1px solid #64748b; border-radius: .3rem; } button { padding: .6rem; } [role=alert] { color: #9f1239; }
</style>
