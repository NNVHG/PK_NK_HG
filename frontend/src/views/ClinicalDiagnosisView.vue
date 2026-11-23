<template>
  <main class="clinical-page">
    <header class="clinical-heading">
      <h1>Khám bệnh &amp; chẩn đoán lâm sàng</h1>
      <p>Chọn lượt khám để kiểm tra cảnh báo an toàn và ghi chẩn đoán.</p>
    </header>

    <div class="clinical-layout">
      <section class="panel queue-panel" aria-label="Lượt khám hôm nay">
        <div class="panel-header">
          <h2>Lượt khám hôm nay</h2>
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty" @click="loadQueue(page)">Làm mới</button>
        </div>
        <div v-if="loadingQueue" class="loading-state-sm" role="status">Đang tải lượt khám…</div>
        <div v-if="queueError" class="alert-box alert-error" role="alert">{{ queueError }}</div>
        <p v-if="!loadingQueue && !queueError && !entries.length" class="empty-text">Chưa có lượt khám được tiếp đón hôm nay.</p>

        <div class="queue-list">
          <button
            v-for="entry in entries"
            :key="entry.queueEntryId"
            type="button"
            class="queue-item-card"
            :class="{ selected: selected?.queueEntryId === entry.queueEntryId }"
            :aria-pressed="selected?.queueEntryId === entry.queueEntryId"
            :disabled="busy || dirty || !entry.visitId || Boolean(queueError)"
            @click="selectEntry(entry)"
          >
            <div class="item-top">
              <span class="queue-num">STT #{{ entry.queueNumber }}</span>
              <span class="queue-status-tag" :data-status="entry.status">{{ entry.statusText }}</span>
            </div>
            <div class="patient-name">{{ entry.patientName }}</div>
            <div class="patient-meta">{{ entry.patientCode }}<template v-if="entry.dentistName"> · {{ entry.dentistName }}</template></div>
          </button>
        </div>

        <nav class="pagination-bar" aria-label="Phân trang lượt khám">
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty || page <= 1" @click="loadQueue(page - 1)">← Trước</button>
          <span class="page-info">Trang <strong>{{ page }}</strong> / {{ Math.max(totalPages, 1) }}</span>
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty || page >= totalPages" @click="loadQueue(page + 1)">Sau →</button>
        </nav>
      </section>

      <section class="panel detail-panel" aria-label="Hồ sơ khám lâm sàng">
        <div v-if="!selected" class="empty-selection-placeholder">
          <h2>Hồ sơ lâm sàng</h2>
          <p v-if="actionError" class="alert-box alert-error" role="alert">{{ actionError }}</p>
          <p>Chọn một bệnh nhân trong danh sách lượt khám hôm nay.</p>
        </div>

        <template v-else>
          <header class="patient-header">
            <div>
              <h2 ref="patientHeading" tabindex="-1">{{ selected.patientName }}</h2>
              <div class="patient-submeta">
                <span>{{ selected.patientCode }}</span>
                <span>Lượt số {{ selected.queueNumber }}</span>
                <span>Lần khám #{{ selected.visitId }}</span>
                <span v-if="selected.roomName && visit?.status !== 'Correcting'">{{ selected.roomName }}</span>
              </div>
            </div>
            <span class="queue-status-tag" :data-status="selected.status">
              {{ visitStatusLabel || selected.statusText }}
            </span>
          </header>

          <div v-if="loadingVisit" class="loading-state-sm" role="status">Đang tải chi tiết lần khám…</div>
          <div v-if="visitError" class="alert-box alert-error" role="alert">
            {{ visitError }}
            <button type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">Thử tải lại</button>
          </div>

          <SafetyAlertBanner v-if="selected.patientId" :patient-id="selected.patientId" :refresh-key="visit?.visitId ?? selected.queueEntryId" />
          <div v-if="visit && [1, 6].includes(selected.status)" class="pre-exam-vitals">
            <strong>Sinh hiệu lần khám</strong>
            <span v-if="vitalSignsError" role="status">{{ vitalSignsError }}</span>
            <span v-else-if="vitals">
              Huyết áp {{ vitals.systolicBp ?? '—' }}/{{ vitals.diastolicBp ?? '—' }} mmHg ·
              Mạch {{ vitals.pulseBpm ?? '—' }} lần/phút · Nhiệt độ {{ vitals.temperatureC ?? '—' }} °C
            </span>
            <span v-else>Chưa có số đo ở lần khám này.</span>
          </div>

          <template v-if="visit">
            <div v-if="visit.status === 'Correcting'" class="alert-box alert-warning">
              ⚠️ Đang ở chế độ đính chính hồ sơ sau ca khám.
              <RouterLink :to="`/management/clinical/visits/${visit.visitId}/invoice-draft`" class="link-inline">
                Hoàn tất và chốt hồ sơ →
              </RouterLink>
            </div>

            <div v-if="auth.role === 'ADMIN' && [1, 6].includes(selected.status) && !visit.isLocked" class="admin-assign-box">
              <label for="start-dentist">Chỉ định nha sĩ thực hiện ca:</label>
              <div class="assign-controls">
                <select id="start-dentist" v-model.number="startDentistId" :disabled="busy || dentistsLoading">
                  <option :value="0">-- Chọn nha sĩ phụ trách --</option>
                  <option v-for="dentist in dentists" :key="dentist.dentistId" :value="dentist.dentistId">
                    {{ dentist.dentistName }} · #{{ dentist.dentistId }}
                  </option>
                </select>
                <button type="button" class="btn btn-outline-sm" :disabled="busy || dentistsLoading" @click="loadDentists">Tải lại nha sĩ</button>
              </div>
              <p v-if="dentistError" class="alert-box alert-error" role="alert">⚠️ {{ dentistError }}</p>
            </div>

            <div v-if="needsReview" class="alert-box alert-warning" role="alert">
              Tải lại hồ sơ để đối chiếu trạng thái trước khi tiếp tục.
            </div>

            <div class="visit-actions">
              <button v-if="canStart" type="button" class="btn btn-primary" :disabled="busy" @click="startConsultation">
                {{ starting ? 'Đang bắt đầu…' : 'Bắt đầu khám' }}
              </button>
              <button
                type="button"
                v-if="selected.status === 6"
                class="btn btn-outline-sm"
                :disabled="busy || tvCalling"
                @click="triggerTvRecall"
              >
                {{ tvCalling ? `Đang gọi trên TV (${tvCallRemainingSeconds}s)…` : 'Gọi lại trên TV' }}
              </button>
              <button type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">
                {{ dirty ? 'Hủy sửa và tải lại' : 'Tải lại hồ sơ' }}
              </button>
              <span v-if="tvCalledMsg" class="tv-called-status" role="status">{{ tvCalledMsg }}</span>
            </div>

            <form v-if="canEdit" class="diagnosis-form" @submit.prevent="saveDiagnosis">
              <h3>Ghi chẩn đoán</h3>
              <div class="form-group">
                <label for="diagnosis">Chẩn đoán bệnh lý chính <span class="required">*</span></label>
                <DiagnosisAutocomplete :key="visit.visitId" v-model="diagnosis" :disabled="busy" />
                <div class="char-count">{{ diagnosis.length }}/1000 ký tự</div>
              </div>

              <div class="form-group">
                <label for="clinical-notes">Ghi chú diễn tiến lâm sàng & dặn dò</label>
                <textarea id="clinical-notes" v-model="clinicalNotes" :disabled="busy" maxlength="2000" rows="5" placeholder="Ghi chú lâm sàng, triệu chứng cụ thể, dặn dò chăm sóc sau thủ thuật..."></textarea>
                <div class="char-count">{{ clinicalNotes.length }}/2000 ký tự</div>
              </div>

              <div v-if="dirty" class="alert-box alert-warning">
                Lưu chẩn đoán hoặc hủy sửa trước khi chuyển lượt khám.
              </div>

              <button type="submit" class="btn btn-primary btn-save" :disabled="busy">
                {{ saving ? 'Đang lưu…' : 'Lưu chẩn đoán' }}
              </button>
            </form>

            <div v-else class="read-only-diagnosis">
              <h3>Chẩn đoán và ghi chú</h3>
              <p class="hint-box">{{ visit.status === 'Created' ? 'Chẩn đoán được ghi sau khi bắt đầu khám.' : 'Hồ sơ hiện chỉ đọc.' }}</p>
              <div class="record-group">
                <span class="record-label">Chẩn đoán:</span>
                <p class="record-val">{{ visit.diagnosis || 'Chưa ghi nhận chẩn đoán' }}</p>
              </div>
              <div class="record-group">
                <span class="record-label">Ghi chú lâm sàng:</span>
                <p class="record-val">{{ visit.clinicalNotes || 'Chưa có ghi chú' }}</p>
              </div>
            </div>
            <nav v-if="!dirty && !busy" class="care-links" aria-label="Nghiệp vụ cùng lần khám">
              <RouterLink :to="`/management/clinical/visits/${visit.visitId}/fdi`">Sơ đồ răng</RouterLink>
              <RouterLink :to="`/management/clinical/visits/${visit.visitId}/prescription`">Đơn thuốc</RouterLink>
              <RouterLink :to="`/management/clinical/visits/${visit.visitId}/images`">Ảnh khám</RouterLink>
              <RouterLink :to="`/management/clinical/visits/${visit.visitId}/invoice-draft`">Hóa đơn nháp</RouterLink>
            </nav>
          </template>

          <div v-if="actionError" class="alert-box alert-error" role="alert">{{ actionError }}</div>
          <div v-if="successMessage" class="alert-box alert-success" role="status">{{ successMessage }}</div>
        </template>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { onBeforeRouteUpdate, useRoute } from 'vue-router';
