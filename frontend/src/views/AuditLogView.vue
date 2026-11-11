<template>
  <section class="audit-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">NHẬT KÝ BẢO MẬT & VẬN HÀNH</p>
        <h1>Nhật ký kiểm toán & Thao tác (Audit Log)</h1>
        <p class="subtitle">Truy vết toàn diện các hành động quản trị, thay đổi tài khoản, đăng nhập và an ninh hệ thống phòng khám.</p>
      </div>
      <div class="header-badges">
        <span class="badge-audit">🔒 Phân quyền ADMIN</span>
      </div>
    </header>

    <p v-if="pageError" class="alert alert-error" role="alert">{{ pageError }}</p>

    <!-- Panel bộ lọc -->
    <form class="panel filters" aria-label="Bộ lọc nhật ký" @submit.prevent="applyFilters">
      <label class="filter-field">
        <span>Người thực hiện</span>
        <div class="input-icon-wrapper">
          <span class="field-icon">👤</span>
          <input v-model="filters.performedBy" type="search" placeholder="Tên hoặc mã tài khoản..." />
        </div>
      </label>
      <label class="filter-field">
        <span>Hành động</span>
        <select v-model="filters.action">
          <option value="">Tất cả hành động</option>
          <option value="LOGIN">Đăng nhập thành công</option>
          <option value="LOGIN_FAILED">Đăng nhập thất bại</option>
          <option value="LOGOUT">Đăng xuất</option>
          <option value="CREATE">Tạo mới</option>
          <option value="UPDATE">Cập nhật</option>
          <option value="DELETE">Xóa</option>
        </select>
      </label>
      <label class="filter-field">
        <span>Loại đối tượng</span>
        <input v-model="filters.entityType" type="search" placeholder="Ví dụ: User, Staff..." />
      </label>
      <label class="filter-field">
        <span>Từ ngày</span>
        <input v-model="filters.fromDate" type="date" />
      </label>
      <label class="filter-field">
        <span>Đến ngày</span>
        <input v-model="filters.toDate" type="date" />
      </label>
      <div class="filter-actions">
        <button class="button button-primary" type="submit" :disabled="loading">
          <span class="btn-icon">🔍</span> Lọc nhật ký
        </button>
      </div>
    </form>

    <!-- Panel danh sách -->
    <section class="panel list-panel">
      <div v-if="loading" class="empty-state">Đang tải nhật ký thao tác...</div>
      <div v-else-if="!items.length" class="empty-state">Không có nhật ký phù hợp với bộ lọc hiện tại.</div>
      <div v-else class="table-scroll">
        <table class="audit-table">
          <thead>
            <tr>
              <th>Thời gian (UTC+7)</th>
              <th>Người thực hiện</th>
              <th>Hành động</th>
              <th>Đối tượng tác động</th>
              <th>Chi tiết nghiệp vụ</th>
              <th>Địa chỉ IP</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="log in items" :key="log.logId">
              <td class="col-time">
                <span class="time-main">{{ formatDate(log.createdAt) }}</span>
                <span class="log-id">#{{ log.logId }}</span>
              </td>
              <td>
                <div class="user-block">
                  <strong>{{ log.userName || 'Hệ thống / Không xác định' }}</strong>
                  <small v-if="log.userId !== null">Mã NV #{{ log.userId }}</small>
                </div>
              </td>
              <td>
                <span class="action-badge" :class="actionBadgeClass(log.action)">
                  <span class="badge-dot"></span>
                  {{ actionLabel(log) }}
                </span>
              </td>
              <td>
                <div class="entity-block">
                  <span class="entity-type">{{ log.entityType }}</span>
                  <span v-if="log.entityId !== null" class="entity-id">#{{ log.entityId }}</span>
                </div>
              </td>
              <td class="col-detail">
                <div class="detail-text">{{ detailSummary(log) }}</div>
              </td>
              <td>
                <code class="ip-code">{{ log.ipAddress || '—' }}</code>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="pagination">
        <p>Hiển thị <strong>{{ firstItem }}–{{ lastItem }}</strong> trong tổng số <strong>{{ totalCount }}</strong> bản ghi kiểm toán</p>
        <div class="page-controls">
          <label>
            <span>Dòng/trang:</span>
            <select v-model.number="pageSize" :disabled="loading" @change="applyFilters">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
            </select>
          </label>
          <span class="page-number">Trang {{ page }} / {{ totalPages }}</span>
          <button class="button button-quiet" type="button" :disabled="page <= 1 || loading" @click="changePage(page - 1)">Trước</button>
          <button class="button button-quiet" type="button" :disabled="page >= totalPages || loading" @click="changePage(page + 1)">Sau</button>
        </div>
      </footer>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { apiClient } from '@/services/api';

interface AuditLog {
  logId: number;
  userId: number | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: number | null;
  detail: string | null;
  ipAddress: string | null;
  createdAt: string;
}

