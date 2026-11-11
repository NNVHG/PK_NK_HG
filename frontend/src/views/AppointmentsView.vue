<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">Đặt hẹn khám</p>
        <h1>Đặt lịch khám điều trị</h1>
        <p class="subtitle">Đặt lịch cho tài khoản cá nhân hoặc người được giám hộ. Hệ thống kiểm tra chỗ trống trực tiếp.</p>
      </div>
      <div class="header-actions">
        <RouterLink to="/patient/appointments/mine" class="button button-secondary">
          <span>📋 Lịch hẹn của tôi</span>
        </RouterLink>
      </div>
    </div>

    <div class="booking-grid">
      <!-- Cột trái: Form đặt lịch -->
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
                <label for="date" class="form-label">
                  <span>Ngày khám <b class="req">*</b></span>
                </label>
                <input id="date" v-model="appointmentDate" type="date" :min="today" :disabled="submitting" required class="form-input" />
              </div>

              <div class="form-group">
                <label for="slot" class="form-label">
                  <span>Khung giờ hẹn <b class="req">*</b></span>
                  <small v-if="slotDurationMinutes" class="label-hint">({{ slotDurationMinutes }} phút/ca)</small>
                </label>
                <select id="slot" v-model="slotTime" :disabled="submitting || scheduleLoading" required class="form-select">
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
              <label for="notes" class="form-label">
                <span>Lý do khám bệnh <small class="text-muted">(không bắt buộc)</small></span>
              </label>
              <textarea
                id="notes"
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
      <!-- Cột phải: Tóm tắt & Hướng dẫn y khoa -->
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
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { patientGuardiansService, type WardSummary } from '@/services/patientGuardians';
import { useBookingSchedule } from '@/composables/useBookingSchedule';

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

  if (!selectedPatientId.value ||
      !appointmentDate.value || !slots.value.includes(slotTime.value) || notes.value.length > 500) {
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
onUnmounted(() => { version++; patient.value = null; wards.value = []; notes.value = ''; success.value = null; });

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}
</script>

<style scoped>
.page-container {
  max-width: 1040px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
}

.eyebrow {
  margin: 0 0 0.25rem;
  color: #0284c7;
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

h1 {
  font-size: 1.65rem;
  font-weight: 850;
  color: #0f2d59;
  margin: 0;
  line-height: 1.25;
}

.subtitle {
  margin: 0.35rem 0 0;
  color: #64748b;
  font-size: 0.885rem;
  line-height: 1.45;
}

.booking-grid {
  display: grid;
  grid-template-columns: 1fr 340px;
  gap: 1.5rem;
  align-items: start;
}

@media (max-width: 860px) {
  .booking-grid {
    grid-template-columns: 1fr;
  }
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid #e2e8f0;
}

.panel-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.panel-icon {
  font-size: 1.35rem;
}

.panel-title {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.panel-subtitle {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #64748b;
}

.step-badge {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.775rem;
  font-weight: 750;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
}

.panel-body {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

/* Forms */
.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin-bottom: 1rem;
}

.fields-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

@media (max-width: 580px) {
  .fields-row {
    grid-template-columns: 1fr;
  }
}

.form-label {
  font-size: 0.835rem;
  font-weight: 700;
  color: #334155;
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.req {
  color: #dc2626;
}

.label-hint {
  font-size: 0.775rem;
  color: #64748b;
  font-weight: normal;
}

.form-input,
.form-select,
.form-textarea {
  width: 100%;
  box-sizing: border-box;
  height: 40px;
  padding: 0 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font: inherit;
  font-size: 0.885rem;
  color: #1e293b;
  background: #ffffff;
  transition: all 0.15s ease;
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
  padding: 0.65rem 0.85rem;
  resize: vertical;
}

.char-count {
  font-size: 0.75rem;
  color: #94a3b8;
  text-align: right;
  margin-top: 0.2rem;
}

.profile-preview-box {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.65rem 0.85rem;
  margin-bottom: 1rem;
}

.preview-icon {
  font-size: 1.25rem;
}

.preview-label {
  font-size: 0.8rem;
  color: #64748b;
  margin-right: 0.35rem;
}

.preview-name {
  color: #0f2d59;
  font-size: 0.885rem;
  margin-right: 0.45rem;
}

.code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.75rem;
  font-weight: 700;
  background: #e2e8f0;
  color: #334155;
  padding: 0.1rem 0.45rem;
  border-radius: 4px;
}

.hint-box {
  background: #fffbeb;
  border: 1px solid #fef3c7;
  color: #92400e;
  padding: 0.75rem;
  border-radius: 8px;
  font-size: 0.835rem;
  margin-bottom: 1rem;
}

.schedule-error-wrap {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
  padding: 0.65rem 0.85rem;
  border-radius: 8px;
  font-size: 0.825rem;
  margin-bottom: 1rem;
}
/* Aside cards */
.booking-aside {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.summary-card,
.notice-card {
  padding: 1.25rem;
}

.aside-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 800;
  color: #0f2d59;
}

.aside-subtitle {
  margin: 0.15rem 0 1rem;
  font-size: 0.785rem;
  color: #64748b;
}

.summary-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.summary-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.835rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px dashed #e2e8f0;
}

.summary-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.item-label {
  color: #64748b;
}

.item-val {
  font-weight: 700;
  color: #1e293b;
}

.item-val.highlight {
  color: #0284c7;
  font-size: 0.95rem;
}

.notice-card {
  background: #f8fafc;
}

.notice-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.65rem;
}

.notice-icon {
  font-size: 1.15rem;
}

.notice-header h4 {
  margin: 0;
  font-size: 0.885rem;
  font-weight: 750;
  color: #0f2d59;
}

.notice-list {
  margin: 0;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  font-size: 0.8rem;
  color: #475569;
  line-height: 1.45;
}

/* Alerts */
.alert {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  font-size: 0.865rem;
  line-height: 1.45;
}

.alert-icon {
  font-size: 1.2rem;
  flex-shrink: 0;
}

.alert-error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
}

.alert-success {
  background: #ecfdf5;
  border: 1px solid #a7f3d0;
  color: #065f46;
}

/* Buttons */
.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  height: 40px;
  padding: 0 1.15rem;
  border: 1px solid transparent;
  border-radius: 8px;
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  cursor: pointer;
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-lg {
  width: 100%;
  height: 44px;
  font-size: 0.95rem;
}

.button-sm {
  height: 32px;
  padding: 0 0.65rem;
  font-size: 0.775rem;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
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
  background: #f1f5f9;
  border-color: #94a3b8;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.spin-icon {
  animation: spin 1s linear infinite;
  display: inline-block;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
