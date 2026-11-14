<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">Cổng thông tin bệnh nhân</p>
        <h1>Lịch hẹn khám bệnh</h1>
        <p class="subtitle">Quản lý các ca hẹn điều trị và đặt lịch khám trực tiếp với phòng khám</p>
      </div>
      <div class="header-actions">
        <button
          v-if="activeTab === 'list'"
          type="button"
          class="button button-primary"
          @click="activeTab = 'book'"
        >
          <span>📅 Đặt lịch khám mới</span>
        </button>
        <button
          v-else
          type="button"
          class="button button-secondary"
          @click="activeTab = 'list'"
        >
          <span>📋 Xem danh sách lịch hẹn</span>
        </button>
      </div>
    </div>

    <!-- Navigation Tabs -->
    <div class="appointment-tabs-bar" role="tablist" aria-label="Khu vực lịch hẹn bệnh nhân">
      <button
        type="button"
        role="tab"
        :aria-selected="activeTab === 'list'"
        class="tab-btn"
        :class="{ active: activeTab === 'list' }"
        @click="activeTab = 'list'"
      >
        <span class="tab-icon">📋</span>
        <span>Lịch hẹn của tôi</span>
        <span v-if="total > 0" class="tab-badge">{{ total }}</span>
      </button>

      <button
        type="button"
        role="tab"
        :aria-selected="activeTab === 'book'"
        class="tab-btn"
        :class="{ active: activeTab === 'book' }"
        @click="activeTab = 'book'"
      >
        <span class="tab-icon">✨</span>
        <span>Đặt lịch khám điều trị</span>
      </button>
    </div>

    <!-- ==================== TAB 1: DANH SÁCH LỊCH HẸN ==================== -->
    <div v-show="activeTab === 'list'" class="tab-content">
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
        <button type="button" class="button button-primary" @click="activeTab = 'book'">
          Đặt lịch khám ngay
        </button>
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
    </div>

    <!-- ==================== TAB 2: ĐẶT LỊCH KHÁM MỚI ==================== -->
    <div v-show="activeTab === 'book'" class="tab-content">
      <PatientAppointmentForm @success="onBookingSuccess" />
    </div>
  </main>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import PatientAppointmentCancellation from '@/components/PatientAppointmentCancellation.vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import PatientAppointmentForm from '@/components/PatientAppointmentForm.vue';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';

const route = useRoute();

// Tab state: 'list' | 'book'
const activeTab = ref<'list' | 'book'>('list');

watch(() => route.query.tab, (tab) => {
  if (tab === 'book') activeTab.value = 'book';
  else if (tab === 'list') activeTab.value = 'list';
}, { immediate: true });

const status = ref('');
const notice = ref('');
const rows = ref<AppointmentResponse[]>([]);
const page = ref(1);
const total = ref(0);
const hasNext = ref(false);
const loading = ref(false);
const error = ref('');
let version = 0;

function formatDate(value: string): string {
  if (!value) return '';
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}

async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 ||
      !['', 'Scheduled', 'CheckedIn', 'Cancelled', 'Completed'].includes(status.value)) return;
  const ticket = ++version;
  rows.value = [];
  error.value = '';
  loading.value = true;
  total.value = 0;
  hasNext.value = false;
  try {
    const result = await appointmentsService.getMine(targetPage, status.value || undefined);
    if (ticket === version) {
      rows.value = result.items;
      page.value = result.page;
      total.value = result.totalCount;
      hasNext.value = result.hasNextPage;
    }
  } catch {
    if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.';
  } finally {
    if (ticket === version) loading.value = false;
  }
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
async function rescheduled() {
  notice.value = 'Đã đổi lịch thành công. Kiểm tra ngày, giờ mới trong danh sách.';
  await load(page.value);
}

function onBookingSuccess(_appointment: AppointmentResponse) {
  notice.value = 'Đặt lịch thành công! Ca hẹn mới đã được thêm vào danh sách.';
  activeTab.value = 'list';
  void load(1);
}

onMounted(refresh);
onUnmounted(() => {
  version++;
  rows.value = [];
  total.value = 0;
  notice.value = '';
});
</script>

<style scoped>
.page-container {
  max-width: 1040px;
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
  gap: 1rem;
  flex-wrap: wrap;
}

