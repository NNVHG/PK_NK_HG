<template>
  <main class="invoice-page">
    <RouterLink to="/">← Trang chủ</RouterLink>
    <h1>Hóa đơn nháp · Lần khám #{{ visitId }}</h1>
    <RouterLink v-if="!busy && !discountBusy" :to="`/clinical/visits/${visitId}/prescription`">Xem / kiểm tra đơn thuốc</RouterLink>
    <button :disabled="busy || discountBusy" @click="load">Tải lại hồ sơ</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <p v-if="invoice?.status === 0">Hóa đơn nháp tự cập nhật dịch vụ khi mở hoặc tải lại hồ sơ. Có thể tiếp tục chỉ định trước khi kết thúc khám.</p>
    <template v-if="visit">
      <p>Trạng thái hồ sơ: {{ isCorrection ? 'Đang đính chính' : visit.status }}</p>
      <section v-if="canUnlock" aria-label="Mở đính chính hồ sơ">
        <h2>Mở đính chính hồ sơ</h2>
        <p>Hóa đơn chưa thu tiền hiện tại sẽ được hủy và giữ trong lịch sử. Đính chính không chiếm ghế, không hiện TV và không chặn ca đang khám. Hóa đơn mới dùng mã mới.</p>
        <label>Lý do mở khóa (bắt buộc, 10–500 ký tự)
          <textarea v-model="unlockReason" minlength="10" maxlength="500" :disabled="busy || discountBusy" />
        </label>
        <button :disabled="busy || discountBusy || unlockReason.trim().length < 10" @click="unlock">Xác nhận mở khóa và hủy hóa đơn chưa thu</button>
      </section>
      <section v-if="isCorrection" aria-label="Đính chính hồ sơ ngoài ca khám">
        <h2>Đính chính hồ sơ</h2>
        <p>Chế độ sửa hồ sơ riêng: không chiếm ghế, không gọi bệnh nhân trên TV và không chặn ca khám khác.</p>
        <form v-if="canCorrect" @submit.prevent="saveCorrection">
          <label>Chẩn đoán <textarea v-model="correctionDiagnosis" required maxlength="1000" :disabled="busy || discountBusy" /></label>
          <label>Ghi chú lâm sàng <textarea v-model="correctionNotes" maxlength="2000" :disabled="busy || discountBusy" /></label>
          <p v-if="correctionDirty">Có thay đổi chưa lưu. Lưu hoặc tải lại trước khi hoàn tất và chuyển màn hình.</p>
          <button :disabled="busy || discountBusy || !correctionDirty">Lưu đính chính chẩn đoán</button>
        </form>
        <p v-if="correctionNeedsReview" role="alert">Tải lại hồ sơ và đối chiếu trước khi tiếp tục đính chính.</p>
        <RouterLink v-if="canCorrect && !busy && !correctionDirty" :to="`/clinical/visits/${visitId}/fdi`">Đính chính tình trạng răng</RouterLink>
      </section>
      <template v-if="canComplete">
        <RouterLink v-if="!busy && !discountBusy" :to="`/clinical/visits/${visitId}/services`">Kiểm tra dịch vụ đã chỉ định</RouterLink>
        <p>{{ isCorrection ? 'Hoàn tất đính chính sẽ khóa lại hồ sơ và chốt hóa đơn mới.' : 'Kết thúc khám sẽ khóa hồ sơ và chốt hóa đơn nháp từ các dịch vụ đã ghi nhận.' }}</p>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy || discountBusy" /> {{ isCorrection ? 'Tôi đã kiểm tra nội dung và xác nhận hoàn tất đính chính' : 'Tôi đã kiểm tra chỉ định và xác nhận hoàn tất khám' }}</label>
        <button :disabled="busy || discountBusy || !confirmed" @click="complete">{{ isCorrection ? 'Hoàn tất đính chính và khóa hồ sơ' : 'Kết thúc khám và tạo hóa đơn nháp' }}</button>
      </template>
      <p v-else-if="!invoice">Lần khám chưa có hóa đơn nháp. Nha sĩ phụ trách cần hoàn tất khám.</p>
    </template>
    <section v-if="invoice" aria-label="Chi tiết hóa đơn nháp">
      <h2>{{ invoice.invoiceCode }}</h2>
      <p>Trạng thái: {{ statuses[invoice.status] || invoice.status }}</p>
      <p>Ngày tạo: {{ new Date(invoice.createdAt).toLocaleString('vi-VN') }}</p>
      <p v-if="!invoice.items.length">Không có dịch vụ tính phí được ghi nhận trong lần khám.</p>
      <ul>
        <li v-for="(item, index) in invoice.items" :key="index">
          <strong>{{ item.code }} · {{ item.name }}</strong>
          <p>{{ item.toothNumber ? `Răng ${item.toothNumber} · ${item.surface || 'Toàn răng'}` : 'Toàn hàm / tổng quát' }}</p>
          <p>{{ item.quantity }} × {{ money(item.unitPrice) }} = {{ money(item.totalAmount) }} (giá trị gốc)<span v-if="item.refundedQuantity"> · Đã hủy {{ item.refundedQuantity }} / còn {{ item.quantity - item.refundedQuantity }}</span></p>
        </li>
      </ul>
      <p>Tổng giá niêm yết: {{ money(invoice.totalAmount) }}</p>
      <p>Giảm giá: {{ money(invoice.discountAmount) }}<span v-if="invoice.discountReason"> · {{ invoice.discountReason }}</span></p>
      <strong>Phải thu sau giảm: {{ money(invoice.finalAmount) }}</strong>
      <p>Đã thu: {{ money(invoice.paidAmount) }} · Còn lại: {{ money(invoice.remainingAmount) }}</p>
      <p>Dòng gốc giữ nguyên giá đã ghi nhận. Sau hoàn, tổng tiền phía dưới là phần còn lại.</p>
      <InvoiceDiscountPanel v-if="auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST'"
        :invoice="invoice" :role="auth.role" @applied="load" @refresh="load" @busy="discountBusy = $event" />
      <InvoiceCashPayment v-if="!discountBusy && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST')"
        :invoice-id="invoice.id" :remaining-amount="invoice.remainingAmount" :status="invoice.status" @paid="onPaymentReceived" />
      <InvoiceBankTransfer v-if="!discountBusy && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST') && [1, 2, 5].includes(invoice.status) && invoice.remainingAmount > 0"
        :invoice-id="invoice.id" @paid="onBankReceived" />
    </section>
    <InvoiceRefundProposalPanel v-if="invoice && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST')" :invoice="invoice" @changed="load" />
    <InvoiceRefundPanel v-if="invoice && auth.role === 'ADMIN'" :invoice-id="invoice.id" :blocked="busy || discountBusy" @changed="load" />
    <section v-if="auth.role === 'ADMIN' && unlockHistory.length" aria-label="Lịch sử mở khóa">
      <h2>Lịch sử mở khóa</h2>
      <ul><li v-for="record in unlockHistory" :key="record.id">
        {{ new Date(record.createdAt).toLocaleString('vi-VN') }} · Admin #{{ record.actorUserId }} ·
        Hóa đơn đã hủy #{{ record.cancelledInvoiceId ?? 'Không có' }}
        <p>{{ record.reason }}</p>
      </li></ul>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { storeToRefs } from 'pinia';
