<template>
  <section class="bank-transfer" aria-label="Chuyển khoản VietQR">
    <h2>Chuyển khoản VietQR</h2>
    <button :disabled="busy" @click="openModal">Mở thanh toán VietQR</button>
    <dialog ref="dialog" @close="stopWatching" @cancel="stopWatching">
    <button @click="closeModal">Đóng</button>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <button v-if="!info && !busy" @click="load">Tải lại thông tin chuyển khoản</button>
    <template v-if="info">
      <p>Nội dung: <strong>{{ info.content }}</strong> · Còn nợ: {{ money(info.remainingAmount) }}</p>
      <p v-if="info.configurationMessage">{{ info.configurationMessage }}</p>
      <img v-if="info.qrUrl && !qrFailed" :src="info.qrUrl" :alt="`VietQR ${info.invoiceCode}`" referrerpolicy="no-referrer" @error="qrFailed = true" />
      <p v-if="qrFailed">Không tải được ảnh VietQR. Kiểm tra kết nối trước khi chuyển tiền.</p>
      <p v-if="info.accountName">Tài khoản phòng khám: {{ info.accountName }}</p>
      <button :disabled="busy || watching" @click="startWatching">Theo dõi chuyển khoản</button>
      <button :disabled="busy" @click="checkNow">Kiểm tra thanh toán</button>
      <button v-if="watching" @click="stopWatching">Dừng theo dõi</button>
      <p v-if="watching" role="status">Đang chờ thông báo khoản thu mới…</p>
      <fieldset v-if="info.simulationEnabled">
        <legend>Mô phỏng trong môi trường phát triển</legend>
        <p>Thao tác này ghi khoản thu MÔ PHỎNG vào hóa đơn, không có tiền ngân hàng thực.</p>
        <label>Số tiền mô phỏng (VND)<input v-model="amount" type="number" min="1" step="1" :disabled="busy || !!pending" /></label>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi xác nhận đây là dữ liệu mô phỏng</label>
        <button :disabled="busy || !confirmed || (!pending && !valid)" @click="simulate">{{ pending ? 'Kiểm tra / thử lại lần mô phỏng' : 'Mô phỏng thanh toán chuyển khoản thành công' }}</button>
      </fieldset>
    </template>
    </dialog>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { bankTransfersApi } from '@/services/bankTransfers';
import type { BankTransferInfo, BankTransferResponse } from '@/services/bankTransfers';
import { useBankTransfersStore } from '@/stores/bankTransfers';
import { useAuthStore } from '@/stores/auth';
const props = defineProps<{ invoiceId: number }>();
const emit = defineEmits<{ paid: [payment: BankTransferResponse | null] }>();
const auth = useAuthStore(); const store = useBankTransfersStore();
const key = computed(() => `${auth.user?.userId}:${props.invoiceId}`);
const pending = computed(() => store.pending[key.value]);
const info = ref<BankTransferInfo | null>(null); const amount = ref<number | string>('');
const dialog = ref<HTMLDialogElement | null>(null);
const busy = ref(false); const confirmed = ref(false); const error = ref(''); const qrFailed = ref(false); const watching = ref(false);
const valid = computed(() => Number.isSafeInteger(Number(amount.value)) && Number(amount.value) > 0);
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0; let timer: ReturnType<typeof setInterval> | undefined; let polling = false;
let checkCurrent: (() => Promise<void>) | undefined;
async function checkNow() { if (watching.value && checkCurrent) await checkCurrent(); else await startWatching(); }
async function openModal() { dialog.value?.showModal(); await load(); }
function closeModal() { stopWatching(); dialog.value?.close(); }
function stopWatching() { if (timer) clearInterval(timer); timer = undefined; watching.value = false; }
async function load() {
  const ticket = ++version; stopWatching(); busy.value = true; confirmed.value = false; info.value = null; error.value = ''; qrFailed.value = false;
  try {
    const data = await bankTransfersApi.info(props.invoiceId);
    if (ticket !== version) return;
    info.value = data; amount.value = pending.value?.amount ?? data.remainingAmount;
  } catch (cause) {
    if (ticket !== version) return;
    if (axios.isAxiosError(cause) && cause.response?.status === 409) { closeModal(); emit('paid', null); }
    else error.value = 'Không thể tải thông tin chuyển khoản. Tải lại hóa đơn để đối chiếu.';
  }
  finally { if (ticket === version) busy.value = false; }
  if (ticket === version && info.value && dialog.value?.open) await startWatching();
}
async function startWatching() {
  if (busy.value || watching.value || !dialog.value?.open) return;
  const ticket = version; busy.value = true; error.value = '';
  try {
    watching.value = true;
    async function checkBalance() {
      if (polling || ticket !== version || !watching.value) return; polling = true;
      try {
        const latest = await bankTransfersApi.state(props.invoiceId);
        if (ticket !== version || !watching.value) return;
        if (latest.remainingAmount !== info.value?.remainingAmount) { closeModal(); emit('paid', null); }
        else error.value = '';
      } catch (cause) {
        if (ticket !== version || !watching.value) return;
        if (axios.isAxiosError(cause) && cause.response?.status === 409) { closeModal(); emit('paid', null); }
        else error.value = 'Mất kết nối theo dõi. Chưa xác định tiền đã vào; kiểm tra hóa đơn khi kết nối lại.';
      } finally { polling = false; }
    }
    checkCurrent = checkBalance;
    await checkBalance();
    if (ticket !== version || !watching.value) return;
    timer = setInterval(async () => {
      await checkBalance();
    }, 3000);
  } catch { if (ticket === version) error.value = 'Không tải được lịch sử để đối chiếu. Hãy thử lại.'; }
  finally { if (ticket === version) busy.value = false; }
}
async function simulate() {
  if (busy.value || !info.value?.simulationEnabled || !confirmed.value || (!pending.value && !valid.value)) return;
  const ticket = version; const requestKey = key.value;
  const request = pending.value ?? { addInfo: info.value.content, amount: Number(amount.value), transactionReference: 'SIM' + crypto.randomUUID().replaceAll('-', '').slice(0, 16) };
  store.put(requestKey, request); busy.value = true; error.value = '';
  try {
    const payment = await bankTransfersApi.simulate(request); store.clear(requestKey);
    if (ticket === version) { confirmed.value = false; closeModal(); emit('paid', payment); }
  } catch (cause) {
    if (axios.isAxiosError(cause) && [400, 403, 404].includes(cause.response?.status ?? 0)) store.clear(requestKey);
    if (ticket === version) error.value = axios.isAxiosError(cause) ? cause.response?.data?.message || 'Chưa xác định kết quả. Thử lại cùng mã mô phỏng.' : 'Chưa xác định kết quả.';
  } finally { if (ticket === version) busy.value = false; }
}
watch(() => props.invoiceId, load, { immediate: true });
onUnmounted(() => { version++; stopWatching(); });
</script>
<style scoped>
.bank-transfer { border-top: 1px solid #ccc; padding-top: 1rem; }
img { max-width: 300px; width: 100%; } label { display: block; margin: .6rem 0; } button { margin: .4rem; }
dialog { width: min(90vw, 600px); max-height: 85vh; overflow: auto; border: 1px solid #ccc; }
dialog::backdrop { background: rgb(0 0 0 / .4); }
</style>
