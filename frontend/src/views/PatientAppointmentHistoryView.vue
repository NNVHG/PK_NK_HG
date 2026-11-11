<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">Hồ sơ lịch khám</p>
        <h1>Lịch hẹn của tôi</h1>
        <p class="subtitle">Theo dõi, đổi lịch hoặc hủy ca hẹn điều trị của bạn và người được giám hộ</p>
      </div>
      <div class="header-actions">
        <RouterLink to="/patient/appointments" class="button button-primary">
          <span>📅 Đặt lịch khám mới</span>
        </RouterLink>
      </div>
    </div>

    <!-- Alert / Notice -->
    <div v-if="notice" class="alert alert-success" role="status">
      <span class="alert-icon">✅</span>
      <span>{{ notice }}</span>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">
      <span class="alert-icon">⚠️</span>
      <span>{{ error }}</span>
    </div>

    <!-- Filter & Control Bar -->
    <div class="panel control-panel">
      <div class="filter-group">
        <label for="status" class="filter-label">Lọc trạng thái ca hẹn:</label>
        <select id="status" v-model="status" class="filter-select" @change="load(1)">
          <option value="">Tất cả trạng thái</option>
          <option value="Scheduled">Đã đặt lịch</option>
          <option value="CheckedIn">Đã tiếp đón</option>
          <option value="Completed">Hoàn tất khám</option>
          <option value="Cancelled">Đã hủy</option>
        </select>
      </div>

      <div class="control-actions">
        <button type="button" class="button button-secondary" :disabled="loading" @click="refresh">
          <span :class="{ 'spin-icon': loading }">🔄</span>
          <span>Làm mới danh sách</span>
        </button>
      </div>
    </div>

    <!-- Content List -->
    <div v-if="loading" class="panel status-panel" role="status">
      <div class="loading-spinner"></div>
      <p>Đang tải danh sách lịch hẹn…</p>
    </div>

    <div v-else-if="!rows.length" class="panel empty-panel">
      <span class="empty-icon">📅</span>
      <h3>Không có lịch hẹn phù hợp</h3>
      <p>Bạn chưa có ca hẹn nào thuộc trạng thái này hoặc chưa đặt lịch khám.</p>
      <RouterLink to="/patient/appointments" class="button button-primary">
        Đặt lịch khám ngay
      </RouterLink>
    </div>

    <div v-else class="appointments-list">
      <div v-for="row in rows" :key="row.appointmentId" class="appointment-card">
        <div class="card-main">
          <div class="card-header-row">
            <div class="patient-info">
              <span class="appointment-id">#{{ row.appointmentId }}</span>
              <h3 class="patient-name">{{ row.patientName }}</h3>
              <span class="code-badge">{{ row.patientCode }}</span>
            </div>
            <span class="status-badge" :class="'status-' + row.status.toLowerCase()">
              {{ statusLabel(row.status) }}
            </span>
          </div>

          <div class="card-meta-grid">
            <div class="meta-item">
              <span class="meta-icon">📅</span>
              <div>
                <span class="meta-title">Ngày khám</span>
                <span class="meta-data">{{ formatDate(row.appointmentDate) }}</span>
              </div>
            </div>

            <div class="meta-item">
              <span class="meta-icon">⏰</span>
              <div>
                <span class="meta-title">Khung giờ</span>
                <span class="meta-data">{{ row.slotTime.slice(0, 5) }}</span>
              </div>
            </div>

            <div v-if="row.dentistName" class="meta-item">
              <span class="meta-icon">👨‍⚕️</span>
              <div>
                <span class="meta-title">Nha sĩ phụ trách</span>
                <span class="meta-data">{{ row.dentistName }}</span>
              </div>
            </div>
          </div>
        </div>

        <div v-if="row.status === 'Scheduled'" class="card-actions">
          <AppointmentRescheduling
            :appointment-id="row.appointmentId"
            :patient-name="row.patientName"
            self-service
            @rescheduled="rescheduled"
          />
          <PatientAppointmentCancellation
            :appointment-id="row.appointmentId"
            :patient-name="row.patientName"
            :date="row.appointmentDate"
            :time="row.slotTime"
            @cancelled="load(page)"
          />
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="total > 0" class="panel pagination-panel" aria-label="Phân trang lịch hẹn">
      <span class="pagination-info">
        Tổng cộng <strong>{{ total }}</strong> lịch hẹn · Trang <strong>{{ page }}</strong>
      </span>
      <div class="pagination-buttons">
        <button
          type="button"
          class="button button-sm button-secondary"
          :disabled="loading || page <= 1"
          @click="load(page - 1)"
        >
          ← Trang trước
        </button>
        <button
          type="button"
          class="button button-sm button-secondary"
          :disabled="loading || !hasNext"
          @click="load(page + 1)"
        >
          Trang sau →
        </button>
      </div>
    </div>

    <aside class="help-box">
      <span class="help-icon">ℹ️</span>
      <p>Cần hỗ trợ gấp hoặc muốn thay đổi lịch cận giờ? Vui lòng gọi trực tiếp hotline phòng khám để được Lễ tân điều phối nhanh nhất.</p>
    </aside>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import PatientAppointmentCancellation from '@/components/PatientAppointmentCancellation.vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';

