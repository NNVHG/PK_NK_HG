<template>
  <div class="history-split" aria-label="Lịch sử hóa đơn tại quầy">
    <aside class="history-sidebar">
      <slot name="before-filters" />

      <section class="history-card history-filter-card" aria-labelledby="history-filter-title">
        <div class="filter-heading">
          <h2 id="history-filter-title">Bộ lọc tìm hóa đơn</h2>
          <p class="history-muted">Có thể tìm theo mã, tên/SĐT, trạng thái hoặc ngày lập.</p>
        </div>
        <form class="filter-form" @submit.prevent="search">
          <label class="filter-field" for="history-invoice-code">
            <span>Mã hóa đơn đầy đủ</span>
            <input
              id="history-invoice-code"
              v-model="invoiceCode"
              class="app-input"
              type="search"
              maxlength="50"
              autocomplete="off"
              :disabled="busy || disabled"
              placeholder="INV-20261015-0001"
            />
          </label>
          <label class="filter-field" for="history-keyword">
            <span>Tên hoặc SĐT bệnh nhân</span>
            <input
              id="history-keyword"
              v-model="keyword"
              class="app-input"
              type="search"
              maxlength="100"
              autocomplete="off"
              :disabled="busy || disabled"
              placeholder="Nhập tên hoặc số điện thoại"
            />
          </label>
          <label class="filter-field" for="history-patient-code">
            <span>Mã bệnh nhân</span>
            <input
              id="history-patient-code"
              v-model="patientCode"
              class="app-input"
              maxlength="12"
              autocomplete="off"
              :disabled="busy || disabled"
              placeholder="BN000001"
            />
          </label>
          <label class="filter-field" for="history-status">
            <span>Trạng thái</span>
            <select id="history-status" v-model="status" class="app-select" :disabled="busy || disabled">
              <option value="">Tất cả</option>
              <option v-for="(name, index) in statuses" :key="index" :value="String(index)">{{ name }}</option>
            </select>
          </label>
          <div class="filter-dates">
            <label class="filter-field" for="history-from-date">
              <span>Từ ngày lập (giờ VN)</span>
              <input
                id="history-from-date"
                v-model="fromDate"
                class="app-input"
                type="date"
                min="0001-01-02"
                max="9999-12-31"
                :disabled="busy || disabled"
              />
            </label>
            <label class="filter-field" for="history-to-date">
              <span>Đến ngày lập (gồm cả ngày)</span>
              <input
                id="history-to-date"
                v-model="toDate"
                class="app-input"
                type="date"
                min="0001-01-01"
                max="9999-12-30"
                :disabled="busy || disabled"
              />
            </label>
          </div>
          <p v-if="error" class="history-error" role="alert">{{ error }}</p>
          <button type="submit" class="app-btn app-btn--primary filter-submit" :disabled="busy || disabled || !allowed">
            {{ busy ? 'Đang tìm…' : 'Tìm hóa đơn' }}
          </button>
        </form>
      </section>
    </aside>

    <section class="history-card history-results-panel" aria-labelledby="history-results-title">
      <header class="results-header">
        <div>
          <h2 id="history-results-title">Danh sách hóa đơn</h2>
          <p v-if="loaded" class="results-subtitle">
            Trang {{ totalPages > 0 ? page : 0 }} / {{ totalPages }} · {{ totalCount }} hóa đơn theo bộ lọc đã áp dụng
          </p>
          <p v-else class="results-subtitle">
            Nhập bộ lọc bên trái rồi nhấn Tìm hóa đơn để hiển thị danh sách.
          </p>
        </div>
      </header>

      <slot name="results-banner" />

      <p v-if="busy" class="results-state" role="status">Đang tải danh sách…</p>

      <div v-else-if="!loaded" class="results-placeholder">
        <p>
          Danh sách hóa đơn sẽ hiển thị tại đây ngay bên cạnh bộ lọc sau khi bạn nhấn
          <strong>Tìm hóa đơn</strong> (có thể để trống các ô để xem toàn bộ).
        </p>
      </div>

      <div v-else-if="!rows.length" class="results-placeholder">
        <p class="results-empty-title">Không có hóa đơn trong trang và bộ lọc này.</p>
        <p>Thử điều chỉnh mã, từ khóa hoặc khoảng ngày ở bộ lọc bên trái.</p>
      </div>

      <ul v-else class="invoice-list">
        <li v-for="row in rows" :key="row.invoiceId" class="invoice-item">
          <div class="invoice-item-head">
            <div class="invoice-item-identity">
              <div class="invoice-item-title">
                <strong class="invoice-item-code">{{ row.invoiceCode }}</strong>
                <span class="invoice-item-patient">{{ row.patientName }}</span>
              </div>
              <p class="invoice-item-meta">
                {{ row.patientCode }} · {{ formatDate(row.createdAt) }} (giờ Việt Nam)
              </p>
            </div>
            <span class="app-badge" :class="statusBadgeClass(row.status)">
              {{ statusLabels[row.status] ?? 'Không xác định' }}
            </span>
          </div>

          <div class="invoice-item-footer">
            <dl class="invoice-item-metrics">
              <div>
                <dt>Tổng</dt>
                <dd>{{ money(row.totalAmount) }}</dd>
              </div>
              <div>
                <dt>Đã trả</dt>
                <dd>{{ money(row.paidAmount) }}</dd>
              </div>
              <div :class="{ 'metric-debt--due': row.remainingAmount > 0 }">
                <dt>Còn nợ</dt>
                <dd>{{ money(row.remainingAmount) }}</dd>
              </div>
            </dl>
            <button
              type="button"
              class="app-btn app-btn--primary item-detail-btn"
              :disabled="busy || disabled || !allowed"
              @click="selectInvoice(row)"
            >
              Xem chi tiết
            </button>
          </div>
        </li>
      </ul>

      <nav v-if="loaded && totalCount > 0" class="results-pagination" aria-label="Phân trang hóa đơn">
        <p class="results-pagination-info">
          Trang {{ page }} / {{ totalPages }} · {{ totalCount }} hóa đơn
        </p>
        <div class="results-pagination-actions">
          <button
            type="button"
            class="app-btn app-btn--secondary"
            :disabled="busy || disabled || page <= 1"
            @click="loadPage(page - 1)"
          >
            Trang trước
          </button>
          <button
            type="button"
            class="app-btn app-btn--secondary"
            :disabled="busy || disabled || page >= totalPages"
            @click="loadPage(page + 1)"
          >
            Trang sau
          </button>
        </div>
      </nav>
    </section>
  </div>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { invoiceHistoryApi, type InvoiceHistoryQuery } from '@/services/invoiceHistory';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
