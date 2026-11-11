<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">Hồ sơ sức khỏe</p>
        <h1>Hồ sơ điều trị của tôi</h1>
        <p class="subtitle">Thông tin bệnh nhân và danh sách người được giám hộ tại Nha khoa Hoàng Gia</p>
      </div>
      <div class="header-actions">
        <button type="button" class="button button-secondary" :disabled="loading" @click="load">
          <span :class="{ 'spin-icon': loading }">🔄</span>
          <span>Tải lại hồ sơ</span>
        </button>
      </div>
    </div>

    <!-- Trạng thái Loading / Error / Empty -->
    <div v-if="loading" class="panel status-panel" role="status">
      <div class="loading-spinner"></div>
      <p>Đang tải thông tin hồ sơ cá nhân…</p>
    </div>

    <div v-else-if="error" class="panel alert-panel alert-error" role="alert">
      <span class="alert-icon">⚠️</span>
      <div>
        <strong>Không thể tải hồ sơ</strong>
        <p>{{ error }}</p>
      </div>
    </div>

    <div v-else-if="!patient" class="panel alert-panel alert-info">
      <span class="alert-icon">ℹ️</span>
      <div>
        <strong>Chưa có hồ sơ đang hoạt động</strong>
        <p>Vui lòng liên hệ Lễ tân tại phòng khám để đối soát thông tin và kích hoạt hồ sơ cá nhân của bạn.</p>
      </div>
    </div>

    <!-- Khối thông tin hồ sơ bệnh nhân chính -->
    <div v-else class="content-wrapper">
      <div class="panel patient-card">
        <div class="patient-card-header">
          <div class="patient-avatar">
            <span>{{ patient.fullName.charAt(0).toUpperCase() }}</span>
          </div>
          <div class="patient-main-info">
            <div class="title-row">
              <h2 class="patient-name">{{ patient.fullName }}</h2>
              <span class="status-pill status-pill--active">Đang hoạt động</span>
            </div>
            <div class="patient-badges">
              <span class="code-badge">Mã BN: {{ patient.patientCode }}</span>
              <span v-if="patient.phone" class="meta-badge">📞 {{ patient.phone }}</span>
              <span v-if="patient.gender" class="meta-badge">Giới tính: {{ genderLabel(patient.gender) }}</span>
            </div>
          </div>
        </div>

        <div class="patient-card-actions">
          <RouterLink
            class="button button-primary"
            :to="{ name: 'patient-portal-patient-detail', params: { id: patient.patientId } }"
          >
            <span>Xem lịch sử khám, đơn thuốc & hình ảnh</span>
            <span aria-hidden="true">→</span>
          </RouterLink>
          <RouterLink class="button button-outline" to="/patient/appointments">
            <span>📅 Đặt lịch khám mới</span>
          </RouterLink>
        </div>

        <!-- Danh sách người được giám hộ (DL-353, DL-358) -->
        <PatientWardsPanel />
      </div>

      <!-- Tóm tắt sức khỏe: mỗi khối tải độc lập, một khối lỗi không làm hỏng cả trang -->
      <section class="health-summary" aria-label="Tóm tắt sức khỏe">
        <div class="summary-head">
          <h2>Tóm tắt sức khỏe</h2>
          <p v-if="summaryLoading" class="muted" role="status">Đang tải dữ liệu…</p>
        </div>
        <div class="summary-grid">
          <article class="panel summary-card">
            <header>
              <h3>Tiền sử &amp; dị ứng</h3>
              <RouterLink :to="tabLink('history')">Xem tất cả</RouterLink>
            </header>
            <p v-if="failed.history" class="muted" role="alert">Không tải được tiền sử &amp; dị ứng.</p>
            <template v-else-if="history && history.items.length">
              <ul class="chip-list">
                <li v-for="item in history.items" :key="item.itemId" :class="{ 'chip-critical': item.isCritical }">
                  <span class="chip-kind">{{ item.type === 'Allergy' ? 'Dị ứng' : 'Bệnh nền' }}</span>
                  <strong>{{ item.name }}</strong>
                  <span v-if="item.isCritical" class="chip-flag">Cần lưu ý</span>
                </li>
              </ul>
              <p class="muted small">Ghi nhận {{ formatDate(history.createdAt) }}</p>
            </template>
            <p v-else-if="!summaryLoading" class="muted">Chưa có tiền sử bệnh hoặc dị ứng được ghi nhận.</p>
          </article>

          <article class="panel summary-card">
            <header>
              <h3>Sinh hiệu gần nhất</h3>
              <RouterLink :to="tabLink('vitals')">Xem tất cả</RouterLink>
            </header>
            <p v-if="failed.vital" class="muted" role="alert">Không tải được sinh hiệu.</p>
            <template v-else-if="vital">
              <dl class="vital-tiles">
                <div><dt>Huyết áp</dt><dd>{{ vital.systolicBp ?? '—' }}/{{ vital.diastolicBp ?? '—' }}<small> mmHg</small></dd></div>
                <div><dt>Mạch</dt><dd>{{ vital.pulseBpm ?? '—' }}<small> lần/phút</small></dd></div>
                <div><dt>Nhiệt độ</dt><dd>{{ vital.temperatureC ?? '—' }}<small> °C</small></dd></div>
              </dl>
              <p class="muted small">Đo {{ formatDate(vital.createdAt) }}</p>
            </template>
            <p v-else-if="!summaryLoading" class="muted">Chưa có sinh hiệu được ghi nhận.</p>
          </article>

          <article class="panel summary-card">
            <header>
              <h3>Lần khám gần đây</h3>
              <RouterLink :to="tabLink('timeline')">Xem tất cả</RouterLink>
            </header>
            <p v-if="failed.visits" class="muted" role="alert">Không tải được lịch sử khám.</p>
            <ul v-else-if="visits.length" class="row-list">
              <li v-for="visit in visits" :key="visit.visitId">
                <div>
                  <strong>{{ visit.startedAt ? formatDate(visit.startedAt) : 'Chưa bắt đầu' }}</strong>
                  <span class="muted small">Nha sĩ: {{ visit.dentistName || 'Chưa phân công' }}</span>
                  <span v-if="visit.diagnosis" class="muted small">Chẩn đoán: {{ visit.diagnosis }}</span>
                </div>
                <span class="tag">{{ visitStatusLabel(visit.status) }}</span>
              </li>
            </ul>
            <p v-else-if="!summaryLoading" class="muted">Chưa có lần khám nào.</p>
          </article>

          <article class="panel summary-card">
            <header>
              <h3>Đơn thuốc gần đây</h3>
              <RouterLink :to="tabLink('prescriptions')">Xem tất cả</RouterLink>
            </header>
            <p v-if="failed.prescriptions" class="muted" role="alert">Không tải được đơn thuốc.</p>
            <ul v-else-if="prescriptions.length" class="row-list">
              <li v-for="rx in prescriptions" :key="rx.id">
                <div>
                  <strong>{{ rx.prescriptionCode }}</strong>
                  <span class="muted small">{{ formatDate(rx.createdAt) }} · {{ rx.medicineCount }} loại thuốc</span>
                </div>
                <span class="tag" :class="{ 'tag-danger': rx.isCancelled, 'tag-ok': !rx.isCancelled && rx.dispensedAt }">
                  {{ rx.isCancelled ? 'Đã hủy' : rx.dispensedAt ? 'Đã giao thuốc' : 'Chưa giao thuốc' }}
                </span>
              </li>
            </ul>
            <p v-else-if="!summaryLoading" class="muted">Chưa có đơn thuốc nào.</p>
          </article>

          <article class="panel summary-card summary-card--wide">
            <header>
              <h3>Ảnh hồ sơ</h3>
              <RouterLink :to="tabLink('images')">Xem tất cả</RouterLink>
            </header>
            <p v-if="failed.images" class="muted" role="alert">Không tải được ảnh hồ sơ.</p>
            <template v-else-if="images.length">
              <div class="thumb-row">
                <DentalImageThumbnail v-for="image in images" :key="image.id" :image="image" @open="openImage" />
              </div>
              <p class="muted small">Hiển thị {{ images.length }} ảnh mới nhất{{ imagesHasMore ? ' — còn thêm ảnh khác' : '' }}.</p>
            </template>
            <p v-else-if="!summaryLoading" class="muted">Chưa có ảnh hồ sơ nào.</p>
          </article>
        </div>
      </section>

      <!-- Quick Guidance Box -->
      <aside class="panel guide-card" aria-label="Lưu ý bệnh nhân">
        <div class="guide-header">
          <span class="guide-icon">🛡️</span>
          <div>
            <h3>An toàn trong điều trị</h3>
            <p>Quy trình khám chuyên nghiệp tại Hoàng Gia</p>
          </div>
        </div>
        <ul class="guide-list">
          <li><strong>Hồ sơ sơ đồ răng FDI:</strong> Toàn bộ chẩn đoán bệnh lý răng và hình ảnh X-quang được lưu vết trọn đời trên hệ thống.</li>
          <li><strong>Cảnh báo an toàn:</strong> Tiền sử bệnh nền (tim mạch, dị ứng thuốc tê) và huyết áp được kiểm tra nghiêm ngặt trước thủ thuật.</li>
          <li><strong>Minh bạch chi phí:</strong> Mọi khoản thanh toán đều có hóa đơn điện tử và theo dõi đợt trả rõ ràng.</li>
        </ul>
      </aside>
    </div>
  </main>
