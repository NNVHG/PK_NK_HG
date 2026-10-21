<template>
  <section aria-labelledby="counter-suggestion-title">
    <h3 id="counter-suggestion-title">Yêu cầu đổi số từ quầy</h3>
    <p v-if="suggestion">Lịch #{{ suggestion.appointmentId }} có đề nghị đổi sang {{ suggestion.newPhone }}. Đây chưa phải thay đổi đã được chấp thuận. Bạn tự kiểm tra số, xem danh sách hồ sơ và xác nhận OTP ở bước dưới.</p>
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
<style scoped>section { padding: .8rem; border-left: 3px solid #64748b; margin: 1rem 0; } button { padding: .6rem; margin: .3rem; font: inherit; } [role=alert] { color: #9f1239; }</style>
