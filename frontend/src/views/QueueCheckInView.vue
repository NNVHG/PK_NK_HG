<template>
  <main class="page">
    <header>
      <h1>Tiếp đón và check-in</h1>
      <p>Tìm hồ sơ bệnh nhân, xác nhận khách đã đến và cấp số thứ tự.</p>
    </header>

    <section class="card">
      <form class="search" @submit.prevent="searchPatients">
        <label for="patient-search">Tìm bệnh nhân theo tên, số điện thoại hoặc mã hồ sơ</label>
        <div class="search-row">
          <input id="patient-search" v-model.trim="keyword" autocomplete="off" placeholder="Nhập thông tin bệnh nhân" />
          <button type="submit" class="secondary" :disabled="searching || !keyword">
            {{ searching ? 'Đang tìm…' : 'Tìm hồ sơ' }}
          </button>
        </div>
      </form>

      <p v-if="searchError" class="message error" role="alert">{{ searchError }}</p>
      <div v-if="patients.length" class="results" aria-label="Kết quả tìm bệnh nhân">
        <button
          v-for="patient in patients"
          :key="patient.patientId"
          type="button"
          class="patient-option"
          :class="{ selected: selectedPatient?.patientId === patient.patientId }"
          :aria-pressed="selectedPatient?.patientId === patient.patientId"
          @click="selectPatient(patient)"
        >
          <span><strong>{{ patient.fullName }}</strong><small>{{ patient.patientCode }} · {{ patient.phone }}</small></span>
          <span v-if="selectedPatient?.patientId === patient.patientId">Đã chọn</span>
        </button>
      </div>
      <p v-else-if="searched" class="hint">Không tìm thấy hồ sơ phù hợp.</p>
    </section>

    <section v-if="selectedPatient" class="card checkin-card">
      <h2>{{ selectedPatient.fullName }} <small>{{ selectedPatient.patientCode }}</small></h2>

      <div v-if="loadingAppointments" class="hint">Đang kiểm tra lịch hẹn hôm nay…</div>
      <div v-else-if="appointmentLookupFailed" class="message error" role="alert">
        <p>Không thể kiểm tra lịch hẹn hôm nay. Vui lòng thử lại trước khi check-in.</p>
        <button type="button" class="secondary" @click="loadAppointments(selectedPatient)">Thử lại</button>
      </div>
      <template v-else-if="appointments.length">
        <label for="appointment">Lịch hẹn hôm nay</label>
        <select id="appointment" v-model.number="appointmentId">
          <option v-for="appointment in appointments" :key="appointment.appointmentId" :value="appointment.appointmentId">
            {{ appointment.slotTime.slice(0, 5) }} · {{ appointment.status }}
          </option>
        </select>
        <p class="hint">Check-in sẽ gắn lượt chờ với lịch đã chọn. Hệ thống tự xác định ưu tiên theo giờ đến.</p>
      </template>
      <p v-else class="hint">Không có lịch hẹn chờ hôm nay. Check-in sẽ được ghi nhận là khách vãng lai.</p>

      <label for="notes">Ghi chú tiếp đón (không bắt buộc)</label>
      <textarea id="notes" v-model.trim="notes" rows="2" maxlength="500" placeholder="Ghi chú ngắn nếu cần" />

      <p v-if="checkInError" class="message error" role="alert">{{ checkInError }}</p>
      <button type="button" :disabled="checkingIn || loadingAppointments || appointmentLookupFailed" @click="submitCheckIn">
        {{ checkingIn ? 'Đang xác nhận…' : 'Xác nhận khách đã đến' }}
      </button>
    </section>

    <section v-if="ticket" class="ticket" role="status" aria-live="polite">
      <p>Đã check-in thành công</p>
      <strong class="number">{{ ticket.queueNumber }}</strong>
      <h2>{{ ticket.patientName }}</h2>
      <p>{{ ticket.queueDate }} · {{ ticket.statusText }}</p>
      <span class="priority" :class="{ normal: !ticket.isPriority }">
        {{ ticket.isPriority ? 'Ưu tiên theo lịch hẹn' : 'Thứ tự chờ thông thường' }}
      </span>
      <button type="button" class="secondary" @click="resetForm">Check-in bệnh nhân khác</button>
    </section>
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { queueService, type QueueEntryResponse } from '@/services/queue';

const keyword = ref('');
const patients = ref<PatientSummary[]>([]);
const selectedPatient = ref<PatientSummary | null>(null);
const appointments = ref<AppointmentResponse[]>([]);
const appointmentId = ref<number | null>(null);
const notes = ref('');
const searching = ref(false);
const searched = ref(false);
const loadingAppointments = ref(false);
const appointmentLookupFailed = ref(false);
const checkingIn = ref(false);
const searchError = ref('');
const checkInError = ref('');
const ticket = ref<QueueEntryResponse | null>(null);

