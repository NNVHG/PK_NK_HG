<template>
  <div class="booking-grid">
    <div class="panel form-panel">
      <div class="panel-header">
        <div class="panel-title-wrap">
          <span class="panel-icon">📅</span>
          <div>
            <h2 class="panel-title">Thông tin lịch hẹn</h2>
            <p class="panel-subtitle">Điền thông tin và lựa chọn khung giờ phù hợp</p>
          </div>
        </div>
        <span class="step-badge">Trực tuyến</span>
      </div>

      <div class="panel-body">
        <div v-if="errorMessage" class="alert alert-error" role="alert">
          <span class="alert-icon">⚠️</span>
          <span>{{ errorMessage }}</span>
        </div>

        <div v-if="success" class="alert alert-success" role="status">
          <span class="alert-icon">✅</span>
          <div>
            <strong>Đặt lịch thành công!</strong>
            <p>Đã ghi nhận lịch hẹn cho <strong>{{ success.patientName }}</strong> vào <strong>{{ formatDate(success.appointmentDate) }}</strong> lúc <strong>{{ success.slotTime.slice(0, 5) }}</strong>.</p>
            <div style="margin-top: 0.5rem;">
              <button type="button" class="button button-sm button-secondary" @click="$emit('success', success)">
                Xem trong danh sách lịch hẹn →
              </button>
            </div>
          </div>
        </div>

        <form @submit.prevent="submit">
          <div v-if="wards.length > 0" class="form-group">
            <label for="booking-target" class="form-label">
              <span>Đặt lịch cho bệnh nhân <b class="req">*</b></span>
            </label>
            <select id="booking-target" v-model.number="selectedPatientId" :disabled="submitting || loadingProfiles" class="form-select">
              <option v-if="patient" :value="patient.patientId">Bản thân: {{ patient.fullName }} ({{ patient.patientCode }})</option>
              <option v-for="w in wards" :key="w.wardPatientId" :value="w.wardPatientId">
                Trẻ: {{ w.fullName }} ({{ w.wardPatientCode }}) · {{ w.relationship }}
              </option>
            </select>
          </div>

          <div v-if="currentProfile" class="profile-preview-box">
            <span class="preview-icon">👤</span>
            <div>
              <span class="preview-label">Hồ sơ khám:</span>
              <strong class="preview-name">{{ currentProfile.fullName }}</strong>
              <span class="code-badge">{{ currentProfile.code }}</span>
            </div>
          </div>
          <div v-else-if="!loadingProfiles" class="hint-box">
            <span>Chưa tìm thấy hồ sơ cá nhân. Liên hệ Lễ tân tại phòng khám để đối soát thông tin.</span>
          </div>

          <div class="fields-row">
            <div class="form-group">
              <label for="appointment-date" class="form-label">
                <span>Ngày khám <b class="req">*</b></span>
              </label>
              <input id="appointment-date" v-model="appointmentDate" type="date" :min="today" :disabled="submitting" required class="form-input" />
            </div>

            <div class="form-group">
              <label for="appointment-slot" class="form-label">
                <span>Khung giờ hẹn <b class="req">*</b></span>
                <small v-if="slotDurationMinutes" class="label-hint">({{ slotDurationMinutes }} phút/ca)</small>
              </label>
              <select id="appointment-slot" v-model="slotTime" :disabled="submitting || scheduleLoading" required class="form-select">
                <option value="" disabled>Chọn khung giờ</option>
                <option v-for="slot in slots" :key="slot" :value="slot">{{ slot }}</option>
              </select>
            </div>
          </div>

          <div v-if="scheduleError" class="schedule-error-wrap" role="alert">
            <span>{{ scheduleError }}</span>
            <button type="button" class="button button-sm button-secondary" :disabled="submitting || scheduleLoading" @click="loadSchedule">
              Tải lại giờ
            </button>
          </div>

          <div class="form-group">
            <label for="appointment-notes" class="form-label">
              <span>Lý do khám bệnh <small class="text-muted">(không bắt buộc)</small></span>
            </label>
            <textarea
              id="appointment-notes"
              v-model.trim="notes"
              :disabled="submitting"
              rows="3"
              maxlength="500"
              placeholder="Ví dụ: Đau nhức răng hàm dưới, kiểm tra răng định kỳ, cạo vôi răng…"
              class="form-textarea"
            />
            <span class="char-count">{{ notes.length }}/500 ký tự</span>
          </div>

          <div class="form-actions">
            <button type="submit" class="button button-primary button-lg" :disabled="submitting || loadingProfiles || !selectedPatientId">
              <span v-if="submitting" class="spin-icon">⏳</span>
              <span>{{ submitting ? 'Đang đặt lịch…' : 'Xác nhận đặt lịch hẹn' }}</span>
            </button>
          </div>
        </form>
      </div>
    </div>

    <aside class="booking-aside">
      <div class="panel summary-card">
        <h3 class="aside-title">Tóm tắt lịch hẹn</h3>
        <p class="aside-subtitle">Thông tin dự kiến gửi tới phòng khám</p>

        <div class="summary-list">
          <div class="summary-item">
            <span class="item-label">Bệnh nhân</span>
            <span class="item-val">{{ currentProfile?.fullName || 'Chưa xác định' }}</span>
          </div>
          <div class="summary-item">
            <span class="item-label">Ngày khám</span>
            <span class="item-val">{{ appointmentDate ? formatDate(appointmentDate) : 'Chưa chọn ngày' }}</span>
          </div>
          <div class="summary-item">
            <span class="item-label">Giờ khám</span>
            <span class="item-val highlight">{{ slotTime || 'Chưa chọn giờ' }}</span>
          </div>
        </div>
      </div>

      <div class="panel notice-card">
        <div class="notice-header">
          <span class="notice-icon">ℹ️</span>
          <h4>Lưu ý trước khi đến khám</h4>
        </div>
        <ul class="notice-list">
          <li>Vui lòng mang theo <strong>CCCD / Hộ chiếu</strong> để nhân viên đối soát hồ sơ tại quầy.</li>
          <li>Đến trước giờ hẹn <strong>10–15 phút</strong> để đo sinh hiệu và kiểm tra tiền sử dị ứng thuốc.</li>
          <li>Bạn có thể tự đổi hoặc hủy lịch trực tuyến tối thiểu trước <strong>2 giờ</strong>.</li>
        </ul>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { patientGuardiansService, type WardSummary } from '@/services/patientGuardians';
