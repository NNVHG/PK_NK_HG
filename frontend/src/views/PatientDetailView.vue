<template>
  <main class="detail-page">
    <div class="back-row"><router-link :to="{ name: 'patients' }">← Danh sách bệnh nhân</router-link></div>
    <section v-if="loadingPatient" class="panel state-panel" role="status">Đang tải hồ sơ bệnh nhân...</section>
    <section v-else-if="pageError" class="panel state-panel" role="alert">
      <h1>{{ notFound ? 'Không tìm thấy bệnh nhân' : 'Không tải được hồ sơ' }}</h1>
      <p>{{ pageError }}</p>
      <button v-if="!notFound" class="button button-primary" type="button" @click="loadPatient">Thử lại</button>
    </section>
    <template v-else-if="patient">
      <header class="profile-header panel">
        <div>
          <p class="eyebrow">HỒ SƠ BỆNH NHÂN · {{ patient.patientCode }}</p>
          <h1>{{ patient.fullName }}</h1>
          <div class="quick-facts">
            <span>{{ calculateAge(patient.dateOfBirth) }} tuổi</span>
            <span>{{ formatGender(patient.gender) }}</span>
            <span>{{ patient.phone }}</span>
          </div>
        </div>
        <button v-if="canCreateVisit" class="button button-primary" type="button" :disabled="creatingVisit || activeVisitLoading || Boolean(activeVisit)" @click="createVisit">
          {{ creatingVisit ? 'Đang tạo...' : activeVisit ? `Đang có lần khám mở #${activeVisit.visitId}` : '+ Tạo lần khám mới' }}
        </button>
      </header>

      <SafetyAlertBanner :patient-id="patient.patientId" :refresh-key="alertRefreshKey" />
      <p v-if="successMessage" class="notice success" role="status">{{ successMessage }}</p>
      <p v-if="visitError" class="notice error" role="alert">{{ visitError }}</p>

      <nav class="tabs" aria-label="Thông tin bệnh nhân">
        <button v-for="tab in tabs" :key="tab.id" type="button" :class="{ active: activeTab === tab.id }" :aria-current="activeTab === tab.id ? 'page' : undefined" @click="activeTab = tab.id">{{ tab.label }}</button>
      </nav>

      <section v-if="activeTab === 'info'" class="panel content-panel">
        <header class="section-heading">
          <div><p class="eyebrow">THÔNG TIN CƠ BẢN</p><h2>Hồ sơ cá nhân</h2></div>
          <button v-if="canEdit" class="button button-quiet" type="button" @click="openEditForm">Sửa hồ sơ</button>
        </header>
        <dl class="profile-grid">
          <div><dt>Họ và tên</dt><dd>{{ patient.fullName }}</dd></div>
          <div><dt>Mã bệnh nhân</dt><dd>{{ patient.patientCode }}</dd></div>
          <div><dt>Ngày sinh</dt><dd>{{ formatDate(patient.dateOfBirth) }}</dd></div>
          <div><dt>Giới tính</dt><dd>{{ formatGender(patient.gender) }}</dd></div>
          <div><dt>Số điện thoại</dt><dd>{{ patient.phone }}</dd></div>
          <div><dt>Email</dt><dd>{{ patient.email || 'Chưa cập nhật' }}</dd></div>
          <div class="wide"><dt>Địa chỉ</dt><dd>{{ patient.address || 'Chưa cập nhật' }}</dd></div>
        </dl>
      </section>

      <section v-else-if="activeTab === 'timeline'" class="panel content-panel">
        <header class="section-heading"><div><p class="eyebrow">CÁC LẦN KHÁM</p><h2>Lịch sử khám</h2></div></header>
        <p v-if="timelineError" class="notice error" role="alert">{{ timelineError }}</p>
        <p v-else-if="timelineLoading && !timeline.length" class="empty-state">Đang tải lịch sử khám...</p>
        <p v-else-if="!timeline.length" class="empty-state">Chưa có lần khám nào được ghi nhận.</p>
        <ol v-else class="timeline-list">
          <li v-for="visit in timeline" :key="visit.visitId" class="timeline-item">
            <span class="timeline-dot" aria-hidden="true"></span>
            <div class="timeline-card">
              <div class="timeline-top"><strong>Lần khám #{{ visit.visitId }}</strong><span :class="['status-pill', `status-${visit.status.toLowerCase()}`]">{{ formatVisitStatus(visit.status) }}</span></div>
              <p>{{ visit.startedAt ? formatDateTime(visit.startedAt) : 'Chưa bắt đầu' }}<template v-if="visit.endedAt"> — {{ formatDateTime(visit.endedAt) }}</template></p>
              <p>Nha sĩ: {{ visit.dentistName || 'Chưa phân công' }}</p>
              <RouterLink :to="`/clinical/visits/${visit.visitId}/fdi`">Xem tình trạng răng của lần khám</RouterLink>
              <RouterLink v-if="['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(authStore.role)" :to="`/clinical/visits/${visit.visitId}/invoice-draft`">Xem hóa đơn nháp</RouterLink>
              <div class="visit-flags"><span>{{ visit.hasMedicalHistory ? 'Có tiền sử' : 'Chưa ghi tiền sử' }}</span><span>{{ visit.hasVitalSigns ? 'Có sinh hiệu' : 'Chưa ghi sinh hiệu' }}</span></div>
            </div>
          </li>
        </ol>
        <button v-if="timelinePage.hasNextPage" class="button button-quiet load-more" type="button" :disabled="timelineLoading" @click="loadMoreTimeline">{{ timelineLoading ? 'Đang tải...' : 'Xem thêm' }}</button>
      </section>

      <section v-else-if="activeTab === 'history'" class="panel content-panel">
        <header class="section-heading">
          <div><p class="eyebrow">BẢN CHỤP MỚI NHẤT</p><h2>Tiền sử &amp; dị ứng</h2></div>
          <button v-if="canWriteRecords" class="button button-quiet" type="button" :disabled="!activeVisit" @click="openMedicalHistoryForm">Cập nhật tiền sử</button>
        </header>
        <p v-if="openVisitError" class="notice error" role="alert">{{ openVisitError }}</p>
        <p v-else-if="canWriteRecords && !activeVisitLoading && !activeVisit" class="notice info" role="status">
          Cần tạo lần khám đang mở trước khi ghi tiền sử. Dùng nút “Tạo lần khám mới” ở đầu trang.
        </p>
        <form v-if="medicalHistoryFormVisible" class="record-form" @submit.prevent="saveMedicalHistory">
          <p class="form-context">Bản chụp này sẽ gắn với lần khám #{{ activeVisit?.visitId }}. Nội dung được lưu toàn bộ thành một lần ghi nhận mới.</p>
          <div v-for="(item, index) in medicalHistoryForm.items" :key="item.rowId" class="editable-record-row">
            <label>Loại
              <select v-model="item.type"><option value="Allergy">Dị ứng</option><option value="Condition">Bệnh nền</option></select>
            </label>
            <label>Tên mục <input v-model="item.name" maxlength="200" required /></label>
            <label>Chi tiết <input v-model="item.detail" maxlength="1000" /></label>
            <label class="critical-check"><input v-model="item.isCritical" type="checkbox" /> Quan trọng — cần cảnh báo</label>
            <button class="text-button remove-row" type="button" :aria-label="`Xóa mục ${index + 1}`" @click="removeMedicalHistoryItem(index)">Xóa dòng</button>
          </div>
          <button class="button button-quiet" type="button" :disabled="medicalHistoryForm.items.length >= 50" @click="addMedicalHistoryItem">+ Thêm mục</button>
          <label class="field-label">Ghi chú <textarea v-model="medicalHistoryForm.note" rows="3" maxlength="2000" /></label>
          <p v-if="medicalHistoryFormError" class="notice error" role="alert">{{ medicalHistoryFormError }}</p>
          <footer class="modal-actions"><button class="button button-quiet" type="button" :disabled="savingMedicalHistory" @click="medicalHistoryFormVisible = false">Hủy</button><button class="button button-primary" type="submit" :disabled="savingMedicalHistory">{{ savingMedicalHistory ? 'Đang lưu...' : 'Lưu bản chụp mới' }}</button></footer>
        </form>
        <p v-if="historyLoading" class="empty-state">Đang tải tiền sử...</p>
        <p v-else-if="historyError" class="notice error" role="alert">{{ historyError }}</p>
        <template v-else-if="latestHistory">
          <p class="record-meta">Ghi nhận {{ formatDateTime(latestHistory.createdAt) }} · Lần khám #{{ latestHistory.visitId }}</p>
          <p v-if="latestHistory.note" class="history-note">{{ latestHistory.note }}</p>
          <ul v-if="latestHistory.items.length" class="history-list">
            <li v-for="item in latestHistory.items" :key="item.itemId">
              <div><span class="history-kind">{{ item.type === 'Allergy' ? 'Dị ứng' : item.type === 'Condition' ? 'Bệnh nền' : item.type }}</span><strong>{{ item.name }}</strong><span v-if="item.isCritical" class="critical-tag">Cần lưu ý</span></div>
              <p v-if="item.detail">{{ item.detail }}</p>
            </li>
          </ul>
          <p v-else class="empty-state">Bản ghi mới nhất không có mục tiền sử.</p>
        </template>
        <p v-else-if="!historyLoading && !historyError" class="empty-state">Chưa có tiền sử bệnh/dị ứng được ghi nhận.</p>
        <section class="history-archive" aria-label="Lịch sử các lần ghi nhận tiền sử">
          <h3>Lịch sử các lần ghi nhận</h3>
          <p v-if="historyArchiveLoading && !historyRecords.length" class="empty-state">Đang tải lịch sử ghi nhận...</p>
          <p v-else-if="historyArchiveError" class="notice error" role="alert">{{ historyArchiveError }}</p>
          <p v-else-if="!historyRecords.length" class="empty-state">Chưa có lần ghi nhận nào.</p>
          <article v-for="record in historyRecords" :key="record.recordId" class="archive-card">
            <header><strong>{{ formatDateTime(record.createdAt) }}</strong><span>Lần khám #{{ record.visitId }}</span></header>
            <p v-if="record.note">{{ record.note }}</p>
            <ul><li v-for="item in record.items" :key="item.itemId">{{ item.type === 'Allergy' ? 'Dị ứng' : 'Bệnh nền' }}: {{ item.name }}<span v-if="item.isCritical"> · Cần cảnh báo</span></li></ul>
          </article>
          <button v-if="historyArchivePage.hasNextPage" class="button button-quiet load-more" type="button" :disabled="historyArchiveLoading" @click="loadMoreHistory">{{ historyArchiveLoading ? 'Đang tải...' : 'Xem thêm lịch sử' }}</button>
        </section>
      </section>

      <section v-else class="panel content-panel">
        <header class="section-heading">
          <div><p class="eyebrow">GHI NHẬN THEO LẦN KHÁM</p><h2>Sinh hiệu</h2></div>
          <button v-if="canWriteRecords" class="button button-quiet" type="button" :disabled="!activeVisit" @click="openVitalForm">Ghi sinh hiệu</button>
        </header>
        <p v-if="openVisitError" class="notice error" role="alert">{{ openVisitError }}</p>
        <p v-else-if="canWriteRecords && !activeVisitLoading && !activeVisit" class="notice info" role="status">
          Cần tạo lần khám đang mở trước khi ghi sinh hiệu. Dùng nút “Tạo lần khám mới” ở đầu trang.
        </p>
        <p v-if="vitalSignsError" class="notice error" role="alert">{{ vitalSignsError }}</p>
        <form v-if="vitalFormVisible" class="record-form vital-form" @submit.prevent="saveVitalSigns">
          <p class="form-context">Bản ghi sẽ gắn với lần khám #{{ activeVisit?.visitId }}. Không có kết luận tự động từ các chỉ số.</p>
          <label>Huyết áp tâm thu (mmHg) <input v-model="vitalForm.systolicBp" type="number" min="50" max="260" step="1" /></label>
          <label>Huyết áp tâm trương (mmHg) <input v-model="vitalForm.diastolicBp" type="number" min="30" max="160" step="1" /></label>
          <label>Mạch (lần/phút) <input v-model="vitalForm.pulseBpm" type="number" min="20" max="220" step="1" /></label>
          <label>Thân nhiệt (°C) <input v-model="vitalForm.temperatureC" type="number" min="30" max="43" step="0.1" /></label>
          <label class="field-label">Ghi chú <textarea v-model="vitalForm.note" rows="3" maxlength="2000" /></label>
          <p v-if="vitalFormError" class="notice error" role="alert">{{ vitalFormError }}</p>
          <footer class="modal-actions"><button class="button button-quiet" type="button" :disabled="savingVitalSigns" @click="vitalFormVisible = false">Hủy</button><button class="button button-primary" type="submit" :disabled="savingVitalSigns">{{ savingVitalSigns ? 'Đang lưu...' : 'Lưu sinh hiệu' }}</button></footer>
        </form>
        <p v-if="vitalSignsLoading && !vitalSigns.length" class="empty-state">Đang tải sinh hiệu...</p>
        <p v-else-if="!vitalSignsLoading && !vitalSigns.length" class="empty-state">Chưa có sinh hiệu được ghi nhận.</p>
        <section v-else class="vital-history" aria-label="Các lần ghi sinh hiệu">
          <article v-for="record in vitalSigns" :key="record.vitalSignRecordId" class="archive-card">
            <header><strong>{{ formatDateTime(record.createdAt) }}</strong><span>Lần khám #{{ record.visitId }}</span></header>
            <dl class="vital-grid">
              <div><dt>Huyết áp</dt><dd>{{ record.systolicBp ?? '—' }} / {{ record.diastolicBp ?? '—' }} mmHg</dd></div>
              <div><dt>Mạch</dt><dd>{{ record.pulseBpm === null ? '—' : `${record.pulseBpm} lần/phút` }}</dd></div>
              <div><dt>Nhiệt độ</dt><dd>{{ record.temperatureC === null ? '—' : `${record.temperatureC} °C` }}</dd></div>
            </dl>
            <p v-if="record.note">{{ record.note }}</p>
          </article>
          <button v-if="vitalSignsPage.hasNextPage" class="button button-quiet load-more" type="button" :disabled="vitalSignsLoading" @click="loadMoreVitalSigns">{{ vitalSignsLoading ? 'Đang tải...' : 'Xem thêm sinh hiệu' }}</button>
        </section>
      </section>
    </template>

    <div v-if="editVisible" class="modal-backdrop" @click.self="closeEditForm">
      <section class="modal-card" role="dialog" aria-modal="true" aria-labelledby="edit-title">
        <header class="section-heading"><div><p class="eyebrow">CẬP NHẬT HỒ SƠ</p><h2 id="edit-title">Sửa thông tin bệnh nhân</h2></div><button class="close-button" type="button" :disabled="saving" aria-label="Đóng" @click="closeEditForm">×</button></header>
        <form class="edit-form" @submit.prevent="saveEdit">
          <label>Họ và tên <input v-model="editForm.fullName" maxlength="100" required /></label>
          <label>Ngày sinh <input v-model="editForm.dateOfBirth" type="date" required /></label>
          <label>Giới tính <select v-model="editForm.gender"><option value="">Chưa cung cấp</option><option value="Male">Nam</option><option value="Female">Nữ</option><option value="Other">Khác</option></select></label>
          <label>Số điện thoại <input v-model="editForm.phone" type="tel" inputmode="numeric" maxlength="10" required /></label>
          <label>Email <input v-model="editForm.email" type="email" maxlength="100" /></label>
          <label>Địa chỉ <textarea v-model="editForm.address" maxlength="500" rows="3" /></label>
          <p v-if="editError" class="notice error" role="alert">{{ editError }}</p>
          <footer class="modal-actions"><button class="button button-quiet" type="button" :disabled="saving" @click="closeEditForm">Hủy</button><button class="button button-primary" type="submit" :disabled="saving">{{ saving ? 'Đang lưu...' : 'Lưu thay đổi' }}</button></footer>
        </form>
      </section>
    </div>

    <div v-if="duplicateVisible" class="modal-backdrop" @click.self="cancelDuplicate">
      <section class="modal-card" role="dialog" aria-modal="true" aria-labelledby="duplicate-title">
        <p class="eyebrow">KIỂM TRA HỒ SƠ TRÙNG</p><h2 id="duplicate-title">Có hồ sơ có thể trùng</h2>
        <p>Kiểm tra danh sách trước khi xác nhận cập nhật. Hệ thống không tự hợp nhất hồ sơ.</p>
        <ul class="duplicate-list"><li v-for="item in duplicates" :key="item.patientCode"><strong>{{ item.patientCode }} · {{ item.fullName }}</strong><span>Ngày sinh: {{ formatDate(item.dateOfBirth) }} · SĐT: {{ item.phone }}</span></li></ul>
        <p v-if="duplicateError" class="notice error" role="alert">{{ duplicateError }}</p>
        <footer class="modal-actions"><button class="button button-quiet" type="button" :disabled="saving" @click="cancelDuplicate">Hủy</button><button class="button button-primary" type="button" :disabled="saving" @click="confirmDuplicate">{{ saving ? 'Đang lưu...' : 'Đây là hồ sơ khác — vẫn cập nhật' }}</button></footer>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';
