<template>
  <section class="staff-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">MOD_MST · F_MST_07</p>
        <h1>Tài khoản nhân viên</h1>
        <p class="subtitle">Tạo tài khoản, cập nhật vai trò và quản lý trạng thái truy cập.</p>
      </div>
      <button class="button button-primary" type="button" @click="openCreateForm">+ Thêm nhân viên</button>
    </header>

    <p v-if="pageError" class="alert alert-error" role="alert">{{ pageError }}</p>
    <p v-if="successMessage" class="alert alert-success" role="status">{{ successMessage }}</p>

    <section class="panel filters" aria-label="Bộ lọc nhân viên">
      <label class="search-field">
        <span>Tìm theo họ tên hoặc SĐT</span>
        <input
          v-model="searchText"
          type="search"
          placeholder="Nhập tên hoặc số điện thoại"
          @keydown.enter="applyFilters"
        />
      </label>
      <label>
        <span>Vai trò</span>
        <select v-model="roleFilter" @change="applyFilters">
          <option value="">Tất cả vai trò</option>
          <option v-for="role in roleOptions" :key="role.code" :value="role.code">{{ role.label }}</option>
        </select>
      </label>
      <label>
        <span>Trạng thái</span>
        <select v-model="statusFilter" @change="applyFilters">
          <option value="">Tất cả trạng thái</option>
          <option value="true">Đang hoạt động</option>
          <option value="false">Đã khóa</option>
        </select>
      </label>
      <button class="button button-secondary filter-button" type="button" @click="applyFilters">Tìm kiếm</button>
    </section>

    <section class="panel list-panel">
      <div v-if="loading" class="empty-state">Đang tải danh sách nhân viên...</div>
      <div v-else-if="!staffItems.length" class="empty-state">Không tìm thấy nhân viên phù hợp.</div>
      <div v-else class="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Nhân viên</th>
              <th>Số điện thoại</th>
              <th>Email</th>
              <th>Vai trò</th>
              <th>Trạng thái</th>
              <th class="actions-heading">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="staff in staffItems" :key="staff.userId">
              <td>
                <strong>{{ staff.fullName }}</strong>
                <small>Mã tài khoản #{{ staff.userId }}</small>
              </td>
              <td>{{ staff.phone }}</td>
              <td>{{ staff.email || '—' }}</td>
              <td><span class="role-badge">{{ staff.roleName }}</span></td>
              <td>
                <span class="status-badge" :class="staff.isActive ? 'status-active' : 'status-locked'">
                  {{ staff.isActive ? 'Đang hoạt động' : 'Đã khóa' }}
                </span>
              </td>
              <td class="row-actions">
                <button class="text-button" type="button" @click="openEditForm(staff)">Sửa</button>
                <button
                  v-if="staff.isActive"
                  class="text-button text-danger"
                  type="button"
                  :disabled="staff.userId === authStore.user?.userId"
                  :title="staff.userId === authStore.user?.userId ? 'Không thể tự khóa tài khoản' : 'Khóa tài khoản'"
                  @click="openStatusConfirmation(staff, 'lock')"
                >
                  Khóa
                </button>
                <button
                  v-else
                  class="text-button text-success"
                  type="button"
                  @click="openStatusConfirmation(staff, 'unlock')"
                >
                  Mở khóa
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="pagination">
        <p>Hiển thị {{ firstItem }}–{{ lastItem }} trong {{ totalCount }} tài khoản</p>
        <div class="page-controls">
          <label>
            <span>Dòng/trang</span>
            <select v-model.number="pageSize" @change="applyFilters">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
            </select>
          </label>
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
            <p class="eyebrow">QUẢN LÝ TÀI KHOẢN</p>
            <h2 :id="formTitleId">{{ editingStaffId === null ? 'Thêm nhân viên' : 'Cập nhật nhân viên' }}</h2>
          </div>
          <button class="close-button" type="button" aria-label="Đóng" :disabled="saving" @click="closeForm">×</button>
        </header>

        <form class="staff-form" @submit.prevent="saveStaff">
          <div class="form-grid">
            <label class="field field-wide">
              <span>Họ và tên <b>*</b></span>
              <input v-model="form.fullName" maxlength="100" autocomplete="name" />
            </label>
            <label class="field">
              <span>Số điện thoại <b>*</b></span>
              <input v-model="form.phone" type="tel" inputmode="numeric" maxlength="10" autocomplete="tel" />
            </label>
            <label class="field">
              <span>Email</span>
              <input v-model="form.email" type="text" inputmode="email" maxlength="100" autocomplete="email" />
            </label>
            <label class="field">
              <span>Vai trò <b>*</b></span>
              <select v-model="form.roleCode">
                <option value="" disabled>Chọn vai trò</option>
                <option v-for="role in roleOptions" :key="role.code" :value="role.code">{{ role.label }}</option>
              </select>
            </label>
            <label class="field">
              <span>Ngày sinh</span>
              <input v-model="form.dateOfBirth" type="date" />
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
            <label v-if="editingStaffId === null" class="field field-wide">
              <span>Mật khẩu ban đầu <b>*</b></span>
              <input v-model="form.password" type="password" autocomplete="new-password" />
              <small>Ít nhất 6 ký tự, gồm chữ hoa, chữ thường và chữ số.</small>
            </label>
          </div>

          <p v-if="formError" class="alert alert-error" role="alert">{{ formError }}</p>
          <footer class="modal-actions">
            <button class="button button-quiet" type="button" :disabled="saving" @click="closeForm">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving">
              {{ saving ? 'Đang lưu...' : editingStaffId === null ? 'Tạo tài khoản' : 'Lưu thay đổi' }}
            </button>
          </footer>
        </form>
      </section>
    </div>

    <div v-if="confirmation" class="modal-backdrop" @click.self="closeConfirmation">
      <section class="confirm-card" role="dialog" aria-modal="true" aria-labelledby="confirm-title">
        <p class="eyebrow">XÁC NHẬN THAO TÁC</p>
        <h2 id="confirm-title">{{ confirmation.action === 'lock' ? 'Khóa tài khoản?' : 'Mở khóa tài khoản?' }}</h2>
        <p>
          Bạn muốn {{ confirmation.action === 'lock' ? 'khóa' : 'mở khóa' }} tài khoản
          <strong>{{ confirmation.staff.fullName }}</strong>?
        </p>
        <p v-if="confirmationError" class="alert alert-error" role="alert">{{ confirmationError }}</p>
        <footer class="modal-actions">
          <button class="button button-quiet" type="button" :disabled="statusSaving" @click="closeConfirmation">Hủy</button>
          <button
            class="button"
            :class="confirmation.action === 'lock' ? 'button-danger' : 'button-primary'"
            type="button"
            :disabled="statusSaving"
            @click="confirmStatusChange"
          >
            {{ statusSaving ? 'Đang xử lý...' : confirmation.action === 'lock' ? 'Xác nhận khóa' : 'Xác nhận mở khóa' }}
          </button>
        </footer>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { apiClient } from '@/services/api';