const filters = reactive({ performedBy: '', action: '', entityType: '', fromDate: '', toDate: '' });
const items = ref<AuditLog[]>([]);
const page = ref(1);
const pageSize = ref(20);
const totalCount = ref(0);
const totalPages = ref(1);
const loading = ref(false);
const pageError = ref('');
let requestNumber = 0;

const firstItem = computed(() => totalCount.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1);
const lastItem = computed(() => Math.min(page.value * pageSize.value, totalCount.value));

onMounted(() => loadLogs());

function backendError(error: any): string {
  const data = error.response?.data;
  if (Array.isArray(data?.errors)) {
    const messages = data.errors.map((item: any) => item?.message).filter(Boolean);
    if (messages.length) return messages.join(' ');
  }
  return data?.message || 'Không tải được nhật ký thao tác. Vui lòng thử lại.';
}

async function loadLogs() {
  const currentRequest = ++requestNumber;
  loading.value = true;
  pageError.value = '';
  try {
    const response = await apiClient.get('/audit-logs', {
      params: {
        page: page.value,
        pageSize: pageSize.value,
        performedBy: filters.performedBy.trim() || undefined,
        action: filters.action || undefined,
        entityType: filters.entityType.trim() || undefined,
        fromDate: filters.fromDate || undefined,
        toDate: filters.toDate || undefined
      }
    });
    if (currentRequest !== requestNumber) return;
    items.value = response.data.items ?? [];
    totalCount.value = response.data.totalCount ?? 0;
    totalPages.value = Math.max(response.data.totalPages ?? 0, 1);
    if (page.value > totalPages.value) {
      page.value = totalPages.value;
      await loadLogs();
    }
  } catch (error: any) {
    if (currentRequest === requestNumber) pageError.value = backendError(error);
  } finally {
    if (currentRequest === requestNumber) loading.value = false;
  }
}

function applyFilters() {
  if (filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
    pageError.value = 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.';
    return;
  }
  page.value = 1;
  void loadLogs();
}

function changePage(nextPage: number) {
  if (nextPage < 1 || nextPage > totalPages.value) return;
  page.value = nextPage;
  void loadLogs();
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh'
  }).format(date);
}

function actionBadgeClass(action: string): string {
  switch (action) {
    case 'LOGIN': return 'badge--login';
    case 'LOGIN_FAILED': return 'badge--danger';
    case 'LOGOUT': return 'badge--logout';
    case 'CREATE': return 'badge--create';
    case 'UPDATE': return 'badge--update';
    case 'DELETE': return 'badge--delete';
    default: return 'badge--neutral';
  }
}

function actionLabel(log: AuditLog): string {
  const labels: Record<string, string> = {
    LOGIN: 'Đăng nhập thành công',
    LOGIN_FAILED: 'Đăng nhập thất bại',
    LOGOUT: 'Đăng xuất',
    CREATE: 'Tạo mới',
    UPDATE: 'Cập nhật',
    DELETE: 'Xóa'
  };
  return labels[log.action] ?? log.action;
}

function detailSummary(log: AuditLog): string {
  if (!log.detail) return '—';
  try {
    const detail = JSON.parse(log.detail) as { action?: string; reason?: string; changedFields?: string[] };
    const actionLabels: Record<string, string> = {
      update_profile: 'Cập nhật hồ sơ cá nhân',
      create_staff: 'Tạo tài khoản nhân viên',
      update_staff: 'Cập nhật tài khoản nhân viên',
      lock_staff: 'Khóa tài khoản nhân viên',
      unlock_staff: 'Mở khóa tài khoản nhân viên',
      change_password_success: 'Đổi mật khẩu thành công',
      change_password_failed: 'Đổi mật khẩu thất bại',
      user_logout: 'Người dùng đăng xuất'
    };
    const reasonLabels: Record<string, string> = {
      phone_not_found: 'Không tìm thấy tài khoản',
      wrong_password: 'Mật khẩu không đúng',
      account_locked: 'Tài khoản đã bị khóa',
      wrong_old_password: 'Mật khẩu hiện tại không đúng'
    };
    const action = detail.action ? actionLabels[detail.action] : undefined;
    const reason = detail.reason ? reasonLabels[detail.reason] : undefined;
    const changedFields = Array.isArray(detail.changedFields) ? detail.changedFields.join(', ') : '';
    return [action, reason, changedFields ? `Trường thay đổi: ${changedFields}` : ''].filter(Boolean).join(' · ') || 'Đã ghi nhận';
  } catch {
    return 'Đã ghi nhận';
  }
}
</script>

<style scoped>
.audit-page {
  max-width: 1300px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
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

.badge-audit {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background: #f1f5f9;
  color: #475569;
  border: 1px solid #cbd5e1;
  font-size: 0.8rem;
  font-weight: 750;
  padding: 0.35rem 0.75rem;
  border-radius: 999px;
}

.panel {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.filters {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)) auto;
  align-items: end;
  gap: 1rem;
  padding: 1.15rem 1.25rem;
}

