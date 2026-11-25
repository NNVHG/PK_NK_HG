<template>
  <main class="page">
    <header class="hero-banner">
      <div class="hero-copy">
        <div class="hero-kicker-row">
          <span class="eyebrow">ĐIỀU PHỐI KHÁM TRONG NGÀY</span>
          <span class="date-badge">{{ todayLabel }}</span>
        </div>
        <h1>Hàng đợi hôm nay</h1>
        <p class="intro">Điều phối tiếp đón, gọi số vào phòng khám và theo dõi tiến trình điều trị theo thời gian thực.</p>
      </div>
      <div class="hero-actions">
        <router-link
          v-if="canViewBookingRequests"
          to="/management/appointments/requests"
          class="hero-btn hero-btn-ghost"
        >
          Yêu cầu chờ duyệt →
        </router-link>
        <router-link to="/queue/display" target="_blank" class="hero-btn hero-btn-solid">
          Mở màn hình phòng chờ <span aria-hidden="true">↗</span>
        </router-link>
      </div>
    </header>

    <section class="auto-rollover-banner" aria-label="Cơ chế chuyển ngày tự động">
      <span class="rollover-icon" aria-hidden="true">🕛</span>
      <div class="rollover-copy">
        <strong>Chuyển ngày tự động lúc 00:00</strong>
        <span>
          Sau khi qua ngày mới theo giờ Việt Nam, các lượt chưa kịp bắt đầu khám còn trong hàng đợi sẽ được hệ thống tự động chuyển thành
          <strong>Yêu cầu đặt hẹn trực tuyến chờ duyệt</strong> cho ngày hôm sau. Ca đang khám hoặc đã khóa được giữ nguyên.
        </span>
      </div>
      <router-link
        v-if="canViewBookingRequests"
        to="/management/appointments/requests"
        class="rollover-link"
      >
        Xem danh sách chờ duyệt
      </router-link>
    </section>

    <section v-if="linkedEntryId" class="queue-link-panel" aria-label="Lượt mở từ quầy">
      <p v-if="linkedEntryLoading" role="status">Đang tải lượt được chọn…</p>
      <p v-else-if="linkedEntryError" class="notice notice-error" role="alert">{{ linkedEntryError }}</p>
      <template v-else-if="linkedEntry">
        <strong>{{ numberLabel(linkedEntry.queueNumber) }} · {{ linkedEntry.patientName }}</strong>
        <span>{{ linkedEntry.statusText }} · {{ linkedEntry.queueDate }}</span>
        <a :href="'#queue-entry-' + linkedEntry.queueEntryId" v-if="rows.some(row => row.queueEntryId === linkedEntry?.queueEntryId)">Đến lượt trong danh sách</a>
      </template>
    </section>

    <section class="control-dock" aria-label="Điều khiển hàng đợi">
      <div class="toolbar">
        <div class="field-group">
          <label for="status">Lọc theo trạng thái</label>
          <select id="status" v-model.number="status" :disabled="writing" @change="load(1)">
            <option :value="0">Tất cả trạng thái</option><option :value="1">Đang chờ</option><option :value="2">Đang khám</option>
            <option :value="8">Tạm dừng</option><option :value="7">Vắng mặt</option><option :value="6">Đang mời vào phòng</option><option :value="5">Đang chụp ảnh</option><option :value="3">Khám xong</option><option :value="4">Đã hủy</option>
          </select>
        </div>
        <button class="button-secondary" :disabled="loading || writing" @click="load(page)">{{ loading ? 'Đang tải…' : 'Tải lại' }}</button>
        <template v-if="needsCallingDentist">
          <div v-if="teamDentistId" class="field-group">
            <label for="queue-team">Phạm vi hàng đợi</label>
            <select id="queue-team" v-model="showTeamOnly" :disabled="writing" @change="load(1)">
              <option :value="true">Nhóm tôi hỗ trợ</option>
              <option :value="false">Tất cả</option>
            </select>
          </div>
          <div class="field-group field-dentist">
            <label for="call-dentist">Nha sĩ gọi khám</label>
            <select id="call-dentist" v-model.number="callDentistId" :disabled="writing || dentistsLoading">
              <option :value="0">Chọn nha sĩ</option><option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">{{ dentist.dentistName }} · {{ locationLabel(dentist) }}</option>
            </select>
          </div>
          <button class="button-secondary" :disabled="writing || dentistsLoading" @click="loadDentists">Tải nha sĩ</button>
        </template>
        <button
          v-if="canCallNext"
          :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || transferRow !== null || needsReview || (needsCallingDentist && (dentistsLoading || !callDentistId))"
          class="btn-call-next"
          @click="handleCallNext"
        >
          {{ callingNext ? 'Đang gọi…' : (authStore.role === 'DENTIST' ? 'Gọi số & vào khám ngay' : authStore.role === 'ASSISTANT' ? 'Gọi số & đo sinh hiệu' : 'Gọi số tiếp theo') }}
        </button>
      </div>

      <div class="status-chips" role="group" aria-label="Lọc nhanh trạng thái lượt khám">
        <button
          v-for="item in statusQuickFilters"
          :key="item.value"
          type="button"
          class="status-chip"
          :class="{ active: status === item.value }"
          :disabled="loading || writing"
          @click="selectStatus(item.value)"
        >
          {{ item.label }}
        </button>
      </div>

      <div v-if="canCallNext || authStore.role === 'ASSISTANT'" class="dock-hints">
        <p v-if="canCallNext" class="workflow-hint"><strong>Lưu ý:</strong> Gọi số chỉ mời bệnh nhân vào phòng. Nha sĩ/Admin bắt đầu khám riêng tại hồ sơ lâm sàng.</p>
        <p v-if="authStore.role === 'ASSISTANT'" class="workflow-hint">Phụ tá gọi số dùm nha sĩ trên lượt đã phân công và tự mở màn hình Đo sinh hiệu &amp; Tiền khám cho đúng bệnh nhân đó; lượt chưa phân cần Lễ tân/Admin đối chiếu.</p>
      </div>
    </section>

    <p v-if="loading" class="notice notice-loading" role="status">Đang tải hàng đợi…</p>
    <p v-if="actionMsg" class="notice notice-success" role="status">{{ actionMsg }}</p>
    <p v-if="error" class="notice notice-error" role="alert">{{ error }}</p>
    <p v-if="dentistError" class="notice notice-error" role="alert">{{ dentistError }}</p>
    <p v-if="needsReview" class="notice notice-warning" role="alert">Tải lại hàng đợi thành công và đối chiếu trạng thái trước khi thao tác tiếp.</p>

    <div v-if="!loading && !error" class="queue-summary" role="status">
      <div class="summary-stat">
        <span class="summary-label">Tổng số lượt</span>
        <strong>{{ total }} lượt</strong>
      </div>
      <span class="summary-divider" aria-hidden="true">·</span>
      <span>Trang {{ page }}</span>
      <span class="summary-divider" aria-hidden="true">·</span>
      <span>Cập nhật {{ updatedAt || 'chưa có dữ liệu' }}</span>
    </div>

    <div v-if="!loading && !error && !rows.length" class="empty-state" role="status">
      <span class="empty-icon" aria-hidden="true">✓</span>
      <strong>Chưa có lượt tiếp đón phù hợp</strong>
      <span>Danh sách sẽ tự động cập nhật khi có bệnh nhân check-in trong ngày.</span>
    </div>

    <ol class="entries" aria-label="Danh sách hàng đợi hôm nay">
      <li v-for="row in rows" :id="'queue-entry-' + row.queueEntryId" :key="row.queueEntryId" class="queue-entry" :class="[{ priority: row.isPriority }, `entry-status-${row.status}`]">
        <div class="queue-entry-header">
          <span class="queue-ticket" :class="`ticket-status-${row.status}`">{{ numberLabel(row.queueNumber) }}</span>
          <div class="queue-patient">
            <div class="queue-patient-row">
              <strong>{{ row.patientName }}</strong>
              <span v-if="row.isChildPatient" class="child-pill">👶 Trẻ &lt;14</span>
              <span v-if="row.isPriority" class="priority-pill">Lịch hẹn đúng giờ</span>
              <span v-else class="queue-type">Lượt thường</span>
            </div>
            <span class="patient-sub">{{ row.patientCode }} · {{ row.queueDate }}</span>
          </div>
          <span class="status-pill" :class="`status-${row.status}`">{{ row.statusText }}</span>
        </div>

        <div class="queue-entry-meta">
          <span class="meta-chip"><b>Nha sĩ:</b> {{ row.dentistName || 'Chưa phân công' }}</span>
          <span class="meta-chip"><b>Phòng:</b> {{ locationLabel(row) }}</span>
        </div>

        <p v-if="row.notes" class="care-note"><strong>Ghi chú cần lưu ý</strong>{{ row.notes }}</p>

        <div class="queue-card-actions">
          <button v-if="canCallOnTv(row)" type="button" class="btn-recall-tv"
            :disabled="loading || writing || needsReview" @click="handleCallOnTv(row)">
            {{ callingOnTv ? 'Đang gửi lời mời…' : 'Gọi lại trên TV' }}
          </button>
          <RouterLink
            v-if="canOpenClinical(row)"
            :to="clinicalLocation(row)"
            :class="[2, 6].includes(row.status) ? 'btn-open-clinical' : 'btn-open-clinical-secondary'"
          >{{ clinicalLinkLabel(row) }}</RouterLink>
          <template v-if="canOpenAssistantVitals(row)">
            <RouterLink
              :to="assistantVitalsLocation(row)"
              :class="[2, 6].includes(row.status) ? 'btn-open-clinical' : 'btn-open-clinical-secondary'"
            >Đo sinh hiệu →</RouterLink>
            <RouterLink :to="patientLocation(row, 'images')">Ảnh hồ sơ</RouterLink>
          </template>
          <button v-if="canCancelWaiting && row.status === 1 && row.canCancelConsultation !== true" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || transferRow !== null || needsReview" @click="openCancellation(row)">Hủy lượt chờ</button>
          <button v-if="canCancelClinical(row)" class="action-danger" :disabled="loading || writing || imagingRow !== null || absenceRow !== null || transferRow !== null || needsReview" @click="openCancellation(row, true)">Hủy ca đang khám</button>
          <button v-if="canRouteImaging(row)" :disabled="loading || writing || cancelRow !== null || absenceRow !== null || transferRow !== null || needsReview" @click="openImaging(row)">{{ row.status === 2 ? 'Chỉ định chụp ảnh' : 'Hoàn tất chụp → chờ nha sĩ' }}</button>
          <button v-if="canChangeAbsence(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || transferRow !== null || needsReview" @click="openAbsence(row)">{{ row.status === 7 ? 'Gọi lại → hàng chờ' : 'Vắng mặt' }}</button>
          <button v-if="canPause(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || transferRow !== null || needsReview" @click="openPause(row)">Tạm dừng ca</button>
          <button v-if="canResume(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || transferRow !== null || needsReview" @click="openResume(row)">Tiếp tục ca</button>
          <button v-if="canTransfer(row)" :disabled="loading || writing || cancelRow !== null || imagingRow !== null || absenceRow !== null || pauseRow !== null || resumeRow !== null || transferRow !== null || needsReview" @click="openTransfer(row)">Chuyển nha sĩ</button>
        </div>
      </li>
    </ol>

    <section v-if="cancelRow" aria-labelledby="cancel-title" class="cancel-panel">
      <h2 id="cancel-title">{{ cancelClinical ? 'Hủy ca đã bắt đầu khám' : 'Hủy lượt chờ chưa khám' }}</h2>
      <p>{{ numberLabel(cancelRow.queueNumber) }} · {{ cancelRow.patientName }} · {{ cancelRow.patientCode }} · {{ cancelRow.queueDate }}</p>
      <p v-if="cancelClinical">Hủy ca sẽ nhả phòng điều trị và tự hủy hóa đơn Draft chưa thu trong cùng thao tác. Giữ lịch sử, lý do và các dòng hóa đơn. Ca đã phát sinh thanh toán không thể hủy qua thao tác này.</p>
      <p v-else>Chỉ hủy lượt đang chờ trước lần khám. Lượt đã bắt đầu khám hoặc trở về sau chụp ảnh cần xử lý theo quy trình chuyên môn.</p>
      <form @submit.prevent="handleCancellation">
        <label for="cancel-reason">Lý do ({{ cancelClinical ? 'bắt buộc' : 'không bắt buộc' }}, tối đa 500 ký tự)</label>
        <textarea id="cancel-reason" v-model="cancelReason" :required="cancelClinical" maxlength="500" :disabled="cancelling" />
        <label><input v-model="cancelConfirmed" type="checkbox" :disabled="cancelling || transferRow !== null || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận {{ cancelClinical ? 'hủy ca cùng Draft chưa thu' : 'hủy lượt chờ chưa khám' }}.</label>
        <div class="panel-actions">
          <button type="submit" class="action-danger" :disabled="cancelling || loading || transferRow !== null || needsReview || !cancelConfirmed">{{ cancelling ? 'Đang hủy…' : 'Xác nhận hủy lượt' }}</button>
          <button type="button" :disabled="cancelling" @click="closeCancellation">Quay lại</button>
        </div>
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
        <label><input v-model="imagingConfirmed" type="checkbox" :disabled="routingImaging || transferRow !== null || needsReview" />
          {{ imagingRow.status === 2 ? 'Tôi đã đối chiếu đúng bệnh nhân và chỉ định chụp ảnh.' : 'Tôi xác nhận bệnh nhân đã hoàn tất chụp theo quy trình phòng khám.' }}
        </label>
        <div class="panel-actions">
          <button type="submit" class="btn-call-next" :disabled="routingImaging || loading || transferRow !== null || needsReview || !imagingConfirmed">{{ routingImaging ? 'Đang chuyển…' : 'Xác nhận chuyển bước' }}</button>
          <button type="button" :disabled="routingImaging" @click="closeImaging">Quay lại</button>
        </div>
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
          <button type="submit" class="btn-call-next" :disabled="transferring || loading || needsReview || !transferConfirmed || !transferDentistId">
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
        <label><input v-model="absenceConfirmed" type="checkbox" :disabled="changingAbsence || transferRow !== null || needsReview" /> Tôi đã đối chiếu đúng bệnh nhân và xác nhận thao tác.</label>
        <div class="panel-actions">
          <button type="submit" class="btn-call-next" :disabled="writing || loading || transferRow !== null || needsReview || !absenceConfirmed">{{ changingAbsence ? 'Đang xử lý…' : 'Xác nhận' }}</button>
          <button type="button" :disabled="changingAbsence" @click="closeAbsence">Quay lại</button>
        </div>
      </form>
    </section>

    <section v-if="pauseRow" class="absence-panel" aria-labelledby="pause-title">
      <h2 id="pause-title">Tạm dừng ca khám (§1.6)</h2>
      <p>{{ numberLabel(pauseRow.queueNumber) }} · {{ pauseRow.patientName }} · {{ pauseRow.patientCode }}</p>
      <p>Hệ thống sẽ nhả phòng điều trị hiện tại, giữ nguyên Visit, lịch sử khám và số thứ tự chờ; đưa ca về hàng đợi trạng thái Tạm dừng mà không chen lên trước ca khác.</p>
      <form @submit.prevent="handlePause">
        <label for="pause-reason">Lý do tạm dừng (không bắt buộc, tối đa 500 ký tự)</label>
        <textarea id="pause-reason" v-model="pauseReason" maxlength="500" :disabled="pausing" />
        <label><input v-model="pauseConfirmed" type="checkbox" :disabled="pausing || needsReview" /> Tôi xác nhận tạm dừng ca và nhả phòng hiện tại.</label>
        <div class="panel-actions">
          <button type="submit" class="btn-call-next" :disabled="pausing || loading || needsReview || !pauseConfirmed">{{ pausing ? 'Đang tạm dừng…' : 'Xác nhận tạm dừng' }}</button>
          <button type="button" :disabled="pausing" @click="closePause">Quay lại</button>
        </div>
      </form>
    </section>

    <section v-if="resumeRow" class="absence-panel" aria-labelledby="resume-title">
      <h2 id="resume-title">Tiếp tục ca khám tạm dừng (§1.6)</h2>
      <p>{{ numberLabel(resumeRow.queueNumber) }} · {{ resumeRow.patientName }} · {{ resumeRow.patientCode }}</p>
      <p>Nha sĩ tiếp nhận sẽ liên kết phòng làm việc của mình và tiếp tục cùng Visit ban đầu.</p>
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
        <label><input v-model="resumeConfirmed" type="checkbox" :disabled="resuming || needsReview" /> Tôi xác nhận tiếp tục ca khám.</label>
        <div class="panel-actions">
          <button type="submit" class="btn-call-next" :disabled="resuming || loading || needsReview || !resumeConfirmed || (authStore.role === 'ADMIN' && !resumeDentistId)">{{ resuming ? 'Đang tiếp tục…' : 'Xác nhận tiếp tục' }}</button>
          <button type="button" :disabled="resuming" @click="closeResume">Quay lại</button>
        </div>
      </form>
    </section>

    <nav aria-label="Phân trang hàng đợi">
      <button :disabled="loading || writing || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || writing || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch, type ComponentPublicInstance } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { queueService, type QueueEntryResponse, type CallingDentist } from '@/services/queue';