.eyebrow {
  font-size: 0.8rem;
  font-weight: 700;
  color: #0284c7;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 0.25rem;
}

h1 {
  font-size: 1.65rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 0.35rem 0;
  line-height: 1.2;
}

.subtitle {
  color: #64748b;
  font-size: 0.925rem;
  margin: 0;
}

/* Tabs Bar */
.appointment-tabs-bar {
  display: flex;
  gap: 0.5rem;
  border-bottom: 2px solid #e2e8f0;
  padding-bottom: 0.25rem;
}

.tab-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  background: transparent;
  border: none;
  border-bottom: 3px solid transparent;
  padding: 0.65rem 1.15rem;
  font-size: 0.95rem;
  font-weight: 750;
  color: #64748b;
  cursor: pointer;
  transition: all 0.2s ease;
  margin-bottom: -0.35rem;
  border-radius: 6px 6px 0 0;
}

.tab-btn:hover {
  color: #0284c7;
  background: #f0f9ff;
}

.tab-btn.active {
  color: #0284c7;
  border-bottom-color: #0284c7;
  background: #ffffff;
}

.tab-icon {
  font-size: 1.1rem;
}

.tab-badge {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.75rem;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  font-weight: 800;
}

.tab-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

/* Buttons */
.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  padding: 0.55rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 750;
  cursor: pointer;
  border: 1px solid transparent;
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
  box-shadow: 0 1px 3px rgba(2, 132, 199, 0.25);
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
  background: #f8fafc;
  border-color: #94a3b8;
}

.button-sm {
  padding: 0.35rem 0.75rem;
  font-size: 0.8rem;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Alerts */
.alert {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  font-size: 0.875rem;
  line-height: 1.45;
}
.alert-success { background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fecaca; }
.alert-icon { font-size: 1.15rem; flex-shrink: 0; }

/* Control panel */
.control-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 0.85rem 1.15rem;
  background: #ffffff;
  border-radius: 10px;
  border: 1px solid #e2e8f0;
  flex-wrap: wrap;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.filter-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: #475569;
}

.filter-select {
  padding: 0.4rem 0.75rem;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  font-size: 0.85rem;
  background: #fff;
}

/* Appointments List */
.appointments-list {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.appointment-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.15rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  transition: all 0.2s ease;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
}

.appointment-card:hover {
  border-color: #cbd5e1;
  box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);
}

.card-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.patient-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.appointment-id {
  font-size: 0.75rem;
  font-weight: 800;
  color: #64748b;
  background: #f1f5f9;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.patient-name {
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f172a;
  margin: 0;
}

.code-badge {
  font-size: 0.75rem;
  font-family: monospace;
  background: #e0f2fe;
  color: #0369a1;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.status-badge {
  font-size: 0.75rem;
  font-weight: 800;
  padding: 0.25rem 0.65rem;
  border-radius: 999px;
  text-transform: uppercase;
}

.status-scheduled { background: #dbeafe; color: #1e40af; }
.status-checkedin { background: #fef3c7; color: #92400e; }
.status-completed { background: #dcfce7; color: #166534; }
.status-cancelled { background: #fee2e2; color: #991b1b; }

.card-meta-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.85rem;
  background: #f8fafc;
  padding: 0.85rem;
  border-radius: 8px;
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.meta-icon {
  font-size: 1.25rem;
}

.meta-title {
  display: block;
  font-size: 0.725rem;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
}

.meta-data {
  display: block;
  font-size: 0.9rem;
  font-weight: 750;
  color: #1e293b;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  border-top: 1px solid #f1f5f9;
  padding-top: 0.85rem;
  flex-wrap: wrap;
}

/* Empty & Status panel */
.empty-panel, .status-panel {
  padding: 3rem 1.5rem;
  text-align: center;
  background: #ffffff;
  border-radius: 12px;
  border: 1px dashed #cbd5e1;
}

.empty-icon { font-size: 2.5rem; display: block; margin-bottom: 0.5rem; }
.loading-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e2e8f0;
  border-top-color: #0284c7;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 0.75rem;
}

/* Pagination */
.pagination-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: #ffffff;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  font-size: 0.85rem;
}

.help-box {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  font-size: 0.825rem;
  color: #475569;
}

.panel {
  background: #ffffff;
  border-radius: 12px;
  border: 1px solid #e2e8f0;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
