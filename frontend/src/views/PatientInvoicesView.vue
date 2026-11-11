<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">Tài chính & Thanh toán</p>
        <h1>Hóa đơn của tôi</h1>
        <p class="subtitle">Theo dõi chi phí dịch vụ, công nợ điều trị và lịch sử các đợt thanh toán</p>
      </div>
      <div class="header-actions">
        <button type="button" class="button button-secondary" :disabled="profilesBusy" @click="loadProfiles">
          <span :class="{ 'spin-icon': profilesBusy }">🔄</span>
          <span>Tải lại hồ sơ</span>
        </button>
      </div>
    </div>

    <!-- Thông báo lỗi chung -->
    <div v-if="profileError" class="alert alert-error" role="alert">
      <span class="alert-icon">⚠️</span>
      <span>{{ profileError }}</span>
    </div>

    <div v-if="!profilesBusy && !profileError && !profiles.length" class="alert alert-info">
      <span class="alert-icon">ℹ️</span>
      <div>
        <strong>Chưa có hồ sơ liên kết</strong>
        <p>Tài khoản chưa có hồ sơ bệnh nhân đang hoạt động. Vui lòng liên hệ Lễ tân để kích hoạt.</p>
      </div>
    </div>

    <!-- Chọn hồ sơ khám (nếu có người giám hộ / nhiều hồ sơ) -->
    <div v-if="profiles.length > 1" class="panel profile-picker-panel">
      <div class="picker-row">
        <label for="select-profile" class="picker-label">
          <span>👶 Xem hóa đơn theo hồ sơ:</span>
        </label>
        <select
          id="select-profile"
          v-model.number="patientId"
          :disabled="profilesBusy || invoicesBusy"
          class="form-select profile-select"
          @change="selectProfile"
        >
          <option v-for="profile in profiles" :key="profile.patientId" :value="profile.patientId">
            {{ profile.fullName }} · {{ profile.patientCode }}
          </option>
        </select>
      </div>

      <div v-if="validProfile && selectedProfile" class="selected-profile-tag">
        <span>Hồ sơ hiện tại: <strong>{{ selectedProfile.fullName }}</strong> (Mã: {{ selectedProfile.patientCode }}) · Ngày sinh: {{ birth(selectedProfile.dateOfBirth) }}</span>
        <span v-if="!selectedProfile.isActive" class="inactive-badge">Đã ngừng hoạt động</span>
      </div>
    </div>

    <!-- Bộ lọc tìm kiếm hóa đơn -->
    <div class="panel filter-panel">
      <form class="filter-form" @submit.prevent="searchInvoices">
        <div class="filter-grid">
          <div class="form-group">
            <label for="filter-status" class="form-label">Trạng thái thanh toán</label>
            <select id="filter-status" v-model="status" :disabled="invoicesBusy || !validProfile" class="form-select">
              <option value="">Tất cả trạng thái</option>
              <option v-for="(name, index) in statuses" :key="index" :value="String(index)">{{ name }}</option>
            </select>
          </div>

          <div class="form-group">
            <label for="filter-from-date" class="form-label">Từ ngày lập</label>
            <input
              id="filter-from-date"
              v-model="fromDate"
              type="date"
              min="0001-01-02"
              max="9999-12-31"
              :disabled="invoicesBusy || !validProfile"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label for="filter-to-date" class="form-label">Đến ngày lập</label>
            <input
              id="filter-to-date"
              v-model="toDate"
              type="date"
              min="0001-01-01"
              max="9999-12-30"
              :disabled="invoicesBusy || !validProfile"
              class="form-input"
            />
          </div>
        </div>

        <div class="filter-actions">
          <button type="button" class="button button-outline" :disabled="invoicesBusy || !validProfile" @click="resetInvoiceFilters">
            Xóa bộ lọc
          </button>
          <button type="submit" class="button button-primary" :disabled="invoicesBusy || !validProfile">
            <span>🔍 Lọc hóa đơn</span>
          </button>
        </div>
      </form>
    </div>

    <!-- Trạng thái lọc đã áp dụng -->
    <div v-if="invoicesLoaded" class="applied-filter-bar">
      <span>Đang hiển thị: <strong>{{ applied.status == null ? 'Tất cả trạng thái' : statuses[applied.status] }}</strong></span>
      <span> · Từ: <strong>{{ applied.fromDate ? birth(applied.fromDate) : 'Ban đầu' }}</strong> đến: <strong>{{ applied.toDate ? birth(applied.toDate) : 'Hiện tại' }}</strong></span>
    </div>

    <!-- Thông báo lỗi khi tải hóa đơn -->
    <div v-if="invoiceError" class="alert alert-error" role="alert">
      <span class="alert-icon">⚠️</span>
      <span>{{ invoiceError }}</span>
    </div>

    <!-- Loading hóa đơn -->
    <div v-if="invoicesBusy" class="panel status-panel" role="status">
      <div class="loading-spinner"></div>
      <p>Đang tải danh sách hóa đơn điều trị…</p>
    </div>

    <!-- Empty state -->
    <div v-else-if="invoicesLoaded && !invoices.length" class="panel empty-panel">
      <span class="empty-icon">🧾</span>
      <h3>Không có hóa đơn phù hợp</h3>
      <p>Hồ sơ này chưa phát sinh chi phí điều trị trong khoảng thời gian đã chọn.</p>
    </div>
    <!-- Danh sách Hóa đơn -->
    <div v-else-if="invoices.length" class="invoices-list">
      <div
        v-for="row in invoices"
        :key="row.invoiceId"
        class="invoice-card"
        :class="{ 'invoice-card--active': selected?.invoiceId === row.invoiceId }"
      >
        <div class="invoice-card-header">
          <div class="invoice-code-wrap">
            <span class="invoice-icon">🧾</span>
            <div>
              <h3 class="invoice-code">{{ row.invoiceCode }}</h3>
              <p class="invoice-date">Lập ngày: {{ date(row.createdAt) }}</p>
            </div>
          </div>
          <span class="status-badge" :class="'status-' + row.status">
            {{ statuses[row.status] ?? 'Không xác định' }}
          </span>
        </div>

        <div class="invoice-card-body">
          <div class="finance-grid">
            <div class="finance-item">
              <span class="finance-label">Tổng chi phí</span>
              <span class="finance-val total">{{ money(row.totalAmount) }}</span>
            </div>
            <div class="finance-item">
              <span class="finance-label">Đã thanh toán</span>
              <span class="finance-val paid">{{ money(row.paidAmount) }}</span>
            </div>
            <div class="finance-item">
              <span class="finance-label">Còn nợ</span>
              <span class="finance-val debt" :class="{ 'debt--zero': row.remainingAmount <= 0 }">
                {{ money(row.remainingAmount) }}
              </span>
            </div>
          </div>
        </div>

        <div class="invoice-card-actions">
          <button
            type="button"
            class="button"
            :class="selected?.invoiceId === row.invoiceId ? 'button-secondary' : 'button-outline-primary'"
            :disabled="invoicesBusy"
            @click="selectInvoice(row.invoiceId)"
          >
            <span>{{ selected?.invoiceId === row.invoiceId ? 'Đang xem chi tiết' : 'Xem dịch vụ & đợt trả tiền' }}</span>
            <span aria-hidden="true">→</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Phân trang hóa đơn -->
    <div v-if="invoicesLoaded && invoicePages > 1" class="panel pagination-panel" aria-label="Phân trang hóa đơn">
      <span class="pagination-info">
        Tổng <strong>{{ invoiceTotal }}</strong> hóa đơn · Trang <strong>{{ invoicePage }}</strong>/{{ invoicePages }}
      </span>
      <div class="pagination-buttons">
        <button
          type="button"
          class="button button-sm button-secondary"
          :disabled="invoicesBusy || invoicePage <= 1"
          @click="loadInvoices(invoicePage - 1)"
        >
          ← Trang trước
        </button>
        <button
          type="button"
          class="button button-sm button-secondary"
          :disabled="invoicesBusy || invoicePage >= invoicePages"
          @click="loadInvoices(invoicePage + 1)"
        >
          Trang sau →
        </button>
      </div>
    </div>
    <!-- Khối chi tiết hóa đơn được chọn -->
    <div v-if="selected" class="detail-container">
      <!-- Section: Dòng dịch vụ thực hiện -->
      <section class="panel detail-panel" aria-label="Chi tiết dịch vụ">
        <div class="detail-header">
          <div class="detail-title-wrap">
            <span class="detail-icon">🦷</span>
            <div>
              <h2 class="detail-title">Dịch vụ điều trị · {{ selected.invoiceCode }}</h2>
              <p class="detail-subtitle">Đơn giá và chiết khấu chốt tại thời điểm điều trị</p>
            </div>
          </div>
          <button type="button" class="button button-sm button-secondary" :disabled="itemsBusy" @click="loadItems(1)">
            <span :class="{ 'spin-icon': itemsBusy }">🔄</span>
            <span>Tải lại dịch vụ</span>
          </button>
        </div>

        <div v-if="itemsBusy" class="status-panel-sm" role="status">
          <div class="loading-spinner"></div>
          <p>Đang tải chi tiết dịch vụ…</p>
        </div>
        <div v-else-if="itemError" class="alert alert-error" role="alert">
          <span>⚠️ {{ itemError }}</span>
        </div>
        <div v-else-if="itemsLoaded && !items.length" class="empty-detail">
          <p>Hóa đơn này chưa có dòng dịch vụ nào.</p>
        </div>
        <div v-else class="items-table-wrap">
          <table class="items-table">
            <thead>
              <tr>
                <th>Dịch vụ / Thủ thuật</th>
                <th class="text-center">Số lượng</th>
                <th class="text-right">Đơn giá</th>
                <th class="text-right">Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in items" :key="item.itemId">
                <td>
                  <strong>{{ item.name }}</strong>
                  <span class="code-badge">{{ item.code }}</span>
                </td>
                <td class="text-center">{{ item.quantity }}</td>
                <td class="text-right">{{ money(item.unitPrice) }}</td>
                <td class="text-right"><strong>{{ money(item.totalAmount) }}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="itemsLoaded && itemPages > 1" class="detail-pagination">
          <button type="button" class="btn-text" :disabled="itemsBusy || itemPage <= 1" @click="loadItems(itemPage - 1)">← Trước</button>
          <span>Trang {{ itemPage }}/{{ itemPages }}</span>
          <button type="button" class="btn-text" :disabled="itemsBusy || itemPage >= itemPages" @click="loadItems(itemPage + 1)">Sau →</button>
        </div>
      </section>

      <!-- Section: Lịch sử các lần thanh toán (F_BIL_03) -->
      <section class="panel detail-panel" aria-label="Lịch sử các đợt trả tiền">
        <div class="detail-header">
          <div class="detail-title-wrap">
            <span class="detail-icon">💳</span>
            <div>
              <h2 class="detail-title">Lịch sử thanh toán & Trả góp · {{ selected.invoiceCode }}</h2>
              <p class="detail-subtitle">Ghi nhận các đợt thanh toán tiền mặt và chuyển khoản</p>
            </div>
          </div>
          <button type="button" class="button button-sm button-secondary" :disabled="paymentsBusy" @click="loadPayments(1)">
            <span :class="{ 'spin-icon': paymentsBusy }">🔄</span>
            <span>Tải lại thanh toán</span>
          </button>
        </div>

        <div v-if="paymentsBusy" class="status-panel-sm" role="status">
          <div class="loading-spinner"></div>
          <p>Đang tải lịch sử thanh toán…</p>
        </div>
        <div v-else-if="paymentError" class="alert alert-error" role="alert">
          <span>⚠️ {{ paymentError }}</span>
        </div>
        <div v-else-if="paymentsLoaded && !payments.length" class="empty-detail">
          <p>Chưa có khoản thanh toán nào được ghi nhận cho hóa đơn này.</p>
        </div>
        <div v-else class="payments-list">
          <div v-for="payment in payments" :key="payment.paymentId" class="payment-item">
            <div class="payment-info">
              <span class="payment-id">Đợt #{{ payment.paymentId }}</span>
              <span class="payment-date">{{ date(payment.paidAt) }}</span>
              <span class="method-tag">{{ payment.paymentMethod === 'Cash' ? '💵 Tiền mặt' : '🏦 Chuyển khoản' }}</span>
              <span v-if="payment.isSimulation" class="sim-badge">MÔ PHỎNG</span>
            </div>
            <div class="payment-amount">
              <span class="amount-label">Số tiền ghi nhận:</span>
              <strong class="amount-val">{{ money(payment.amount) }}</strong>
            </div>
          </div>
        </div>

        <div v-if="paymentsLoaded && paymentPages > 1" class="detail-pagination">
          <button type="button" class="btn-text" :disabled="paymentsBusy || paymentPage <= 1" @click="loadPayments(paymentPage - 1)">← Trước</button>
          <span>Trang {{ paymentPage }}/{{ paymentPages }}</span>
          <button type="button" class="btn-text" :disabled="paymentsBusy || paymentPage >= paymentPages" @click="loadPayments(paymentPage + 1)">Sau →</button>
        </div>
      </section>
    </div>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { invoicePortalApi, type PortalPayment, type PortalInvoiceItem, type PortalInvoiceProfile, type PortalInvoiceFilters } from '@/services/invoicePortal';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
