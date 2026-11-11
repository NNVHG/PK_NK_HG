<template>
  <main class="invoice-desk">
    <h1>Tra cứu hóa đơn tại quầy</h1>
    <InvoiceHistoryPanel v-if="!paymentOpen" :disabled="busy" @selected="selectFromHistory" />
    <form @submit.prevent="lookup">
      <label>Mã hóa đơn <input v-model="code" maxlength="50" autocomplete="off" :disabled="busy || paymentOpen" /></label>
      <button :disabled="busy || paymentOpen || !allowed">Tra cứu</button>
    </form>
    <p v-if="busy" role="status">Đang đối chiếu hóa đơn…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <section v-if="invoice" aria-label="Thông tin đối chiếu">
      <h2>{{ invoice.invoiceCode }}</h2>
      <p>Bệnh nhân: {{ invoice.patientCode }} · {{ invoice.patientName }}</p>
      <p>Ngày lập: {{ new Date(invoice.createdAt).toLocaleString('vi-VN') }}</p>
      <p>Trạng thái: {{ statuses[invoice.status] ?? 'Không xác định' }}</p>
      <p>Tổng tiền: {{ money(invoice.totalAmount) }} · Đã trả: {{ money(invoice.paidAmount) }} · Còn nợ: {{ money(invoice.remainingAmount) }}</p>
      <template v-if="!paymentOpen && eligible">
        <label><input v-model="confirmed" type="checkbox" /> Tôi đã đối chiếu đúng mã hóa đơn, bệnh nhân và công nợ</label>
        <button :disabled="!confirmed || busy" @click="openPayment">Mở thu tiền / biên lai</button>
      </template>
      <p v-else-if="!eligible">Hóa đơn nháp hoặc đã hủy không thể mở thu tiền.</p>
      <template v-if="paymentOpen">
        <p>Đang xử lý hóa đơn này. Hoàn tất đối chiếu lần thu trước trước khi tải lại trang để tra cứu mã khác.</p>
        <button :disabled="busy" @click="refresh">Đối chiếu lại công nợ</button>
        <InvoiceCashPayment :invoice-id="invoice.invoiceId" :remaining-amount="reviewRequired ? 0 : invoice.remainingAmount"
          :status="reviewRequired ? 0 : invoice.status" @paid="onPaid" />
        <section v-if="lastPayment" aria-label="Biên lai lần thu vừa ghi nhận">
          <p>Đã ghi nhận khoản thu #{{ lastPayment.id }} · {{ money(lastPayment.amount) }} · Tiền thối {{ money(lastPayment.changeAmount ?? 0) }}</p>
          <PaymentReceiptModal :invoice-id="invoice.invoiceId" :payment-id="lastPayment.id" />
        </section>
      </template>
    </section>
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
.invoice-desk {
  max-width: 960px;
  margin: 1.5rem auto;
  padding: 1.5rem;
  box-sizing: border-box;
  font-family: inherit;
  color: #1e293b;
}

h1 {
  font-size: 1.75rem;
  font-weight: 800;
  color: #0f172a;
  margin-bottom: 1.25rem;
}

form {
  display: flex;
  gap: 0.75rem;
  align-items: flex-end;
  margin: 1.5rem 0;
  background: #f8fafc;
  padding: 1.25rem;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
}

label {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-weight: 600;
  flex: 1;
}

input, button {
  font: inherit;
  min-height: 44px;
  padding: 0.65rem 0.9rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  box-sizing: border-box;
}

input {
  background: #fff;
}

button {
  cursor: pointer;
  background: #0284c7;
  color: #fff;
  border: none;
  font-weight: 600;
  transition: all 0.2s;
  padding: 0.65rem 1.25rem;
}
button:hover:not(:disabled) {
  background: #0369a1;
}
button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

section {
  border: 1px solid #e2e8f0;
  background: #ffffff;
  border-radius: 12px;
  padding: 1.5rem;
  margin-top: 1.5rem;
  box-shadow: 0 2px 8px rgba(0,0,0,0.05);
}

section h2 {
  font-size: 1.5rem;
  font-weight: 800;
  color: #0284c7;
  margin-top: 0;
}

[role=alert] {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 1rem 0;
}

[role=status] {
  color: #0284c7;
  font-weight: 600;
}

@media (max-width: 600px) {
  .invoice-desk {
    padding: 0.75rem;
  }
  form {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
