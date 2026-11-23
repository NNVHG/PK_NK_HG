<template>
  <main class="clinical-page">
    <header class="clinical-topbar">
      <div class="topbar-left">
        <RouterLink to="/management/queue/today" class="back-queue-link">← Hàng đợi hôm nay</RouterLink>
        <h1>Khám bệnh &amp; chẩn đoán lâm sàng</h1>
      </div>
    </header>

    <section class="panel detail-panel" aria-label="Hồ sơ khám lâm sàng">
      <div v-if="!selected" class="empty-selection-placeholder">
        <h2>Chưa có ca khám đang mở</h2>
        <p v-if="actionError" class="alert-box alert-error" role="alert">{{ actionError }}</p>
        <p>Chọn hoặc gọi số bệnh nhân từ <strong>Hàng đợi hôm nay</strong> để bắt đầu khám.</p>
        <RouterLink to="/management/queue/today" class="btn btn-primary go-queue-btn">Đi tới Hàng đợi hôm nay để gọi số →</RouterLink>
      </div>

      <template v-else>
        <header class="patient-header">
          <div class="patient-identity">
            <h2 ref="patientHeading" tabindex="-1">{{ selected.patientName }}</h2>
            <div class="patient-submeta">
              <span>{{ selected.patientCode }}</span>
              <span>Lượt số {{ selected.queueNumber }}</span>
              <span>Lần khám #{{ selected.visitId }}</span>
              <span v-if="selected.roomName && visit?.status !== 'Correcting'">{{ selected.roomName }}</span>
            </div>
          </div>

          <div class="patient-header-right">
            <span class="queue-status-tag" :data-status="selected.status">
              {{ visitStatusLabel || selected.statusText }}
            </span>
            <div v-if="visit" class="visit-actions">
              <button v-if="canStart" type="button" class="btn btn-primary" :disabled="busy" @click="startConsultation">
                {{ starting ? 'Đang bắt đầu…' : '▶ Bắt đầu khám' }}
              </button>
              <button
                v-if="canCallOnTv"
                type="button"
                class="btn btn-outline-sm"
                :disabled="busy || needsReview"
                @click="triggerTvRecall"
              >
                {{ tvCalling ? 'Đang gửi lời mời…' : 'Gọi lại trên TV' }}
              </button>
              <button type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">
                {{ dirty ? 'Hủy sửa & tải lại' : 'Tải lại' }}
              </button>
            </div>
          </div>
        </header>

        <div v-if="tvCalledMsg" class="tv-called-banner" role="status">{{ tvCalledMsg }}</div>
        <div v-if="loadingVisit" class="loading-state-sm" role="status">Đang tải chi tiết lần khám…</div>
        <div v-if="visitError" class="alert-box alert-error" role="alert">
          {{ visitError }}
          <button type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">Thử tải lại</button>
        </div>
        <div v-if="actionError" class="alert-box alert-error" role="alert">{{ actionError }}</div>
        <div v-if="successMessage" class="alert-box alert-success" role="status">{{ successMessage }}</div>

        <template v-if="visit">
          <div v-if="visit.status === 'Correcting'" class="alert-box alert-warning">
            <span>⚠️ Đang ở chế độ đính chính hồ sơ sau ca khám.</span>
            <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/invoice-draft`, query: visitNavQuery }" class="link-inline">
              Hoàn tất và chốt hồ sơ →
            </RouterLink>
          </div>
          <div v-if="needsReview" class="alert-box alert-warning" role="alert">
            Tải lại hồ sơ để đối chiếu trạng thái trước khi tiếp tục.
          </div>

          <!-- TRẠNG THÁI TIỀN KHÁM (Chờ / Đang gọi số) -->
          <div v-if="[1, 6].includes(selected.status) && !canEdit" class="pre-exam-grid">
            <SafetyAlertBanner v-if="selected.patientId" :patient-id="selected.patientId" :refresh-key="visit?.visitId ?? selected.queueEntryId" />

            <div class="pre-exam-card">
              <h3>Sinh hiệu tiền khám</h3>
              <p v-if="vitalSignsError" class="muted-note" role="status">{{ vitalSignsError }}</p>
              <div v-else-if="vitals" class="vitals-grid">
                <div class="vital-tile">
                  <span class="vital-label">Huyết áp</span>
                  <strong>{{ vitals.systolicBp ?? '—' }}/{{ vitals.diastolicBp ?? '—' }}</strong>
                  <span class="vital-unit">mmHg</span>
                </div>
                <div class="vital-tile">
                  <span class="vital-label">Mạch</span>
                  <strong>{{ vitals.pulseBpm ?? '—' }}</strong>
                  <span class="vital-unit">lần/phút</span>
                </div>
                <div class="vital-tile">
                  <span class="vital-label">Nhiệt độ</span>
                  <strong>{{ vitals.temperatureC ?? '—' }}</strong>
                  <span class="vital-unit">°C</span>
                </div>
              </div>
              <p v-else class="muted-note">Chưa có số đo sinh hiệu ở lần khám này.</p>

              <div v-if="auth.role === 'ADMIN' && !visit.isLocked" class="admin-assign-box">
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

              <p class="pre-exam-hint">Kiểm tra dị ứng, huyết áp và bấm <strong>▶ Bắt đầu khám</strong> ở góc trên bên phải để mở khu chẩn đoán &amp; sơ đồ răng FDI.</p>
            </div>
          </div>

          <!-- TRẠNG THÁI ĐANG KHÁM / ĐÍNH CHÍNH: Cockpit 2 cột trực quan -->
          <div v-else-if="canEdit" class="clinical-cockpit">
            <div class="clinical-main-col">
              <form class="diagnosis-form" @submit.prevent="saveDiagnosis">
                <div class="form-head">
                  <h3>Chẩn đoán &amp; ghi chú lâm sàng</h3>
                  <span v-if="dirty" class="dirty-badge">Chưa lưu</span>
                </div>

                <div class="form-group">
                  <label for="diagnosis">Chẩn đoán bệnh lý chính <span class="required">*</span></label>
                  <DiagnosisAutocomplete :key="visit.visitId" v-model="diagnosis" :disabled="busy" />
                  <div class="char-count">{{ diagnosis.length }}/1000 ký tự</div>
                </div>

                <div class="form-group">
                  <label for="clinical-notes">Ghi chú diễn tiến lâm sàng &amp; dặn dò</label>
                  <textarea id="clinical-notes" v-model="clinicalNotes" :disabled="busy" maxlength="2000" rows="4" placeholder="Ghi chú lâm sàng, triệu chứng cụ thể, dặn dò chăm sóc sau thủ thuật..."></textarea>
                  <div class="char-count">{{ clinicalNotes.length }}/2000 ký tự</div>
                </div>

                <div class="diagnosis-footer">
                  <div class="diagnosis-actions">
                    <button type="submit" class="btn btn-primary btn-save" :disabled="busy">
                      {{ saving ? 'Đang lưu…' : 'Lưu chẩn đoán' }}
                    </button>
                    <button v-if="dirty" type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">
                      Hủy sửa
                    </button>
                  </div>
                  <span v-if="dirty" class="unsaved-hint">Lưu chẩn đoán hoặc hủy sửa trước khi chuyển bước khác.</span>
                </div>
              </form>
            </div>

            <aside class="clinical-side-col" aria-label="An toàn và thao tác nhanh trong ca khám">
              <SafetyAlertBanner v-if="selected.patientId" :patient-id="selected.patientId" :refresh-key="visit?.visitId ?? selected.queueEntryId" />

              <div class="side-card vitals-card">
                <h3>Sinh hiệu lần khám</h3>
                <p v-if="vitalSignsError" class="muted-note" role="status">{{ vitalSignsError }}</p>
                <div v-else-if="vitals" class="vitals-grid compact">
                  <div class="vital-tile">
                    <span class="vital-label">Huyết áp</span>
                    <strong>{{ vitals.systolicBp ?? '—' }}/{{ vitals.diastolicBp ?? '—' }}</strong>
                    <span class="vital-unit">mmHg</span>
                  </div>
                  <div class="vital-tile">
                    <span class="vital-label">Mạch</span>
                    <strong>{{ vitals.pulseBpm ?? '—' }}</strong>
                    <span class="vital-unit">l/ph</span>
                  </div>
                  <div class="vital-tile">
                    <span class="vital-label">Nhiệt độ</span>
                    <strong>{{ vitals.temperatureC ?? '—' }}</strong>
                    <span class="vital-unit">°C</span>
                  </div>
                </div>
                <p v-else class="muted-note">Chưa có số đo sinh hiệu ở lần khám này.</p>
              </div>

              <div class="side-card care-links-panel">
                <h3>Thao tác chuyên môn (1 chạm)</h3>
                <p v-if="dirty || busy" class="muted-note">Lưu chẩn đoán hoặc hủy sửa để mở các bước tiếp theo.</p>
                <nav v-else class="care-links" aria-label="Nghiệp vụ cùng lần khám">
                  <div class="care-links-grid">
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/fdi`, query: visitNavQuery }" class="care-tile care-tile-wide">Sơ đồ răng FDI &amp; Chỉ định dịch vụ</RouterLink>
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/prescription`, query: visitNavQuery }" class="care-tile">Kê đơn thuốc</RouterLink>
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/images`, query: visitNavQuery }" class="care-tile">Ảnh khám</RouterLink>
                  </div>
                  <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/invoice-draft`, query: visitNavQuery }" class="care-finish-btn">
                    Hoàn tất khám &amp; Hóa đơn nháp →
                  </RouterLink>
                </nav>
              </div>
            </aside>
          </div>

          <!-- TRẠNG THÁI CHỈ ĐỌC (Đã khám xong / Đã khóa) -->
          <div v-else class="clinical-cockpit">
            <div class="clinical-main-col">
              <div class="read-only-diagnosis">
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
            </div>

            <aside class="clinical-side-col">
              <SafetyAlertBanner v-if="selected.patientId" :patient-id="selected.patientId" :refresh-key="visit?.visitId ?? selected.queueEntryId" />
              <div v-if="!dirty && !busy" class="side-card care-links-panel">
                <h3>Xem hồ sơ lần khám</h3>
                <nav class="care-links" aria-label="Nghiệp vụ cùng lần khám">
                  <div class="care-links-grid">
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/fdi`, query: visitNavQuery }" class="care-tile care-tile-wide">Sơ đồ răng FDI &amp; Chỉ định dịch vụ</RouterLink>
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/prescription`, query: visitNavQuery }" class="care-tile">Đơn thuốc</RouterLink>
                    <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/images`, query: visitNavQuery }" class="care-tile">Ảnh khám</RouterLink>
                  </div>
                  <RouterLink :to="{ path: `/management/clinical/visits/${visit.visitId}/invoice-draft`, query: visitNavQuery }" class="care-finish-btn secondary">
                    Hóa đơn nháp →
                  </RouterLink>
                </nav>
              </div>
            </aside>
          </div>
        </template>
      </template>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { onBeforeRouteUpdate, useRoute } from 'vue-router';
