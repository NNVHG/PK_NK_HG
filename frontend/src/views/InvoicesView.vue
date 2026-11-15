<template>
  <main class="invoice-desk-page">
    <header class="invoice-desk-header">
      <div class="header-badge-row">
        <span class="app-badge app-badge--info">Quầy Thu Ngân · Tài Chính</span>
      </div>
      <h1 class="invoice-desk-title">Tra Cứu &amp; Thu Hóa Đơn Điều Trị</h1>
      <p class="invoice-desk-desc">
        Tra cứu mã hóa đơn, đối chiếu số tiền đã thanh toán và dư nợ còn lại, ghi nhận thu tiền mặt hoặc chuyển khoản và in biên lai thu ngân.
      </p>
    </header>

    <div class="invoice-desk-grid">
      <!-- Cột 1: Form tra cứu & Lịch sử thao tác -->
      <div class="desk-col-left">
        <article class="app-card">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">💳</span>
              <div>
                <h2 class="app-card-title">Tra Cứu Hóa Đơn</h2>
                <p class="app-card-subtitle">Nhập mã hóa đơn (ví dụ: HD-202611-001)</p>
              </div>
            </div>
          </div>

          <div class="app-card-body">
            <form class="lookup-form" @submit.prevent="lookup">
              <div class="app-form-group">
                <label class="app-label" for="invoice-code-input">
                  <span>Mã hóa đơn thanh toán</span>
                  <span class="app-label-required">*</span>
                </label>
                <div class="search-input-row">
                  <input
                    id="invoice-code-input"
                    v-model="code"
                    class="app-input search-input"
                    maxlength="50"
                    autocomplete="off"
                    autocorrect="off"
                    autocapitalize="characters"
                    spellcheck="false"
                    enterkeyhint="search"
                    placeholder="Nhập mã hóa đơn..."
                    :disabled="busy || paymentOpen"
                  />
                  <button
                    type="submit"
                    class="app-btn app-btn--primary"
                    :disabled="busy || paymentOpen || !allowed"
                  >
                    {{ busy ? 'Đang tìm…' : 'Tra cứu' }}
                  </button>
                </div>
              </div>
            </form>

            <p v-if="busy" class="app-hint" role="status">Đang đối chiếu dữ liệu hóa đơn…</p>
            <p v-if="error" class="error-banner" role="alert">{{ error }}</p>

            <div v-if="!paymentOpen" class="history-block">
              <h3 class="history-title">Hóa đơn thao tác gần đây</h3>
              <InvoiceHistoryPanel :disabled="busy" @selected="selectFromHistory" />
            </div>
          </div>
        </article>
      </div>

      <!-- Cột 2: Chi tiết hóa đơn & Màn hình thu tiền -->
      <div class="desk-col-right">
        <article v-if="invoice" class="app-card invoice-detail-card" aria-label="Thông tin đối chiếu">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">🧾</span>
              <div>
                <h2 class="app-card-title">{{ invoice.invoiceCode }}</h2>
                <p class="app-card-subtitle">Bệnh nhân: <strong>{{ invoice.patientName }}</strong> ({{ invoice.patientCode }})</p>
              </div>
            </div>
            <span
              class="app-badge"
              :class="{
                'app-badge--success': invoice.status === 3,
                'app-badge--warning': invoice.status === 2,
                'app-badge--danger': invoice.status === 4,
                'app-badge--info': [0, 1].includes(invoice.status)
              }"
            >
              {{ statuses[invoice.status] ?? 'Không xác định' }}
            </span>
          </div>

          <div class="app-card-body">
            <!-- Thống kê tài chính 3 cột -->
            <div class="financial-summary-grid">
              <div class="summary-box">
                <span class="summary-label">TỔNG CHI PHÍ</span>
                <strong class="summary-val">{{ money(invoice.totalAmount) }}</strong>
              </div>
              <div class="summary-box summary-box--paid">
                <span class="summary-label">ĐÃ THANH TOÁN</span>
                <strong class="summary-val">{{ money(invoice.paidAmount) }}</strong>
              </div>
              <div class="summary-box summary-box--debt">
                <span class="summary-label">DƯ NỢ CÒN LẠI</span>
                <strong class="summary-val">{{ money(invoice.remainingAmount) }}</strong>
              </div>
            </div>

            <div class="invoice-meta-info">
              <span>📅 Ngày tạo: <strong>{{ new Date(invoice.createdAt).toLocaleString('vi-VN') }}</strong></span>
            </div>

            <!-- Khối xác nhận đối chiếu trước khi mở cổng thu tiền -->
            <div v-if="!paymentOpen && eligible" class="confirmation-section">
              <label class="confirm-checkbox-card">
                <input v-model="confirmed" type="checkbox" class="confirm-input" />
                <span class="confirm-text">Tôi đã đối chiếu khớp thông tin mã hóa đơn, họ tên bệnh nhân và số dư nợ</span>
              </label>
              <button
                class="app-btn app-btn--gold app-btn--lg open-payment-btn"
                :disabled="!confirmed || busy"
                @click="openPayment"
              >
                💰 Mở Cổng Thu Tiền / Xuất Biên Lai
              </button>
            </div>

            <p v-else-if="!eligible" class="app-hint">
              Hóa đơn nháp hoặc đã hủy không thể mở tiếp nhận thanh toán.
            </p>

            <!-- Cổng thu tiền tiền mặt / QR khi đã mở -->
            <div v-if="paymentOpen" class="payment-processing-area">
              <div class="processing-notice-box">
                <p>Đang trong phiên thu tiền cho hóa đơn <strong>{{ invoice.invoiceCode }}</strong>. Vui lòng hoàn tất lần thu hiện tại trước khi chuyển sang hóa đơn khác.</p>
                <button type="button" class="app-btn app-btn--secondary app-btn--sm" :disabled="busy" @click="refresh">
                  🔄 Đối chiếu lại công nợ
                </button>
              </div>

              <InvoiceCashPayment
                :invoice-id="invoice.invoiceId"
                :remaining-amount="reviewRequired ? 0 : invoice.remainingAmount"
                :status="reviewRequired ? 0 : invoice.status"
                @paid="onPaid"
              />

              <div v-if="lastPayment" class="receipt-success-card" aria-label="Biên lai lần thu vừa ghi nhận">
                <div class="receipt-success-header">
                  <span class="app-badge app-badge--success">✓ Giao dịch thành công</span>
                  <span class="receipt-id">Mã giao dịch #{{ lastPayment.id }}</span>
                </div>
                <div class="receipt-success-amounts">
                  <div>Số tiền thực thu: <strong>{{ money(lastPayment.amount) }}</strong></div>
                  <div v-if="lastPayment.changeAmount && lastPayment.changeAmount > 0">Tiền thối lại khách: <strong>{{ money(lastPayment.changeAmount) }}</strong></div>
                </div>
                <PaymentReceiptModal :invoice-id="invoice.invoiceId" :payment-id="lastPayment.id" />
              </div>
            </div>
          </div>
        </article>

        <!-- Placeholder khi chưa tra cứu -->
        <article v-else class="app-card empty-invoice-placeholder">
          <div class="empty-icon">🧾</div>
          <h3>Chưa Chọn Hóa Đơn</h3>
          <p>Nhập mã hóa đơn hoặc bấm vào một mã từ lịch sử thao tác để xem chi tiết đối soát công nợ.</p>
        </article>
      </div>
    </div>
  </main>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { invoiceLookupApi, type InvoiceLookupResponse } from '@/services/invoiceLookup';