const props = defineProps<{ disabled?: boolean }>();
const emit = defineEmits<{ selected: [code: string] }>();
const auth = useAuthStore();
const allowed = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role));
const patientCode = ref('');
const keyword = ref('');
const invoiceCode = ref('');
const status = ref('');
const fromDate = ref('');
const toDate = ref('');
const rows = ref<InvoiceLookupResponse[]>([]);
const page = ref(1);
const totalPages = ref(0);
const totalCount = ref(0);
const busy = ref(false);
const loaded = ref(false);
const error = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const statusLabels = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy', 'Đã hoàn một phần', 'Đã hoàn toàn bộ'];
function statusBadgeClass(value: number) {
  if (value === 3) return 'app-badge--success';
  if (value === 2 || value === 5) return 'app-badge--warning';
  if (value === 4) return 'app-badge--danger';
  return 'app-badge--info';
}
const formatDate = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23'
}).format(new Date(value));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let applied: Omit<InvoiceHistoryQuery, 'page'> = { pageSize: 20 };
let version = 0;
function validCode(value: string) {
  if (!value) return true;
  if (!/^BN[0-9]{6,10}$/.test(value)) return false;
  const number = Number(value.slice(2));
  return number > 0 && number <= 2147483647 && value === 'BN' + String(number).padStart(6, '0');
}
function validDate(value: string) {
  if (!value) return true;
  if (!/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(value) || value < '0001-01-01') return false;
  try { return new Date(value + 'T00:00:00Z').toISOString().slice(0, 10) === value; } catch { return false; }
}
async function search() {
  if (!allowed.value || props.disabled || busy.value) return;
  rows.value = []; loaded.value = false; totalCount.value = 0; totalPages.value = 0; error.value = '';
  if (keyword.value.length > 100) { error.value = 'Tên/SĐT tìm kiếm không được quá 100 ký tự.'; return; }
  const exactCode = invoiceCode.value.trim();
  if (exactCode && !/^[A-Za-z0-9-]{1,50}$/.test(exactCode)) {
    error.value = 'Nhập mã hóa đơn đầy đủ, tối đa 50 chữ/số ASCII hoặc dấu gạch nối.'; return;
  }
  const normalizedInvoiceCode = exactCode.toUpperCase();
  const term = keyword.value.trim();
  const code = patientCode.value.trim().toUpperCase();
  if (!validCode(code) || !['', '0', '1', '2', '3', '4'].includes(status.value)) {
    error.value = 'Nhập đúng mã bệnh nhân đầy đủ và trạng thái hợp lệ.'; return;
  }
  if (!validDate(fromDate.value) || !validDate(toDate.value) ||
      (fromDate.value && fromDate.value < '0001-01-02') || (toDate.value && toDate.value > '9999-12-30') ||
      (fromDate.value && toDate.value && fromDate.value > toDate.value)) {
    error.value = 'Nhập khoảng ngày lập hóa đơn hợp lệ; từ ngày không sau đến ngày.'; return;
  }
  invoiceCode.value = normalizedInvoiceCode;
  patientCode.value = code;
  applied = { pageSize: 20, ...(normalizedInvoiceCode ? { invoiceCode: normalizedInvoiceCode } : {}), ...(term ? { keyword: term } : {}), ...(fromDate.value ? { fromDate: fromDate.value } : {}), ...(toDate.value ? { toDate: toDate.value } : {}), ...(code ? { patientCode: code } : {}), ...(status.value !== '' ? { status: Number(status.value) } : {}) };
  await loadPage(1);
}
async function loadPage(target: number) {
  if (!allowed.value || props.disabled || busy.value || !Number.isInteger(target) || target < 1 || target > 1000000) return;
  if (target !== 1 && (!loaded.value || target > totalPages.value)) return;
  const ticket = ++version; busy.value = true; error.value = ''; rows.value = []; loaded.value = false;
  try {
    const result = await invoiceHistoryApi.get({ ...applied, page: target });
    if (ticket !== version || !allowed.value) return;
    rows.value = result.items; page.value = result.page; totalPages.value = result.totalPages; totalCount.value = result.totalCount; loaded.value = true;
  } catch { if (ticket === version) { totalPages.value = 0; totalCount.value = 0; error.value = 'Không tải được danh sách. Tìm lại để đối chiếu.'; } }
  finally { if (ticket === version) busy.value = false; }
}
async function refresh() {
  if (!loaded.value || busy.value || props.disabled || !allowed.value) return;
  await loadPage(page.value);
}
function selectInvoice(row: InvoiceLookupResponse) {
  if (!allowed.value || props.disabled || busy.value || !loaded.value || !rows.value.some(x => x.invoiceId === row.invoiceId && x.invoiceCode === row.invoiceCode)) return;
  emit('selected', row.invoiceCode);
}
defineExpose({ refresh });
onUnmounted(() => { version++; rows.value = []; patientCode.value = ''; keyword.value = ''; invoiceCode.value = ''; status.value = ''; fromDate.value = ''; toDate.value = ''; totalCount.value = 0; totalPages.value = 0; loaded.value = false; applied = { pageSize: 20 }; });
</script>
<style scoped>
.history-split {
  container-type: inline-size;
  display: grid;
  gap: 1rem;
  align-items: start;
}