import axios from 'axios';
import DiagnosisAutocomplete from '@/components/DiagnosisAutocomplete.vue';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';

import { useAuthStore } from '@/stores/auth';
import { queueService, type QueueEntryResponse } from '@/services/queue';
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
const starting = ref(false);
const saving = ref(false);
const tvCalling = ref(false);
const tvCalledMsg = ref('');
const queueError = ref('');
const visitError = ref('');
const actionError = ref('');
const successMessage = ref('');
const needsReview = ref(false), dentists = ref<BookingDentist[]>([]), startDentistId = ref(0), dentistsLoading = ref(false), dentistError = ref('');
let life = 0, queueVersion = 0, visitVersion = 0, dentistVersion = 0, preferredVersion = 0;
const targetDentistId = computed(() => auth.role === 'DENTIST' ? auth.user?.userId : startDentistId.value);
const visitNavQuery = computed(() => selected.value?.queueEntryId ? { queueEntryId: String(selected.value.queueEntryId) } : undefined);
const busy = computed(() => loadingQueue.value || loadingVisit.value || starting.value || saving.value || tvCalling.value);
const canCallOnTv = computed(() => auth.role === 'DENTIST' && selected.value?.status === 6 &&
  selected.value.dentistId === auth.user?.userId && Boolean(selected.value.visitId));
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
function clearTvCallStatus() {
  tvCalling.value = false;
  tvCalledMsg.value = '';
}