import { WORKSPACE_UPDATED_EVENT, workspacesService } from '@/services/workspaces';

const router = useRouter();
const todayLabel = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  weekday: 'long',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
}).format(new Date());

const linkedEntry = ref<QueueEntryResponse | null>(null), linkedEntryLoading = ref(false), linkedEntryError = ref('');
const linkedEntryId = computed(() => {
  const raw = router.currentRoute?.value.query.queueEntryId;
  const id = typeof raw === 'string' && /^\d+$/.test(raw) ? Number(raw) : 0;
  return Number.isSafeInteger(id) && id > 0 && id <= 2147483647 ? id : null;
});
let linkedVersion = 0;

async function loadLinkedEntry() {
  const version = ++linkedVersion;
  const id = linkedEntryId.value;
  linkedEntry.value = null; linkedEntryError.value = ''; linkedEntryLoading.value = Boolean(id);
  if (!id) return;
  try {
    const entry = await queueService.getEntry(id);
    if (version !== linkedVersion) return;
    if (entry.queueEntryId !== id) throw new Error('Queue entry mismatch');
    linkedEntry.value = entry;
  } catch {
    if (version === linkedVersion) linkedEntryError.value = 'Không tải được lượt được chọn. Kiểm tra lại danh sách bên dưới.';
  } finally {
    if (version === linkedVersion) linkedEntryLoading.value = false;
  }
}
watch(linkedEntryId, loadLinkedEntry, { immediate: true });