import { useBookingSchedule } from '@/composables/useBookingSchedule';

const emit = defineEmits<{
  (e: 'success', appointment: AppointmentResponse): void;
}>();

const patient = ref<PatientSummary | null>(null);
const wards = ref<WardSummary[]>([]);
const selectedPatientId = ref<number>(0);
const appointmentDate = ref('');
const slotTime = ref('');
const notes = ref('');
const loadingProfiles = ref(true);
const submitting = ref(false);
const errorMessage = ref('');
const success = ref<AppointmentResponse | null>(null);
let version = 0;

const today = computed(() => {
  const date = new Date();
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 10);
});

const currentProfile = computed(() => {
  if (patient.value && selectedPatientId.value === patient.value.patientId) {
    return { fullName: patient.value.fullName, code: patient.value.patientCode };
  }
  const ward = wards.value.find(w => w.wardPatientId === selectedPatientId.value);
  if (ward) {
    return { fullName: ward.fullName, code: ward.wardPatientCode };
  }
  return null;
});

const { slots, revision: scheduleRevision, duration: slotDurationMinutes, loading: scheduleLoading, error: scheduleError, load: loadSchedule } = useBookingSchedule();
watch(scheduleRevision, () => { slotTime.value = ''; });

onMounted(async () => {
  const ticket = version;
  try {
    const [pResult, wResult] = await Promise.allSettled([
      patientsService.getMyPatient(),
      patientGuardiansService.getMyWards()
    ]);
    if (ticket !== version) return;
    if (pResult.status === 'fulfilled') {
      patient.value = pResult.value;
      selectedPatientId.value = pResult.value.patientId;
    }
    if (wResult.status === 'fulfilled') {
      wards.value = wResult.value;
      if (!selectedPatientId.value && wResult.value.length > 0) {
        selectedPatientId.value = wResult.value[0].wardPatientId;
      }
    }
    if (!patient.value && wards.value.length === 0) {
      errorMessage.value = 'Không thể tải hồ sơ của bạn. Hãy đăng nhập lại hoặc thử sau.';
    }
  } catch {
    if (ticket === version) errorMessage.value = 'Không thể tải hồ sơ của bạn. Hãy đăng nhập lại hoặc thử sau.';
  } finally {
    if (ticket === version) loadingProfiles.value = false;
  }
});

async function submit() {
  if (submitting.value || loadingProfiles.value || scheduleLoading.value || !scheduleRevision.value) return;
  errorMessage.value = '';
  success.value = null;

  if (!selectedPatientId.value || !appointmentDate.value || !slots.value.includes(slotTime.value) || notes.value.length > 500) {
    errorMessage.value = 'Vui lòng chọn hồ sơ, ngày và khung giờ khám.';
    return;
  }

  submitting.value = true;
  const ticket = version;
  try {
    const result = await appointmentsService.create({
      patientId: selectedPatientId.value,
      appointmentDate: appointmentDate.value,
      slotTime: `${slotTime.value}:00`,
      notes: notes.value || null,
      expectedScheduleRevision: scheduleRevision.value
    });
    if (ticket !== version) return;
    success.value = result;
    notes.value = '';
    emit('success', result);
  } catch (error) {
    if (ticket !== version) return;
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    if (response?.code === 'APP_SCHEDULE') { slotTime.value = ''; await loadSchedule(); }
    if (ticket !== version) return;
    errorMessage.value = response?.message
      ?? response?.errors?.[0]?.message
      ?? 'Không thể đặt lịch. Khung giờ có thể đã đầy hoặc thông tin không hợp lệ.';
  } finally {
    if (ticket === version) submitting.value = false;
  }
}