import axios from 'axios';
import DiagnosisAutocomplete from '@/components/DiagnosisAutocomplete.vue';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';

import { useAuthStore } from '@/stores/auth';
import { queueService, maskPublicQueueName, type QueueEntryResponse } from '@/services/queue';
import { visitsService } from '@/services/visits';
import { appointmentsService, type BookingDentist } from '@/services/appointments';
import { patientsService, type VisitDetails, type VitalSignRecord } from '@/services/patients';

const auth = useAuthStore();
const route = useRoute();
const entries = ref<QueueEntryResponse[]>([]);
const selected = ref<QueueEntryResponse | null>(null);
const visit = ref<VisitDetails | null>(null);
const vitals = ref<VitalSignRecord | null>(null);
const vitalSignsError = ref('');
const patientHeading = ref<HTMLHeadingElement | null>(null);
const diagnosis = ref('');
const clinicalNotes = ref('');
const page = ref(1);
const totalPages = ref(1);
const loadingQueue = ref(false);
const loadingVisit = ref(false);
const TV_CALL_DURATION_SECONDS = 15;
const starting = ref(false);
const saving = ref(false);
const tvCalling = ref(false);
const tvCallRemainingSeconds = ref(0);
const tvCalledMsg = ref('');
const queueError = ref('');
const visitError = ref('');
const actionError = ref('');
const successMessage = ref('');
const needsReview = ref(false), dentists = ref<BookingDentist[]>([]), startDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref('');
let life = 0, queueVersion = 0, visitVersion = 0, dentistVersion = 0, preferredVersion = 0;
let tvCallTimer: ReturnType<typeof setInterval> | null = null;
const targetDentistId = computed(() => auth.role === 'DENTIST' ? auth.user?.userId : startDentistId.value);
const busy = computed(() => loadingQueue.value || loadingVisit.value || starting.value || saving.value);
const canEdit = computed(() => !needsReview.value && !visit.value?.isLocked && ['InProgress', 'Correcting'].includes(visit.value?.status ?? '')
  && (auth.role === 'ADMIN' || (auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId)));
