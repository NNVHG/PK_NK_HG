<template>
  <main class="clinical-page">
    <div class="header-banner">
      <div class="header-icon">🩺</div>
      <div class="header-titles">
        <RouterLink class="back-link" to="/management/dashboard">← Bảng điều khiển</RouterLink>
        <h1>Khám bệnh & chẩn đoán lâm sàng</h1>
        <p class="subtitle">Tiếp nhận bệnh nhân từ hàng đợi tiếp đón, mở hồ sơ chuyên môn và ghi nhận chẩn đoán điều trị.</p>
      </div>
    </div>

    <div class="columns">
      <!-- Left: Queue Today -->
      <section class="panel queue-panel" aria-label="Lượt khám hôm nay">
        <div class="panel-header">
          <h2>Lượt khám hôm nay</h2>
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty" @click="loadQueue(page)">🔄 Làm mới</button>
        </div>
        <div v-if="loadingQueue" class="loading-state-sm" role="status">Đang tải lượt khám…</div>
        <div v-if="queueError" class="alert-box alert-error" role="alert">⚠️ {{ queueError }}</div>
        <p v-if="!loadingQueue && !queueError && !entries.length" class="empty-text">Chưa có bệnh nhân nào check-in hôm nay.</p>
        
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
              <span class="queue-status-tag">{{ entry.statusText }}</span>
            </div>
            <div class="patient-name">{{ entry.patientName }}</div>
            <div class="patient-meta">{{ entry.patientCode }} · Nha sĩ: <strong>{{ entry.dentistName || 'Chưa gán' }}</strong></div>
          </button>
        </div>

        <nav class="pagination-bar" aria-label="Phân trang lượt khám">
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty || page <= 1" @click="loadQueue(page - 1)">← Trước</button>
          <span class="page-info">Trang <strong>{{ page }}</strong> / {{ Math.max(totalPages, 1) }}</span>
          <button type="button" class="btn btn-outline-sm" :disabled="busy || dirty || page >= totalPages" @click="loadQueue(page + 1)">Sau →</button>
        </nav>
      </section>

      <!-- Right: Clinical Diagnosis Detail -->
      <section class="panel detail-panel" aria-label="Hồ sơ khám lâm sàng">
        <div v-if="!selected" class="empty-selection-placeholder">
          <div class="placeholder-icon">📋</div>
          <p>Vui lòng chọn một lượt khám ở danh sách bên trái để mở hồ sơ lâm sàng.</p>
        </div>

        <template v-else>
          <div class="patient-header">
            <div>
              <h2>{{ selected.patientName }} <span class="badge-code">{{ selected.patientCode }}</span></h2>
              <div class="patient-submeta">
                <span>📅 Tiếp đón: {{ selected.queueDate }}</span>
                <span>•</span>
                <span>Lần khám <strong>#{{ selected.visitId }}</strong></span>
              </div>
            </div>
            <button type="button" class="btn btn-outline-sm" :disabled="busy" @click="reloadVisit">
              {{ dirty ? '🔄 Hủy sửa & tải lại' : '🔄 Tải lại hồ sơ' }}
            </button>
          </div>

          <div v-if="visit?.status !== 'Correcting'" class="location-chip">
            📍 Vị trí khám: <strong>{{ selected.roomName || 'Chưa xếp phòng' }} · {{ selected.chairNumber || 'Chưa xếp ghế' }}</strong>
            <RouterLink to="/management/staff/workspace" class="link-inline">Kiểm tra vị trí làm việc →</RouterLink>
          </div>

          <div v-if="loadingVisit" class="loading-state-sm" role="status">Đang tải chi tiết lần khám…</div>
          <div v-if="visitError" class="alert-box alert-error" role="alert">⚠️ {{ visitError }}</div>

          <SafetyAlertBanner v-if="selected.patientId" :patient-id="selected.patientId" :refresh-key="visit?.visitId ?? selected.queueEntryId" />

          <template v-if="visit">
            <div class="quick-nav-bar" v-if="!dirty && !busy">
              <RouterLink class="quick-nav-btn quick-nav-btn--fdi" :to="`/management/clinical/visits/${visit.visitId}/fdi`">
                <span>🦷 Sơ đồ răng FDI &amp; Bệnh lý</span>
              </RouterLink>
              <RouterLink class="quick-nav-btn quick-nav-btn--rx" :to="`/management/clinical/visits/${visit.visitId}/prescription`">
                <span>💊 Kê đơn thuốc điện tử</span>
              </RouterLink>
              <RouterLink class="quick-nav-btn quick-nav-btn--img" :to="`/management/clinical/visits/${visit.visitId}/images`">
                <span>📸 Ảnh X-quang / Chụp miệng</span>
              </RouterLink>
              <RouterLink class="quick-nav-btn quick-nav-btn--bill" :to="`/management/clinical/visits/${visit.visitId}/invoice-draft`">
                <span>🧾 Lập hóa đơn nháp</span>
              </RouterLink>
            </div>

            <div class="visit-status-row">
              <span class="status-label">Trạng thái lần khám:</span>
              <span class="status-val font-bold">{{ visitStatusLabel }}</span>
            </div>

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
              ℹ️ Vui lòng tải lại hồ sơ để đối chiếu trạng thái trước khi thực hiện thao tác kế tiếp.
            </div>

            <div v-if="canStart" class="start-action-box">
              <button type="button" class="btn btn-primary btn-start" :disabled="busy" @click="startConsultation">
                {{ starting ? 'Đang kích hoạt ca khám…' : '🩺 Bắt đầu khám ca này' }}
              </button>
            </div>

            <!-- Nút gọi lại trên TV cho Nha sĩ khi ca đang mời vào phòng hoặc đang khám -->
            <div v-if="[1, 6].includes(selected.status)" class="tv-recall-box">
              <button
                type="button"
                class="btn btn-recall-tv"
                :disabled="busy || tvCalling"
                @click="triggerTvRecall"
              >
                {{ tvCalling ? 'Đang phát tín hiệu TV…' : '📢 Đọc & Gọi lại trên TV (30s)' }}
              </button>
              <span v-if="tvCalledMsg" class="tv-called-status">{{ tvCalledMsg }}</span>
            </div>

            <p v-if="visit.status === 'Created'" class="hint-box">
              ℹ️ Nha sĩ cần chọn ghế/phòng sẵn sàng tại mục <em>Vị trí làm việc</em> trước khi bấm bắt đầu khám.
            </p>
            <form v-if="canEdit" class="diagnosis-form" @submit.prevent="saveDiagnosis">
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
                ⚠️ Có thay đổi chưa lưu. Vui lòng bấm lưu trước khi chuyển sang lượt khám khác.
              </div>

              <button type="submit" class="btn btn-primary btn-save" :disabled="busy">
                {{ saving ? 'Đang lưu…' : '💾 Lưu chẩn đoán lâm sàng' }}
              </button>
            </form>

            <div v-else class="read-only-diagnosis">
              <p class="hint-box">🔒 Hồ sơ ở chế độ chỉ đọc. Chỉ nha sĩ phụ trách hoặc Admin được quyền cập nhật khi ca đang diễn ra.</p>
              <div class="record-group">
                <span class="record-label">Chẩn đoán:</span>
                <p class="record-val">{{ visit.diagnosis || 'Chưa ghi nhận chẩn đoán' }}</p>
              </div>
              <div class="record-group">
                <span class="record-label">Ghi chú lâm sàng:</span>
                <p class="record-val">{{ visit.clinicalNotes || 'Chưa có ghi chú' }}</p>
              </div>
            </div>
          </template>

          <div v-if="actionError" class="alert-box alert-error" role="alert">⚠️ {{ actionError }}</div>
          <div v-if="successMessage" class="alert-box alert-success" role="status">✅ {{ successMessage }}</div>
        </template>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import axios from 'axios';
