<template>
  <button :disabled="reading || busy" @click="open">{{ reading ? 'Đang kiểm tra…' : 'Tự hủy lịch' }}</button>
  <dialog ref="dialog" :aria-labelledby="'self-cancel-title-' + appointmentId" @cancel.prevent="close">
    <form @submit.prevent="cancel">
      <h2 :id="'self-cancel-title-' + appointmentId">Hủy lịch #{{ appointmentId }} · {{ patientName }}</h2>
      <p>{{ date }} · {{ time.slice(0, 5) }}</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <p v-if="eligibility?.message" role="status">{{ eligibility.message }}</p>
      <template v-if="eligibility?.canCancel">
        <p>Hệ thống chỉ cho tự hủy khi còn ít nhất 2 giờ trước lịch hẹn.</p>
        <label :for="'self-cancel-reason-' + appointmentId">Lý do (tùy chọn, tối đa {{ eligibility.reasonMaxLength }} ký tự còn lại)</label>
        <textarea :id="'self-cancel-reason-' + appointmentId" v-model.trim="reason" :maxlength="eligibility.reasonMaxLength" :disabled="busy || eligibility.reasonMaxLength === 0" />
        <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi đã kiểm tra hồ sơ, ngày và giờ; xác nhận hủy lịch này</label>
        <button :disabled="busy || !confirmed">{{ busy ? 'Đang hủy…' : 'Xác nhận hủy lịch' }}</button>
      </template>
      <button type="button" :disabled="busy" @click="close">Quay lại</button>
    </form>
  </dialog>
</template>
<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import axios from 'axios';
import { appointmentsService, type SelfCancellationEligibility } from '@/services/appointments';
const props = defineProps<{ appointmentId: number; patientName: string; date: string; time: string }>();
const emit = defineEmits<{ cancelled: [] }>();
const dialog = ref<HTMLDialogElement | null>(null), eligibility = ref<SelfCancellationEligibility | null>(null);
const reading = ref(false), busy = ref(false), confirmed = ref(false), reason = ref(''), error = ref('');
let version = 0;
async function open() {
  if (reading.value || busy.value || !Number.isInteger(props.appointmentId) || props.appointmentId <= 0) return;
  const ticket = ++version; reading.value = true; eligibility.value = null; confirmed.value = false; reason.value = ''; error.value = '';
  try { const result = await appointmentsService.selfCancellationEligibility(props.appointmentId); if (ticket === version) eligibility.value = result; }
  catch { if (ticket === version) error.value = 'Không thể kiểm tra điều kiện hủy. Vui lòng tải lại lịch hoặc liên hệ Lễ tân.'; }
  finally {
    if (ticket === version) { reading.value = false; if (dialog.value && !dialog.value.open) dialog.value.showModal(); }
  }
}
function reset() { version++; dialog.value?.close(); eligibility.value = null; confirmed.value = false; reason.value = ''; error.value = ''; }
function close() { if (!busy.value) reset(); }
async function cancel() {
  if (busy.value || reading.value || !confirmed.value || !eligibility.value?.canCancel || eligibility.value.appointmentId !== props.appointmentId) return;
  if (reason.value.trim().length > eligibility.value.reasonMaxLength) { error.value = 'Lý do hủy vượt giới hạn còn lại.'; return; }
  const ticket = version; busy.value = true; error.value = '';
  try {
    await appointmentsService.cancel(props.appointmentId, reason.value.trim() || null);
    if (ticket !== version) return;
    busy.value = false; reset(); emit('cancelled');
  } catch (cause) {
    if (ticket !== version) return;
    confirmed.value = false; eligibility.value = null;
    error.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Tải lại lịch để đối chiếu.' : 'Chưa xác định kết quả. Tải lại lịch để đối chiếu.';
    try { const result = await appointmentsService.selfCancellationEligibility(props.appointmentId); if (ticket === version) eligibility.value = result; }
    catch { /* Keep the form disabled when the current state cannot be verified. */ }
  } finally { if (ticket === version) busy.value = false; }
}
onUnmounted(() => reset());
</script>
<style scoped>
dialog { max-width: 520px; width: calc(100% - 3rem); border: 1px solid #ddd; border-radius: 10px; }
dialog::backdrop { background: #0006; } form { display: grid; gap: .7rem; padding: .5rem; }
button, textarea { font: inherit; padding: .6rem; } [role=alert] { color: #b91c1c; }
</style>