import InvoiceCashPayment from '@/components/InvoiceCashPayment.vue';
import PaymentReceiptModal from '@/components/PaymentReceiptModal.vue';
import type { CashPaymentResponse } from '@/services/cashPayments';
import InvoiceHistoryPanel from '@/components/InvoiceHistoryPanel.vue';
const lastPayment = ref<CashPaymentResponse | null>(null);
const auth = useAuthStore();
const allowed = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role));
const code = ref('');
const invoice = ref<InvoiceLookupResponse | null>(null);
const confirmed = ref(false);
const paymentOpen = ref(false);
const reviewRequired = ref(false);
const busy = ref(false);
const error = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const eligible = computed(() => allowed.value && !!invoice.value && [1, 2, 3].includes(invoice.value.status));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
async function selectFromHistory(selectedCode: string) {
  if (!allowed.value || busy.value || paymentOpen.value) return;
  code.value = selectedCode;
  await lookup();
}
async function lookup() {
  if (!allowed.value || busy.value || paymentOpen.value) return;
  invoice.value = null; confirmed.value = false; error.value = '';
  const normalized = code.value.trim().toUpperCase();
  if (!/^[A-Z0-9-]{1,50}$/.test(normalized)) { error.value = 'Nhập đúng mã hóa đơn, tối đa 50 chữ/số hoặc dấu gạch nối.'; return; }
  code.value = normalized;
  const ticket = ++version; busy.value = true;
  try { const row = await invoiceLookupApi.get(normalized); if (ticket === version) invoice.value = row; }
  catch (cause) {
    if (ticket === version) error.value = axios.isAxiosError(cause) && cause.response?.status === 404
      ? 'Không tìm thấy đúng mã hóa đơn này.' : 'Không thể tra cứu hóa đơn. Thử lại để đối chiếu.';
  } finally { if (ticket === version) busy.value = false; }
}
function openPayment() {
  if (!eligible.value || !confirmed.value || busy.value) return;
  paymentOpen.value = true;
}
async function onPaid(payment: CashPaymentResponse) {
  if (!allowed.value || !invoice.value || payment.invoiceId !== invoice.value.invoiceId) return;
  lastPayment.value = payment;
  version++; busy.value = false;
  await refresh();
}
async function refresh() {
  if (!allowed.value || !paymentOpen.value || !invoice.value || busy.value) return;
  const original = invoice.value; const ticket = ++version; busy.value = true; reviewRequired.value = true; error.value = '';
  try {
    const row = await invoiceLookupApi.get(original.invoiceCode);
    if (ticket !== version) return;
    if (row.invoiceId !== original.invoiceId) throw new Error('Invoice identity changed');
    invoice.value = row; reviewRequired.value = false;
  } catch { if (ticket === version) error.value = 'Chưa đối chiếu được công nợ mới. Không nhận thêm tiền; đối chiếu lại hóa đơn.'; }
  finally { if (ticket === version) busy.value = false; }
}
onUnmounted(() => { version++; lastPayment.value = null; invoice.value = null; code.value = ''; confirmed.value = false; paymentOpen.value = false; });
</script>
<style scoped>
.invoice-desk-page {
  max-width: 1380px;
  margin: 0 auto;
  padding: var(--spacing-6);
  box-sizing: border-box;
}