import DiagnosisAutocomplete from '@/components/DiagnosisAutocomplete.vue';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';

import { useAuthStore } from '@/stores/auth';
import { queueService, type QueueEntryResponse } from '@/services/queue';
import { visitsService } from '@/services/visits';
import { appointmentsService, type BookingDentist } from '@/services/appointments';
import type { VisitDetails } from '@/services/patients';

const auth = useAuthStore();
const entries = ref<QueueEntryResponse[]>([]);
const selected = ref<QueueEntryResponse | null>(null);
const visit = ref<VisitDetails | null>(null);
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
let life = 0, queueVersion = 0, visitVersion = 0, dentistVersion = 0;
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
async function loadQueue(targetPage = 1) {
  if (busy.value || dirty.value || !Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000) return;
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

async function selectEntry(entry: QueueEntryResponse) {
  if (busy.value || dirty.value) return;
  selected.value = entry; startDentistId.value = entry.dentistId ?? 0;
  needsReview.value = false;
  await reloadVisit();
}

async function reloadVisit() {
  if (!selected.value?.visitId) return;
  const ticket = ++visitVersion;
  loadingVisit.value = true;
  visitError.value = '';
  actionError.value = '';
  successMessage.value = '';
  visit.value = null;
  try {
    const [result, currentEntry] = await Promise.all([
      visitsService.getById(selected.value.visitId), queueService.getEntry(selected.value.queueEntryId)
    ]);
    if (ticket !== visitVersion) return;
    if (currentEntry.visitId !== result.visitId) throw new Error('Visit mismatch');
    selected.value = currentEntry;
    entries.value = entries.value.map(entry => entry.queueEntryId === currentEntry.queueEntryId ? currentEntry : entry);
    if (currentEntry.dentistId !== null) startDentistId.value = currentEntry.dentistId;
    needsReview.value = false; visit.value = result;
    diagnosis.value = result.diagnosis ?? '';
    clinicalNotes.value = result.clinicalNotes ?? '';
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
    if (ticket !== life) return; needsReview.value = true;
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
  if (!selected.value || tvCalling.value) return;
  tvCalling.value = true;
  tvCalledMsg.value = '';

  const payload = {
    queueEntryId: selected.value.queueEntryId,
    queueNumberDisplay: `A-${String(selected.value.queueNumber).padStart(3, '0')}`,
    maskedPatientName: selected.value.patientName || 'Bệnh nhân',
    roomName: selected.value.roomName || 'Phòng khám chuyên khoa',
    chairNumber: selected.value.chairNumber || '',
    dentistName: selected.value.dentistName || auth.user?.fullName || 'Bác sĩ phụ trách'
  };

  try {
    const channel = new BroadcastChannel('dental_tv_call_channel');
    channel.postMessage(payload);
    channel.close();
  } catch {}

  try {
    localStorage.setItem('dental_tv_call_event', JSON.stringify({ ...payload, timestamp: Date.now() }));
  } catch {}

  tvCalledMsg.value = `Đã phát tín hiệu gọi TV cho số ${payload.queueNumberDisplay} (30s)`;
  setTimeout(() => {
    tvCalling.value = false;
    setTimeout(() => { tvCalledMsg.value = ''; }, 4000);
  }, 1000);
}

onMounted(() => Promise.all([loadQueue(), loadDentists()]));
onUnmounted(() => { life++; queueVersion++; visitVersion++; dentistVersion++; entries.value = []; selected.value = null; visit.value = null; diagnosis.value = ''; clinicalNotes.value = ''; dentists.value = []; startDentistId.value = 0; dentistError.value = ''; actionError.value = ''; successMessage.value = ''; queueError.value = ''; visitError.value = ''; });
</script>

<style scoped>
.clinical-page {
  max-width: 1400px;
  margin: 0 auto;
  padding: var(--spacing-6);
  color: var(--color-text-main);
  box-sizing: border-box;
}

.header-banner {
  display: flex;
  align-items: center;
  gap: var(--spacing-4);
  margin-block-end: var(--spacing-6);
  padding-block-end: var(--spacing-4);
  border-block-end: 1px solid var(--color-border-subtle);
}

.header-icon {
  font-size: 2.2rem;
  background: var(--color-medical-light);
  width: 3.5rem;
  height: 3.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-lg);
  flex-shrink: 0;
}

.back-link {
  font-size: var(--font-size-xs);
  color: var(--color-medical);
  text-decoration: none;
  font-weight: 750;
  margin-block-end: 0.2rem;
  display: inline-block;
}

.back-link:hover {
  text-decoration: underline;
}

.header-titles h1 {
  margin: 0;
  font-size: var(--font-size-2xl);
  color: var(--color-primary);
  font-weight: 850;
  letter-spacing: -0.01em;
}

.subtitle {
  margin: 0.25rem 0 0;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}

.columns {
  display: grid;
  grid-template-columns: 360px 1fr;
  gap: var(--spacing-6);
  align-items: start;
}

.panel {
  padding: var(--card-padding);
  border: 1px solid var(--color-border-subtle);
  background: #ffffff;
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-card);
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-block-end: var(--spacing-4);
  padding-block-end: var(--spacing-3);
  border-block-end: 1px solid var(--color-border-subtle);
}