</template>
<script setup lang="ts">
import { onUnmounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import {
  patientsService,
  type MedicalHistoryRecord,
  type PatientSummary,
  type PatientTimelineItem,
  type VitalSignRecord
} from '@/services/patients';
import { patientPrescriptionHistoryService, type PatientPrescriptionSummary } from '@/services/patientPrescriptionHistory';
import { dentalImagesService, type DentalImage } from '@/services/dentalImages';
import { useAuthStore } from '@/stores/auth';
import PatientWardsPanel from '@/components/PatientWardsPanel.vue';
import DentalImageThumbnail from '@/components/DentalImageThumbnail.vue';

type SummaryKey = 'history' | 'vital' | 'visits' | 'prescriptions' | 'images';
type DetailTab = 'timeline' | 'history' | 'vitals' | 'prescriptions' | 'images';

const auth = useAuthStore();
const router = useRouter();
const patient = ref<PatientSummary | null>(null), loading = ref(false), error = ref('');
const history = ref<MedicalHistoryRecord | null>(null);
const vital = ref<VitalSignRecord | null>(null);
const visits = ref<PatientTimelineItem[]>([]);
const prescriptions = ref<PatientPrescriptionSummary[]>([]);
const images = ref<DentalImage[]>([]);
const imagesHasMore = ref(false);
const summaryLoading = ref(false);
const failed = reactive<Record<SummaryKey, boolean>>({ history: false, vital: false, visits: false, prescriptions: false, images: false });
let revision = 0;

function tabLink(tab: DetailTab) {
  return { name: 'patient-portal-patient-detail', params: { id: patient.value?.patientId ?? 0 }, query: { tab } };
}

function openImage() {
  void router.push(tabLink('images'));
}

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
}