import axios from 'axios';
import { visitsService } from '@/services/visits';
import { invoiceDraftApi } from '@/services/invoiceDraft';
import type { VisitUnlockHistory } from '@/services/invoiceDraft';
import InvoiceRefundPanel from '@/components/InvoiceRefundPanel.vue';
import InvoiceRefundProposalPanel from '@/components/InvoiceRefundProposalPanel.vue';
import InvoiceCashPayment from '@/components/InvoiceCashPayment.vue';
import InvoiceBankTransfer from '@/components/InvoiceBankTransfer.vue';
import InvoiceDiscountPanel from '@/components/InvoiceDiscountPanel.vue';
import type { BankTransferResponse } from '@/services/bankTransfers';
import type { CashPaymentResponse } from '@/services/cashPayments';
import { useInvoiceDraftStore } from '@/stores/invoiceDraft';
import { useAuthStore } from '@/stores/auth';
import type { VisitDetails } from '@/services/patients';

const route = useRoute();
const auth = useAuthStore();
const store = useInvoiceDraftStore();
const { invoice } = storeToRefs(store);
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const busy = ref(false);
const discountBusy = ref(false);
const error = ref('');
const success = ref('');
const confirmed = ref(false);
const unlockReason = ref('');
const correctionDiagnosis = ref(''), correctionNotes = ref(''), correctionNeedsReview = ref(false);
const isCorrection = computed(() => visit.value?.status === 'Correcting');
const correctionDirty = computed(() => isCorrection.value && (correctionDiagnosis.value !== (visit.value?.diagnosis ?? '') || correctionNotes.value !== (visit.value?.clinicalNotes ?? '')));
const canCorrect = computed(() => isCorrection.value && !visit.value?.isLocked && !correctionNeedsReview.value &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId));
const unlockHistory = ref<VisitUnlockHistory[]>([]);
const canUnlock = computed(() => auth.role === 'ADMIN' && visit.value?.status === 'Completed' && visit.value.isLocked &&
  (!invoice.value || invoice.value.paidAmount === 0 && [0, 1].includes(invoice.value.status)));
