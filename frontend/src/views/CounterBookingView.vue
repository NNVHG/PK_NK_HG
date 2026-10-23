<template>
  <main class="page">
    <h1>Đặt lịch tại quầy</h1>
    <p v-if="slotDurationMinutes">Độ dài khung hẹn mới: {{ slotDurationMinutes }} phút.</p>
    <p v-if="scheduleError" role="alert">{{ scheduleError }} <button type="button" :disabled="busy || scheduleLoading" @click="loadSchedule">Tải lại giờ</button></p>
    <p v-if="scheduleLoading" role="status">Đang tải khung giờ…</p>
    <p>Chọn hồ sơ bệnh nhân để đặt lịch. Khách chỉ vào hàng đợi khi được check-in.</p>
    <form @submit.prevent="search">
      <label for="lookup">Tìm theo tên, SĐT hoặc mã hồ sơ</label>
      <input id="lookup" v-model.trim="keyword" maxlength="200" :disabled="busy" autocomplete="off" />
      <button :disabled="busy || searching || !keyword.trim()">{{ searching ? 'Đang tìm…' : 'Tìm hồ sơ' }}</button>
    </form>
    <p v-if="searched && !patients.length">Không tìm thấy hồ sơ. Thử từ khóa khác hoặc tạo hồ sơ tại màn hình bệnh nhân.</p>
    <p v-if="patients.length">Hiển thị tối đa 20 kết quả; thu hẹp từ khóa nếu chưa thấy hồ sơ.</p>
    <ul><li v-for="patient in patients" :key="patient.patientId">
      <button type="button" :disabled="busy || searching" :aria-pressed="selected?.patientId === patient.patientId" @click="choose(patient)">
        {{ patient.fullName }} · {{ patient.dateOfBirth }} · {{ patient.patientCode }}
      </button>
    </li></ul>
    <form v-if="selected" @submit.prevent="book">
      <h2>{{ selected.fullName }} · {{ selected.patientCode }}</h2>
      <label for="dentist">Nha sĩ (tùy chọn)</label>
      <select id="dentist" v-model.number="dentistId" :disabled="busy || loadingDentists" @change="bookingDetailsChanged">
        <option :value="0">Chưa phân nha sĩ</option>
        <option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">{{ dentist.dentistName }} · #{{ dentist.dentistId }}</option>
      </select>
      <p v-if="loadingDentists" role="status">Đang tải nha sĩ hoạt động…</p>
      <p v-if="dentistError" role="alert">{{ dentistError }}</p>
      <button type="button" :disabled="busy || loadingDentists" @click="loadDentists">Tải lại nha sĩ</button>
      <label for="day">Ngày khám</label><input id="day" v-model="day" type="date" :min="today" :disabled="busy" required @change="bookingDetailsChanged" />
      <label for="slot">Khung giờ</label><select id="slot" v-model="slot" :disabled="busy" required @change="bookingDetailsChanged">
        <option value="" disabled>Chọn giờ</option><option v-for="time in slots" :key="time">{{ time }}</option>
      </select>
      <label for="reason">Lý do khám / ghi chú cần lưu ý (tùy chọn, tối đa 500 ký tự)</label><textarea id="reason" v-model.trim="notes" maxlength="500" :disabled="busy" aria-describedby="booking-note-hint" />
      <p id="booking-note-hint">Ghi tình trạng bệnh nặng hoặc điều cần lưu ý để nhân viên đối chiếu. Lịch hẹn vẫn theo khung giờ đã chọn.</p>
      <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Đã đối chiếu hồ sơ, nha sĩ, ngày, giờ với khách</label>
      <button :disabled="busy || searching || loadingDentists || !confirmed">{{ busy ? 'Đang đặt lịch…' : 'Xác nhận đặt lịch' }}</button>
    </form>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="success" role="status">Đã đặt lịch #{{ success.appointmentId }} cho {{ success.patientName }} · {{ success.appointmentDate }} · {{ success.slotTime.slice(0, 5) }} · {{ success.dentistName || 'Chưa phân nha sĩ' }}.</p>
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
.page { max-width: 680px; margin: auto; padding: 1.5rem; }
form { display: grid; gap: .6rem; margin: 1rem 0; padding: 1rem; border: 1px solid #ddd; border-radius: 8px; }
input, select, textarea, button { font: inherit; padding: .6rem; }
ul { list-style: none; padding: 0; } li { margin: .4rem 0; }
button[aria-pressed=true] { border: 2px solid #13795b; } [role=alert] { color: #b91c1c; } [role=status] { color: #13795b; }
</style>
