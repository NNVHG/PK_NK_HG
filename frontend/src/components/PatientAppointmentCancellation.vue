<template>
  <button type="button" class="action-btn action-btn--cancel" :disabled="reading || busy" @click="open">
    <span>{{ reading ? 'Đang kiểm tra…' : 'Hủy lịch' }}</span>
  </button>

  <dialog ref="dialog" class="custom-dialog" :aria-labelledby="'self-cancel-title-' + appointmentId" @cancel.prevent="close">
    <div class="dialog-content">
      <div class="dialog-header">
        <div class="dialog-title-wrap">
          <span class="dialog-icon">⚠️</span>
          <div>
            <h2 :id="'self-cancel-title-' + appointmentId" class="dialog-title">
              Hủy lịch khám #{{ appointmentId }}
            </h2>
            <p class="dialog-subtitle">Bệnh nhân: <strong>{{ patientName }}</strong></p>
          </div>
        </div>
        <button type="button" class="dialog-close-btn" :disabled="busy" aria-label="Đóng hộp thoại" @click="close">✕</button>
      </div>

      <form class="dialog-form" @submit.prevent="cancel">
        <div class="info-box">
          <div class="info-row">
            <span class="info-label">Thời gian khám:</span>
            <span class="info-val">{{ formatDate(date) }} lúc {{ time.slice(0, 5) }}</span>
          </div>
        </div>

        <div v-if="error" class="dialog-alert dialog-alert--error" role="alert">
          <span>⚠️ {{ error }}</span>
        </div>

        <div v-if="eligibility?.message" class="eligibility-notice">
          <span>ℹ️ {{ eligibility.message }}</span>
        </div>

        <template v-if="eligibility?.canCancel">
          <p class="rule-hint">Hệ thống chỉ hỗ trợ tự hủy lịch trực tuyến khi còn ít nhất 2 giờ trước giờ hẹn khám.</p>

          <div class="form-group">
            <label :for="'self-cancel-reason-' + appointmentId" class="form-label">
              Lý do hủy lịch <small class="text-muted">(tùy chọn, tối đa {{ eligibility.reasonMaxLength }} ký tự)</small>
            </label>
            <textarea
              :id="'self-cancel-reason-' + appointmentId"
              v-model.trim="reason"
              :maxlength="eligibility.reasonMaxLength"
              :disabled="busy || eligibility.reasonMaxLength === 0"
              rows="3"
              placeholder="Vui lòng cho phòng khám biết lý do bạn hủy lịch…"
              class="form-textarea"
            />
          </div>

          <label class="checkbox-label">
            <input v-model="confirmed" type="checkbox" :disabled="busy" />
            <span>Tôi đã kiểm tra hồ sơ, ngày và giờ; xác nhận muốn hủy lịch hẹn này</span>
          </label>

          <div class="dialog-actions">
            <button type="button" class="btn-dialog btn-dialog--secondary" :disabled="busy" @click="close">
              Quay lại, không hủy
            </button>
            <button type="submit" class="btn-dialog btn-dialog--danger" :disabled="busy || !confirmed">
              {{ busy ? 'Đang hủy…' : 'Xác nhận hủy lịch' }}
            </button>
          </div>
        </template>

        <template v-else>
          <div class="dialog-actions">
            <button type="button" class="btn-dialog btn-dialog--secondary" :disabled="busy" @click="close">Đóng</button>
          </div>
        </template>
      </form>
    </div>
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
function formatDate(value: string): string {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}

onUnmounted(() => reset());
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

.action-btn--cancel {
  background: #fef2f2;
  color: #dc2626;
  border-color: #fecaca;
}

.action-btn--cancel:hover:not(:disabled) {
  background: #fee2e2;
  border-color: #ef4444;
}

.action-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.custom-dialog {
  max-width: 500px;
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
  background: #fffbeb;
  border: 1px solid #fef3c7;
  color: #92400e;
  padding: 0.65rem 0.85rem;
  border-radius: 6px;
  font-size: 0.825rem;
}

.rule-hint {
  font-size: 0.825rem;
  color: #64748b;
  margin: 0;
  line-height: 1.45;
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

.form-textarea {
  width: 100%;
  box-sizing: border-box;
  padding: 0.5rem 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font: inherit;
  font-size: 0.875rem;
  color: #1e293b;
  background: #ffffff;
  resize: vertical;
}

.form-textarea:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
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

.btn-dialog--danger {
  background: #dc2626;
  color: #ffffff;
}

.btn-dialog--danger:hover:not(:disabled) {
  background: #b91c1c;
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
</style>
