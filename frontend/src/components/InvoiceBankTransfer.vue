<template>
  <section class="bank-transfer" aria-label="Chuyển khoản VietQR">
    <h2>Chuyển khoản VietQR</h2>
    <button :disabled="busy" @click="openModal">Mở thanh toán VietQR</button>
    <dialog ref="dialog" @close="endSession" @cancel="endSession">
    <button @click="closeModal">Đóng</button>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <button v-if="!info && !busy" @click="load">Tải lại thông tin chuyển khoản</button>
    <template v-if="info">
      <p>Nội dung: <strong>{{ info.content }}</strong> · Còn nợ: {{ money(info.remainingAmount) }}</p>
      <p v-if="!expired">Phiên VietQR còn: <strong>{{ countdown }}</strong></p>
      <div v-if="expired" role="alert">
        <p><strong>Mã thanh toán đã hết hạn</strong></p>
        <button :disabled="busy" @click="load">Tạo lại mã QR</button>
        <button :disabled="busy" @click="chooseOtherMethod">Chọn phương thức khác (Tiền mặt)</button>
      </div>
      <p v-if="info.configurationMessage">{{ info.configurationMessage }}</p>
      <img v-if="qrDisplay === 'online'" :class="{ 'qr-expired': expired }" :src="info.qrUrl!" :alt="`VietQR ${info.invoiceCode}`" referrerpolicy="no-referrer" @error="handleQrImageError" />
      <div v-else-if="qrDisplay === 'offline-demo'" :class="['offline-demo', { 'qr-expired': expired }]" role="status">
        <p><strong>Chế độ mô phỏng ngoại tuyến (Offline Demo)</strong></p>
        <img :src="demoQrImage" alt="Minh họa VietQR ngoại tuyến — không dùng để chuyển tiền" />
        <p>Ảnh minh họa không chứa tài khoản ngân hàng. Dùng nút mô phỏng bên dưới để nghiệm thu luồng; cần kết nối với backend cục bộ.</p>
      </div>
      <p v-else-if="qrFailed" role="alert">Không tải được ảnh VietQR. Kiểm tra kết nối trước khi chuyển tiền.</p>
      <button v-if="qrFailed && info.qrUrl" :disabled="busy || expired" @click="retryQrImage">Thử tải lại VietQR trực tuyến</button>
      <p v-if="info.accountName">Tài khoản phòng khám: {{ info.accountName }}</p>
      <button :disabled="busy || watching || expired" @click="startWatching">Theo dõi chuyển khoản</button>
      <button :disabled="busy || expired" @click="checkNow">Kiểm tra thanh toán</button>
      <button v-if="watching" @click="stopWatching">Dừng theo dõi</button>
      <p v-if="watching" role="status">Đang chờ thông báo khoản thu mới…</p>
      <fieldset v-if="info.simulationEnabled" :disabled="expired">
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
import { MOD_BIL_bankQrDisplay } from '@/services/bankQrDisplay';
import { MOD_BIL_createQrSession, MOD_BIL_formatQrCountdown } from '@/services/bankQrSession';
import demoQrImage from '@/assets/images/vietqr-demo-fallback.svg';
const props = defineProps<{ invoiceId: number }>();
const emit = defineEmits<{ paid: [payment: BankTransferResponse | null] }>();
const auth = useAuthStore(); const store = useBankTransfersStore();
const key = computed(() => `${auth.user?.userId}:${props.invoiceId}`);
const pending = computed(() => store.pending[key.value]);
const info = ref<BankTransferInfo | null>(null); const amount = ref<number | string>('');
const dialog = ref<HTMLDialogElement | null>(null);
const busy = ref(false); const confirmed = ref(false); const error = ref(''); const qrFailed = ref(false); const watching = ref(false);
const remainingSeconds = ref(0); const expired = ref(false);
const countdown = computed(() => MOD_BIL_formatQrCountdown(remainingSeconds.value));
const qrSession = MOD_BIL_createQrSession(seconds => { remainingSeconds.value = seconds; }, () => {
  expired.value = true; confirmed.value = false; stopWatching();
});
const qrDisplay = computed(() => MOD_BIL_bankQrDisplay(info.value?.qrUrl ?? null, qrFailed.value, info.value?.simulationEnabled ?? false));
function handleQrImageError() { qrFailed.value = true; }
const valid = computed(() => Number.isSafeInteger(Number(amount.value)) && Number(amount.value) > 0);
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0; let timer: ReturnType<typeof setInterval> | undefined; let polling = false;
let checkCurrent: (() => Promise<void>) | undefined;
async function checkNow() { if (!qrSession.isActive()) return; if (watching.value && checkCurrent) await checkCurrent(); else await startWatching(); }
async function openModal() { dialog.value?.showModal(); await load(); }
function endSession() { version++; qrSession.stop(); stopWatching(); busy.value = false; }
function closeModal() { endSession(); dialog.value?.close(); }
function chooseOtherMethod() {
  closeModal();
  document.querySelector<HTMLInputElement>('[aria-label="Thu tiền mặt"] input')?.focus();
}
function retryQrImage() { if (qrSession.isActive()) qrFailed.value = false; }
function stopWatching() { if (timer) clearInterval(timer); timer = undefined; watching.value = false; }
async function load() {
  const ticket = ++version; qrSession.stop(); stopWatching(); expired.value = false; remainingSeconds.value = 0;
  busy.value = true; confirmed.value = false; info.value = null; error.value = ''; qrFailed.value = false;
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
  if (ticket === version && info.value && dialog.value?.open) { qrSession.start(); await startWatching(); }
}
async function startWatching() {
  if (busy.value || watching.value || !dialog.value?.open || !qrSession.isActive()) return;
  const ticket = version; busy.value = true; error.value = '';
  try {
    watching.value = true;
    async function checkBalance() {
      if (polling || ticket !== version || !watching.value || !qrSession.isActive()) return; polling = true;
      try {
        const latest = await bankTransfersApi.state(props.invoiceId);
        if (ticket !== version || !watching.value || !qrSession.isActive()) return;
        if (latest.remainingAmount !== info.value?.remainingAmount) { closeModal(); emit('paid', null); }
        else error.value = '';
      } catch (cause) {
        if (ticket !== version || !watching.value || !qrSession.isActive()) return;
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
  if (busy.value || !info.value?.simulationEnabled || !confirmed.value || (!pending.value && !valid.value) || !qrSession.isActive()) return;
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
onUnmounted(endSession);
</script>
<style scoped>
.bank-transfer {
  border-top: 1px solid #e2e8f0;
  padding-top: 1.25rem;
  margin-top: 1.25rem;
}

h3 {
  color: #0f2d59;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

img {
  max-width: 260px;
  width: 100%;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  padding: 0.5rem;
  background: #ffffff;
  display: block;
  margin: 0.75rem 0;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

input:not([type="checkbox"]):not([type="radio"]),
select {
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible,
select:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  vertical-align: middle;
  margin-right: 0.4rem;
}

button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #0f2d59;
  color: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  margin: 0.4rem 0.4rem 0.4rem 0;
}

button:hover:not(:disabled) {
  background: #1e3a8a;
}

button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

dialog {
  width: min(90vw, 600px);
  max-height: 85vh;
  overflow: auto;
  border: 1px solid #cbd5e1;
  border-radius: 12px;
  box-shadow: 0 10px 25px rgba(15, 45, 89, 0.15);
  padding: 1.5rem;
}

dialog::backdrop {
  background: rgba(15, 45, 89, 0.5);
  backdrop-filter: blur(2px);
}

.offline-demo {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fde68a;
  border-left: 4px solid #d97706;
  border-radius: 8px;
  padding: 0.75rem 1rem;
  margin: 0.75rem 0;
}

.qr-expired {
  opacity: 0.3;
  filter: grayscale(1);
  pointer-events: none;
}

@media (max-width: 640px) {
  button {
    width: 100%;
    margin-right: 0;
  }
}
</style>
