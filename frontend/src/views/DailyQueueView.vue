<template>
  <main class="page">
    <div class="header-row">
      <h1>Hàng đợi hôm nay</h1>
      <router-link to="/queue/display" target="_blank" class="tv-link">Màn hình TV phòng chờ ↗</router-link>
    </div>
    <p>Khách hẹn đúng giờ được xếp theo giờ hẹn; khách thường hoặc trễ hẹn theo thời điểm check-in.</p>
    <div class="toolbar">
      <label for="status">Trạng thái</label>
      <select id="status" v-model.number="status" :disabled="writing" @change="load(1)">
        <option :value="0">Tất cả</option><option :value="1">Đang chờ</option><option :value="2">Đang khám</option>
        <option :value="5">Đang chụp ảnh</option><option :value="3">Khám xong</option><option :value="4">Đã hủy</option>
      </select>
      <button :disabled="loading || writing" @click="load(page)">Tải lại</button>
      <template v-if="authStore.role === 'ADMIN'">
        <label for="call-dentist">Nha sĩ gọi khám</label>
        <select id="call-dentist" v-model.number="callDentistId" :disabled="writing || dentistsLoading">
          <option :value="0">Chọn nha sĩ</option><option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">{{ dentist.dentistName }} · #{{ dentist.dentistId }}</option>
        </select>
        <button :disabled="writing || dentistsLoading" @click="loadDentists">Tải nha sĩ</button>
      </template>
      <button v-if="canCallNext" :disabled="loading || writing || cancelRow !== null || needsReview || (authStore.role === 'ADMIN' && (dentistsLoading || !callDentistId))" class="btn-call-next" @click="handleCallNext">
        {{ callingNext ? 'Đang gọi…' : 'Gọi tiếp theo' }}
      </button>
    </div>
    <p v-if="loading" role="status">Đang tải hàng đợi…</p>
    <p v-if="actionMsg" class="action-msg" role="status">{{ actionMsg }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="dentistError" role="alert">{{ dentistError }}</p>
    <p v-if="needsReview" role="alert">Tải lại hàng đợi thành công và đối chiếu trạng thái trước khi thao tác tiếp.</p>
    <p v-if="!loading && !error && !rows.length">Không có lượt tiếp đón phù hợp hôm nay.</p>
    <p v-if="!loading && !error">{{ total }} lượt · Trang {{ page }} · Cập nhật {{ updatedAt }}</p>
    <ol class="entries"><li v-for="row in rows" :key="row.queueEntryId" :class="{ priority: row.isPriority }">
      <strong>{{ numberLabel(row.queueNumber) }} · {{ row.patientName }}</strong>
      <span v-if="row.isEmergency" class="emergency-badge">CẤP CỨU</span>
      <p>{{ row.patientCode }} · {{ row.statusText }} · {{ row.queueDate }}</p>
      <p>{{ row.isPriority ? 'Ưu tiên lịch hẹn đúng giờ' : 'Chờ thông thường' }} · {{ row.dentistName || 'Chưa phân nha sĩ' }}</p>
      <button v-if="canCancelWaiting && row.status === 1" :disabled="loading || writing || needsReview" @click="openCancellation(row)">Hủy lượt chưa khám</button>
    </li></ol>
    <section v-if="cancelRow" aria-labelledby="cancel-title" class="cancel-panel">
      <h2 id="cancel-title">Hủy lượt chờ chưa khám</h2>
      <p>{{ numberLabel(cancelRow.queueNumber) }} · {{ cancelRow.patientName }} · {{ cancelRow.patientCode }} · {{ cancelRow.queueDate }}</p>
      <p>Chỉ hủy lượt đang chờ trước lần khám. Lượt đã bắt đầu khám hoặc trở về sau chụp ảnh cần xử lý theo quy trình chuyên môn.</p>
      <form @submit.prevent="handleCancellation">
        <label for="cancel-reason">Lý do (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="cancel-reason" v-model="cancelReason" maxlength="500" :disabled="cancelling" />
        <label><input v-model="cancelConfirmed" type="checkbox" :disabled="cancelling || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận hủy lượt chờ chưa khám.</label>
        <button type="submit" :disabled="cancelling || loading || needsReview || !cancelConfirmed">{{ cancelling ? 'Đang hủy…' : 'Xác nhận hủy lượt' }}</button>
        <button type="button" :disabled="cancelling" @click="closeCancellation">Quay lại</button>
      </form>
    </section>
    <nav aria-label="Phân trang hàng đợi">
      <button :disabled="loading || writing || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || writing || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { appointmentsService, type BookingDentist } from '@/services/appointments';
import { queueService, type QueueEntryResponse } from '@/services/queue';
const rows = ref<QueueEntryResponse[]>([]), status = ref(1), page = ref(1), total = ref(0), hasNext = ref(false);
const loading = ref(false), callingNext = ref(false), error = ref(''), actionMsg = ref(''), updatedAt = ref('');
const authStore = useAuthStore(), canCallNext = computed(() => ['ADMIN', 'DENTIST'].includes(authStore.role));
const canCancelWaiting = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const cancelRow = ref<QueueEntryResponse | null>(null), cancelReason = ref(''), cancelConfirmed = ref(false), cancelling = ref(false);
const writing = computed(() => callingNext.value || cancelling.value);
const dentists = ref<BookingDentist[]>([]), callDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref(''), needsReview = ref(false);
let version = 0, life = 0, dentistVersion = 0;
async function loadDentists() {
  if (authStore.role !== 'ADMIN' || writing.value) return;
  const ticket = ++dentistVersion; dentistsLoading.value = true; dentistError.value = ''; dentists.value = [];
  try { const result = await appointmentsService.getBookingDentists(); if (ticket === dentistVersion) { dentists.value = result; if (!result.some(d => d.dentistId === callDentistId.value)) callDentistId.value = 0; } }
  catch { if (ticket === dentistVersion) { callDentistId.value = 0; dentistError.value = 'Không thể tải nha sĩ hoạt động. Tải lại trước khi gọi ca.'; } }
  finally { if (ticket === dentistVersion) dentistsLoading.value = false; }
}
async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000 || ![0, 1, 2, 3, 4, 5].includes(status.value)) return;
  closeCancellation();
  const ticket = ++version; loading.value = true; rows.value = []; error.value = ''; updatedAt.value = ''; total.value = 0; hasNext.value = false;
  try {
    const result = await queueService.getToday(targetPage, status.value === 0 ? undefined : status.value);
    if (ticket !== version) return;
    rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage;
    needsReview.value = false;
    updatedAt.value = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date());
  } catch { if (ticket === version) error.value = 'Không thể tải hàng đợi. Vui lòng tải lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
async function handleCallNext() {
  if (!canCallNext.value || writing.value || cancelRow.value !== null || loading.value || needsReview.value) return;
  if (authStore.role === 'ADMIN' && (dentistsLoading.value || !Number.isInteger(callDentistId.value) || !dentists.value.some(d => d.dentistId === callDentistId.value))) { dentistError.value = 'Chọn nha sĩ hoạt động đã tải để gọi ca.'; return; }
  const ticket = life;
  callingNext.value = true; error.value = ''; actionMsg.value = '';
  try {
    const entry = await queueService.callNext(authStore.role === 'ADMIN' ? { dentistId: callDentistId.value } : undefined);
    if (ticket !== life) return;
    actionMsg.value = `Đã gọi số ${numberLabel(entry.queueNumber)} - ${entry.patientName} vào khám.`;
    needsReview.value = true;
    await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const anyErr = err as { response?: { data?: { message?: string } } };
    error.value = anyErr?.response?.data?.message || 'Không thể gọi ca tiếp theo.';
    needsReview.value = true;
  } finally {
    if (ticket === life) callingNext.value = false;
  }
}
function clearCancellation() { cancelRow.value = null; cancelReason.value = ''; cancelConfirmed.value = false; }
function closeCancellation() { if (!cancelling.value) clearCancellation(); }
function openCancellation(row: QueueEntryResponse) {
  if (!canCancelWaiting.value || writing.value || loading.value || needsReview.value || row.status !== 1 ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId)) return;
  clearCancellation(); cancelRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleCancellation() {
  const selected = cancelRow.value;
  if (!canCancelWaiting.value || !selected || selected.status !== 1 || writing.value || loading.value || needsReview.value || !cancelConfirmed.value) return;
  if (cancelReason.value.length > 500) { error.value = 'Lý do không được vượt quá 500 ký tự.'; return; }
  const ticket = life; cancelling.value = true; error.value = ''; actionMsg.value = '';
  try {
    const result = await queueService.cancelWaiting(selected.queueEntryId, cancelReason.value.trim() || null);
    if (ticket !== life) return;
    actionMsg.value = `Đã hủy lượt ${numberLabel(result.queueNumber)} - ${result.patientName}.`;
    clearCancellation(); needsReview.value = true; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả hủy. Tải lại hàng đợi để đối chiếu.';
    cancelConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) cancelling.value = false; }
}
function numberLabel(value: number) { return `A-${String(value).padStart(3, '0')}`; }
onMounted(() => Promise.all([load(), loadDentists()]));
onUnmounted(() => { life++; version++; dentistVersion++; clearCancellation(); rows.value = []; dentists.value = []; callDentistId.value = 0; dentistError.value = ''; actionMsg.value = ''; error.value = ''; needsReview.value = false; updatedAt.value = ''; total.value = 0; });
</script>
<style scoped>
.page { max-width: 900px; margin: auto; padding: 1.5rem; }
.header-row { display: flex; justify-content: space-between; align-items: center; }
.tv-link { color: #0284c7; text-decoration: none; font-weight: 600; padding: 0.4rem 0.8rem; border: 1px solid #0284c7; border-radius: 6px; }
.tv-link:hover { background: #0284c7; color: #fff; }
.toolbar { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin: 1rem 0; }
button, select { font: inherit; padding: .5rem; }
.btn-call-next { background: #0284c7; color: #fff; border: none; border-radius: 4px; font-weight: 600; cursor: pointer; }
.btn-call-next:hover:not(:disabled) { background: #0369a1; }
.action-msg { color: #15803d; font-weight: 600; }
.cancel-panel { border: 1px solid #b91c1c; border-radius: 8px; padding: 1rem; margin: 1rem 0; }
.cancel-panel form { display: grid; gap: .75rem; } textarea { font: inherit; min-height: 5rem; }
.entries { list-style: none; padding: 0; } li { padding: 1rem; margin: .6rem 0; border: 1px solid #ddd; border-radius: 8px; }
.priority { border-left: 5px solid #13795b; } [role=alert] { color: #b91c1c; }
.emergency-badge { display: inline-block; margin-left: .5rem; padding: .2rem .55rem; border-radius: 999px; color: #fff; background: #b91c1c; font-size: .8rem; font-weight: 800; }
@media (prefers-reduced-motion: no-preference) { .emergency-badge { animation: emergency-pulse 1.2s ease-in-out infinite alternate; } }
@keyframes emergency-pulse { from { box-shadow: 0 0 0 0 #b91c1c33; } to { box-shadow: 0 0 0 5px #b91c1c55; } }
</style>
