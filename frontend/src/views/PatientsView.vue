<template>
  <section class="patients-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">MOD_PAT · F_PAT_01 · F_PAT_02 · F_PAT_10</p>
        <h1>Hồ sơ bệnh nhân</h1>
        <p class="subtitle">Tìm kiếm nhanh và quản lý thông tin hồ sơ bệnh nhân.</p>
      </div>
      <button v-if="canCreate" class="button button-primary" type="button" @click="openCreateForm">
        + Thêm bệnh nhân
      </button>
    </header>

    <p v-if="pageError" class="alert alert-error" role="alert">{{ pageError }}</p>
    <p v-if="successMessage" class="alert alert-success" role="status">{{ successMessage }}</p>

    <section class="panel search-panel" aria-label="Tìm kiếm hồ sơ bệnh nhân">
      <label class="search-field">
        <span>Tìm theo SĐT, mã bệnh nhân hoặc họ tên</span>
        <input
          v-model="keyword"
          type="search"
          maxlength="100"
          placeholder="Nhập SĐT, BN000001 hoặc tên"
          autocomplete="off"
        />
      </label>
      <label class="page-size-field">
        <span>Dòng/trang</span>
        <select v-model.number="pageSize" @change="applyPageSize">
          <option :value="10">10</option>
          <option :value="20">20</option>
          <option :value="50">50</option>
        </select>
      </label>
    </section>

    <section class="panel list-panel">
      <div v-if="loading" class="empty-state">Đang tải hồ sơ bệnh nhân...</div>
      <div v-else-if="!patients.length" class="empty-state">Không tìm thấy hồ sơ bệnh nhân phù hợp.</div>
      <div v-else class="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Mã bệnh nhân</th>
              <th>Họ và tên</th>
              <th>Ngày sinh</th>
              <th>Giới tính</th>
              <th>Số điện thoại</th>
              <th class="actions-heading">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="patient in patients" :key="patient.patientCode">
              <td><span class="patient-code">{{ patient.patientCode }}</span></td>
              <td><strong>{{ patient.fullName }}</strong></td>
              <td>{{ formatDate(patient.dateOfBirth) }}</td>
              <td>{{ formatGender(patient.gender) }}</td>
              <td>{{ patient.phone }}</td>
              <td class="row-actions">
                <router-link
                  class="text-button"
                  :to="{ name: 'patient-detail', params: { id: patient.patientId } }"
                >
                  Chi tiết
                </router-link>
                <button
                  v-if="canEdit"
                  class="text-button"
                  type="button"
                  :disabled="patient.patientId === undefined"
                  :title="patient.patientId === undefined ? 'API danh sách chưa trả mã định danh hồ sơ' : 'Sửa hồ sơ'"
                  @click="openEditForm(patient)"
                >
                  Sửa
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="pagination">
        <p>Hiển thị {{ firstItem }}–{{ lastItem }} trong {{ totalCount }} hồ sơ</p>
        <div class="page-controls">
          <button class="button button-quiet" type="button" :disabled="page <= 1 || loading" @click="changePage(page - 1)">
            Trước
          </button>
          <span class="page-number">Trang {{ page }} / {{ totalPages }}</span>
          <button class="button button-quiet" type="button" :disabled="page >= totalPages || loading" @click="changePage(page + 1)">
            Sau
          </button>
        </div>
      </footer>
    </section>

    <div v-if="formVisible" class="modal-backdrop" @click.self="closeForm">
      <section class="modal-card" role="dialog" aria-modal="true" :aria-labelledby="formTitleId">
        <header class="modal-heading">
          <div>
            <p class="eyebrow">QUẢN LÝ HỒ SƠ</p>
            <h2 :id="formTitleId">{{ editingPatientId === null ? 'Thêm bệnh nhân' : 'Cập nhật hồ sơ' }}</h2>
          </div>
          <button class="close-button" type="button" aria-label="Đóng" :disabled="saving || formLoading" @click="closeForm">×</button>
        </header>

        <form class="patient-form" @submit.prevent="savePatient">
          <div class="form-grid">
            <label class="field field-wide">
              <span>Họ và tên <b>*</b></span>
              <input v-model="form.fullName" maxlength="100" autocomplete="name" required />
            </label>
            <label class="field">
              <span>Ngày sinh <b>*</b></span>
              <input v-model="form.dateOfBirth" type="date" required />
            </label>
            <label class="field">
              <span>Giới tính</span>
              <select v-model="form.gender">
                <option value="">Chưa cung cấp</option>
                <option value="Male">Nam</option>
                <option value="Female">Nữ</option>
                <option value="Other">Khác</option>
              </select>
            </label>
            <label class="field">
              <span>Số điện thoại <b>*</b></span>
              <input v-model="form.phone" type="tel" inputmode="numeric" maxlength="10" autocomplete="tel" required />
            </label>
            <label class="field">
              <span>Email</span>
              <input v-model="form.email" type="email" maxlength="100" autocomplete="email" />
            </label>
            <label class="field field-wide">
              <span>Địa chỉ</span>
              <textarea v-model="form.address" rows="3" maxlength="500" autocomplete="street-address" />
            </label>
          </div>

          <p v-if="formLoading" class="form-loading">Đang tải thông tin hồ sơ...</p>
          <p v-if="formError" class="alert alert-error" role="alert">{{ formError }}</p>
          <footer class="modal-actions">
            <button class="button button-quiet" type="button" :disabled="saving || formLoading" @click="closeForm">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving || formLoading">
              {{ saving ? 'Đang lưu...' : editingPatientId === null ? 'Tạo hồ sơ' : 'Lưu thay đổi' }}
            </button>
          </footer>
        </form>
      </section>
    </div>

    <div v-if="duplicateDialogVisible" class="modal-backdrop duplicate-backdrop" @click.self="cancelDuplicate">
      <section class="confirm-card" role="dialog" aria-modal="true" aria-labelledby="duplicate-title">
        <p class="eyebrow">KIỂM TRA HỒ SƠ TRÙNG</p>
        <h2 id="duplicate-title">Có hồ sơ có thể trùng</h2>
        <p class="duplicate-description">
          Kiểm tra danh sách bên dưới. Chỉ tiếp tục nếu xác nhận đây là {{ pendingDuplicate?.kind === 'create' ? 'người khác' : 'thông tin của hồ sơ khác' }}.
        </p>
        <div class="duplicate-list">
          <article v-for="duplicate in possibleDuplicates" :key="duplicate.patientCode" class="duplicate-item">
            <strong>{{ duplicate.patientCode }} · {{ duplicate.fullName }}</strong>
            <span>Ngày sinh: {{ formatDate(duplicate.dateOfBirth) }}</span>
            <span>SĐT: {{ duplicate.phone }}</span>
          </article>
        </div>
        <p v-if="duplicateError" class="alert alert-error" role="alert">{{ duplicateError }}</p>
        <footer class="modal-actions duplicate-actions">
          <button class="button button-quiet" type="button" :disabled="duplicateSaving" @click="cancelDuplicate">Hủy</button>
          <button class="button button-primary" type="button" :disabled="duplicateSaving" @click="confirmDuplicate">
            {{ duplicateSaving ? 'Đang lưu...' : pendingDuplicate?.kind === 'create' ? 'Đây là người khác — vẫn tạo mới' : 'Đây là hồ sơ khác — vẫn cập nhật' }}
          </button>
        </footer>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import axios from 'axios';
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import {
  patientsService,
  type PatientApiProblem,
  type PatientDetails,
  type PatientDuplicateInfo,
  type PatientPayload,
  type PatientSummary
} from '@/services/patients';
import { useAuthStore } from '@/stores/auth';