.panel-header h2 {
  margin: 0;
  font-size: var(--font-size-lg);
  color: var(--color-primary);
  font-weight: 800;
}

.queue-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-2);
  max-height: calc(100vh - 280px);
  overflow-y: auto;
  padding-right: 0.25rem;
}

.queue-item-card {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: var(--spacing-3) var(--spacing-4);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
  cursor: pointer;
  text-align: left;
  border-left: 4px solid var(--color-border-medium);
  transition: all 0.15s ease;
  font-family: inherit;
  color: inherit;
}

.queue-item-card:hover:not(:disabled) {
  background: #f0f9ff;
  border-color: #bae6fd;
  transform: translateX(2px);
}

.queue-item-card.selected {
  background: #e0f2fe;
  border-color: var(--color-medical);
  border-left-color: var(--color-medical);
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.2);
}

.item-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.queue-num {
  font-weight: 850;
  color: var(--color-medical);
  font-size: var(--font-size-sm);
}

.queue-status-tag {
  font-size: 0.7rem;
  padding: 0.15rem 0.5rem;
  border-radius: var(--radius-pill);
  background: #e2e8f0;
  color: #334155;
  font-weight: 750;
}

.patient-name {
  font-weight: 800;
  color: var(--color-primary);
  font-size: var(--font-size-sm);
}