import { useAuthStore } from '@/stores/auth';

interface StaffAccount {
  userId: number;
  phone: string;
  fullName: string;
  email: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  roleCode: string;
  roleName: string;
  isActive: boolean;
}

interface StaffForm {
  fullName: string;
  phone: string;
  email: string;
  roleCode: string;
  dateOfBirth: string;
  gender: string;
  password: string;
}

type StatusAction = 'lock' | 'unlock';

const roleOptions = [
  { code: 'RECEPTIONIST', label: 'Lễ tân/Thu ngân' },
  { code: 'DENTIST', label: 'Nha sĩ' },
  { code: 'ASSISTANT', label: 'Phụ tá' },
  { code: 'ADMIN', label: 'Quản trị viên' }
];

const authStore = useAuthStore();
const staffItems = ref<StaffAccount[]>([]);
const searchText = ref('');
const roleFilter = ref('');
const statusFilter = ref('');
const page = ref(1);
const pageSize = ref(20);
const totalCount = ref(0);
const totalPages = ref(0);
const loading = ref(false);
const pageError = ref('');
const successMessage = ref('');
const formVisible = ref(false);
const editingStaffId = ref<number | null>(null);
const formError = ref('');
const saving = ref(false);
const statusSaving = ref(false);
const confirmationError = ref('');
const confirmation = ref<{ staff: StaffAccount; action: StatusAction } | null>(null);
const form = reactive<StaffForm>(emptyForm());
let listRequestId = 0;