interface PatientForm {
  fullName: string;
  dateOfBirth: string;
  gender: string;
  phone: string;
  email: string;
  address: string;
}

type PendingDuplicate =
  | { kind: 'create'; payload: PatientPayload }
  | { kind: 'update'; patientId: number; payload: PatientPayload };

const authStore = useAuthStore();
const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
const canCreate = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const canEdit = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));

const patients = ref<PatientSummary[]>([]);
const keyword = ref('');
const page = ref(1);
const pageSize = ref(20);
const totalCount = ref(0);
const totalPages = ref(1);
const loading = ref(false);
const pageError = ref('');
const successMessage = ref('');
const formVisible = ref(false);
const formLoading = ref(false);
const editingPatientId = ref<number | null>(null);
const saving = ref(false);
const formError = ref('');
const duplicateDialogVisible = ref(false);
const possibleDuplicates = ref<PatientDuplicateInfo[]>([]);
const pendingDuplicate = ref<PendingDuplicate | null>(null);
const duplicateSaving = ref(false);
const duplicateError = ref('');
const form = reactive<PatientForm>(emptyForm());

let searchTimer: ReturnType<typeof setTimeout> | undefined;
let listRequestId = 0;

const formTitleId = computed(() => editingPatientId.value === null ? 'create-patient-title' : 'edit-patient-title');
const firstItem = computed(() => totalCount.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1);
const lastItem = computed(() => Math.min(page.value * pageSize.value, totalCount.value));

