<template>
  <main v-if="canCheckIn" class="page">
    <header>
      <h1>Tiếp đón và check-in</h1>
      <p>Tìm hồ sơ, đối chiếu CCCD và số điện thoại tại quầy, sau đó xác nhận khách đã đến. Lượt chưa khám được đóng sau 23:30 giờ Việt Nam; khi quay lại sẽ nhận lượt mới.</p>
    </header>

    <section class="card" aria-labelledby="today-title">
      <h2 id="today-title">Tiếp đón hôm nay · {{ todayLocal() }}</h2>
      <button class="secondary" :disabled="deskLoading || waitingLoading" @click="refreshToday">Tải lại danh sách</button>
      <h3>Lịch hẹn chờ tiếp đón</h3>
      <p v-if="deskLoading" role="status">Đang tải lịch hẹn…</p>
      <p v-else-if="deskError" class="error" role="alert">{{ deskError }} <button class="secondary" @click="loadDesk(deskPage)">Thử lại</button></p>
      <template v-else>
        <p v-if="!deskRows.length">Chưa có lịch hẹn chờ tiếp đón hôm nay.</p>
        <div class="results">
          <button v-for="row in deskRows" :key="row.appointmentId" type="button" class="patient-option" :disabled="checkingIn || selecting" @click="selectFromDesk(row)">
            <span><strong>{{ row.slotTime.slice(0, 5) }} · {{ row.patientName }}</strong><small>{{ row.patientCode }} · Chờ tiếp đón</small></span>
            <span>Chọn</span>
          </button>
        </div>
      </template>
      <nav class="pagination" aria-label="Phân trang lịch hẹn">
        <button class="secondary" :disabled="deskLoading || deskPage <= 1" @click="loadDesk(deskPage - 1)">Trước</button>
        <span>Trang {{ deskPage }} · {{ deskTotal }} lịch</span>
        <button class="secondary" :disabled="deskLoading || !deskNext" @click="loadDesk(deskPage + 1)">Sau</button>
      </nav>
      <h3>Bệnh nhân đã tiếp đón, đang chờ</h3>
      <p v-if="waitingLoading" role="status">Đang tải hàng chờ…</p>
      <p v-else-if="waitingError" class="error" role="alert">{{ waitingError }} <button class="secondary" @click="loadWaiting(waitingPage)">Thử lại</button></p>
      <template v-else>
        <p v-if="!waitingRows.length">Chưa có bệnh nhân đang chờ.</p>
        <ol class="waiting-list"><li v-for="row in waitingRows" :key="row.queueEntryId"><strong>A-{{ String(row.queueNumber).padStart(3, '0') }} · {{ row.patientName }}</strong><span>{{ row.patientCode }} · {{ row.statusText }}</span></li></ol>
      </template>
      <nav class="pagination" aria-label="Phân trang hàng chờ">
        <button class="secondary" :disabled="waitingLoading || waitingPage <= 1" @click="loadWaiting(waitingPage - 1)">Trước</button>
        <span>Trang {{ waitingPage }} · {{ waitingTotal }} lượt</span>
        <button class="secondary" :disabled="waitingLoading || !waitingNext" @click="loadWaiting(waitingPage + 1)">Sau</button>
      </nav>
      <RouterLink to="/queue/today">Mở hàng đợi để gọi số</RouterLink>
    </section>

    <section class="card">
      <form class="search" @submit.prevent="searchPatients(1)">
        <label for="patient-search">Tìm theo tên, số điện thoại, CCCD hoặc mã hồ sơ</label>
        <div class="search-row">
          <input id="patient-search" v-model.trim="keyword" :disabled="checkingIn" autocomplete="off" maxlength="100" placeholder="Nhập thông tin bệnh nhân" />
          <button type="submit" class="secondary" :disabled="searching || checkingIn || !keyword">
            {{ searching ? 'Đang tìm…' : 'Tìm hồ sơ' }}
          </button>
        </div>
      </form>

      <p v-if="searching" role="status">Đang tìm hồ sơ…</p>
      <p v-if="searchError" class="message error" role="alert">{{ searchError }}</p>
      <div v-if="patients.length" class="results" aria-label="Kết quả tìm bệnh nhân">
        <button
          v-for="patient in patients"
          :key="patient.patientId"
          type="button"
          class="patient-option"
          :class="{ selected: selectedPatient?.patientId === patient.patientId }"
          :aria-pressed="selectedPatient?.patientId === patient.patientId"
          :disabled="checkingIn || selecting"
          @click="selectPatient(patient)"
        >
          <span><strong>{{ patient.fullName }}</strong><small>{{ patient.patientCode }} · {{ patient.phone }}</small></span>
          <span v-if="selectedPatient?.patientId === patient.patientId">Đã chọn</span>
        </button>
      </div>
      <p v-else-if="searched && !searching && !searchError" class="hint">Không tìm thấy hồ sơ phù hợp. <RouterLink to="/patients">Mở hồ sơ bệnh nhân để đối soát hoặc tạo mới</RouterLink>.</p>
      <nav v-if="searched && !searchError" class="pagination" aria-label="Phân trang kết quả tìm">
        <button class="secondary" :disabled="searching || searchPage <= 1" @click="searchPatients(searchPage - 1)">Trước</button>
        <span>Trang {{ searchPage }} · {{ searchTotal }} hồ sơ</span>
        <button class="secondary" :disabled="searching || !searchNext" @click="searchPatients(searchPage + 1)">Sau</button>
      </nav>
      <p v-if="selecting" role="status">Đang tải hồ sơ đã chọn…</p>
    </section>

    <section v-if="selectedPatient" class="card checkin-card">
      <h2>{{ selectedPatient.fullName }} <small>{{ selectedPatient.patientCode }}</small></h2>

      <label for="checkin-citizen-id">CCCD của người bệnh</label>
      <input :aria-invalid="!!fieldErrors.citizenId" aria-describedby="citizen-error" id="checkin-citizen-id" v-model="citizenId" inputmode="numeric" autocomplete="off" maxlength="12" placeholder="Nhập CCCD 12 chữ số" />
      <p v-if="fieldErrors.citizenId" id="citizen-error" class="error">{{ fieldErrors.citizenId }}</p>
      <label for="checkin-phone">Số điện thoại trong hồ sơ</label>
      <input :aria-invalid="!!fieldErrors.phone" aria-describedby="phone-error" id="checkin-phone" v-model="checkInPhone" inputmode="tel" autocomplete="tel" maxlength="10" placeholder="Nhập số điện thoại đã đăng ký" />
      <p v-if="fieldErrors.phone" id="phone-error" class="error">{{ fieldErrors.phone }}</p>
      <p class="hint">Thông tin chỉ dùng để đối chiếu tại quầy. Hệ thống không lưu CCCD rõ.</p>

      <div v-if="debtLoading" class="hint" role="status">Đang kiểm tra công nợ cũ…</div>
      <div v-else-if="debtLookupFailed" class="message debt-error" role="alert">
        <p>Không thể tải công nợ cũ. Bạn vẫn có thể check-in, nhưng cần kiểm tra hóa đơn thủ công.</p>
        <button type="button" class="secondary" @click="loadOutstandingDebt(selectedPatient)">Thử tải lại công nợ</button>
      </div>
      <div v-else-if="outstandingDebt && outstandingDebt.totalOutstandingDebt > 0" class="debt-warning" role="alert">
        <strong>Bệnh nhân còn nợ cũ: {{ formatCurrency(outstandingDebt.totalOutstandingDebt) }}</strong>
        <p>Mã hóa đơn còn dư nợ:</p>
        <ul><li v-for="invoiceCode in outstandingDebt.invoiceCodes" :key="invoiceCode">{{ invoiceCode }}</li></ul>
      </div>

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
        <p v-for="appointment in appointments.filter(a => a.appointmentId === appointmentId && a.notes)" :key="appointment.appointmentId">Ghi chú khi đặt lịch: {{ appointment.notes }}</p>
        <p class="hint">Check-in sẽ gắn lượt chờ với lịch đã chọn. Hệ thống tự xác định ưu tiên theo giờ đến.</p>
      </template>
      <p v-else class="hint">Không có lịch hẹn chờ hôm nay. Check-in sẽ được ghi nhận là khách vãng lai.</p>

      <label for="notes">Ghi chú tiếp đón (không bắt buộc)</label>
      <textarea id="notes" v-model="notes" :aria-invalid="!!fieldErrors.notes" aria-describedby="notes-error" rows="2" maxlength="500" placeholder="Ghi tình trạng bệnh nặng hoặc điều cần lưu ý" />

      <p v-if="fieldErrors.notes" id="notes-error" class="error">{{ fieldErrors.notes }}</p>
      <p v-if="checkInError" class="message error" role="alert">{{ checkInError }}</p>
      <button type="button" :disabled="checkingIn || loadingAppointments || appointmentLookupFailed || debtLoading" @click="submitCheckIn">
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
  <p v-else role="alert">Bạn không có quyền tiếp đón bệnh nhân.</p>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { hashCitizenId, qrPatientIntakeService } from '@/services/qrPatientIntake';