const auth = useAuthStore();
const allowed = computed(() => auth.role === 'PATIENT');
const profiles = ref<PortalInvoiceProfile[]>([]);
const patientId = ref(0), profilesBusy = ref(false), profileError = ref('');
const selectedProfile = computed(() => profiles.value.find(p => p.patientId === patientId.value));
const validProfile = computed(() => allowed.value && Number.isInteger(patientId.value) && patientId.value > 0 && !!selectedProfile.value);
const invoices = ref<InvoiceLookupResponse[]>([]), invoicePage = ref(1), invoicePages = ref(0), invoiceTotal = ref(0);
const invoicesBusy = ref(false), invoicesLoaded = ref(false), invoiceError = ref('');
const status = ref(''), fromDate = ref(''), toDate = ref('');
const applied = ref<PortalInvoiceFilters>({});
const selected = ref<InvoiceLookupResponse | null>(null), payments = ref<PortalPayment[]>([]);
const paymentPage = ref(1), paymentPages = ref(0), paymentTotal = ref(0), paymentsBusy = ref(false), paymentsLoaded = ref(false), paymentError = ref('');
const items = ref<PortalInvoiceItem[]>([]), itemPage = ref(1), itemPages = ref(0), itemTotal = ref(0);
const itemsBusy = ref(false), itemsLoaded = ref(false), itemError = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const birth = (value: string) => value.split('-').reverse().join('/');
const date = (value: string) => new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
let profileVersion = 0, invoiceVersion = 0, paymentVersion = 0, itemVersion = 0;
function clearItems() { itemVersion++; items.value = []; itemsBusy.value = false; itemsLoaded.value = false; itemPage.value = 1; itemPages.value = 0; itemTotal.value = 0; itemError.value = ''; }
function clearPayments() { clearItems(); paymentVersion++; selected.value = null; payments.value = []; paymentsBusy.value = false; paymentsLoaded.value = false; paymentTotal.value = 0; paymentPages.value = 0; paymentPage.value = 1; paymentError.value = ''; }
function clearInvoices() { invoiceVersion++; invoices.value = []; invoicesBusy.value = false; invoicesLoaded.value = false; invoiceTotal.value = 0; invoicePages.value = 0; invoicePage.value = 1; invoiceError.value = ''; clearPayments(); }
function clearFilters() { status.value = ''; fromDate.value = ''; toDate.value = ''; applied.value = {}; }
function selectProfile() { clearFilters(); clearInvoices(); if (validProfile.value) void loadInvoices(1); }
function validDate(value: string) {
  if (!value) return true;
  if (!/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(value) || value < '0001-01-01') return false;
  try { return new Date(value + 'T00:00:00Z').toISOString().slice(0, 10) === value; } catch { return false; }
}
async function searchInvoices() {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value) return;
  clearInvoices();
  if (!['', '0', '1', '2', '3', '4'].includes(status.value)) { invoiceError.value = 'Chọn trạng thái hóa đơn hợp lệ.'; return; }
  if (!validDate(fromDate.value) || !validDate(toDate.value) ||
      (fromDate.value && fromDate.value < '0001-01-02') || (toDate.value && toDate.value > '9999-12-30') ||
      (fromDate.value && toDate.value && fromDate.value > toDate.value)) {
    invoiceError.value = 'Nhập khoảng ngày lập hóa đơn hợp lệ; từ ngày không sau đến ngày.'; return;
  }
  applied.value = { ...(status.value !== '' ? { status: Number(status.value) } : {}),
    ...(fromDate.value ? { fromDate: fromDate.value } : {}), ...(toDate.value ? { toDate: toDate.value } : {}) };
  await loadInvoices(1);
}
async function resetInvoiceFilters() {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value) return;
  clearFilters(); await loadInvoices(1);
}
async function loadProfiles() { clearFilters();
  const ticket = ++profileVersion; profiles.value = []; patientId.value = 0; profileError.value = ''; clearInvoices();
  if (!allowed.value) { profilesBusy.value = false; return; }
  profilesBusy.value = true;
  try {
    const rows = await invoicePortalApi.profiles();
    if (ticket === profileVersion && allowed.value) {
      if (rows.length > 0 && new Set(rows.map(p => p.patientId)).size !== rows.length)
        profileError.value = 'Danh sách hồ sơ không hợp lệ. Vui lòng tải lại.';
      else if (rows.length > 0) {
        profiles.value = rows.map(p => ({ patientId: p.patientId, patientCode: p.patientCode, fullName: p.fullName, dateOfBirth: p.dateOfBirth, isActive: p.isActive }));
        patientId.value = rows[0].patientId;
        void loadInvoices(1);
      }
    }
  } catch { if (ticket === profileVersion) profileError.value = 'Không tải được hồ sơ. Vui lòng tải lại.'; }
  finally { if (ticket === profileVersion) profilesBusy.value = false; }
}
async function loadInvoices(target = 1) {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!invoicesLoaded.value || target > invoicePages.value))) return;
  const requestedPatient = patientId.value; const ticket = ++invoiceVersion;
  clearPayments(); invoices.value = []; invoicesLoaded.value = false; invoicesBusy.value = true; invoiceError.value = '';
  try {
    const result = await invoicePortalApi.list(requestedPatient, target, { ...applied.value });
    if (ticket !== invoiceVersion || !allowed.value) return;
    invoices.value = result.items; invoicePage.value = result.page; invoicePages.value = result.totalPages; invoiceTotal.value = result.totalCount; invoicesLoaded.value = true;
  } catch { if (ticket === invoiceVersion) { invoiceTotal.value = 0; invoicePages.value = 0; invoiceError.value = 'Không tải được hóa đơn của hồ sơ đã chọn. Vui lòng thử lại.'; } }
  finally { if (ticket === invoiceVersion) invoicesBusy.value = false; }
}
async function selectInvoice(id: number) {
  if (!validProfile.value || invoicesBusy.value || !invoicesLoaded.value) return;
  const row = invoices.value.find(i => i.invoiceId === id); if (!row || ((paymentsBusy.value || itemsBusy.value) && selected.value?.invoiceId === id)) return;
  clearPayments(); selected.value = row; await Promise.all([loadItems(1), loadPayments(1)]);
}
async function loadItems(target = 1) {
  if (!validProfile.value || !selected.value || itemsBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!itemsLoaded.value || target > itemPages.value))) return;
  const ticket = ++itemVersion; const invoiceId = selected.value.invoiceId;
  items.value = []; itemsLoaded.value = false; itemsBusy.value = true; itemError.value = '';
  try {
    const result = await invoicePortalApi.items(patientId.value, invoiceId, target);
    if (ticket !== itemVersion || !allowed.value) return;
    items.value = result.items; itemPage.value = result.page; itemPages.value = result.totalPages; itemTotal.value = result.totalCount; itemsLoaded.value = true;
  } catch { if (ticket === itemVersion) { itemTotal.value = 0; itemPages.value = 0; itemError.value = 'Không tải được chi tiết hóa đơn. Chọn lại hóa đơn hoặc thử lại.'; } }
  finally { if (ticket === itemVersion) itemsBusy.value = false; }
}
async function loadPayments(target = 1) {
  if (!validProfile.value || !selected.value || paymentsBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!paymentsLoaded.value || target > paymentPages.value))) return;
  const ticket = ++paymentVersion; const invoiceId = selected.value.invoiceId;
  payments.value = []; paymentsLoaded.value = false; paymentsBusy.value = true; paymentError.value = '';
  try {
    const result = await invoicePortalApi.payments(patientId.value, invoiceId, target);
    if (ticket !== paymentVersion || !allowed.value) return;
    payments.value = result.items; paymentPage.value = result.page; paymentPages.value = result.totalPages; paymentTotal.value = result.totalCount; paymentsLoaded.value = true;
  } catch { if (ticket === paymentVersion) { paymentTotal.value = 0; paymentPages.value = 0; paymentError.value = 'Không tải được các lần thanh toán. Chọn lại hóa đơn hoặc thử lại.'; } }
  finally { if (ticket === paymentVersion) paymentsBusy.value = false; }
}
watch(() => [auth.role, auth.user?.userId], loadProfiles);
onMounted(loadProfiles);
onUnmounted(() => { profileVersion++; profiles.value = []; patientId.value = 0; clearFilters(); clearInvoices(); });
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

