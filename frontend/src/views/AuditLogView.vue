<template>
  <section class="audit-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">MOD_AUTH · F_AUTH_06</p>
        <h1>Nhật ký thao tác</h1>
        <p class="subtitle">Tra cứu các hoạt động đã ghi nhận trong hệ thống.</p>
      </div>
    </header>

    <p v-if="pageError" class="alert alert-error" role="alert">{{ pageError }}</p>

    <form class="panel filters" aria-label="Bộ lọc nhật ký" @submit.prevent="applyFilters">
      <label class="filter-field">
        <span>Người thực hiện</span>
        <input v-model="filters.performedBy" type="search" placeholder="Tên hoặc mã tài khoản" />
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
        <input v-model="filters.entityType" type="search" placeholder="Ví dụ: User" />
      </label>
      <label class="filter-field">
        <span>Từ ngày</span>
        <input v-model="filters.fromDate" type="date" />
      </label>
      <label class="filter-field">
        <span>Đến ngày</span>
        <input v-model="filters.toDate" type="date" />
      </label>
      <button class="button button-primary" type="submit" :disabled="loading">Lọc nhật ký</button>
    </form>

    <section class="panel list-panel">
      <div v-if="loading" class="empty-state">Đang tải nhật ký...</div>
      <div v-else-if="!items.length" class="empty-state">Không có nhật ký phù hợp.</div>
      <div v-else class="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Thời gian (Việt Nam)</th>
              <th>Người thực hiện</th>
              <th>Hành động</th>
              <th>Đối tượng</th>
              <th>Chi tiết</th>
              <th>IP</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="log in items" :key="log.logId">
              <td>{{ formatDate(log.createdAt) }}</td>
              <td>
                <strong>{{ log.userName || 'Hệ thống / Không xác định' }}</strong>
                <small v-if="log.userId !== null">Mã tài khoản #{{ log.userId }}</small>
              </td>
              <td><span class="action-badge">{{ actionLabel(log) }}</span></td>
              <td>{{ log.entityType }}<small v-if="log.entityId !== null">#{{ log.entityId }}</small></td>
              <td>{{ detailSummary(log) }}</td>
              <td>{{ log.ipAddress || '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="pagination">
        <p>Hiển thị {{ firstItem }}–{{ lastItem }} trong {{ totalCount }} nhật ký</p>
        <div class="page-controls">
          <label>
            <span>Dòng/trang</span>
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
.audit-page { max-width: 1280px; margin: 0 auto; color: #223247; }
.page-heading { margin-bottom: 1.5rem; }
.eyebrow { margin: 0 0 .35rem; color: #3980ad; font-size: .73rem; font-weight: 700; letter-spacing: .12em; }
h1 { margin: 0; font-size: clamp(1.65rem, 3vw, 2.1rem); }
.subtitle { margin: .45rem 0 0; color: #6b7b8d; line-height: 1.5; }
.panel { border: 1px solid #e3eaf0; border-radius: 13px; background: #fff; box-shadow: 0 8px 24px rgba(30, 56, 79, .05); }
.filters { display: grid; grid-template-columns: repeat(3, minmax(150px, 1fr)) auto; align-items: end; gap: .85rem; padding: 1rem; margin-bottom: 1rem; }
.filter-field, .page-controls label { display: grid; gap: .4rem; color: #405467; font-size: .82rem; font-weight: 600; }
.filter-field input, .filter-field select, .page-controls select { box-sizing: border-box; width: 100%; min-height: 2.55rem; padding: .55rem .7rem; border: 1px solid #d3dde6; border-radius: 8px; background: #fff; color: #26394c; font: inherit; font-size: .88rem; }
.list-panel { overflow: hidden; }
.table-scroll { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; text-align: left; min-width: 950px; }
th, td { padding: .85rem .9rem; border-bottom: 1px solid #edf1f4; vertical-align: middle; }
th { background: #f8fafb; color: #68798a; font-size: .73rem; font-weight: 700; text-transform: uppercase; }
td { color: #46596b; font-size: .84rem; }
td strong, td small { display: block; }
td strong { color: #26394c; }
td small { margin-top: .2rem; color: #8593a0; font-size: .74rem; }
.action-badge { display: inline-flex; padding: .3rem .6rem; border-radius: 999px; background: #eef4f8; color: #49677e; font-size: .75rem; font-weight: 700; }
.pagination, .page-controls { display: flex; align-items: center; justify-content: space-between; gap: .85rem; }
.pagination { min-height: 4.25rem; padding: 0 1rem; }
.pagination p { margin: 0; color: #718090; font-size: .82rem; }
.page-controls label { display: flex; align-items: center; }
.page-controls select { width: 4.5rem; min-height: 2.2rem; padding: .3rem; }
.page-number { color: #52677a; font-size: .82rem; white-space: nowrap; }
.button { min-height: 2.55rem; padding: .6rem .9rem; border: 0; border-radius: 8px; font: inherit; font-size: .86rem; font-weight: 700; cursor: pointer; }
.button-primary { background: #3181ad; color: #fff; }
.button-quiet { border: 1px solid #d8e1e8; background: #fff; color: #536779; }
.button:disabled { cursor: not-allowed; opacity: .55; }
.empty-state { padding: 2.6rem 1rem; color: #7b8b99; text-align: center; }
.alert { padding: .75rem .9rem; border-radius: 8px; font-size: .87rem; line-height: 1.45; }
.alert-error { background: #fff0ef; color: #a52b25; }
@media (max-width: 900px) { .filters { grid-template-columns: 1fr 1fr; } }
@media (max-width: 600px) { .filters { grid-template-columns: 1fr; } .pagination { align-items: flex-start; flex-direction: column; padding: 1rem; } .page-controls { flex-wrap: wrap; } }
</style>