import {
  patientsService,
  type MedicalHistoryRecord,
  type MedicalHistoryPayload,
  type PatientApiProblem,
  type PatientDetails,
  type PatientDuplicateInfo,
  type PatientPayload,
  type PatientTimelineItem,
  type PagedResult,
  type VitalSignRecord,
  type VisitDetails
} from '@/services/patients';
import { useAuthStore } from '@/stores/auth';

type TabId = 'info' | 'timeline' | 'history' | 'vitals';
interface EditForm { fullName: string; dateOfBirth: string; gender: string; phone: string; email: string; address: string }
interface MedicalHistoryFormItem { rowId: number; type: 'Allergy' | 'Condition'; name: string; detail: string; isCritical: boolean }
interface VitalForm { systolicBp: string; diastolicBp: string; pulseBpm: string; temperatureC: string; note: string }

const route = useRoute();
const authStore = useAuthStore();
const patientId = Number(route.params.id);
const patient = ref<PatientDetails | null>(null);
const loadingPatient = ref(true);
const pageError = ref('');
const notFound = ref(false);
const activeTab = ref<TabId>('info');
const tabs: Array<{ id: TabId; label: string }> = [
  { id: 'info', label: 'Thông tin' }, { id: 'timeline', label: 'Lịch sử khám' },
  { id: 'history', label: 'Tiền sử & dị ứng' }, { id: 'vitals', label: 'Sinh hiệu' }
];
const canEdit = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const canCreateVisit = computed(() => ['ADMIN', 'RECEPTIONIST', 'ASSISTANT'].includes(authStore.role));
const canWriteRecords = computed(() => ['ADMIN', 'RECEPTIONIST', 'ASSISTANT'].includes(authStore.role));
const alertRefreshKey = ref(0);
const successMessage = ref('');
const visitError = ref('');
const creatingVisit = ref(false);
const openVisits = ref<VisitDetails[]>([]);
const activeVisit = computed(() => openVisits.value[0] ?? null);
const activeVisitLoading = ref(false);
const openVisitError = ref('');
const timeline = ref<PatientTimelineItem[]>([]);
const timelinePage = ref<PagedResult<PatientTimelineItem>>(emptyPage());
const timelineLoading = ref(false);
const timelineError = ref('');
const latestHistory = ref<MedicalHistoryRecord | null>(null);
const historyLoading = ref(false);
const historyError = ref('');
const historyRecords = ref<MedicalHistoryRecord[]>([]);
const historyArchivePage = ref<PagedResult<MedicalHistoryRecord>>(emptyPage());
const historyArchiveLoading = ref(false);
const historyArchiveError = ref('');
const vitalSigns = ref<VitalSignRecord[]>([]);
const vitalSignsPage = ref<PagedResult<VitalSignRecord>>(emptyPage());
const vitalSignsLoading = ref(false);
const vitalSignsError = ref('');
const medicalHistoryFormVisible = ref(false);
const savingMedicalHistory = ref(false);
const medicalHistoryFormError = ref('');
const medicalHistoryForm = reactive<{ items: MedicalHistoryFormItem[]; note: string }>({ items: [], note: '' });
const vitalFormVisible = ref(false);
const savingVitalSigns = ref(false);
const vitalFormError = ref('');
const vitalForm = reactive<VitalForm>({ systolicBp: '', diastolicBp: '', pulseBpm: '', temperatureC: '', note: '' });
const editVisible = ref(false);
const editError = ref('');
const saving = ref(false);
const editForm = reactive<EditForm>({ fullName: '', dateOfBirth: '', gender: '', phone: '', email: '', address: '' });
const duplicateVisible = ref(false);
const duplicateError = ref('');
const duplicates = ref<PatientDuplicateInfo[]>([]);
const pendingPayload = ref<PatientPayload | null>(null);
let nextMedicalHistoryRowId = 0;

