<template>
  <button :disabled="busy" @click="open">Xem / in biên lai #{{ paymentId }}</button>
  <dialog ref="dialog" class="receipt-dialog" @close="cleanup" @cancel="cleanup">
    <div class="receipt-controls">
      <button @click="close">Đóng</button>
      <p v-if="busy" role="status">Đang tải biên lai…</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <button v-if="!receipt && !busy" @click="load">Tải lại biên lai</button>
      <p v-if="receipt && (!receipt.clinicAddress || !receipt.clinicPhone)" role="alert">Chưa cấu hình đủ địa chỉ/hotline phòng khám.</p>
      <p>Lượt in là yêu cầu mở hộp thoại in, kể cả khi bạn hủy hoặc máy in lỗi. Tải lại để đối chiếu nếu kết quả chưa rõ.</p>
      <button v-if="receipt" :disabled="busy" @click="print">{{ receipt.printCount ? 'In lại biên lai K80' : 'In biên lai K80' }}</button>
    </div>
    <article v-if="receipt" class="payment-receipt" aria-label="Biên lai K80">
      <header><h2>{{ receipt.clinicName }}</h2><p v-if="receipt.clinicAddress">{{ receipt.clinicAddress }}</p><p v-if="receipt.clinicPhone">Hotline: {{ receipt.clinicPhone }}</p></header>
      <h3>BIÊN LAI THU TIỀN</h3>
      <p v-if="receipt.printNumber > 1"><strong>[BẢN IN LẠI - LẦN {{ receipt.printNumber }}]</strong><br />Thời gian in lại: {{ date(receipt.printedAt) }}</p>
      <p v-if="receipt.source === 'Simulation'"><strong>MÔ PHỎNG — KHÔNG CÓ TIỀN NGÂN HÀNG THỰC</strong></p>
      <p>Hóa đơn: {{ receipt.invoiceCode }} · Giao dịch #{{ receipt.paymentId }}</p>
      <p>Ngày thu: {{ date(receipt.paidAt) }}<br />Thu ngân: {{ receipt.cashierName }}</p>
      <p>Bệnh nhân: {{ receipt.patientName }}<br />Mã: {{ receipt.patientCode }} · Năm sinh: {{ receipt.birthYear }}</p>
      <p>Chi tiết và tổng hóa đơn bên dưới là phần còn lại hiện tại sau điều chỉnh; khoản thu kỳ này giữ nguyên.</p>
      <table><thead><tr><th>Khoản mục</th><th>SL</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead>
        <tbody><tr v-for="(item, index) in receipt.items" :key="index"><td>{{ item.name }}</td><td>{{ item.quantity }}</td><td>{{ money(item.unitPrice) }}</td><td>{{ money(item.totalAmount) }}</td></tr></tbody>
      </table>
      <p>Tổng giá niêm yết: {{ money(receipt.subTotalAmount) }}<br />Giảm giá: {{ money(receipt.discountAmount) }}<br />Phải thanh toán: {{ money(receipt.finalAmount) }}<br />Phương thức: {{ receipt.paymentMethod === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản VietQR' }}</p>
      <p><strong>Thu kỳ này: {{ money(receipt.amount) }}</strong><br />{{ receipt.paymentMethod === 'Cash' ? 'Khách đưa' : 'Ngân hàng nhận' }}: {{ money(receipt.amountTendered) }}<br />{{ receipt.paymentMethod === 'Cash' ? 'Tiền thối' : 'Chuyển dư cần đối soát' }}: {{ money(receipt.changeAmount) }}</p>
      <p>Tổng đã thu hiện tại: {{ money(receipt.paidAmount) }}<br /><strong>Còn nợ: {{ money(receipt.remainingAmount) }}</strong></p>
      <p v-if="receipt.note">{{ receipt.note }}</p>
      <svg v-if="barcode" :viewBox="`0 0 ${barcode.width} 40`" class="receipt-barcode" role="img" :aria-label="`Mã tra cứu hóa đơn ${receipt.invoiceCode}`" shape-rendering="crispEdges">
        <rect :width="barcode.width" height="40" fill="white" />
        <rect v-for="(bar, index) in barcode.bars" :key="index" :x="bar.x" :width="bar.width" height="40" fill="black" />
      </svg>
      <p>Mã tra cứu: {{ receipt.invoiceCode }}</p>
      <footer>Cảm ơn Quý khách và hẹn gặp lại!</footer>
    </article>
  </dialog>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import { paymentReceiptsApi } from '@/services/paymentReceipts';
import type { ReceiptPrintDto } from '@/services/paymentReceipts';
import { MOD_BIL_receiptBarcode } from '@/services/receiptBarcode';
const props = defineProps<{ invoiceId: number; paymentId: number }>();
const dialog = ref<HTMLDialogElement | null>(null); const receipt = ref<ReceiptPrintDto | null>(null);
const busy = ref(false); const error = ref(''); let version = 0;
const barcode = computed(() => receipt.value ? MOD_BIL_receiptBarcode(receipt.value.invoiceCode) : null);
const money = (value: number | null) => value === null ? '—' : new Intl.NumberFormat('vi-VN').format(value) + ' VND';
const date = (value: string | null) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Sẽ ghi nhận khi yêu cầu in';
function finishPrint() { document.body.classList.remove('receipt-printing'); window.removeEventListener('afterprint', finishPrint); }
function cleanup() { version++; busy.value = false; finishPrint(); }
function close() { cleanup(); dialog.value?.close(); }
async function open() { dialog.value?.showModal(); await load(); }
async function load() {
  const ticket = ++version; busy.value = true; error.value = ''; receipt.value = null;
  try { const data = await paymentReceiptsApi.preview(props.invoiceId, props.paymentId); if (ticket === version) receipt.value = data; }
  catch { if (ticket === version) error.value = 'Không tải được biên lai. Kiểm tra quyền hoặc tải lại.'; }
  finally { if (ticket === version) busy.value = false; }
}
async function print() {
  if (busy.value || !receipt.value || !dialog.value?.open || props.invoiceId <= 0 || props.paymentId <= 0) return;
  const ticket = version; busy.value = true; error.value = '';
  try {
    const data = await paymentReceiptsApi.print(props.invoiceId, props.paymentId);
    if (ticket !== version || !dialog.value?.open) return;
    receipt.value = data; await nextTick();
    if (ticket !== version || !dialog.value?.open) return;
    document.body.classList.add('receipt-printing'); window.addEventListener('afterprint', finishPrint, { once: true }); window.print();
  } catch {
    finishPrint(); if (ticket === version) { receipt.value = null; error.value = 'Chưa xác định lượt in hoặc máy in lỗi. Tải lại biên lai để đối chiếu trước khi thử lại.'; }
  } finally { if (ticket === version) busy.value = false; }
}
watch(() => [props.invoiceId, props.paymentId], () => { close(); receipt.value = null; });
onUnmounted(cleanup);
</script>
<style>
.receipt-dialog { width: min(90vw, 500px); max-height: 85vh; overflow: auto; }
.receipt-dialog::backdrop { background: rgb(0 0 0 / .4); }
.payment-receipt { background: white; color: black; padding: 12px; font: 12px Arial, sans-serif; }
.payment-receipt header, .payment-receipt h3, .payment-receipt footer { text-align: center; }
.payment-receipt table { border-collapse: collapse; width: 100%; font-size: 10px; }
.payment-receipt td, .payment-receipt th { padding: 3px; border-bottom: 1px dotted #555; overflow-wrap: anywhere; }
.receipt-barcode { display: block; width: 100%; height: 15mm; }
@media print {
  @page { size: 80mm 297mm; margin: 3mm; }
  body.receipt-printing * { visibility: hidden !important; }
  body.receipt-printing .payment-receipt, body.receipt-printing .payment-receipt * { visibility: visible !important; }
  body.receipt-printing .receipt-dialog { position: absolute; inset: 0 auto auto 0; width: 74mm; margin: 0; padding: 0; border: 0; max-height: none; overflow: visible; }
  body.receipt-printing .receipt-dialog::backdrop { display: none; }
  body.receipt-printing .receipt-controls { display: none; }
  body.receipt-printing .payment-receipt { width: 74mm; padding: 0; }
  body.receipt-printing tr { break-inside: avoid; }
}
</style>