async function searchPatients() {
  searching.value = true;
  searched.value = false;
  searchError.value = '';
  selectedPatient.value = null;
  appointments.value = [];
  appointmentId.value = null;
  ticket.value = null;
  try {
    const result = await patientsService.search({ keyword: keyword.value, page: 1, pageSize: 20 });
    patients.value = result.items;
    searched.value = true;
  } catch {
    searchError.value = 'Không thể tìm hồ sơ bệnh nhân. Vui lòng thử lại.';
  } finally {
    searching.value = false;
  }
}

async function selectPatient(patient: PatientSummary) {
  selectedPatient.value = patient;
  await loadAppointments(patient);
}

async function loadAppointments(patient: PatientSummary) {
  appointments.value = [];
  appointmentId.value = null;
  appointmentLookupFailed.value = false;
  checkInError.value = '';
  ticket.value = null;
  loadingAppointments.value = true;
  try {
    const result = await appointmentsService.getScheduledForPatientOnDate(patient.patientId, todayLocal());
    appointments.value = result.items;
    appointmentId.value = result.items[0]?.appointmentId ?? null;
  } catch {
    appointmentLookupFailed.value = true;
  } finally {
    loadingAppointments.value = false;
  }
}

async function submitCheckIn() {
  if (!selectedPatient.value) return;
  checkingIn.value = true;
  checkInError.value = '';
  try {
    ticket.value = await queueService.checkIn({
      patientId: selectedPatient.value.patientId,
      appointmentId: appointmentId.value,
      dentistId: null,
      notes: notes.value || null
    });
    notes.value = '';
  } catch (error) {
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    checkInError.value = response?.message
      ?? response?.errors?.[0]?.message
      ?? 'Không thể check-in. Bệnh nhân có thể đã có lượt đang mở hoặc lịch hẹn đã được tiếp đón.';
  } finally {
    checkingIn.value = false;
  }
}

function resetForm() {
  keyword.value = '';
  patients.value = [];
  selectedPatient.value = null;
  appointments.value = [];
  appointmentId.value = null;
  appointmentLookupFailed.value = false;
  notes.value = '';
  searched.value = false;
  ticket.value = null;
  searchError.value = '';
  checkInError.value = '';
}

function todayLocal(): string {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}
</script>

<style scoped>
.page { max-width: 760px; margin: 0 auto; padding: 2rem 1rem; color: #243447; }
header { margin-bottom: 1.25rem; }
h1 { margin: 0 0 .4rem; font-size: 1.6rem; }
header p, .hint { color: #607080; }
.card, .ticket { margin-bottom: 1rem; padding: 1.25rem; background: #fff; border: 1px solid #e2e8ef; border-radius: 10px; box-shadow: 0 3px 12px #18324b0d; }
.search { display: grid; gap: .6rem; }
label { display: block; margin-bottom: .4rem; font-weight: 600; }
.search-row { display: flex; gap: .5rem; }
input, select, textarea { width: 100%; box-sizing: border-box; padding: .7rem .75rem; border: 1px solid #cbd5df; border-radius: 6px; font: inherit; background: #fff; }
textarea { resize: vertical; }
.results { display: grid; gap: .5rem; margin-top: 1rem; }
.patient-option { display: flex; align-items: center; justify-content: space-between; gap: .75rem; padding: .75rem; color: inherit; text-align: left; background: #fff; border: 1px solid #d5dee7; border-radius: 6px; cursor: pointer; }
.patient-option.selected { border-color: #13795b; background: #edf8f1; }
.patient-option small { display: block; margin-top: .2rem; color: #607080; }
.checkin-card { display: grid; gap: .55rem; }
.checkin-card h2 { margin: 0 0 .4rem; }
.checkin-card h2 small { color: #607080; font-size: .85rem; font-weight: 500; }
.hint, .message { margin: .15rem 0 .5rem; font-size: .92rem; }
.error { color: #a32323; }
button { padding: .75rem 1rem; border: 0; border-radius: 6px; color: #fff; background: #13795b; font: inherit; font-weight: 700; cursor: pointer; }
button.secondary { flex: 0 0 auto; padding: .65rem .85rem; color: #174e3d; background: #e7f3ee; }
button:disabled { cursor: wait; opacity: .6; }
.ticket { text-align: center; border-color: #b8dfca; background: #f3fbf6; }
.ticket > p:first-child { color: #176b45; font-weight: 700; }
.number { display: inline-grid; width: 5rem; height: 5rem; place-items: center; border-radius: 50%; color: #fff; background: #13795b; font-size: 2rem; }
.ticket h2 { margin: .75rem 0 .3rem; }
.priority { display: inline-block; margin-bottom: 1rem; padding: .35rem .7rem; border-radius: 999px; color: #176b45; background: #d9f0e2; }
.priority.normal { color: #5d4b0e; background: #f7efcc; }
.ticket button { display: block; margin: 0 auto; }
@media (max-width: 520px) { .search-row { flex-direction: column; } }
</style>
