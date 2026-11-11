<template>
  <section aria-label="Thu tiền mặt" class="cash-payment">
    <h2>Thu tiền mặt</h2>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <button v-if="!historyReady && !busy" @click="loadHistory">Tải lại lịch sử thu</button>
    <template v-if="canPay || pending">
      <label>Số tiền thu cho hóa đơn (VND)
        <input v-model="amount" type="number" min="1" step="1" :disabled="busy || !!pending" />
      </label>
      <label>Tiền khách đưa (VND)
        <input v-model="tendered" type="number" min="1" step="1" :disabled="busy || !!pending" />
      </label>
      <p>Tiền thối: {{ money(change) }}</p>
      <p v-if="pending">Đang kiểm tra lần thu trước. Thử lại sẽ dùng cùng yêu cầu để tránh ghi nhận hai lần.</p>
      <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi đã nhận tiền mặt và kiểm tra số tiền thu</label>
      <button :disabled="busy || !historyReady || !confirmed || (!pending && !valid)" @click="receive">
        {{ pending ? 'Kiểm tra / thử lại lần thu trước' : 'Xác nhận thu tiền mặt' }}
      </button>
      <p v-if="!pending && !valid">Nhập số nguyên VND: số tiền thu không vượt nợ, tiền khách đưa đủ số tiền thu.</p>
    </template>
    <p v-else>Hóa đơn hiện không còn khoản tiền mặt có thể thu.</p>
    <h3 v-if="history.length">Các lần thu đã ghi nhận</h3>
    <ul><li v-for="payment in history" :key="payment.id">
      #{{ payment.id }} · {{ new Date(payment.paidAt).toLocaleString('vi-VN') }} · Thu ngân #{{ payment.cashierId }}
      <p v-if="payment.amount < 0">Chi hoàn {{ money(-payment.amount) }} · {{ payment.paymentMethod }}. Admin xem phiếu chi trong lịch sử hoàn / điều chỉnh.</p>
      <p v-else-if="payment.paymentMethod === 'Cash'">Tiền mặt · Đã thu {{ money(payment.amount) }} · Khách đưa {{ money(payment.amountTendered ?? 0) }} · Thối {{ money(payment.changeAmount ?? 0) }}</p>
      <p v-else>Chuyển khoản · Ghi vào hóa đơn {{ money(payment.amount) }} · Ngân hàng nhận {{ money(payment.bankReceivedAmount ?? 0) }} · Mã {{ payment.transactionReference }}
        <span v-if="payment.source === 'Manual'"> · Xác nhận thủ công</span>
        <strong v-if="payment.source === 'Simulation'"> · MÔ PHỎNG</strong></p>
      <p v-if="payment.note" role="alert">{{ payment.note }}</p>
      <PaymentReceiptModal v-if="payment.amount > 0" :invoice-id="invoiceId" :payment-id="payment.id" />
    </li></ul>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { cashPaymentsApi } from '@/services/cashPayments';
import type { CashPaymentResponse } from '@/services/cashPayments';
import { useCashPaymentsStore } from '@/stores/cashPayments';
import { useAuthStore } from '@/stores/auth';
import PaymentReceiptModal from '@/components/PaymentReceiptModal.vue';

const props = defineProps<{ invoiceId: number; remainingAmount: number; status: number }>();
const emit = defineEmits<{ paid: [payment: CashPaymentResponse] }>();
const auth = useAuthStore();
const store = useCashPaymentsStore();
const key = computed(() => `${auth.user?.userId}:${props.invoiceId}`);
const pending = computed(() => store.pending[key.value]);
const amount = ref<number | string>('');
const tendered = ref<number | string>('');
const confirmed = ref(false);
const busy = ref(false);
const error = ref('');
const history = ref<CashPaymentResponse[]>([]);
const historyReady = ref(false);
const canPay = computed(() => [1, 2, 5].includes(props.status) && props.remainingAmount > 0);
const valid = computed(() => canPay.value && Number.isSafeInteger(Number(amount.value)) && Number(amount.value) > 0 &&
  Number(amount.value) <= props.remainingAmount && Number.isSafeInteger(Number(tendered.value)) && Number(tendered.value) >= Number(amount.value));
