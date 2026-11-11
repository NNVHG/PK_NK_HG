<template>
  <section aria-label="Lịch sử đơn thuốc" class="rx-history">
    <div class="rx-header">
      <div class="header-info">
        <h2 ref="heading" tabindex="-1" class="rx-title">Đơn thuốc điện tử</h2>
        <p class="rx-subtitle">Lịch sử kê đơn và hướng dẫn dùng thuốc qua các lần khám</p>
      </div>
      <button type="button" class="btn-refresh" :disabled="busy" @click="load(1)">
        <span class="refresh-icon" :class="{ 'spin': busy }">🔄</span>
        <span>{{ busy ? 'Đang tải…' : 'Tải lại danh sách' }}</span>
      </button>
    </div>

    <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>

    <template v-if="history">
      <p v-if="!history.items.length" class="empty-state">
        Chưa có đơn thuốc nào trong hồ sơ của bệnh nhân.
      </p>
      <ol v-else class="rx-card-list">
        <li v-for="row in history.items" :key="row.id" class="rx-card">
          <div class="card-main">
            <div class="rx-badge-row">
              <span class="rx-code-badge">{{ row.prescriptionCode }}</span>
              <span
                class="status-pill"
                :class="row.isCancelled ? 'pill-cancelled' : row.dispensedAt ? 'pill-dispensed' : 'pill-pending'"
              >
                {{ row.isCancelled ? 'ĐÃ HỦY' : row.dispensedAt ? 'Đã giao thuốc' : 'Chưa giao thuốc' }}
              </span>
            </div>
            <div class="rx-meta-grid">
              <div class="meta-item">
                <span class="meta-label">Ngày kê:</span>
                <span class="meta-val">{{ date(row.createdAt) }}</span>
              </div>
              <div class="meta-item">
                <span class="meta-label">Nha sĩ:</span>
                <span class="meta-val">{{ row.doctorName }}</span>
              </div>
              <div class="meta-item">
                <span class="meta-label">Số loại thuốc:</span>
                <span class="meta-val font-semibold">{{ row.medicineCount }} loại</span>
              </div>
              <div class="meta-item full-width">
                <span class="meta-label">Chẩn đoán:</span>
                <span class="meta-val">{{ row.visitDiagnosis || 'Chưa ghi nhận' }}</span>
              </div>
            </div>
          </div>
          <div class="card-actions">
            <button type="button" class="btn-view-detail" :disabled="busy" @click="showDetail(row)">
              <span>Xem chi tiết</span>
              <span aria-hidden="true">→</span>
            </button>
          </div>
        </li>
      </ol>

      <nav v-if="history.totalPages > 1" aria-label="Phân trang đơn thuốc" class="pagination-bar">
        <button
          type="button"
          class="btn-page"
          :disabled="busy || !history.hasPreviousPage"
          @click="load(history.page - 1)"
        >
          ← Trang trước
        </button>
        <span class="page-indicator">
          Trang <strong>{{ history.page }}</strong> / {{ Math.max(1, history.totalPages) }} · Tổng {{ history.totalCount }} đơn
        </span>
        <button
          type="button"
          class="btn-page"
          :disabled="busy || !history.hasNextPage"
          @click="load(history.page + 1)"
        >
          Trang sau →
        </button>
      </nav>
    </template>
    <section
      v-if="detail"
      ref="detailHeading"
      tabindex="-1"
      aria-label="Chi tiết đơn thuốc lịch sử"
      class="detail-modal-box"
    >
      <div class="detail-header">
        <div>
          <h3 class="detail-title">Chi tiết đơn: {{ detail.prescriptionCode }}</h3>
          <p class="detail-doctor">Kê bởi: <strong>{{ detail.doctorName }}</strong> · {{ date(detail.createdAt) }}</p>
        </div>
        <button type="button" class="btn-close" :disabled="busy" aria-label="Đóng chi tiết" @click="closeDetail">✕</button>
      </div>

      <div class="detail-status-bar">
        <span
          class="status-pill"
          :class="detail.isCancelled ? 'pill-cancelled' : detail.dispensedAt ? 'pill-dispensed' : 'pill-pending'"
        >
          {{ detail.isCancelled ? 'ĐÃ HỦY' : detail.dispensedAt ? 'Đã giao thuốc' : 'Chưa giao thuốc' }}
        </span>
        <span class="diagnosis-text"><strong>Chẩn đoán:</strong> {{ detail.diagnosis }}</span>
      </div>

      <div class="medicine-table-wrap">
        <table class="medicine-table">
          <thead>
            <tr>
              <th scope="col">#</th>
              <th scope="col">Tên thuốc</th>
              <th scope="col">Số lượng</th>
              <th scope="col">Hướng dẫn sử dụng</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, index) in detail.items" :key="index">
              <td>{{ index + 1 }}</td>
              <td>
                <strong>{{ line.medicineName }}</strong>
                <span v-if="line.strength"> ({{ line.strength }})</span>
              </td>
              <td>{{ line.quantity }} {{ line.unit }}</td>
              <td>
                <p class="med-instruction">{{ line.usageInstructions }}</p>
                <small v-if="line.treatmentDays" class="med-days">Dùng {{ line.treatmentDays }} ngày</small>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="detail.doctorAdvice || detail.revisitDate" class="advice-box">
        <p v-if="detail.doctorAdvice"><strong>Lời dặn nha sĩ:</strong> {{ detail.doctorAdvice }}</p>
        <p v-if="detail.revisitDate"><strong>Hẹn tái khám:</strong> {{ detail.revisitDate }}</p>
      </div>

      <div class="detail-footer">
        <PrescriptionPrint
          v-if="detail.canPrint && !detail.isCancelled"
          :visit-id="detail.visitId"
          :prescription-id="detail.id"
          :prescription-version="detail.version"
          :blocked="busy"
        />
        <p v-else class="print-disabled-text">Đơn hoặc lần khám đã hủy, không được in lại.</p>
        <button type="button" class="btn-secondary" :disabled="busy" @click="closeDetail">Đóng chi tiết</button>
      </div>
    </section>
  </section>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import PrescriptionPrint from '@/components/PrescriptionPrint.vue';