function visitStatusLabel(status: string) {
  const labels: Record<string, string> = { Created: 'Đã tạo', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Correcting: 'Đang đính chính' };
  return labels[status] ?? status;
}

function resetSummary() {
  history.value = null; vital.value = null; visits.value = []; prescriptions.value = []; images.value = []; imagesHasMore.value = false;
  failed.history = failed.vital = failed.visits = failed.prescriptions = failed.images = false;
}

function notFound(reason: unknown) {
  return (reason as { response?: { status?: number } } | null)?.response?.status === 404;
}

async function loadSummary(patientId: number, current: number) {
  resetSummary();
  summaryLoading.value = true;
  const [h, v, t, p, i] = await Promise.allSettled([
    patientsService.getLatestMedicalHistory(patientId),
    patientsService.getVitalSigns(patientId, 1, 1),
    patientsService.getTimeline(patientId, 1, 3),
    patientPrescriptionHistoryService.list(patientId, 1),
    dentalImagesService.list(patientId, 0)
  ]);
  if (current !== revision) return;
  if (h.status === 'fulfilled') history.value = h.value ?? null; else failed.history = !notFound(h.reason);
  if (v.status === 'fulfilled') vital.value = Array.isArray(v.value?.items) ? v.value.items[0] ?? null : null; else failed.vital = true;
  if (t.status === 'fulfilled') visits.value = Array.isArray(t.value?.items) ? t.value.items.slice(0, 3) : []; else failed.visits = true;
  if (p.status === 'fulfilled') prescriptions.value = Array.isArray(p.value?.items) ? p.value.items.slice(0, 3) : []; else failed.prescriptions = true;
  if (i.status === 'fulfilled' && Array.isArray(i.value?.items)) {
    images.value = i.value.items.slice(0, 4);
    imagesHasMore.value = i.value.hasMore || i.value.items.length > 4;
  } else failed.images = true;
  summaryLoading.value = false;
}