import { validateCheckIn, checkInProblem, type CheckInErrors } from '@/services/checkInForm';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { queueService, type OutstandingDebtResponse, type QueueEntryResponse } from '@/services/queue';

const auth = useAuthStore();
const canCheckIn = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role ?? ''));
const keyword = ref('');
const patients = ref<PatientSummary[]>([]);
const selectedPatient = ref<PatientSummary | null>(null);
const appointments = ref<AppointmentResponse[]>([]);
const appointmentId = ref<number | null>(null);
const notes = ref('');
const citizenId = ref('');
const checkInPhone = ref('');
const searching = ref(false);
const searched = ref(false);
const loadingAppointments = ref(false);
const appointmentLookupFailed = ref(false);
const checkingIn = ref(false);
const searchError = ref('');
const checkInError = ref('');
const fieldErrors = ref<CheckInErrors>({});
const ticket = ref<QueueEntryResponse | null>(null);
const outstandingDebt = ref<OutstandingDebtResponse | null>(null);
const debtLoading = ref(false);
const debtLookupFailed = ref(false);
let debtRequestSequence = 0;

const searchPage = ref(1), searchTotal = ref(0), searchNext = ref(false);
const deskRows = ref<AppointmentResponse[]>([]), deskPage = ref(1), deskTotal = ref(0), deskNext = ref(false);
const deskLoading = ref(false), deskError = ref('');
const waitingRows = ref<QueueEntryResponse[]>([]), waitingPage = ref(1), waitingTotal = ref(0), waitingNext = ref(false);
const waitingLoading = ref(false), waitingError = ref(''), selecting = ref(false);
let searchSequence = 0, deskSequence = 0, waitingSequence = 0, selectionSequence = 0, appointmentSequence = 0;
let searchTimer: ReturnType<typeof setTimeout> | undefined;
let disposed = false;

