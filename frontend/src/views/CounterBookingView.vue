<template>
  <main class="page">
    <div class="header-banner">
      <div class="header-icon">🛎️</div>
      <div class="header-titles">
        <h1>Đặt lịch khám tại quầy</h1>
        <p class="subtitle">Lễ tân hỗ trợ tìm kiếm hồ sơ và đặt lịch hẹn trực tiếp cho bệnh nhân.</p>
      </div>
    </div>

    <div v-if="slotDurationMinutes" class="info-banner">
      ⏱️ Độ dài mỗi khung hẹn chuẩn: <strong>{{ slotDurationMinutes }} phút</strong>.
    </div>

    <div v-if="scheduleError" class="alert-box alert-error" role="alert">
      ⚠️ {{ scheduleError }}
      <button type="button" class="btn btn-outline-sm" :disabled="busy || scheduleLoading" @click="loadSchedule">Tải lại khung giờ</button>
    </div>
    <div v-if="scheduleLoading" class="loading-state-sm" role="status">Đang đồng bộ khung giờ khám…</div>

    <!-- Step 1: Search Patient -->
    <div class="card search-card">
      <div class="card-header">
        <h2>1. Tìm kiếm hồ sơ bệnh nhân</h2>
      </div>
      <form class="search-form" @submit.prevent="search">
        <div class="form-group flex-1">
          <label for="lookup">Nhập tên, số điện thoại hoặc mã hồ sơ</label>
          <div class="input-with-icon">
            <span class="input-icon">🔍</span>
            <input
              id="lookup"
              v-model.trim="keyword"
              placeholder="VD: Nguyễn Văn A, 0912..., BN0001"
              maxlength="200"
              :disabled="busy"
              autocomplete="off"
            />
          </div>
        </div>
        <button class="btn btn-primary" :disabled="busy || searching || !keyword.trim()">
          {{ searching ? 'Đang tìm…' : 'Tìm kiếm' }}
        </button>
      </form>

      <div v-if="searched && !patients.length" class="empty-state">
        <p>Không tìm thấy hồ sơ nào phù hợp. Vui lòng thử từ khóa khác hoặc tạo hồ sơ bệnh nhân mới.</p>
      </div>

      <div v-if="patients.length" class="patient-results">
        <p class="results-hint">Hiển thị tối đa 20 kết quả; chọn đúng bệnh nhân để chuyển sang bước chọn giờ:</p>
        <ul class="patient-list">
          <li v-for="patient in patients" :key="patient.patientId">
            <button
              type="button"
              class="patient-item-btn"
              :class="{ selected: selected?.patientId === patient.patientId }"
              :disabled="busy || searching"
              :aria-pressed="selected?.patientId === patient.patientId"
              @click="choose(patient)"
            >
              <div class="patient-main">
                <span class="patient-name">{{ patient.fullName }}</span>
                <span class="patient-code">#{{ patient.patientCode }}</span>
              </div>
              <div class="patient-sub">
                <span>🎂 Ngày sinh: {{ patient.dateOfBirth }}</span>
              </div>
            </button>
          </li>
        </ul>
      </div>
    </div>
    <!-- Step 2: Booking Form -->
    <div v-if="selected" class="card booking-card">
      <div class="card-header">
        <h2>2. Lập lịch hẹn cho {{ selected.fullName }} <span class="badge-code">#{{ selected.patientCode }}</span></h2>
      </div>

      <form class="booking-form" @submit.prevent="book">
        <div class="form-grid">
          <div class="form-group">
            <label for="dentist">Nha sĩ phụ trách (tùy chọn)</label>
            <div class="select-with-btn">
              <select id="dentist" v-model.number="dentistId" :disabled="busy || loadingDentists" @change="bookingDetailsChanged">
                <option :value="0">Chưa phân nha sĩ (phòng khám phân sau)</option>
                <option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">
                  {{ dentist.dentistName }} · #{{ dentist.dentistId }}
                </option>
              </select>
              <button type="button" class="btn btn-outline-sm" :disabled="busy || loadingDentists" @click="loadDentists" title="Tải lại nha sĩ">🔄</button>
            </div>
            <p v-if="loadingDentists" class="loading-state-sm" role="status">Đang tải nha sĩ…</p>
            <p v-if="dentistError" class="alert-box alert-error" role="alert">{{ dentistError }}</p>
          </div>

          <div class="form-group">
            <label for="day">Ngày khám dự kiến <span class="required">*</span></label>
            <input id="day" v-model="day" type="date" :min="today" :disabled="busy" required @change="bookingDetailsChanged" />
          </div>

          <div class="form-group">
            <label for="slot">Khung giờ hẹn <span class="required">*</span></label>
            <select id="slot" v-model="slot" :disabled="busy" required @change="bookingDetailsChanged">
              <option value="" disabled>-- Chọn khung giờ --</option>
              <option v-for="time in slots" :key="time" :value="time">{{ time }}</option>
            </select>
          </div>
        </div>

        <div class="form-group">
          <label for="reason">Lý do khám / Ghi chú cần lưu ý</label>
          <textarea
            id="reason"
            v-model.trim="notes"
            maxlength="500"
            rows="3"
            placeholder="Ghi chú triệu chứng đau, tình trạng đặc biệt cần bác sĩ lưu ý..."
            :disabled="busy"
            aria-describedby="booking-note-hint"
          ></textarea>
          <p id="booking-note-hint" class="hint-text">
            ℹ️ Ghi chú thông tin tình trạng để nhân viên tiếp đón đối chiếu; lịch hẹn vẫn áp dụng khung giờ đã chọn.
          </p>
        </div>

        <label class="confirm-checkbox">
          <input v-model="confirmed" type="checkbox" :disabled="busy" />
          <span>Tôi đã kiểm tra kỹ thông tin hồ sơ bệnh nhân, ngày khám và khung giờ trước khi xác nhận.</span>
        </label>

        <button class="btn btn-primary btn-submit" :disabled="busy || searching || loadingDentists || !confirmed">
          {{ busy ? 'Đang tạo lịch hẹn…' : '✅ Xác nhận đặt lịch tại quầy' }}
        </button>
      </form>
    </div>

    <div v-if="error" class="alert-box alert-error" role="alert">⚠️ {{ error }}</div>
    <div v-if="success" class="alert-box alert-success" role="status">
      ✅ Đã đặt lịch thành công <strong>#{{ success.appointmentId }}</strong> cho bệnh nhân <strong>{{ success.patientName }}</strong> vào ngày {{ success.appointmentDate }} lúc {{ success.slotTime.slice(0, 5) }} (Nha sĩ: {{ success.dentistName || 'Chưa phân công' }}).
    </div>
    <CounterPatientPhoneRequest v-if="success" :key="success.appointmentId" :appointment-id="success.appointmentId" />
  </main>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import CounterPatientPhoneRequest from '@/components/CounterPatientPhoneRequest.vue';
