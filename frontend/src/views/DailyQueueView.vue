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
        <option :value="7">Vắng mặt</option><option :value="6">Đang mời vào phòng</option><option :value="5">Đang chụp ảnh</option><option :value="3">Khám xong</option><option :value="4">Đã hủy</option>
      </select>
      <button :disabled="loading || writing" @click="load(page)">Tải lại</button>
      <template v-if="needsCallingDentist">
        <label for="call-dentist">Nha sĩ gọi khám</label>
        <select id="call-dentist" v-model.number="callDentistId" :disabled="writing || dentistsLoading">
          <option :value="0">Chọn nha sĩ</option><option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">{{ dentist.dentistName }} · {{ locationLabel(dentist) }} · #{{ dentist.dentistId }}</option>
        </select>
        <button :disabled="writing || dentistsLoading" @click="loadDentists">Tải nha sĩ</button>
      </template>
      <button v-if="canCallNext" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || cleanupOpen || needsReview || (needsCallingDentist && (dentistsLoading || !callDentistId))" class="btn-call-next" @click="handleCallNext">
        {{ callingNext ? 'Đang gọi…' : 'Gọi số tiếp theo' }}
      </button>
      <button v-if="canCancelWaiting" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || cleanupOpen || needsReview" @click="openCleanup">Dọn lượt chưa khám quá hạn</button>
    </div>
    <p v-if="canCallNext">Gọi số chỉ mời bệnh nhân vào phòng. Nha sĩ/Admin cần bấm “Bắt đầu khám” tại hồ sơ lâm sàng.</p>
    <p v-if="authStore.role === 'ASSISTANT'">Phụ tá chỉ gọi các lượt đã có nha sĩ được chọn; lượt chưa phân nha sĩ cần Lễ tân/Admin đối chiếu trước.</p>
    <p v-if="loading" role="status">Đang tải hàng đợi…</p>
    <p v-if="actionMsg" class="action-msg" role="status">{{ actionMsg }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="dentistError" role="alert">{{ dentistError }}</p>
    <p v-if="needsReview" role="alert">Tải lại hàng đợi thành công và đối chiếu trạng thái trước khi thao tác tiếp.</p>
    <p v-if="!loading && !error && !rows.length">Không có lượt tiếp đón phù hợp hôm nay.</p>
    <p v-if="!loading && !error">{{ total }} lượt · Trang {{ page }} · Cập nhật {{ updatedAt }}</p>
    <ol class="entries"><li v-for="row in rows" :key="row.queueEntryId" :class="{ priority: row.isPriority }">
      <strong>{{ numberLabel(row.queueNumber) }} · {{ row.patientName }}</strong>
      <p>{{ row.patientCode }} · {{ row.statusText }} · {{ row.queueDate }}</p>
      <p>{{ row.isPriority ? 'Ưu tiên lịch hẹn đúng giờ' : 'Chờ thông thường' }} · {{ row.dentistName || 'Chưa phân nha sĩ' }} · {{ locationLabel(row) }}</p>
      <RouterLink v-if="row.status === 6 && ['ADMIN', 'DENTIST'].includes(authStore.role)" to="/clinical/diagnosis">Mở hồ sơ để bắt đầu khám</RouterLink>
      <button v-if="canCancelWaiting && row.status === 1 && row.canCancelConsultation !== true" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || cleanupOpen || needsReview" @click="openCancellation(row)">Hủy lượt chưa khám</button>
      <button v-if="canCancelClinical(row)" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || cleanupOpen || needsReview" @click="openCancellation(row, true)">Hủy ca đã bắt đầu khám</button>
      <button v-if="canRouteImaging(row)" :disabled="loading || writing || cancelRow !== null || absenceRow !== null || cleanupOpen || needsReview" @click="openImaging(row)">
        {{ row.status === 2 ? 'Chỉ định chụp ảnh' : 'Hoàn tất chụp → chờ nha sĩ' }}
      </button>
      <button v-if="canChangeAbsence(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || cleanupOpen || needsReview" @click="openAbsence(row)">
        {{ row.status === 7 ? 'Gọi lại → hàng chờ' : 'Vắng mặt' }}
      </button>
    </li></ol>
    <section v-if="cancelRow" aria-labelledby="cancel-title" class="cancel-panel">
      <h2 id="cancel-title">{{ cancelClinical ? 'Hủy ca đã bắt đầu khám' : 'Hủy lượt chờ chưa khám' }}</h2>
      <p>{{ numberLabel(cancelRow.queueNumber) }} · {{ cancelRow.patientName }} · {{ cancelRow.patientCode }} · {{ cancelRow.queueDate }}</p>
      <p v-if="cancelClinical">Hủy ca sẽ giải phóng ghế và tự hủy hóa đơn Draft chưa thu trong cùng thao tác. Giữ lịch sử, lý do và các dòng hóa đơn. Ca đã phát sinh thanh toán không thể hủy qua thao tác này.</p>
      <p v-else>Chỉ hủy lượt đang chờ trước lần khám. Lượt đã bắt đầu khám hoặc trở về sau chụp ảnh cần xử lý theo quy trình chuyên môn.</p>
      <form @submit.prevent="handleCancellation">
        <label for="cancel-reason">Lý do ({{ cancelClinical ? 'bắt buộc' : 'không bắt buộc' }}, tối đa 500 ký tự)</label>
        <textarea id="cancel-reason" v-model="cancelReason" :required="cancelClinical" maxlength="500" :disabled="cancelling" />
        <label><input v-model="cancelConfirmed" type="checkbox" :disabled="cancelling || cleanupOpen || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận {{ cancelClinical ? 'hủy ca cùng Draft chưa thu' : 'hủy lượt chờ chưa khám' }}.</label>
        <button type="submit" :disabled="cancelling || loading || cleanupOpen || needsReview || !cancelConfirmed">{{ cancelling ? 'Đang hủy…' : 'Xác nhận hủy lượt' }}</button>
        <button type="button" :disabled="cancelling" @click="closeCancellation">Quay lại</button>
      </form>
    </section>
    <section v-if="imagingRow" aria-labelledby="imaging-title" class="imaging-panel">
      <h2 id="imaging-title">{{ imagingRow.status === 2 ? 'Chuyển sang chụp ảnh' : 'Hoàn tất chụp ảnh' }}</h2>
      <p>{{ numberLabel(imagingRow.queueNumber) }} · {{ imagingRow.patientName }} · {{ imagingRow.patientCode }}</p>
      <p>Nha sĩ phụ trách: {{ imagingRow.dentistName }} · #{{ imagingRow.dentistId }}</p>
      <p v-if="imagingRow.status === 5">Sau xác nhận, bệnh nhân về hàng chờ ưu tiên cao nhất để nha sĩ cũ gọi lại. Lần khám hiện tại được giữ nguyên.</p>
      <form @submit.prevent="handleImaging">
        <label for="imaging-reason">{{ imagingRow.status === 2 ? 'Chỉ định/nhu cầu chụp (bắt buộc)' : 'Ghi chú hoàn tất (không bắt buộc)' }}</label>
        <textarea id="imaging-reason" v-model="imagingReason" maxlength="500" :required="imagingRow.status === 2" :disabled="routingImaging" />
        <label><input v-model="imagingConfirmed" type="checkbox" :disabled="routingImaging || cleanupOpen || needsReview" />
          {{ imagingRow.status === 2 ? 'Tôi đã đối chiếu đúng bệnh nhân và chỉ định chụp ảnh.' : 'Tôi xác nhận bệnh nhân đã hoàn tất chụp theo quy trình phòng khám.' }}
        </label>
        <button type="submit" :disabled="routingImaging || loading || cleanupOpen || needsReview || !imagingConfirmed">{{ routingImaging ? 'Đang chuyển…' : 'Xác nhận chuyển bước' }}</button>
        <button type="button" :disabled="routingImaging" @click="closeImaging">Quay lại</button>
      </form>
    </section>
    <section v-if="absenceRow" class="absence-panel" aria-labelledby="absence-title">
      <h2 id="absence-title">{{ absenceRow.status === 7 ? 'Phục hồi lượt vắng mặt' : 'Xác nhận bệnh nhân vắng mặt' }}</h2>
      <p>{{ numberLabel(absenceRow.queueNumber) }} · {{ absenceRow.patientName }} · {{ absenceRow.patientCode }}</p>
      <p>{{ absenceRow.status === 7 ? 'Đưa về đầu hàng chờ, sau lượt trở lại từ chụp ảnh. Giữ nha sĩ và số thứ tự; cần gọi số riêng.' : 'Chỉ áp dụng ca chưa bắt đầu khám. Giữ lượt để Lễ tân/Admin có thể gọi lại khi bệnh nhân trở về.' }}</p>
      <form @submit.prevent="handleAbsence">
        <label for="absence-reason">Ghi chú (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="absence-reason" v-model="absenceReason" maxlength="500" :disabled="changingAbsence" />
        <label><input v-model="absenceConfirmed" type="checkbox" :disabled="changingAbsence || cleanupOpen || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận thao tác.</label>
        <button type="submit" :disabled="writing || loading || cleanupOpen || needsReview || !absenceConfirmed">{{ changingAbsence ? 'Đang xử lý…' : 'Xác nhận' }}</button>
        <button type="button" :disabled="changingAbsence" @click="closeAbsence">Quay lại</button>
      </form>
    </section>
    <section v-if="cleanupOpen" class="absence-panel" aria-labelledby="cleanup-title">
      <h2 id="cleanup-title">Dọn lượt chưa khám quá hạn</h2>
      <p>Hệ thống đóng lượt chưa khám sau 23:30 giờ Việt Nam và bù những ngày trước. Ca đã bắt đầu khám được giữ nguyên. Lịch sử vẫn còn; không tự tạo lịch hẹn mới.</p>
      <form @submit.prevent="handleCleanup">
        <label><input v-model="cleanupConfirmed" type="checkbox" :disabled="cleaningUp || needsReview" /> Tôi xác nhận dọn các lượt chưa khám đã quá hạn theo giờ máy chủ.</label>
        <button type="submit" :disabled="writing || loading || needsReview || !cleanupConfirmed">{{ cleaningUp ? 'Đang dọn…' : 'Xác nhận dọn lượt' }}</button>
        <button type="button" :disabled="cleaningUp" @click="closeCleanup">Quay lại</button>
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
import { queueService, type QueueEntryResponse, type CallingDentist } from '@/services/queue';
const rows = ref<QueueEntryResponse[]>([]), status = ref(1), page = ref(1), total = ref(0), hasNext = ref(false);
function locationLabel(item: { roomName?: string | null; chairNumber?: string | null }): string {
  return [item.roomName, item.chairNumber ? `Ghế ${item.chairNumber}` : null].filter(Boolean).join(' · ') || 'Chưa có phòng/ghế';
}
const loading = ref(false), callingNext = ref(false), error = ref(''), actionMsg = ref(''), updatedAt = ref('');
const authStore = useAuthStore(), canCallNext = computed(() => ['ADMIN', 'DENTIST', 'ASSISTANT'].includes(authStore.role));
const needsCallingDentist = computed(() => ['ADMIN', 'ASSISTANT'].includes(authStore.role));
const canCancelWaiting = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const cancelClinical = ref(false);
const cancelRow = ref<QueueEntryResponse | null>(null), cancelReason = ref(''), cancelConfirmed = ref(false), cancelling = ref(false);
const imagingRow = ref<QueueEntryResponse | null>(null), imagingReason = ref(''), imagingConfirmed = ref(false), routingImaging = ref(false);
const absenceRow = ref<QueueEntryResponse | null>(null), absenceReason = ref(''), absenceConfirmed = ref(false), changingAbsence = ref(false);
const cleanupOpen = ref(false), cleanupConfirmed = ref(false), cleaningUp = ref(false);
const writing = computed(() => callingNext.value || cancelling.value || routingImaging.value || changingAbsence.value || cleaningUp.value);
const dentists = ref<CallingDentist[]>([]), callDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref(''), needsReview = ref(false);
let version = 0, life = 0, dentistVersion = 0;
async function loadDentists() {
  if (!needsCallingDentist.value || writing.value) return;
  const ticket = ++dentistVersion; dentistsLoading.value = true; dentistError.value = ''; dentists.value = [];
  try { const result = await queueService.getCallingDentists(); if (ticket === dentistVersion) { dentists.value = result; if (!result.some(d => d.dentistId === callDentistId.value)) callDentistId.value = 0; } }
  catch { if (ticket === dentistVersion) { callDentistId.value = 0; dentistError.value = 'Không thể tải nha sĩ hoạt động. Tải lại trước khi gọi ca.'; } }
  finally { if (ticket === dentistVersion) dentistsLoading.value = false; }
}
async function load(targetPage = 1) {
  if (cancelling.value || routingImaging.value || changingAbsence.value || cleaningUp.value) return;
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000 || ![0, 1, 2, 3, 4, 5, 6, 7].includes(status.value)) return;
  closeCancellation();
  closeImaging();
  closeAbsence();
  closeCleanup();
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
  if (!canCallNext.value || writing.value || cancelRow.value !== null || imagingRow.value !== null || absenceRow.value !== null || loading.value || cleanupOpen.value || needsReview.value) return;
  if (needsCallingDentist.value && (dentistsLoading.value || !Number.isInteger(callDentistId.value) || !dentists.value.some(d => d.dentistId === callDentistId.value))) { dentistError.value = 'Chọn nha sĩ hoạt động đã tải để gọi ca.'; return; }
  const ticket = life;
  callingNext.value = true; error.value = ''; actionMsg.value = '';
  try {
    const entry = await queueService.callNext(needsCallingDentist.value ? { dentistId: callDentistId.value } : undefined);
    if (ticket !== life) return;
    if (entry.status !== 6) throw new Error('Unexpected invitation status');
    actionMsg.value = `Đã mời số ${numberLabel(entry.queueNumber)} - ${entry.patientName} ${locationLabel(entry)}; cần xác nhận bắt đầu khám riêng.`;
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
function canCancelClinical(row: QueueEntryResponse) {
  return row.canCancelConsultation === true && [1, 2, 5, 6].includes(row.status) &&
    (authStore.role === 'ADMIN' || (authStore.role === 'DENTIST' && row.dentistId === authStore.user?.userId));
}
function clearCancellation() { cancelRow.value = null; cancelReason.value = ''; cancelConfirmed.value = false; cancelClinical.value = false; }
function closeCancellation() { if (!cancelling.value) clearCancellation(); }
function openCancellation(row: QueueEntryResponse, clinical = false) {
  if (!(clinical ? canCancelClinical(row) : canCancelWaiting.value && row.status === 1 && row.canCancelConsultation !== true) || writing.value || imagingRow.value !== null || absenceRow.value !== null || loading.value || cleanupOpen.value || needsReview.value ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status && (!clinical || r.canCancelConsultation === true && r.dentistId === row.dentistId))) return;
  clearCancellation(); cancelClinical.value = clinical; cancelRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleCancellation() {
  const selected = cancelRow.value;
  if (!selected || !(cancelClinical.value ? canCancelClinical(selected) : canCancelWaiting.value && selected.status === 1 && selected.canCancelConsultation !== true) || writing.value || loading.value || cleanupOpen.value || needsReview.value || !cancelConfirmed.value) return;
  if (cancelReason.value.length > 500 || (cancelClinical.value && !cancelReason.value.trim())) { error.value = 'Ghi lý do hủy ca, tối đa 500 ký tự.'; return; }
  const ticket = life; cancelling.value = true; error.value = ''; actionMsg.value = '';
  try {
    const clinical = cancelClinical.value;
    const result = clinical
      ? await queueService.cancelConsultation(selected.queueEntryId, cancelReason.value.trim())
      : await queueService.cancelWaiting(selected.queueEntryId, cancelReason.value.trim() || null);
    if (ticket !== life) return;
    if (clinical && (result.queueEntryId !== selected.queueEntryId || result.status !== 4 || result.dentistId !== selected.dentistId)) throw new Error('Invalid cancellation result');
    actionMsg.value = clinical ? `Đã hủy ca ${numberLabel(result.queueNumber)}; Draft chưa thu đã được hủy, lịch sử được giữ.` : `Đã hủy lượt ${numberLabel(result.queueNumber)} - ${result.patientName}.`;
    clearCancellation(); needsReview.value = true; cancelling.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả hủy. Tải lại hàng đợi để đối chiếu.';
    cancelConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) cancelling.value = false; }
}
function canRouteImaging(row: QueueEntryResponse) {
  if (!Number.isInteger(row.dentistId) || !row.dentistId || row.dentistId <= 0) return false;
  return (row.status === 2 && (authStore.role === 'ADMIN' || (authStore.role === 'DENTIST' && row.dentistId === authStore.user?.userId))) ||
    (row.status === 5 && ['ADMIN', 'ASSISTANT'].includes(authStore.role));
}
function clearImaging() { imagingRow.value = null; imagingReason.value = ''; imagingConfirmed.value = false; }
function closeImaging() { if (!routingImaging.value) clearImaging(); }
function openImaging(row: QueueEntryResponse) {
  if (!canRouteImaging(row) || writing.value || loading.value || cancelRow.value !== null || absenceRow.value !== null || cleanupOpen.value || needsReview.value ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status)) return;
  clearImaging(); imagingRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleImaging() {
  const selected = imagingRow.value;
  if (!selected || !canRouteImaging(selected) || writing.value || loading.value || cancelRow.value !== null || absenceRow.value !== null || cleanupOpen.value || needsReview.value || !imagingConfirmed.value) return;
  if (imagingReason.value.length > 500 || (selected.status === 2 && !imagingReason.value.trim())) { error.value = 'Ghi chỉ định chụp ảnh, tối đa 500 ký tự.'; return; }
  const ticket = life; routingImaging.value = true; error.value = ''; actionMsg.value = '';
  try {
    const target = selected.status === 2 ? 5 : 1;
    const result = await queueService.routeImaging(selected.queueEntryId, target, imagingReason.value.trim() || null);
    if (ticket !== life) return;
    if (result.queueEntryId !== selected.queueEntryId || result.status !== target || result.dentistId !== selected.dentistId) throw new Error('Invalid routing result');
    actionMsg.value = target === 5 ? 'Đã chuyển bệnh nhân sang chụp ảnh.' : 'Đã hoàn tất chụp; bệnh nhân chờ ưu tiên để gặp lại nha sĩ phụ trách.';
    clearImaging(); needsReview.value = true; routingImaging.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả. Tải lại hàng đợi trước khi thao tác tiếp.';
    imagingConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) routingImaging.value = false; }
}
function canChangeAbsence(row: QueueEntryResponse) {
  if (row.status === 7) return ['ADMIN', 'RECEPTIONIST'].includes(authStore.role);
  return row.status === 6 && row.canMarkAbsent === true &&
    (['ADMIN', 'RECEPTIONIST'].includes(authStore.role) || (authStore.role === 'DENTIST' && row.dentistId === authStore.user?.userId));
}
function clearAbsence() { absenceRow.value = null; absenceReason.value = ''; absenceConfirmed.value = false; }
function closeAbsence() { if (!changingAbsence.value) clearAbsence(); }
function openAbsence(row: QueueEntryResponse) {
  if (!canChangeAbsence(row) || writing.value || loading.value || cleanupOpen.value || needsReview.value || cancelRow.value !== null || imagingRow.value !== null ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status && (row.status === 7 || r.canMarkAbsent))) return;
  clearAbsence(); absenceRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleAbsence() {
  const selected = absenceRow.value;
  if (!selected || !canChangeAbsence(selected) || writing.value || loading.value || cleanupOpen.value || needsReview.value || !absenceConfirmed.value || cancelRow.value !== null || imagingRow.value !== null) return;
  if (absenceReason.value.length > 500) { error.value = 'Ghi chú tối đa 500 ký tự.'; return; }
  const ticket = life, recall = selected.status === 7; changingAbsence.value = true; error.value = ''; actionMsg.value = '';
  try {
    const result = await queueService.changeAbsence(selected.queueEntryId, recall, absenceReason.value.trim() || null);
    if (ticket !== life) return;
    if (result.queueEntryId !== selected.queueEntryId || result.status !== (recall ? 1 : 7) || result.dentistId !== selected.dentistId) throw new Error('Invalid absence result');
    actionMsg.value = recall ? 'Đã phục hồi lượt vào hàng chờ; cần gọi số để mời vào phòng.' : 'Đã ghi nhận vắng mặt; có thể gọi bệnh nhân tiếp theo.';
    clearAbsence(); needsReview.value = true; changingAbsence.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả. Tải lại hàng đợi trước khi thao tác tiếp.';
    absenceConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) changingAbsence.value = false; }
}
function closeCleanup() { if (!cleaningUp.value) { cleanupOpen.value = false; cleanupConfirmed.value = false; } }
function openCleanup() {
  if (!canCancelWaiting.value || writing.value || loading.value || needsReview.value || cancelRow.value || imagingRow.value || absenceRow.value) return;
  cleanupOpen.value = true; cleanupConfirmed.value = false; error.value = ''; actionMsg.value = '';
}
async function handleCleanup() {
  if (!canCancelWaiting.value || !cleanupOpen.value || !cleanupConfirmed.value || writing.value || loading.value || needsReview.value || cancelRow.value || imagingRow.value || absenceRow.value) return;
  const ticket = life; cleaningUp.value = true; error.value = ''; actionMsg.value = '';
  try {
    const result = await queueService.cleanupExpired();
    if (ticket !== life) return;
    if (!Number.isInteger(result.cancelledCount) || result.cancelledCount < 0 || !/^\d{4}-\d{2}-\d{2}$/.test(result.lastExpiredDate)) throw new Error('Invalid cleanup result');
    actionMsg.value = `Đã đóng ${result.cancelledCount} lượt chưa khám quá hạn đến ngày ${result.lastExpiredDate}. Lịch sử được giữ nguyên.`;
    cleanupOpen.value = false; cleanupConfirmed.value = false; needsReview.value = true; cleaningUp.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả dọn lượt. Tải lại để đối chiếu.';
    cleanupConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) cleaningUp.value = false; }
}
function numberLabel(value: number) { return `A-${String(value).padStart(3, '0')}`; }
onMounted(() => Promise.all([load(), loadDentists()]));
onUnmounted(() => { life++; version++; dentistVersion++; clearCancellation(); clearImaging(); clearAbsence(); cleanupOpen.value = false; cleanupConfirmed.value = false; rows.value = []; dentists.value = []; callDentistId.value = 0; dentistError.value = ''; actionMsg.value = ''; error.value = ''; needsReview.value = false; updatedAt.value = ''; total.value = 0; });
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
.imaging-panel { border: 1px solid #0284c7; border-radius: 8px; padding: 1rem; margin: 1rem 0; }
.imaging-panel form, .absence-panel form { display: grid; gap: .75rem; }
.absence-panel { border: 1px solid #a16207; border-radius: 8px; padding: 1rem; margin: 1rem 0; }
.entries { list-style: none; padding: 0; } li { padding: 1rem; margin: .6rem 0; border: 1px solid #ddd; border-radius: 8px; }
.priority { border-left: 5px solid #13795b; } [role=alert] { color: #b91c1c; }
</style>