const statuses: Record<number, string> = { 0: 'Nháp', 1: 'Chờ thanh toán', 2: 'Thanh toán một phần', 3: 'Đã thanh toán', 4: 'Đã hủy', 5: 'Đã hoàn một phần', 6: 'Đã hoàn toàn bộ' };
const canComplete = computed(() => ['InProgress', 'Correcting'].includes(visit.value?.status ?? '') && !visit.value?.isLocked && !correctionDirty.value && !correctionNeedsReview.value &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
function message(cause: unknown) {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể tải hoặc tạo hóa đơn nháp.' : 'Không thể xử lý yêu cầu.';
}
async function load() {
  const ticket = ++version;
  busy.value = true; error.value = ''; success.value = ''; visit.value = null; store.replace(null); confirmed.value = false; unlockReason.value = ''; unlockHistory.value = []; correctionDiagnosis.value = ''; correctionNotes.value = ''; correctionNeedsReview.value = false;
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) { error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return; }
  try {
    const details = await visitsService.getById(visitId.value);
    if (ticket !== version) return;
    visit.value = details; correctionDiagnosis.value = details.diagnosis ?? ''; correctionNotes.value = details.clinicalNotes ?? '';
    try {
      const data = await invoiceDraftApi.get(visitId.value);
      if (ticket === version) store.replace(data);
    } catch (cause) {
      if (!axios.isAxiosError(cause) || cause.response?.status !== 404) throw cause;
    }
    if (ticket === version && auth.role === 'ADMIN') {
      const records = await invoiceDraftApi.unlockHistory(visitId.value);
      if (ticket === version) unlockHistory.value = records;
    }
  } catch (cause) { if (ticket === version) { error.value = message(cause); visit.value = null; } }
  finally { if (ticket === version) busy.value = false; }
}
async function onPaymentReceived(payment: CashPaymentResponse) {
  const expectedVersion = version + 1;
  await load();
  if (version === expectedVersion)
    success.value = `Đã ghi nhận khoản thu #${payment.id}: ${money(payment.amount)}. Tiền thối: ${money(payment.changeAmount ?? 0)}.`;
}
async function onBankReceived(payment: BankTransferResponse | null) {
  const expectedVersion = version + 1; await load();
  if (version === expectedVersion) success.value = payment
    ? `Đã ghi nhận MÔ PHỎNG #${payment.id}: ${money(payment.amount)} vào hóa đơn. ${payment.note ?? ''}`
    : 'Đã có khoản thu mới. Đối chiếu hóa đơn và lịch sử khoản thu bên dưới.';
}
async function unlock() {
  const reason = unlockReason.value.trim();
  if (!canUnlock.value || busy.value || discountBusy.value || reason.length < 10 || reason.length > 500) return;
  const ticket = version; const oldCode = invoice.value?.invoiceCode;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.unlock(visitId.value, reason);
    if (ticket !== version) return;
    const refreshVersion = version + 1; await load();
    if (refreshVersion === version && visit.value?.status === 'Correcting')
      success.value = oldCode ? `Đã mở đính chính và hủy hóa đơn ${oldCode}. Hóa đơn mới dùng mã mới.` : 'Đã mở đính chính hồ sơ.';
  } catch (cause) {
    if (ticket !== version) return;
    const failureMessage = message(cause); const refreshVersion = version + 1;
    await load(); if (refreshVersion === version) error.value = failureMessage;
  } finally { if (ticket === version) busy.value = false; }
}
async function saveCorrection() {
  if (!canCorrect.value || !correctionDirty.value || busy.value || discountBusy.value) return;
  if (!correctionDiagnosis.value.trim() || correctionDiagnosis.value.length > 1000 || correctionNotes.value.length > 2000) {
    error.value = 'Nhập chẩn đoán tối đa 1000 ký tự và ghi chú tối đa 2000 ký tự.'; return;
  }
  const ticket = version, expectedId = visitId.value;
  busy.value = true; confirmed.value = false; error.value = ''; success.value = '';
  try {
    const result = await visitsService.updateDiagnosis(expectedId, { diagnosis: correctionDiagnosis.value.trim(), clinicalNotes: correctionNotes.value.trim() || null });
    if (ticket !== version) return;
    if (result.visitId !== expectedId || result.status !== 'Correcting' || result.isLocked) throw new Error('Correction changed');
    visit.value = result; correctionDiagnosis.value = result.diagnosis ?? ''; correctionNotes.value = result.clinicalNotes ?? '';
    success.value = 'Đã lưu đính chính chẩn đoán. Kiểm tra dịch vụ trước khi hoàn tất.';
  } catch (cause) {
    if (ticket === version) { error.value = message(cause); correctionNeedsReview.value = true; }
  } finally { if (ticket === version) busy.value = false; }
}
async function complete() {
  if (!confirmed.value || !canComplete.value || busy.value || discountBusy.value) return;
  const ticket = version, completingCorrection = isCorrection.value; busy.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.complete(visitId.value);
    if (ticket !== version) return;
    const refreshVersion = version + 1;
    await load();
    if (refreshVersion === version && invoice.value && visit.value?.status === 'Completed' && visit.value.isLocked) success.value = completingCorrection ? 'Đã hoàn tất đính chính, khóa hồ sơ và chốt hóa đơn chờ thanh toán.' : 'Đã kết thúc khám và tạo hóa đơn chờ thanh toán. Hồ sơ đã được khóa.';
  } catch (cause) {
    if (ticket !== version) return;
    const failureMessage = message(cause);
    const refreshVersion = version + 1;
    await load(); if (refreshVersion === version) error.value = failureMessage;
  } finally { if (ticket === version) busy.value = false; }
}
watch(visitId, load, { immediate: true });
onUnmounted(() => { version++; store.replace(null); visit.value = null; correctionDiagnosis.value = ''; correctionNotes.value = ''; unlockReason.value = ''; unlockHistory.value = []; error.value = ''; success.value = ''; });
</script>

<style scoped>
.invoice-page { max-width: 900px; margin: 2rem auto; padding: 1rem; }
button { margin: .6rem; padding: .6rem; }
label { display: block; margin: 1rem 0; }
li { margin: 1rem 0; }
.error { color: #b91c1c; }
</style>
