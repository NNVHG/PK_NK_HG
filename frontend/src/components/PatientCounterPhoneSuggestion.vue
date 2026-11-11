<template>
  <section aria-labelledby="counter-suggestion-title">
    <h3 id="counter-suggestion-title">Yêu cầu đổi số từ quầy</h3>
    <p v-if="suggestion">Lịch #{{ suggestion.appointmentId }} có đề nghị đổi sang {{ suggestion.newPhone }}. Đây chưa phải thay đổi đã được chấp thuận. Bạn tự kiểm tra số và phạm vi, nhập mật khẩu để lưu. OTP được yêu cầu ở lần đăng nhập tiếp theo.</p>
    <p v-else-if="loaded">Không có yêu cầu hiện hành từ quầy.</p>
    <button v-if="suggestion" type="button" :disabled="busy || disabled" @click="useSuggestion">Dùng số đề nghị để xem lại phạm vi</button>
    <button type="button" :disabled="busy || disabled" @click="load">{{ busy ? 'Đang tải yêu cầu…' : 'Tải yêu cầu từ quầy' }}</button>
    <p v-if="error" role="alert">{{ error }}</p>
  </section>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { counterPatientPhone, isCounterPhoneSuggestion, type CounterPhoneSuggestion } from '@/services/counterPatientPhone';
const props = defineProps<{ disabled: boolean }>();
const emit = defineEmits<{ select: [phone: string] }>();
const auth = useAuthStore(), owner = auth.user?.userId;
const suggestion = ref<CounterPhoneSuggestion | null>(null), busy = ref(false), loaded = ref(false), error = ref('');
let alive = true, revision = 0;
const current = (n: number) => alive && n === revision && auth.user?.userId === owner && auth.role === 'PATIENT';
watch(() => [auth.user?.userId, auth.role], () => { alive = false; revision++; suggestion.value = null; error.value = ''; });
onUnmounted(() => { alive = false; revision++; suggestion.value = null; error.value = ''; });
async function load() {
  if (busy.value || props.disabled || !current(revision)) return;
  busy.value = true; suggestion.value = null; error.value = ''; loaded.value = false; const n = ++revision;
  try {
    const data = await counterPatientPhone.own();
    if (!current(n)) return;
    if (!data || !Object.hasOwn(data, 'request') || data.request !== null && !isCounterPhoneSuggestion(data.request)) throw new Error('Invalid request');
    suggestion.value = data.request; loaded.value = true;
  } catch { if (current(n)) error.value = 'Không tải được yêu cầu từ quầy. Bạn có thể tải lại; số hiện tại chưa thay đổi.'; }
  finally { if (alive) busy.value = false; }
}
function useSuggestion() { if (!busy.value && !props.disabled && current(revision) && suggestion.value) emit('select', suggestion.value.newPhone); }
onMounted(load);
</script>
<style scoped>
section {
  padding: 1rem 1.25rem;
  border-left: 4px solid #0284c7;
  border-radius: 8px;
  background: #f0f9ff;
  margin: 1rem 0;
  border-top: 1px solid #e0f2fe;
  border-right: 1px solid #e0f2fe;
  border-bottom: 1px solid #e0f2fe;
}

h3 {
  color: #0f2d59;
  font-size: 1.1rem;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.5rem;
}

p {
  color: #334155;
  margin: 0.5rem 0;
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
  margin: 0.35rem 0.35rem 0.35rem 0;
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
    margin-right: 0;
  }
}
</style>