const canStart = computed(() => !needsReview.value && !visit.value?.isLocked && ['Created', 'InProgress'].includes(visit.value?.status ?? '')
  && selected.value !== null && [1, 6].includes(selected.value.status) && ['ADMIN', 'DENTIST'].includes(auth.role)
  && Number.isInteger(targetDentistId.value) && Number(targetDentistId.value) > 0
  && (selected.value.dentistId === null || selected.value.dentistId === targetDentistId.value)
  && (visit.value?.dentistId === null || visit.value?.dentistId === targetDentistId.value)
  && (auth.role !== 'ADMIN' || (!dentistsLoading.value && dentists.value.some(d => d.dentistId === targetDentistId.value))));
const dirty = computed(() => canEdit.value && Boolean(visit.value)
  && (diagnosis.value !== (visit.value?.diagnosis ?? '') || clinicalNotes.value !== (visit.value?.clinicalNotes ?? '')));
const visitStatusLabel = computed(() => ({ Created: 'Chưa bắt đầu', InProgress: 'Đang khám', Completed: 'Đã kết thúc', Cancelled: 'Đã hủy', Correcting: 'Đang đính chính' }[visit.value?.status ?? ''] ?? visit.value?.status));

function errorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.errors?.[0]?.message ?? error.response?.data?.message ?? fallback;
  }
  return fallback;
}

