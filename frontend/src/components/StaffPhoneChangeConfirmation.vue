<template>
  <section class="phone-confirm" aria-labelledby="phone-confirm-title">
    <h2 id="phone-confirm-title">Xác nhận đổi SĐT đăng nhập</h2>
    <p>Số hiện tại và phiên đăng nhập chỉ thay đổi sau khi bạn xác nhận OTP số mới. Mật khẩu được giữ nguyên.</p>
    <button type="button" :disabled="busy" @click="load">Kiểm tra yêu cầu từ Admin</button>
    <form v-if="pending" @submit.prevent="confirm">
      <p>Số mới: <strong>{{ pending.newPhone }}</strong>. OTP có hạn 5 phút từ lúc gửi.</p>
      <label for="phone-change-code">OTP gửi tới số mới</label>
      <input id="phone-change-code" v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy" required />
      <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi xác nhận số mới thuộc quyền sử dụng của mình. Các phiên cũ sẽ bị thu hồi và tôi cần đăng nhập lại.</label>
      <button type="submit" :disabled="busy || !confirmed">{{ busy ? 'Đang xử lý…' : 'Xác minh và đổi số' }}</button>
    </form>
    <p role="status">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
  </section>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { apiClient } from '@/services/api';
const auth = useAuthStore(), router = useRouter();
const pending = ref<{ challengeId: string; newPhone: string; expiresAtUtc: string } | null>(null);
const code = ref(''), confirmed = ref(false), busy = ref(false), message = ref(''), error = ref('');
let active = true;
function clear() { pending.value = null; code.value = ''; confirmed.value = false; message.value = ''; error.value = ''; }
onUnmounted(() => { active = false; clear(); });
watch(() => [auth.user?.userId, auth.role], () => { active = false; clear(); });
watch(code, () => { confirmed.value = false; });
onMounted(load);
async function load() {
  if (!active || busy.value || !['ADMIN', 'DENTIST', 'ASSISTANT', 'RECEPTIONIST'].includes(auth.role)) return;
  busy.value = true; clear();
  try {
    const response = await apiClient.get('/auth/staff-phone-change');
    if (!active) return;
    const item = response.data?.pending;
    if (item && (!item.challengeId || !/^0[0-9]{9}$/.test(item.newPhone) || !Number.isFinite(Date.parse(item.expiresAtUtc)))) throw new Error('Invalid response');
    pending.value = item ?? null;
    if (!item) message.value = 'Chưa có yêu cầu còn hiệu lực. Liên hệ Admin khi cần đổi số.';
  } catch (e: any) { if (active && e.response?.status !== 403) error.value = e.response?.data?.message || 'Không tải được yêu cầu. Hãy kiểm tra lại.'; }
  finally { if (active) busy.value = false; }
}
async function confirm() {
  if (!active || busy.value || !pending.value || !confirmed.value) return;
  error.value = '';
  if (code.value.length !== 6 || !/^[0-9]{6}$/.test(code.value)) { error.value = 'OTP cần đúng 6 chữ số.'; return; }
  if (Date.parse(pending.value.expiresAtUtc) <= Date.now()) { clear(); error.value = 'Yêu cầu hết hạn. Nhờ Admin gửi OTP mới.'; return; }
  busy.value = true;
  try {
    const response = await apiClient.post('/auth/staff-phone-change/confirm', { challengeId: pending.value.challengeId, code: code.value, confirmed: true });
    if (!active) return;
    if (response.data?.completed !== true) throw new Error('Invalid response');
    clear();
    auth.clearSession();
    await router.replace('/login?phoneChanged=1');
  } catch (e: any) { if (active) { code.value = ''; confirmed.value = false; error.value = e.response?.data?.message || 'Chưa xác định kết quả. Kiểm tra đăng nhập/số hiện tại trước khi yêu cầu mới.'; } }
  finally { if (active) busy.value = false; }
}
</script>
<style scoped>
.phone-confirm {
  margin: 1.25rem 0;
  padding: 1.5rem;
  border: 1px solid #e2e8f0;
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
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
  margin-top: 1rem;
  max-width: 42rem;
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

@media (max-width: 640px) {
  button {
    width: 100%;
  }
}
</style>