const change = computed(() => Math.max(0, Number(tendered.value) - Number(amount.value)));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
async function loadHistory() {
  const ticket = ++version; busy.value = true; historyReady.value = false; confirmed.value = false; error.value = ''; history.value = [];
  amount.value = pending.value?.amount ?? ''; tendered.value = pending.value?.amountTendered ?? '';
  try {
    const rows = await cashPaymentsApi.history(props.invoiceId);
    if (ticket !== version) return;
    history.value = rows;
    historyReady.value = true;
    if (pending.value && rows.some(x => x.requestId === pending.value?.requestId)) store.clear(key.value);
  } catch { if (ticket === version) error.value = 'Không thể tải các lần thu. Tải lại hóa đơn để đối chiếu trước khi thu tiếp.'; }
  finally { if (ticket === version) busy.value = false; }
}
watch(() => props.invoiceId, loadHistory, { immediate: true });
async function receive() {
  if (busy.value || !historyReady.value || !confirmed.value || (!pending.value && !valid.value)) return;
  const ticket = version; const requestKey = key.value;
  const request = pending.value ?? { amount: Number(amount.value), amountTendered: Number(tendered.value), requestId: crypto.randomUUID(), paymentMethod: 'Cash' as const };
  store.put(requestKey, request); busy.value = true; error.value = '';
  try {
    const payment = await cashPaymentsApi.receive(props.invoiceId, request);
    store.clear(requestKey);
    if (ticket === version) { confirmed.value = false; emit('paid', payment); }
  } catch (cause) {
    if (axios.isAxiosError(cause) && [400, 403, 404].includes(cause.response?.status ?? 0)) store.clear(requestKey);
    if (ticket === version) {
      const conflict = axios.isAxiosError(cause) && cause.response?.data?.code === 'BIL_006';
      if (conflict) confirmed.value = false;
      error.value = conflict
        ? 'Có thao tác thu tiền đồng thời. Đối chiếu hóa đơn và xác nhận lại để kiểm tra / thử lại cùng yêu cầu; không nhận thêm tiền khách.'
        : axios.isAxiosError(cause) ? cause.response?.data?.message || 'Chưa xác định kết quả lần thu. Kiểm tra hoặc thử lại cùng yêu cầu.' : 'Chưa xác định kết quả lần thu.';
    }
  } finally { if (ticket === version) busy.value = false; }
}
onUnmounted(() => { version++; });
</script>

<style scoped>
.cash-payment {
  border-top: 2px solid #e2e8f0;
  padding-top: 1.5rem;
  margin-top: 1.5rem;
}

h2 {
  font-size: 1.35rem;
  font-weight: 800;
  color: #0f172a;
  margin-bottom: 1rem;
}

.cash-payment label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin: 0.85rem 0;
  font-weight: 600;
  color: #334155;
}

input[type=number] {
  padding: 0.65rem 0.85rem;
  min-height: 44px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font-size: 1.1rem;
  font-weight: 700;
  color: #0369a1;
  background: #f8fafc;
  box-sizing: border-box;
}

input[type=number]:focus {
  background: #fff;
  border-color: #0284c7;
  outline: none;
}

input[type=checkbox] {
  width: 18px;
  height: 18px;
  accent-color: #0284c7;
}

button {
  min-height: 44px;
  padding: 0.65rem 1.25rem;
  border-radius: 8px;
  border: none;
  background: #0284c7;
  color: #fff;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}

button:hover:not(:disabled) {
  background: #0369a1;
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

ul {
  list-style: none;
  padding: 0;
  margin: 1rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

li {
  border: 1px solid #e2e8f0;
  background: #f8fafc;
  border-radius: 10px;
  padding: 1rem 1.25rem;
  box-shadow: 0 1px 4px rgba(0,0,0,0.03);
}

[role=alert] {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
}
</style>
