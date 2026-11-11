<template>
  <main class="pd-page">
    <header class="pd-hero">
      <div>
        <span class="pd-badge">Cổng Bệnh Nhân</span>
        <h1>Xin chào, {{ auth.user?.fullName || 'Bệnh nhân' }}!</h1>
        <p>Tổng quan lịch khám, hóa đơn và lịch sử điều trị của bạn tại Nha khoa Hoàng Gia.</p>
      </div>
      <button type="button" class="pd-btn pd-btn--light" :disabled="loading" @click="load">
        {{ loading ? 'Đang tải…' : 'Tải lại' }}
      </button>
    </header>

    <div v-if="loading && !loaded" class="pd-panel pd-state" role="status">Đang tải dữ liệu cá nhân…</div>

    <template v-else>
      <div v-if="error" class="pd-panel pd-alert" role="alert">{{ error }}</div>

      <section class="pd-kpis" aria-label="Chỉ số nhanh">
        <article class="pd-kpi">
          <span class="pd-kpi-label">Lịch hẹn sắp tới</span>
          <strong class="pd-kpi-val">{{ upcoming.length }}</strong>
          <span class="pd-kpi-note">{{ nextAppointmentText }}</span>
        </article>
        <article class="pd-kpi" :class="{ 'pd-kpi--warn': totalDebt > 0 }">
          <span class="pd-kpi-label">Dư nợ cần thanh toán</span>
          <strong class="pd-kpi-val">{{ formatCurrency(totalDebt) }}</strong>
          <span class="pd-kpi-note">{{ unpaid.length }} hóa đơn chưa hoàn tất</span>
        </article>
        <article class="pd-kpi">
          <span class="pd-kpi-label">Lần khám gần đây</span>
          <strong class="pd-kpi-val">{{ visits.length }}</strong>
          <span class="pd-kpi-note">{{ lastVisitText }}</span>
        </article>
      </section>

      <section class="pd-panel" aria-label="Lối tắt thao tác nhanh">
        <h2>Thao tác nhanh</h2>
        <div class="pd-shortcuts-grid">
          <RouterLink class="pd-quick-card pd-quick-card--primary" to="/patient/appointments">
            <span class="pd-quick-icon">📅</span>
            <div>
              <strong>Đặt lịch khám mới</strong>
              <span>Chọn nha sĩ và khung giờ trực tuyến</span>
            </div>
          </RouterLink>

          <RouterLink class="pd-quick-card" to="/patient/my-record">
            <span class="pd-quick-icon">📋</span>
            <div>
              <strong>Hồ sơ điều trị</strong>
              <span>Tiền sử, sinh hiệu và quá trình khám</span>
            </div>
          </RouterLink>

          <RouterLink v-if="patient" class="pd-quick-card" :to="{ name: 'patient-portal-patient-detail', params: { id: patient.patientId } }">
            <span class="pd-quick-icon">🦷</span>
            <div>
              <strong>Sơ đồ răng &amp; Đơn thuốc</strong>
              <span>Sơ đồ FDI, đơn thuốc và phim X-quang</span>
            </div>
          </RouterLink>

          <RouterLink class="pd-quick-card" to="/patient/invoices/mine">
            <span class="pd-quick-icon">💳</span>
            <div>
              <strong>Hóa đơn &amp; Chi phí</strong>
              <span>Theo dõi công nợ, đợt thu và biên lai</span>
            </div>
          </RouterLink>

          <RouterLink class="pd-quick-card" to="/patient/profile">
            <span class="pd-quick-icon">👤</span>
            <div>
              <strong>Tài khoản &amp; Bảo mật</strong>
              <span>Đổi mật khẩu, cập nhật thông tin</span>
            </div>
          </RouterLink>
        </div>
      </section>

      <div class="pd-grid">
        <section class="pd-panel" aria-label="Lịch hẹn sắp tới">
          <div class="pd-panel-head">
            <h2>Lịch hẹn sắp tới</h2>
            <RouterLink to="/patient/appointments/mine">Xem tất cả</RouterLink>
          </div>
          <p v-if="!upcoming.length" class="pd-empty">Bạn chưa có lịch hẹn nào sắp tới.</p>
          <ul v-else class="pd-list">
            <li v-for="a in upcoming" :key="a.appointmentId">
              <div>
                <strong>{{ formatDate(a.appointmentDate) }} · {{ a.slotTime.slice(0, 5) }}</strong>
                <span>{{ a.patientName }}<template v-if="a.dentistName"> · Nha sĩ {{ a.dentistName }}</template></span>
              </div>
              <span class="pd-pill pd-pill--info">Đã đặt lịch</span>
            </li>
          </ul>
        </section>

        <section class="pd-panel" aria-label="Hóa đơn chưa thanh toán">
          <div class="pd-panel-head">
            <h2>Hóa đơn chưa thanh toán</h2>
            <RouterLink to="/patient/invoices/mine">Xem tất cả</RouterLink>
          </div>
          <p v-if="!unpaid.length" class="pd-empty">Bạn không có khoản dư nợ nào.</p>
          <ul v-else class="pd-list">
            <li v-for="inv in unpaid" :key="inv.invoiceId">
              <div>
                <strong>{{ inv.invoiceCode }}</strong>
                <span>Đã thu {{ formatCurrency(inv.paidAmount) }} / {{ formatCurrency(inv.totalAmount) }}</span>
              </div>
              <span class="pd-pill pd-pill--warn">Còn {{ formatCurrency(inv.remainingAmount) }}</span>
            </li>
          </ul>
        </section>
      </div>

      <section class="pd-panel" aria-label="Lịch sử khám">
        <div class="pd-panel-head">
          <h2>Lịch sử khám gần đây</h2>
          <RouterLink v-if="patient" :to="{ name: 'patient-portal-patient-detail', params: { id: patient.patientId } }">Chi tiết hồ sơ</RouterLink>
        </div>
        <p v-if="!visits.length" class="pd-empty">Chưa có lần khám nào được ghi nhận.</p>
        <ul v-else class="pd-list">
          <li v-for="v in visits" :key="v.visitId">
            <div>
              <strong>Lần khám #{{ v.visitId }}</strong>
              <span>{{ v.startedAt ? formatDateTime(v.startedAt) : 'Chưa bắt đầu' }}<template v-if="v.dentistName"> · Nha sĩ {{ v.dentistName }}</template></span>
            </div>
            <span class="pd-pill" :class="v.status === 'Completed' ? 'pd-pill--ok' : 'pd-pill--info'">{{ visitStatusLabel(v.status) }}</span>
          </li>
        </ul>
      </section>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { invoicePortalApi } from '@/services/invoicePortal';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
