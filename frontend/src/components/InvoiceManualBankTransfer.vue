<template>
  <section class="manual-bank" aria-labelledby="manual-bank-title">
    <h2 id="manual-bank-title">Xác nhận chuyển khoản thủ công</h2>
    <p>Kiểm tra tiền thực vào tài khoản phòng khám và đối chiếu đúng hóa đơn trên ứng dụng ngân hàng trước khi xác nhận.</p>
    <p>Không cần kết nối webhook. Việc khách gửi ảnh chuyển tiền chưa đủ để xác nhận đã nhận tiền.</p>
    <button type="button" :disabled="busy" @click="load">Tải lại số dư</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="state">Hóa đơn <strong>{{ state.invoiceCode }}</strong> · Còn nợ: {{ money(state.remainingAmount) }}</p>
    <p v-if="pending" role="status">Còn yêu cầu cần đối chiếu. Thử lại giữ nguyên số tiền và mã yêu cầu; không nhận thêm tiền khách.</p>
    <p v-if="state && !payable && !pending">Hóa đơn không còn ở trạng thái có thể thu thêm.</p>
    <form v-if="state && (payable || pending)" @submit.prevent="receive">
      <label for="manual-bank-amount">Số tiền thực nhận (VND)</label>
      <input id="manual-bank-amount" ref="amountInput" v-model="amount" name="bankAmount" type="number" inputmode="numeric"
        min="1" max="9007199254740991" step="1" required :disabled="busy || !!pending" />
      <label for="manual-bank-reference">Mã giao dịch ngân hàng (6–20 ký tự)</label>
      <input id="manual-bank-reference" ref="referenceInput" v-model="reference" name="bankReference" type="text" autocomplete="off"
        minlength="6" maxlength="20" pattern="[A-Za-z0-9_-]{6,20}" required :disabled="busy || !!pending" aria-describedby="manual-bank-reference-help" />
      <p id="manual-bank-reference-help">Nhập đúng mã trên ngân hàng, phân biệt chữ hoa/thường; chỉ chữ, số, dấu gạch ngang hoặc gạch dưới.</p>
      <p v-if="excess > 0" role="status">Chuyển dư {{ money(excess) }}. Hệ thống chỉ ghi giảm nợ bằng số còn phải thu và lưu phần dư để đối soát hoàn trả.</p>
      <label><input v-model="confirmed" type="checkbox" required :disabled="busy" /> Tôi đã kiểm tra tiền thực vào ngân hàng, đúng hóa đơn, số tiền và mã giao dịch.</label>
      <button type="submit" :disabled="busy">{{ pending ? 'Kiểm tra / thử lại cùng giao dịch' : 'Xác nhận đã nhận tiền chuyển khoản' }}</button>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { bankTransfersApi, type BankTransferResponse } from '@/services/bankTransfers';
import { useManualBankTransfersStore } from '@/stores/manualBankTransfers';
import { useAuthStore } from '@/stores/auth';

const props = defineProps<{ invoiceId: number }>();
const emit = defineEmits<{ paid: [payment: BankTransferResponse] }>();
const auth = useAuthStore(), store = useManualBankTransfersStore();
const key = computed(() => `${auth.user?.userId}:${props.invoiceId}`);
const pending = computed(() => store.pending[key.value]);
const state = ref<Awaited<ReturnType<typeof bankTransfersApi.state>> | null>(null);
const amount = ref<number | string>(''), reference = ref(''), confirmed = ref(false), busy = ref(false), error = ref('');
const amountInput = ref<HTMLInputElement | null>(null), referenceInput = ref<HTMLInputElement | null>(null);
const canWrite = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role));
const payable = computed(() => !!state.value && [1, 2, 5].includes(state.value.status) && state.value.remainingAmount > 0);
const validAmount = computed(() => Number.isSafeInteger(Number(amount.value)) && Number(amount.value) > 0);
const validReference = computed(() => /^[A-Za-z0-9_-]{6,20}$/.test(reference.value));
const excess = computed(() => Math.max(0, Number(amount.value) - (pending.value?.expectedRemainingAmount ?? state.value?.remainingAmount ?? 0)));
const money = (v: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(v);
let version = 0;

async function load() {
  const ticket = ++version; state.value = null; confirmed.value = false; error.value = ''; amount.value = ''; reference.value = '';
  if (!canWrite.value) { busy.value = false; return; }
  busy.value = true;
  try {
    const data = await bankTransfersApi.state(props.invoiceId);
    if (ticket !== version) return;
    state.value = data; amount.value = pending.value?.amount ?? data.remainingAmount; reference.value = pending.value?.transactionReference ?? '';
  } catch { if (ticket === version) error.value = 'Không tải được số dư. Tải lại và đối chiếu trước khi xác nhận.'; }
  finally { if (ticket === version) busy.value = false; }
}

async function receive() {
  if (busy.value || !canWrite.value || !state.value || (!payable.value && !pending.value)) return;
  if (!pending.value && !validAmount.value) { error.value = 'Số tiền phải là số nguyên đồng lớn hơn 0.'; amountInput.value?.focus(); return; }
  if (!pending.value && !validReference.value) { error.value = 'Mã giao dịch cần 6–20 ký tự hợp lệ.'; referenceInput.value?.focus(); return; }
  if (!confirmed.value) { error.value = 'Cần xác nhận đã đối soát tiền thực vào tài khoản phòng khám.'; return; }
  const ticket = version, requestKey = key.value;
  const request = pending.value ?? { requestId: crypto.randomUUID(), amount: Number(amount.value), transactionReference: reference.value,
    expectedRemainingAmount: state.value.remainingAmount, confirmed: true as const };
  store.put(requestKey, request); busy.value = true; error.value = '';
  try {
    const payment = await bankTransfersApi.confirmManual(props.invoiceId, request);
    store.clear(requestKey);
    if (ticket === version) { confirmed.value = false; reference.value = ''; emit('paid', payment); }
  } catch (cause) {
    const response = axios.isAxiosError(cause) ? cause.response : undefined;
    const code = response?.data?.code;
    const definitive = [400, 401, 403, 404].includes(response?.status ?? 0) || ['BIL_064', 'BIL_065'].includes(code);
    if (definitive) store.clear(requestKey);
    if (ticket !== version) return;
    confirmed.value = false;
    if (code === 'BIL_064') {
      await load();
      if (ticket + 1 !== version) return;
      error.value = response?.data?.message ?? 'Số dư thay đổi. Đối chiếu lại trước khi xác nhận.';
    } else error.value = response?.data?.message || 'Chưa xác định kết quả. Đối chiếu hoặc thử lại cùng yêu cầu; không nhận thêm tiền khách.';
  } finally { if (ticket === version) busy.value = false; }
}
watch(() => `${key.value}:${auth.role}`, load, { immediate: true });
onUnmounted(() => { version++; });
</script>

<style scoped>
.manual-bank { border-top: 1px solid #ccc; padding-top: 1rem; }
label { display: block; margin: .7rem 0 .3rem; }
input[type=number], input[type=text] { display: block; padding: .5rem; max-width: 100%; box-sizing: border-box; }
button { margin: .5rem 0; padding: .5rem; }
</style>
