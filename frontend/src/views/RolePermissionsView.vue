<template>
  <main v-if="auth.role === 'ADMIN'" class="permissions-page">
    <nav class="sub-nav" aria-label="Quản lý tài khoản và quyền">
      <RouterLink to="/management/admin/staff" class="nav-link">
        <span class="nav-icon">👥</span> Tài khoản nhân viên
      </RouterLink>
      <span class="nav-sep" aria-hidden="true">/</span>
      <RouterLink to="/management/admin/role-permissions" class="nav-link nav-link--active" aria-current="page">
        <span class="nav-icon">🛡️</span> Ma trận quyền RBAC
      </RouterLink>
    </nav>

    <header class="page-heading">
      <div>
        <p class="eyebrow">QUẢN TRỊ TỔ CHỨC & PHÂN QUYỀN</p>
        <h1>Ma trận phân quyền hệ thống (RBAC)</h1>
        <p class="subtitle">
          Quy chuẩn phân quyền cố định trên 5 vai trò theo thiết kế bảo mật y tế. Bảng phân quyền mang tính tham chiếu và kiểm soát quyền truy cập nghiệp vụ.
        </p>
      </div>
      <button class="button button-secondary reload-btn" :disabled="loading" @click="load">
        <span class="btn-icon">{{ loading ? '⏳' : '🔄' }}</span>
        {{ loading ? 'Đang tải…' : 'Tải lại cấu hình' }}
      </button>
    </header>

    <div class="info-card">
      <div class="info-icon">ℹ️</div>
      <div class="info-content">
        <strong>Nguyên tắc phân quyền 5 vai trò bất biến:</strong>
        <ul>
          <li><strong>ADMIN:</strong> Toàn quyền kiểm soát tài khoản, danh mục kỹ thuật, giá dịch vụ, vật tư, kho và báo cáo doanh thu.</li>
          <li><strong>DENTIST (Nha sĩ):</strong> Phụ trách chẩn đoán lâm sàng, sơ đồ răng FDI, kê đơn thuốc và chỉ định dịch vụ.</li>
          <li><strong>ASSISTANT (Phụ tá):</strong> Hỗ trợ nhập liệu lâm sàng, ảnh chụp, quản lý trạng thái ghế và xác nhận tiêu hao ca khám.</li>
          <li><strong>RECEPTIONIST (Lễ tân/Thu ngân):</strong> Tiếp đón bệnh nhân, điều phối hàng đợi, lập hóa đơn và thu tiền.</li>
          <li><strong>PATIENT (Bệnh nhân):</strong> Xem hồ sơ bệnh án cá nhân và đặt lịch hẹn khám trực tuyến.</li>
        </ul>
      </div>
    </div>

    <p v-if="loading" class="status-msg" role="status">Đang đọc cấu hình ma trận quyền hiện hành…</p>
    <p v-if="error" role="alert" class="alert alert-error">{{ error }}</p>

    <template v-if="matrix">
      <section class="panel filter-bar">
        <div class="filter-controls">
          <label class="filter-label">
            <span>Lọc theo phân hệ (Module)</span>
            <select v-model="moduleFilter" class="select-field">
              <option value="">Tất cả phân hệ ({{ modules.length }})</option>
              <option v-for="module in modules" :key="module" :value="module">{{ module }}</option>
            </select>
          </label>
        </div>
        <div class="filter-summary">
          <span class="badge-count"><strong>{{ visiblePermissions.length }}</strong> nhóm quyền</span>
          <span class="badge-count"><strong>{{ matrix.roles.length }}</strong> vai trò y tế</span>
        </div>
      </section>

      <section class="panel table-panel">
        <div class="table-container" tabindex="0" aria-label="Ma trận quyền có thể cuộn ngang">
          <table class="rbac-table">
            <thead>
              <tr>
                <th scope="col" class="col-perm">Nhóm chức năng & Nghiệp vụ</th>
                <th v-for="role in matrix.roles" :key="role.code" scope="col" class="col-role">
                  <div class="role-header">
                    <span class="role-title">{{ role.name }}</span>
                    <span class="role-code">{{ role.code }}</span>
                  </div>
                </th>
                <th scope="col" class="col-conditions">Điều kiện kiểm tra bổ sung</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="permission in visiblePermissions" :key="permission.code">
                <th scope="row" class="row-perm">
                  <div class="perm-title">{{ permission.name }}</div>
                  <div class="perm-badges">
                    <span class="module-tag">{{ permission.module }}</span>
                    <span class="perm-code">{{ permission.code }}</span>
                  </div>
                </th>
                <td v-for="role in matrix.roles" :key="role.code" class="cell-role">
                  <span
                    v-if="permission.allowedRoles.includes(role.code)"
                    class="access-allowed"
                    title="Được phép theo vai trò"
                  >
                    <span class="icon-check">✓</span> Được phép
                  </span>
                  <span v-else class="access-denied" title="Không có quyền">
                    <span class="icon-dash">—</span>
                  </span>
                </td>
                <td class="cell-conditions">
                  <span v-if="permission.conditions" class="conditions-text">{{ permission.conditions }}</span>
                  <span v-else class="text-muted">— Không yêu cầu thêm —</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="!visiblePermissions.length" class="empty-state">
          Không có nhóm quyền nào trong phân hệ đang chọn.
        </div>
      </section>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { storeToRefs } from 'pinia';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { useRolePermissionsStore } from '@/stores/rolePermissions';
import { rolePermissionsApi } from '@/services/rolePermissions';