async function loadDentists() {
  if (auth.role !== 'ADMIN' || starting.value || saving.value) return;
  const ticket = ++dentistVersion; dentistsLoading.value = true; dentists.value = []; dentistError.value = '';
  try {
    const result = await appointmentsService.getBookingDentists();
    if (ticket !== dentistVersion) return;
    dentists.value = result; if (!result.some(d => d.dentistId === startDentistId.value)) startDentistId.value = 0;
  } catch { if (ticket === dentistVersion) { startDentistId.value = 0; dentistError.value = 'Không tải được nha sĩ hoạt động. Tải lại trước khi bắt đầu khám.'; } }
  finally { if (ticket === dentistVersion) dentistsLoading.value = false; }
}
function clearTvCallTimer() {
  if (tvCallTimer !== null) {
    clearInterval(tvCallTimer);
    tvCallTimer = null;
  }
  tvCalling.value = false;
  tvCallRemainingSeconds.value = 0;
  tvCalledMsg.value = '';
}

async function loadQueue(targetPage = 1) {
  if (busy.value || dirty.value || !Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000) return;
  clearTvCallTimer();
  const ticket = ++queueVersion; entries.value = []; selected.value = null; visit.value = null; diagnosis.value = ''; clinicalNotes.value = ''; startDentistId.value = 0;
  loadingQueue.value = true;
  queueError.value = '';
  try {
    const result = await queueService.getToday(targetPage);
    if (ticket !== queueVersion) return;
    entries.value = result.items;
    page.value = result.page;
    totalPages.value = result.totalPages;
    selected.value = null;
    visit.value = null;
    actionError.value = '';
    successMessage.value = '';
  } catch (error) {
    if (ticket !== queueVersion) return;
    queueError.value = errorMessage(error, 'Không tải được lượt khám. Vui lòng thử lại.');
  } finally { if (ticket === queueVersion) loadingQueue.value = false; }
}

function queryId(value: unknown): number | null {
  if (typeof value !== 'string' || !/^\d+$/.test(value)) return null;
  const id = Number(value);
  return Number.isSafeInteger(id) && id > 0 && id <= 2147483647 ? id : null;
}

async function openInitialVisit() {
  const ticket = ++preferredVersion;
  const entryId = queryId(route.query.queueEntryId);
  const visitId = queryId(route.query.visitId);
  try {
    let entry: QueueEntryResponse | undefined;
    if (route.query.queueEntryId !== undefined || route.query.visitId !== undefined) {
      if (!entryId || !visitId) throw new Error('Invalid visit link');
      const linked = await queueService.getEntry(entryId);
      const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short' }).format(new Date());
      if (linked.visitId !== visitId || linked.queueDate !== today ||
        (auth.role === 'DENTIST' && linked.dentistId !== auth.user?.userId)) throw new Error('Invalid visit link');
      entry = linked;
    } else if (auth.role === 'DENTIST' && auth.user) {
      const [calling, inProgress] = await Promise.all([
        queueService.getToday(1, 6, auth.user.userId),
        queueService.getToday(1, 2, auth.user.userId)
      ]);
      entry = calling.totalCount === 1 ? calling.items[0] : inProgress.totalCount === 1 ? inProgress.items[0] : undefined;
    }
    if (ticket !== preferredVersion || !entry?.visitId || busy.value || dirty.value) return;
    if (!entries.value.some(row => row.queueEntryId === entry.queueEntryId)) entries.value = [entry, ...entries.value];
    await selectEntry(entry);
  } catch {
    if (ticket === preferredVersion && (route.query.queueEntryId !== undefined || route.query.visitId !== undefined))
      actionError.value = 'Không mở được lượt được liên kết. Chọn lượt khám trong danh sách.';
  }
}

