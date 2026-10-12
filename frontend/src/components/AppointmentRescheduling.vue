<template>
  <button :disabled="reading || busy" @click="open">{{ reading ? 'Đang tải…' : 'Đổi lịch' }}</button>
  <dialog ref="dialog" :aria-labelledby="'reschedule-title-' + appointmentId" @cancel.prevent="close">
    <form @submit.prevent="save">
      <h2 :id="'reschedule-title-' + appointmentId">Đổi lịch #{{ appointmentId }} · {{ patientName }}</h2>
      <p v-if="current">Lịch hiện tại: {{ current.appointmentDate }} · {{ current.slotTime.slice(0, 5) }} · {{ current.dentistName || 'Chưa phân nha sĩ' }}. Đã đổi {{ current.rescheduleCount }} lần.</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <template v-if="current?.status === 'Scheduled'">
        <label :for="'new-day-' + appointmentId">Ngày mới</label><input :id="'new-day-' + appointmentId" v-model="day" type="date" :min="today" :disabled="busy" required />
        <label :for="'new-time-' + appointmentId">Giờ mới</label><select :id="'new-time-' + appointmentId" v-model="time" :disabled="busy" required><option v-for="slot in slots" :key="slot">{{ slot }}</option></select>
        <label :for="'reschedule-reason-' + appointmentId">Lý do đổi lịch (bắt buộc, tối đa 500 ký tự)</label><textarea :id="'reschedule-reason-' + appointmentId" v-model.trim="reason" maxlength="500" :disabled="busy" required />
        <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Đã đối chiếu ngày, giờ và yêu cầu với khách</label>
        <button :disabled="busy || !confirmed">{{ busy ? 'Đang đổi lịch…' : 'Xác nhận đổi lịch' }}</button>
      </template>
      <p v-else-if="current">Lịch không còn ở trạng thái chờ khám. Tải lại danh sách để đối chiếu.</p>
      <button type="button" :disabled="busy" @click="close">Quay lại, giữ lịch</button>
    </form>
  </dialog>
</template>
<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
const props = defineProps<{ appointmentId: number; patientName: string }>();
const emit = defineEmits<{ rescheduled: [] }>();
const dialog = ref<HTMLDialogElement | null>(null), current = ref<AppointmentResponse | null>(null);
const reading = ref(false), busy = ref(false), confirmed = ref(false), day = ref(''), time = ref(''), reason = ref(''), error = ref('');
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const slots = Array.from({ length: 18 }, (_, i) => 480 + i * 30).filter(n => n <= 690 || n >= 810)
  .map(n => `${String(Math.floor(n / 60)).padStart(2, '0')}:${String(n % 60).padStart(2, '0')}`);
let version = 0;
async function open() {
  if (reading.value || busy.value || !Number.isInteger(props.appointmentId) || props.appointmentId <= 0) return;
  const ticket = ++version; reading.value = true; current.value = null; confirmed.value = false; reason.value = ''; error.value = '';
  try {
    const result = await appointmentsService.getById(props.appointmentId);
    if (ticket === version) { current.value = result; day.value = result.appointmentDate; time.value = result.slotTime.slice(0, 5); }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch. Vui lòng thử lại.'; }
  finally { if (ticket === version) { reading.value = false; if (dialog.value && !dialog.value.open) dialog.value.showModal(); } }
}
function reset() { version++; dialog.value?.close(); current.value = null; confirmed.value = false; day.value = ''; time.value = ''; reason.value = ''; error.value = ''; }
function close() { if (!busy.value) reset(); }
async function save() {
  if (busy.value || reading.value || !confirmed.value || current.value?.status !== 'Scheduled' || current.value.appointmentId !== props.appointmentId) return;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day.value) || day.value < today || !slots.includes(time.value) || !reason.value.trim() || reason.value.trim().length > 500 ||
      (day.value === current.value.appointmentDate && time.value === current.value.slotTime.slice(0, 5))) { error.value = 'Chọn ngày/giờ mới hợp lệ và nhập lý do đổi lịch.'; return; }
  const ticket = version; busy.value = true; error.value = '';
  try {
    await appointmentsService.reschedule(props.appointmentId, day.value, `${time.value}:00`, reason.value.trim());
    if (ticket === version) { busy.value = false; reset(); emit('rescheduled'); }
  } catch (cause) {
    if (ticket !== version) return;
    confirmed.value = false;
    error.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Tải lại lịch trước khi thử tiếp.' : 'Chưa xác định kết quả. Tải lại lịch trước khi thử tiếp.';
    try { const result = await appointmentsService.getById(props.appointmentId); if (ticket === version) current.value = result; }
    catch { if (ticket === version) current.value = null; }
  } finally { if (ticket === version) busy.value = false; }
}
onUnmounted(reset);
</script>
<style scoped>
dialog { max-width: 560px; width: calc(100% - 3rem); border: 1px solid #ddd; border-radius: 10px; }
dialog::backdrop { background: #0006; } form { display: grid; gap: .7rem; padding: .5rem; }
button, input, select, textarea { font: inherit; padding: .6rem; } [role=alert] { color: #b91c1c; }
</style>