const formTitleId = computed(() => editingStaffId.value === null ? 'create-staff-title' : 'edit-staff-title');
const firstItem = computed(() => totalCount.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1);
const lastItem = computed(() => Math.min(page.value * pageSize.value, totalCount.value));

onMounted(() => loadStaff());

function emptyForm(): StaffForm {
  return {
    fullName: '',
    phone: '',
    email: '',
    roleCode: '',
    dateOfBirth: '',
    gender: '',
    password: ''
  };
}

function backendError(error: any, fallback: string): string {
  const data = error.response?.data;
  if (Array.isArray(data?.errors)) {
    const messages = data.errors.map((item: any) => item?.message).filter(Boolean);
    if (messages.length) return messages.join(' ');
  }
  return data?.message || fallback;
}

async function loadStaff() {
  const requestId = ++listRequestId;
  loading.value = true;
  pageError.value = '';
  try {
    const response = await apiClient.get('/staff', {
      params: {
        page: page.value,
        pageSize: pageSize.value,
        search: searchText.value.trim() || undefined,
        roleCode: roleFilter.value || undefined,
        isActive: statusFilter.value === '' ? undefined : statusFilter.value === 'true'
      }
    });
    if (requestId !== listRequestId) return;
    staffItems.value = response.data.items ?? [];
    totalCount.value = response.data.totalCount ?? 0;
    totalPages.value = Math.max(response.data.totalPages ?? 0, 1);
    if (page.value > totalPages.value) {
      page.value = totalPages.value;
      await loadStaff();
    }
  } catch (error: any) {
    if (requestId === listRequestId) {
      pageError.value = backendError(error, 'Không tải được danh sách nhân viên. Vui lòng thử lại.');
    }
  } finally {
    if (requestId === listRequestId) loading.value = false;
  }
}

function applyFilters() {
  page.value = 1;
  void loadStaff();
}

function changePage(nextPage: number) {
  if (nextPage < 1 || nextPage > totalPages.value) return;
  page.value = nextPage;
  void loadStaff();
}

function openCreateForm() {
  editingStaffId.value = null;
  Object.assign(form, emptyForm());
  formError.value = '';
  formVisible.value = true;
}

function openEditForm(staff: StaffAccount) {
  editingStaffId.value = staff.userId;
  Object.assign(form, {
    fullName: staff.fullName,
    phone: staff.phone,
    email: staff.email ?? '',
    roleCode: staff.roleCode,
    dateOfBirth: staff.dateOfBirth ?? '',
    gender: staff.gender ?? '',
    password: ''
  });
  formError.value = '';
  formVisible.value = true;
}

function closeForm() {
  if (saving.value) return;
  formVisible.value = false;
  formError.value = '';
  Object.assign(form, emptyForm());
}

function validateForm(): string | null {
  if (!form.fullName.trim()) return 'Họ tên không được để trống.';
  if (!/^0\d{9}$/.test(form.phone.trim())) return 'Số điện thoại cần có 10 chữ số và bắt đầu bằng số 0.';
  if (form.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
    return 'Email không đúng định dạng.';
  }
  if (!roleOptions.some(role => role.code === form.roleCode)) return 'Vui lòng chọn vai trò nhân viên hợp lệ.';
  if (editingStaffId.value === null) {
    if (!form.password) return 'Mật khẩu ban đầu không được để trống.';
    if (form.password.length < 6 || !/[A-Z]/.test(form.password) || !/[a-z]/.test(form.password) || !/[0-9]/.test(form.password)) {
      return 'Mật khẩu cần có ít nhất 6 ký tự, gồm chữ hoa, chữ thường và chữ số.';
    }
  }
  return null;
}