async function selectEntry(entry: QueueEntryResponse) {
  if (busy.value || dirty.value) return;
  preferredVersion++;
  clearTvCallTimer();
  selected.value = entry; startDentistId.value = entry.dentistId ?? 0;
  needsReview.value = false;
  const request = reloadVisit();
  void nextTick(() => patientHeading.value?.focus());
  await request;
}

async function reloadVisit() {
  if (!selected.value?.visitId) return;
  const ticket = ++visitVersion;
  loadingVisit.value = true;
  visitError.value = '';
  actionError.value = '';
  successMessage.value = '';
  visit.value = null;
  vitals.value = null;
  vitalSignsError.value = '';
  try {
    const entry = selected.value;
    const [result, currentEntry, recentVitals] = await Promise.all([
      visitsService.getById(entry.visitId!), queueService.getEntry(entry.queueEntryId),
      [1, 6].includes(entry.status) ? patientsService.getVitalSigns(entry.patientId, 1, 20).catch(() => null) : null
    ]);
    if (ticket !== visitVersion) return;
    if (currentEntry.visitId !== result.visitId || currentEntry.patientId !== result.patientId) throw new Error('Visit mismatch');
    selected.value = currentEntry;
    entries.value = entries.value.map(entry => entry.queueEntryId === currentEntry.queueEntryId ? currentEntry : entry);
    if (currentEntry.dentistId !== null) startDentistId.value = currentEntry.dentistId;
    needsReview.value = false; visit.value = result;
    diagnosis.value = result.diagnosis ?? '';
    clinicalNotes.value = result.clinicalNotes ?? '';
    vitals.value = recentVitals?.items.find(record => record.visitId === result.visitId) ?? null;
    if (!recentVitals && [1, 6].includes(currentEntry.status)) vitalSignsError.value = 'Không tải được sinh hiệu. Có thể tải lại hồ sơ để kiểm tra.';
  } catch (error) {
    if (ticket !== visitVersion) return; needsReview.value = true;
    visitError.value = errorMessage(error, 'Không tải được hồ sơ khám. Vui lòng thử lại.');
  } finally { if (ticket === visitVersion) loadingVisit.value = false; }
}

async function startConsultation() {
  if (busy.value || !canStart.value || !selected.value || !auth.user) return;
  const ticket = life;
  starting.value = true;
  actionError.value = '';
  try {
    const updated = await queueService.startConsultation(selected.value.queueEntryId, Number(targetDentistId.value));
    if (ticket !== life) return;
    clearTvCallTimer();
    needsReview.value = true;
    entries.value = entries.value.map(entry => entry.queueEntryId === updated.queueEntryId ? updated : entry);
    selected.value = updated;
    await reloadVisit();
  } catch (error) {
    if (ticket !== life) return; needsReview.value = true;
    actionError.value = errorMessage(error, 'Không bắt đầu được phiên khám. Tải lại hồ sơ để kiểm tra trạng thái.');
  } finally { if (ticket === life) starting.value = false; }
}