onMounted(() => void loadPatient());

function emptyPage<T>(): PagedResult<T> { return { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0, hasNextPage: false, hasPreviousPage: false }; }

async function loadPatient() {
  if (!Number.isInteger(patientId) || patientId < 1) {
    notFound.value = true;
    pageError.value = 'Mã hồ sơ không hợp lệ.';
    loadingPatient.value = false;
    return;
  }
  loadingPatient.value = true;
  pageError.value = '';
  notFound.value = false;
  try {
    patient.value = await patientsService.getById(patientId);
    await Promise.all([
      loadTimeline(true), loadLatestHistory(), loadHistoryArchive(true),
      loadVitalSigns(true), loadOpenVisits()
    ]);
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response?.status === 404) {
      notFound.value = true;
      pageError.value = 'Hồ sơ bệnh nhân này không tồn tại hoặc không còn khả dụng.';
    } else {
      pageError.value = 'Đã xảy ra lỗi mạng khi tải hồ sơ. Vui lòng thử lại.';
    }
  } finally {
    loadingPatient.value = false;
  }
}

async function loadTimeline(reset: boolean) {
  if (reset) { timeline.value = []; timelinePage.value = emptyPage(); }
  timelineLoading.value = true;
  timelineError.value = '';
  try {
    const nextPage = reset ? 1 : timelinePage.value.page + 1;
    const result = await patientsService.getTimeline(patientId, nextPage, 10);
    timeline.value = reset ? result.items : [...timeline.value, ...result.items];
    timelinePage.value = result;
  } catch (error: unknown) {
    timelineError.value = apiError(error, 'Không tải được lịch sử khám. Vui lòng thử lại.');
  } finally {
    timelineLoading.value = false;
  }
}

