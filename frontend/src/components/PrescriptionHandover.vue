<template>
 <section class="handover" aria-label="Bàn giao thuốc">
  <h2 ref="heading" tabindex="-1">Bàn giao thuốc</h2>
  <button :disabled="busy || blocked" @click="load">Tải lại thông tin bàn giao</button>
  <p role="status">{{ busy ? 'Đang xử lý…' : message }}</p>
  <p v-if="error" role="alert">{{ error }}</p>
  <p v-if="needsReview">Kết quả chưa được xác minh. Tải lại thông tin bàn giao để đối chiếu trước khi gửi tiếp.</p>
  <template v-if="status">
   <p><strong>{{ status.prescriptionCode }} · {{ status.patientName }}</strong></p>
   <p v-if="status.dispensedAt">Đã giao thuốc · {{ date(status.dispensedAt) }} · {{ status.dispensedByName || 'Chưa ghi nhận người giao' }}</p>
   <p v-else>Chưa giao thuốc · Hạn cấp phát: {{ date(status.expiresAt) }}</p>
   <p v-if="status.receivedByProxy">Người nhận thay: {{ status.receiverName || 'Chưa ghi nhận' }} · {{ status.receiverPhone || 'Chưa ghi nhận SĐT' }}</p>
   <p v-if="status.blockReason">{{ status.blockReason }}</p>
   <form v-if="mayWrite && (status.canDispense || status.canOverrideExpired)" @submit.prevent="save(false)">
    <p>Hóa đơn cần thanh toán đủ số tiền sau chiết khấu. Đối chiếu bệnh nhân, đơn đã lưu và số thuốc thực giao. Thao tác này chỉ ghi nhận bàn giao; không tự cập nhật kho.</p>
    <label><input v-model="proxy" type="checkbox" :disabled="busy || blocked || needsReview || status.receiverRequired" /> Người nhà / Người giám hộ nhận thay</label>
    <p v-if="status.receiverRequired">Bệnh nhân dưới 15 tuổi: bắt buộc họ tên và SĐT người nhận thay.</p>
    <template v-if="proxy">
     <label>Họ tên người nhận<input v-model="receiverName" name="receiver-name" autocomplete="off" maxlength="100" :required="status.receiverRequired" :disabled="busy || blocked || needsReview" /></label>
     <label>SĐT người nhận<input v-model="receiverPhone" name="receiver-phone" type="tel" inputmode="tel" autocomplete="off" maxlength="10" pattern="0[0-9]{9}" :required="status.receiverRequired" :disabled="busy || blocked || needsReview" /></label>
    </template>
    <template v-if="status.canOverrideExpired && auth.role === 'ADMIN'">
     <label><input v-model="overrideExpired" type="checkbox" :disabled="busy || blocked || needsReview" /> Admin xác nhận ngoại lệ cấp phát quá hạn</label>
     <label>Lý do ngoại lệ (5–500 ký tự)<input v-model="overrideReason" minlength="5" maxlength="500" required :disabled="busy || blocked || needsReview" /></label>
    </template>
    <label><input v-model="confirmed" type="checkbox" required :disabled="busy || blocked || needsReview" /> Tôi đã kiểm tra đúng người nhận và xác nhận đã giao thuốc</label>
    <button :disabled="busy || blocked || needsReview">Xác nhận đã cấp phát thuốc</button>
   </form>
   <form v-if="mayWrite && status.canUndo" @submit.prevent="save(true)">
    <h3>Hoàn tác bàn giao</h3>
    <p>Chỉ Admin hoặc chính người giao trong 30 phút. Giữ nguyên nội dung đơn và tiền đã thu; không tự hoàn tiền hoặc nhập lại kho.</p>
    <label>Lý do hoàn tác (5–500 ký tự)<input v-model="reason" required minlength="5" maxlength="500" :disabled="busy || blocked || needsReview" /></label>
    <label><input v-model="confirmed" type="checkbox" required :disabled="busy || blocked || needsReview" /> Tôi xác nhận hoàn tác đúng lần bàn giao này</label>
    <button :disabled="busy || blocked || needsReview">Hoàn tác bàn giao thuốc</button>
   </form>
  </template>
 </section>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { prescriptionHandoverService, MOD_RX_validHandover, type HandoverStatus } from '@/services/prescriptionHandover';