function genderLabel(gender: string) {
  if (gender === 'Male') return 'Nam';
  if (gender === 'Female') return 'Nữ';
  return 'Khác';
}

async function load() {
  const current = ++revision;
  patient.value = null; error.value = ''; resetSummary(); summaryLoading.value = false;
  if (auth.role !== 'PATIENT' || !auth.user?.userId) { loading.value = false; return; }
  loading.value = true;
  try {
    const result = await patientsService.getMyPatient();
    if (current !== revision) return;
    if (result !== null && (!Number.isSafeInteger(result.patientId) || result.patientId <= 0 || typeof result.fullName !== 'string'))
      throw Error('Hồ sơ cá nhân không hợp lệ.');
    patient.value = result;
    if (result !== null) void loadSummary(result.patientId, current);
  }
  catch { if (current === revision) error.value = 'Chưa tìm thấy hồ sơ cá nhân đang hoạt động. Vui lòng liên hệ Lễ tân.'; }
  finally { if (current === revision) loading.value = false; }
}
watch(() => `${auth.user?.userId}:${auth.role}`, load, { immediate: true });
onUnmounted(() => { revision++; patient.value = null; error.value = ''; });
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
  padding: 1.5rem;
}

.content-wrapper {
  display: grid;
  gap: 1.5rem;
}

/* Patient Card */
.patient-card-header {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  margin-bottom: 1.5rem;
}

.patient-avatar {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: #0284c7;
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 850;
  font-size: 1.5rem;
  box-shadow: 0 2px 8px rgba(2, 132, 199, 0.25);
  flex-shrink: 0;
}

.patient-main-info {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-width: 0;
}

.title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.patient-name {
  margin: 0;
  font-size: 1.35rem;
  font-weight: 850;
  color: #0f2d59;
}

.status-pill {
  font-size: 0.75rem;
  font-weight: 750;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
}

.status-pill--active {
  background: #dcfce7;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.patient-badges {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.78rem;
  font-weight: 750;
  background: #f1f5f9;
  color: #0f2d59;
  padding: 0.15rem 0.5rem;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
}

.meta-badge {
  font-size: 0.8rem;
  color: #475569;
  background: #f8fafc;
  padding: 0.15rem 0.5rem;
  border-radius: 6px;
  border: 1px solid #e2e8f0;
}

.patient-card-actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

/* Guide Card */
.guide-card {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
}

.guide-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.85rem;
}