import { patientsService, type PatientSummary } from '@/services/patients';
import { appointmentsService, type AppointmentResponse, type BookingDentist } from '@/services/appointments';
import { useBookingSchedule } from '@/composables/useBookingSchedule';
const keyword = ref(''), patients = ref<PatientSummary[]>([]), selected = ref<PatientSummary | null>(null);
const day = ref(''), slot = ref(''), notes = ref(''), confirmed = ref(false);
const dentists = ref<BookingDentist[]>([]), dentistId = ref(0), loadingDentists = ref(true), dentistError = ref('');
const searching = ref(false), searched = ref(false), busy = ref(false), error = ref(''), success = ref<AppointmentResponse | null>(null);
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const { slots, revision: scheduleRevision, duration: slotDurationMinutes, loading: scheduleLoading, error: scheduleError, load: loadSchedule } = useBookingSchedule();
watch(scheduleRevision, () => { confirmed.value = false; slot.value = ''; });
let version = 0;
let dentistVersion = 0;
async function loadDentists() {
  if (busy.value) return;
  const ticket = ++dentistVersion; loadingDentists.value = true; dentistError.value = ''; dentists.value = []; confirmed.value = false;
  try {
    const result = await appointmentsService.getBookingDentists();
    if (ticket === dentistVersion) { dentists.value = result; if (!result.some(d => d.dentistId === dentistId.value)) dentistId.value = 0; }
  } catch { if (ticket === dentistVersion) { dentistId.value = 0; dentistError.value = 'Không thể tải nha sĩ. Tải lại để chọn nha sĩ, hoặc đặt lịch chưa phân nha sĩ.'; } }
  finally { if (ticket === dentistVersion) loadingDentists.value = false; }
}
onMounted(loadDentists);
async function search() {
  if (busy.value || searching.value || !keyword.value.trim() || keyword.value.length > 200) return;
  const ticket = ++version; searching.value = true; searched.value = false; error.value = ''; success.value = null;
  patients.value = []; selected.value = null; confirmed.value = false; notes.value = '';
  try {
    const result = await patientsService.search({ keyword: keyword.value.trim(), page: 1, pageSize: 20 });
    if (ticket === version) { patients.value = result.items; searched.value = true; }
  } catch { if (ticket === version) error.value = 'Không thể tìm hồ sơ. Vui lòng thử lại.'; }
  finally { if (ticket === version) searching.value = false; }
}
function choose(patient: PatientSummary) {
  if (busy.value || searching.value || !patients.value.some(p => p.patientId === patient.patientId)) return;
  selected.value = patient; confirmed.value = false; notes.value = ''; success.value = null; error.value = '';
}
function bookingDetailsChanged() { confirmed.value = false; }
async function book() {
  if (busy.value || searching.value || loadingDentists.value || !confirmed.value || !selected.value || success.value) return;
  error.value = '';
  if (!patients.value.some(p => p.patientId === selected.value?.patientId) || !/^\d{4}-\d{2}-\d{2}$/.test(day.value) ||
      !scheduleRevision.value || scheduleLoading.value || day.value < today || !slots.value.includes(slot.value) || notes.value.trim().length > 500 || !Number.isInteger(dentistId.value) ||
      (dentistId.value !== 0 && !dentists.value.some(d => d.dentistId === dentistId.value))) {
    error.value = 'Kiểm tra lại hồ sơ, ngày khám, khung giờ và lý do.'; return;
  }
  const ticket = version; busy.value = true;
  try {
    const result = await appointmentsService.create({
      patientId: selected.value.patientId, appointmentDate: day.value, slotTime: `${slot.value}:00`,
      dentistId: dentistId.value || null, notes: notes.value.trim() || null,
      expectedScheduleRevision: scheduleRevision.value
    });
    if (ticket === version) { success.value = result; notes.value = ''; confirmed.value = false; }
  } catch (cause) {
    if (ticket === version) {
      const data = axios.isAxiosError(cause) ? cause.response?.data as { code?: string; message?: string; details?: unknown } | undefined : undefined;
      if (data?.code === 'APP_SCHEDULE') { slot.value = ''; await loadSchedule(); }
      if (ticket !== version) return;
      confirmed.value = false;
      error.value = data?.message ?? (axios.isAxiosError(cause) ? cause.response?.data?.errors?.[0]?.message ?? 'Không thể đặt lịch. Tải lại để đối chiếu trước khi thử tiếp.' : 'Chưa xác định kết quả. Đối chiếu lịch hẹn trước khi thử tiếp.');
    }
  } finally { if (ticket === version) busy.value = false; }
}
onUnmounted(() => { version++; dentistVersion++; patients.value = []; selected.value = null; dentists.value = []; dentistId.value = 0; notes.value = ''; success.value = null; dentistError.value = ''; });
</script>
<style scoped>
.page { max-width: 860px; margin: 0 auto; padding: 1.5rem; color: #1e293b; box-sizing: border-box; }
.header-banner { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }

.info-banner { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1.25rem; }

.card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }
.card-header h2 { margin: 0 0 1rem; font-size: 1.2rem; color: #0f172a; display: flex; align-items: center; gap: 0.5rem; }
.badge-code { font-size: 0.8rem; background: #e0f2fe; color: #0369a1; padding: 0.2rem 0.5rem; border-radius: 4px; }

.search-form { display: flex; gap: 0.75rem; align-items: flex-end; }
.flex-1 { flex: 1; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.required { color: #dc2626; }
.input-with-icon { position: relative; display: flex; align-items: center; }
.input-icon { position: absolute; left: 0.75rem; font-size: 0.9rem; color: #94a3b8; pointer-events: none; }
.input-with-icon input { width: 100%; height: 44px; padding: 0.5rem 0.75rem 0.5rem 2.25rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; box-sizing: border-box; }

.patient-results { margin-top: 1.25rem; border-top: 1px solid #f1f5f9; padding-top: 1rem; }
.results-hint { font-size: 0.85rem; color: #64748b; margin: 0 0 0.75rem; }
.patient-list { list-style: none; padding: 0; margin: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.75rem; }
.patient-item-btn { width: 100%; padding: 0.75rem 1rem; border: 1px solid #e2e8f0; border-radius: 8px; background: #f8fafc; cursor: pointer; text-align: left; transition: all 0.15s; }
.patient-item-btn:hover:not(:disabled) { background: #f0f9ff; border-color: #bae6fd; }
.patient-item-btn.selected { background: #e0f2fe; border-color: #0284c7; outline: 2px solid #0284c7; }
.patient-main { display: flex; justify-content: space-between; align-items: baseline; }
.patient-name { font-weight: 700; color: #0f172a; font-size: 0.95rem; }
.patient-code { font-size: 0.8rem; color: #64748b; font-weight: 600; }
.patient-sub { font-size: 0.8rem; color: #64748b; margin-top: 0.25rem; }

.booking-form { display: flex; flex-direction: column; gap: 1.25rem; }
.form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.select-with-btn { display: flex; gap: 0.5rem; align-items: center; }
.select-with-btn select { flex: 1; }
select, input[type="date"] { height: 44px; padding: 0.5rem 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; background: #fff; box-sizing: border-box; }
textarea { border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.65rem; font-family: inherit; font-size: 0.95rem; box-sizing: border-box; }
.hint-text { font-size: 0.8rem; color: #64748b; margin: 0.25rem 0 0; }

.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.85rem; color: #334155; }
.btn-submit { width: 100%; height: 48px; font-size: 1.05rem; }

.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0369a1; }
.btn-outline-sm { height: 36px; padding: 0 0.75rem; border-radius: 6px; background: #fff; color: #334155; border: 1px solid #cbd5e1; font-size: 0.85rem; cursor: pointer; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }

.alert-box { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.loading-state-sm { font-size: 0.85rem; color: #64748b; padding: 0.5rem 0; }
.empty-state { text-align: center; padding: 2rem; color: #64748b; background: #f8fafc; border-radius: 8px; margin-top: 1rem; }

@media (max-width: 640px) {
  .page { padding: 1rem; }
  .search-form { flex-direction: column; align-items: stretch; }
  .btn { width: 100%; }
}
</style>