import { patientPrescriptionHistoryService, MOD_RX_validHistoryRow, MOD_RX_validHistoryDetail,
 type PatientPrescriptionSummary, type PatientPrescriptionDetail } from '@/services/patientPrescriptionHistory';
import type { PagedResult } from '@/services/patients';
const props = defineProps<{patientId:number}>(), auth = useAuthStore();
const history = ref<PagedResult<PatientPrescriptionSummary> | null>(null), detail = ref<PatientPrescriptionDetail | null>(null);
const busy = ref(false), error = ref(''), heading = ref<HTMLElement | null>(null), detailHeading = ref<HTMLElement | null>(null);
let generation = 0, disposed = false;
const identity = () => [props.patientId,auth.user?.userId,auth.role].join('/');
const mayRead = computed(() => Number.isInteger(props.patientId) && props.patientId > 0 && props.patientId <= 2147483647 && ['ADMIN','RECEPTIONIST','ASSISTANT','DENTIST','PATIENT'].includes(auth.role));
const alive = (n:number, id:string) => !disposed && generation === n && identity() === id;
function date(s:string) { return new Date(s).toLocaleString('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'}); }
function clear() { history.value = null; detail.value = null; error.value = ''; }
async function load(page = 1) {
 if (disposed || busy.value || !mayRead.value || !Number.isInteger(page) || page < 1 || page > 1000000) return;
 const n = ++generation, id = identity(); clear(); busy.value = true;
 try {
  const data = await patientPrescriptionHistoryService.list(props.patientId,page); if (!alive(n,id)) return;
  if (!data || data.page !== page || data.pageSize !== 10 || !Number.isInteger(data.totalCount) || data.totalCount < 0 ||
   data.totalPages !== Math.ceil(data.totalCount / 10) || data.hasNextPage !== (page < data.totalPages) || data.hasPreviousPage !== (page > 1) ||
   !Array.isArray(data.items) || data.items.length > 10 || data.items.some(r => !MOD_RX_validHistoryRow(r,props.patientId))) throw Error('Invalid history');
  history.value = data; await nextTick(); if (alive(n,id) && page > 1) heading.value?.focus();
 } catch { if (alive(n,id)) error.value = 'Không tải được lịch sử đơn hoặc bạn không có quyền xem hồ sơ này.'; }
 finally { if (alive(n,id)) busy.value = false; }
}
async function showDetail(row:PatientPrescriptionSummary) {
 if (disposed || busy.value || !mayRead.value || !MOD_RX_validHistoryRow(row,props.patientId)) return;
 const n = ++generation, id = identity(); detail.value = null; error.value = ''; busy.value = true;
 try {
  const data = await patientPrescriptionHistoryService.detail(props.patientId,row.id); if (!alive(n,id)) return;
  if (!MOD_RX_validHistoryDetail(data,props.patientId,row.id) || data.visitId !== row.visitId) throw Error('Invalid detail');
  detail.value = data; await nextTick(); if (alive(n,id)) detailHeading.value?.focus();
 } catch { if (alive(n,id)) error.value = 'Không mở được đơn: dữ liệu hoặc quyền đọc có thể đã thay đổi. Tải lại danh sách để đối chiếu.'; }
 finally { if (alive(n,id)) busy.value = false; }
}
function closeDetail() { if (!busy.value) { generation++; detail.value = null; heading.value?.focus(); } }
watch(identity, () => { generation++; busy.value = false; clear(); void load(1); }, {immediate:true});
onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>
.rx-history {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.rx-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #e2e8f0;
}