/* Profile picker */
.profile-picker-panel {
  padding: 1rem 1.25rem;
  background: #f8fafc;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.picker-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.picker-label {
  font-size: 0.85rem;
  font-weight: 750;
  color: #0f2d59;
}

.profile-select {
  max-width: 320px;
}

.selected-profile-tag {
  font-size: 0.8rem;
  color: #475569;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.inactive-badge {
  background: #fee2e2;
  color: #991b1b;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  font-size: 0.725rem;
}

/* Filter panel */
.filter-panel {
  padding: 1.25rem;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.form-label {
  font-size: 0.825rem;
  font-weight: 700;
  color: #334155;
}

.form-input,
.form-select {
  width: 100%;
  box-sizing: border-box;
  height: 38px;
  padding: 0 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font: inherit;
  font-size: 0.85rem;
  color: #1e293b;
  background: #ffffff;
}

.form-input:focus,
.form-select:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.filter-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}

.applied-filter-bar {
  font-size: 0.825rem;
  color: #64748b;
  background: #f1f5f9;
  padding: 0.6rem 0.85rem;
  border-radius: 6px;
}

/* Invoices list */
.invoices-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.invoice-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  transition: all 0.15s ease;
}

.invoice-card:hover {
  border-color: #cbd5e1;
  box-shadow: 0 4px 12px rgba(15, 45, 89, 0.05);
}