function formatDate(value: string): string {
  if (!value) return '';
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}

onUnmounted(() => { version++; patient.value = null; wards.value = []; notes.value = ''; success.value = null; });
</script>

<style scoped>
.booking-grid {
  display: grid;
  grid-template-columns: 1fr 340px;
  gap: 1.5rem;
}

@media (max-width: 860px) {
  .booking-grid {
    grid-template-columns: 1fr;
  }
}

.panel {
  background: #ffffff;
  border-radius: 12px;
  border: 1px solid #e2e8f0;
}

.form-panel {
  padding: 1.5rem;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.25rem;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 0.85rem;
}

.panel-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.panel-icon {
  font-size: 1.5rem;
}

.panel-title {
  font-size: 1.15rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.15rem 0;
}

.panel-subtitle {
  font-size: 0.825rem;
  color: #64748b;
  margin: 0;
}

.step-badge {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
}

.form-group {
  margin-bottom: 1rem;
}

.form-label {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.85rem;
  font-weight: 750;
  color: #334155;
  margin-bottom: 0.35rem;
}

.req { color: #dc2626; }
.label-hint { color: #64748b; font-weight: normal; font-size: 0.75rem; }

.form-input, .form-select, .form-textarea {
  width: 100%;
  padding: 0.6rem 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font-size: 0.9rem;
  color: #0f172a;
  background: #ffffff;
  box-sizing: border-box;
}

.form-input:focus, .form-select:focus, .form-textarea:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.12);
}

.fields-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

@media (max-width: 520px) {
  .fields-row { grid-template-columns: 1fr; }
}

.profile-preview-box {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.65rem 0.85rem;
  border-radius: 8px;
  margin-bottom: 1rem;
}

.preview-label { font-size: 0.8rem; color: #0369a1; margin-right: 0.35rem; }
.preview-name { font-size: 0.925rem; color: #0c4a6e; margin-right: 0.5rem; }
.code-badge {
  font-size: 0.75rem;
  font-family: monospace;
  background: #e0f2fe;
  color: #0369a1;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.hint-box {
  background: #fffbeb;
  border: 1px solid #fef3c7;
  color: #92400e;
  padding: 0.65rem 0.85rem;
  border-radius: 8px;
  font-size: 0.825rem;
  margin-bottom: 1rem;
}

.char-count {
  display: block;
  text-align: right;
  font-size: 0.75rem;
  color: #94a3b8;
  margin-top: 0.25rem;
}

.schedule-error-wrap {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fecaca;
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  font-size: 0.825rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  padding: 0.55rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 750;
  cursor: pointer;
  border: 1px solid transparent;
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
  box-shadow: 0 1px 3px rgba(2, 132, 199, 0.25);
}
.button-primary:hover:not(:disabled) {
  background: #0369a1;
}

.button-secondary {
  background: #ffffff;
  color: #334155;
  border-color: #cbd5e1;
}
.button-secondary:hover:not(:disabled) {
  background: #f8fafc;
  border-color: #94a3b8;
}

.button-sm {
  padding: 0.35rem 0.75rem;
  font-size: 0.8rem;
}
.button-lg {
  padding: 0.75rem 1.5rem;
  font-size: 1rem;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.alert {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  font-size: 0.875rem;
  line-height: 1.45;
  margin-bottom: 1rem;
}
.alert-success { background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fecaca; }
.alert-icon { font-size: 1.15rem; flex-shrink: 0; }

.booking-aside {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.summary-card, .notice-card {
  padding: 1.15rem;
}

.aside-title {
  font-size: 1rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.15rem 0;
}

.aside-subtitle {
  font-size: 0.785rem;
  color: #64748b;
  margin: 0 0 0.85rem 0;
}

.summary-list {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.summary-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.85rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px dashed #e2e8f0;
}

.summary-item:last-child { border-bottom: none; }
.item-label { color: #64748b; }
.item-val { font-weight: 750; color: #0f172a; }
.item-val.highlight { color: #0284c7; }

.notice-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.65rem;
}

.notice-header h4 {
  font-size: 0.9rem;
  font-weight: 800;
  margin: 0;
  color: #0f172a;
}

.notice-list {
  margin: 0;
  padding-left: 1.15rem;
  font-size: 0.825rem;
  color: #475569;
  line-height: 1.5;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
</style>