import { patientsService, type PatientSummary, type PatientTimelineItem } from '@/services/patients';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();
const patient = ref<PatientSummary | null>(null);
const upcoming = ref<AppointmentResponse[]>([]);
const unpaid = ref<InvoiceLookupResponse[]>([]);
const visits = ref<PatientTimelineItem[]>([]);
const loading = ref(false), loaded = ref(false), error = ref('');
let revision = 0;

const totalDebt = computed(() => unpaid.value.reduce((sum, inv) => sum + (inv.remainingAmount || 0), 0));
const nextAppointmentText = computed(() => {
  const first = upcoming.value[0];
  return first ? `Gần nhất: ${formatDate(first.appointmentDate)} ${first.slotTime.slice(0, 5)}` : 'Chưa có lịch hẹn';
});
const lastVisitText = computed(() => {
  const first = visits.value.find(v => v.startedAt);
  return first?.startedAt ? `Gần nhất: ${formatDateTime(first.startedAt)}` : 'Chưa có dữ liệu';
});

function formatCurrency(value: number): string {
  return `${new Intl.NumberFormat('vi-VN').format(Math.round(value || 0))} ₫`;
}
function formatDate(value: string): string {
  const d = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(d.getTime()) ? value : new Intl.DateTimeFormat('vi-VN').format(d);
}
function formatDateTime(value: string): string {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(d);
}
function visitStatusLabel(status: string): string {
  const labels: Record<string, string> = { Created: 'Đã tạo', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Correcting: 'Đang đính chính' };
  return labels[status] ?? status;
}

async function load() {
  const current = ++revision;
  error.value = '';
  if (auth.role !== 'PATIENT' || !auth.user?.userId) { loading.value = false; return; }
  loading.value = true;
  const failures: string[] = [];
  try {
    const [apptRes, patientRes] = await Promise.allSettled([
      appointmentsService.getMine(1, 'Scheduled'),
      patientsService.getMyPatient()
    ]);
    if (current !== revision) return;

    if (apptRes.status === 'fulfilled') {
      upcoming.value = [...apptRes.value.items]
        .sort((a, b) => `${a.appointmentDate}${a.slotTime}`.localeCompare(`${b.appointmentDate}${b.slotTime}`))
        .slice(0, 5);
    } else { upcoming.value = []; failures.push('lịch hẹn'); }

    if (patientRes.status === 'fulfilled' && patientRes.value && Number.isSafeInteger(patientRes.value.patientId) && patientRes.value.patientId > 0) {
      patient.value = patientRes.value;
      const pid = patientRes.value.patientId;
      const [pending, partial, timeline] = await Promise.allSettled([
        invoicePortalApi.list(pid, 1, { status: 1 }),
        invoicePortalApi.list(pid, 1, { status: 2 }),
        patientsService.getTimeline(pid, 1, 5)
      ]);
      if (current !== revision) return;
      if (pending.status === 'fulfilled' && partial.status === 'fulfilled') {
        unpaid.value = [...pending.value.items, ...partial.value.items].filter(inv => inv.remainingAmount > 0);
      } else { unpaid.value = []; failures.push('hóa đơn'); }
      if (timeline.status === 'fulfilled') visits.value = timeline.value.items;
      else { visits.value = []; failures.push('lịch sử khám'); }
    } else {
      patient.value = null; unpaid.value = []; visits.value = [];
      failures.push('hồ sơ cá nhân');
    }
    if (failures.length) error.value = `Không tải được: ${failures.join(', ')}. Vui lòng thử lại hoặc liên hệ Lễ tân.`;
  } finally {
    if (current === revision) { loading.value = false; loaded.value = true; }
  }
}