async function saveDiagnosis() {
  if (busy.value || !canEdit.value || !visit.value) return;
  actionError.value = '';
  successMessage.value = '';
  if (!diagnosis.value.trim() || diagnosis.value.length > 1000 || clinicalNotes.value.length > 2000) {
    actionError.value = 'Nhập chẩn đoán (tối đa 1000 ký tự), ghi chú tối đa 2000 ký tự.';
    return;
  }
  const ticket = life;
  saving.value = true;
  try {
    const result = await visitsService.updateDiagnosis(visit.value.visitId, {
      diagnosis: diagnosis.value.trim(), clinicalNotes: clinicalNotes.value.trim() || null
    });
    if (ticket !== life) return;
    visit.value = result;
    diagnosis.value = result.diagnosis ?? '';
    clinicalNotes.value = result.clinicalNotes ?? '';
    successMessage.value = 'Đã lưu chẩn đoán và ghi chú lâm sàng. Có thể tải lại hồ sơ để xem kết quả đã lưu.';
  } catch (error) {
    if (ticket !== life) return;
    actionError.value = errorMessage(error, 'Không lưu được chẩn đoán. Kiểm tra lại quyền và trạng thái lần khám.');
  } finally { if (ticket === life) saving.value = false; }
}

function triggerTvRecall() {
  if (!selected.value || selected.value.status !== 6 || tvCalling.value) return;
  clearTvCallTimer();
  tvCalling.value = true;
  tvCallRemainingSeconds.value = TV_CALL_DURATION_SECONDS;

  const payload = {
    queueNumberDisplay: `A-${String(selected.value.queueNumber).padStart(3, '0')}`,
    maskedPatientName: maskPublicQueueName(selected.value.patientName),
    roomName: selected.value.roomName || null,
    status: selected.value.status
  };

  try {
    const channel = new BroadcastChannel('dental_tv_call_channel');
    channel.postMessage(payload);
    channel.close();
  } catch {}

  tvCalledMsg.value = `Đã phát tín hiệu gọi TV cho số ${payload.queueNumberDisplay} (${tvCallRemainingSeconds.value}s)`;
  tvCallTimer = setInterval(() => {
    tvCallRemainingSeconds.value--;
    if (tvCallRemainingSeconds.value <= 0) {
      clearTvCallTimer();
      return;
    }
    tvCalledMsg.value = `Đã phát tín hiệu gọi TV cho số ${payload.queueNumberDisplay} (${tvCallRemainingSeconds.value}s)`;
  }, 1000);
}

onMounted(async () => {
  const ticket = life;
  await Promise.all([loadQueue(), loadDentists()]);
  if (ticket === life) await openInitialVisit();
});
onBeforeRouteUpdate(() => !dirty.value && !busy.value);
watch(() => [route.query.queueEntryId, route.query.visitId], () => void openInitialVisit());
onUnmounted(() => { life++; queueVersion++; visitVersion++; dentistVersion++; preferredVersion++; clearTvCallTimer(); entries.value = []; selected.value = null; visit.value = null; diagnosis.value = ''; clinicalNotes.value = ''; dentists.value = []; startDentistId.value = 0; dentistError.value = ''; actionError.value = ''; successMessage.value = ''; queueError.value = ''; visitError.value = ''; });
</script>

