<template>
  <main class="clinical-page">
    <header>
      <RouterLink to="/">← Trang chủ</RouterLink>
      <h1>Khám và chẩn đoán</h1>
      <p>Chọn bệnh nhân đã check-in để ghi kết quả lâm sàng của lần khám.</p>
    </header>

    <div class="columns">
      <section class="panel" aria-label="Lượt khám hôm nay">
        <div class="heading">
          <h2>Lượt khám hôm nay</h2>
          <button type="button" :disabled="busy || dirty" @click="loadQueue(page)">Làm mới</button>
        </div>
        <p v-if="loadingQueue" role="status">Đang tải lượt khám…</p>
        <p v-if="queueError" class="error" role="alert">{{ queueError }}</p>
        <p v-if="!loadingQueue && !queueError && !entries.length">Chưa có bệnh nhân check-in.</p>
        <div class="queue-list">
          <button v-for="entry in entries" :key="entry.queueEntryId" type="button"
            class="queue-option" :class="{ selected: selected?.queueEntryId === entry.queueEntryId }"
            :aria-pressed="selected?.queueEntryId === entry.queueEntryId"
            :disabled="busy || dirty || !entry.visitId || Boolean(queueError)" @click="selectEntry(entry)">
            <strong>#{{ entry.queueNumber }} · {{ entry.patientName }}</strong>
            <span>{{ entry.patientCode }} · {{ entry.statusText }}</span>
            <small>{{ entry.dentistName || 'Chưa phân công nha sĩ' }}</small>
          </button>
        </div>
        <nav class="pagination" aria-label="Phân trang lượt khám">
          <button type="button" :disabled="busy || dirty || page <= 1" @click="loadQueue(page - 1)">Trước</button>
          <span>Trang {{ page }} / {{ Math.max(totalPages, 1) }}</span>
          <button type="button" :disabled="busy || dirty || page >= totalPages" @click="loadQueue(page + 1)">Sau</button>
        </nav>
      </section>

      <section class="panel" aria-label="Hồ sơ khám lâm sàng">
        <p v-if="!selected">Chọn một lượt khám ở danh sách bên cạnh.</p>
        <template v-else>
          <h2>{{ selected.patientName }} · {{ selected.patientCode }}</h2>
          <p>Ngày tiếp đón: {{ selected.queueDate }} · Lần khám #{{ selected.visitId }}</p>
          <p v-if="loadingVisit" role="status">Đang tải hồ sơ khám…</p>
          <p v-if="visitError" class="error" role="alert">{{ visitError }}</p>
          <button type="button" :disabled="busy" @click="reloadVisit">
            {{ dirty ? 'Bỏ thay đổi và tải lại' : 'Tải lại hồ sơ' }}
          </button>
          <template v-if="visit">
            <p>Trạng thái: {{ visitStatusLabel }}</p>
            <button v-if="canStart" type="button" :disabled="busy" @click="startConsultation">
              {{ starting ? 'Đang bắt đầu…' : 'Bắt đầu khám' }}
            </button>
            <p v-if="visit.status === 'Created'" class="hint">Nha sĩ cần bắt đầu khám trước khi ghi chẩn đoán.</p>
            <form v-if="canEdit" @submit.prevent="saveDiagnosis">
              <label for="diagnosis">Chẩn đoán <span>(bắt buộc)</span></label>
              <textarea id="diagnosis" v-model="diagnosis" :disabled="busy" required maxlength="1000" rows="4" />
              <small>{{ diagnosis.length }}/1000 ký tự</small>
              <label for="clinical-notes">Ghi chú lâm sàng</label>
              <textarea id="clinical-notes" v-model="clinicalNotes" :disabled="busy" maxlength="2000" rows="6" />
              <small>{{ clinicalNotes.length }}/2000 ký tự</small>
              <p v-if="dirty" class="hint">Có thay đổi chưa lưu. Lưu hoặc bỏ thay đổi trước khi chọn lượt khám khác.</p>
              <button type="submit" :disabled="busy">{{ saving ? 'Đang lưu…' : 'Lưu chẩn đoán' }}</button>
            </form>
            <div v-else>
              <p class="hint">Chỉ Admin hoặc nha sĩ phụ trách được sửa khi lần khám đang diễn ra.</p>
              <h3>Chẩn đoán</h3><p class="record">{{ visit.diagnosis || 'Chưa ghi nhận' }}</p>
              <h3>Ghi chú lâm sàng</h3><p class="record">{{ visit.clinicalNotes || 'Chưa ghi nhận' }}</p>
            </div>
          </template>
          <p v-if="actionError" class="error" role="alert">{{ actionError }}</p>
          <p v-if="successMessage" class="success" role="status">{{ successMessage }}</p>
        </template>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { queueService, type QueueEntryResponse } from '@/services/queue';
import { visitsService } from '@/services/visits';
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
const queueError = ref('');
const visitError = ref('');
const actionError = ref('');
const successMessage = ref('');
const busy = computed(() => loadingQueue.value || loadingVisit.value || starting.value || saving.value);
const canEdit = computed(() => visit.value?.status === 'InProgress'
  && (auth.role === 'ADMIN' || (auth.role === 'DENTIST' && visit.value.dentistId === auth.user?.userId)));
const canStart = computed(() => auth.role === 'DENTIST' && visit.value?.status === 'Created'
  && selected.value?.status === 1 && (selected.value.dentistId === null || selected.value.dentistId === auth.user?.userId));
