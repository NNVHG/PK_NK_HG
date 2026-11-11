<template>
  <section v-if="auth.role === 'ADMIN'" class="revenue-report">
    <header class="page-heading no-print">
      <div>
        <p class="eyebrow">TÀI CHÍNH & KẾ TOÁN PHÒNG KHÁM</p>
        <h1>Báo cáo doanh thu & Dòng tiền</h1>
        <p class="subtitle">Hệ thống ghi nhận theo giờ Việt Nam (UTC+7) · Đơn vị tiền tệ: VNĐ · Dữ liệu đối soát thực thu theo ca khám & hóa đơn.</p>
      </div>
      <div v-if="report" class="heading-actions">
        <button class="button button-secondary" :disabled="!current || loading || exporting" @click="exportCsv">
          <span class="btn-icon">📥</span> {{ exporting ? 'Đang xuất…' : 'Xuất CSV' }}
        </button>
        <button class="button button-primary" :disabled="!current || loading || exporting" @click="printReport">
          <span class="btn-icon">🖨️</span> In A4 ngang
        </button>
      </div>
    </header>

    <form class="panel filters-panel no-print" @submit.prevent="load">
      <div class="filters-grid">
        <label class="filter-field">
          <span>Từ ngày</span>
          <input v-model="form.fromDate" type="date" required :disabled="loading || exporting" />
        </label>
        <label class="filter-field">
          <span>Đến ngày</span>
          <input v-model="form.toDate" type="date" required :disabled="loading || exporting" />
        </label>
        <label class="filter-field">
          <span>Nhóm thời gian</span>
          <select v-model="form.groupBy" :disabled="loading || exporting">
            <option value="Auto">Tự động</option>
            <option value="Day">Theo ngày</option>
            <option value="Week">Theo tuần ISO</option>
            <option value="Month">Theo tháng</option>
          </select>
        </label>
        <label class="filter-field">
          <span>Top dịch vụ</span>
          <select v-model="form.top" :disabled="loading || exporting">
            <option>5</option>
            <option>10</option>
            <option>20</option>
            <option value="All">Tất cả</option>
          </select>
        </label>
        <label class="filter-field">
          <span>Sắp xếp</span>
          <select v-model="form.sortBy" :disabled="loading || exporting">
            <option value="Revenue">Theo doanh thu</option>
            <option value="UsageCount">Theo số lượng làm</option>
          </select>
        </label>
        <label class="filter-field">
          <span>Mã thu ngân (tùy chọn)</span>
          <input v-model="cashier" type="number" min="1" max="2147483647" step="1" placeholder="Tất cả thu ngân" :disabled="loading || exporting" />
        </label>
        <div class="filter-submit">
          <button class="button button-primary" :disabled="loading || exporting">
            <span class="btn-icon">{{ loading ? '⏳' : '🔍' }}</span>
            {{ loading ? 'Đang tải…' : 'Xem báo cáo' }}
          </button>
        </div>
      </div>
      <div class="quick-pills">
        <span class="quick-label">Khoảng nhanh:</span>
        <button
          v-for="q in quickFilters"
          :key="q.value"
          type="button"
          class="pill-btn"
          :disabled="loading || exporting"
          @click="quick(q.value)"
        >
          {{ q.label }}
        </button>
      </div>
    </form>

    <p v-if="error" role="alert" class="alert alert-error no-print">{{ error }}</p>
    <p v-if="report && !current" class="alert alert-warning no-print" role="status">
      Bộ lọc đã thay đổi. Bấm "Xem báo cáo" để cập nhật số liệu mới nhất.
    </p>

    <template v-if="report">
      <div class="report-meta">
        <span class="meta-item"><strong>Kỳ báo cáo:</strong> {{ report.filter.fromDate }} ➔ {{ report.filter.toDate }}</span>
        <span class="meta-item"><strong>Thu ngân:</strong> {{ report.filter.cashierId ? '#' + report.filter.cashierId : 'Toàn phòng khám' }}</span>
        <span class="meta-item"><strong>Cập nhật lúc:</strong> {{ dateTime(report.generatedAt) }}</span>
      </div>

      <div class="kpi-cards">
        <div class="kpi-card kpi-card--gross">
          <div class="kpi-label">Thu trước hoàn trong kỳ</div>
          <div class="kpi-value">{{ money(report.summary.grossRevenue) }}</div>
          <div class="kpi-hint">Tổng tiền mặt & chuyển khoản đã nhận</div>
        </div>
        <div class="kpi-card kpi-card--refund">
          <div class="kpi-label">Đã hoàn trả trong kỳ</div>
          <div class="kpi-value">{{ money(report.summary.totalRefunded) }}</div>
          <div class="kpi-hint">Các khoản trả lại bệnh nhân</div>
        </div>
        <div class="kpi-card kpi-card--net">
          <div class="kpi-label">Doanh thu thuần trong kỳ</div>
          <div class="kpi-value text-emerald">{{ money(report.summary.netRevenue) }}</div>
          <div class="kpi-hint">Doanh số thực thu sau đối trừ hoàn trả</div>
        </div>
      </div>

      <aside class="panel debt-panel">
        <div class="debt-header">
          <div>
            <div class="debt-title">TỔNG DƯ NỢ HIỆN TẠI TOÀN PHÒNG KHÁM</div>
            <div class="debt-amount">{{ money(report.summary.totalOutstanding) }}</div>
          </div>
          <span class="debt-badge">Dư nợ lâm sàng</span>
        </div>
        <p class="debt-desc">
          Số liệu độc lập với kỳ lọc và thu ngân; tính tại thời điểm hiện hành trên tất cả hồ sơ điều trị đang có số dư nợ chưa quyết toán.
        </p>
      </aside>
      <section class="panel details-panel">
        <h3 class="panel-subtitle">Chỉ số tài chính phụ trợ trong kỳ</h3>
        <div class="details-grid">
          <div class="detail-item">
            <span class="detail-title">Hóa đơn xác nhận lập (sau giảm)</span>
            <span class="detail-val">{{ money(report.summary.totalInvoiced) }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-title">Tổng chiết khấu / giảm giá</span>
            <span class="detail-val">{{ money(report.summary.totalDiscount) }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-title">Khoản thu của hóa đơn trả góp (PartiallyPaid)</span>
            <span class="detail-val">{{ money(report.summary.temporaryCollections) }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-title">Hóa đơn hủy trước khi thu tiền</span>
            <span class="detail-val">{{ report.summary.cancelledInvoiceCount }} hóa đơn</span>
          </div>
        </div>
      </section>

      <section class="panel section-panel">
        <h2 class="section-title">Diễn biến doanh thu thuần ({{ groupLabel(report.filter.groupBy) }})</h2>
        <div class="timeline" aria-label="Doanh thu theo thời gian">
          <div v-for="p in report.revenueTimeline" :key="p.label" class="period-row">
            <div class="period-label">{{ p.label }}</div>
            <div class="bar-track">
              <div
                class="bar-fill"
                :class="{ 'bar-fill--negative': p.revenue < 0 }"
                :style="{ width: barWidth(p.revenue) + '%' }"
              ></div>
            </div>
            <div class="period-val">{{ money(p.revenue) }}</div>
            <div class="period-count">{{ p.transactionCount }} GD</div>
          </div>
        </div>
      </section>

      <div class="tables-row">
        <section class="panel table-panel">
          <h2 class="section-title">Cơ cấu phương thức thanh toán</h2>
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Phương thức</th>
                  <th>Thu trước hoàn</th>
                  <th>Đã hoàn</th>
                  <th>Thuần</th>
                  <th>Tỷ trọng</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="m in report.byPaymentMethod" :key="m.method">
                  <td><strong>{{ methodLabel(m.method) }}</strong></td>
                  <td>{{ money(m.grossRevenue) }}</td>
                  <td>{{ money(m.totalRefunded) }}</td>
                  <td><strong>{{ money(m.amount) }}</strong></td>
                  <td><span class="badge-ratio">{{ m.percentage }}%</span></td>
                </tr>
                <tr v-if="!report.byPaymentMethod.length">
                  <td colspan="5" class="empty-cell">Không có giao dịch trong kỳ.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel table-panel">
          <h2 class="section-title">Hiệu suất theo thu ngân</h2>
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Mã</th>
                  <th>Thu ngân viên</th>
                  <th>Số giao dịch</th>
                  <th>Doanh thu thuần</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="c in report.byCashier" :key="c.cashierId">
                  <td><code>#{{ c.cashierId }}</code></td>
                  <td><strong>{{ c.name }}</strong></td>
                  <td>{{ c.transactionCount }}</td>
                  <td><strong>{{ money(c.amount) }}</strong></td>
                </tr>
                <tr v-if="!report.byCashier.length">
                  <td colspan="4" class="empty-cell">Không có giao dịch trong kỳ.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
      <section class="panel table-panel">
        <h2 class="section-title">Top dịch vụ theo khoản thu phân bổ</h2>
        <p class="explanation">
          Chỉ tính từ hóa đơn đã hoàn tất thanh toán và ca khám đã kết thúc, không có hoàn tiền. Tiền thu phân bổ về đúng kỳ thực thu sau chiết khấu; tiền thuốc kê đơn tách riêng.
        </p>
        <div class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th>Mã DV</th>
                <th>Tên dịch vụ kỹ thuật</th>
                <th>Số lượt thực hiện</th>
                <th>Thực thu phân bổ</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in report.topServices" :key="s.serviceCode + '/' + s.serviceName">
                <td><code>{{ s.serviceCode }}</code></td>
                <td><strong>{{ s.serviceName }}</strong></td>
                <td>{{ s.usageCount }} ca</td>
                <td><strong class="text-emerald">{{ money(s.revenue) }}</strong></td>
              </tr>
              <tr v-if="!report.topServices.length">
                <td colspan="4" class="empty-cell">Không có dịch vụ đủ điều kiện phân bổ trong kỳ.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <details class="panel daily-accordion">
        <summary class="daily-summary">
          <span>📅 Xem chi tiết dữ liệu từng ngày trong kỳ</span>
        </summary>
        <div class="table-wrap daily-table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th>Ngày (UTC+7)</th>
                <th>Thu trước hoàn</th>
                <th>Đã hoàn</th>
                <th>Thuần</th>
                <th>Số giao dịch</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="d in report.dailyRevenue" :key="d.date">
                <td><code>{{ d.date }}</code></td>
                <td>{{ money(d.grossRevenue) }}</td>
                <td>{{ money(d.totalRefunded) }}</td>
                <td><strong>{{ money(d.revenue) }}</strong></td>
                <td>{{ d.transactionCount }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </details>

      <footer class="signatures">
        <p><strong>Người lập biểu</strong><br /><small>(Ký, ghi rõ họ tên)</small></p>
        <p><strong>Giám đốc chuyên môn / Người đại diện</strong><br /><small>(Ký, ghi rõ họ tên)</small></p>
      </footer>
    </template>
  </section>
  <p v-else role="alert" class="alert alert-error">Chỉ tài khoản Quản trị viên (Admin) mới có quyền truy cập Báo cáo doanh thu.</p>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { reportsService, type RevenueQuery, type RevenueReport } from '@/services/reports';

const auth = useAuthStore();
function vietnamToday() { return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()); }
function dayString(d: Date) { return d.toISOString().slice(0, 10); }
const today = vietnamToday();
const monthEnd = new Date(Date.UTC(Number(today.slice(0, 4)), Number(today.slice(5, 7)), 0));
const form = reactive({ fromDate: today.slice(0, 7) + '-01', toDate: dayString(monthEnd), groupBy: 'Auto', top: '10', sortBy: 'Revenue' });
const cashier = ref('');
const report = ref<RevenueReport | null>(null), loading = ref(false), exporting = ref(false), error = ref('');
const loadedKey = ref('');
let disposed = false, generation = 0;
const query = (): RevenueQuery => ({ ...form, ...(cashier.value.trim() ? { cashierId: Number(cashier.value) } : {}) });
const key = () => JSON.stringify(query());
const current = computed(() => !!report.value && loadedKey.value === key() && auth.role === 'ADMIN');
const actorKey = () => `${auth.user?.userId}/${auth.role}`;
function validate() {
  const start = Date.parse(form.fromDate), end = Date.parse(form.toDate);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(form.fromDate) || !/^\d{4}-\d{2}-\d{2}$/.test(form.toDate) ||
      !Number.isFinite(start) || !Number.isFinite(end) || dayString(new Date(start)) !== form.fromDate || dayString(new Date(end)) !== form.toDate ||
      Number(form.fromDate.slice(0, 4)) < 2 || Number(form.toDate.slice(0, 4)) > 9998 || end < start || (end - start) / 86400000 > 366 ||
      !['Auto', 'Day', 'Week', 'Month'].includes(form.groupBy) || !['5', '10', '20', 'All'].includes(form.top) || !['Revenue', 'UsageCount'].includes(form.sortBy) ||
      cashier.value.trim() && (!/^\d+$/.test(cashier.value.trim()) || !Number.isSafeInteger(Number(cashier.value)) || Number(cashier.value) < 1 || Number(cashier.value) > 2147483647)) {
    error.value = 'Bộ lọc không hợp lệ: ngày bắt đầu ≤ ngày kết thúc, tối đa 366 ngày; mã thu ngân là số nguyên dương.'; return false;
  }
  return true;
}
async function load() {
  if (disposed || auth.role !== 'ADMIN' || loading.value || exporting.value || !validate()) return;
  const mine = ++generation, actor = actorKey(), filterKey = key(), params = query();
  loading.value = true; report.value = null; error.value = ''; loadedKey.value = '';
  try {
    const data = await reportsService.revenue(params);
    if (disposed || mine !== generation || actor !== actorKey() || auth.role !== 'ADMIN' || filterKey !== key()) return;
    if (!data?.summary || !Array.isArray(data.dailyRevenue) || !Array.isArray(data.topServices) || !Array.isArray(data.byCashier) || !Array.isArray(data.byPaymentMethod) || !Array.isArray(data.revenueTimeline)) throw new Error('Invalid report');
    report.value = data; loadedKey.value = filterKey;
  } catch { if (!disposed && mine === generation && actor === actorKey()) error.value = 'Không tải được báo cáo. Kiểm tra quyền và bộ lọc rồi thử lại.'; }
  finally { if (!disposed && mine === generation) loading.value = false; }
}
const quickFilters = [{ value: 'today', label: 'Hôm nay' }, { value: 'week', label: '7 ngày qua' }, { value: 'month', label: 'Tháng này' }, { value: 'quarter', label: 'Quý này' }, { value: 'year', label: 'Năm nay' }];
function quick(value: string) {
  if (loading.value || exporting.value || disposed) return;
  const date = new Date(vietnamToday() + 'T00:00:00Z'), y = date.getUTCFullYear(), m = date.getUTCMonth();
  if (value === 'today') form.fromDate = form.toDate = dayString(date);
  if (value === 'week') { form.toDate = dayString(date); date.setUTCDate(date.getUTCDate() - 6); form.fromDate = dayString(date); }
  if (value === 'month') { form.fromDate = dayString(new Date(Date.UTC(y, m, 1))); form.toDate = dayString(new Date(Date.UTC(y, m + 1, 0))); }
  if (value === 'quarter') { const q = Math.floor(m / 3) * 3; form.fromDate = dayString(new Date(Date.UTC(y, q, 1))); form.toDate = dayString(new Date(Date.UTC(y, q + 3, 0))); }
  if (value === 'year') { form.fromDate = `${y}-01-01`; form.toDate = `${y}-12-31`; }
}
async function exportCsv() {
  if (disposed || !current.value || loading.value || exporting.value || !validate()) return;
  const actor = actorKey(), filterKey = key(), mine = generation;
  exporting.value = true; error.value = '';
  try {
    const result = await reportsService.exportRevenue(query());
    if (disposed || actor !== actorKey() || auth.role !== 'ADMIN' || mine !== generation || filterKey !== key()) return;
    const url = URL.createObjectURL(result.blob);
    try { const a = document.createElement('a'); a.href = url; a.download = result.filename; document.body.appendChild(a); a.click(); a.remove(); }
    finally { URL.revokeObjectURL(url); }
  } catch { if (!disposed && actor === actorKey()) error.value = 'Không xuất được CSV. Vui lòng thử lại.'; }
  finally { if (!disposed && actor === actorKey()) exporting.value = false; }
}
function printReport() {
  if (disposed || !current.value || loading.value || exporting.value) return;
  const style = document.createElement('style'); style.textContent = '@page { size: A4 landscape; margin: 12mm; }'; document.head.appendChild(style);
  try { window.print(); } finally { style.remove(); }
}
function money(value: number) { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value); }
function dateTime(value: string) { return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value)); }
function groupLabel(value: string) { return ({ Day: 'Ngày', Week: 'Tuần ISO', Month: 'Tháng' } as Record<string, string>)[value] ?? value; }
function methodLabel(value: string) { return ({ Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', Card: 'Thẻ POS' } as Record<string, string>)[value] ?? value; }
function barWidth(value: number) { const max = Math.max(1, ...(report.value?.revenueTimeline.map(p => Math.abs(p.revenue)) ?? [])); return Math.abs(value) / max * 100; }
watch(actorKey, () => { generation++; report.value = null; loadedKey.value = ''; error.value = ''; loading.value = false; exporting.value = false; });
onMounted(load);
onUnmounted(() => { disposed = true; generation++; report.value = null; loadedKey.value = ''; });
</script>

<style scoped>
.revenue-report {
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
  gap: 1.25rem;
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

.heading-actions {
  display: flex;
  gap: 0.65rem;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  height: 40px;
  padding: 0 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
  border: 1px solid transparent;
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
  opacity: 0.55;
  cursor: not-allowed;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
  padding: 1.25rem;
}

.filters-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.85rem;
  align-items: end;
}

.filter-field {
  display: grid;
  gap: 0.35rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
}

.filter-field input,
.filter-field select {
  height: 40px;
  padding: 0 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #f8fafc;
  color: #1e293b;
  font: inherit;
  font-size: 0.875rem;
  outline: none;
}

.filter-field input:focus,
.filter-field select:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.filter-submit {
  display: flex;
}

.filter-submit .button {
  width: 100%;
}

.quick-pills {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid #f1f5f9;
}

.quick-label {
  font-size: 0.8rem;
  color: #64748b;
  font-weight: 600;
}

.pill-btn {
  height: 32px;
  padding: 0 0.85rem;
  border-radius: 999px;
  border: 1px solid #e2e8f0;
  background: #f8fafc;
  color: #475569;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}

.pill-btn:hover:not(:disabled) {
  background: #e0f2fe;
  border-color: #bae6fd;
  color: #0284c7;
}

.report-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 1.25rem;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 8px;
  padding: 0.75rem 1rem;
  font-size: 0.85rem;
  color: #0369a1;
}

/* KPI summary */
.kpi-cards {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem;
}

.kpi-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
  border-left: 4px solid #94a3b8;
}

.kpi-card--gross { border-left-color: #0284c7; }
.kpi-card--refund { border-left-color: #ef4444; }
.kpi-card--net { border-left-color: #10b981; }

.kpi-label {
  font-size: 0.825rem;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.kpi-value {
  font-size: 1.95rem;
  font-weight: 850;
  color: #0f2d59;
  line-height: 1.2;
  margin: 0.35rem 0;
}

.text-emerald {
  color: #059669;
}

.kpi-hint {
  font-size: 0.775rem;
  color: #94a3b8;
}

/* Debt panel */
.debt-panel {
  background: #fffbeb;
  border: 1px solid #fef3c7;
  border-left: 4px solid #f59e0b;
}

.debt-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.debt-title {
  font-size: 0.825rem;
  font-weight: 800;
  color: #b45309;
  letter-spacing: 0.04em;
}

.debt-amount {
  font-size: 1.85rem;
  font-weight: 850;
  color: #92400e;
  line-height: 1.2;
  margin: 0.25rem 0;
}

.debt-badge {
  background: #fde68a;
  color: #78350f;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 0.25rem 0.65rem;
  border-radius: 6px;
}

.debt-desc {
  font-size: 0.85rem;
  color: #78350f;
  margin: 0.5rem 0 0;
  line-height: 1.5;
}
/* Details grid */
.details-panel {
  padding: 1.15rem 1.25rem;
}

.panel-subtitle {
  font-size: 0.95rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0 0 0.85rem;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 1rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  background: #f8fafc;
  padding: 0.75rem 0.95rem;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
}

.detail-title {
  font-size: 0.785rem;
  color: #64748b;
  font-weight: 600;
}

.detail-val {
  font-size: 1.1rem;
  font-weight: 800;
  color: #0f2d59;
}

/* Section panel & timeline */
.section-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section-title {
  font-size: 1.1rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
}

.timeline {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.period-row {
  display: grid;
  grid-template-columns: 8rem 1fr 11rem 6rem;
  gap: 0.85rem;
  align-items: center;
  font-size: 0.885rem;
}

.period-label {
  font-weight: 700;
  color: #334155;
}

.bar-track {
  background: #e2e8f0;
  height: 1.15rem;
  border-radius: 6px;
  overflow: hidden;
}

.bar-fill {
  background: #0284c7;
  height: 100%;
  border-radius: 6px;
  transition: width 0.3s ease;
}

.bar-fill--negative {
  background: #ef4444;
}

.period-val {
  font-weight: 800;
  color: #0f2d59;
  text-align: right;
}

.period-count {
  color: #64748b;
  font-size: 0.8rem;
  text-align: right;
}

/* Tables row */
.tables-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.table-panel {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.table-wrap {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.data-table th,
.data-table td {
  padding: 0.75rem 0.85rem;
  border-bottom: 1px solid #f1f5f9;
  text-align: right;
}

.data-table th:first-child,
.data-table td:first-child,
.data-table th:nth-child(2),
.data-table td:nth-child(2) {
  text-align: left;
}

.data-table thead th {
  background: #f8fafc;
  color: #475569;
  font-size: 0.775rem;
  font-weight: 750;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  border-bottom: 1px solid #e2e8f0;
}

.badge-ratio {
  background: #e0f2fe;
  color: #0369a1;
  font-weight: 800;
  font-size: 0.75rem;
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
}

.explanation {
  font-size: 0.825rem;
  color: #64748b;
  line-height: 1.5;
  margin: 0;
}

.empty-cell {
  text-align: center !important;
  color: #94a3b8;
  padding: 2rem !important;
}

/* Accordion */
.daily-accordion {
  padding: 0;
  overflow: hidden;
}

.daily-summary {
  padding: 1rem 1.25rem;
  font-weight: 750;
  color: #0f2d59;
  cursor: pointer;
  background: #f8fafc;
  user-select: none;
}

.daily-table-wrap {
  padding: 0.5rem 1.25rem 1.25rem;
}

.signatures {
  display: none;
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

.alert-warning {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fef3c7;
}

@media (max-width: 900px) {
  .tables-row { grid-template-columns: 1fr; }
  .kpi-cards { grid-template-columns: 1fr; }
  .period-row { grid-template-columns: 7rem 1fr 9rem; }
  .period-count { display: none; }
}

@media print {
  .no-print { display: none !important; }
  .panel { border: 1px solid #cbd5e1; box-shadow: none; break-inside: avoid; }
  .kpi-cards, .debt-panel { break-inside: avoid; }
  thead { display: table-header-group; }
  tr, .period-row { break-inside: avoid; }
  .signatures { display: flex; justify-content: space-around; margin: 3rem 0; text-align: center; }
  .daily-accordion { display: none; }
  .table-wrap { overflow: visible; }
  .data-table th, .data-table td { padding: 0.4rem; }
  .bar-fill { print-color-adjust: exact; }
}
</style>
<style>
@media print { body:has(.revenue-report) .navbar { display: none !important; } body:has(.revenue-report) .main-content { max-width: none; margin: 0; padding: 0; } }
</style>
