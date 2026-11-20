<template>
  <section class="invoice-desk" aria-labelledby="invoice-desk-title">
    <header class="desk-heading">
      <h1 id="invoice-desk-title">Tra cứu &amp; thu hóa đơn điều trị</h1>
      <p>Tìm hóa đơn, đối chiếu công nợ và in biên lai tại quầy.</p>
    </header>

    <div class="desk-layout">
      <section class="desk-panel lookup-panel" aria-labelledby="lookup-title">
        <h2 id="lookup-title">Tra cứu hóa đơn</h2>
        <form @submit.prevent="lookup">
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
              :disabled="busy || paymentOpen"
            />
            <button type="submit" class="app-btn app-btn--primary" :disabled="busy || paymentOpen || !allowed">
              {{ busy && !paymentOpen ? 'Đang tìm…' : 'Tra cứu' }}
            </button>
          </div>
        </form>
        <p v-if="error && !paymentOpen" class="desk-error" role="alert">{{ error }}</p>
        <div v-if="!paymentOpen" class="recent-invoices">
          <h3>Hóa đơn gần đây</h3>
          <InvoiceHistoryPanel :disabled="busy" @selected="selectFromHistory" />
        </div>
        <p v-else class="desk-muted">Bạn đang đối chiếu hóa đơn {{ invoice?.invoiceCode }}.</p>
      </section>

      <div class="desk-results">
        <section v-if="invoice" class="desk-panel" aria-labelledby="invoice-reference">
          <header class="invoice-heading">
            <div>
              <p class="desk-muted">Hóa đơn {{ invoice.invoiceCode }}</p>
              <h2 id="invoice-reference">{{ invoice.patientName }}</h2>
              <p class="desk-muted">{{ invoice.patientCode }} · {{ dateTime.format(new Date(invoice.createdAt)) }}</p>
            </div>
            <span class="app-badge" :class="{
              'app-badge--success': invoice.status === 3,
              'app-badge--warning': [2, 5].includes(invoice.status),
              'app-badge--danger': invoice.status === 4,
              'app-badge--info': [0, 1, 6].includes(invoice.status)
            }">{{ statuses[invoice.status] ?? 'Không xác định' }}</span>
          </header>

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

          <div v-if="!paymentOpen && eligible" class="payment-confirmation">
            <label>
              <input v-model="confirmed" type="checkbox" />
              <span>Tôi đã đối chiếu mã hóa đơn, tên bệnh nhân và số tiền còn phải thu.</span>
            </label>
            <button type="button" class="app-btn app-btn--primary" :disabled="!confirmed || busy" @click="openPayment">
              {{ invoice.remainingAmount > 0 ? 'Mở thu tiền và biên lai' : 'Xem lịch sử thu và biên lai' }}
            </button>
          </div>
          <p v-else-if="!eligible && !paymentOpen" class="desk-muted">Hóa đơn này chưa thể mở phần thu tiền.</p>
        </section>

        <section v-else class="desk-panel empty-invoice" aria-live="polite">
          <h2>{{ busy ? 'Đang tìm hóa đơn…' : 'Chi tiết hóa đơn' }}</h2>
          <p v-if="!busy">Nhập mã hoặc chọn hóa đơn gần đây để xem số tiền còn phải thu.</p>
        </section>

        <section v-if="invoice && paymentOpen" class="desk-panel payment-panel" aria-labelledby="payment-title">
          <header class="payment-heading">
            <h2 id="payment-title" ref="paymentHeading" tabindex="-1">Thu tiền và biên lai</h2>
            <div class="payment-actions">
              <button type="button" class="app-btn app-btn--secondary" :disabled="busy" @click="refresh">Đối chiếu lại công nợ</button>
              <button
                type="button"
                class="app-btn app-btn--secondary"
                :disabled="busy || reviewRequired || !paymentPanel?.canLeave"
                @click="startNewLookup"
              >Tra hóa đơn khác</button>
            </div>
          </header>
          <p v-if="error" class="desk-error" role="alert">{{ error }}</p>
          <div v-if="lastPayment" class="receipt-summary" aria-label="Biên lai lần thu vừa ghi nhận">
            <strong>Đã ghi nhận giao dịch #{{ lastPayment.id }}</strong>
            <span>Thực thu {{ money(lastPayment.amount) }}</span>
            <span v-if="lastPayment.changeAmount && lastPayment.changeAmount > 0">Tiền thối {{ money(lastPayment.changeAmount) }}</span>
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
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy', 'Đã hoàn một phần', 'Đã hoàn toàn bộ'];
const invoiceCodeInput = ref<HTMLInputElement | null>(null);
const paymentPanel = ref<InstanceType<typeof InvoiceCashPayment> | null>(null);
const paymentHeading = ref<HTMLHeadingElement | null>(null);
const dateTime = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh'
});
const eligible = computed(() => allowed.value && !!invoice.value && [1, 2, 3, 5, 6].includes(invoice.value.status));
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
  void nextTick(() => paymentHeading.value?.focus());
}
function startNewLookup() {
  if (busy.value || reviewRequired.value || !paymentPanel.value?.canLeave) return;
  paymentOpen.value = false;
  confirmed.value = false;
  invoice.value = null;
  lastPayment.value = null;
  code.value = '';
  error.value = '';
  void nextTick(() => invoiceCodeInput.value?.focus());
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
.invoice-desk { container-type: inline-size; color: #172b42; }
.desk-heading { margin-bottom: 1.2rem; }
.desk-heading h1 { margin: 0; font-size: clamp(1.45rem, 3vw, 2.1rem); line-height: 1.2; }
.desk-heading p { margin: 0.45rem 0 0; color: #51647a; }
.desk-layout { display: grid; gap: 1rem; align-items: start; }
.desk-results { display: grid; gap: 1rem; min-width: 0; }
.desk-panel {
  display: grid;
  gap: 1rem;
  min-width: 0;
  padding: clamp(0.9rem, 2.5cqw, 1.35rem);
  border: 1px solid #dbe5ed;
  border-radius: 14px;
  background: #fff;
}
.desk-panel h2, .desk-panel h3, .desk-panel p { margin: 0; }
.desk-panel h2 { font-size: 1.16rem; line-height: 1.3; }
.desk-panel h3 { font-size: 0.95rem; }
.desk-muted, .empty-invoice p { color: #51647a; font-size: 0.88rem; }
.lookup-panel form { display: grid; gap: 0.45rem; }
.lookup-panel label { font-size: 0.88rem; font-weight: 650; }
.lookup-controls { display: flex; flex-wrap: wrap; gap: 0.45rem; }
.lookup-controls input { flex: 1 1 9rem; min-width: 0; max-width: 100%; }
.desk-panel .app-btn { min-height: 42px; white-space: normal; text-align: center; }
.recent-invoices { display: grid; gap: 0.6rem; padding-top: 0.8rem; border-top: 1px solid #e4ecf2; }
.invoice-heading, .payment-heading { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 0.65rem; }
.invoice-heading > div { display: grid; gap: 0.25rem; overflow-wrap: anywhere; }
.invoice-amounts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.55rem; margin: 0; }
.invoice-amounts > div { padding: 0.8rem; background: #f1f5f9; border-radius: 9px; }
.invoice-amounts dt { font-size: 0.82rem; color: #3f546b; }
.invoice-amounts dd { margin: 0.28rem 0 0; font-weight: 750; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.invoice-amounts .outstanding { grid-column: 1 / -1; color: #064e52; background: #e3f3f1; }
.outstanding dd { font-size: clamp(1.24rem, 4cqw, 1.9rem); }
.payment-confirmation { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.85rem; }
.payment-confirmation label { display: flex; align-items: flex-start; gap: 0.6rem; cursor: pointer; }
.payment-confirmation input { margin-top: 0.2rem; width: 1.1rem; height: 1.1rem; flex: none; }
.payment-actions { display: flex; flex-wrap: wrap; gap: 0.45rem; }
.desk-error, .receipt-summary { border-radius: 9px; padding: 0.7rem 0.9rem; }
.desk-error { background: #fff0ed; color: #8d2624; }
.receipt-summary { display: flex; flex-wrap: wrap; align-items: center; gap: 0.65rem; background: #edf8ef; color: #1d5733; }
.empty-invoice { background: #f8fafc; }
@container (min-width: 740px) {
  .desk-layout { grid-template-columns: minmax(15rem, 1fr) minmax(0, 1.9fr); }
  .invoice-amounts { grid-template-columns: minmax(0, 1.35fr) repeat(2, minmax(0, 1fr)); }
  .invoice-amounts .outstanding { grid-column: auto; }
}
</style>