const dirty = computed(() => canEdit.value && Boolean(visit.value)
  && (diagnosis.value !== (visit.value?.diagnosis ?? '') || clinicalNotes.value !== (visit.value?.clinicalNotes ?? '')));
const visitStatusLabel = computed(() => ({ Created: 'Chưa bắt đầu', InProgress: 'Đang khám', Completed: 'Đã kết thúc', Cancelled: 'Đã hủy' }[visit.value?.status ?? ''] ?? visit.value?.status));

function errorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.errors?.[0]?.message ?? error.response?.data?.message ?? fallback;
  }
  return fallback;
}

async function loadQueue(targetPage = 1) {
  if (busy.value || dirty.value) return;
  loadingQueue.value = true;
  queueError.value = '';
  try {
    const result = await queueService.getToday(targetPage);
    entries.value = result.items;
    page.value = result.page;
    totalPages.value = result.totalPages;
    selected.value = null;
    visit.value = null;
    actionError.value = '';
    successMessage.value = '';
  } catch (error) {
    queueError.value = errorMessage(error, 'Không tải được lượt khám. Vui lòng thử lại.');
  } finally { loadingQueue.value = false; }
}

async function selectEntry(entry: QueueEntryResponse) {
  if (busy.value || dirty.value) return;
  selected.value = entry;
  await reloadVisit();
}

async function reloadVisit() {
  if (!selected.value?.visitId) return;
  loadingVisit.value = true;
  visitError.value = '';
  actionError.value = '';
  successMessage.value = '';
  visit.value = null;
  try {
    const result = await visitsService.getById(selected.value.visitId);
    visit.value = result;
    diagnosis.value = result.diagnosis ?? '';
    clinicalNotes.value = result.clinicalNotes ?? '';
  } catch (error) {
    visitError.value = errorMessage(error, 'Không tải được hồ sơ khám. Vui lòng thử lại.');
  } finally { loadingVisit.value = false; }
}

async function startConsultation() {
  if (busy.value || !canStart.value || !selected.value || !auth.user) return;
  starting.value = true;
  actionError.value = '';
  try {
    const updated = await queueService.startConsultation(selected.value.queueEntryId, auth.user.userId);
    entries.value = entries.value.map(entry => entry.queueEntryId === updated.queueEntryId ? updated : entry);
    selected.value = updated;
    await reloadVisit();
  } catch (error) {
    actionError.value = errorMessage(error, 'Không bắt đầu được phiên khám. Tải lại hồ sơ để kiểm tra trạng thái.');
  } finally { starting.value = false; }
}

async function saveDiagnosis() {
  if (busy.value || !canEdit.value || !visit.value) return;
  actionError.value = '';
  successMessage.value = '';
  if (!diagnosis.value.trim() || diagnosis.value.length > 1000 || clinicalNotes.value.length > 2000) {
    actionError.value = 'Nhập chẩn đoán (tối đa 1000 ký tự), ghi chú tối đa 2000 ký tự.';
    return;
  }
  saving.value = true;
  try {
    const result = await visitsService.updateDiagnosis(visit.value.visitId, {
      diagnosis: diagnosis.value.trim(), clinicalNotes: clinicalNotes.value.trim() || null
    });
    visit.value = result;
    diagnosis.value = result.diagnosis ?? '';
    clinicalNotes.value = result.clinicalNotes ?? '';
    successMessage.value = 'Đã lưu chẩn đoán và ghi chú lâm sàng. Có thể tải lại hồ sơ để xem kết quả đã lưu.';
  } catch (error) {
    actionError.value = errorMessage(error, 'Không lưu được chẩn đoán. Kiểm tra lại quyền và trạng thái lần khám.');
  } finally { saving.value = false; }
}

onMounted(() => { void loadQueue(); });
</script>

<style scoped>
.clinical-page { max-width: 1100px; margin: 0 auto; padding: 1.5rem; color: #243447; }
header { margin-bottom: 1.5rem; }
h1 { margin-bottom: .5rem; } h2 { font-size: 1.2rem; }
.columns { display: grid; grid-template-columns: minmax(260px, 1fr) minmax(350px, 2fr); gap: 1rem; }
.panel { padding: 1.25rem; border: 1px solid #dce5eb; background: white; border-radius: 10px; }
.heading, .pagination { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.queue-list, form { display: grid; gap: .6rem; }
button { padding: .65rem .85rem; border: 1px solid #cbd5df; border-radius: 6px; background: #f2f6f9; color: #243447; font: inherit; cursor: pointer; }
button:disabled { opacity: .55; cursor: default; }
.queue-option { display: grid; gap: .3rem; text-align: left; background: white; }
.queue-option.selected { background: #edf8f1; border-color: #13795b; }
.pagination { margin-top: 1rem; font-size: .85rem; }
label { margin-top: .5rem; font-weight: 600; } label span, small, .hint { color: #607080; }
textarea { width: 100%; box-sizing: border-box; padding: .7rem; border: 1px solid #cbd5df; border-radius: 6px; font: inherit; resize: vertical; }
form button { margin-top: .5rem; color: white; background: #13795b; }
.record { white-space: pre-wrap; overflow-wrap: anywhere; }
.error { color: #a32323; } .success { color: #176b45; }
@media (max-width: 750px) { .columns { grid-template-columns: 1fr; } }
</style>