const rows = ref<QueueEntryResponse[]>([]), status = ref(1), page = ref(1), total = ref(0), hasNext = ref(false);
const statusQuickFilters = [
  { value: 1, label: 'Đang chờ' },
  { value: 6, label: 'Đang mời' },
  { value: 2, label: 'Đang khám' },
  { value: 8, label: 'Tạm dừng' },
  { value: 5, label: 'Chụp ảnh' },
  { value: 3, label: 'Khám xong' },
  { value: 7, label: 'Vắng mặt' },
  { value: 0, label: 'Tất cả' }
] as const;

function selectStatus(value: number) {
  if (loading.value || writing.value) return;
  status.value = value;
  void load(1);
}
function locationLabel(item: { roomName?: string | null; chairNumber?: string | null }): string {
  return item.roomName || 'Chưa nhận phòng';
}

const loading = ref(false), callingNext = ref(false), error = ref(''), actionMsg = ref(''), updatedAt = ref('');
const callingOnTv = ref(false);
const authStore = useAuthStore();
const canCallNext = computed(() => ['ADMIN', 'DENTIST', 'ASSISTANT'].includes(authStore.role));
const canTransferQueue = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST'].includes(authStore.role));
const needsCallingDentist = computed(() => ['ADMIN', 'ASSISTANT'].includes(authStore.role));
const canViewBookingRequests = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const teamDentistId = ref<number | null>(null), showTeamOnly = ref(false);
const canCancelWaiting = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const cancelClinical = ref(false);
const cancelRow = ref<QueueEntryResponse | null>(null), cancelReason = ref(''), cancelConfirmed = ref(false), cancelling = ref(false);
const imagingRow = ref<QueueEntryResponse | null>(null), imagingReason = ref(''), imagingConfirmed = ref(false), routingImaging = ref(false);
const absenceRow = ref<QueueEntryResponse | null>(null), absenceReason = ref(''), absenceConfirmed = ref(false), changingAbsence = ref(false);
const pauseRow = ref<QueueEntryResponse | null>(null), pauseReason = ref(''), pauseConfirmed = ref(false), pausing = ref(false);
const resumeRow = ref<QueueEntryResponse | null>(null), resumeDentistId = ref(0), resumeReason = ref(''), resumeConfirmed = ref(false), resuming = ref(false);
const transferRow = ref<QueueEntryResponse | null>(null), transferDentistId = ref(0), transferReason = ref(''), transferConfirmed = ref(false), transferring = ref(false);
const writing = computed(() => callingNext.value || callingOnTv.value || cancelling.value || routingImaging.value || changingAbsence.value || pausing.value || resuming.value || transferring.value);
const dentists = ref<CallingDentist[]>([]), callDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref(''), needsReview = ref(false);
const transferTargets = computed(() => dentists.value.filter(dentist => dentist.dentistId !== transferRow.value?.dentistId));
let version = 0, life = 0, dentistVersion = 0;