.history-sidebar {
  display: grid;
  gap: 1rem;
  min-width: 0;
}

.history-card {
  display: grid;
  gap: 0.9rem;
  min-width: 0;
  padding: clamp(0.9rem, 2.2cqw, 1.25rem);
  border: 1px solid #dbe5ed;
  border-radius: 14px;
  background: #fff;
}

.history-card h2,
.history-card p {
  margin: 0;
}

.history-card h2 {
  font-size: 1.12rem;
  line-height: 1.3;
  color: #102e56;
}

.history-muted,
.results-subtitle {
  color: #51647a;
  font-size: 0.86rem;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.filter-heading {
  display: grid;
  gap: 0.25rem;
}

.filter-form {
  display: grid;
  gap: 0.75rem;
}

.filter-field {
  display: grid;
  gap: 0.3rem;
  min-width: 0;
  font-size: 0.86rem;
  font-weight: 650;
  color: #243b53;
}

.filter-field input,
.filter-field select {
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  max-width: 100%;
  min-height: 44px;
}

.filter-dates {
  display: grid;
  gap: 0.75rem;
}

.history-card .app-btn {
  min-height: 44px;
  white-space: normal;
  text-align: center;
}

.filter-submit {
  width: 100%;
}

.history-error {
  color: #8d2624;
  background: #fff0ed;
  border: 1px solid #fecaca;
  padding: 0.7rem 0.9rem;
  border-radius: 9px;
  font-size: 0.88rem;
  font-weight: 500;
  overflow-wrap: anywhere;
}

.history-results-panel {
  container-type: inline-size;
}

.results-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.6rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #e4ecf2;
}

