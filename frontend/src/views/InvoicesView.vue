<template>
  <main class="invoice-desk">
    <h1>Tra cứu hóa đơn tại quầy</h1>
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
.invoice-desk { max-width: 900px; margin: 2rem auto; padding: 1rem; }
label { display: block; margin: 1rem 0; }
input, button { padding: .5rem; }
section { border: 1px solid #ccc; padding: 1rem; margin-top: 1rem; }
</style>
