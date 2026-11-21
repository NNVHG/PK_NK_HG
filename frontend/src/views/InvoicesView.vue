<template>
  <section class="invoice-desk" aria-labelledby="invoice-desk-title">
    <header class="desk-heading">
      <h1 id="invoice-desk-title">Tra cứu &amp; thu hóa đơn điều trị</h1>
      <p>Tìm hóa đơn bên cạnh, mở chi tiết trong hộp thoại nổi để đối chiếu công nợ và in biên lai.</p>
    </header>

    <InvoiceHistoryPanel
      ref="historyPanel"
      :disabled="busy || paymentOpen || !!invoice"
      @selected="selectFromHistory"
    >
      <template #before-filters>
        <section class="desk-panel quick-lookup-panel" aria-labelledby="lookup-title">
          <h2 id="lookup-title">Tra cứu nhanh theo mã</h2>
          <form @submit.prevent="lookup()">
            <label for="invoice-code-input">Mã hóa đơn</label>
            <div class="lookup-controls">
              <input
                id="invoice-code-input"
                ref="invoiceCodeInput"
                v-model="code"
                class="app-input"
                maxlength="50"
                autocomplete="off"
                autocapitalize="characters"
                spellcheck="false"
                enterkeyhint="search"
                placeholder="Nhập mã trên hóa đơn"
                :disabled="busy || paymentOpen || !!invoice"
              />
              <button
                type="submit"
                class="app-btn app-btn--primary"
                :disabled="busy || paymentOpen || !!invoice || !allowed"
              >
                {{ busy && !invoice && lookupSource === 'manual' ? 'Đang tìm…' : 'Tra cứu' }}
              </button>
            </div>
          </form>
          <p v-if="error && !invoice && lookupSource === 'manual'" class="desk-error" role="alert">{{ error }}</p>
        </section>
      </template>

      <template #results-banner>
        <p v-if="busy && !invoice && lookupSource === 'history'" class="desk-status" role="status">
          Đang tải chi tiết hóa đơn…
        </p>
        <p v-else-if="error && !invoice && lookupSource === 'history'" class="desk-error" role="alert">
          {{ error }}
        </p>
      </template>
    </InvoiceHistoryPanel>

    <dialog
      v-if="invoice"
      :ref="bindDetailDialog"
      class="invoice-detail-dialog"
      aria-labelledby="invoice-reference"
      @cancel="onDetailDialogCancel"
    >
      <div class="detail-modal-shell">
        <header class="detail-modal-header">
          <div class="detail-modal-title-group">
            <p class="desk-muted">Hóa đơn {{ invoice.invoiceCode }}</p>
            <h2 id="invoice-reference">{{ invoice.patientName }}</h2>
            <p class="desk-muted">{{ invoice.patientCode }} · {{ dateTime.format(new Date(invoice.createdAt)) }}</p>
          </div>
          <div class="detail-modal-header-actions">
            <span class="app-badge" :class="statusBadgeClass(invoice.status)">
              {{ statuses[invoice.status] ?? 'Không xác định' }}
            </span>
            <button
              type="button"
              class="detail-close-btn"
              aria-label="Đóng chi tiết hóa đơn"
              :disabled="!canCloseDetail"
              @click="closeDetail"
            >
              ✕
            </button>
          </div>
        </header>

        <div class="detail-modal-body">
          <dl class="invoice-amounts">
            <div class="outstanding">
              <dt>Phải thu còn lại</dt>
              <dd>{{ reviewRequired ? (busy ? 'Đang đối chiếu' : 'Cần đối chiếu lại') : money(invoice.remainingAmount) }}</dd>
            </div>
            <div>
              <dt>Tổng hóa đơn</dt>
              <dd>{{ money(invoice.totalAmount) }}</dd>
            </div>
            <div>
              <dt>Đã thanh toán</dt>
              <dd>{{ money(invoice.paidAmount) }}</dd>
            </div>
          </dl>

          <template v-if="!paymentOpen">
            <div v-if="eligible" class="payment-confirmation">
              <label>
                <input v-model="confirmed" type="checkbox" />
                <span>Tôi đã đối chiếu mã hóa đơn, tên bệnh nhân và số tiền còn phải thu.</span>
              </label>
              <div class="detail-footer-actions">
                <button
                  type="button"
                  class="app-btn app-btn--secondary"
                  :disabled="!canCloseDetail"
                  @click="closeDetail"
                >
                  Đóng
                </button>
                <button
                  type="button"
                  class="app-btn app-btn--primary"
                  :disabled="!confirmed || busy"
                  @click="openPayment"
                >
                  {{ invoice.remainingAmount > 0 ? 'Mở thu tiền và biên lai' : 'Xem lịch sử thu và biên lai' }}
                </button>
              </div>
            </div>
            <div v-else class="detail-ineligible">
              <p class="desk-muted">Hóa đơn này chưa thể mở phần thu tiền.</p>
              <div class="detail-footer-actions">
                <button
                  type="button"
                  class="app-btn app-btn--secondary"
                  :disabled="!canCloseDetail"
                  @click="closeDetail"
                >
                  Đóng
                </button>
              </div>
            </div>
          </template>

          <section v-else class="payment-panel" aria-labelledby="payment-title">
            <header class="payment-heading">
              <h3 id="payment-title" ref="paymentHeading" tabindex="-1">Thu tiền và biên lai</h3>
              <div class="payment-actions">
                <button type="button" class="app-btn app-btn--secondary" :disabled="busy" @click="refresh">
                  Đối chiếu lại công nợ
                </button>
                <button
                  type="button"
                  class="app-btn app-btn--secondary"
                  :disabled="busy || reviewRequired || !paymentPanel?.canLeave"
                  @click="startNewLookup"
                >
                  Đóng &amp; tra hóa đơn khác
                </button>
              </div>
            </header>

            <p v-if="!canCloseDetail" class="desk-notice">
              Đang xử lý hoặc cần đối chiếu lại công nợ trước khi đóng hộp thoại.
            </p>
            <p v-if="error" class="desk-error" role="alert">{{ error }}</p>
            <div v-if="lastPayment" class="receipt-summary" aria-label="Biên lai lần thu vừa ghi nhận">
              <strong>Đã ghi nhận giao dịch #{{ lastPayment.id }}</strong>
              <span>Thực thu {{ money(lastPayment.amount) }}</span>
              <span v-if="lastPayment.changeAmount && lastPayment.changeAmount > 0">
                Tiền thối {{ money(lastPayment.changeAmount) }}
              </span>
              <PaymentReceiptModal :invoice-id="invoice.invoiceId" :payment-id="lastPayment.id" />
            </div>
            <InvoiceCashPayment
              ref="paymentPanel"
              :invoice-id="invoice.invoiceId"
              :remaining-amount="reviewRequired ? 0 : invoice.remainingAmount"
              :status="reviewRequired ? 0 : invoice.status"
              @paid="onPaid"
            />
          </section>
        </div>
      </div>
    </dialog>
  </section>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref } from 'vue';
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
const lookupSource = ref<'manual' | 'history'>('manual');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy', 'Đã hoàn một phần', 'Đã hoàn toàn bộ'];
const invoiceCodeInput = ref<HTMLInputElement | null>(null);
const paymentPanel = ref<InstanceType<typeof InvoiceCashPayment> | null>(null);
const paymentHeading = ref<HTMLHeadingElement | null>(null);
const historyPanel = ref<{ refresh: () => Promise<void> } | null>(null);
let detailDialogEl: HTMLDialogElement | null = null;
let paymentRecorded = false;
const dateTime = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh'
});
const eligible = computed(() => allowed.value && !!invoice.value && [1, 2, 3, 5, 6].includes(invoice.value.status));
const canCloseDetail = computed(() => !busy.value && (!paymentOpen.value || (!reviewRequired.value && !!paymentPanel.value?.canLeave)));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
function statusBadgeClass(value: number) {
  if (value === 3) return 'app-badge--success';
  if (value === 2 || value === 5) return 'app-badge--warning';
  if (value === 4) return 'app-badge--danger';
  return 'app-badge--info';
}
function bindDetailDialog(element: unknown) {
  if (typeof HTMLDialogElement !== 'undefined' && element instanceof HTMLDialogElement) {
    detailDialogEl = element;
    if (!element.open) element.showModal();
  } else if (element === null) {
    detailDialogEl = null;
  }
}
async function selectFromHistory(selectedCode: string) {
  if (!allowed.value || busy.value || paymentOpen.value) return;
  code.value = selectedCode;
  await lookup('history');
}
async function lookup(source: 'manual' | 'history' = 'manual') {
  if (!allowed.value || busy.value || paymentOpen.value) return;
  const activeSource = source === 'history' ? 'history' : 'manual';
  lookupSource.value = activeSource;
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
  void nextTick(() => paymentHeading.value?.focus());
}
function refreshHistoryIfNeeded() {
  if (!paymentRecorded) return;
  paymentRecorded = false;
  void nextTick(() => {
    void historyPanel.value?.refresh();
  });
}
function closeDetail() {
  if (paymentOpen.value) {
    startNewLookup();
    return;
  }
  if (busy.value) return;
  detailDialogEl?.close();
  confirmed.value = false;
  invoice.value = null;
  lastPayment.value = null;
  code.value = '';
  error.value = '';
  refreshHistoryIfNeeded();
  if (lookupSource.value === 'manual') {
    void nextTick(() => invoiceCodeInput.value?.focus());
  }
}
function startNewLookup() {
  if (busy.value || reviewRequired.value || !paymentPanel.value?.canLeave) return;
  detailDialogEl?.close();
  paymentOpen.value = false;
  confirmed.value = false;
  invoice.value = null;
  lastPayment.value = null;
  code.value = '';
  error.value = '';
  refreshHistoryIfNeeded();
  if (lookupSource.value === 'manual') {
    void nextTick(() => invoiceCodeInput.value?.focus());
  }
}
function onDetailDialogCancel(event: Event) {
  if (event.target !== detailDialogEl) return;
  event.preventDefault();
  closeDetail();
}
async function onPaid(payment: CashPaymentResponse) {
  if (!allowed.value || !invoice.value || payment.invoiceId !== invoice.value.invoiceId) return;
  lastPayment.value = payment;
  paymentRecorded = true;
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
onUnmounted(() => {
  version++;
  detailDialogEl?.close();
  detailDialogEl = null;
  paymentRecorded = false;
  lastPayment.value = null;
  invoice.value = null;
  code.value = '';
  confirmed.value = false;
  paymentOpen.value = false;
  reviewRequired.value = false;
  error.value = '';
});
</script>
<style scoped>
.invoice-desk { container-type: inline-size; color: #172b42; }
.desk-heading { margin-bottom: 1.2rem; }
.desk-heading h1 { margin: 0; font-size: clamp(1.45rem, 3vw, 2.1rem); line-height: 1.2; }
.desk-heading p { margin: 0.45rem 0 0; color: #51647a; }

.desk-panel {
  display: grid;
  gap: 0.85rem;
  min-width: 0;
  padding: clamp(0.9rem, 2.2cqw, 1.25rem);
  border: 1px solid #dbe5ed;
  border-radius: 14px;
  background: #fff;
}

.desk-panel h2,
.desk-panel p { margin: 0; }
.desk-panel h2 { font-size: 1.12rem; line-height: 1.3; color: #102e56; }
.desk-muted { color: #51647a; font-size: 0.88rem; }
.quick-lookup-panel form { display: grid; gap: 0.45rem; }
.quick-lookup-panel label { font-size: 0.86rem; font-weight: 650; color: #243b53; }
.lookup-controls { display: flex; flex-wrap: wrap; gap: 0.45rem; }
.lookup-controls input { flex: 1 1 9rem; min-width: 0; max-width: 100%; min-height: 44px; }
.desk-panel .app-btn { min-height: 44px; white-space: normal; text-align: center; }

.desk-status {
  padding: 0.7rem 0.9rem;
  border: 1px solid #bfdbfe;
  border-radius: 9px;
  background: #f0f7ff;
  color: #075985;
  font-size: 0.9rem;
}

.desk-notice {
  margin: 0;
  padding: 0.65rem 0.85rem;
  border: 1px solid #fed7aa;
  border-radius: 9px;
  background: #fff7ed;
  color: #9a3412;
  font-size: 0.86rem;
}

.desk-error,
.receipt-summary { border-radius: 9px; padding: 0.7rem 0.9rem; }
.desk-error { background: #fff0ed; border: 1px solid #fecaca; color: #8d2624; }
.receipt-summary { display: flex; flex-wrap: wrap; align-items: center; gap: 0.65rem; background: #edf8ef; color: #1d5733; }

.invoice-detail-dialog {
  box-sizing: border-box;
  width: min(820px, calc(100% - 1.5rem));
  max-width: 100%;
  max-height: calc(100dvh - 1.5rem);
  margin: auto;
  padding: 0;
  border: 1px solid #dbe5ed;
  border-radius: 16px;
  background: #fff;
  color: #172b42;
  box-shadow: 0 24px 70px rgba(15, 28, 45, 0.28);
  overflow: hidden;
}

.invoice-detail-dialog:modal {
  overscroll-behavior: contain;
}

.invoice-detail-dialog::backdrop {
  background: rgba(15, 28, 45, 0.52);
  backdrop-filter: blur(3px);
  overflow: hidden;
  overscroll-behavior: contain;
}

.detail-modal-shell {
  display: flex;
  flex-direction: column;
  max-height: calc(100dvh - 1.5rem);
  min-width: 0;
}

.detail-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: clamp(0.9rem, 2.5vw, 1.25rem);
  border-bottom: 1px solid #e4ecf2;
  background: #f8fafc;
}

.detail-modal-title-group {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
  flex: 1 1 14rem;
  overflow-wrap: anywhere;
}

.detail-modal-title-group h2,
.detail-modal-title-group p { margin: 0; }
.detail-modal-title-group h2 { font-size: clamp(1.15rem, 2.5vw, 1.4rem); line-height: 1.25; color: #102e56; }

.detail-modal-header-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.detail-close-btn {
  flex: 0 0 44px;
  min-width: 44px;
  min-height: 44px;
  border: 1px solid #cbd5e1;
  border-radius: 9px;
  background: #fff;
  color: #172b42;
  font-size: 1.05rem;
  font-weight: 700;
  cursor: pointer;
}

.detail-close-btn:hover:not(:disabled) { background: #f1f5f9; }
.detail-close-btn:focus-visible { outline: 3px solid rgba(2, 132, 199, 0.35); outline-offset: 2px; }
.detail-close-btn:disabled { opacity: 0.5; cursor: not-allowed; }

.detail-modal-body {
  display: grid;
  gap: 1rem;
  min-width: 0;
  padding: clamp(0.9rem, 2.5vw, 1.25rem);
  overflow-y: auto;
  overscroll-behavior: contain;
  container-type: inline-size;
}

.detail-modal-body .app-btn {
  min-height: 44px;
  white-space: normal;
  text-align: center;
}

.invoice-amounts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.55rem; margin: 0; }
.invoice-amounts > div { padding: 0.8rem; background: #f1f5f9; border-radius: 9px; min-width: 0; }
.invoice-amounts dt { font-size: 0.82rem; color: #3f546b; }
.invoice-amounts dd { margin: 0.28rem 0 0; font-weight: 750; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.invoice-amounts .outstanding { grid-column: 1 / -1; color: #064e52; background: #e3f3f1; }
.outstanding dd { font-size: clamp(1.2rem, 4cqw, 1.75rem); }

.payment-confirmation,
.detail-ineligible {
  display: grid;
  gap: 0.9rem;
  padding-top: 0.25rem;
}

.payment-confirmation label { display: flex; align-items: flex-start; gap: 0.6rem; cursor: pointer; }
.payment-confirmation input { margin-top: 0.2rem; width: 1.1rem; height: 1.1rem; flex: none; }

.detail-footer-actions {
  display: flex;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.payment-panel {
  display: grid;
  gap: 0.9rem;
  padding-top: 0.9rem;
  border-top: 1px solid #e4ecf2;
  min-width: 0;
}

.payment-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.65rem;
}

.payment-heading h3 { margin: 0; font-size: 1.12rem; color: #102e56; }
.payment-actions { display: flex; flex-wrap: wrap; gap: 0.45rem; }

@container (min-width: 540px) {
  .invoice-amounts { grid-template-columns: minmax(0, 1.35fr) repeat(2, minmax(0, 1fr)); }
  .invoice-amounts .outstanding { grid-column: auto; }
}

@container (max-width: 360px) {
  .invoice-amounts { grid-template-columns: 1fr; }
  .detail-footer-actions,
  .payment-actions { flex-direction: column; align-items: stretch; }
}

@media print {
  :global(body.receipt-printing) .invoice-detail-dialog,
  :global(body.receipt-printing) .detail-modal-shell,
  :global(body.receipt-printing) .detail-modal-body {
    position: static;
    inset: auto;
    width: auto;
    max-width: none;
    max-height: none;
    margin: 0;
    padding: 0;
    border: 0;
    overflow: visible;
    background: transparent;
    box-shadow: none;
  }

  :global(body.receipt-printing) .invoice-detail-dialog::backdrop {
    display: none;
  }
}
</style>
