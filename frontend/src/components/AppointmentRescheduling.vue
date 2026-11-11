<template>
  <button type="button" class="action-btn action-btn--edit" :disabled="reading || busy" @click="open">
    <span>{{ reading ? 'Đang tải…' : 'Đổi lịch hẹn' }}</span>
  </button>

  <dialog ref="dialog" class="custom-dialog" :aria-labelledby="'reschedule-title-' + appointmentId" @cancel.prevent="close">
    <div class="dialog-content">
      <div class="dialog-header">
        <div class="dialog-title-wrap">
          <span class="dialog-icon">📅</span>
          <div>
            <h2 :id="'reschedule-title-' + appointmentId" class="dialog-title">
              Đổi lịch khám #{{ appointmentId }}
            </h2>
            <p class="dialog-subtitle">Bệnh nhân: <strong>{{ patientName }}</strong></p>
          </div>
        </div>
        <button type="button" class="dialog-close-btn" :disabled="busy" aria-label="Đóng hộp thoại" @click="close">✕</button>
      </div>

      <form class="dialog-form" @submit.prevent="save">
        <div v-if="current" class="info-box">
          <div class="info-row">
            <span class="info-label">Lịch hiện tại:</span>
            <span class="info-val">{{ formatDate(current.appointmentDate) }} lúc {{ current.slotTime.slice(0, 5) }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">Nha sĩ:</span>
            <span class="info-val">{{ current.dentistName || 'Chưa phân nha sĩ' }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">Số lần đã đổi:</span>
            <span class="info-val">{{ current.rescheduleCount }} lần</span>
          </div>
        </div>

        <div v-if="error" class="dialog-alert dialog-alert--error" role="alert">
          <span>⚠️ {{ error }}</span>
        </div>

        <div v-if="scheduleError" class="dialog-alert dialog-alert--error" role="alert">
          <span>{{ scheduleError }}</span>
          <button type="button" class="btn-text" :disabled="busy || scheduleLoading" @click="loadSchedule">Tải lại giờ</button>
        </div>

        <div v-if="selfService && eligibility" class="eligibility-notice">
          <span>ℹ️ {{ eligibility.message || `Bạn còn ${eligibility.remainingChanges} lần đổi lịch. Chỉ được đổi trước giờ hẹn ít nhất 2 giờ.` }}</span>
        </div>

        <template v-if="current?.status === 'Scheduled' && (!selfService || eligibility?.canReschedule)">
          <div class="form-row">
            <div class="form-group">
              <label :for="'new-day-' + appointmentId" class="form-label">Ngày khám mới <b class="req">*</b></label>
              <input :id="'new-day-' + appointmentId" v-model="day" type="date" :min="today" :disabled="busy" required class="form-input" />
            </div>

            <div class="form-group">
              <label :for="'new-time-' + appointmentId" class="form-label">
                Giờ mới <b class="req">*</b>
                <small v-if="slotDurationMinutes" class="text-muted">({{ slotDurationMinutes }}p)</small>
              </label>
              <select :id="'new-time-' + appointmentId" v-model="time" :disabled="busy || scheduleLoading" required class="form-select">
                <option value="" disabled>Chọn khung giờ</option>
                <option v-for="slot in slots" :key="slot" :value="slot">{{ slot }}</option>
              </select>
            </div>
          </div>

          <div class="form-group">
            <label :for="'reschedule-reason-' + appointmentId" class="form-label">
              Lý do đổi lịch <b class="req">*</b>
              <small class="text-muted">(tối đa 500 ký tự)</small>
            </label>
            <textarea
              :id="'reschedule-reason-' + appointmentId"
              v-model.trim="reason"
              maxlength="500"
              :disabled="busy"
              required
              rows="3"
              placeholder="Vui lòng nhập lý do bạn cần thay đổi lịch hẹn…"
              class="form-textarea"
            />
          </div>

          <label class="checkbox-label">
            <input v-model="confirmed" type="checkbox" :disabled="busy" />
            <span>{{ selfService ? 'Tôi xác nhận ngày, giờ mới cho hồ sơ này' : 'Đã đối chiếu ngày, giờ và yêu cầu với khách' }}</span>
          </label>

          <div class="dialog-actions">
            <button type="button" class="btn-dialog btn-dialog--secondary" :disabled="busy" @click="close">
              Quay lại, giữ lịch
            </button>
            <button type="submit" class="btn-dialog btn-dialog--primary" :disabled="busy || !confirmed">
              {{ busy ? 'Đang đổi lịch…' : 'Xác nhận đổi lịch' }}
            </button>
          </div>
        </template>

        <template v-else>
          <p v-if="current && !selfService" class="text-muted">Lịch không còn ở trạng thái chờ khám. Tải lại danh sách để đối chiếu.</p>
          <div class="dialog-actions">
            <button type="button" class="btn-dialog btn-dialog--secondary" :disabled="busy" @click="close">Đóng</button>
          </div>
        </template>
      </form>
    </div>
  </dialog>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse, type SelfReschedulingEligibility } from '@/services/appointments';
import { useBookingSchedule } from '@/composables/useBookingSchedule';
const props = defineProps<{ appointmentId: number; patientName: string; selfService?: boolean }>();
const emit = defineEmits<{ rescheduled: [] }>();
const dialog = ref<HTMLDialogElement | null>(null), current = ref<AppointmentResponse | null>(null);
const eligibility = ref<SelfReschedulingEligibility | null>(null);
const reading = ref(false), busy = ref(false), confirmed = ref(false), day = ref(''), time = ref(''), reason = ref(''), error = ref('');
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const { slots, revision: scheduleRevision, duration: slotDurationMinutes, loading: scheduleLoading, error: scheduleError, load: loadSchedule } = useBookingSchedule(false);
watch(scheduleRevision, () => { confirmed.value = false; time.value = ''; });
let version = 0;
async function readCurrent(ticket: number) {
  if (props.selfService) {
    const result = await appointmentsService.selfReschedulingEligibility(props.appointmentId);
    if (ticket === version) { eligibility.value = result; current.value = result.appointment; }
    return result.appointment;
  } else {
    const result = await appointmentsService.getById(props.appointmentId);
    if (ticket === version) current.value = result;
    return result;
  }
}
async function open() {
  if (reading.value || busy.value || !Number.isInteger(props.appointmentId) || props.appointmentId <= 0) return;
  const ticket = ++version; reading.value = true; current.value = null; eligibility.value = null; confirmed.value = false; reason.value = ''; error.value = '';
  try {
    const [result] = await Promise.all([readCurrent(ticket), loadSchedule()]);
    if (ticket === version) { day.value = result.appointmentDate; time.value = result.slotTime.slice(0, 5); }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch. Vui lòng thử lại.'; }
  finally { if (ticket === version) { reading.value = false; if (dialog.value && !dialog.value.open) dialog.value.showModal(); } }
}
function reset() { version++; dialog.value?.close(); current.value = null; eligibility.value = null; confirmed.value = false; day.value = ''; time.value = ''; reason.value = ''; error.value = ''; }
function close() { if (!busy.value) reset(); }
async function save() {
  if (busy.value || reading.value || scheduleLoading.value || !scheduleRevision.value || !confirmed.value || current.value?.status !== 'Scheduled' || current.value.appointmentId !== props.appointmentId) return;
  if (props.selfService && !eligibility.value?.canReschedule) return;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day.value) || day.value < today || !slots.value.includes(time.value) || !reason.value.trim() || reason.value.trim().length > 500 ||
      (day.value === current.value.appointmentDate && time.value === current.value.slotTime.slice(0, 5))) { error.value = 'Chọn ngày/giờ mới hợp lệ và nhập lý do đổi lịch.'; return; }
  const ticket = version; busy.value = true; error.value = '';
  try {
    await appointmentsService.reschedule(props.appointmentId, day.value, `${time.value}:00`, reason.value.trim(), scheduleRevision.value);
    if (ticket === version) { busy.value = false; reset(); emit('rescheduled'); }
  } catch (cause) {
    if (ticket !== version) return;
    if (axios.isAxiosError(cause) && cause.response?.data?.code === 'APP_SCHEDULE') { time.value = ''; await loadSchedule(); }
    if (ticket !== version) return;
    confirmed.value = false;
    error.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Tải lại lịch trước khi thử tiếp.' : 'Chưa xác định kết quả. Tải lại lịch trước khi thử tiếp.';
    try { await readCurrent(ticket); }
    catch { if (ticket === version) { current.value = null; eligibility.value = null; } }
  } finally { if (ticket === version) busy.value = false; }
}
function formatDate(value: string): string {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}