const status = ref('');
const notice = ref('');
const rows = ref<AppointmentResponse[]>([]), page = ref(1), total = ref(0), hasNext = ref(false), loading = ref(false), error = ref('');
let version = 0;

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}

async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 ||
      !['', 'Scheduled', 'CheckedIn', 'Cancelled', 'Completed'].includes(status.value)) return;
  const ticket = ++version; rows.value = []; error.value = ''; loading.value = true; total.value = 0; hasNext.value = false;
  try {
    const result = await appointmentsService.getMine(targetPage, status.value || undefined);
    if (ticket === version) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}

function statusLabel(value: string) {
  return ({
    Scheduled: 'Đã đặt lịch',
    CheckedIn: 'Đã tiếp đón',
    Cancelled: 'Đã hủy',
    Completed: 'Hoàn tất'
  } as Record<string, string>)[value] ?? value;
}

async function refresh() { await load(1); }
async function rescheduled() { notice.value = 'Đã đổi lịch thành công. Kiểm tra ngày, giờ mới trong danh sách.'; await load(page.value); }
onMounted(refresh);
onUnmounted(() => { version++; rows.value = []; total.value = 0; notice.value = ''; });
</script>
<style scoped>
.page-container {
  max-width: 960px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
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

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.control-panel {
  padding: 1rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.filter-label {
  font-size: 0.835rem;
  font-weight: 700;
  color: #334155;
}

.filter-select {
  height: 38px;
  padding: 0 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font: inherit;
  font-size: 0.85rem;
  color: #1e293b;
  background: #ffffff;
}

.filter-select:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

/* Appointments List */
.appointments-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.appointment-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1.25rem;
  transition: all 0.15s ease;
}

.appointment-card:hover {
  border-color: #cbd5e1;
  box-shadow: 0 4px 12px rgba(15, 45, 89, 0.05);
}

@media (max-width: 680px) {
  .appointment-card {
    flex-direction: column;
    align-items: flex-start;
  }
  .card-actions {
    width: 100%;
    justify-content: flex-end;
  }
}

.card-main {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  flex-grow: 1;
}

.card-header-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.patient-info {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.appointment-id {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.825rem;
  font-weight: 750;
  color: #64748b;
}

.patient-name {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.75rem;
  font-weight: 700;
  background: #f1f5f9;
  color: #334155;
  padding: 0.1rem 0.45rem;
  border-radius: 4px;
}

.card-meta-grid {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.meta-icon {
  font-size: 1.1rem;
}

.meta-title {
  display: block;
  font-size: 0.725rem;
  color: #64748b;
}

.meta-data {
  display: block;
  font-size: 0.85rem;
  font-weight: 700;
  color: #1e293b;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  flex-shrink: 0;
}

/* Status Badges */
.status-badge {
  font-size: 0.775rem;
  font-weight: 750;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
}

.status-scheduled {
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
}

.status-checkedin {
  background: #fef3c7;
  color: #92400e;
  border: 1px solid #fde68a;
}

.status-completed {
  background: #dcfce7;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.status-cancelled {
  background: #f1f5f9;
  color: #64748b;
  border: 1px solid #e2e8f0;
}

/* Pagination */
.pagination-panel {
  padding: 0.85rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.pagination-info {
  font-size: 0.835rem;
  color: #64748b;
}

.pagination-buttons {
  display: flex;
  gap: 0.5rem;
}

/* Buttons */
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
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-sm {
  height: 34px;
  padding: 0 0.85rem;
  font-size: 0.825rem;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
}

.button-primary:hover:not(:disabled) {
  background: #0369a1;
}

.button-secondary {
  background: #ffffff;
  color: #334155;
  border-color: #cbd5e1;
}

.button-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Alerts & Status */
.alert {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  font-size: 0.865rem;
}

.alert-icon {
  font-size: 1.2rem;
  flex-shrink: 0;
}

.alert-error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
}

.alert-success {
  background: #ecfdf5;
  border: 1px solid #a7f3d0;
  color: #065f46;
}

.status-panel,
.empty-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 3rem 1rem;
  color: #64748b;
  text-align: center;
}

.empty-icon {
  font-size: 2.2rem;
}

.empty-panel h3 {
  margin: 0;
  color: #0f2d59;
  font-size: 1.1rem;
}

.empty-panel p {
  margin: 0 0 0.5rem;
  font-size: 0.85rem;
}

.loading-spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e2e8f0;
  border-top-color: #0284c7;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.spin-icon {
  animation: spin 1s linear infinite;
  display: inline-block;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.help-box {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.825rem;
  color: #475569;
}

.help-icon {
  font-size: 1.25rem;
  flex-shrink: 0;
}

.help-box p {
  margin: 0;
}
</style>
