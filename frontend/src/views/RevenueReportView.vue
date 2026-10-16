<template>
  <section v-if="auth.role === 'ADMIN'" class="revenue-report">
    <h1>Báo cáo doanh thu</h1>
    <p>Phòng Khám Nha Khoa · Tiền Việt Nam (VNĐ) · Ngày theo giờ Việt Nam (UTC+7)</p>
    <form class="filters no-print" @submit.prevent="load">
      <label>Từ ngày<input v-model="form.fromDate" type="date" required :disabled="loading || exporting" /></label>
      <label>Đến ngày<input v-model="form.toDate" type="date" required :disabled="loading || exporting" /></label>
      <label>Nhóm thời gian<select v-model="form.groupBy" :disabled="loading || exporting"><option value="Auto">Tự động</option><option value="Day">Ngày</option><option value="Week">Tuần ISO</option><option value="Month">Tháng</option></select></label>
      <label>Top dịch vụ<select v-model="form.top" :disabled="loading || exporting"><option>5</option><option>10</option><option>20</option><option value="All">Tất cả</option></select></label>
      <label>Sắp xếp<select v-model="form.sortBy" :disabled="loading || exporting"><option value="Revenue">Theo doanh thu</option><option value="UsageCount">Theo số lượng thực hiện</option></select></label>
      <label>Mã thu ngân (tùy chọn)<input v-model="cashier" type="number" min="1" max="2147483647" step="1" :disabled="loading || exporting" /></label>
      <button :disabled="loading || exporting">{{ loading ? 'Đang tải…' : 'Xem / Làm mới' }}</button>
      <div class="quick"><button v-for="q in quickFilters" :key="q.value" type="button" :disabled="loading || exporting" @click="quick(q.value)">{{ q.label }}</button></div>
    </form>
    <p v-if="error" role="alert" class="error no-print">{{ error }}</p>
    <p v-if="report && !current" class="no-print" role="status">Bộ lọc đã đổi. Bấm Xem / Làm mới để cập nhật kết quả.</p>
    <template v-if="report">
      <div class="no-print actions"><button :disabled="!current || loading || exporting" @click="exportCsv">{{ exporting ? 'Đang xuất…' : 'Xuất CSV' }}</button><button :disabled="!current || loading || exporting" @click="printReport">In A4 ngang</button></div>
      <p><strong>Kỳ:</strong> {{ report.filter.fromDate }} → {{ report.filter.toDate }} · Thu ngân: {{ report.filter.cashierId ?? 'Tất cả' }} · Cập nhật: {{ dateTime(report.generatedAt) }}</p>
      <div class="cards">
        <article><h2>Thu trước hoàn trong kỳ</h2><strong>{{ money(report.summary.grossRevenue) }}</strong></article>
        <article><h2>Đã hoàn trong kỳ</h2><strong>{{ money(report.summary.totalRefunded) }}</strong></article>
        <article><h2>Doanh thu thuần trong kỳ</h2><strong>{{ money(report.summary.netRevenue) }}</strong></article>
      </div>
      <aside class="debt"><h2>Tổng nợ hiện tại toàn phòng khám</h2><strong>{{ money(report.summary.totalOutstanding) }}</strong><p>Tại thời điểm cập nhật trên; độc lập với kỳ lọc và thu ngân. Không phải số dư nợ lịch sử cuối kỳ.</p></aside>
      <dl class="details">
        <dt>Giá trị sau giảm của hóa đơn xác nhận lập trong kỳ (toàn phòng khám)</dt><dd>{{ money(report.summary.totalInvoiced) }}</dd>
        <dt>Chiết khấu các hóa đơn đó (toàn phòng khám)</dt><dd>{{ money(report.summary.totalDiscount) }}</dd>
        <dt>Khoản thu trong kỳ của hóa đơn hiện còn PartiallyPaid (đã gồm trong tổng thu)</dt><dd>{{ money(report.summary.temporaryCollections) }}</dd>
        <dt>Hóa đơn lập trong kỳ hiện đã hủy trước thu tiền</dt><dd>{{ report.summary.cancelledInvoiceCount }}</dd>
      </dl>
      <h2>Diễn biến doanh thu thuần · {{ groupLabel(report.filter.groupBy) }}</h2>
      <div class="timeline" aria-label="Doanh thu theo thời gian"><div v-for="p in report.revenueTimeline" :key="p.label" class="period"><span>{{ p.label }}</span><div class="bar-track"><div class="bar" :class="{ negative: p.revenue < 0 }" :style="{ width: barWidth(p.revenue) + '%' }"></div></div><strong>{{ money(p.revenue) }}</strong><span>{{ p.transactionCount }} giao dịch</span></div></div>
      <h2>Phương thức thanh toán</h2>
      <div class="table-wrap"><table><thead><tr><th>Phương thức</th><th>Thu trước hoàn</th><th>Đã hoàn</th><th>Thuần</th><th>% tiền thu trước hoàn</th></tr></thead><tbody><tr v-for="m in report.byPaymentMethod" :key="m.method"><td>{{ methodLabel(m.method) }}</td><td>{{ money(m.grossRevenue) }}</td><td>{{ money(m.totalRefunded) }}</td><td>{{ money(m.amount) }}</td><td>{{ m.percentage }}%</td></tr><tr v-if="!report.byPaymentMethod.length"><td colspan="5">Không có giao dịch trong kỳ.</td></tr></tbody></table></div>
      <h2>Theo thu ngân</h2>
      <div class="table-wrap"><table><thead><tr><th>Mã</th><th>Thu ngân</th><th>Số giao dịch</th><th>Doanh thu thuần</th></tr></thead><tbody><tr v-for="c in report.byCashier" :key="c.cashierId"><td>{{ c.cashierId }}</td><td>{{ c.name }}</td><td>{{ c.transactionCount }}</td><td>{{ money(c.amount) }}</td></tr><tr v-if="!report.byCashier.length"><td colspan="4">Không có giao dịch trong kỳ.</td></tr></tbody></table></div>
      <h2>Top dịch vụ theo khoản thu phân bổ</h2>
      <p class="explanation">Chỉ từ hóa đơn hiện đã Paid và ca khám Completed, không có hoàn tiền. Tiền thu phân bổ về đúng kỳ thực thu sau chiết khấu; tiền thuốc tách khỏi bảng này. Khi tất toán, cơ cấu kỳ cũ được cập nhật. Số lượng thực hiện tính một lần mỗi dòng dịch vụ trong kỳ có khoản thu được phân bổ; không cộng số lượng nhiều lần theo số phiếu thu.</p>
      <div class="table-wrap"><table><thead><tr><th>Mã</th><th>Dịch vụ</th><th>Số lượng thực hiện</th><th>Thực thu phân bổ</th></tr></thead><tbody><tr v-for="s in report.topServices" :key="s.serviceCode + '/' + s.serviceName"><td>{{ s.serviceCode }}</td><td>{{ s.serviceName }}</td><td>{{ s.usageCount }}</td><td>{{ money(s.revenue) }}</td></tr><tr v-if="!report.topServices.length"><td colspan="4">Không có dịch vụ đủ điều kiện phân bổ trong kỳ.</td></tr></tbody></table></div>
      <details class="daily"><summary>Chi tiết từng ngày</summary><table><thead><tr><th>Ngày UTC+7</th><th>Thu trước hoàn</th><th>Đã hoàn</th><th>Thuần</th><th>Số giao dịch</th></tr></thead><tbody><tr v-for="d in report.dailyRevenue" :key="d.date"><td>{{ d.date }}</td><td>{{ money(d.grossRevenue) }}</td><td>{{ money(d.totalRefunded) }}</td><td>{{ money(d.revenue) }}</td><td>{{ d.transactionCount }}</td></tr></tbody></table></details>
      <footer class="signatures"><p>Người lập biểu<br /><small>(Ký, ghi rõ họ tên)</small></p><p>Giám đốc phòng khám / Người đại diện<br /><small>(Ký, ghi rõ họ tên)</small></p></footer>
    </template>
  </section>
  <p v-else role="alert">Chỉ Admin được xem báo cáo doanh thu.</p>
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
h1 { margin-bottom: .4rem; } h2 { font-size: 1.05rem; margin: 1.5rem 0 .7rem; }
.filters { display: flex; gap: .8rem; flex-wrap: wrap; padding: 1rem; margin: 1.2rem 0; background: #f1f5f9; border-radius: 8px; align-items: end; }
label { display: grid; gap: .3rem; font-size: .9rem; } input, select, button { padding: .55rem; border: 1px solid #94a3b8; border-radius: 4px; } button { cursor: pointer; } button:disabled { opacity: .55; cursor: default; }
.quick { flex-basis: 100%; display: flex; gap: .4rem; flex-wrap: wrap; } .actions { display: flex; gap: .6rem; justify-content: flex-end; }
.cards { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; } .cards article { padding: 1rem; border: 1px solid #cbd5e1; border-radius: 8px; } .cards h2, .debt h2 { margin-top: 0; } .cards strong, .debt strong { font-size: 1.4rem; }
.debt { background: #eff6ff; border-left: 4px solid #2563eb; padding: 1rem; margin-top: 1rem; } .debt p, .explanation { font-size: .9rem; line-height: 1.6; } .details { display: grid; grid-template-columns: 1fr auto; gap: .6rem; padding: 1rem 0; } dd { text-align: right; margin: 0; }
.period { display: grid; grid-template-columns: 7.5rem 1fr 10rem 7rem; gap: .5rem; padding: .4rem 0; align-items: center; font-size: .85rem; } .bar-track { background: #e2e8f0; height: 1rem; } .bar { background: #0f766e; height: 100%; } .negative { background: #be123c; }
.table-wrap { overflow-x: auto; } table { width: 100%; border-collapse: collapse; font-size: .9rem; } th, td { border-bottom: 1px solid #cbd5e1; padding: .7rem; text-align: right; } th:first-child, td:first-child, th:nth-child(2), td:nth-child(2) { text-align: left; } th { background: #f1f5f9; } .daily { margin-top: 1.5rem; } .error { color: #b91c1c; } .signatures { display: none; }
@media (max-width: 700px) { .cards { grid-template-columns: 1fr; } .period { grid-template-columns: 7rem 1fr; } .details { grid-template-columns: 1fr; } dd { text-align: left; } }
@media print { .no-print { display: none !important; } .cards, .debt { break-inside: avoid; } thead { display: table-header-group; } tr, .period { break-inside: avoid; } .signatures { display: flex; justify-content: space-around; margin: 2rem 0 4rem; text-align: center; } .daily { display: none; } .table-wrap { overflow: visible; } th, td { padding: .35rem; } .bar { print-color-adjust: exact; } }
</style>
<style>
@media print { body:has(.revenue-report) .navbar { display: none !important; } body:has(.revenue-report) .main-content { max-width: none; margin: 0; padding: 0; } }
</style>