.invoice-card--active {
  border-color: #0284c7;
  background: #f0f9ff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.2);
}

.invoice-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  flex-wrap: wrap;
}

.invoice-code-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.invoice-icon {
  font-size: 1.35rem;
}

.invoice-code {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 850;
  color: #0f2d59;
}

.invoice-date {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #64748b;
}

.status-badge {
  font-size: 0.775rem;
  font-weight: 750;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
}

.status-0 { background: #f1f5f9; color: #475569; }
.status-1 { background: #fef3c7; color: #92400e; border: 1px solid #fde68a; }
.status-2 { background: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd; }
.status-3 { background: #dcfce7; color: #166534; border: 1px solid #bbf7d0; }
.status-4 { background: #fee2e2; color: #991b1b; border: 1px solid #fecaca; }

.finance-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1rem;
  background: #f8fafc;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
}

.finance-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.finance-label {
  font-size: 0.75rem;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-weight: 700;
}

.finance-val {
  font-size: 1.1rem;
  font-weight: 800;
  color: #1e293b;
}

.finance-val.total { color: #0f2d59; }
.finance-val.paid { color: #166534; }
.finance-val.debt { color: #dc2626; }
.finance-val.debt--zero { color: #64748b; }

.invoice-card-actions {
  display: flex;
  justify-content: flex-end;
}
/* Detail container */
.detail-container {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  margin-top: 1rem;
}

.detail-panel {
  padding: 1.5rem;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
  gap: 1rem;
  flex-wrap: wrap;
}

.detail-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.detail-icon {
  font-size: 1.35rem;
}

.detail-title {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.detail-subtitle {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #64748b;
}

/* Tables */
.items-table-wrap {
  overflow-x: auto;
}

.items-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.865rem;
}

.items-table th {
  background: #f8fafc;
  color: #475569;
  font-weight: 750;
  padding: 0.75rem 0.85rem;
  border-bottom: 1px solid #e2e8f0;
  text-align: left;
}

.items-table td {
  padding: 0.85rem;
  border-bottom: 1px solid #f1f5f9;
  color: #1e293b;
}

.text-center { text-align: center; }
.text-right { text-align: right; }

.code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.725rem;
  font-weight: 700;
  background: #e2e8f0;
  color: #334155;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  margin-left: 0.4rem;
}

/* Payments list */
.payments-list {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.payment-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.85rem 1rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  gap: 1rem;
  flex-wrap: wrap;
}

.payment-info {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  flex-wrap: wrap;
  font-size: 0.85rem;
}

.payment-id {
  font-weight: 750;
  color: #0f2d59;
}

.payment-date {
  color: #64748b;
}

.method-tag {
  font-weight: 700;
  color: #334155;
}

.sim-badge {
  background: #fef3c7;
  color: #92400e;
  font-size: 0.725rem;
  font-weight: 750;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.payment-amount {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.amount-label {
  font-size: 0.8rem;
  color: #64748b;
}

.amount-val {
  font-size: 1rem;
  color: #166534;
}

.detail-pagination {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  font-size: 0.825rem;
  color: #64748b;
}

/* Pagination panel */
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

.button-outline {
  background: #ffffff;
  color: #475569;
  border-color: #cbd5e1;
}

.button-outline:hover:not(:disabled) {
  background: #f8fafc;
}

.button-outline-primary {
  background: #f0f9ff;
  color: #0284c7;
  border-color: #bae6fd;
}

.button-outline-primary:hover:not(:disabled) {
  background: #e0f2fe;
}

.button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-text {
  background: transparent;
  border: none;
  color: #0284c7;
  font-weight: 700;
  cursor: pointer;
  padding: 0;
}

.btn-text:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Alerts & status */
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

.alert-info {
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  color: #0369a1;
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

.empty-panel p,
.empty-detail p {
  margin: 0;
  font-size: 0.85rem;
  color: #64748b;
}

.status-panel-sm {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 1.5rem 0;
  color: #64748b;
  font-size: 0.85rem;
}

.loading-spinner {
  width: 24px;
  height: 24px;
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
</style>