.patient-meta {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-block-start: var(--spacing-4);
  padding-block-start: var(--spacing-3);
  border-block-start: 1px solid var(--color-border-subtle);
}

.empty-selection-placeholder {
  text-align: center;
  padding: 5rem 2rem;
  color: var(--color-text-subtle);
}

.placeholder-icon {
  font-size: 3rem;
  margin-block-end: var(--spacing-3);
}

.patient-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: var(--spacing-3);
  border-block-end: 1px solid var(--color-border-subtle);
  padding-block-end: var(--spacing-4);
  margin-block-end: var(--spacing-4);
}

.patient-header h2 {
  margin: 0;
  font-size: var(--font-size-xl);
  color: var(--color-primary);
  display: flex;
  align-items: center;
  gap: var(--spacing-2);
  font-weight: 850;
}

.badge-code {
  font-size: var(--font-size-xs);
  font-weight: 800;
  background: var(--color-medical-light);
  color: var(--color-medical-hover);
  padding: 0.2rem 0.55rem;
  border-radius: var(--radius-pill);
  border: 1px solid #bae6fd;
}

.patient-submeta {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
  display: flex;
  gap: var(--spacing-2);
  margin-block-start: 0.25rem;
}

.location-chip {
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  padding: var(--spacing-3) var(--spacing-4);
  font-size: var(--font-size-sm);
  margin-block-end: var(--spacing-4);
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--spacing-2);
}

.link-inline {
  color: var(--color-medical);
  text-decoration: underline;
  font-weight: 700;
  font-size: var(--font-size-xs);
}

/* Quick Nav Bar - 4 Nút nghiệp vụ chuyên khoa */
.quick-nav-bar {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--spacing-3);
  margin-block-end: var(--spacing-5);
}

.quick-nav-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--spacing-2);
  padding: var(--spacing-3) var(--spacing-4);
  border-radius: var(--radius-md);
  font-size: var(--font-size-sm);
  font-weight: 750;
  text-decoration: none;
  transition: all 0.15s ease;
  min-block-size: 46px;
  border: 1px solid transparent;
}

.quick-nav-btn--fdi {
  background: #f0fdf4;
  border-color: #bbf7d0;
  color: #166534;
}
.quick-nav-btn--fdi:hover {
  background: #dcfce7;
  border-color: #86efac;
}

.quick-nav-btn--rx {
  background: #eff6ff;
  border-color: #bfdbfe;
  color: #1e40af;
}
.quick-nav-btn--rx:hover {
  background: #dbeafe;
  border-color: #93c5fd;
}

.quick-nav-btn--img {
  background: #faf5ff;
  border-color: #e9d5ff;
  color: #6b21a8;
}
.quick-nav-btn--img:hover {
  background: #f3e8ff;
  border-color: #d8b4fe;
}

.quick-nav-btn--bill {
  background: #fff7ed;
  border-color: #fed7aa;
  color: #9a3412;
}
.quick-nav-btn--bill:hover {
  background: #ffedd5;
  border-color: #fdba74;
}

