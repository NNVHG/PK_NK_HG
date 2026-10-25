<template>
 <section aria-label="In đơn thuốc A5"><button ref="trigger" :disabled="busy || printing || blocked || !mayRead" @click="prepare(false)">Xem phôi A5</button> <button :disabled="busy || printing || blocked || !mayRead" @click="prepare(true)">In A5</button>
 <p v-if="error" role="alert">{{ error }}</p><p v-if="busy" role="status">Đang lấy dữ liệu và ảnh chữ ký hiện tại…</p>
 <p>Ảnh chữ ký lấy theo hồ sơ bác sĩ ở mỗi lần chuẩn bị in. Hộp thoại trình duyệt cho phép in hoặc lưu PDF.</p>
 </section>
 <Teleport to="body"><div v-if="printDoc" class="mod-rx-print-root" role="dialog" aria-modal="true" aria-label="Phôi in đơn thuốc A5" @keydown.esc="close">
  <div class="mod-rx-print-controls"><button ref="closer" @click="close">Đóng phôi</button> <button :disabled="busy || printing || blocked" @click="prepare(true)">In A5 — lấy lại dữ liệu hiện tại</button><p v-if="error" role="alert">{{ error }}</p></div>
  <article ref="sheet" class="mod-rx-print-sheet">
   <header><div class="mod-rx-brand-block"><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 128 128" class="mod-rx-brand-logo" role="img" aria-label="Logo Nha khoa Hoàng Gia"><g transform="translate(4 0) scale(1.2)"><path fill="#1857B6" d="M20 34 C27 27 36 27 49 31 C62 27 72 26 80 34 C93 46 82 69 77 81 C71 98 66 104 59 96 C55 91 57 73 50 73 C43 73 45 91 41 96 C34 104 29 96 23 81 C18 67 7 47 20 34 Z"/><path fill="#E5B95E" d="M24 24 L21 9 L39 16 L50 5 L61 16 L79 9 L76 24 Z"/><path d="M30 28 H70" stroke="#E5B95E" stroke-width="4" stroke-linecap="round"/><path d="M37 49 Q50 60 63 49" fill="none" stroke="#FFFFFF" stroke-width="5.6" stroke-linecap="round"/></g></svg><div><h1>{{ printDoc.clinicName }}</h1><p>Địa chỉ: {{ printDoc.clinicAddress }}</p><p>Hotline: {{ printDoc.clinicPhone }}</p></div></div><figure><img :src="qrImage" alt="QR tra cứu đơn" /><figcaption>{{ printDoc.prescriptionCode }}</figcaption></figure></header>
   <h2>ĐƠN THUỐC</h2>
   <section><p><strong>Họ tên: {{ printDoc.patientName.toLocaleUpperCase('vi-VN') }}</strong></p><p>Năm sinh: {{ printDoc.dateOfBirth.slice(0,4) }} · Giới tính: {{ gender(printDoc.gender) }}</p><p>Địa chỉ: {{ printDoc.patientAddress || 'Chưa ghi nhận' }}</p><p>Số điện thoại: {{ printDoc.patientPhone || 'Chưa ghi nhận' }}</p><p v-if="printDoc.receiverName || printDoc.receiverPhone">Người nhận thuốc thay: {{ printDoc.receiverName || 'Chưa ghi nhận' }} · SĐT: {{ printDoc.receiverPhone || 'Chưa ghi nhận' }}</p><p><strong>Chẩn đoán:</strong> {{ printDoc.diagnosis }}</p></section>
   <table><thead><tr><th>STT</th><th>Thuốc / hàm lượng / cách dùng</th><th>ĐVT</th><th>SL</th></tr></thead><tbody><tr v-for="(line,index) in printDoc.items" :key="index"><td>{{ index + 1 }}</td><td><strong>{{ line.medicineName }} {{ line.strength || '' }}</strong><p>{{ line.usageInstructions }}</p><p v-if="line.treatmentDays">Số ngày dùng: {{ line.treatmentDays }}</p></td><td>{{ line.unit }}</td><td>{{ line.quantity }}</td></tr></tbody></table>
   <section><p><strong>Lời dặn:</strong> {{ printDoc.doctorAdvice || 'Không có lời dặn bổ sung' }}</p><p v-if="printDoc.revisitDate"><strong>Hẹn tái khám:</strong> {{ date(printDoc.revisitDate) }}</p></section>
   <footer><p class="mod-rx-date">Ngày kê đơn: {{ date(printDoc.prescribedDate) }}</p><div class="mod-rx-signatures"><div><strong>Người nhận thuốc</strong><div class="mod-rx-sign-space"></div><p>{{ printDoc.receiverName || '' }}</p></div><div><strong>Bác sĩ điều trị</strong><div class="mod-rx-sign-space"><img :src="signatureImage" alt="Ảnh chữ ký bác sĩ" /></div><p><strong>{{ printDoc.doctorTitle }} {{ printDoc.doctorName }}</strong></p></div></div><p>Đơn thuốc có giá trị mua trong vòng 5 ngày kể từ ngày kê đơn</p><small>Quét QR và nhập CCCD của chủ đơn để tra cứu.</small></footer>
  </article>
 </div></Teleport>