async function saveStaff() {
  formError.value = '';
  const validationError = validateForm();
  if (validationError) {
    formError.value = validationError;
    return;
  }

  saving.value = true;
  try {
    const commonPayload = {
      fullName: form.fullName.trim(),
      phone: form.phone.trim(),
      email: form.email.trim() || null,
      dateOfBirth: form.dateOfBirth || null,
      gender: form.gender || null,
      roleCode: form.roleCode
    };

    if (editingStaffId.value === null) {
      await apiClient.post('/staff', { ...commonPayload, password: form.password });
      successMessage.value = 'Tạo tài khoản nhân viên thành công.';
    } else {
      await apiClient.put(`/staff/${editingStaffId.value}`, commonPayload);
      successMessage.value = 'Cập nhật tài khoản nhân viên thành công.';
    }

    formVisible.value = false;
    Object.assign(form, emptyForm());
    await loadStaff();
  } catch (error: any) {
    formError.value = backendError(error, 'Không lưu được tài khoản. Vui lòng thử lại.');
  } finally {
    saving.value = false;
  }
}

function openStatusConfirmation(staff: StaffAccount, action: StatusAction) {
  confirmationError.value = '';
  confirmation.value = { staff, action };
}

function closeConfirmation() {
  if (statusSaving.value) return;
  confirmation.value = null;
  confirmationError.value = '';
}

async function confirmStatusChange() {
  if (!confirmation.value) return;
  statusSaving.value = true;
  confirmationError.value = '';
  try {
    const { staff, action } = confirmation.value;
    await apiClient.post(`/staff/${staff.userId}/${action}`);
    confirmation.value = null;
    successMessage.value = action === 'lock' ? 'Đã khóa tài khoản nhân viên.' : 'Đã mở khóa tài khoản nhân viên.';
    await loadStaff();
  } catch (error: any) {
    confirmationError.value = backendError(error, 'Không thể cập nhật trạng thái tài khoản. Vui lòng thử lại.');
  } finally {
    statusSaving.value = false;
  }
}
</script>

<style scoped>
.staff-page {
  max-width: 1180px;
  margin: 0 auto;
  color: #223247;
}

.page-heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.25rem;
  margin-bottom: 1.5rem;
}

.eyebrow {
  margin: 0 0 0.35rem;
  color: #3980ad;
  font-size: 0.73rem;
  font-weight: 700;
  letter-spacing: 0.12em;
}

h1,
h2 {
  margin: 0;
}

h1 {
  font-size: clamp(1.65rem, 3vw, 2.1rem);
}

.subtitle {
  margin: 0.45rem 0 0;
  color: #6b7b8d;
  line-height: 1.5;
}

.panel {
  border: 1px solid #e3eaf0;
  border-radius: 13px;
  background: #fff;
  box-shadow: 0 8px 24px rgba(30, 56, 79, 0.05);
}

.filters {
  display: grid;
  grid-template-columns: minmax(220px, 1.6fr) minmax(160px, 1fr) minmax(160px, 1fr) auto;
  align-items: end;
  gap: 0.85rem;
  padding: 1rem;
  margin-bottom: 1rem;
}

.filters label,
.page-controls label,
.field {
  display: grid;
  gap: 0.4rem;
  color: #405467;
  font-size: 0.82rem;
  font-weight: 600;
}

.filters input,
.filters select,
.page-controls select,
.field input,
.field select {
  box-sizing: border-box;
  width: 100%;
  min-height: 2.55rem;
  padding: 0.55rem 0.7rem;
  border: 1px solid #d3dde6;
  border-radius: 8px;
  background: #fff;
  color: #26394c;
  font: inherit;
  font-size: 0.88rem;
}