.results-header > div {
  display: grid;
  gap: 0.25rem;
  min-width: 0;
}

.results-state,
.results-placeholder {
  padding: 1.25rem;
  border: 1px dashed #cbd5e1;
  border-radius: 12px;
  background: #f8fafc;
  color: #475569;
  font-size: 0.92rem;
  line-height: 1.5;
  overflow-wrap: anywhere;
}

.results-empty-title {
  color: #0f2d59;
  font-weight: 700;
  margin-bottom: 0.3rem;
}

.invoice-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.75rem;
}

.invoice-item {
  display: grid;
  gap: 0.75rem;
  min-width: 0;
  padding: 0.95rem 1rem;
  border: 1px solid #dbe5ed;
  border-radius: 12px;
  background: #fff;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.invoice-item:hover {
  border-color: #93c5fd;
  box-shadow: 0 3px 10px rgba(16, 46, 86, 0.06);
}

.invoice-item-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.55rem;
}

.invoice-item-identity {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
  flex: 1 1 13rem;
}

.invoice-item-title {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 0.45rem;
  min-width: 0;
}

.invoice-item-code {
  color: #0f2d59;
  font-size: 1rem;
  overflow-wrap: anywhere;
}

.invoice-item-patient {
  font-weight: 700;
  color: #172b42;
  overflow-wrap: anywhere;
}

.invoice-item-meta {
  color: #51647a;
  font-size: 0.83rem;
  overflow-wrap: anywhere;
}

.invoice-item-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.invoice-item-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(6.25rem, 1fr));
  gap: 0.45rem;
  margin: 0;
  flex: 1 1 18rem;
  min-width: 0;
}

.invoice-item-metrics > div {
  padding: 0.5rem 0.65rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #f8fafc;
  min-width: 0;
}

.invoice-item-metrics dt {
  font-size: 0.76rem;
  color: #51647a;
}

.invoice-item-metrics dd {
  margin: 0.15rem 0 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: #0f172a;
  font-variant-numeric: tabular-nums;
  overflow-wrap: anywhere;
}

.invoice-item-metrics .metric-debt--due {
  background: #e3f3f1;
  border-color: #bfe2dd;
  color: #064e52;
}

.invoice-item-metrics .metric-debt--due dt,
.invoice-item-metrics .metric-debt--due dd {
  color: #064e52;
}

.item-detail-btn {
  flex: 0 0 auto;
}

.results-pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid #e4ecf2;
}

.results-pagination-info {
  color: #51647a;
  font-size: 0.85rem;
}

.results-pagination-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
}

@container (min-width: 740px) {
  .history-split {
    grid-template-columns: minmax(17.5rem, 22rem) minmax(0, 1fr);
  }
}

@container (max-width: 440px) {
  .item-detail-btn {
    width: 100%;
  }

  .results-pagination {
    flex-direction: column;
    align-items: stretch;
  }

  .results-pagination-actions {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@container (max-width: 320px) {
  .invoice-item-metrics {
    grid-template-columns: 1fr;
  }
}
</style>