onMounted(() => {
  void loadPatients();
});

onBeforeUnmount(() => {
  if (searchTimer !== undefined) clearTimeout(searchTimer);
});

watch(keyword, () => {
  if (searchTimer !== undefined) clearTimeout(searchTimer);
  page.value = 1;
  searchTimer = setTimeout(() => {
    void loadPatients();
  }, 400);
});

function emptyForm(): PatientForm {
  return { fullName: '', dateOfBirth: '', gender: '', phone: '', email: '', address: '' };
}

function getApiProblem(error: unknown): PatientApiProblem | null {
  if (!axios.isAxiosError<PatientApiProblem>(error)) return null;
  return error.response?.data ?? null;
}

function backendError(error: unknown, fallback: string): string {
  const problem = getApiProblem(error);
  const validationMessages = problem?.errors?.map(item => item.message).filter((message): message is string => Boolean(message));
  if (validationMessages?.length) return validationMessages.join(' ');
  return problem?.message || fallback;
}

async function loadPatients() {
  const requestId = ++listRequestId;
  loading.value = true;
  pageError.value = '';
  try {
    const result = await patientsService.search({
      keyword: keyword.value.trim() || undefined,
      page: page.value,
      pageSize: pageSize.value
    });
    if (requestId !== listRequestId) return;
    patients.value = result.items ?? [];
    totalCount.value = result.totalCount ?? 0;
    totalPages.value = Math.max(result.totalPages ?? 0, 1);
    if (page.value > totalPages.value) {
      page.value = totalPages.value;
      await loadPatients();
    }
  } catch (error: unknown) {
    if (requestId === listRequestId) {
      pageError.value = backendError(error, 'Không tải được danh sách bệnh nhân. Vui lòng thử lại.');
    }
  } finally {
    if (requestId === listRequestId) loading.value = false;
  }
}

function applyPageSize() {
  page.value = 1;
  void loadPatients();
}

function changePage(nextPage: number) {
  if (nextPage < 1 || nextPage > totalPages.value) return;
  page.value = nextPage;
  void loadPatients();
}

function openCreateForm() {
  editingPatientId.value = null;
  Object.assign(form, emptyForm());
  formError.value = '';
  formVisible.value = true;
}