.invoice-desk-header {
  margin-block-end: var(--spacing-6);
  padding-block-end: var(--spacing-4);
  border-block-end: 1px solid var(--color-border-subtle);
}

.header-badge-row {
  margin-block-end: 0.35rem;
}

.invoice-desk-title {
  font-size: var(--font-size-2xl);
  font-weight: 850;
  color: var(--color-primary);
  margin: 0 0 0.35rem;
  letter-spacing: -0.01em;
}

.invoice-desk-desc {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin: 0;
  max-width: 75ch;
}

.invoice-desk-grid {
  display: grid;
  grid-template-columns: 420px 1fr;
  gap: var(--spacing-6);
  align-items: start;
}

.desk-col-left,
.desk-col-right {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-6);
}

.search-input-row {
  display: flex;
  gap: var(--spacing-2);
}

.search-input {
  flex: 1;
}

.history-block {
  margin-block-start: var(--spacing-4);
  border-block-start: 1px solid var(--color-border-subtle);
  padding-block-start: var(--spacing-4);
}

.history-title {
  font-size: var(--font-size-sm);
  font-weight: 800;
  color: var(--color-primary);
  margin: 0 0 var(--spacing-3);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

/* Thống kê tài chính 3 khối */
.financial-summary-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--spacing-3);
  margin-block: var(--spacing-3);
}

.summary-box {
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  padding: var(--spacing-3);
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.summary-label {
  font-size: 0.7rem;
  font-weight: 800;
  color: var(--color-text-subtle);
  letter-spacing: 0.05em;
}

.summary-val {
  font-size: var(--font-size-lg);
  font-weight: 850;
  color: var(--color-primary);
}

.summary-box--paid .summary-val {
  color: var(--color-success);
}

.summary-box--debt .summary-val {
  color: var(--color-danger);
}

.invoice-meta-info {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
  padding-block-end: var(--spacing-3);
  border-block-end: 1px solid var(--color-border-subtle);
}

/* Khối checkbox xác nhận đối chiếu */
.confirmation-section {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-4);
  margin-block-start: var(--spacing-4);
}

.confirm-checkbox-card {
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  padding: var(--spacing-3) var(--spacing-4);
  background: #f8fafc;
  border: 1px solid var(--color-border-medium);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: all 0.15s ease;
}

.confirm-checkbox-card:hover {
  background: #f1f5f9;
}

.confirm-input {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: var(--color-medical);
}

.confirm-text {
  font-size: var(--font-size-sm);
  font-weight: 700;
  color: var(--color-text-main);
}

.open-payment-btn {
  inline-size: 100%;
  min-block-size: 48px;
  font-size: var(--font-size-base);
}

/* Khu vực xử lý thanh toán */
.payment-processing-area {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-4);
  margin-block-start: var(--spacing-4);
}

.processing-notice-box {
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-3) var(--spacing-4);
  font-size: var(--font-size-xs);
  color: #9a3412;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--spacing-3);
  flex-wrap: wrap;
}

.receipt-success-card {
  background: var(--color-success-light);
  border: 1px solid var(--color-success-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-4);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-3);
}

.receipt-success-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.receipt-id {
  font-size: var(--font-size-xs);
  font-weight: 700;
  color: #166534;
}

.receipt-success-amounts {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: var(--font-size-sm);
  color: #14532d;
}

.empty-invoice-placeholder {
  text-align: center;
  padding: 5rem 2rem;
  color: var(--color-text-subtle);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-2);
}

.empty-icon {
  font-size: 3.5rem;
}

.empty-invoice-placeholder h3 {
  margin: 0;
  color: var(--color-primary);
  font-size: var(--font-size-lg);
}

.empty-invoice-placeholder p {
  margin: 0;
  font-size: var(--font-size-sm);
  max-width: 45ch;
}

.error-banner {
  padding: var(--spacing-2) var(--spacing-3);
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  border-radius: var(--radius-sm);
  font-size: var(--font-size-xs);
  color: #991b1b;
  margin: 0.25rem 0;
}

@media (max-width: 900px) {
  .invoice-desk-grid {
    grid-template-columns: 1fr;
  }
  .financial-summary-grid {
    grid-template-columns: 1fr;
  }
}
</style>