function loadMoreTimeline() { if (!timelineLoading.value) void loadTimeline(false); }

async function loadLatestHistory() {
  historyLoading.value = true;
  historyError.value = '';
  try {
    latestHistory.value = await patientsService.getLatestMedicalHistory(patientId);
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response?.status === 404) latestHistory.value = null;
    else historyError.value = apiError(error, 'Không tải được tiền sử bệnh/dị ứng. Vui lòng thử lại.');
  } finally { historyLoading.value = false; }
}

async function loadVitalSigns(reset: boolean) {
  if (reset) { vitalSigns.value = []; vitalSignsPage.value = emptyPage(); }
  vitalSignsLoading.value = true;
  vitalSignsError.value = '';
  try {
    const nextPage = reset ? 1 : vitalSignsPage.value.page + 1;
    const result = await patientsService.getVitalSigns(patientId, nextPage, 10);
    vitalSigns.value = reset ? result.items : [...vitalSigns.value, ...result.items];
    vitalSignsPage.value = result;
  } catch (error: unknown) {
    vitalSignsError.value = apiError(error, 'Không tải được sinh hiệu. Vui lòng thử lại.');
  } finally { vitalSignsLoading.value = false; }
}

function loadMoreVitalSigns() { if (!vitalSignsLoading.value) void loadVitalSigns(false); }