function clinicalLocation(row: QueueEntryResponse) {
  return { name: 'management-clinical-diagnosis', query: { queueEntryId: row.queueEntryId, visitId: row.visitId } };
}
function canOpenClinical(row: QueueEntryResponse): boolean {
  if (!row.visitId || row.status === 4) return false;
  if (authStore.role === 'ADMIN') return true;
  if (authStore.role !== 'DENTIST') return false;
  if ([2, 6, 3].includes(row.status)) return row.dentistId === authStore.user?.userId;
  if (row.status === 1) return row.dentistId === null || row.dentistId === authStore.user?.userId;
  return false;
}
function clinicalLinkLabel(row: QueueEntryResponse): string {
  if ([2, 6].includes(row.status)) return 'Vào khám ngay →';
  if (row.status === 3) return 'Xem hồ sơ khám';
  return 'Mở hồ sơ khám';
}
async function openDentistVisit(row: QueueEntryResponse) {
  if (authStore.role !== 'DENTIST' || !row.visitId || row.dentistId !== authStore.user?.userId) return false;
  return !await router.push(clinicalLocation(row));
}
function assistantVitalsLocation(row: QueueEntryResponse) {
  return { name: 'management-assistant-vitals', query: { queueEntryId: row.queueEntryId, visitId: row.visitId } };
}
function canOpenAssistantVitals(row: QueueEntryResponse): boolean {
  return authStore.role === 'ASSISTANT' && Boolean(row.visitId) && [1, 2, 5, 6].includes(row.status);
}
async function openAssistantVitals(row: QueueEntryResponse) {
  if (authStore.role !== 'ASSISTANT' || !row.queueEntryId || !row.visitId) return false;
  return !await router.push(assistantVitalsLocation(row));
}
function patientLocation(row: QueueEntryResponse, tab: 'vitals' | 'images') {
  return { name: 'management-patient-detail', params: { id: row.patientId }, query: { tab, visitId: row.visitId } };
}
async function loadAssistantWorkspace() {
  if (authStore.role !== 'ASSISTANT' || !authStore.user) return;
  const ticket = life;
  const workspace = await workspacesService.get(authStore.user.userId).catch(() => null);
  if (ticket !== life || !workspace || workspace.userId !== authStore.user?.userId) return;
  const prior = teamDentistId.value;
  teamDentistId.value = workspace.workingDentistId;
  if (teamDentistId.value !== prior) {
    showTeamOnly.value = Boolean(teamDentistId.value);
    callDentistId.value = teamDentistId.value ?? 0;
  }
}
async function refreshAssistantWorkspace() {
  if (authStore.role !== 'ASSISTANT') return;
  const ticket = life;
  await loadAssistantWorkspace();
  if (ticket === life) await Promise.all([load(), loadDentists()]);
}
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
  if (cancelling.value || routingImaging.value || changingAbsence.value || pausing.value || resuming.value || transferring.value) return;
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000 || ![0, 1, 2, 3, 4, 5, 6, 7, 8].includes(status.value)) return;
  closeCancellation();
  closeImaging();
  closeAbsence();
  closePause();
  closeResume();
  closeTransfer();
  const ticket = ++version; loading.value = true; rows.value = []; error.value = ''; updatedAt.value = ''; total.value = 0; hasNext.value = false;
  try {
    const filter = status.value === 0 ? undefined : status.value;
    const dentistId = showTeamOnly.value ? teamDentistId.value : null;
    const result = dentistId ? await queueService.getToday(targetPage, filter, dentistId) : await queueService.getToday(targetPage, filter);
    if (ticket !== version) return;
    rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage;
    needsReview.value = false;
    updatedAt.value = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date());
  } catch { if (ticket === version) error.value = 'Không thể tải hàng đợi. Vui lòng tải lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function canCallOnTv(entry: QueueEntryResponse) {
  return authStore.role === 'DENTIST' && entry.status === 6 &&
    entry.dentistId === authStore.user?.userId && Boolean(entry.visitId);
}
async function handleCallOnTv(entry: QueueEntryResponse) {
  if (loading.value || writing.value || needsReview.value || !canCallOnTv(entry)) return;
  const ticket = life;
  callingOnTv.value = true;
  error.value = '';
  actionMsg.value = '';
  try {
    const result = await queueService.callOnTv(entry.queueEntryId);
    if (ticket !== life) return;
    if (result.queueEntryId !== entry.queueEntryId || !canCallOnTv(result)) throw new Error('Unexpected invitation');
    actionMsg.value = `Đã gửi lời mời số ${numberLabel(result.queueNumber)} tới TV phòng chờ.`;
  } catch (err: unknown) {
    if (ticket !== life) return;
    const failure = err as { response?: { data?: { message?: string } } };
    error.value = failure.response?.data?.message || 'Không thể gửi lời mời tới TV. Tải lại hàng đợi và thử lại.';
  } finally {
    if (ticket === life) callingOnTv.value = false;
  }
}

