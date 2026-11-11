<template>
 <section class="lifecycle" aria-label="Hủy đơn và lịch sử trong lần khám">
  <form v-if="mayCancel" @submit.prevent="cancelOrder">
   <h2>Hủy riêng đơn {{ prescription?.prescriptionCode }}</h2>
   <p>Giữ ca khám và dịch vụ, bỏ tiền thuốc của đơn này khỏi hóa đơn nháp. Đơn cũ giữ trong lịch sử; có thể kê đơn thay thế với mã mới. Các thay đổi chưa lưu trên màn hình sẽ bị bỏ khi tải lại.</p>
   <p v-if="colleagueOrder">Đơn do nha sĩ khác kê. Hủy đơn này rồi kê đơn thay thế mang tên và chữ ký của bạn; đơn cũ giữ nguyên người kê và nội dung.</p>
   <label v-if="colleagueOrder">Lý do hủy để kê thay thế (bắt buộc)
    <textarea v-model="reason" required maxlength="500" :disabled="busy || blocked || needsReview" @input="confirmed = false" />
   </label>
   <label><input v-model="confirmed" type="checkbox" :disabled="busy || blocked || needsReview" /> Tôi xác nhận hủy đúng đơn đã lưu ở trên</label>
   <button :disabled="busy || blocked || needsReview || !confirmed || (colleagueOrder && !reason.trim())">Hủy riêng đơn thuốc</button>
  </form>
  <p v-if="error" role="alert">{{ error }}</p>
  <p v-if="message" role="status">{{ message }}</p>
  <button v-if="needsReview" :disabled="busy || blocked" @click="reload">Tải lại hồ sơ để đối chiếu</button>
  <h2>Lịch sử đơn trong lần khám</h2>
  <button :disabled="busy || blocked" @click="loadHistory(1)">Xem / tải lại lịch sử</button>
  <template v-if="history">
   <p v-if="!history.items.length">Chưa có đơn thuốc đã lưu.</p>
   <ol><li v-for="order in history.items" :key="order.id"><details>
    <summary>{{ order.prescriptionCode }} · {{ order.isCancelled ? 'ĐÃ HỦY — không dùng đơn này' : 'Đơn hiện hành' }}</summary>
    <p>Ngày kê: {{ date(order.createdAt) }} · Mã nha sĩ kê: {{ order.doctorId }}<span v-if="order.isCancelled"> · Hủy lúc: {{ date(order.updatedAt) }}</span></p>
    <p>Chẩn đoán: {{ order.diagnosis }}</p>
    <ul><li v-for="line in order.items" :key="line.id">{{ line.medicineName }} {{ line.strength || '' }} — {{ line.quantity }} {{ line.unit }}<br />Cách dùng: {{ line.usageInstructions }}<span v-if="line.treatmentDays"> · {{ line.treatmentDays }} ngày</span></li></ul>
    <p v-if="order.doctorAdvice">Lời dặn: {{ order.doctorAdvice }}</p><p v-if="order.revisitDate">Hẹn tái khám: {{ order.revisitDate }}</p>
   </details></li></ol>
   <button :disabled="busy || blocked || history.page <= 1" @click="loadHistory(history.page - 1)">Trước</button>
   <span> Trang {{ history.page }} / {{ Math.max(1, history.totalPages) }} </span>
   <button :disabled="busy || blocked || !history.hasNextPage" @click="loadHistory(history.page + 1)">Sau</button>
  </template>
 </section>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { prescriptionsService, type Prescription } from '@/services/prescriptions';