<style scoped>
.pre-exam-vitals { display: grid; gap: .25rem; margin-top: .8rem; overflow-wrap: anywhere; }
.clinical-page { container-type: inline-size; color: #172b42; }
.clinical-heading { margin-bottom: 1.2rem; }
.clinical-heading h1 { margin: 0; font-size: clamp(1.45rem, 3vw, 2.1rem); line-height: 1.2; }
.clinical-heading p { margin: 0.45rem 0 0; color: #51647a; }
.clinical-layout { display: grid; align-items: start; gap: 1rem; }
.panel {
  min-width: 0;
  padding: clamp(0.85rem, 2.5cqw, 1.3rem);
  border: 1px solid #dbe5ed;
  border-radius: 14px;
  background: #fff;
}
.queue-panel, .detail-panel { display: grid; gap: 0.9rem; }
.panel-header, .patient-header, .item-top, .pagination-bar, .visit-actions, .assign-controls {
  display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem;
}
.panel h2, .panel h3, .panel p { margin: 0; }
.panel h2 { font-size: 1.15rem; }
.panel h3 { font-size: 1.04rem; }
.queue-list { display: grid; gap: 0.45rem; max-height: 52vh; overflow-y: auto; padding: 0.15rem; }
.queue-item-card {
  display: grid;
  gap: 0.25rem;
  width: 100%;
  min-width: 0;
  padding: 0.7rem;
  border: 1px solid #dbe5ed;
  border-inline-start: 3px solid transparent;
  border-radius: 9px;
  background: #f8fafc;
  color: inherit;
  text-align: start;
  font: inherit;
  cursor: pointer;
}
.queue-item-card.selected { border-color: #75aca9; border-inline-start-color: #176d6a; background: #edf8f6; }
.queue-item-card:not(:disabled):hover { border-color: #25827e; }
.queue-item-card:disabled { cursor: not-allowed; }
.queue-num { font-weight: 750; color: #235274; }
.queue-status-tag {
  padding: 0.24rem 0.52rem; border-radius: 7px; background: #edf2f6; color: #3f5369; font-size: 0.78rem; font-weight: 650;
}
.queue-status-tag[data-status="2"], .queue-status-tag[data-status="6"] { background: #def2ef; color: #144f4d; }
.queue-status-tag[data-status="1"], .queue-status-tag[data-status="7"] { background: #fff2d5; color: #76461a; }
.patient-name { font-weight: 750; }
.patient-name, .patient-header h2, .patient-meta, .record-val { overflow-wrap: anywhere; }
.patient-meta, .patient-submeta, .page-info, .hint-box, .empty-text, .loading-state-sm, .empty-selection-placeholder p { color: #51647a; font-size: 0.88rem; }
.patient-header { align-items: flex-start; padding-bottom: 0.8rem; border-bottom: 1px solid #e5edf2; }
.patient-header h2 { font-size: clamp(1.22rem, 3cqw, 1.55rem); }
.patient-submeta { display: flex; flex-wrap: wrap; gap: 0.38rem 0.75rem; margin-top: 0.35rem; }
.empty-selection-placeholder { display: grid; gap: 0.4rem; }
.visit-actions, .assign-controls { justify-content: flex-start; }
.btn {
  border: 1px solid #bccddc; border-radius: 8px; padding: 0.52rem 0.7rem; min-height: 38px;
  background: #fff; color: #254965; font: inherit; font-size: 0.88rem; font-weight: 700; cursor: pointer;
}
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.btn-primary { background: #176864; border-color: #176864; color: #fff; }
.tv-called-status { color: #176864; font-size: 0.84rem; }
.diagnosis-form, .read-only-diagnosis, .admin-assign-box {
  display: grid; gap: 0.85rem; padding: 0.85rem; border: 1px solid #e1e9ef; border-radius: 10px; min-width: 0;
}
.diagnosis-form > button { justify-self: start; }
.form-group { display: grid; gap: 0.4rem; min-width: 0; }
.form-group label, .admin-assign-box label, .record-label { font-weight: 650; font-size: 0.9rem; }
.form-group textarea, .assign-controls select {
  width: 100%; min-width: 0; padding: 0.6rem 0.7rem; border: 1px solid #b9cedd; border-radius: 8px; font: inherit;
}
.form-group textarea { resize: vertical; }
.assign-controls select { flex: 1 1 12rem; }
.char-count { color: #51647a; font-size: 0.79rem; text-align: end; }
.required { color: #9f2e27; }
.record-val { white-space: pre-wrap; }
.alert-box { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem; border-radius: 8px; padding: 0.65rem 0.8rem; }
.alert-error { color: #8d2624; background: #fff0ed; }
.alert-warning { color: #71401a; background: #fff3db; }
.alert-success { color: #215732; background: #e8f6ed; }
.link-inline, .care-links a { color: #1e5273; font-weight: 650; }
.care-links { display: flex; flex-wrap: wrap; gap: 0.45rem; padding-top: 0.8rem; border-top: 1px solid #e5edf2; }
.care-links a { padding: 0.42rem 0.65rem; border: 1px solid #d5e2eb; border-radius: 7px; font-size: 0.87rem; }
@container (min-width: 730px) {
  .clinical-layout { grid-template-columns: minmax(15rem, 1fr) minmax(0, 2.15fr); }
}
</style>