async function handleCallNext() {
  if (!canCallNext.value || writing.value || transferRow.value !== null || cancelRow.value !== null || imagingRow.value !== null || absenceRow.value !== null || loading.value || needsReview.value) return;
  if (needsCallingDentist.value && (dentistsLoading.value || !Number.isInteger(callDentistId.value) || !dentists.value.some(d => d.dentistId === callDentistId.value))) { dentistError.value = 'Chọn nha sĩ hoạt động đã tải để gọi ca.'; return; }
  const ticket = life;
  callingNext.value = true; error.value = ''; actionMsg.value = '';
  try {
    const entry = await queueService.callNext(needsCallingDentist.value ? { dentistId: callDentistId.value } : undefined);
    if (ticket !== life) return;
    if (entry.status !== 6) throw new Error('Unexpected invitation status');
    const room = entry.roomName ? ` vào ${entry.roomName}` : '';
    actionMsg.value = `Đã mời số ${numberLabel(entry.queueNumber)} - ${entry.patientName}${room}.`;
    if (await openDentistVisit(entry)) return;
    if (await openAssistantVitals(entry)) return;
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
  if (transferRow.value !== null || !(clinical ? canCancelClinical(row) : canCancelWaiting.value && row.status === 1 && row.canCancelConsultation !== true) || writing.value || imagingRow.value !== null || absenceRow.value !== null || loading.value || needsReview.value ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status && (!clinical || r.canCancelConsultation === true && r.dentistId === row.dentistId))) return;
  clearCancellation(); cancelClinical.value = clinical; cancelRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleCancellation() {
  const selected = cancelRow.value;
  if (!selected || !(cancelClinical.value ? canCancelClinical(selected) : canCancelWaiting.value && selected.status === 1 && selected.canCancelConsultation !== true) || writing.value || loading.value || needsReview.value || !cancelConfirmed.value) return;
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
  if (transferRow.value !== null || !canRouteImaging(row) || writing.value || loading.value || cancelRow.value !== null || absenceRow.value !== null || needsReview.value ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status)) return;
  clearImaging(); imagingRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleImaging() {
  const selected = imagingRow.value;
  if (!selected || !canRouteImaging(selected) || writing.value || loading.value || cancelRow.value !== null || absenceRow.value !== null || needsReview.value || !imagingConfirmed.value) return;
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
  if (!canPause(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || pauseRow.value || resumeRow.value || needsReview.value) return;
  clearPause(); pauseRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handlePause() {
  const selected = pauseRow.value;
  if (!selected || !canPause(selected) || writing.value || loading.value || needsReview.value || !pauseConfirmed.value) return;
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
  if (!canResume(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || pauseRow.value || resumeRow.value || needsReview.value) return;
  clearResume(); resumeRow.value = { ...row }; error.value = ''; actionMsg.value = '';
  if (authStore.role === 'ADMIN' && !dentists.value.length) void loadDentists();
}
async function handleResume() {
  const selected = resumeRow.value;
  if (!selected || !canResume(selected) || writing.value || loading.value || needsReview.value || !resumeConfirmed.value) return;
  if (authStore.role === 'ADMIN' && !resumeDentistId.value) { error.value = 'Vui lòng chọn nha sĩ tiếp nhận ca.'; return; }
  if (resumeReason.value.length > 500) { error.value = 'Ghi chú tối đa 500 ký tự.'; return; }
  const ticket = life; resuming.value = true; error.value = ''; actionMsg.value = '';
  try {
    const targetDentist = authStore.role === 'DENTIST' ? authStore.user?.userId || null : resumeDentistId.value || null;
    const result = await queueService.resumeVisit(selected.queueEntryId, targetDentist, resumeReason.value.trim() || null);
    if (ticket !== life) return;
    actionMsg.value = `Đã tiếp tục ca ${numberLabel(result.queueNumber)} - ${result.patientName} tại ${locationLabel(result)}.`;
    clearResume(); needsReview.value = true; resuming.value = false;
    if (!await openDentistVisit(result)) await load(page.value);
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
  if (!canTransfer(row) || writing.value || loading.value || cancelRow.value || imagingRow.value || absenceRow.value || needsReview.value ||
      !rows.value.some(item => item.queueEntryId === row.queueEntryId && item.status === row.status && item.dentistId === row.dentistId)) return;
  clearTransfer(); transferRow.value = { ...row }; error.value = ''; actionMsg.value = '';
  if (!dentists.value.length) void loadDentists();
}
async function handleTransfer() {
  const selected = transferRow.value, targetId = transferDentistId.value;
  if (!selected || !canTransfer(selected) || writing.value || loading.value || dentistsLoading.value || needsReview.value || !transferConfirmed.value ||
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
  if (transferRow.value !== null || !canChangeAbsence(row) || writing.value || loading.value || needsReview.value || cancelRow.value !== null || imagingRow.value !== null ||
      !Number.isInteger(row.queueEntryId) || row.queueEntryId <= 0 || !rows.value.some(r => r.queueEntryId === row.queueEntryId && r.status === row.status && (row.status === 7 || r.canMarkAbsent))) return;
  clearAbsence(); absenceRow.value = { ...row }; error.value = ''; actionMsg.value = '';
}
async function handleAbsence() {
  const selected = absenceRow.value;
  if (!selected || !canChangeAbsence(selected) || writing.value || loading.value || needsReview.value || !absenceConfirmed.value || cancelRow.value !== null || imagingRow.value !== null) return;
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
function numberLabel(value: number) { return `A-${String(value).padStart(3, '0')}`; }

onMounted(async () => {
  const ticket = life;
  if (authStore.role === 'ASSISTANT') globalThis.window?.addEventListener(WORKSPACE_UPDATED_EVENT, refreshAssistantWorkspace);
  await loadAssistantWorkspace();
  if (ticket === life) await Promise.all([load(), loadDentists()]);
});
onUnmounted(() => {
  globalThis.window?.removeEventListener(WORKSPACE_UPDATED_EVENT, refreshAssistantWorkspace);
  life++; version++; dentistVersion++; linkedVersion++;
  clearCancellation(); clearImaging(); clearAbsence(); clearTransfer();
  rows.value = []; dentists.value = [];
  callDentistId.value = 0; dentistError.value = ''; actionMsg.value = ''; error.value = '';
  needsReview.value = false; updatedAt.value = ''; total.value = 0;
});
</script>
<style scoped>
.page {
  --ink: #172b42;
  --muted: #5a6d7e;
  --line: #dbe4ed;
  --teal: #0f746f;
  --teal-dark: #0a5c59;
  --surface: #ffffff;
  max-width: 1380px;
  margin: 0 auto 2.5rem;
  padding: 1.5rem clamp(1rem, 2vw, 2rem) 2.5rem;
  box-sizing: border-box;
  color: var(--ink);
  display: grid;
  gap: 1rem;
}

.hero-banner {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 1.25rem;
  padding: clamp(1.25rem, 2.5vw, 1.85rem);
  border-radius: 18px;
  background: linear-gradient(135deg, #0b4f57 0%, #0f746f 58%, #168f84 100%);
  color: #ffffff;
  box-shadow: 0 12px 28px rgb(11 79 87 / 18%);
}

.hero-copy {
  min-width: 0;
  flex: 1 1 320px;
}

.hero-kicker-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.55rem;
  margin-bottom: 0.45rem;
}

.eyebrow {
  margin: 0;
  color: #b7efe6;
  font-size: 0.74rem;
  letter-spacing: 0.13em;
  font-weight: 800;
}

.date-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: rgb(255 255 255 / 16%);
  border: 1px solid rgb(255 255 255 / 26%);
  color: #ffffff;
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: capitalize;
}

.hero-banner h1 {
  margin: 0;
  font-size: clamp(1.6rem, 2.6vw, 2.15rem);
  line-height: 1.15;
  letter-spacing: -0.03em;
  color: #ffffff;
}

.intro {
  margin: 0.5rem 0 0;
  color: #e4f6f3;
  font-size: 0.94rem;
  line-height: 1.5;
  max-width: 68ch;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.65rem;
  flex: 0 0 auto;
}

.hero-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  min-height: 44px;
  padding: 0.65rem 1.05rem;
  border-radius: 11px;
  font-weight: 700;
  font-size: 0.88rem;
  text-decoration: none;
  transition: transform 0.15s ease, background 0.15s ease, border-color 0.15s ease;
}

.hero-btn-solid {
  background: #ffffff;
  color: #0b4f57;
  border: 1px solid #ffffff;
}

.hero-btn-solid:hover {
  background: #effcf9;
  transform: translateY(-1px);
}

.hero-btn-ghost {
  background: rgb(255 255 255 / 14%);
  color: #ffffff;
  border: 1px solid rgb(255 255 255 / 34%);
}

.hero-btn-ghost:hover {
  background: rgb(255 255 255 / 24%);
  transform: translateY(-1px);
}

.auto-rollover-banner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem 1rem;
  padding: 0.9rem 1.15rem;
  border-radius: 14px;
  border: 1px solid #bde0dc;
  background: linear-gradient(180deg, #f3fbfa 0%, #eaf7f5 100%);
  color: #164947;
}

.rollover-icon {
  font-size: 1.25rem;
  flex: 0 0 auto;
}

.rollover-copy {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
  flex: 1 1 320px;
  font-size: 0.86rem;
  line-height: 1.48;
}

.rollover-copy strong {
  color: #0d4b47;
  font-size: 0.92rem;
}

.rollover-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 38px;
  padding: 0.45rem 0.85rem;
  border-radius: 9px;
  border: 1px solid #8ac4bd;
  background: #ffffff;
  color: #0d5c57;
  font-size: 0.82rem;
  font-weight: 700;
  text-decoration: none;
  white-space: nowrap;
}

.rollover-link:hover {
  background: #dff3f0;
}

.queue-link-panel {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1rem;
  padding: 0.85rem 1rem;
  border: 1px solid #cbd5e1;
  border-radius: 12px;
  background: #ffffff;
  overflow-wrap: anywhere;
}

.control-dock {
  display: grid;
  gap: 0.85rem;
  padding: 1.05rem 1.15rem;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--surface);
  box-shadow: 0 6px 20px rgb(23 43 66 / 5%);
}

.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.75rem;
}

.field-group {
  display: grid;
  gap: 0.35rem;
  min-width: 185px;
}

.field-group label {
  font-size: 0.78rem;
  font-weight: 750;
  color: #496175;
}

.field-dentist {
  flex: 1 1 260px;
}

button,
select,
textarea,
input[type="text"] {
  max-width: 100%;
  min-height: 44px;
  box-sizing: border-box;
  font: inherit;
  border: 1px solid #c9d6e1;
  border-radius: 10px;
  background: #ffffff;
  color: #20384e;
}

select {
  width: 100%;
  padding: 0.6rem 0.75rem;
}

button {
  padding: 0.62rem 0.95rem;
  cursor: pointer;
  font-weight: 700;
  transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}

button:hover:not(:disabled) {
  background: #f1f7f8;
  border-color: #93b7b5;
}

button:disabled {
  opacity: 0.56;
  cursor: not-allowed;
}

button:focus-visible,
select:focus-visible,
textarea:focus-visible,
a:focus-visible,
input:focus-visible {
  outline: 3px solid #64b5ad;
  outline-offset: 2px;
}

.btn-call-next {
  border-color: var(--teal);
  background: var(--teal);
  color: #ffffff;
}

.btn-call-next:hover:not(:disabled) {
  border-color: var(--teal-dark);
  background: var(--teal-dark);
  color: #ffffff;
}

.status-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
  padding-top: 0.2rem;
  border-top: 1px solid #edf2f7;
}

.status-chip {
  min-height: 36px;
  padding: 0.35rem 0.85rem;
  border: 1px solid #d4dfe7;
  border-radius: 999px;
  background: #ffffff;
  color: #3d566b;
  font-size: 0.82rem;
  font-weight: 700;
  cursor: pointer;
}

.status-chip:hover:not(:disabled) {
  border-color: #87bdb9;
  background: #f4fafb;
}

.status-chip.active {
  border-color: var(--teal);
  background: #e3f4f2;
  color: var(--teal-dark);
}

.button-secondary {
  background: #f8fafc;
}

.dock-hints {
  display: grid;
  gap: 0.25rem;
}

.workflow-hint {
  margin: 0;
  color: #566b7d;
  font-size: 0.85rem;
  line-height: 1.48;
}

.notice {
  margin: 0;
  padding: 0.85rem 1rem;
  border: 1px solid;
  border-radius: 12px;
  font-weight: 600;
  line-height: 1.5;
  overflow-wrap: anywhere;
}

.notice-loading {
  color: #355b72;
  background: #f2f8fb;
  border-color: #cfe1eb;
}

.notice-success {
  color: #155e50;
  background: #eef9f4;
  border-color: #b8dfd1;
}

.notice-warning {
  color: #7b5214;
  background: #fff8e8;
  border-color: #ecd59c;
}

.notice-error {
  color: #9b3434;
  background: #fff4f3;
  border-color: #efc4c0;
}

.queue-summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.55rem;
  padding: 0.2rem 0.15rem;
  color: var(--muted);
  font-size: 0.88rem;
}