async function loadQueue(targetPage = 1) {
  if (busy.value || dirty.value || !Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000) return;
  clearTvCallStatus();
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
        (auth.role === 'DENTIST' && linked.dentistId !== null && linked.dentistId !== auth.user?.userId)) throw new Error('Invalid visit link');
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
      actionError.value = 'Không mở được lượt được liên kết. Quay lại Hàng đợi hôm nay để chọn lượt.';
  }
}

async function selectEntry(entry: QueueEntryResponse) {
  if (busy.value || dirty.value) return;
  preferredVersion++;
  clearTvCallStatus();
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
      [1, 2, 6].includes(entry.status) ? patientsService.getVitalSigns(entry.patientId, 1, 20).catch(() => null) : null
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
    if (!recentVitals && [1, 2, 6].includes(currentEntry.status)) vitalSignsError.value = 'Không tải được sinh hiệu. Có thể tải lại hồ sơ để kiểm tra.';
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
    clearTvCallStatus();
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

async function triggerTvRecall() {
  const entry = selected.value;
  if (!entry || !canCallOnTv.value || busy.value || needsReview.value) return;
  const ticket = life;
  clearTvCallStatus();
  actionError.value = '';
  tvCalling.value = true;
  try {
    const result = await queueService.callOnTv(entry.queueEntryId);
    if (ticket !== life) return;
    if (result.queueEntryId !== entry.queueEntryId || result.visitId !== entry.visitId ||
      result.status !== 6 || result.dentistId !== auth.user?.userId) throw new Error('Unexpected invitation');
    tvCalledMsg.value = 'Đã gửi lời mời tới TV phòng chờ.';
  } catch (error) {
    if (ticket === life) actionError.value = errorMessage(error, 'Không thể gửi lời mời tới TV. Tải lại hồ sơ và thử lại.');
  } finally {
    if (ticket === life) tvCalling.value = false;
  }
}

onMounted(async () => {
  const ticket = life;
  await loadDentists();
  if (ticket === life) await openInitialVisit();
});
onBeforeRouteUpdate(() => !dirty.value && !busy.value);
watch(() => [route.query.queueEntryId, route.query.visitId], () => void openInitialVisit());
onUnmounted(() => { life++; queueVersion++; visitVersion++; dentistVersion++; preferredVersion++; clearTvCallStatus(); entries.value = []; selected.value = null; visit.value = null; diagnosis.value = ''; clinicalNotes.value = ''; dentists.value = []; startDentistId.value = 0; dentistError.value = ''; actionError.value = ''; successMessage.value = ''; queueError.value = ''; visitError.value = ''; });
</script>

<style scoped>
.clinical-page {
  container-type: inline-size;
  max-width: 1320px;
  margin: 0 auto;
  color: #172b42;
}
.clinical-topbar { margin-bottom: 0.9rem; }
.topbar-left { display: flex; flex-wrap: wrap; align-items: center; gap: 0.65rem 1.1rem; }
.back-queue-link {
  display: inline-flex; align-items: center; min-height: 36px; padding: 0.3rem 0.75rem;
  border: 1px solid #c7d7e4; border-radius: 8px; background: #fff; color: #1e5273;
  font-size: 0.86rem; font-weight: 700; text-decoration: none;
}
.back-queue-link:hover { background: #f4fafb; border-color: #87bdb9; }
.clinical-topbar h1 { margin: 0; font-size: clamp(1.35rem, 2.5vw, 1.85rem); line-height: 1.2; color: #102e56; }

.panel {
  min-width: 0;
  padding: clamp(0.95rem, 2.2cqw, 1.35rem);
  border: 1px solid #dbe5ed;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 4px 16px rgba(16, 46, 86, 0.04);
}
.detail-panel { display: grid; gap: 0.95rem; }
.panel h2, .panel h3, .panel p { margin: 0; }
.panel h2 { font-size: 1.25rem; }
.panel h3 { font-size: 1.02rem; color: #102e56; }

.empty-selection-placeholder {
  display: grid;
  justify-items: center;
  text-align: center;
  gap: 0.75rem;
  padding: 2.2rem 1rem;
}
.empty-selection-placeholder p { color: #51647a; font-size: 0.94rem; }
.go-queue-btn { text-decoration: none; display: inline-flex; align-items: center; margin-top: 0.25rem; }

.patient-header {
  display: flex; align-items: flex-start; justify-content: space-between; flex-wrap: wrap; gap: 0.75rem 1rem;
  padding-bottom: 0.85rem; border-bottom: 1px solid #e5edf2;
}
.patient-identity { min-width: 0; }
.patient-header h2 { font-size: clamp(1.25rem, 2.8cqw, 1.55rem); color: #102e56; overflow-wrap: anywhere; }
.patient-submeta { display: flex; flex-wrap: wrap; gap: 0.35rem 0.85rem; margin-top: 0.3rem; color: #51647a; font-size: 0.88rem; }
.patient-header-right { display: flex; flex-wrap: wrap; align-items: center; gap: 0.55rem; }
.visit-actions, .assign-controls { display: flex; align-items: center; flex-wrap: wrap; gap: 0.5rem; }

.queue-status-tag {
  padding: 0.28rem 0.65rem; border-radius: 999px; background: #edf2f6; color: #3f5369; font-size: 0.8rem; font-weight: 700;
}
.queue-status-tag[data-status="2"], .queue-status-tag[data-status="6"] { background: #def2ef; color: #144f4d; }
.queue-status-tag[data-status="1"], .queue-status-tag[data-status="7"] { background: #fff2d5; color: #76461a; }

.tv-called-banner {
  padding: 0.5rem 0.8rem; border-radius: 8px; background: #e8f5f3; color: #0f5c59; font-size: 0.86rem; font-weight: 600;
}
.loading-state-sm, .hint-box, .muted-note { color: #51647a; font-size: 0.86rem; }

.pre-exam-grid, .clinical-cockpit {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}
.clinical-main-col, .clinical-side-col { display: grid; gap: 0.85rem; min-width: 0; }

.pre-exam-card, .side-card, .diagnosis-form, .read-only-diagnosis, .admin-assign-box {
  display: grid; gap: 0.75rem; padding: 0.95rem; border: 1px solid #e1e9ef; border-radius: 12px; background: #fcfdff; min-width: 0;
}
.pre-exam-hint { font-size: 0.86rem; color: #3c5468; background: #f0f7fb; padding: 0.6rem 0.75rem; border-radius: 8px; }

.vitals-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.55rem; }
.vital-tile {
  display: grid; gap: 0.15rem; padding: 0.55rem 0.65rem;
  border: 1px solid #d9e6f0; border-radius: 9px; background: #fff; text-align: center;
}
.vital-label { font-size: 0.74rem; color: #5b7184; font-weight: 650; }
.vital-tile strong { font-size: 1.05rem; color: #102e56; line-height: 1.2; }
.vital-unit { font-size: 0.72rem; color: #6c8091; }

.form-head { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; }
.dirty-badge { padding: 0.18rem 0.5rem; border-radius: 999px; background: #fff3db; color: #8a4a08; font-size: 0.75rem; font-weight: 700; }
.form-group { display: grid; gap: 0.38rem; min-width: 0; }
.form-group label, .admin-assign-box label, .record-label { font-weight: 650; font-size: 0.9rem; }
.form-group textarea, .assign-controls select {
  width: 100%; min-width: 0; padding: 0.58rem 0.7rem; border: 1px solid #b9cedd; border-radius: 8px; font: inherit; background: #fff;
}
.form-group textarea { resize: vertical; }
.assign-controls select { flex: 1 1 12rem; }
.char-count { color: #51647a; font-size: 0.78rem; text-align: end; }
.required { color: #9f2e27; }
.diagnosis-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 0.65rem; }
.diagnosis-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
.unsaved-hint { font-size: 0.82rem; color: #8a4a08; font-weight: 600; }

.care-links { display: grid; gap: 0.55rem; }
.care-links-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }
.care-tile {
  display: flex; align-items: center; justify-content: center; text-align: center;
  min-height: 44px; padding: 0.5rem 0.65rem;
  border: 1px solid #cddce8; border-radius: 9px; background: #fff;
  color: #18496a; font-size: 0.86rem; font-weight: 700; text-decoration: none;
  transition: background 0.15s ease, border-color 0.15s ease, transform 0.15s ease;
}
.care-tile:hover { background: #eef7fb; border-color: #176864; color: #0f524f; transform: translateY(-1px); }
.care-tile-wide { grid-column: 1 / -1; background: #eff6ff; border-color: #bfdbfe; color: #1d4ed8; }
.care-tile-wide:hover { background: #dbeafe; border-color: #1d4ed8; color: #1e3a8a; }
.care-finish-btn {
  display: flex; align-items: center; justify-content: center; text-align: center;
  min-height: 44px; padding: 0.55rem 0.85rem;
  border-radius: 9px; background: #176864; border: 1px solid #176864;
  color: #fff; font-size: 0.88rem; font-weight: 750; text-decoration: none;
}
.care-finish-btn:hover { background: #0f524f; border-color: #0f524f; }
.care-finish-btn.secondary { background: #255c81; border-color: #255c81; }

.btn {
  border: 1px solid #bccddc; border-radius: 8px; padding: 0.5rem 0.8rem; min-height: 40px;
  background: #fff; color: #254965; font: inherit; font-size: 0.88rem; font-weight: 700; cursor: pointer;
}
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.btn-primary { background: #176864; border-color: #176864; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0f524f; border-color: #0f524f; }
.record-val { white-space: pre-wrap; overflow-wrap: anywhere; }
.alert-box { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem; border-radius: 8px; padding: 0.65rem 0.8rem; }
.alert-error { color: #8d2624; background: #fff0ed; }
.alert-warning { color: #71401a; background: #fff3db; }
.alert-success { color: #215732; background: #e8f6ed; }
.link-inline { color: #1e5273; font-weight: 650; }

@container (min-width: 820px) {
  .pre-exam-grid { grid-template-columns: minmax(0, 1.15fr) minmax(0, 0.85fr); }
  .clinical-cockpit { grid-template-columns: minmax(0, 1.35fr) minmax(0, 0.95fr); }
}
@media (max-width: 480px) {
  .vitals-grid, .care-links-grid { grid-template-columns: 1fr; }
}
</style>
