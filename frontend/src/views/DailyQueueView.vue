<template>
  <main class="page">
    <header class="page-header">
      <div class="heading-copy">
        <p class="eyebrow">ĐIỀU PHỐI KHÁM</p>
        <h1>Hàng đợi hôm nay</h1>
        <p class="intro">Theo dõi lượt tiếp đón và phối hợp bệnh nhân vào phòng khám.</p>
      </div>
      <router-link to="/queue/display" target="_blank" class="tv-link">Mở màn hình phòng chờ <span aria-hidden="true">↗</span></router-link>
    </header>

    <section class="toolbar" aria-label="Điều khiển hàng đợi">
      <div class="field-group">
        <label for="status">Lọc theo trạng thái</label>
        <select id="status" v-model.number="status" :disabled="writing" @change="load(1)">
          <option :value="0">Tất cả trạng thái</option><option :value="1">Đang chờ</option><option :value="2">Đang khám</option>
          <option :value="8">Tạm dừng</option><option :value="7">Vắng mặt</option><option :value="6">Đang mời vào phòng</option><option :value="5">Đang chụp ảnh</option><option :value="3">Khám xong</option><option :value="4">Đã hủy</option>
        </select>
      </div>
      <button class="button-secondary" :disabled="loading || writing" @click="load(page)">{{ loading ? 'Đang tải…' : 'Tải lại' }}</button>
      <template v-if="needsCallingDentist">
        <div class="field-group field-dentist">
          <label for="call-dentist">Nha sĩ gọi khám</label>
          <select id="call-dentist" v-model.number="callDentistId" :disabled="writing || dentistsLoading">
            <option :value="0">Chọn nha sĩ</option><option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">{{ dentist.dentistName }} · {{ locationLabel(dentist) }}</option>
          </select>
        </div>
        <button class="button-secondary" :disabled="writing || dentistsLoading" @click="loadDentists">Tải nha sĩ</button>
      </template>
      <button v-if="canCallNext" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || cleanupOpen || transferRow !== null || needsReview || (needsCallingDentist && (dentistsLoading || !callDentistId))" class="btn-call-next" @click="handleCallNext">
        {{ callingNext ? 'Đang gọi…' : 'Gọi số tiếp theo' }}
      </button>
      <button v-if="canCancelWaiting" class="button-warning" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openCleanup">Dọn lượt quá hạn</button>
    </section>

    <p v-if="canCallNext" class="workflow-hint"><strong>Lưu ý:</strong> Gọi số chỉ mời bệnh nhân vào phòng. Nha sĩ/Admin bắt đầu khám riêng tại hồ sơ lâm sàng.</p>
    <p v-if="authStore.role === 'ASSISTANT'" class="workflow-hint">Phụ tá chỉ gọi lượt đã được phân nha sĩ; lượt chưa phân cần Lễ tân/Admin đối chiếu.</p>
    <p v-if="loading" class="notice notice-loading" role="status">Đang tải hàng đợi…</p>
    <p v-if="actionMsg" class="notice notice-success" role="status">{{ actionMsg }}</p>
    <p v-if="error" class="notice notice-error" role="alert">{{ error }}</p>
    <p v-if="dentistError" class="notice notice-error" role="alert">{{ dentistError }}</p>
    <p v-if="needsReview" class="notice notice-warning" role="alert">Tải lại hàng đợi thành công và đối chiếu trạng thái trước khi thao tác tiếp.</p>
    <div v-if="!loading && !error && !rows.length" class="empty-state" role="status">
      <span class="empty-icon" aria-hidden="true">✓</span>
      <strong>Chưa có lượt tiếp đón phù hợp</strong>
      <span>Danh sách sẽ cập nhật khi có bệnh nhân được tiếp nhận hôm nay.</span>
    </div>
    <div v-if="!loading && !error" class="queue-summary" role="status">
      <strong>{{ total }} lượt</strong><span>·</span><span>Trang {{ page }}</span><span>·</span><span>Cập nhật {{ updatedAt || 'chưa có dữ liệu' }}</span>
    </div>

    <ol class="entries" aria-label="Danh sách hàng đợi hôm nay">
      <li v-for="row in rows" :key="row.queueEntryId" class="queue-entry" :class="{ priority: row.isPriority }">
        <div class="queue-entry-header">
          <span class="queue-ticket">{{ numberLabel(row.queueNumber) }}</span>
          <div class="queue-patient">
            <div class="queue-patient-row">
              <strong>{{ row.patientName }}</strong>
              <span v-if="row.isChildPatient" class="child-pill">👶 Trẻ &lt;14</span>
            </div>
            <span>{{ row.patientCode }} · {{ row.queueDate }}</span>
          </div>
          <span class="status-pill" :class="`status-${row.status}`">{{ row.statusText }}</span>
        </div>
        <div class="queue-entry-meta">
          <span v-if="row.isPriority" class="priority-pill">Lịch hẹn đúng giờ</span>
          <span v-else class="queue-type">Lượt thường</span>
          <span><b>Nha sĩ</b>{{ row.dentistName || 'Chưa phân công' }}</span>
          <span><b>Phòng</b>{{ locationLabel(row) }}</span>
        </div>
        <p v-if="row.notes" class="care-note"><strong>Ghi chú cần lưu ý</strong>{{ row.notes }}</p>
        <div class="queue-card-actions">
          <button v-if="row.status === 6" type="button" class="btn-recall-tv" @click="emitTvCall(row)">Đọc lại trên TV</button>
          <RouterLink v-if="row.status === 6 && ['ADMIN', 'DENTIST'].includes(authStore.role)" to="/management/clinical/diagnosis">Mở hồ sơ lâm sàng</RouterLink>
          <button v-if="canCancelWaiting && row.status === 1 && row.canCancelConsultation !== true" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openCancellation(row)">Hủy lượt chờ</button>
          <button v-if="canCancelClinical(row)" class="action-danger" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openCancellation(row, true)">Hủy ca đang khám</button>
          <button v-if="canRouteImaging(row)" :disabled="loading || writing || cancelRow !== null || absenceRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openImaging(row)">{{ row.status === 2 ? 'Chỉ định chụp ảnh' : 'Hoàn tất chụp → chờ nha sĩ' }}</button>
          <button v-if="canChangeAbsence(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openAbsence(row)">{{ row.status === 7 ? 'Gọi lại → hàng chờ' : 'Vắng mặt' }}</button>
          <button v-if="canPause(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openPause(row)">Tạm dừng ca</button>
          <button v-if="canResume(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openResume(row)">Tiếp tục ca</button>
          <button v-if="canTransfer(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || cleanupOpen || transferRow !== null || needsReview" @click="openTransfer(row)">Chuyển nha sĩ</button>
        </div>
      </li>
    </ol>    <section v-if="cancelRow" aria-labelledby="cancel-title" class="cancel-panel">
      <h2 id="cancel-title">{{ cancelClinical ? 'Hủy ca đã bắt đầu khám' : 'Hủy lượt chờ chưa khám' }}</h2>
      <p>{{ numberLabel(cancelRow.queueNumber) }} · {{ cancelRow.patientName }} · {{ cancelRow.patientCode }} · {{ cancelRow.queueDate }}</p>
      <p v-if="cancelClinical">Hủy ca sẽ nhả phòng điều trị và tự hủy hóa đơn Draft chưa thu trong cùng thao tác. Giữ lịch sử, lý do và các dòng hóa đơn. Ca đã phát sinh thanh toán không thể hủy qua thao tác này.</p>
      <p v-else>Chỉ hủy lượt đang chờ trước lần khám. Lượt đã bắt đầu khám hoặc trở về sau chụp ảnh cần xử lý theo quy trình chuyên môn.</p>
      <form @submit.prevent="handleCancellation">
        <label for="cancel-reason">Lý do ({{ cancelClinical ? 'bắt buộc' : 'không bắt buộc' }}, tối đa 500 ký tự)</label>
        <textarea id="cancel-reason" v-model="cancelReason" :required="cancelClinical" maxlength="500" :disabled="cancelling" />
        <label><input v-model="cancelConfirmed" type="checkbox" :disabled="cancelling || cleanupOpen || transferRow !== null || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận {{ cancelClinical ? 'hủy ca cùng Draft chưa thu' : 'hủy lượt chờ chưa khám' }}.</label>
        <button type="submit" :disabled="cancelling || loading || cleanupOpen || transferRow !== null || needsReview || !cancelConfirmed">{{ cancelling ? 'Đang hủy…' : 'Xác nhận hủy lượt' }}</button>
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
        <label><input v-model="imagingConfirmed" type="checkbox" :disabled="routingImaging || cleanupOpen || transferRow !== null || needsReview" />
          {{ imagingRow.status === 2 ? 'Tôi đã đối chiếu đúng bệnh nhân và chỉ định chụp ảnh.' : 'Tôi xác nhận bệnh nhân đã hoàn tất chụp theo quy trình phòng khám.' }}
        </label>
        <button type="submit" :disabled="routingImaging || loading || cleanupOpen || transferRow !== null || needsReview || !imagingConfirmed">{{ routingImaging ? 'Đang chuyển…' : 'Xác nhận chuyển bước' }}</button>
        <button type="button" :disabled="routingImaging" @click="closeImaging">Quay lại</button>
      </form>
    </section>
    <dialog v-if="transferRow" :ref="mountTransferDialog" class="transfer-panel" aria-labelledby="transfer-title" @cancel.prevent="closeTransfer">
      <h2 id="transfer-title">Chuyển nha sĩ phụ trách</h2>
      <p>{{ numberLabel(transferRow.queueNumber) }} · {{ transferRow.patientName }} · {{ transferRow.patientCode }}</p>
      <p v-if="transferRow.canCancelConsultation">Ca đang diễn ra sẽ chuyển sang phòng nha sĩ mới đang chọn. Phòng đích phải sẵn sàng.</p>
      <p v-else>Lượt chưa bắt đầu khám sẽ giữ nguyên số thứ tự và trạng thái.</p>
      <form @submit.prevent="handleTransfer">
        <label for="transfer-dentist">Nha sĩ tiếp nhận</label>
        <select id="transfer-dentist" v-model.number="transferDentistId" required :disabled="transferring || dentistsLoading || needsReview" @change="transferConfirmed = false">
          <option :value="0" disabled>Chọn nha sĩ</option>
          <option v-for="dentist in transferTargets" :key="dentist.dentistId" :value="dentist.dentistId">
            {{ dentist.dentistName }} · {{ locationLabel(dentist) }} · #{{ dentist.dentistId }}
          </option>
        </select>
        <button type="button" :disabled="transferring || dentistsLoading" @click="loadDentists">Tải lại nha sĩ</button>
        <label for="transfer-reason">Lý do hoặc ghi chú (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="transfer-reason" v-model="transferReason" maxlength="500" :disabled="transferring || needsReview" @input="transferConfirmed = false" />
        <label><input v-model="transferConfirmed" type="checkbox" :disabled="transferring || loading || needsReview" /> Tôi đã đối chiếu đúng lượt và xác nhận nha sĩ tiếp nhận.</label>
        <p v-if="error || dentistError" class="notice notice-error transfer-notice" role="alert">{{ error || dentistError }}</p>
        <p v-if="needsReview" class="notice notice-warning transfer-notice" role="status">Quay lại và tải lại hàng đợi trước khi thử lại.</p>
        <div class="transfer-actions">
          <button type="button" :disabled="transferring" @click="closeTransfer">Quay lại</button>
          <button type="submit" class="btn-call-next" :disabled="transferring || loading || cleanupOpen || needsReview || !transferConfirmed || !transferDentistId">
            {{ transferring ? 'Đang chuyển…' : 'Xác nhận chuyển nha sĩ' }}
          </button>
        </div>
      </form>
    </dialog>
    <section v-if="absenceRow" class="absence-panel" aria-labelledby="absence-title">
      <h2 id="absence-title">{{ absenceRow.status === 7 ? 'Phục hồi lượt vắng mặt' : 'Xác nhận bệnh nhân vắng mặt' }}</h2>
      <p>{{ numberLabel(absenceRow.queueNumber) }} · {{ absenceRow.patientName }} · {{ absenceRow.patientCode }}</p>
      <p>{{ absenceRow.status === 7 ? 'Đưa về đầu hàng chờ, sau lượt trở lại từ chụp ảnh. Giữ nha sĩ và số thứ tự; cần gọi số riêng.' : 'Chỉ áp dụng ca chưa bắt đầu khám. Giữ lượt để Lễ tân/Admin có thể gọi lại khi bệnh nhân trở về.' }}</p>
      <form @submit.prevent="handleAbsence">
        <label for="absence-reason">Ghi chú (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="absence-reason" v-model="absenceReason" maxlength="500" :disabled="changingAbsence" />
        <label><input v-model="absenceConfirmed" type="checkbox" :disabled="changingAbsence || cleanupOpen || transferRow !== null || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận thao tác.</label>
        <button type="submit" :disabled="writing || loading || cleanupOpen || transferRow !== null || needsReview || !absenceConfirmed">{{ changingAbsence ? 'Đang xử lý…' : 'Xác nhận' }}</button>
        <button type="button" :disabled="changingAbsence" @click="closeAbsence">Quay lại</button>
      </form>
    </section>
    <section v-if="pauseRow" class="absence-panel" aria-labelledby="pause-title">
      <h2 id="pause-title">Tạm dừng ca khám (§1.6)</h2>
      <p>{{ numberLabel(pauseRow.queueNumber) }} · {{ pauseRow.patientName }} · {{ pauseRow.patientCode }}</p>
      <p>Hệ thống sẽ nhả phòng/ghế điều trị hiện tại, giữ nguyên Visit, lịch sử khám và số thứ tự chờ; đưa ca về hàng đợi trạng thái Tạm dừng mà không chen lên trước ca khác.</p>
      <form @submit.prevent="handlePause">
        <label for="pause-reason">Lý do tạm dừng (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="pause-reason" v-model="pauseReason" maxlength="500" :disabled="pausing" />
        <label><input v-model="pauseConfirmed" type="checkbox" :disabled="pausing || cleanupOpen || needsReview" /> Tôi xác nhận tạm dừng ca và nhả phòng/ghế hiện tại.</label>
        <button type="submit" :disabled="pausing || loading || cleanupOpen || needsReview || !pauseConfirmed">{{ pausing ? 'Đang tạm dừng…' : 'Xác nhận tạm dừng' }}</button>
        <button type="button" :disabled="pausing" @click="closePause">Quay lại</button>
      </form>
    </section>
    <section v-if="resumeRow" class="absence-panel" aria-labelledby="resume-title">
      <h2 id="resume-title">Tiếp tục ca khám tạm dừng (§1.6)</h2>
      <p>{{ numberLabel(resumeRow.queueNumber) }} · {{ resumeRow.patientName }} · {{ resumeRow.patientCode }}</p>
      <p>Nha sĩ tiếp nhận sẽ liên kết phòng/ghế làm việc của mình và tiếp tục cùng Visit ban đầu.</p>
      <form @submit.prevent="handleResume">
        <template v-if="authStore.role === 'ADMIN'">
          <label for="resume-dentist">Nha sĩ tiếp nhận</label>
          <select id="resume-dentist" v-model.number="resumeDentistId" required :disabled="resuming || dentistsLoading || needsReview">
            <option :value="0" disabled>Chọn nha sĩ</option>
            <option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">
              {{ dentist.dentistName }} · {{ locationLabel(dentist) }} · #{{ dentist.dentistId }}
            </option>
          </select>
        </template>
        <label for="resume-reason">Ghi chú (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="resume-reason" v-model="resumeReason" maxlength="500" :disabled="resuming" />
        <label><input v-model="resumeConfirmed" type="checkbox" :disabled="resuming || cleanupOpen || needsReview" /> Tôi xác nhận tiếp tục ca khám.</label>
        <button type="submit" :disabled="resuming || loading || cleanupOpen || needsReview || !resumeConfirmed || (authStore.role === 'ADMIN' && !resumeDentistId)">{{ resuming ? 'Đang tiếp tục…' : 'Xác nhận tiếp tục' }}</button>
        <button type="button" :disabled="resuming" @click="closeResume">Quay lại</button>
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
import { computed, onMounted, onUnmounted, ref, type ComponentPublicInstance } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { queueService, maskPublicQueueName, type QueueEntryResponse, type CallingDentist } from '@/services/queue';
const rows = ref<QueueEntryResponse[]>([]), status = ref(1), page = ref(1), total = ref(0), hasNext = ref(false);
function locationLabel(item: { roomName?: string | null; chairNumber?: string | null }): string {
  return item.roomName || 'Chưa nhận phòng';
}
const loading = ref(false), callingNext = ref(false), error = ref(''), actionMsg = ref(''), updatedAt = ref('');
const authStore = useAuthStore(), canCallNext = computed(() => ['ADMIN', 'DENTIST', 'ASSISTANT'].includes(authStore.role));
const canTransferQueue = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST'].includes(authStore.role));
const needsCallingDentist = computed(() => ['ADMIN', 'ASSISTANT'].includes(authStore.role));
const canCancelWaiting = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const cancelClinical = ref(false);
const cancelRow = ref<QueueEntryResponse | null>(null), cancelReason = ref(''), cancelConfirmed = ref(false), cancelling = ref(false);
const imagingRow = ref<QueueEntryResponse | null>(null), imagingReason = ref(''), imagingConfirmed = ref(false), routingImaging = ref(false);
const absenceRow = ref<QueueEntryResponse | null>(null), absenceReason = ref(''), absenceConfirmed = ref(false), changingAbsence = ref(false);
const pauseRow = ref<QueueEntryResponse | null>(null), pauseReason = ref(''), pauseConfirmed = ref(false), pausing = ref(false);
const resumeRow = ref<QueueEntryResponse | null>(null), resumeDentistId = ref(0), resumeReason = ref(''), resumeConfirmed = ref(false), resuming = ref(false);
const transferRow = ref<QueueEntryResponse | null>(null), transferDentistId = ref(0), transferReason = ref(''), transferConfirmed = ref(false), transferring = ref(false);
const cleanupOpen = ref(false), cleanupConfirmed = ref(false), cleaningUp = ref(false);
const writing = computed(() => callingNext.value || cancelling.value || routingImaging.value || changingAbsence.value || pausing.value || resuming.value || transferring.value || cleaningUp.value);
const dentists = ref<CallingDentist[]>([]), callDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref(''), needsReview = ref(false);
const transferTargets = computed(() => dentists.value.filter(dentist => dentist.dentistId !== transferRow.value?.dentistId));
let version = 0, life = 0, dentistVersion = 0;
async function loadDentists() {
  if (!(needsCallingDentist.value || transferRow.value !== null) || writing.value) return;
  transferConfirmed.value = false;
  transferDentistId.value = 0;
  const ticket = ++dentistVersion; dentistsLoading.value = true; dentistError.value = ''; dentists.value = [];
  try { const result = await queueService.getCallingDentists(); if (ticket === dentistVersion) { dentists.value = result; if (!result.some(d => d.dentistId === callDentistId.value)) callDentistId.value = 0; } }
  catch { if (ticket === dentistVersion) { callDentistId.value = 0; dentistError.value = 'Không thể tải nha sĩ hoạt động. Tải lại trước khi gọi ca.'; } }
  finally { if (ticket === dentistVersion) dentistsLoading.value = false; }
}
async function load(targetPage = 1) {
  if (cancelling.value || routingImaging.value || changingAbsence.value || pausing.value || resuming.value || transferring.value || cleaningUp.value) return;
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000 || ![0, 1, 2, 3, 4, 5, 6, 7, 8].includes(status.value)) return;
  closeCancellation();
  closeImaging();
  closeAbsence();
  closePause();
  closeResume();
  closeTransfer();
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
function emitTvCall(entry: QueueEntryResponse) {
  if (entry.status !== 6) return;
  const payload = {
    queueNumberDisplay: numberLabel(entry.queueNumber),
    maskedPatientName: maskPublicQueueName(entry.patientName),
    roomName: entry.roomName || null,
    status: entry.status
  };
  try {
    const ch = new BroadcastChannel('dental_tv_call_channel');
    ch.postMessage(payload);
    ch.close();
  } catch {}
  actionMsg.value = `Đã phát tín hiệu gọi số ${numberLabel(entry.queueNumber)} - ${entry.patientName} lên màn hình TV.`;
}

async function handleCallNext() {
  if (!canCallNext.value || writing.value || transferRow.value !== null || cancelRow.value !== null || imagingRow.value !== null || absenceRow.value !== null || loading.value || cleanupOpen.value || needsReview.value) return;
  if (needsCallingDentist.value && (dentistsLoading.value || !Number.isInteger(callDentistId.value) || !dentists.value.some(d => d.dentistId === callDentistId.value))) { dentistError.value = 'Chọn nha sĩ hoạt động đã tải để gọi ca.'; return; }
  const ticket = life;
  callingNext.value = true; error.value = ''; actionMsg.value = '';
  try {
    const entry = await queueService.callNext(needsCallingDentist.value ? { dentistId: callDentistId.value } : undefined);
    if (ticket !== life) return;
    if (entry.status !== 6) throw new Error('Unexpected invitation status');
    emitTvCall(entry);
    actionMsg.value = `Đã mời số ${numberLabel(entry.queueNumber)} - ${entry.patientName} ${locationLabel(entry)}; đã gửi thông báo TV.`;
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
  if (transferRow.value !== null || !(clinical ? canCancelClinical(row) : canCancelWaiting.value && row.status === 1 && row.canCancelConsultation !== true) || writing.value || imagingRow.value !== null || absenceRow.value !== null || loading.value || cleanupOpen.value || needsReview.value ||
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
  if (transferRow.value !== null || !canRouteImaging(row) || writing.value || loading.value || cancelRow.value !== null || absenceRow.value !== null || cleanupOpen.value || needsReview.value ||
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
function canPause(row: QueueEntryResponse) {
  if (row.status !== 2) return false;
  return authStore.role === 'ADMIN' || (authStore.role === 'DENTIST' && row.dentistId === authStore.user?.userId);
}
function canResume(row: QueueEntryResponse) {
  if (row.status !== 8) return false;
  return ['ADMIN', 'DENTIST'].includes(authStore.role);
}
function clearPause() { pauseRow.value = null; pauseReason.value = ''; pauseConfirmed.value = false; }
function closePause() { if (!pausing.value) clearPause(); }
function openPause(row: QueueEntryResponse) {
  if (!canPause(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || pauseRow.value || resumeRow.value || cleanupOpen.value || needsReview.value) return;
  clearPause(); pauseRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handlePause() {
  const selected = pauseRow.value;
  if (!selected || !canPause(selected) || writing.value || loading.value || cleanupOpen.value || needsReview.value || !pauseConfirmed.value) return;
  if (pauseReason.value.length > 500) { error.value = 'Lý do tối đa 500 ký tự.'; return; }
  const ticket = life; pausing.value = true; error.value = ''; actionMsg.value = '';
  try {
    const result = await queueService.pauseVisit(selected.queueEntryId, pauseReason.value.trim() || null);
    if (ticket !== life) return;
    actionMsg.value = `Đã tạm dừng ca ${numberLabel(result.queueNumber)} - ${result.patientName}, giải phóng phòng làm việc.`;
    clearPause(); needsReview.value = true; pausing.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả tạm dừng.';
    pauseConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) pausing.value = false; }
}

function clearResume() { resumeRow.value = null; resumeDentistId.value = 0; resumeReason.value = ''; resumeConfirmed.value = false; }
function closeResume() { if (!resuming.value) clearResume(); }
function openResume(row: QueueEntryResponse) {
  if (!canResume(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || pauseRow.value || resumeRow.value || cleanupOpen.value || needsReview.value) return;
  clearResume(); resumeRow.value = { ...row }; error.value = ''; actionMsg.value = '';
  if (authStore.role === 'ADMIN' && !dentists.value.length) void loadDentists();
}
async function handleResume() {
  const selected = resumeRow.value;
  if (!selected || !canResume(selected) || writing.value || loading.value || cleanupOpen.value || needsReview.value || !resumeConfirmed.value) return;
  if (authStore.role === 'ADMIN' && !resumeDentistId.value) { error.value = 'Vui lòng chọn nha sĩ tiếp nhận ca.'; return; }
  if (resumeReason.value.length > 500) { error.value = 'Ghi chú tối đa 500 ký tự.'; return; }
  const ticket = life; resuming.value = true; error.value = ''; actionMsg.value = '';
  try {
    const targetDentist = authStore.role === 'DENTIST' ? authStore.user?.userId || null : resumeDentistId.value || null;
    const result = await queueService.resumeVisit(selected.queueEntryId, targetDentist, resumeReason.value.trim() || null);
    if (ticket !== life) return;
    actionMsg.value = `Đã tiếp tục ca ${numberLabel(result.queueNumber)} - ${result.patientName} tại ${locationLabel(result)}.`;
    clearResume(); needsReview.value = true; resuming.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả tiếp tục ca.';
    resumeConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) resuming.value = false; }
}

function canChangeAbsence(row: QueueEntryResponse) {
  if (row.status === 7) return ['ADMIN', 'RECEPTIONIST'].includes(authStore.role);
  return row.status === 6 && row.canMarkAbsent === true &&
    (['ADMIN', 'RECEPTIONIST'].includes(authStore.role) || (authStore.role === 'DENTIST' && row.dentistId === authStore.user?.userId));
}
function canTransfer(row: QueueEntryResponse) {
  if (!canTransferQueue.value || !Number.isInteger(row.dentistId) || !row.dentistId) return false;
  if (authStore.role === 'DENTIST' && row.dentistId !== authStore.user?.userId) return false;
  if (row.status === 1) return true;
  return row.canCancelConsultation === true && [2, 5, 6].includes(row.status);
}
let transferDialog: HTMLDialogElement | null = null;
function mountTransferDialog(element: Element | ComponentPublicInstance | null) {
  transferDialog = element instanceof HTMLDialogElement ? element : null;
  if (transferDialog && !transferDialog.open) transferDialog.showModal();
}
function clearTransfer() {
  transferDialog?.close();
  transferRow.value = null; transferDentistId.value = 0; transferReason.value = ''; transferConfirmed.value = false;
}
function closeTransfer() { if (!transferring.value) clearTransfer(); }
function openTransfer(row: QueueEntryResponse) {
  if (!canTransfer(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || cleanupOpen.value || needsReview.value ||
      !rows.value.some(item => item.queueEntryId === row.queueEntryId && item.status === row.status && item.dentistId === row.dentistId)) return;
  clearTransfer(); transferRow.value = { ...row }; error.value = ''; actionMsg.value = '';
  if (!dentists.value.length) void loadDentists();
}
async function handleTransfer() {
  const selected = transferRow.value, targetId = transferDentistId.value;
  if (!selected || !canTransfer(selected) || writing.value || loading.value || dentistsLoading.value || cleanupOpen.value || needsReview.value || !transferConfirmed.value ||
      cancelRow.value || imagingRow.value || absenceRow.value ||
      !Number.isInteger(targetId) || !transferTargets.value.some(dentist => dentist.dentistId === targetId) ||
      !rows.value.some(item => item.queueEntryId === selected.queueEntryId && item.status === selected.status && item.dentistId === selected.dentistId)) return;
  if (transferReason.value.length > 500) { error.value = 'Lý do chuyển tối đa 500 ký tự.'; return; }
  const ticket = life; transferring.value = true; error.value = ''; actionMsg.value = '';
  try {
    const result = await queueService.transferDentist(selected.queueEntryId, { targetDentistId: targetId, expectedDentistId: selected.dentistId!, expectedStatus: selected.status, confirmed: true, reason: transferReason.value.trim() || null });
    if (ticket !== life) return;
    if (result.queueEntryId !== selected.queueEntryId || result.dentistId !== targetId || result.status !== selected.status || result.visitId !== selected.visitId) throw new Error('Invalid transfer result');
    const target = dentists.value.find(dentist => dentist.dentistId === targetId);
    actionMsg.value = `Đã chuyển số ${numberLabel(result.queueNumber)} sang ${target?.dentistName || 'nha sĩ mới'} · ${locationLabel(result)}.`;
    clearTransfer(); needsReview.value = true; transferring.value = false; await load(page.value);
  } catch (err: unknown) {
    if (ticket !== life) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả chuyển. Tải lại hàng đợi để đối chiếu.';
    transferConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) transferring.value = false; }
}
function clearAbsence() { absenceRow.value = null; absenceReason.value = ''; absenceConfirmed.value = false; }
function closeAbsence() { if (!changingAbsence.value) clearAbsence(); }
function openAbsence(row: QueueEntryResponse) {
  if (transferRow.value !== null || !canChangeAbsence(row) || writing.value || loading.value || cleanupOpen.value || needsReview.value || cancelRow.value !== null || imagingRow.value !== null ||
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
  if (!canCancelWaiting.value || transferRow.value !== null || writing.value || loading.value || needsReview.value || cancelRow.value || imagingRow.value || absenceRow.value) return;
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
onUnmounted(() => { life++; version++; dentistVersion++; clearCancellation(); clearImaging(); clearAbsence(); clearTransfer(); cleanupOpen.value = false; cleanupConfirmed.value = false; rows.value = []; dentists.value = []; callDentistId.value = 0; dentistError.value = ''; actionMsg.value = ''; error.value = ''; needsReview.value = false; updatedAt.value = ''; total.value = 0; });
</script>
<style scoped>
.page {
  --ink: #172b42;
  --muted: #66788a;
  --line: #dce5ed;
  --teal: #0f746f;
  --teal-dark: #0a5c59;
  max-width: 1380px;
  margin: 0 auto 2rem;
  padding: 1.75rem clamp(1rem, 2vw, 2rem) 2.5rem;
  box-sizing: border-box;
  color: var(--ink);
}
.page-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 1.25rem; margin: 0 0 1.5rem; }
.heading-copy { min-width: 0; }
.eyebrow { margin: 0 0 .35rem; color: var(--teal); font-size: .72rem; letter-spacing: .13em; font-weight: 800; }
h1 { margin: 0; font-size: clamp(1.7rem, 2.5vw, 2.2rem); line-height: 1.15; letter-spacing: -.035em; color: #12283d; }
.intro { margin: .55rem 0 0; color: var(--muted); font-size: .96rem; }
.tv-link { display: inline-flex; align-items: center; justify-content: center; gap: .55rem; min-height: 46px; flex: 0 0 auto; padding: .65rem 1rem; border: 1px solid #c7d7e4; border-radius: 10px; color: #24445e; background: #fff; text-decoration: none; font-weight: 700; transition: background .15s ease, border-color .15s ease, transform .15s ease; }
.tv-link:hover { background: #f4fafb; border-color: #87bdb9; transform: translateY(-1px); }
.toolbar { display: flex; flex-wrap: wrap; align-items: flex-end; gap: .75rem; padding: 1rem; border: 1px solid var(--line); border-radius: 14px; background: #fff; box-shadow: 0 5px 18px rgb(27 55 79 / 5%); }
.field-group { display: grid; gap: .35rem; min-width: 185px; }
.field-group label { font-size: .78rem; font-weight: 750; color: #496175; }
.field-dentist { flex: 1 1 270px; }
button, select, textarea, input[type="text"] { max-width: 100%; min-height: 44px; box-sizing: border-box; font: inherit; border: 1px solid #c9d6e1; border-radius: 9px; background: #fff; color: #20384e; }
select { width: 100%; padding: .6rem .75rem; }
button { padding: .62rem .9rem; cursor: pointer; font-weight: 700; transition: background .15s ease, border-color .15s ease, color .15s ease; }
button:hover:not(:disabled) { background: #f1f7f8; border-color: #93b7b5; }
button:disabled { opacity: .56; cursor: not-allowed; }
button:focus-visible, select:focus-visible, textarea:focus-visible, a:focus-visible, input:focus-visible { outline: 3px solid #64b5ad; outline-offset: 2px; }
.btn-call-next { border-color: var(--teal); background: var(--teal); color: #fff; }
.btn-call-next:hover:not(:disabled) { border-color: var(--teal-dark); background: var(--teal-dark); color: #fff; }
.button-secondary { background: #f8fafc; }
.button-warning { color: #8a4a08; background: #fffaf0; border-color: #f0d39d; }
.button-warning:hover:not(:disabled) { background: #fff3db; border-color: #e8bb67; }
.workflow-hint { margin: .8rem 0 0; color: #5e7183; font-size: .88rem; line-height: 1.5; }
.notice { margin: 1rem 0 0; padding: .85rem 1rem; border: 1px solid; border-radius: 10px; font-weight: 600; line-height: 1.5; overflow-wrap: anywhere; }
.notice-loading { color: #355b72; background: #f2f8fb; border-color: #cfe1eb; }
.notice-success { color: #155e50; background: #eef9f4; border-color: #b8dfd1; }
.notice-warning { color: #7b5214; background: #fff8e8; border-color: #ecd59c; }
.notice-error { color: #9b3434; background: #fff4f3; border-color: #efc4c0; }
.queue-summary { display: flex; align-items: center; gap: .55rem; margin: 1.45rem 0 .7rem; color: var(--muted); font-size: .86rem; }
.queue-summary strong { color: #243c52; font-size: .95rem; }
.entries { list-style: none; padding: 0; margin: 0; display: grid; gap: .85rem; }
.queue-entry { min-width: 0; padding: 1.05rem 1.1rem; border: 1px solid var(--line); background: #fff; border-radius: 14px; box-shadow: 0 3px 12px rgb(29 56 80 / 4%); box-sizing: border-box; overflow-wrap: anywhere; }
.queue-entry.priority { border-left: 4px solid #c89435; }
.queue-entry-header { display: grid; grid-template-columns: 62px minmax(0, 1fr) auto; align-items: center; gap: .85rem; }
.queue-ticket { display: grid; place-items: center; min-height: 46px; border-radius: 10px; color: #075f5e; background: #e8f5f3; font-size: .95rem; font-weight: 850; letter-spacing: .015em; }
.queue-patient { display: grid; min-width: 0; gap: .2rem; }
.queue-patient-row { display: flex; flex-wrap: wrap; align-items: center; gap: .4rem; }
.queue-patient strong { color: #183149; font-size: 1.02rem; line-height: 1.3; }
.queue-patient > span { color: var(--muted); font-size: .82rem; }
.child-pill, .missing-account-pill { display: inline-flex; align-items: center; padding: .15rem .5rem; border-radius: 999px; font-size: .73rem; font-weight: 700; line-height: 1.2; }
.child-pill { background: #dbeafe; color: #1e40af; }
.missing-account-pill { background: #fef3c7; border: 1px solid #f59e0b; color: #92400e; }
.status-pill, .priority-pill, .queue-type { display: inline-flex; align-items: center; justify-content: center; width: fit-content; min-height: 28px; padding: .3rem .65rem; border-radius: 999px; font-size: .78rem; font-weight: 750; line-height: 1.2; }
.status-pill { background: #f1f5f8; color: #53687b; }
.status-1 { background: #fff4da; color: #87590d; }
.status-2, .status-6 { background: #eaf3ff; color: #285d91; }
.status-5 { background: #f3edff; color: #684c9b; }
.status-7, .status-8 { background: #fff0e8; color: #99502a; }
.status-3 { background: #e9f6ee; color: #276846; }
.status-4 { background: #f1f2f4; color: #697583; }
.queue-entry-meta { display: flex; flex-wrap: wrap; align-items: center; gap: .55rem 1.1rem; margin: .8rem 0 0 4.85rem; color: #587084; font-size: .84rem; }
.queue-entry-meta > span:not(.priority-pill):not(.queue-type) { display: inline-flex; flex-wrap: wrap; gap: .3rem; }
.queue-entry-meta b { color: #304b62; }
.priority-pill { color: #785313; background: #fff5df; border: 1px solid #efdfba; }
.queue-type { color: #5d7081; background: #f3f6f8; }
.care-note { display: grid; gap: .22rem; margin: .8rem 0 0 4.85rem; padding: .65rem .8rem; border: 1px solid #efe1bc; border-radius: 9px; color: #675629; background: #fffaf0; font-size: .84rem; line-height: 1.45; white-space: pre-wrap; overflow-wrap: anywhere; }
.care-note strong { color: #745419; font-size: .77rem; }
.queue-card-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .9rem 0 0 4.85rem; }
.queue-card-actions button, .queue-card-actions a { display: inline-flex; align-items: center; justify-content: center; min-height: 44px; padding: .5rem .75rem; border: 1px solid #d4dfe7; border-radius: 8px; color: #35526a; background: #fff; text-decoration: none; font-size: .82rem; font-weight: 700; line-height: 1.25; text-align: center; }
.queue-card-actions a { color: #fff; border-color: #255c81; background: #255c81; }
.queue-card-actions a:hover { background: #194766; }
.queue-card-actions .btn-recall-tv { color: #72520f; border-color: #ead69a; background: #fff7dc; }
.queue-card-actions .action-danger { color: #a13e3b; border-color: #e7c2bf; background: #fff8f7; }
.empty-state { display: grid; justify-items: center; gap: .45rem; padding: 2rem 1rem; border: 1px dashed #c7d6df; border-radius: 14px; color: #5f7384; background: #fff; text-align: center; }
.empty-state strong { color: #24445b; }
.empty-icon { display: grid; place-items: center; width: 38px; height: 38px; margin-bottom: .25rem; border-radius: 50%; color: #27674f; background: #e9f6ee; font-weight: 850; }
.cancel-panel, .imaging-panel, .absence-panel { margin: 1rem 0; padding: 1.2rem; border: 1px solid var(--line); border-radius: 14px; background: #fff; box-shadow: 0 4px 15px rgb(29 56 80 / 4%); box-sizing: border-box; }
.cancel-panel { border-color: #efc8c4; background: #fffafa; }
.imaging-panel { border-color: #c9dfe6; background: #f8fcfd; }
.absence-panel { border-color: #ecd9a6; background: #fffdf7; }
.transfer-panel {
  position: fixed;
  inset: 0;
  margin: auto;
  width: min(34rem, calc(100% - 2rem));
  max-width: none;
  max-height: calc(100vh - 2rem);
  max-height: calc(100dvh - 2rem);
  box-sizing: border-box;
  overflow: auto;
  padding: clamp(1rem, 3vw, 1.5rem);
  border: 1px solid #c9dfe6;
  border-radius: 14px;
  background: #fff;
  color: var(--ink);
  box-shadow: 0 18px 52px rgb(29 56 80 / 22%);
}
.transfer-panel::backdrop { background: rgb(17 36 56 / 45%); }
.transfer-panel p { overflow-wrap: anywhere; }
.transfer-panel .transfer-notice { margin: 0; }
.transfer-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: .5rem; }
.cancel-panel h2, .imaging-panel h2, .absence-panel h2, .transfer-panel h2 { margin: 0 0 .6rem; font-size: 1.12rem; color: #1b354b; }
.cancel-panel form, .imaging-panel form, .absence-panel form, .transfer-panel form { display: grid; gap: .75rem; }
textarea { width: 100%; min-height: 5.5rem; padding: .7rem .8rem; resize: vertical; }
form label { color: #3d566b; font-size: .88rem; line-height: 1.45; }
form label input[type="checkbox"] { width: 17px; height: 17px; min-height: 0; margin: 0 .45rem 0 0; vertical-align: middle; accent-color: var(--teal); }
nav[aria-label="Phân trang hàng đợi"] { display: flex; justify-content: flex-end; gap: .65rem; margin-top: 1.1rem; }
[role="alert"] { overflow-wrap: anywhere; }
@media (max-width: 760px) {
  .page { padding: 1.1rem .8rem 2rem; }
  .page-header { align-items: flex-start; flex-direction: column; }
  .tv-link { align-self: stretch; }
  .toolbar { align-items: stretch; padding: .8rem; }
  .field-group, .toolbar > button { min-width: 0; width: 100%; }
  .queue-entry-header { grid-template-columns: 54px minmax(0, 1fr); gap: .65rem; }
  .status-pill { grid-column: 2; }
  .queue-entry-meta, .care-note, .queue-card-actions { margin-left: 0; }
  .queue-card-actions > * { flex: 1 1 160px; }
}
@media (max-width: 420px) {
  .queue-summary { flex-wrap: wrap; }
  .queue-entry { padding: .85rem; }
  .queue-entry-meta { display: grid; gap: .5rem; }
  .queue-card-actions { display: grid; grid-template-columns: 1fr; }
  .queue-card-actions > * { width: 100%; min-width: 0; }
  nav[aria-label="Phân trang hàng đợi"] { display: grid; grid-template-columns: 1fr 1fr; }
}
</style>