async function loadDesk(page = 1) {
  if (!canCheckIn.value || disposed || page < 1) return;
  const sequence = ++deskSequence;
  deskLoading.value = true; deskError.value = ''; deskRows.value = []; deskTotal.value = 0; deskNext.value = false; deskPage.value = page;
  try {
    const result = await appointmentsService.getForDesk(todayLocal(), page, 'Scheduled');
    if (sequence !== deskSequence || disposed) return;
    deskRows.value = result.items; deskTotal.value = result.totalCount; deskNext.value = result.hasNextPage;
  } catch { if (sequence === deskSequence) deskError.value = 'Không tải được lịch hẹn hôm nay.'; }
  finally { if (sequence === deskSequence) deskLoading.value = false; }
}
async function loadWaiting(page = 1) {
  if (!canCheckIn.value || disposed || page < 1) return;
  const sequence = ++waitingSequence;
  waitingLoading.value = true; waitingError.value = ''; waitingRows.value = []; waitingTotal.value = 0; waitingNext.value = false; waitingPage.value = page;
  try {
    const result = await queueService.getToday(page, 1);
    if (sequence !== waitingSequence || disposed) return;
    waitingRows.value = result.items; waitingTotal.value = result.totalCount; waitingNext.value = result.hasNextPage;
  } catch { if (sequence === waitingSequence) waitingError.value = 'Không tải được hàng chờ hôm nay.'; }
  finally { if (sequence === waitingSequence) waitingLoading.value = false; }
}
async function refreshToday() { await Promise.all([loadDesk(), loadWaiting()]); }
async function selectFromDesk(row: AppointmentResponse) {
  if (!canCheckIn.value || checkingIn.value || selecting.value) return;
  const sequence = ++selectionSequence;
  selecting.value = true; searchError.value = '';
  try {
    const patient = await patientsService.getById(row.patientId);
    if (sequence !== selectionSequence || disposed) return;
    await selectPatient(patient);
    if (sequence === selectionSequence && appointments.value.some(a => a.appointmentId === row.appointmentId)) appointmentId.value = row.appointmentId;
  } catch { if (sequence === selectionSequence) searchError.value = 'Không tải được hồ sơ. Vui lòng thử lại.'; }
  finally { if (sequence === selectionSequence) selecting.value = false; }
}
function clearSelection() {
  appointmentSequence++; debtRequestSequence++; selectionSequence++;
  selecting.value = false; selectedPatient.value = null; citizenId.value = ''; checkInPhone.value = ''; notes.value = '';
  appointments.value = []; appointmentId.value = null; loadingAppointments.value = false; appointmentLookupFailed.value = false;
  outstandingDebt.value = null; debtLoading.value = false; debtLookupFailed.value = false; fieldErrors.value = {}; checkInError.value = ''; ticket.value = null;
}
async function searchPatients(page = 1) {
  clearTimeout(searchTimer);
  if (!canCheckIn.value || checkingIn.value || disposed || page < 1) return;
  const sequence = ++searchSequence, value = keyword.value.trim();
  clearSelection(); patients.value = []; searched.value = false; searchError.value = ''; searchTotal.value = 0; searchNext.value = false; searchPage.value = page;
  if (!value) { searching.value = false; return; }
  searching.value = true;
  try {
    if (/^[0-9]{12}$/.test(value)) {
      const hash = await hashCitizenId(value);
      if (sequence !== searchSequence || disposed) return;
      const lookup = await qrPatientIntakeService.lookup(hash);
      if (sequence !== searchSequence || disposed) return;
      const patient = lookup.existing?.isActive ? await patientsService.getById(lookup.existing.patientId) : null;
      if (sequence !== searchSequence || disposed) return;
      patients.value = patient ? [patient] : []; searchTotal.value = patients.value.length; searchPage.value = 1;
    } else {
      const result = await patientsService.search({ keyword: value, page, pageSize: 20 });
      if (sequence !== searchSequence || disposed) return;
      patients.value = result.items; searchTotal.value = result.totalCount; searchNext.value = result.hasNextPage;
    }
    searched.value = true;
  } catch { if (sequence === searchSequence) searchError.value = 'Không thể tìm hồ sơ bệnh nhân. Vui lòng thử lại; tìm CCCD cần HTTPS hoặc localhost.'; }
  finally { if (sequence === searchSequence) searching.value = false; }
}
watch(keyword, () => {
  clearTimeout(searchTimer); searchSequence++; clearSelection(); patients.value = []; searched.value = false; searchError.value = ''; searching.value = false;
  if (keyword.value.trim() && canCheckIn.value) searchTimer = setTimeout(() => { void searchPatients(1); }, 350);
});
watch(() => auth.user?.userId, () => { clearTimeout(searchTimer); searchSequence++; deskSequence++; waitingSequence++; clearSelection(); keyword.value = ''; deskRows.value = []; waitingRows.value = []; patients.value = []; });
onMounted(refreshToday);