async function loadHistoryArchive(reset: boolean) {
  if (reset) { historyRecords.value = []; historyArchivePage.value = emptyPage(); }
  historyArchiveLoading.value = true;
  historyArchiveError.value = '';
  try {
    const nextPage = reset ? 1 : historyArchivePage.value.page + 1;
    const result = await patientsService.getMedicalHistory(patientId, nextPage, 10);
    historyRecords.value = reset ? result.items : [...historyRecords.value, ...result.items];
    historyArchivePage.value = result;
  } catch (error: unknown) {
    historyArchiveError.value = apiError(error, 'Không tải được lịch sử các lần ghi nhận. Vui lòng thử lại.');
  } finally { historyArchiveLoading.value = false; }
}

function loadMoreHistory() { if (!historyArchiveLoading.value) void loadHistoryArchive(false); }

async function loadOpenVisits() {
  activeVisitLoading.value = true;
  openVisitError.value = '';
  try {
    const result = await patientsService.getVisits(patientId, 1, 100);
    openVisits.value = result.items.filter(visit => visit.status === 'Created' || visit.status === 'InProgress');
  } catch (error: unknown) {
    openVisitError.value = apiError(error, 'Không tải được trạng thái lần khám đang mở. Vui lòng thử lại.');
  } finally { activeVisitLoading.value = false; }
}

async function createVisit() {
  if (creatingVisit.value) return;
  creatingVisit.value = true;
  visitError.value = '';
  successMessage.value = '';
  try {
    const visit = await patientsService.createVisit(patientId);
    openVisits.value = [visit];
    openVisitError.value = '';
    successMessage.value = `Đã tạo lần khám #${visit.visitId}.`;
    activeTab.value = 'timeline';
    await loadTimeline(true);
  } catch (error: unknown) {
    visitError.value = apiError(error, 'Không tạo được lần khám. Vui lòng thử lại.');
    void loadOpenVisits();
  } finally { creatingVisit.value = false; }
}

function newMedicalHistoryItem(): MedicalHistoryFormItem {
  nextMedicalHistoryRowId += 1;
  return { rowId: nextMedicalHistoryRowId, type: 'Allergy', name: '', detail: '', isCritical: false };
}

function openMedicalHistoryForm() {
  if (!activeVisit.value) {
    medicalHistoryFormError.value = 'Cần tạo lần khám đang mở trước khi ghi tiền sử.';
    return;
  }
  medicalHistoryForm.items = latestHistory.value?.items.map(item => ({
    ...newMedicalHistoryItem(),
    type: item.type === 'Condition' ? 'Condition' : 'Allergy',
    name: item.name,
    detail: item.detail ?? '',
    isCritical: item.isCritical
  })) ?? [newMedicalHistoryItem()];
  medicalHistoryForm.note = latestHistory.value?.note ?? '';
  medicalHistoryFormError.value = '';
  medicalHistoryFormVisible.value = true;
}

function addMedicalHistoryItem() {
  if (medicalHistoryForm.items.length < 50) medicalHistoryForm.items.push(newMedicalHistoryItem());
}

function removeMedicalHistoryItem(index: number) {
  medicalHistoryForm.items.splice(index, 1);
}

function validateMedicalHistoryForm(): string | null {
  if (!activeVisit.value) return 'Cần tạo lần khám đang mở trước khi ghi tiền sử.';
  if (medicalHistoryForm.items.length > 50) return 'Mỗi bản chụp chỉ được có tối đa 50 mục.';
  if (medicalHistoryForm.note.length > 2000) return 'Ghi chú không được vượt quá 2000 ký tự.';
  const names = medicalHistoryForm.items.map(item => item.name.trim());
  if (names.some(name => !name)) return 'Tên mục tiền sử không được để trống.';
  if (names.some(name => name.length > 200)) return 'Tên mục tiền sử không được vượt quá 200 ký tự.';
  if (new Set(names.map(name => name.toLocaleLowerCase('vi-VN'))).size !== names.length) {
    return 'Tên mục không được trùng trong cùng một bản chụp.';
  }
  return null;
}

async function saveMedicalHistory() {
  medicalHistoryFormError.value = '';
  const validationError = validateMedicalHistoryForm();
  if (validationError) { medicalHistoryFormError.value = validationError; return; }
  const visitId = activeVisit.value?.visitId;
  if (!visitId) return;
  const payload: MedicalHistoryPayload = {
    note: medicalHistoryForm.note.trim() || null,
    items: medicalHistoryForm.items.map(item => ({
      type: item.type,
      name: item.name.trim(),
      isCritical: item.isCritical,
      detail: item.detail.trim() || null
    }))
  };
  savingMedicalHistory.value = true;
  try {
    await patientsService.recordMedicalHistory(visitId, payload);
    medicalHistoryFormVisible.value = false;
    alertRefreshKey.value += 1;
    successMessage.value = 'Đã ghi nhận bản chụp tiền sử mới.';
    await Promise.all([loadLatestHistory(), loadHistoryArchive(true), loadTimeline(true)]);
  } catch (error: unknown) {
    medicalHistoryFormError.value = apiError(error, 'Không lưu được tiền sử. Vui lòng thử lại.');
  } finally { savingMedicalHistory.value = false; }
}