watch(() => `${auth.user?.userId}:${auth.role}`, load, { immediate: true });
onUnmounted(() => { revision++; });
</script>

<style scoped>
.pd-page { max-width: 1040px; margin: 0 auto; padding: 1.5rem 1rem 3rem; display: flex; flex-direction: column; gap: 1.25rem; color: #1e293b; }
.pd-hero { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; padding: 1.5rem; border-radius: 14px; color: #fff; background: linear-gradient(135deg, #0f2d59, #0284c7); }
.pd-hero h1 { margin: 0.5rem 0 0.25rem; font-size: 1.5rem; overflow-wrap: anywhere; }
.pd-hero p { margin: 0; opacity: 0.92; font-size: 0.92rem; }
.pd-badge { display: inline-block; padding: 0.2rem 0.7rem; border-radius: 999px; font-size: 0.75rem; font-weight: 700; background: rgba(255, 255, 255, 0.18); border: 1px solid rgba(255, 255, 255, 0.4); }
.pd-panel { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.15rem 1.25rem; box-shadow: 0 2px 6px rgba(15, 45, 89, 0.04); }
.pd-panel h2 { margin: 0 0 0.75rem; font-size: 1.05rem; color: #0f2d59; }
.pd-panel-head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.5rem; }
.pd-panel-head h2 { margin: 0; }
.pd-panel-head a { font-size: 0.85rem; color: #0284c7; font-weight: 600; text-decoration: none; }
.pd-state { text-align: center; color: #64748b; }
.pd-alert { background: #fef2f2; border-color: #fecaca; color: #991b1b; }
.pd-kpis { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; }
.pd-kpi { display: flex; flex-direction: column; gap: 0.25rem; padding: 1rem 1.15rem; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; border-left: 4px solid #0284c7; }
.pd-kpi--warn { border-left-color: #f59e0b; background: #fffbeb; }
.pd-kpi-label { font-size: 0.8rem; font-weight: 600; color: #64748b; }
.pd-kpi-val { font-size: 1.5rem; color: #0f2d59; overflow-wrap: anywhere; }
.pd-kpi-note { font-size: 0.78rem; color: #64748b; }
.pd-shortcuts-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 0.85rem;
}

.pd-quick-card {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  padding: 0.9rem 1rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  text-decoration: none;
  color: #1e293b;
  transition: all 0.15s ease-in-out;
}

.pd-quick-card:hover {
  background: #f0f7ff;
  border-color: #bae6fd;
  transform: translateY(-1px);
}

.pd-quick-card--primary {
  background: #f0f9ff;
  border-color: #bae6fd;
}

.pd-quick-card--primary:hover {
  background: #e0f2fe;
  border-color: #7dd3fc;
}

.pd-quick-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.pd-quick-card strong {
  display: block;
  font-size: 0.92rem;
  color: #0f2d59;
  margin-bottom: 0.15rem;
}

.pd-quick-card span {
  display: block;
  font-size: 0.78rem;
  color: #64748b;
  line-height: 1.35;
}

.pd-shortcuts { display: flex; flex-wrap: wrap; gap: 0.6rem; }
.pd-btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.5rem 0.95rem; border-radius: 8px; border: 1px solid #cbd5e1; background: #f8fafc; color: #1e293b; font-size: 0.875rem; font-weight: 700; text-decoration: none; cursor: pointer; }
.pd-btn:hover { background: #f1f5f9; }
.pd-btn--primary { background: #0284c7; border-color: #0369a1; color: #fff; }
.pd-btn--primary:hover { background: #0369a1; }
.pd-btn--light { background: rgba(255, 255, 255, 0.15); border-color: rgba(255, 255, 255, 0.5); color: #fff; }
.pd-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.pd-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.pd-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.5rem; }
.pd-list li { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; padding: 0.65rem 0.8rem; border: 1px solid #f1f5f9; border-radius: 8px; background: #fbfcfe; }
.pd-list li > div { display: flex; flex-direction: column; min-width: 0; }
.pd-list strong { font-size: 0.92rem; }
.pd-list span { font-size: 0.8rem; color: #64748b; overflow-wrap: anywhere; }
.pd-empty { margin: 0; color: #64748b; font-size: 0.9rem; }
.pd-list .pd-pill, .pd-pill { flex-shrink: 0; padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.75rem; font-weight: 700; white-space: nowrap; }
.pd-pill--info { background: #e0f2fe; color: #0369a1 !important; }
.pd-pill--warn { background: #fef3c7; color: #92400e !important; }
.pd-pill--ok { background: #dcfce7; color: #166534 !important; }
@media (max-width: 720px) {
  .pd-kpis, .pd-grid { grid-template-columns: minmax(0, 1fr); }
  .pd-hero { padding: 1.1rem; }
  .pd-hero h1 { font-size: 1.25rem; }
  .pd-list li { flex-wrap: wrap; }
  .pd-btn { flex: 1 1 100%; }
}
</style>