<template>
 <section aria-label="Lịch sử đơn thuốc" class="rx-history">
  <h2 ref="heading" tabindex="-1">Đơn thuốc</h2>
  <button :disabled="busy" @click="load(1)">Tải lại danh sách đơn thuốc</button>
  <p role="status">{{ busy ? 'Đang tải…' : '' }}</p>
  <p v-if="error" role="alert">{{ error }}</p>
  <template v-if="history">
   <p v-if="!history.items.length">Chưa có đơn thuốc trong phạm vi được xem.</p>
   <ol v-else class="rx-history-list">
    <li v-for="row in history.items" :key="row.id">
     <h3>{{ row.prescriptionCode }}</h3>
     <p>{{ date(row.createdAt) }} · Nha sĩ: {{ row.doctorName }} · {{ row.medicineCount }} loại thuốc</p>
     <p>Chẩn đoán lần khám: {{ row.visitDiagnosis || 'Chưa ghi nhận' }}</p>
     <p>{{ row.isCancelled ? 'ĐÃ HỦY — chỉ xem lịch sử' : row.dispensedAt ? 'Đã giao thuốc' : 'Chưa giao thuốc' }}</p>
     <button :disabled="busy" @click="showDetail(row)">Xem chi tiết {{ row.prescriptionCode }}</button>
    </li>
   </ol>
   <nav aria-label="Phân trang đơn thuốc">
    <button :disabled="busy || !history.hasPreviousPage" @click="load(history.page - 1)">Trang trước</button>
    <span> Trang {{ history.page }} / {{ Math.max(1,history.totalPages) }} · {{ history.totalCount }} đơn </span>
    <button :disabled="busy || !history.hasNextPage" @click="load(history.page + 1)">Trang sau</button>
   </nav>
  </template>
  <section v-if="detail" ref="detailHeading" tabindex="-1" aria-label="Chi tiết đơn thuốc lịch sử" class="rx-history-detail">
   <h3>{{ detail.prescriptionCode }} · {{ detail.doctorName }}</h3>
   <button :disabled="busy" @click="closeDetail">Đóng chi tiết</button>
   <p>{{ date(detail.createdAt) }} · {{ detail.isCancelled ? 'ĐÃ HỦY' : detail.dispensedAt ? 'Đã giao thuốc' : 'Chưa giao thuốc' }}</p>
   <p>Chẩn đoán trên đơn: {{ detail.diagnosis }}</p>
   <ol><li v-for="(line,index) in detail.items" :key="index">
    <strong>{{ line.medicineName }} {{ line.strength || '' }}</strong> · {{ line.quantity }} {{ line.unit }}
    <p>{{ line.usageInstructions }}<span v-if="line.treatmentDays"> · {{ line.treatmentDays }} ngày</span></p>
   </li></ol>
   <p v-if="detail.doctorAdvice">Lời dặn: {{ detail.doctorAdvice }}</p>
   <p v-if="detail.revisitDate">Hẹn tái khám: {{ detail.revisitDate }}</p>
   <PrescriptionPrint v-if="detail.canPrint && !detail.isCancelled" :visit-id="detail.visitId" :prescription-id="detail.id" :prescription-version="detail.version" :blocked="busy" />
   <p v-else>Đơn hoặc lần khám đã hủy, không được in lại.</p>
  </section>
 </section>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import PrescriptionPrint from '@/components/PrescriptionPrint.vue';
import { patientPrescriptionHistoryService, MOD_RX_validHistoryRow, MOD_RX_validHistoryDetail,
 type PatientPrescriptionSummary, type PatientPrescriptionDetail } from '@/services/patientPrescriptionHistory';
import type { PagedResult } from '@/services/patients';
const props = defineProps<{patientId:number}>(), auth = useAuthStore();
const history = ref<PagedResult<PatientPrescriptionSummary> | null>(null), detail = ref<PatientPrescriptionDetail | null>(null);
const busy = ref(false), error = ref(''), heading = ref<HTMLElement | null>(null), detailHeading = ref<HTMLElement | null>(null);
let generation = 0, disposed = false;
const identity = () => [props.patientId,auth.user?.userId,auth.role].join('/');
const mayRead = computed(() => Number.isInteger(props.patientId) && props.patientId > 0 && props.patientId <= 2147483647 && ['ADMIN','RECEPTIONIST','ASSISTANT','DENTIST','PATIENT'].includes(auth.role));
const alive = (n:number, id:string) => !disposed && generation === n && identity() === id;
function date(s:string) { return new Date(s).toLocaleString('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'}); }
function clear() { history.value = null; detail.value = null; error.value = ''; }
async function load(page = 1) {
 if (disposed || busy.value || !mayRead.value || !Number.isInteger(page) || page < 1 || page > 1000000) return;
 const n = ++generation, id = identity(); clear(); busy.value = true;
 try {
  const data = await patientPrescriptionHistoryService.list(props.patientId,page); if (!alive(n,id)) return;
  if (!data || data.page !== page || data.pageSize !== 10 || !Number.isInteger(data.totalCount) || data.totalCount < 0 ||
   data.totalPages !== Math.ceil(data.totalCount / 10) || data.hasNextPage !== (page < data.totalPages) || data.hasPreviousPage !== (page > 1) ||
   !Array.isArray(data.items) || data.items.length > 10 || data.items.some(r => !MOD_RX_validHistoryRow(r,props.patientId))) throw Error('Invalid history');
  history.value = data; await nextTick(); if (alive(n,id) && page > 1) heading.value?.focus();
 } catch { if (alive(n,id)) error.value = 'Không tải được lịch sử đơn hoặc bạn không có quyền xem hồ sơ này.'; }
 finally { if (alive(n,id)) busy.value = false; }
}
async function showDetail(row:PatientPrescriptionSummary) {
 if (disposed || busy.value || !mayRead.value || !MOD_RX_validHistoryRow(row,props.patientId)) return;
 const n = ++generation, id = identity(); detail.value = null; error.value = ''; busy.value = true;
 try {
  const data = await patientPrescriptionHistoryService.detail(props.patientId,row.id); if (!alive(n,id)) return;
  if (!MOD_RX_validHistoryDetail(data,props.patientId,row.id) || data.visitId !== row.visitId) throw Error('Invalid detail');
  detail.value = data; await nextTick(); if (alive(n,id)) detailHeading.value?.focus();
 } catch { if (alive(n,id)) error.value = 'Không mở được đơn: dữ liệu hoặc quyền đọc có thể đã thay đổi. Tải lại danh sách để đối chiếu.'; }
 finally { if (alive(n,id)) busy.value = false; }
}
function closeDetail() { if (!busy.value) { generation++; detail.value = null; heading.value?.focus(); } }
watch(identity, () => { generation++; busy.value = false; clear(); void load(1); }, {immediate:true});
onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>
.rx-history-list { padding:0;list-style:none; }.rx-history-list>li,.rx-history-detail { border:1px solid #64748b;border-radius:.5rem;padding:1rem;margin:1rem 0;overflow-wrap:anywhere; }
.rx-history button { min-height:2.5rem; }.rx-history nav { display:flex;align-items:center;gap:.5rem;flex-wrap:wrap; }
</style>