async function openEditForm(patient: PatientSummary) {
  const patientId = patient.patientId;
  if (typeof patientId !== 'number') {
    return;
  }

  editingPatientId.value = patientId;
  formVisible.value = true;
  formLoading.value = true;
  formError.value = '';
  try {
    const details = await patientsService.getById(patientId);
    if (editingPatientId.value !== patientId) return;
    setFormFromDetails(details);
  } catch (error: unknown) {
    formError.value = backendError(error, 'Không tải được hồ sơ để sửa. Vui lòng thử lại.');
  } finally {
    formLoading.value = false;
  }
}

function setFormFromDetails(patient: PatientDetails) {
  Object.assign(form, {
    fullName: patient.fullName,
    dateOfBirth: patient.dateOfBirth.slice(0, 10),
    gender: patient.gender ?? '',
    phone: patient.phone,
    email: patient.email ?? '',
    address: patient.address ?? ''
  });
}

function closeForm() {
  if (saving.value || formLoading.value) return;
  formVisible.value = false;
  editingPatientId.value = null;
  formError.value = '';
  Object.assign(form, emptyForm());
}

function validateForm(): string | null {
  if (!form.fullName.trim()) return 'Họ tên không được để trống.';
  if (form.fullName.trim().length > 100) return 'Họ tên không được vượt quá 100 ký tự.';
  if (!form.dateOfBirth) return 'Ngày sinh không được để trống.';

  const dateOfBirth = new Date(`${form.dateOfBirth}T00:00:00`);
  if (Number.isNaN(dateOfBirth.getTime())) return 'Ngày sinh không hợp lệ.';
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  if (dateOfBirth > today) return 'Ngày sinh không được ở tương lai.';
  const earliestDate = new Date(today);
  earliestDate.setFullYear(earliestDate.getFullYear() - 120);
  if (dateOfBirth < earliestDate) return 'Ngày sinh không được quá 120 tuổi.';

  if (!/^0\d{9}$/.test(form.phone.trim())) return 'Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).';
  if (form.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) return 'Email không đúng định dạng.';
  if (form.email.trim().length > 100) return 'Email không được vượt quá 100 ký tự.';
  if (form.address.trim().length > 500) return 'Địa chỉ không được vượt quá 500 ký tự.';
  return null;
}

function toPayload(): PatientPayload {
  return {
    fullName: form.fullName.trim(),
    dateOfBirth: form.dateOfBirth,
    gender: form.gender || null,
    phone: form.phone.trim(),
    email: form.email.trim() || null,
    address: form.address.trim() || null,
    confirmNotDuplicate: false
  };
}

async function savePatient() {
  formError.value = '';
  const validationError = validateForm();
  if (validationError) {
    formError.value = validationError;
    return;
  }

  const payload = toPayload();
  saving.value = true;
  try {
    if (editingPatientId.value === null) {
      await patientsService.create(payload);
      finishSave('Đã tạo hồ sơ bệnh nhân thành công.');
    } else {
      await patientsService.update(editingPatientId.value, payload);
      finishSave('Đã cập nhật hồ sơ bệnh nhân thành công.');
    }
  } catch (error: unknown) {
    const action: PendingDuplicate = editingPatientId.value === null
      ? { kind: 'create', payload }
      : { kind: 'update', patientId: editingPatientId.value, payload };
    if (!showDuplicateDialog(error, action)) {
      formError.value = backendError(error, 'Không lưu được hồ sơ bệnh nhân. Vui lòng thử lại.');
    }
  } finally {
    saving.value = false;
  }
}

function showDuplicateDialog(error: unknown, action: PendingDuplicate): boolean {
  const problem = getApiProblem(error);
  if (problem?.code !== 'PATIENT_POSSIBLE_DUPLICATE') return false;
  possibleDuplicates.value = problem.duplicates ?? [];
  pendingDuplicate.value = action;
  duplicateError.value = '';
  duplicateDialogVisible.value = true;
  return true;
}

function cancelDuplicate() {
  if (duplicateSaving.value) return;
  duplicateDialogVisible.value = false;
  possibleDuplicates.value = [];
  pendingDuplicate.value = null;
  duplicateError.value = '';
}