.summary-stat {
  display: inline-flex;
  align-items: baseline;
  gap: 0.4rem;
}

.summary-label {
  color: var(--muted);
}

.queue-summary strong {
  color: #12314a;
  font-size: 1rem;
}

.entries {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.85rem;
}

.queue-entry {
  min-width: 0;
  padding: 1.1rem 1.2rem;
  border: 1px solid var(--line);
  background: #ffffff;
  border-radius: 16px;
  box-shadow: 0 4px 14px rgb(23 43 66 / 4%);
  box-sizing: border-box;
  overflow-wrap: anywhere;
}

.queue-entry.priority {
  border-left: 4px solid #c89435;
}

.queue-entry.entry-status-2,
.queue-entry.entry-status-6 {
  border-color: #bfdbfe;
  box-shadow: 0 6px 18px rgb(37 99 235 / 8%);
}

.queue-entry-header {
  display: grid;
  grid-template-columns: 68px minmax(0, 1fr) auto;
  align-items: center;
  gap: 0.9rem;
}

.queue-ticket {
  display: grid;
  place-items: center;
  min-height: 48px;
  border-radius: 12px;
  color: #075f5e;
  background: #e6f5f3;
  font-size: 0.98rem;
  font-weight: 850;
  letter-spacing: 0.02em;
}