function openVitalForm() {
  vitalForm.systolicBp = '';
  vitalForm.diastolicBp = '';
  vitalForm.pulseBpm = '';
  vitalForm.temperatureC = '';
  vitalForm.note = '';
  vitalFormError.value = '';
  vitalFormVisible.value = true;
}

function validateVitalForm(): string | null {
  if (!activeVisit.value) return 'Cần tạo lần khám đang mở trước khi ghi sinh hiệu.';
  const systolic = parseOptionalNumber(vitalForm.systolicBp);
  const diastolic = parseOptionalNumber(vitalForm.diastolicBp);
  const pulse = parseOptionalNumber(vitalForm.pulseBpm);
  const temperature = parseOptionalNumber(vitalForm.temperatureC);
  if ([systolic, diastolic, pulse, temperature].some(value => value !== null && !Number.isFinite(value))) return 'Các chỉ số sinh hiệu phải là số hợp lệ.';
  if ([systolic, diastolic, pulse, temperature].every(value => value === null)) return 'Cần nhập ít nhất một chỉ số sinh hiệu.';
  if (systolic !== null && (!Number.isInteger(systolic) || systolic < 50 || systolic > 260)) return 'Huyết áp tâm thu phải là số nguyên từ 50 đến 260 mmHg.';
  if (diastolic !== null && (!Number.isInteger(diastolic) || diastolic < 30 || diastolic > 160)) return 'Huyết áp tâm trương phải là số nguyên từ 30 đến 160 mmHg.';
  if (systolic !== null && diastolic !== null && systolic <= diastolic) return 'Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.';
  if (pulse !== null && (!Number.isInteger(pulse) || pulse < 20 || pulse > 220)) return 'Mạch phải là số nguyên từ 20 đến 220 lần/phút.';
  if (temperature !== null && (temperature < 30 || temperature > 43)) return 'Thân nhiệt phải từ 30 đến 43 °C.';
  if (vitalForm.note.length > 2000) return 'Ghi chú không được vượt quá 2000 ký tự.';
  return null;
}

function parseOptionalNumber(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

async function saveVitalSigns() {
  vitalFormError.value = '';
  const validationError = validateVitalForm();
  if (validationError) { vitalFormError.value = validationError; return; }
  const visitId = activeVisit.value?.visitId;
  if (!visitId) return;
  savingVitalSigns.value = true;
  try {
    await patientsService.recordVitalSigns(visitId, {
      systolicBp: nullableNumber(vitalForm.systolicBp),
      diastolicBp: nullableNumber(vitalForm.diastolicBp),
      pulseBpm: nullableNumber(vitalForm.pulseBpm),
      temperatureC: nullableNumber(vitalForm.temperatureC),
      note: vitalForm.note.trim() || null
    });
    vitalFormVisible.value = false;
    successMessage.value = 'Đã ghi sinh hiệu thành công.';
    await Promise.all([loadVitalSigns(true), loadTimeline(true)]);
  } catch (error: unknown) {
    vitalFormError.value = apiError(error, 'Không lưu được sinh hiệu. Vui lòng thử lại.');
  } finally { savingVitalSigns.value = false; }
}

function nullableNumber(value: string): number | null {
  return value.trim() ? Number(value) : null;
}

function openEditForm() {
  if (!patient.value) return;
  Object.assign(editForm, {
    fullName: patient.value.fullName,
    dateOfBirth: patient.value.dateOfBirth.slice(0, 10),
    gender: patient.value.gender ?? '',
    phone: patient.value.phone,
    email: patient.value.email ?? '',
    address: patient.value.address ?? ''
  });
  editError.value = '';
  editVisible.value = true;
}

function closeEditForm() { if (!saving.value) { editVisible.value = false; editError.value = ''; } }

function validateEdit(): string | null {
  if (!editForm.fullName.trim()) return 'Họ tên không được để trống.';
  if (editForm.fullName.trim().length > 100) return 'Họ tên không được vượt quá 100 ký tự.';
  if (!editForm.dateOfBirth) return 'Ngày sinh không được để trống.';
  const birth = new Date(`${editForm.dateOfBirth}T00:00:00`);
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const earliest = new Date(today); earliest.setFullYear(earliest.getFullYear() - 120);
  if (Number.isNaN(birth.getTime()) || birth > today || birth < earliest) return 'Ngày sinh không hợp lệ (từ 0 đến 120 tuổi).';
  if (!/^0\d{9}$/.test(editForm.phone.trim())) return 'Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).';
  if (editForm.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(editForm.email.trim())) return 'Email không đúng định dạng.';
  if (editForm.email.trim().length > 100) return 'Email không được vượt quá 100 ký tự.';
  if (editForm.address.trim().length > 500) return 'Địa chỉ không được vượt quá 500 ký tự.';
  return null;
}

function toEditPayload(confirmNotDuplicate = false): PatientPayload {
  return {
    fullName: editForm.fullName.trim(), dateOfBirth: editForm.dateOfBirth, gender: editForm.gender || null,
    phone: editForm.phone.trim(), email: editForm.email.trim() || null, address: editForm.address.trim() || null,
    confirmNotDuplicate
  };
}

async function saveEdit() {
  editError.value = '';
  const validationError = validateEdit();
  if (validationError) { editError.value = validationError; return; }
  pendingPayload.value = toEditPayload();
  await submitEdit(pendingPayload.value);
}

async function submitEdit(payload: PatientPayload) {
  if (!patient.value) return;
  saving.value = true;
  try {
    patient.value = await patientsService.update(patient.value.patientId, payload);
    editVisible.value = false;
    duplicateVisible.value = false;
    duplicates.value = [];
    pendingPayload.value = null;
    successMessage.value = 'Đã cập nhật hồ sơ bệnh nhân thành công.';
  } catch (error: unknown) {
    const problem = getProblem(error);
    if (problem?.code === 'PATIENT_POSSIBLE_DUPLICATE') {
      duplicates.value = problem.duplicates ?? [];
      duplicateVisible.value = true;
      duplicateError.value = '';
    } else if (duplicateVisible.value) duplicateError.value = apiError(error, 'Không cập nhật được hồ sơ bệnh nhân. Vui lòng thử lại.');
    else editError.value = apiError(error, 'Không cập nhật được hồ sơ bệnh nhân. Vui lòng thử lại.');
  } finally { saving.value = false; }
}

async function confirmDuplicate() {
  if (!pendingPayload.value) return;
  duplicateError.value = '';
  await submitEdit({ ...pendingPayload.value, confirmNotDuplicate: true });
}

function cancelDuplicate() { if (!saving.value) { duplicateVisible.value = false; duplicates.value = []; pendingPayload.value = null; duplicateError.value = ''; } }

function getProblem(error: unknown): PatientApiProblem | null {
  return axios.isAxiosError<PatientApiProblem>(error) ? error.response?.data ?? null : null;
}

function apiError(error: unknown, fallback: string): string {
  const problem = getProblem(error);
  const validationMessages = problem?.errors?.map(item => item.message).filter((message): message is string => Boolean(message));
  return validationMessages?.length ? validationMessages.join(' ') : problem?.message || fallback;
}

function calculateAge(dateOfBirth: string): number | string {
  const birth = new Date(`${dateOfBirth.slice(0, 10)}T00:00:00`);
  if (Number.isNaN(birth.getTime())) return '—';
  const today = new Date();
  let age = today.getFullYear() - birth.getFullYear();
  if (today.getMonth() < birth.getMonth() || (today.getMonth() === birth.getMonth() && today.getDate() < birth.getDate())) age -= 1;
  return age;
}

function formatDate(value: string): string {
  const [year, month, day] = value.slice(0, 10).split('-');
  return year && month && day ? `${day}/${month}/${year}` : '—';
}

function formatDateTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(date);
}