async function confirmDuplicate() {
  const action = pendingDuplicate.value;
  if (!action) return;
  duplicateSaving.value = true;
  duplicateError.value = '';
  try {
    const payload: PatientPayload = { ...action.payload, confirmNotDuplicate: true };
    if (action.kind === 'create') {
      await patientsService.create(payload);
      finishSave('Đã tạo hồ sơ bệnh nhân mới thành công.');
    } else {
      await patientsService.update(action.patientId, payload);
      finishSave('Đã cập nhật hồ sơ bệnh nhân thành công.');
    }
  } catch (error: unknown) {
    duplicateError.value = backendError(error, 'Không thể lưu hồ sơ. Vui lòng thử lại.');
  } finally {
    duplicateSaving.value = false;
  }
}

function finishSave(message: string) {
  duplicateDialogVisible.value = false;
  possibleDuplicates.value = [];
  pendingDuplicate.value = null;
  formVisible.value = false;
  editingPatientId.value = null;
  Object.assign(form, emptyForm());
  successMessage.value = message;
  void loadPatients();
}

function formatDate(value: string): string {
  const [year, month, day] = value.slice(0, 10).split('-');
  return year && month && day ? `${day}/${month}/${year}` : '—';
}

function formatGender(value: string | null): string {
  if (value === 'Male') return 'Nam';
  if (value === 'Female') return 'Nữ';
  if (value === 'Other') return 'Khác';
  return '—';
}
</script>

<style scoped>
.patients-page {
  max-width: 1180px;
  margin: 0 auto;
  display: grid;
  gap: 1rem;
  color: #26394c;
}

.page-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.35rem 0 0.6rem;
}

.eyebrow {
  margin-bottom: 0.35rem;
  color: #3181ad;
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.09em;
}

h1,
h2,
p {
  margin-top: 0;
}

h1 {
  margin-bottom: 0.35rem;
  font-size: clamp(1.45rem, 3vw, 1.9rem);
}

.subtitle {
  margin-bottom: 0;
  color: #748394;
}

.panel {
  border: 1px solid #e5ebf0;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 5px 18px rgba(28, 48, 67, 0.045);
}

.search-panel {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) 120px;
  align-items: end;
  gap: 0.9rem;
  padding: 1rem;
}

.search-field,
.page-size-field,
.field {
  display: grid;
  gap: 0.42rem;
  color: #536779;
  font-size: 0.83rem;
  font-weight: 650;
}

input,
select,
textarea {
  width: 100%;
  min-height: 2.55rem;
  padding: 0.6rem 0.7rem;
  border: 1px solid #d7e0e7;
  border-radius: 8px;
  background: #fff;
  color: #26394c;
  font: inherit;
  font-size: 0.9rem;
}

textarea {
  resize: vertical;
}

input:focus,
select:focus,
textarea:focus {
  border-color: #5b9fc4;
  outline: 3px solid rgba(49, 129, 173, 0.14);
}

.button {
  min-height: 2.55rem;
  padding: 0.6rem 0.9rem;
  border: 0;
  border-radius: 8px;
  font: inherit;
  font-size: 0.86rem;
  font-weight: 700;
  cursor: pointer;
  transition: filter 0.15s ease, opacity 0.15s ease;
}

.button:hover:not(:disabled) {
  filter: brightness(0.95);
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.button-primary {
  background: #3181ad;
  color: #fff;
}

.button-quiet {
  border: 1px solid #d8e1e8;
  background: #fff;
  color: #536779;
}

.list-panel {
  overflow: hidden;
}

.table-scroll {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  white-space: nowrap;
}

th,
td {
  padding: 0.85rem 1rem;
  border-bottom: 1px solid #edf1f4;
}

th {
  background: #f7f9fb;
  color: #708092;
  font-size: 0.74rem;
  font-weight: 750;
  letter-spacing: 0.035em;
  text-transform: uppercase;
}

td {
  color: #4b5d6e;
  font-size: 0.88rem;
}

td strong {
  color: #26394c;
}

.patient-code {
  color: #35627d;
  font-weight: 750;
}

.actions-heading {
  text-align: right;
}

.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.7rem;
}