onUnmounted(reset);
</script>

<style scoped>
.action-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  height: 36px;
  padding: 0 0.85rem;
  border-radius: 6px;
  font: inherit;
  font-size: 0.825rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
  border: 1px solid transparent;
}

.action-btn--edit {
  background: #f0f9ff;
  color: #0369a1;
  border-color: #bae6fd;
}

.action-btn--edit:hover:not(:disabled) {
  background: #e0f2fe;
  border-color: #0284c7;
}

.action-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.custom-dialog {
  position: fixed;
  inset: 0;
  margin: auto;
  max-width: 540px;
  width: calc(100% - 2rem);
  padding: 0;
  border: 1px solid #cbd5e1;
  border-radius: 12px;
  box-shadow: 0 10px 25px -5px rgba(15, 45, 89, 0.2);
  background: #ffffff;
}

.custom-dialog::backdrop {
  background: rgba(15, 45, 89, 0.45);
  backdrop-filter: blur(2px);
}

.dialog-content {
  display: flex;
  flex-direction: column;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid #e2e8f0;
}

.dialog-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.dialog-icon {
  font-size: 1.35rem;
}

.dialog-title {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.dialog-subtitle {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #64748b;
}

.dialog-close-btn {
  border: none;
  background: transparent;
  font-size: 1.25rem;
  color: #94a3b8;
  cursor: pointer;
  padding: 0.2rem 0.4rem;
  border-radius: 4px;
}

.dialog-close-btn:hover {
  color: #0f2d59;
  background: #f1f5f9;
}

.dialog-form {
  padding: 1.25rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.info-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-size: 0.825rem;
}

.info-row {
  display: flex;
  justify-content: space-between;
}

.info-label {
  color: #64748b;
}

.info-val {
  font-weight: 700;
  color: #1e293b;
}

.dialog-alert {
  padding: 0.65rem 0.85rem;
  border-radius: 6px;
  font-size: 0.825rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.dialog-alert--error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
}

.eligibility-notice {
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #166534;
  padding: 0.65rem 0.85rem;
  border-radius: 6px;
  font-size: 0.825rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.85rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.form-label {
  font-size: 0.825rem;
  font-weight: 700;
  color: #334155;
}

.req {
  color: #dc2626;
}

.form-input,
.form-select,
.form-textarea {
  width: 100%;
  box-sizing: border-box;
  height: 38px;
  padding: 0 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font: inherit;
  font-size: 0.875rem;
  color: #1e293b;
  background: #ffffff;
}

.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.form-textarea {
  height: auto;
  padding: 0.5rem 0.75rem;
  resize: vertical;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.825rem;
  color: #334155;
  cursor: pointer;
  padding: 0.35rem 0;
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.65rem;
  margin-top: 0.5rem;
  padding-top: 1rem;
  border-top: 1px solid #e2e8f0;
}

.btn-dialog {
  height: 38px;
  padding: 0 1rem;
  border-radius: 6px;
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  cursor: pointer;
  border: 1px solid transparent;
  transition: all 0.15s ease;
}

.btn-dialog--primary {
  background: #0284c7;
  color: #ffffff;
}

.btn-dialog--primary:hover:not(:disabled) {
  background: #0369a1;
}

.btn-dialog--secondary {
  background: #ffffff;
  color: #475569;
  border-color: #cbd5e1;
}

.btn-dialog--secondary:hover:not(:disabled) {
  background: #f1f5f9;
}

.btn-dialog:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-text {
  background: transparent;
  border: none;
  color: #0284c7;
  font-size: 0.775rem;
  font-weight: 700;
  cursor: pointer;
  text-decoration: underline;
  padding: 0;
}
</style>