const auth = useAuthStore(), store = useRolePermissionsStore();
const { matrix } = storeToRefs(store);
const loading = ref(false), error = ref(''), moduleFilter = ref('');
const modules = computed(() => [...new Set(matrix.value?.permissions.map(x => x.module) ?? [])].sort());
const visiblePermissions = computed(() => matrix.value?.permissions.filter(x => !moduleFilter.value || x.module === moduleFilter.value) ?? []);
let version = 0;
async function load() {
  if (auth.role !== 'ADMIN') { version++; store.replace(null); loading.value = false; error.value = ''; return; }
  if (loading.value) return;
  const ticket = ++version; loading.value = true; error.value = ''; moduleFilter.value = ''; store.replace(null);
  try {
    const result = await rolePermissionsApi.get();
    if (ticket !== version || auth.role !== 'ADMIN') return;
    if (!result.isReadOnly || result.roles.length !== 5 || !result.permissions.length) throw new Error('Invalid matrix');
    store.replace(result);
  } catch (cause) {
    if (ticket === version) error.value = axios.isAxiosError(cause)
      ? cause.response?.data?.message || 'Không thể tải ma trận quyền. Vui lòng tải lại.' : 'Cấu hình ma trận quyền chưa sẵn sàng. Vui lòng tải lại.';
  } finally { if (ticket === version) loading.value = false; }
}
onMounted(load);
onUnmounted(() => { version++; store.replace(null); moduleFilter.value = ''; error.value = ''; });
</script>

<style scoped>
.permissions-page {
  max-width: 1300px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.sub-nav {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  font-size: 0.875rem;
}

.nav-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: #64748b;
  text-decoration: none;
  font-weight: 600;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  transition: all 0.15s ease;
}

.nav-link:hover {
  color: #0284c7;
  background: #f0f9ff;
}

.nav-link--active {
  color: #0284c7;
  background: #e0f2fe;
}

.nav-sep {
  color: #cbd5e1;
}

.page-heading {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
}

.eyebrow {
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  color: #0284c7;
  text-transform: uppercase;
  margin: 0 0 0.25rem;
}

h1 {
  font-size: 1.65rem;
  font-weight: 850;
  color: #0f2d59;
  margin: 0 0 0.4rem;
  line-height: 1.25;
}

.subtitle {
  font-size: 0.885rem;
  color: #475569;
  max-width: 800px;
  margin: 0;
  line-height: 1.45;
}

.info-card {
  display: flex;
  gap: 1rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-left: 4px solid #0284c7;
  border-radius: 10px;
  padding: 1rem 1.25rem;
  font-size: 0.85rem;
  color: #334155;
}

.info-icon {
  font-size: 1.35rem;
  line-height: 1;
}

.info-content strong {
  color: #0f2d59;
  display: block;
  margin-bottom: 0.45rem;
}

.info-content ul {
  margin: 0;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.button {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  height: 40px;
  padding: 0 1.15rem;
  font-size: 0.875rem;
  font-weight: 700;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s ease;
  border: 1px solid transparent;
}

.button-secondary {
  background: #ffffff;
  color: #334155;
  border-color: #cbd5e1;
}

.button-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
  color: #0f172a;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.filter-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 0.85rem 1.25rem;
}

.filter-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: #334155;
}

.select-field {
  height: 38px;
  padding: 0 0.85rem;
  font-size: 0.875rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  background-color: #f8fafc;
  color: #1e293b;
  outline: none;
}

.select-field:focus {
  border-color: #0284c7;
  background-color: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.filter-summary {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.badge-count {
  background: #f1f5f9;
  color: #475569;
  font-size: 0.8rem;
  padding: 0.3rem 0.65rem;
  border-radius: 6px;
  border: 1px solid #e2e8f0;
}

.table-panel {
  overflow: hidden;
}

.table-container {
  overflow-x: auto;
}

.rbac-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  min-width: 980px;
}

.rbac-table th,
.rbac-table td {
  padding: 0.85rem 1rem;
  border-bottom: 1px solid #f1f5f9;
  vertical-align: middle;
}

.rbac-table thead th {
  background: #f8fafc;
  color: #475569;
  font-size: 0.8rem;
  font-weight: 750;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid #e2e8f0;
}

.col-perm {
  width: 28%;
}

.col-role {
  width: 11%;
  text-align: center;
}

.col-conditions {
  width: 17%;
}

.role-header {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.role-title {
  color: #0f2d59;
  font-size: 0.825rem;
  font-weight: 800;
}

.role-code {
  font-size: 0.685rem;
  color: #64748b;
  font-family: monospace;
}

.row-perm {
  font-weight: normal;
}

.perm-title {
  font-weight: 700;
  color: #0f2d59;
  font-size: 0.885rem;
  margin-bottom: 0.25rem;
}

.perm-badges {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.module-tag {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.685rem;
  font-weight: 800;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.perm-code {
  font-family: monospace;
  font-size: 0.7rem;
  color: #64748b;
}

.cell-role {
  text-align: center;
}

.access-allowed {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  background: #ecfdf5;
  color: #059669;
  border: 1px solid #a7f3d0;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 0.25rem 0.55rem;
  border-radius: 6px;
  white-space: nowrap;
}

.icon-check {
  font-weight: 900;
}

.access-denied {
  color: #94a3b8;
  font-weight: 700;
  font-size: 0.95rem;
}

.cell-conditions {
  font-size: 0.825rem;
}

.conditions-text {
  color: #b45309;
  background: #fffbeb;
  border: 1px solid #fef3c7;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  display: inline-block;
}

.text-muted {
  color: #94a3b8;
  font-style: italic;
  font-size: 0.775rem;
}

.empty-state {
  padding: 3rem;
  text-align: center;
  color: #64748b;
  font-size: 0.95rem;
}

.alert {
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 600;
}

.alert-error {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fee2e2;
}

.status-msg {
  color: #0284c7;
  font-size: 0.875rem;
  font-weight: 600;
}
</style>