.text-button {
  padding: 0;
  border: 0;
  background: transparent;
  color: #28769f;
  font: inherit;
  font-size: 0.82rem;
  font-weight: 700;
  cursor: pointer;
}

.text-button:disabled {
  color: #8998a5;
  cursor: not-allowed;
}

.empty-state {
  padding: 2.6rem 1rem;
  color: #7b8b99;
  text-align: center;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.8rem 1rem;
}

.pagination p {
  margin: 0;
  color: #718092;
  font-size: 0.82rem;
}

.page-controls {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.page-number {
  color: #536779;
  font-size: 0.83rem;
  font-weight: 650;
}

.alert {
  margin: 0;
  padding: 0.75rem 0.9rem;
  border-radius: 8px;
  font-size: 0.87rem;
  line-height: 1.45;
}

.alert-error {
  background: #fff0ef;
  color: #a52b25;
}

.alert-success {
  background: #e8f7ef;
  color: #176341;
}

.modal-backdrop {
  position: fixed;
  z-index: 20;
  inset: 0;
  display: grid;
  align-items: center;
  justify-items: center;
  overflow-y: auto;
  padding: 1.25rem;
  background: rgba(22, 37, 51, 0.52);
}

.duplicate-backdrop {
  z-index: 30;
}

.modal-card,
.confirm-card {
  width: min(100%, 660px);
  max-height: calc(100vh - 2.5rem);
  overflow-y: auto;
  padding: 1.5rem;
  border: 1px solid #e3eaf0;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 20px 60px rgba(17, 37, 55, 0.2);
}

.confirm-card {
  width: min(100%, 550px);
}

.modal-heading {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #edf1f4;
  margin-bottom: 1.2rem;
}

.modal-heading h2,
.confirm-card h2 {
  margin-bottom: 0;
  font-size: 1.35rem;
}

.close-button {
  width: 2rem;
  height: 2rem;
  border: 0;
  border-radius: 7px;
  background: #f1f5f7;
  color: #506274;
  font-size: 1.35rem;
  cursor: pointer;
}

.patient-form {
  display: grid;
  gap: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.9rem;
}

.field b {
  color: #c44b45;
}

.field-wide {
  grid-column: 1 / -1;
}

.form-loading {
  margin: 0;
  color: #748394;
  font-size: 0.85rem;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.65rem;
  margin-top: 0.5rem;
}

.duplicate-description {
  margin: 0.75rem 0 1rem;
  color: #68798a;
  line-height: 1.5;
}

.duplicate-list {
  display: grid;
  max-height: 240px;
  gap: 0.55rem;
  overflow-y: auto;
}

.duplicate-item {
  display: grid;
  gap: 0.25rem;
  padding: 0.75rem;
  border: 1px solid #e4eaf0;
  border-radius: 8px;
  color: #647486;
  font-size: 0.83rem;
}

.duplicate-item strong {
  color: #26394c;
}

.duplicate-actions {
  flex-wrap: wrap;
  margin-top: 1rem;
}

@media (max-width: 700px) {
  .page-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .search-panel {
    grid-template-columns: 1fr 100px;
  }

  .pagination {
    align-items: flex-start;
    flex-direction: column;
  }
}

@media (max-width: 520px) {
  .search-panel,
  .form-grid {
    grid-template-columns: 1fr;
  }

  .field-wide {
    grid-column: auto;
  }

  .page-controls {
    flex-wrap: wrap;
  }

  .modal-card,
  .confirm-card {
    padding: 1.1rem;
  }
}
</style>