.guide-icon {
  font-size: 1.35rem;
}

.guide-header h3 {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 800;
  color: #0f2d59;
}

.guide-header p {
  margin: 0.1rem 0 0;
  font-size: 0.785rem;
  color: #64748b;
}

.guide-list {
  margin: 0;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  font-size: 0.835rem;
  color: #475569;
  line-height: 1.45;
}

.guide-list strong {
  color: #0f2d59;
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

.button-outline {
  background: #ffffff;
  color: #0f2d59;
  border-color: #cbd5e1;
}

.button-outline:hover:not(:disabled) {
  background: #f8fafc;
  border-color: #0f2d59;
}

.spin-icon {
  animation: spin 1s linear infinite;
  display: inline-block;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Status & Alerts */
.status-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 3rem 1rem;
  color: #64748b;
}

.loading-spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e2e8f0;
  border-top-color: #0284c7;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.alert-panel {
  display: flex;
  align-items: flex-start;
  gap: 0.85rem;
  line-height: 1.45;
}

.alert-icon {
  font-size: 1.35rem;
  flex-shrink: 0;
}

.alert-error {
  background: #fef2f2;
  border-color: #fecaca;
  color: #991b1b;
}

.alert-info {
  background: #f0f9ff;
  border-color: #bae6fd;
  color: #0369a1;
}

/* Health summary */
.health-summary {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.summary-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.summary-head h2 {
  margin: 0;
  font-size: 1.2rem;
  font-weight: 800;
  color: #0f2d59;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.summary-card {
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  min-width: 0;
}

.summary-card--wide {
  grid-column: 1 / -1;
}

.summary-card header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.summary-card h3 {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 800;
  color: #0f2d59;
}

.summary-card header a {
  font-size: 0.8rem;
  font-weight: 700;
  color: #0284c7;
  text-decoration: none;
  white-space: nowrap;
}

.summary-card header a:hover {
  text-decoration: underline;
}

.muted {
  margin: 0;
  color: #64748b;
  font-size: 0.85rem;
  line-height: 1.45;
}

.small {
  font-size: 0.78rem;
}

.chip-list,
.row-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.chip-list li {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.4rem;
  padding: 0.45rem 0.65rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #f8fafc;
  overflow-wrap: anywhere;
}

.chip-list li.chip-critical {
  border-color: #fecaca;
  background: #fef2f2;
}

.chip-kind {
  font-size: 0.75rem;
  color: #64748b;
}

.chip-flag {
  margin-left: auto;
  padding: 0.1rem 0.5rem;
  border-radius: 99px;
  background: #fee2e2;
  color: #991b1b;
  font-size: 0.72rem;
  font-weight: 700;
}

.vital-tiles {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.5rem;
  margin: 0;
}

.vital-tiles > div {
  padding: 0.6rem 0.5rem;
  border-radius: 8px;
  background: #f0f9ff;
  text-align: center;
}

.vital-tiles dt {
  font-size: 0.72rem;
  color: #64748b;
}

.vital-tiles dd {
  margin: 0.15rem 0 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.vital-tiles dd small {
  display: block;
  font-size: 0.68rem;
  font-weight: 600;
  color: #64748b;
}

.row-list li {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.6rem;
  padding: 0.55rem 0.7rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
}

.row-list li > div {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
  overflow-wrap: anywhere;
}

.tag {
  flex-shrink: 0;
  padding: 0.15rem 0.55rem;
  border-radius: 99px;
  background: #e2e8f0;
  color: #334155;
  font-size: 0.72rem;
  font-weight: 700;
}

.tag-ok {
  background: #dcfce7;
  color: #166534;
}

.tag-danger {
  background: #fee2e2;
  color: #991b1b;
}

.thumb-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 0.6rem;
}

.thumb-row :deep(.image-thumbnail button) {
  width: 100%;
  height: auto;
  aspect-ratio: 1 / 1;
}

@media (max-width: 720px) {
  .summary-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>