input:focus,
select:focus {
  border-color: #4b93bf;
  outline: 3px solid rgba(75, 147, 191, 0.14);
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
  padding: 0.9rem 1rem;
  border-bottom: 1px solid #edf1f4;
  vertical-align: middle;
}

th {
  background: #f8fafb;
  color: #68798a;
  font-size: 0.74rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

td {
  color: #46596b;
  font-size: 0.88rem;
}

td strong,
td small {
  display: block;
}

td strong {
  color: #26394c;
}

td small {
  margin-top: 0.25rem;
  color: #8593a0;
  font-size: 0.75rem;
}

.actions-heading {
  text-align: right;
}

.row-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.8rem;
}

.role-badge,
.status-badge {
  display: inline-flex;
  align-items: center;
  min-height: 1.65rem;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  font-size: 0.76rem;
  font-weight: 700;
}

.role-badge {
  background: #eef4f8;
  color: #49677e;
}

.status-active {
  background: #e7f7ef;
  color: #176341;
}

.status-locked {
  background: #fff0ef;
  color: #a52b25;
}

.text-button {
  padding: 0;
  border: 0;
  background: transparent;
  color: #347ba7;
  font: inherit;
  font-size: 0.82rem;
  font-weight: 700;
  cursor: pointer;
}

.text-button:disabled {
  color: #a2adb6;
  cursor: not-allowed;
}

.text-danger {
  color: #b8433d;
}

.text-success {
  color: #24784f;
}

.pagination,
.page-controls {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.85rem;
}

.pagination {
  min-height: 4.25rem;
  padding: 0 1rem;
}

.pagination p {
  margin: 0;
  color: #718090;
  font-size: 0.82rem;
}

.page-controls label {
  display: flex;
  align-items: center;
}

.page-controls select {
  min-height: 2.2rem;
  width: 4.5rem;
  padding: 0.3rem;
}

.page-number {
  color: #52677a;
  font-size: 0.82rem;
  white-space: nowrap;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
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

.button-secondary {
  background: #e8f1f6;
  color: #35627d;
}

.button-quiet {
  border: 1px solid #d8e1e8;
  background: #fff;
  color: #536779;
}

.button-danger {
  background: #b8433d;
  color: #fff;
}

.filter-button {
  min-width: 6.5rem;
}

.empty-state {
  padding: 2.6rem 1rem;
  color: #7b8b99;
  text-align: center;
}

.alert {
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

.modal-card,
.confirm-card {
  width: min(100%, 660px);
  padding: 1.5rem;
  border: 1px solid #e3eaf0;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 20px 60px rgba(17, 37, 55, 0.2);
}

.confirm-card {
  width: min(100%, 430px);
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

.staff-form {
  display: grid;
  gap: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.9rem;
}

.field {
  align-content: start;
}

.field b {
  color: #c44b45;
}

.field small {
  color: #7b8b99;
  font-size: 0.75rem;
  font-weight: 400;
}

.field-wide {
  grid-column: 1 / -1;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.65rem;
  margin-top: 0.5rem;
}

.confirm-card > p:not(.eyebrow):not(.alert) {
  color: #68798a;
  line-height: 1.55;
}

.confirm-card strong {
  color: #26394c;
}

@media (max-width: 850px) {
  .filters {
    grid-template-columns: 1fr 1fr;
  }

  .search-field {
    grid-column: 1 / -1;
  }
}

@media (max-width: 600px) {
  .page-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .filters,
  .form-grid {
    grid-template-columns: 1fr;
  }

  .search-field,
  .field-wide {
    grid-column: auto;
  }

  .pagination {
    align-items: flex-start;
    flex-direction: column;
    padding: 1rem;
  }

  .page-controls {
    flex-wrap: wrap;
  }
}
</style>