.ticket-status-2,
.ticket-status-6 {
  background: #dbeafe;
  color: #1e40af;
}

.ticket-status-8,
.ticket-status-7 {
  background: #ffedd5;
  color: #9a3412;
}

.ticket-status-5 {
  background: #ede9fe;
  color: #5b21b6;
}

.queue-patient {
  display: grid;
  min-width: 0;
  gap: 0.28rem;
}

.queue-patient-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
}

.queue-patient strong {
  color: #152e47;
  font-size: 1.05rem;
  line-height: 1.3;
}

.patient-sub {
  color: var(--muted);
  font-size: 0.82rem;
}

.child-pill,
.status-pill,
.priority-pill,
.queue-type {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: fit-content;
  border-radius: 999px;
  font-weight: 750;
  line-height: 1.2;
}

.child-pill {
  padding: 0.16rem 0.52rem;
  font-size: 0.73rem;
  background: #dbeafe;
  color: #1e40af;
}

.status-pill {
  min-height: 30px;
  padding: 0.3rem 0.75rem;
  font-size: 0.78rem;
  background: #f1f5f8;
  color: #53687b;
}

.status-1 {
  background: #fff4da;
  color: #87590d;
}

.status-2,
.status-6 {
  background: #eaf3ff;
  color: #1d4ed8;
}

.status-5 {
  background: #f3edff;
  color: #684c9b;
}

.status-7,
.status-8 {
  background: #fff0e8;
  color: #99502a;
}

.status-3 {
  background: #e9f6ee;
  color: #276846;
}