.visit-status-row {
  display: flex;
  gap: var(--spacing-2);
  align-items: center;
  font-size: var(--font-size-sm);
  margin-block-end: var(--spacing-4);
}

.admin-assign-box {
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-3) var(--spacing-4);
  margin-block-end: var(--spacing-4);
}

.assign-controls {
  display: flex;
  gap: var(--spacing-2);
  align-items: center;
}

.assign-controls select {
  height: 42px;
  padding: 0.4rem 0.75rem;
  border: 1px solid var(--color-border-medium);
  border-radius: var(--radius-sm);
  font-size: var(--font-size-sm);
  flex: 1;
}

.btn-start {
  width: 100%;
  height: 48px;
  font-size: var(--font-size-base);
}

/* TV Recall Bar */
.tv-recall-box {
  margin-block-end: var(--spacing-4);
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  flex-wrap: wrap;
}

.btn-recall-tv {
  background: linear-gradient(135deg, #fef08a, #fde047);
  border: 1px solid #eab308;
  color: #713f12;
  font-weight: 800;
  height: 42px;
  padding: 0 var(--spacing-4);
  border-radius: var(--radius-md);
  box-shadow: 0 1px 2px rgba(0,0,0,0.05);
}

.btn-recall-tv:hover:not(:disabled) {
  background: #facc15;
}

.tv-called-status {
  font-size: var(--font-size-xs);
  color: #15803d;
  font-weight: 750;
}

.hint-box {
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  padding: var(--spacing-3) var(--spacing-4);
  font-size: var(--font-size-xs);
  color: var(--color-text-muted);
  margin-block-end: var(--spacing-4);
}

.diagnosis-form {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-4);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.form-group label {
  font-size: var(--font-size-sm);
  font-weight: 700;
  color: var(--color-text-main);
}

.required {
  color: var(--color-danger);
}

.char-count {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
  text-align: right;
}

textarea {
  width: 100%;
  border: 1px solid var(--color-border-medium);
  border-radius: var(--radius-md);
  padding: var(--spacing-3);
  font-family: inherit;
  font-size: var(--font-size-sm);
  box-sizing: border-box;
}

textarea:focus-visible {
  outline: none;
  border-color: var(--color-medical);
  box-shadow: var(--shadow-focus);
}

.btn-save {
  height: 48px;
  width: 100%;
  font-size: var(--font-size-base);
}

.record-group {
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  padding: var(--spacing-3) var(--spacing-4);
  margin-block-end: var(--spacing-3);
}

.record-label {
  font-size: var(--font-size-xs);
  text-transform: uppercase;
  color: var(--color-text-subtle);
  font-weight: 800;
  display: block;
  margin-block-end: 0.25rem;
  letter-spacing: 0.04em;
}

.record-val {
  margin: 0;
  font-size: var(--font-size-sm);
  color: var(--color-text-main);
  white-space: pre-wrap;
  line-height: var(--line-height-normal);
}

.alert-box {
  padding: var(--spacing-3) var(--spacing-4);
  border-radius: var(--radius-md);
  font-size: var(--font-size-sm);
  margin-block-end: var(--spacing-4);
}

.alert-error {
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  color: #991b1b;
}

.alert-success {
  background: var(--color-success-light);
  border: 1px solid var(--color-success-border);
  color: #166534;
}

.alert-warning {
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  color: #92400e;
}

.btn {
  height: 44px;
  padding: 0 var(--spacing-4);
  border-radius: var(--radius-md);
  font-weight: 700;
  font-size: var(--font-size-sm);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  font-family: inherit;
  transition: all 0.15s ease;
}

.btn-primary {
  background: var(--color-primary);
  color: #ffffff;
}
.btn-primary:hover:not(:disabled) {
  background: var(--color-primary-hover);
}

.btn-outline-sm {
  height: 36px;
  padding: 0 var(--spacing-3);
  border-radius: var(--radius-sm);
  background: #ffffff;
  color: var(--color-text-main);
  border: 1px solid var(--color-border-medium);
  font-size: var(--font-size-xs);
}
.btn-outline-sm:hover:not(:disabled) {
  background: var(--color-surface-subtle);
}

.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

@media (max-width: 900px) {
  .columns {
    grid-template-columns: 1fr;
  }
}
</style>