</template>
<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { prescriptionPrintService, MOD_RX_signatureDataUrl, MOD_RX_validPrint, type PrescriptionPrintDocument } from '@/services/prescriptionPrint';
import { MOD_RX_lookupQr } from '@/services/prescriptionQr';
const props = defineProps<{ visitId: number; prescriptionId: number; prescriptionVersion: number; blocked: boolean }>(), auth = useAuthStore();
const printDoc = ref<PrescriptionPrintDocument | null>(null), qrImage = ref(''), signatureImage = ref(''), busy = ref(false), printing = ref(false), error = ref('');
const sheet = ref<HTMLElement | null>(null), trigger = ref<HTMLButtonElement | null>(null), closer = ref<HTMLButtonElement | null>(null);
let generation = 0, disposed = false;
const identity = () => props.visitId + '/' + props.prescriptionId + '/' + props.prescriptionVersion + '/' + auth.user?.userId + '/' + auth.role;
const mayRead = computed(() => ['ADMIN','DENTIST','RECEPTIONIST','ASSISTANT','PATIENT'].includes(auth.role));
function clear() { printDoc.value = null; qrImage.value = ''; signatureImage.value = ''; printing.value = false; document.body.classList.remove('mod-rx-printing'); }
function close() { generation++; busy.value = false; clear(); trigger.value?.focus(); }
function alive(n: number, id: string) { return !disposed && generation === n && id === identity() && !props.blocked; }
function date(s: string) { return /^\d{4}-\d{2}-\d{2}$/.test(s) ? s.slice(8,10) + '/' + s.slice(5,7) + '/' + s.slice(0,4) : '—'; }
function gender(s: string | null) { return s === 'Male' ? 'Nam' : s === 'Female' ? 'Nữ' : s === 'Other' ? 'Khác' : 'Chưa ghi nhận'; }
async function prepare(toPrinter = false) {
 if (disposed || busy.value || printing.value || props.blocked || !mayRead.value || [props.visitId,props.prescriptionId,props.prescriptionVersion].some(n => !Number.isInteger(n) || n < 1 || n > 2147483647)) return;
 const n = ++generation, id = identity(); clear(); error.value = ''; busy.value = true;
 try {
  const data = await prescriptionPrintService.prepare(props.visitId, props.prescriptionId, props.prescriptionVersion); if (!alive(n,id)) return;
  if (!MOD_RX_validPrint(data,props.visitId,props.prescriptionId,props.prescriptionVersion)) throw Error('Invalid print document');
  const signature = MOD_RX_signatureDataUrl(data.signaturePngBase64), qr = await MOD_RX_lookupQr(data.relativeLookupPath,window.location.origin); if (!alive(n,id)) return;
  printDoc.value = data; signatureImage.value = signature; qrImage.value = qr.image; await nextTick();
  if (!alive(n,id)) return;
  if (!sheet.value) throw Error('Missing print sheet');
  for (const img of Array.from(sheet.value.querySelectorAll('img'))) await img.decode();
  if (!alive(n,id)) return;
  if (toPrinter) { document.body.classList.add('mod-rx-printing'); printing.value = true; window.print(); }
  else closer.value?.focus();
 } catch { if (!disposed && generation === n && id === identity()) { clear(); error.value = 'Không chuẩn bị được bản in: đơn có thể đã đổi/hủy, thiếu quyền hoặc thiếu thông tin phòng khám, học vị, ảnh chữ ký. Tải lại đơn và kiểm tra cấu hình trước khi in.'; } }
 finally { if (!disposed && generation === n && id === identity()) busy.value = false; }
}
function afterPrint() { if (printing.value) { clear(); trigger.value?.focus(); } }
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; });
watch(() => props.blocked, blocked => { if (blocked) { generation++; busy.value = false; clear(); } });
onMounted(() => window.addEventListener('afterprint',afterPrint));
onUnmounted(() => { disposed = true; generation++; clear(); window.removeEventListener('afterprint',afterPrint); });
</script>
<style>
.mod-rx-print-root { position:fixed;inset:0;z-index:1000;background:#e2e8f0;overflow:auto;padding:1rem; }
.mod-rx-print-controls { max-width:132mm;margin:0 auto 1rem; }.mod-rx-print-controls button { padding:.5rem; }
.mod-rx-print-sheet { width:148mm;min-height:210mm;box-sizing:border-box;max-width:100%;padding:8mm;margin:auto;background:white;color:black;font:10pt/1.35 Arial,sans-serif;overflow-wrap:anywhere; }
.mod-rx-print-sheet header { display:flex;justify-content:space-between;gap:3mm; }
.mod-rx-brand-block { display:flex;gap:3mm;align-items:flex-start; }
.mod-rx-brand-logo { flex-shrink:0;margin-top:1mm; }
.mod-rx-print-sheet h1 { font-size:13pt;margin:0; }.mod-rx-print-sheet h2 { text-align:center;font-size:15pt; }
.mod-rx-print-sheet p { margin:2mm 0;white-space:pre-wrap; }.mod-rx-print-sheet figure { margin:0;text-align:center;font-size:7pt;flex-shrink:0; }.mod-rx-print-sheet figure img { width:26mm;height:26mm;image-rendering:pixelated; }
.mod-rx-print-sheet table { width:100%;border-collapse:collapse; }.mod-rx-print-sheet th,.mod-rx-print-sheet td { border:1px solid #666;padding:2mm;text-align:left;vertical-align:top; }.mod-rx-print-sheet thead { display:table-header-group; }
.mod-rx-print-sheet tr,.mod-rx-print-sheet footer { break-inside:avoid; }.mod-rx-print-sheet footer { margin-top:4mm; }.mod-rx-date { text-align:right; }.mod-rx-signatures { display:grid;grid-template-columns:1fr 1fr;gap:4mm;text-align:center; }.mod-rx-sign-space { height:60px; }.mod-rx-sign-space img { height:60px;max-width:100%;object-fit:contain; }
@page mod-rx-a5 { size:A5 portrait;margin:8mm; }
@media print {
 .mod-rx-print-root { display:none!important; }
 body.mod-rx-printing > *:not(.mod-rx-print-root) { display:none!important; }
 body.mod-rx-printing > .mod-rx-print-root { display:block!important;position:static;inset:auto;padding:0;overflow:visible;background:white; }
 body.mod-rx-printing .mod-rx-print-controls { display:none!important; }
 body.mod-rx-printing .mod-rx-print-sheet { page:mod-rx-a5;width:100%;min-height:0;max-width:none;padding:0;margin:0; }
 body.mod-rx-printing { margin:0;padding:0;min-width:0;background:white; }
}
</style>