.status-4 {
  background: #f1f2f4;
  color: #697583;
}

.priority-pill {
  padding: 0.16rem 0.55rem;
  font-size: 0.73rem;
  color: #785313;
  background: #fff5df;
  border: 1px solid #efdfba;
}

.queue-type {
  padding: 0.16rem 0.55rem;
  font-size: 0.73rem;
  color: #526779;
  background: #f1f5f8;
}

.queue-entry-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 0.9rem;
  margin: 0.8rem 0 0 5.15rem;
}

.meta-chip {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  padding: 0.3rem 0.65rem;
  border-radius: 8px;
  background: #f6f9fb;
  border: 1px solid #e5edf4;
  color: #4c6377;
  font-size: 0.83rem;
}

.meta-chip b {
  color: #233c54;
}

.care-note {
  display: grid;
  gap: 0.22rem;
  margin: 0.75rem 0 0 5.15rem;
  padding: 0.65rem 0.85rem;
  border: 1px solid #efe1bc;
  border-radius: 10px;
  color: #675629;
  background: #fffaf0;
  font-size: 0.84rem;
  line-height: 1.45;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.care-note strong {
  color: #745419;
  font-size: 0.77rem;
}

.queue-card-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin: 0.9rem 0 0 5.15rem;
}

.queue-card-actions button,
.queue-card-actions a {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 0.85rem;
  border: 1px solid #d4dfe7;
  border-radius: 9px;
  color: #35526a;
  background: #ffffff;
  text-decoration: none;
  font-size: 0.82rem;
  font-weight: 700;
  line-height: 1.25;
  text-align: center;
}

.queue-card-actions a {
  color: #ffffff;
  border-color: #255c81;
  background: #255c81;
}

.queue-card-actions a:hover {
  background: #194766;
}

.queue-card-actions a.btn-open-clinical {
  color: #ffffff;
  border-color: var(--teal);
  background: var(--teal);
}

.queue-card-actions a.btn-open-clinical:hover {
  background: var(--teal-dark);
  border-color: var(--teal-dark);
}

.queue-card-actions a.btn-open-clinical-secondary {
  color: #1e5273;
  border-color: #b9d0e6;
  background: #f4faff;
}

.queue-card-actions a.btn-open-clinical-secondary:hover {
  background: #e1f0fc;
  border-color: #89b7df;
}

.queue-card-actions .btn-recall-tv {
  color: #72520f;
  border-color: #ead69a;
  background: #fff7dc;
}

.action-danger {
  color: #a13e3b;
  border-color: #e7c2bf;
  background: #fff8f7;
}

.action-danger:hover:not(:disabled) {
  background: #feeceb;
  border-color: #dc9d98;
}

.empty-state {
  display: grid;
  justify-items: center;
  gap: 0.45rem;
  padding: 2.25rem 1rem;
  border: 1px dashed #c7d6df;
  border-radius: 16px;
  color: #5f7384;
  background: #ffffff;
  text-align: center;
}

.empty-state strong {
  color: #24445b;
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  margin-bottom: 0.25rem;
  border-radius: 50%;
  color: #27674f;
  background: #e9f6ee;
  font-weight: 850;
}

.cancel-panel,
.imaging-panel,
.absence-panel {
  padding: 1.25rem;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: #ffffff;
  box-shadow: 0 4px 15px rgb(29 56 80 / 5%);
  box-sizing: border-box;
}

.cancel-panel {
  border-color: #efc8c4;
  background: #fffafa;
}

.imaging-panel {
  border-color: #c9dfe6;
  background: #f8fcfd;
}

.absence-panel {
  border-color: #ecd9a6;
  background: #fffdf7;
}

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
  border-radius: 16px;
  background: #ffffff;
  color: var(--ink);
  box-shadow: 0 18px 52px rgb(29 56 80 / 22%);
}

.transfer-panel::backdrop {
  background: rgb(17 36 56 / 45%);
}

.transfer-panel p {
  overflow-wrap: anywhere;
}

.transfer-panel .transfer-notice {
  margin: 0;
}

.panel-actions,
.transfer-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.5rem;
}

.cancel-panel h2,
.imaging-panel h2,
.absence-panel h2,
.transfer-panel h2 {
  margin: 0 0 0.6rem;
  font-size: 1.12rem;
  color: #1b354b;
}

.cancel-panel form,
.imaging-panel form,
.absence-panel form,
.transfer-panel form {
  display: grid;
  gap: 0.75rem;
}

textarea {
  width: 100%;
  min-height: 5.5rem;
  padding: 0.7rem 0.8rem;
  resize: vertical;
}

form label {
  color: #3d566b;
  font-size: 0.88rem;
  line-height: 1.45;
}

form label input[type="checkbox"] {
  width: 17px;
  height: 17px;
  min-height: 0;
  margin: 0 0.45rem 0 0;
  vertical-align: middle;
  accent-color: var(--teal);
}

nav[aria-label="Phân trang hàng đợi"] {
  display: flex;
  justify-content: flex-end;
  gap: 0.65rem;
}

[role="alert"] {
  overflow-wrap: anywhere;
}

@media (max-width: 760px) {
  .page {
    padding: 1rem 0.8rem 2rem;
  }
  .hero-actions {
    width: 100%;
  }
  .hero-btn {
    flex: 1 1 180px;
  }
  .toolbar {
    align-items: stretch;
  }
  .field-group,
  .toolbar > button {
    min-width: 0;
    width: 100%;
  }
  .queue-entry-header {
    grid-template-columns: 56px minmax(0, 1fr);
    gap: 0.65rem;
  }
  .status-pill {
    grid-column: 2;
  }
  .queue-entry-meta,
  .care-note,
  .queue-card-actions {
    margin-left: 0;
  }
  .queue-card-actions > * {
    flex: 1 1 160px;
  }
}

@media (max-width: 420px) {
  .queue-summary {
    flex-wrap: wrap;
  }
  .queue-entry {
    padding: 0.9rem;
  }
  .queue-card-actions {
    display: grid;
    grid-template-columns: 1fr;
  }
  .queue-card-actions > * {
    width: 100%;
    min-width: 0;
  }
}
</style>
