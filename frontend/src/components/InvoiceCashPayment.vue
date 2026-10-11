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
      <p>Đã thu {{ money(payment.amount) }} · Khách đưa {{ money(payment.amountTendered) }} · Thối {{ money(payment.changeAmount) }}</p>
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
const canPay = computed(() => [1, 2].includes(props.status) && props.remainingAmount > 0);
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
    if (ticket === version) error.value = axios.isAxiosError(cause) ? cause.response?.data?.message || 'Chưa xác định kết quả lần thu. Kiểm tra hoặc thử lại cùng yêu cầu.' : 'Chưa xác định kết quả lần thu.';
  } finally { if (ticket === version) busy.value = false; }
}
onUnmounted(() => { version++; });
</script>

<style scoped>
.cash-payment { border-top: 1px solid #ccc; padding-top: 1rem; }
label { display: block; margin: .7rem 0; }
input[type=number] { display: block; padding: .5rem; }
</style>