function formatGender(value: string | null): string {
  if (value === 'Male') return 'Nam';
  if (value === 'Female') return 'Nữ';
  if (value === 'Other') return 'Khác';
  return 'Chưa cung cấp';
}

function formatVisitStatus(status: string): string {
  const labels: Record<string, string> = { Created: 'Đã tạo', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy' };
  return labels[status] ?? status;
}
</script>

<style scoped>
.detail-page { max-width: 1120px; margin: 0 auto; display: grid; gap: 1rem; color: #26394c; }
.back-row a { color: #267da8; font-weight: 700; text-decoration: none; }
.panel { padding: 1.2rem; border: 1px solid #dfe8ee; border-radius: 15px; background: #fff; box-shadow: 0 8px 24px #173f5809; }
.profile-header, .section-heading { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
.profile-header h1 { margin: 0 0 0.7rem; color: #213c50; font-size: clamp(1.5rem, 3vw, 2rem); }
.eyebrow { margin: 0 0 0.3rem; color: #3181ad; font-size: 0.7rem; font-weight: 800; letter-spacing: 0.09em; }
.quick-facts { display: flex; flex-wrap: wrap; gap: 0.55rem; }
.quick-facts span, .visit-flags span { border-radius: 99px; padding: 0.3rem 0.65rem; background: #eef5f8; color: #476275; font-size: 0.82rem; }
.button { border: 1px solid #cbdbe4; border-radius: 9px; padding: 0.58rem 0.9rem; font-weight: 700; cursor: pointer; }
.button-primary { border-color: #267ba7; background: #267ba7; color: #fff; }
.button-quiet { background: #fff; color: #315369; }
.button:disabled { opacity: 0.62; cursor: wait; }
.notice { margin: 0; border-radius: 10px; padding: 0.7rem 0.9rem; }
.success { background: #e8f8f0; color: #176541; }
.error { background: #fff0ee; color: #9e332b; }
.tabs { display: flex; flex-wrap: wrap; gap: 0.35rem; border-bottom: 1px solid #dfe8ee; }
.tabs button { margin-bottom: -1px; border: 0; border-bottom: 3px solid transparent; padding: 0.75rem 0.9rem; background: transparent; color: #526a7c; font-weight: 700; cursor: pointer; }
.tabs button.active { border-color: #2b86b1; color: #216d94; }
.content-panel { min-height: 220px; }
.section-heading { margin-bottom: 1rem; }
.section-heading h2 { margin: 0; font-size: 1.22rem; }
.profile-grid, .vital-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; margin: 0; }
.profile-grid .wide { grid-column: 1 / -1; }
dt { color: #6b7f8d; font-size: 0.82rem; }
dd { margin: 0.2rem 0 0; color: #263e50; font-weight: 650; white-space: pre-wrap; }
.state-panel { padding: 2rem; }
.state-panel h1 { margin: 0 0 0.5rem; }
.empty-state { color: #687f8e; }
.timeline-list { position: relative; display: grid; gap: 0.85rem; margin: 0; padding: 0 0 0 1rem; list-style: none; }
.timeline-list::before { position: absolute; top: 0.8rem; bottom: 0.8rem; left: 0.36rem; width: 2px; background: #d8e8ef; content: ''; }
.timeline-item { position: relative; padding-left: 1rem; }
.timeline-dot { position: absolute; top: 0.75rem; left: -1rem; width: 0.7rem; height: 0.7rem; border: 2px solid #fff; border-radius: 50%; background: #2782ac; box-shadow: 0 0 0 1px #2782ac; }
.timeline-card { padding: 0.85rem; border: 1px solid #e3ebef; border-radius: 11px; }
.timeline-card p { margin: 0.45rem 0 0; color: #5b7180; font-size: 0.9rem; }
.timeline-top { display: flex; justify-content: space-between; align-items: center; gap: 0.6rem; }
.status-pill { border-radius: 99px; padding: 0.22rem 0.55rem; background: #edf3f6; font-size: 0.75rem; }
.status-inprogress { background: #e3f4ff; color: #146a96; }
.status-completed { background: #e6f7ee; color: #24724d; }
.status-cancelled { background: #f5e9e8; color: #8d423b; }
.visit-flags { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.65rem; }
.visit-flags span { font-size: 0.72rem; }
.load-more { display: block; margin: 1rem auto 0; }
.record-meta { color: #71828c; font-size: 0.84rem; }
.history-note { border-left: 3px solid #8cb8ca; padding: 0.65rem 0.85rem; background: #f4fafc; white-space: pre-wrap; }
.history-list, .duplicate-list { display: grid; gap: 0.65rem; margin: 0; padding: 0; list-style: none; }
.history-list li, .duplicate-list li { padding: 0.8rem; border: 1px solid #e2ebef; border-radius: 10px; }
.history-list li > div { display: flex; flex-wrap: wrap; align-items: center; gap: 0.6rem; }
.history-archive, .vital-history { display: grid; gap: 0.7rem; margin-top: 1.4rem; }
.history-archive h3 { margin: 0; color: #314d61; font-size: 1rem; }
.archive-card { padding: 0.85rem; border: 1px solid #e2ebef; border-radius: 10px; }
.archive-card header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.4rem; color: #496477; }
.archive-card header span { color: #738694; font-size: 0.85rem; }
.archive-card p { margin: 0.45rem 0 0; white-space: pre-wrap; }
.archive-card ul { margin: 0.45rem 0 0; padding-left: 1.2rem; }
.archive-card li span { color: #9a6312; font-size: 0.85rem; }
.record-form { display: grid; gap: 0.8rem; margin: 0.8rem 0 1.2rem; padding: 1rem; border: 1px solid #d9e7ed; border-radius: 12px; background: #f7fbfd; }
.form-context { margin: 0; color: #597181; font-size: 0.88rem; }
.editable-record-row { display: grid; grid-template-columns: 0.85fr 1.3fr 1.3fr auto auto; align-items: end; gap: 0.65rem; padding: 0.8rem; border: 1px solid #e0e9ee; border-radius: 10px; background: #fff; }
.editable-record-row label, .vital-form > label, .field-label { display: grid; gap: 0.35rem; color: #496374; font-size: 0.84rem; font-weight: 650; }
.record-form input:not([type='checkbox']), .record-form select, .record-form textarea { width: 100%; border: 1px solid #cddbe3; border-radius: 8px; padding: 0.58rem; color: #26394c; font: inherit; }
.record-form input[type='checkbox'] { width: 1rem; height: 1rem; accent-color: #277da6; }
.critical-check { display: flex !important; align-items: center; gap: 0.35rem !important; white-space: nowrap; }
.remove-row { border: 0; padding: 0.55rem 0.2rem; background: transparent; color: #a44640; cursor: pointer; }
.remove-row:disabled { opacity: 0.5; cursor: not-allowed; }
.vital-form { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.vital-form .form-context, .vital-form .field-label, .vital-form .notice, .vital-form .modal-actions { grid-column: 1 / -1; }
.info { background: #eef7fb; color: #36586c; }
.history-kind { color: #507287; font-size: 0.82rem; }
.critical-tag { border-radius: 99px; padding: 0.2rem 0.5rem; background: #fff0dd; color: #925300; font-size: 0.72rem; font-weight: 700; }
.history-list li p { margin: 0.5rem 0 0; white-space: pre-wrap; }
.vital-grid { margin: 0 0 1rem; }
.modal-backdrop { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; overflow-y: auto; padding: 1rem; background: #152b3a88; }
.modal-card { width: min(100%, 600px); padding: 1.2rem; border-radius: 16px; background: #fff; box-shadow: 0 25px 75px #0c243755; }
.close-button { border: 0; background: transparent; color: #587284; font-size: 1.6rem; cursor: pointer; }
.edit-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.8rem; }
.edit-form label { display: grid; gap: 0.35rem; color: #496374; font-size: 0.86rem; font-weight: 650; }
.edit-form input, .edit-form select, .edit-form textarea { width: 100%; border: 1px solid #cddbe3; border-radius: 8px; padding: 0.6rem; color: #26394c; font: inherit; }
.edit-form label:nth-of-type(6), .edit-form .notice, .modal-actions { grid-column: 1 / -1; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.6rem; padding-top: 0.5rem; }
.duplicate-list li { display: grid; gap: 0.3rem; }
.duplicate-list span { color: #6e808a; font-size: 0.84rem; }
@media (max-width: 760px) { .editable-record-row { grid-template-columns: 1fr 1fr; } .editable-record-row label:nth-child(3) { grid-column: 1 / -1; } }
@media (max-width: 640px) { .profile-header { align-items: flex-start; flex-direction: column; } .profile-grid, .vital-grid, .edit-form, .vital-form { grid-template-columns: 1fr; } .edit-form label:nth-of-type(6), .edit-form .notice, .modal-actions, .vital-form .form-context, .vital-form .field-label, .vital-form .notice { grid-column: auto; } .editable-record-row { grid-template-columns: 1fr; } .editable-record-row label:nth-child(3) { grid-column: auto; } .tabs { overflow-x: auto; flex-wrap: nowrap; } .tabs button { white-space: nowrap; } }
</style>