.rx-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  color: #0f2d59;
}

.rx-subtitle {
  margin: 0.2rem 0 0;
  font-size: 0.875rem;
  color: #64748b;
}

.btn-refresh {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.45rem 0.85rem;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #f8fafc;
  color: #334155;
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}

.btn-refresh:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.btn-refresh:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.refresh-icon.spin {
  display: inline-block;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  100% { transform: rotate(360deg); }
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 6px;
  font-size: 0.875rem;
  margin: 0;
}

.alert-error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #b91c1c;
}

.empty-state {
  padding: 2.5rem 1rem;
  text-align: center;
  color: #64748b;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 8px;
  font-size: 0.95rem;
}

.rx-card-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.rx-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 1.25rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  transition: all 0.15s ease;
}

.rx-card:hover {
  border-color: #0284c7;
  box-shadow: 0 4px 8px -2px rgba(2, 132, 199, 0.12);
}

.card-main {
  flex: 1;
  min-width: 260px;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.rx-badge-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.rx-code-badge {
  font-weight: 700;
  font-size: 1rem;
  color: #0f2d59;
  letter-spacing: 0.03em;
}

.status-pill {
  display: inline-block;
  padding: 0.2rem 0.55rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.pill-pending {
  background: #fef3c7;
  color: #b45309;
}

.pill-dispensed {
  background: #dcfce7;
  color: #15803d;
}

.pill-cancelled {
  background: #fee2e2;
  color: #b91c1c;
}

.rx-meta-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.5rem 1rem;
  font-size: 0.85rem;
}

.meta-item {
  display: flex;
  gap: 0.35rem;
}

.meta-item.full-width {
  grid-column: 1 / -1;
}

.meta-label {
  color: #64748b;
}

.meta-val {
  color: #1e293b;
}

.font-semibold {
  font-weight: 600;
}

.btn-view-detail {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.55rem 1rem;
  border-radius: 6px;
  background: #f0f9ff;
  color: #0369a1;
  border: 1px solid #bae6fd;
  font-weight: 600;
  font-size: 0.875rem;
  cursor: pointer;
  transition: all 0.15s ease;
}

.btn-view-detail:hover:not(:disabled) {
  background: #0284c7;
  color: #ffffff;
  border-color: #0284c7;
}

.pagination-bar {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 0;
  flex-wrap: wrap;
}

.btn-page {
  padding: 0.4rem 0.85rem;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #334155;
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
}

.btn-page:hover:not(:disabled) {
  background: #f1f5f9;
}

.btn-page:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.page-indicator {
  font-size: 0.875rem;
  color: #475569;
}

.detail-modal-box {
  background: #ffffff;
  border: 2px solid #0284c7;
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 10px 25px -5px rgba(2, 132, 199, 0.2);
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.detail-title {
  margin: 0;
  font-size: 1.15rem;
  color: #0f2d59;
}

.detail-doctor {
  margin: 0.2rem 0 0;
  font-size: 0.85rem;
  color: #64748b;
}

.btn-close {
  background: #f1f5f9;
  border: none;
  border-radius: 6px;
  padding: 0.35rem 0.65rem;
  font-size: 0.95rem;
  cursor: pointer;
}

.btn-close:hover {
  background: #e2e8f0;
}

.detail-status-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: #f8fafc;
  border-radius: 8px;
  font-size: 0.875rem;
}

.medicine-table-wrap {
  overflow-x: auto;
}

.medicine-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.medicine-table th,
.medicine-table td {
  padding: 0.65rem 0.75rem;
  border-bottom: 1px solid #e2e8f0;
  text-align: left;
}

.medicine-table th {
  background: #f8fafc;
  color: #475569;
  font-weight: 600;
}

.med-instruction {
  margin: 0;
  color: #1e293b;
}

.advice-box {
  padding: 0.75rem 1rem;
  background: #fefce8;
  border: 1px solid #fef08a;
  border-radius: 8px;
  font-size: 0.875rem;
  color: #713f12;
}

.detail-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid #e2e8f0;
}

.btn-secondary {
  padding: 0.5rem 1.25rem;
  background: #e2e8f0;
  color: #1e293b;
  border: none;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
}

.btn-secondary:hover {
  background: #cbd5e1;
}

.print-disabled-text {
  font-size: 0.85rem;
  color: #94a3b8;
  font-style: italic;
  margin: 0;
}
</style>