.filter-field,
.page-controls label {
  display: grid;
  gap: 0.35rem;
  color: #334155;
  font-size: 0.825rem;
  font-weight: 600;
}

.input-icon-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.field-icon {
  position: absolute;
  left: 0.75rem;
  color: #94a3b8;
  font-size: 0.85rem;
  pointer-events: none;
}

.input-icon-wrapper input {
  padding-left: 2.25rem !important;
}

.filter-field input,
.filter-field select,
.page-controls select {
  box-sizing: border-box;
  width: 100%;
  height: 40px;
  padding: 0 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #f8fafc;
  color: #1e293b;
  font: inherit;
  font-size: 0.875rem;
  outline: none;
  transition: all 0.15s ease;
}

.filter-field input:focus,
.filter-field select:focus,
.page-controls select:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.filter-actions {
  display: flex;
  align-items: flex-end;
}

.list-panel {
  overflow: hidden;
}

.table-scroll {
  overflow-x: auto;
}

.audit-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  min-width: 980px;
}

.audit-table th,
.audit-table td {
  padding: 0.85rem 1rem;
  border-bottom: 1px solid #f1f5f9;
  vertical-align: middle;
}

.audit-table thead th {
  background: #f8fafc;
  color: #475569;
  font-size: 0.775rem;
  font-weight: 750;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid #e2e8f0;
}

.col-time {
  white-space: nowrap;
}

.time-main {
  display: block;
  font-size: 0.825rem;
  font-weight: 700;
  color: #0f2d59;
}

.log-id {
  font-family: monospace;
  font-size: 0.72rem;
  color: #94a3b8;
}

.user-block strong {
  display: block;
  color: #0f2d59;
  font-size: 0.885rem;
}

.user-block small {
  display: block;
  color: #64748b;
  font-size: 0.75rem;
}

.action-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.25rem 0.65rem;
  border-radius: 6px;
  font-size: 0.76rem;
  font-weight: 750;
  white-space: nowrap;
}

.badge-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.badge--login {
  background: #ecfdf5;
  color: #059669;
  border: 1px solid #a7f3d0;
}

.badge--danger {
  background: #fef2f2;
  color: #dc2626;
  border: 1px solid #fecaca;
}

.badge--logout {
  background: #f1f5f9;
  color: #475569;
  border: 1px solid #cbd5e1;
}

.badge--create {
  background: #eff6ff;
  color: #2563eb;
  border: 1px solid #bfdbfe;
}

.badge--update {
  background: #fffbeb;
  color: #d97706;
  border: 1px solid #fde68a;
}

.badge--delete {
  background: #fef2f2;
  color: #b91c1c;
  border: 1px solid #fca5a5;
}

.badge--neutral {
  background: #f8fafc;
  color: #64748b;
  border: 1px solid #e2e8f0;
}

.entity-block {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.entity-type {
  font-weight: 700;
  color: #334155;
  font-size: 0.85rem;
}

.entity-id {
  font-family: monospace;
  font-size: 0.75rem;
  color: #0284c7;
  background: #f0f9ff;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.col-detail {
  max-width: 320px;
}

.detail-text {
  font-size: 0.825rem;
  color: #475569;
  line-height: 1.45;
  word-break: break-word;
}

.ip-code {
  font-family: monospace;
  font-size: 0.78rem;
  background: #f1f5f9;
  color: #475569;
  padding: 0.2rem 0.45rem;
  border-radius: 4px;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 0.9rem 1.25rem;
  background: #f8fafc;
  border-top: 1px solid #e2e8f0;
}

.pagination p {
  margin: 0;
  color: #64748b;
  font-size: 0.825rem;
}

.page-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.page-controls label {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.page-controls select {
  height: 34px;
  width: 4.8rem;
  padding: 0 0.5rem;
}

.page-number {
  color: #334155;
  font-size: 0.825rem;
  font-weight: 600;
  white-space: nowrap;
}

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
  transition: all 0.15s ease;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
}

.button-primary:hover:not(:disabled) {
  background: #0369a1;
}

.button-quiet {
  background: #ffffff;
  border-color: #cbd5e1;
  color: #475569;
}

.button-quiet:hover:not(:disabled) {
  background: #f1f5f9;
  color: #0f172a;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.btn-icon {
  font-size: 0.95rem;
}

.empty-state {
  padding: 3.5rem 1rem;
  color: #64748b;
  text-align: center;
  font-size: 0.95rem;
}

.alert {
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 600;
  margin: 0;
}

.alert-error {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fee2e2;
}

@media (max-width: 900px) {
  .filters {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 600px) {
  .filters {
    grid-template-columns: 1fr;
  }

  .pagination {
    align-items: flex-start;
    flex-direction: column;
  }

  .page-controls {
    flex-wrap: wrap;
  }
}
</style>