import type { PagedResult } from '@/services/patients';
const props = defineProps<{ visitId: number; prescription: Prescription | null; canCancel: boolean; blocked: boolean }>();
const emit = defineEmits<{ changed: [] }>(), auth = useAuthStore();
const history = ref<PagedResult<Prescription> | null>(null), busy = ref(false), error = ref(''), message = ref(''), confirmed = ref(false), needsReview = ref(false);
const reason = ref('');
let generation = 0, disposed = false;
const identity = () => props.visitId + '/' + props.prescription?.id + '/' + props.prescription?.version + '/' + auth.user?.userId + '/' + auth.role + '/' + props.canCancel;
const mayRead = computed(() => Number.isSafeInteger(props.visitId) && props.visitId > 0 && ['ADMIN','RECEPTIONIST','DENTIST','ASSISTANT','PATIENT'].includes(auth.role));
const colleagueOrder = computed(() => !!props.prescription && props.prescription.doctorId !== auth.user?.userId);
const mayCancel = computed(() => { const order = props.prescription; return !!order && mayRead.value && props.canCancel && auth.role === 'DENTIST' && order.isCancelled === false && !order.dispensedAt; });
function clear() { history.value = null; confirmed.value = false; reason.value = ''; error.value = ''; message.value = ''; }
function alive(ticket: number, actor: string) { return !disposed && generation === ticket && identity() === actor; }
function date(value: string | null) { if (!value) return '—'; const d = new Date(value); return Number.isFinite(d.getTime()) ? d.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—'; }
async function loadHistory(page = 1) {
 if (disposed || busy.value || props.blocked || !mayRead.value || !Number.isInteger(page) || page < 1 || page > 1000000) return;
 const ticket = ++generation, actor = identity(); history.value = null; error.value = ''; busy.value = true;
 try {
  const data = await prescriptionsService.history(props.visitId, page); if (!alive(ticket, actor)) return;
  if (data.page !== page || data.pageSize !== 10 || !Array.isArray(data.items) || data.items.some(o => o.visitId !== props.visitId || !Array.isArray(o.items))) throw Error('Invalid history');
  history.value = data;
 } catch { if (alive(ticket, actor)) error.value = 'Không tải được lịch sử hoặc không có quyền xem lần khám này.'; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function cancelOrder() {
 const order = props.prescription;
 if (disposed || busy.value || props.blocked || needsReview.value || !mayCancel.value || !confirmed.value || !order || !Number.isInteger(order.id) || order.id <= 0 || !Number.isInteger(order.version) || order.version < 1 || order.version >= 2147483647) return;
 const cancellationReason = colleagueOrder.value ? reason.value.trim() : undefined;
 if (reason.value.length > 500 || colleagueOrder.value && !cancellationReason) { error.value = 'Nhập lý do hủy, tối đa 500 ký tự.'; return; }
 const ticket = ++generation, actor = identity(), expected = order.version + 1, id = order.id;
 clear(); busy.value = true;
 try {
  const result = await prescriptionsService.cancel(props.visitId, id, order.version, cancellationReason); if (!alive(ticket, actor)) return;
  if (result.visitId !== props.visitId || result.prescriptionId !== id || result.version !== expected || !Number.isInteger(result.invoiceId) || result.invoiceId <= 0 || !/^[0-9]+$/.test(result.invoiceTotal)) throw Error('Invalid cancellation');
  needsReview.value = true; message.value = 'Đã hủy đơn và cập nhật hóa đơn. Tải lại để kê đơn mới nếu cần.';
  busy.value = false; emit('changed');
 } catch { if (alive(ticket, actor)) { needsReview.value = true; error.value = 'Chưa xác minh kết quả hủy hoặc trạng thái đã đổi. Tải lại đơn và hóa đơn trước khi thao tác tiếp; không tự gửi lại.'; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
function reload() { if (!disposed && !busy.value && !props.blocked) { clear(); emit('changed'); } }
watch(identity, () => { generation++; busy.value = false; needsReview.value = false; clear(); });
onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>
.lifecycle {
  margin: 1.25rem 0;
  padding: 1.25rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

h2 {
  color: #0f2d59;
  font-size: 1.15rem;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

textarea {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 80px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

textarea:focus-visible {
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

p, li {
  line-height: 1.6;
}

li {
  margin: 0.6rem 0;
}

summary {
  cursor: pointer;
  font-weight: 600;
  color: #0f2d59;
  padding: 0.4rem 0;
}

[role="alert"] {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 0.75rem 0;
}

@media (max-width: 640px) {
  button {
    width: 100%;
    margin-right: 0;
  }
}
</style>
