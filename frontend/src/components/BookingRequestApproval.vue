<template>
  <section class="approval" aria-labelledby="booking-approval-heading">
    <h2 id="booking-approval-heading">Đối soát và xác nhận lịch</h2>
    <button type="button" :disabled="busy" @click="load">Đối chiếu lại yêu cầu</button>
    <p v-if="error" role="alert">{{ error }}</p>
    <p role="status" aria-live="polite">{{ message }}</p>
    <template v-if="context && !context.result">
      <p>{{ context.fullName }} · {{ context.dateOfBirth }} · {{ context.phone }}</p>
      <p>{{ context.appointmentDate }} {{ context.slotTime.slice(0, 5) }} · {{ context.slotDurationMinutes }} phút · {{ context.dentistName || 'Chưa phân công nha sĩ' }}</p>
      <p>Tài khoản bệnh nhân được tạo hoặc chuyển tại quầy sau khi lễ tân đối chiếu CCCD và số điện thoại lúc check-in.</p>
      <p v-if="context.blockReason" role="alert">{{ context.blockReason }}</p>
      <form v-else @submit.prevent="approve">
        <label>Hồ sơ nhận lịch
          <select v-model="selection" :disabled="busy || needsReview || context.verifiedPatientId !== null" required>
            <option value="">Chọn sau khi đối soát</option>
            <option v-for="p in context.patients" :key="p.patientId" :value="String(p.patientId)" :disabled="!p.canSelect">{{ p.fullName }} · {{ p.dateOfBirth }} · #{{ p.patientId }}{{ p.canSelect ? '' : ' · Không đủ điều kiện' }}</option>
            <option v-if="context.verifiedPatientId === null" value="new">Tạo hồ sơ mới theo thông tin yêu cầu</option>
          </select>
        </label>
        <p v-if="context.patients.length">Đã tìm thấy hồ sơ tương đồng. Đối chiếu từng người; không tạo trùng hoặc chuyển hồ sơ đã có chủ.</p>
        <label v-if="selection === 'new'"><input v-model="confirmedNew" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu hồ sơ tương đồng và xác nhận đây là người mới cần tạo hồ sơ</label>
        <label v-if="context.requiresCounterConfirmation"><input v-model="counter" type="checkbox" :disabled="busy || needsReview" /> Khách/người đại diện đang tại quầy; tôi đã đối soát và xác nhận đúng hồ sơ bệnh nhân</label>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu đúng người và giờ hẹn để xác nhận lịch</label>
        <button type="submit" :disabled="busy || needsReview">Xác nhận lịch đã hẹn</button>
      </form>
    </template>
    <p v-if="context?.result">Lịch #{{ context.result.appointmentId }} đã được xác nhận. Tài khoản được xử lý tại quầy khi khách check-in và đối chiếu CCCD/SĐT.</p>
  </section>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { bookingRequestsService, type BookingApprovalContext, type BookingApprovalResult } from '@/services/bookingRequests';

const props = defineProps<{ requestId: number }>();
const emit = defineEmits<{ approved: [result: BookingApprovalResult]; working: [value: boolean] }>();
const auth = useAuthStore();
const context = ref<BookingApprovalContext | null>(null), selection = ref(''), counter = ref(false), confirmed = ref(false), confirmedNew = ref(false);
const busy = ref(false), needsReview = ref(true), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}/${props.requestId}`;
const allowed = () => ['ADMIN', 'RECEPTIONIST'].includes(auth.role);
const alive = (t: number, owner: string) => !disposed && t === generation && owner === identity() && allowed();
function clear() { context.value = null; selection.value = ''; counter.value = false; confirmed.value = false; confirmedNew.value = false; }
const validResult = (r: BookingApprovalResult) => r && Number.isSafeInteger(r.appointmentId) && r.appointmentId > 0 && Number.isSafeInteger(r.patientId) && r.patientId > 0 && typeof r.createdAccount === 'boolean' && typeof r.requiresPhoneActivation === 'boolean';
async function load() {
  if (disposed || busy.value || !allowed() || !Number.isSafeInteger(props.requestId) || props.requestId < 1) return;
  const t = ++generation, owner = identity(); clear(); busy.value = true; needsReview.value = true; error.value = ''; message.value = '';
  try {
    const data = await bookingRequestsService.approval(props.requestId);
    if (!alive(t, owner)) return;
    if (data.id !== props.requestId || !Number.isSafeInteger(data.version) || data.version < 1 || !/^[A-F0-9]{64}$/.test(data.reviewStamp) || !Array.isArray(data.patients) || data.patients.length > 20 || data.result && !validResult(data.result)) throw Error();
    context.value = data; selection.value = data.verifiedPatientId ? String(data.verifiedPatientId) : ''; needsReview.value = false;
  } catch { if (alive(t, owner)) error.value = 'Không tải được thông tin đối soát hoặc bạn không có quyền.'; }
  finally { if (alive(t, owner)) busy.value = false; }
}
async function approve() {
  const c = context.value;
  if (disposed || busy.value || needsReview.value || !allowed() || !c || c.id !== props.requestId || c.result || c.blockReason) return;
  const patientId = selection.value === 'new' ? null : Number(selection.value);
  if (!confirmed.value || patientId === null && (!confirmedNew.value || c.verifiedPatientId !== null) || patientId !== null && !c.patients.some(p => p.patientId === patientId && p.canSelect) || c.requiresCounterConfirmation && !counter.value) {
    error.value = 'Chọn đúng hồ sơ và hoàn tất các xác nhận đối soát trước khi gửi.'; return;
  }
  const t = generation, owner = identity(); busy.value = true; emit('working', true); error.value = '';
  const body = { expectedVersion: c.version, reviewStamp: c.reviewStamp, patientId, confirmedNewPatient: patientId === null, confirmedAtCounter: counter.value, confirmed: true };
  confirmed.value = false;
  try {
    const result = await bookingRequestsService.approve(c.id, body);
    if (!alive(t, owner)) return;
    if (!validResult(result)) throw Error();
    clear(); needsReview.value = true; emit('approved', result);
  } catch { if (alive(t, owner)) { error.value = 'Chưa xác minh kết quả hoặc dữ liệu đã đổi. Bấm Đối chiếu lại yêu cầu trước khi xử lý tiếp.'; needsReview.value = true; counter.value = false; confirmedNew.value = false; } }
  finally { if (alive(t, owner)) { busy.value = false; emit('working', false); } }
}
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; message.value = ''; if (!disposed) void load(); });
watch(selection, () => { confirmed.value = false; confirmedNew.value = false; });
onMounted(() => void load()); onUnmounted(() => { disposed = true; generation++; clear(); emit('working', false); });
</script>
<style scoped>
.approval { padding: 1rem; border: 1px solid #cbd5e1; margin-block: 1rem; }
label { display: block; margin-block: 1rem; } select, button { font: inherit; padding: .6rem; max-width: 100%; }
[role=alert] { color: #9f1239; }
</style>