const props = defineProps<{ prescriptionId: number; visitId: number; prescriptionVersion: number; blocked: boolean }>();
const emit = defineEmits<{ changed: [] }>(), auth = useAuthStore();
const heading = ref<HTMLElement | null>(null);
const status = ref<HandoverStatus | null>(null), busy = ref(false), needsReview = ref(false), error = ref(''), message = ref('');
const proxy = ref(false), receiverName = ref(''), receiverPhone = ref(''), confirmed = ref(false), reason = ref(''), overrideExpired = ref(false), overrideReason = ref('');
let generation = 0, disposed = false;
const mayWrite = computed(() => ['ADMIN','RECEPTIONIST','ASSISTANT'].includes(auth.role));
const identity = () => [props.prescriptionId,props.visitId,props.prescriptionVersion,auth.user?.userId,auth.role].join('/');
const alive = (n: number, actor: string) => !disposed && n === generation && actor === identity();
function clear() { status.value = null; proxy.value = false; receiverName.value = ''; receiverPhone.value = ''; confirmed.value = false; reason.value = ''; overrideExpired.value = false; overrideReason.value = ''; message.value = ''; error.value = ''; }
function date(s: string) { return new Date(s).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }); }
async function load() {
 if (disposed || busy.value || props.blocked || !['ADMIN','RECEPTIONIST','ASSISTANT','DENTIST','PATIENT'].includes(auth.role) || [props.prescriptionId,props.visitId].some(x => !Number.isInteger(x) || x < 1)) return;
 const n = ++generation, actor = identity(); clear(); busy.value = true;
 try {
  const data = await prescriptionHandoverService.read(props.prescriptionId); if (!alive(n,actor)) return;
  if (!MOD_RX_validHandover(data,props.prescriptionId,props.visitId)) throw Error('Invalid handover');
  status.value = data; proxy.value = data.receiverRequired; needsReview.value = false;
 } catch { if (alive(n,actor)) { error.value = 'Không tải được thông tin bàn giao hoặc bạn không có quyền xem.'; needsReview.value = true; } }
 finally { if (alive(n,actor)) busy.value = false; }
}
async function save(undo: boolean) {
 const s = status.value;
 if (disposed || busy.value || props.blocked || needsReview.value || !s || !mayWrite.value) return;
 error.value = ''; message.value = '';
 if (!confirmed.value || undo && (!s.canUndo || reason.value.trim().length < 5 || reason.value.length > 500 || /[\x00-\x1f\x7f]/.test(reason.value)) ||
  !undo && (!(s.canDispense || s.canOverrideExpired && auth.role === 'ADMIN' && overrideExpired.value) ||
   s.receiverRequired && (!proxy.value || !receiverName.value.trim() || !receiverPhone.value.trim()) ||
   proxy.value && (receiverName.value.length > 100 || /[\x00-\x1f\x7f]/.test(receiverName.value) || !!receiverPhone.value.trim() && !/^0[0-9]{9}$/.test(receiverPhone.value.trim())) ||
   overrideExpired.value && (overrideReason.value.trim().length < 5 || overrideReason.value.length > 500 || /[\x00-\x1f\x7f]/.test(overrideReason.value)))) {
  error.value = 'Kiểm tra quyền, thời hạn, thông tin người nhận, lý do và ô xác nhận trước khi gửi.'; return;
 }
 const n = ++generation, actor = identity(); busy.value = true;
 try {
  const data = undo ? await prescriptionHandoverService.undo(props.prescriptionId,s.version,reason.value.trim()) : await prescriptionHandoverService.dispense(props.prescriptionId, {
   expectedVersion:s.version,confirmed:true,receivedByProxy:proxy.value,receiverName:proxy.value ? receiverName.value.trim() || null : null,
   receiverPhone:proxy.value ? receiverPhone.value.trim() || null : null,overrideExpired:overrideExpired.value,overrideReason:overrideExpired.value ? overrideReason.value.trim() : null });
  if (!alive(n,actor)) return;
  if (!MOD_RX_validHandover(data,props.prescriptionId,props.visitId) || data.version !== s.version + 1 || (undo ? data.dispensedAt !== null : data.dispensedAt === null)) throw Error('Unexpected handover result');
  clear(); status.value = data; proxy.value = data.receiverRequired; message.value = undo ? 'Đã hoàn tác bàn giao; tiền đã thu giữ nguyên.' : 'Đã ghi nhận bàn giao thuốc.'; await nextTick(); if (alive(n,actor)) { heading.value?.focus(); emit('changed'); }
 } catch { if (alive(n,actor)) { clear(); needsReview.value = true; error.value = 'Chưa xác minh được kết quả, đơn có thể đã đổi hoặc hết thời hạn. Tải lại để đối chiếu trước khi thao tác tiếp.'; } }
 finally { if (alive(n,actor)) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; needsReview.value = false; clear(); void load(); }, { immediate:true });
watch(() => props.blocked, blocked => { if (!blocked && !status.value) void load(); });
onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>
.handover { margin:1rem 0;padding:1rem;border:1px solid #64748b;border-radius:.5rem; }
.handover label { display:block;margin:.75rem 0; }.handover input:not([type=checkbox]) { display:block;max-width:100%;box-sizing:border-box;min-height:2.5rem; }.handover button { min-height:2.5rem; }
</style>