async function selectPatient(patient: PatientSummary) {
  if (!canCheckIn.value || checkingIn.value || disposed) return;
  notes.value = '';
  selectedPatient.value = patient;
  citizenId.value = '';
  fieldErrors.value = {};
  checkInPhone.value = '';
  await Promise.all([loadAppointments(patient), loadOutstandingDebt(patient)]);
}

async function loadOutstandingDebt(patient: PatientSummary) {
  const requestSequence = ++debtRequestSequence;
  outstandingDebt.value = null;
  debtLookupFailed.value = false;
  debtLoading.value = true;
  try {
    const result = await queueService.getOutstandingDebt(patient.patientId);
    if (requestSequence !== debtRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    outstandingDebt.value = result;
  } catch {
    if (requestSequence !== debtRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    debtLookupFailed.value = true;
  } finally {
    if (requestSequence === debtRequestSequence && selectedPatient.value?.patientId === patient.patientId) {
      debtLoading.value = false;
    }
  }
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(amount);
}

async function loadAppointments(patient: PatientSummary) {
  const sequence = ++appointmentSequence;
  appointments.value = [];
  appointmentId.value = null;
  appointmentLookupFailed.value = false;
  checkInError.value = '';
  ticket.value = null;
  loadingAppointments.value = true;
  try {
    const result = await appointmentsService.getScheduledForPatientOnDate(patient.patientId, todayLocal());
    if (sequence !== appointmentSequence || disposed || selectedPatient.value?.patientId !== patient.patientId) return;
    appointments.value = result.items;
    appointmentId.value = result.items[0]?.appointmentId ?? null;
  } catch {
    if (sequence === appointmentSequence && !disposed) appointmentLookupFailed.value = true;
  } finally {
    if (sequence === appointmentSequence) loadingAppointments.value = false;
  }
}

async function submitCheckIn() {
  if (!canCheckIn.value || !selectedPatient.value || checkingIn.value || loadingAppointments.value || appointmentLookupFailed.value || debtLoading.value) return;
  fieldErrors.value = validateCheckIn(citizenId.value, checkInPhone.value, notes.value);
  checkInError.value = '';
  if (Object.keys(fieldErrors.value).length) return;
  checkingIn.value = true;
  checkInError.value = '';
  try {
    ticket.value = await queueService.checkIn({
      patientId: selectedPatient.value.patientId,
      appointmentId: appointmentId.value,
      dentistId: null,
      notes: notes.value.trim() || null,
      citizenId: citizenId.value.trim(),
      phone: checkInPhone.value.trim()
    });
    notes.value = ''; citizenId.value = ''; checkInPhone.value = ''; selectedPatient.value = null;
    await refreshToday();
  } catch (error) {
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    const problem = checkInProblem(response);
    fieldErrors.value = problem.fields;
    checkInError.value = problem.message;
  } finally {
    checkingIn.value = false;
  }
}

function resetForm() {
  clearTimeout(searchTimer); searchSequence++; clearSelection(); keyword.value = ''; patients.value = []; searched.value = false; searching.value = false; searchError.value = '';
}
onBeforeUnmount(() => {
  disposed = true; clearTimeout(searchTimer); searchSequence++; deskSequence++; waitingSequence++; clearSelection(); keyword.value = ''; patients.value = []; deskRows.value = []; waitingRows.value = [];
});

function todayLocal(): string {
  return new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
}
</script>

<style scoped>
.page { max-width: 760px; margin: 0 auto; padding: 2rem 1rem; color: #243447; }
.pagination { display: flex; flex-wrap: wrap; align-items: center; gap: .6rem; margin: 1rem 0; }
.waiting-list { padding-left: 1.25rem; }
.waiting-list li { margin: .75rem 0; }
.waiting-list span { display: block; color: #607080; }
.page, .card, .patient-option > span { min-width: 0; overflow-wrap: anywhere; }
input, select, textarea { min-width: 0; }
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
.debt-warning { margin: .35rem 0 .6rem; padding: .85rem 1rem; color: #704500; background: #fff5d8; border: 1px solid #efd58b; border-left: 4px solid #d99b16; border-radius: 6px; }
.debt-warning p { margin: .35rem 0 .2rem; }
.debt-warning ul { margin: .2rem 0 0; padding-left: 1.25rem; }
.debt-error { color: #704500; background: #fff8e5; padding: .75rem; border-radius: 6px; }
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
