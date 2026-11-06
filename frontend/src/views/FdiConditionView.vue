<template>
  <main class="fdi-page">
    <div class="breadcrumb-bar">
      <RouterLink v-if="auth.role !== 'PATIENT'" to="/management/clinical/diagnosis" class="back-link">← Khám và chẩn đoán</RouterLink>
      <span class="breadcrumb-current">Lâm sàng › Sơ đồ răng FDI</span>
    </div>

    <div v-if="visit?.status === 'Correcting'" class="correcting-alert">
      <span>Đang đính chính hồ sơ ngoài ca khám.</span>
      <RouterLink :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`">Trở lại để hoàn tất và khóa đính chính</RouterLink>
    </div>

    <div class="page-header-row">
      <div>
        <h1 class="page-title">Tình trạng răng · Lần khám #{{ visitId }}</h1>
        <p v-if="visit" class="page-subtitle">Bệnh nhân #{{ visit.patientId }} · Trạng thái: <strong>{{ visit.status }}</strong></p>
      </div>
      <div class="header-actions">
        <RouterLink v-if="!busy" :to="`${portalPrefix}/clinical/visits/${visitId}/services`" class="btn-secondary">Chỉ định dịch vụ theo răng →</RouterLink>
        <button type="button" class="btn-refresh" :disabled="busy" @click="load">Tải lại hồ sơ</button>
      </div>
    </div>

    <p v-if="busy" role="status" class="status-msg">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status" class="success-msg">{{ success }}</p>
    <template v-if="visit">
      <SafetyAlertBanner v-if="isStaff" :patient-id="visit.patientId" />

      <p v-if="!canEdit" class="readonly-notice">Chỉ Admin hoặc nha sĩ phụ trách được ghi tình trạng khi lần khám đang diễn ra và chưa khóa.</p>
      <p class="guide-text">Chọn răng trên sơ đồ để tra cứu lịch sử qua các lần khám.</p>
      <div class="chart-section-card">
        <FdiDentalChart v-model="selection" v-model:dentition-type="dentition" :latest-conditions="latestConditions" :multiple="false" :readonly="busy" @select="openHistory" @update:model-value="clearSuggestion" />
      </div>
      <ToothHistoryDrawer ref="history" :patient-id="visit.patientId">
        <template #actions="{toothNumber,allJaw}">
          <RouterLink v-if="toothNumber && ['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role)" :to="{name:auth.role === 'PATIENT' ? 'patient-portal-visit-images' : 'management-tooth-images',params:{visitId},query:{toothNumber:allJaw?undefined:toothNumber}}" class="drawer-action-link">{{ allJaw ? 'Xem ảnh hồ sơ bệnh nhân' : `Xem ảnh răng ${toothNumber}` }}</RouterLink>
        </template>
      </ToothHistoryDrawer>
      <div v-if="selection[0] && ['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role)" class="image-link-wrap">
        <RouterLink :to="{name:auth.role === 'PATIENT' ? 'patient-portal-visit-images' : 'management-tooth-images',params:{visitId},query:{toothNumber:selection[0].toothNumber}}" class="btn-tooth-images">Xem ảnh răng {{ selection[0].toothNumber }} qua các lần khám</RouterLink>
      </div>
      <div class="clinical-work-grid">
        <section v-if="canEdit" class="card form-card" aria-label="Ghi nhận tình trạng răng">
          <h2 class="card-title">{{ editingId ? 'Sửa tình trạng răng đã chọn' : 'Ghi nhận tình trạng mới' }}</h2>
          <p v-if="selection[0]" class="selected-target-badge">Răng {{ selection[0].toothNumber }} · {{ selection[0].surface ? SURFACE_METADATA[selection[0].surface].nameVi : 'Toàn răng' }}</p>
          <p v-else class="empty-target-hint">Chọn một răng hoặc mặt răng trên sơ đồ.</p>

          <form @submit.prevent="save" class="clinical-form">
            <div class="form-field">
              <label for="fdi-condition-code">Tình trạng (danh mục F_MST_02)</label>
              <select id="fdi-condition-code" v-model="code" :disabled="busy" required @change="clearSuggestion">
                <option v-for="(label, value) in FDI_CONDITIONS" :key="value" :value="value">{{ label }}</option>
              </select>
            </div>

            <div class="form-field">
              <label for="fdi-clinical-note">Ghi chú lâm sàng theo răng (không bắt buộc)</label>
              <textarea id="fdi-clinical-note" v-model="note" :disabled="busy" maxlength="500" rows="3" placeholder="Ghi nhận chuyên môn cho răng/mặt răng đã chọn" />
              <small>{{ note.length }}/500 ký tự · Lưu cùng bản ghi tình trạng mới, giữ lịch sử cũ.</small>
            </div>

            <div class="form-actions">
              <button type="submit" class="btn-primary" :disabled="busy || !selection.length">{{ editingId ? 'Lưu chỉnh sửa tình trạng' : 'Ghi nhận tình trạng sau điều trị' }}</button>
              <button v-if="editingId" type="button" class="btn-cancel" :disabled="busy" @click="cancelEditing">Hủy chỉnh sửa</button>
            </div>
          </form>

          <div class="suggestion-block" aria-labelledby="suggested-service-title">
            <h3 id="suggested-service-title" class="suggestion-title">Gợi ý dịch vụ theo tình trạng răng</h3>
            <button type="button" class="btn-secondary" :disabled="busy || !selection.length" @click="loadSuggestion">Tải gợi ý dịch vụ</button>
            <p v-if="suggestionMessage" role="status" class="suggestion-status">{{ suggestionMessage }}</p>
            <button v-if="suggestedService" type="button" class="btn-assign-suggest" :disabled="busy || !selection.length" @click="assignSuggestion">
              Chỉ định {{ suggestedService.label }} cho răng {{ selection[0]?.toothNumber }}
              <template v-if="selection[0]?.surface"> · {{ SURFACE_METADATA[selection[0].surface].nameVi }}</template>
            </button>
            <small class="disclaimer-note">Chỉ định dịch vụ không xác nhận thủ thuật đã thực hiện.</small>
          </div>
        </section>

        <section class="card history-card" aria-label="Tình trạng đã ghi trong lần khám">
          <h2 class="card-title">Tình trạng đã ghi trong lần khám</h2>
          <p v-if="!rows.length" class="empty-rows-hint">Chưa ghi nhận tình trạng răng.</p>
          <ul v-else class="condition-row-list">
            <li v-for="row in rows" :key="row.id" class="condition-row-item">
              <div class="row-main">
                <strong class="row-target">Răng {{ row.toothNumber }} · {{ row.surface || 'Toàn răng' }}</strong>
                <span class="row-condition-label">— {{ FDI_CONDITIONS[row.conditionCode] || row.conditionCode }}</span>
              </div>
              <p v-if="row.note" class="clinical-note">Ghi chú: {{ row.note }}</p>
              <small class="row-time">{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>
              <div class="row-actions">
                <button type="button" :disabled="busy" @click="locate(row)">Xem trên sơ đồ</button>
                <button type="button" :disabled="busy" @click="openHistory({ toothNumber: row.toothNumber })">Lịch sử răng</button>
                <template v-if="canEdit">
                  <button type="button" :disabled="busy" @click="edit(row)">Sửa bản ghi</button>
                  <button v-if="pendingDeleteId !== row.id" type="button" class="btn-danger-text" :disabled="busy" @click="pendingDeleteId = row.id">Xóa bản ghi</button>
                  <span v-else class="delete-confirm-box" role="group" :aria-label="`Xác nhận xóa tình trạng răng ${row.toothNumber}`">
                    <span>Chỉ xóa bản ghi trong lần khám đang mở này.</span>
                    <button type="button" class="btn-danger" :disabled="busy" @click="remove(row)">Xác nhận xóa</button>
                    <button type="button" :disabled="busy" @click="pendingDeleteId = null">Giữ lại</button>
                  </span>
                </template>
              </div>
            </li>
          </ul>
        </section>
      </div>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useRoute } from 'vue-router';
import axios from 'axios';
import FdiDentalChart from '@/components/fdi/FdiDentalChart.vue';
import ToothHistoryDrawer from '@/components/fdi/ToothHistoryDrawer.vue';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';
import { FDI_CONDITIONS, fdiService, type ConditionCode, type ToothCondition } from '@/services/fdi';
import { useFdiStore } from '@/stores/fdi';
import { visitsService } from '@/services/visits';
import type { VisitDetails } from '@/services/patients';
import { SURFACE_METADATA, ALL_SURFACES, type SelectedToothSurface, type DentitionType } from '@/types/fdi';
import { useAuthStore } from '@/stores/auth';
import { visitServicesApi, type CatalogOption } from '@/services/visitServices';

const route = useRoute();
const auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
const isStaff = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'].includes(auth.role));
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const fdiStore = useFdiStore();
const { rows } = storeToRefs(fdiStore);
const selection = ref<SelectedToothSurface[]>([]);
const latestConditions = ref<Awaited<ReturnType<typeof fdiService.latestForPatient>>>([]);
const dentition = ref<DentitionType>('adult');
const code = ref<ConditionCode>('CARIES');
const note = ref('');
const editingId = ref<number | null>(null);
const pendingDeleteId = ref<number | null>(null);
const busy = ref(false);
const error = ref('');
const success = ref('');
const history = ref<InstanceType<typeof ToothHistoryDrawer> | null>(null);
const suggestedService = ref<(CatalogOption & { label: string }) | null>(null);
const suggestionMessage = ref('');
function openHistory(selected: { toothNumber: number }) { if (!busy.value) void history.value?.open(selected.toothNumber); }
function clearSuggestion() { suggestedService.value = null; suggestionMessage.value = ''; }
const canEdit = computed(() => ['InProgress', 'Correcting'].includes(visit.value?.status ?? '') && !visit.value?.isLocked &&
  (auth.role === 'ADMIN' || (auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId)));

function message(cause: unknown) {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể tải hoặc lưu tình trạng răng. Vui lòng thử lại.' : 'Không thể xử lý yêu cầu.';
}
function locate(row: ToothCondition) {
  dentition.value = row.toothNumber >= 51 ? 'child' : 'adult';
  selection.value = row.surface
    ? ALL_SURFACES.filter(surface => row.surface!.includes(SURFACE_METADATA[surface].code))
      .map(surface => ({ toothNumber: row.toothNumber, surface }))
    : [{ toothNumber: row.toothNumber }];
}
function edit(row: ToothCondition) {
  if (!canEdit.value || busy.value) return;
  editingId.value = row.id;
  pendingDeleteId.value = null;
  locate(row);
  code.value = row.conditionCode;
  note.value = row.note || '';
}
function cancelEditing() {
  editingId.value = null;
  selection.value = [];
  note.value = '';
  code.value = 'CARIES';
}
async function loadSuggestion() {
  if (!canEdit.value || busy.value || !selection.value[0]) return;
  busy.value = true; error.value = ''; suggestedService.value = null; suggestionMessage.value = '';
  try {
    const suggestions = await fdiService.serviceSuggestions(code.value);
    if (!suggestions.length) { suggestionMessage.value = 'Tình trạng này chưa có dịch vụ gợi ý.'; return; }
    const candidate = suggestions[0];
    const service = await visitServicesApi.findActiveByCode(candidate.serviceCode);
    if (!service) {
      suggestionMessage.value = `Danh mục chưa có dịch vụ đang hoạt động có mã ${candidate.serviceCode}.`;
      return;
    }
    suggestedService.value = { ...service, label: candidate.label };
  } catch (cause) { error.value = message(cause); }
  finally { busy.value = false; }
}
async function assignSuggestion() {
  const selected = selection.value[0];
  const service = suggestedService.value;
  if (!canEdit.value || busy.value || !selected || !service) return;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await visitServicesApi.assign(visitId.value, {
      serviceId: service.dentalServiceId, toothNumbers: [selected.toothNumber],
      surface: selected.surface ? SURFACE_METADATA[selected.surface].code : null, quantity: 1
    });
    success.value = `Đã chỉ định ${service.name} cho răng ${selected.toothNumber}. Đây là chỉ định, không xác nhận thủ thuật đã thực hiện.`;
    suggestionMessage.value = '';
  } catch (cause) { error.value = message(cause); }
  finally { busy.value = false; }
}
let loadVersion = 0;
async function refreshLatestConditions(patientId: number, version: number, visitKey: number) {
  try {
    const latest = await fdiService.latestForPatient(patientId);
    if (version === loadVersion && visitKey === visitId.value) latestConditions.value = latest;
    return true;
  } catch {
    if (version === loadVersion && visitKey === visitId.value) latestConditions.value = [];
    return false;
  }
}
async function load() {
  const version = ++loadVersion;
  busy.value = true; error.value = ''; success.value = ''; visit.value = null; fdiStore.reset(); selection.value = [];
  note.value = ''; editingId.value = null; pendingDeleteId.value = null; latestConditions.value = [];
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) {
    error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return;
  }
  try {
    const [details, conditions] = await Promise.all([visitsService.getById(visitId.value), fdiService.get(visitId.value)]);
    if (version !== loadVersion) return;
    visit.value = details; fdiStore.replace(conditions);
    if (!(await refreshLatestConditions(details.patientId, version, visitId.value)) && version === loadVersion)
      error.value = 'Không tải được trạng thái răng mới nhất để tô màu sơ đồ.';
  } catch (cause) { if (version === loadVersion) error.value = message(cause); }
  finally { if (version === loadVersion) busy.value = false; }
}
async function save() {
  const selected = selection.value[0];
  if (!selected || !canEdit.value || busy.value) return;
  const quadrant = Math.floor(selected.toothNumber / 10);
  const tooth = selected.toothNumber % 10;
  const validTooth = quadrant >= 1 && quadrant <= 4 && tooth >= 1 && tooth <= 8 ||
    quadrant >= 5 && quadrant <= 8 && tooth >= 1 && tooth <= 5;
  if (!validTooth || (selected.surface && !(selected.surface in SURFACE_METADATA))) {
    error.value = 'Số răng hoặc mặt răng không hợp lệ.'; return;
  }
  if (!(code.value in FDI_CONDITIONS)) { error.value = 'Tình trạng không hợp lệ.'; return; }
  if (note.value.length > 500) { error.value = 'Ghi chú theo răng tối đa 500 ký tự.'; return; }
  const id = visitId.value;
  const version = loadVersion;
  busy.value = true; error.value = ''; success.value = '';
  try {
    const surface = selected.surface ? SURFACE_METADATA[selected.surface].code : null;
    const saved = editingId.value
      ? await fdiService.update(id, editingId.value, selected.toothNumber, surface, code.value, note.value.trim() || null)
      : await fdiService.add(id, selected.toothNumber, surface, code.value, note.value.trim() || null);
    if (id !== visitId.value || version !== loadVersion) return;
    if (editingId.value) fdiStore.replace(rows.value.map(row => row.id === saved.id ? saved : row));
    else fdiStore.prepend(saved);
    editingId.value = null; selection.value = [];
    note.value = '';
    const patientId = visit.value?.patientId;
    const latestLoaded = patientId ? await refreshLatestConditions(patientId, version, id) : false;
    if (id !== visitId.value || version !== loadVersion) return;
    success.value = latestLoaded
      ? 'Đã lưu tình trạng răng và cập nhật màu trạng thái mới nhất trên sơ đồ.'
      : 'Đã lưu tình trạng răng nhưng không tải lại được màu mới nhất; hãy tải lại hồ sơ.';
  } catch (cause) { if (id === visitId.value && version === loadVersion) error.value = message(cause); }
  finally { if (id === visitId.value && version === loadVersion) busy.value = false; }
}
async function remove(row: ToothCondition) {
  if (!canEdit.value || busy.value || pendingDeleteId.value !== row.id) return;
  const id = visitId.value;
  const version = loadVersion;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await fdiService.delete(id, row.id);
    if (id !== visitId.value || version !== loadVersion) return;
    fdiStore.replace(rows.value.filter(item => item.id !== row.id));
    pendingDeleteId.value = null;
    if (editingId.value === row.id) cancelEditing();
    const latestLoaded = visit.value ? await refreshLatestConditions(visit.value.patientId, version, id) : false;
    if (id !== visitId.value || version !== loadVersion) return;
    success.value = latestLoaded
      ? 'Đã xóa bản ghi và cập nhật màu trạng thái mới nhất trên sơ đồ.'
      : 'Đã xóa bản ghi nhưng không tải lại được màu mới nhất; hãy tải lại hồ sơ.';
  } catch (cause) { if (id === visitId.value && version === loadVersion) error.value = message(cause); }
  finally { if (id === visitId.value && version === loadVersion) busy.value = false; }
}
watch(visitId, load, { immediate: true });
onUnmounted(() => { loadVersion++; fdiStore.reset(); latestConditions.value = []; });
</script>

<style scoped>
.fdi-page {
  max-width: 1200px;
  margin: 1.5rem auto;
  padding: 1rem;
  box-sizing: border-box;
  color: #18354c;
}

.breadcrumb-bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.85rem;
  margin-bottom: 0.5rem;
}

.back-link {
  color: #1857b6;
  text-decoration: none;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  min-height: 44px;
}
.back-link:hover { text-decoration: underline; }

.breadcrumb-current {
  color: #64748b;
  font-weight: 500;
}

.correcting-alert {
  background: #fef3c7;
  border: 1px solid #fde68a;
  border-radius: 8px;
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  color: #92400e;
  font-size: 0.9rem;
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}
.correcting-alert a { color: #b45309; font-weight: 600; }

.page-header-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.page-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  color: #102e56;
}

.page-subtitle {
  margin: 0.35rem 0 0;
  font-size: 0.9rem;
  color: #64748b;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

button, select, input, textarea {
  font: inherit;
  box-sizing: border-box;
}

.btn-secondary, .btn-refresh {
  min-height: 44px;
  padding: 0.55rem 1rem;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.875rem;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

.btn-secondary {
  background: #d8ecfa;
  color: #102e56;
  border: 1px solid #a6d7ef;
}
.btn-secondary:hover { background: #bae6fd; }

.btn-refresh {
  background: #f4faff;
  color: #1857b6;
  border: 1px solid #cbd5e1;
}
.btn-refresh:hover:not(:disabled) { background: #d8ecfa; }
.btn-refresh:disabled { opacity: 0.55; cursor: not-allowed; }

.status-msg { color: #1857b6; font-weight: 500; }
.error { color: #b91c1c; font-weight: 600; background: #fef2f2; border: 1px solid #fecaca; padding: 0.65rem 1rem; border-radius: 8px; }
.success-msg { color: #15803d; font-weight: 600; background: #f0fdf4; border: 1px solid #bbf7d0; padding: 0.65rem 1rem; border-radius: 8px; }

.readonly-notice {
  background: #f8fafc;
  border-left: 4px solid #64748b;
  padding: 0.65rem 1rem;
  color: #475569;
  font-size: 0.9rem;
}

.guide-text {
  font-size: 0.9rem;
  color: #64748b;
  margin: 0.5rem 0;
}

.chart-section-card {
  margin: 1rem 0;
}

.drawer-action-link, .btn-tooth-images {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.875rem;
  text-decoration: none;
  background: #f4faff;
  color: #1857b6;
  border: 1px solid #d8ecfa;
}
.drawer-action-link:hover, .btn-tooth-images:hover { background: #d8ecfa; }

.image-link-wrap {
  margin: 0.75rem 0 1.25rem;
}

.clinical-work-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
  align-items: start;
  margin-top: 1.25rem;
}

@media (max-width: 900px) {
  .clinical-work-grid {
    grid-template-columns: 1fr;
  }
}

.card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
  box-shadow: 0 1px 3px rgba(16, 46, 86, 0.05);
  box-sizing: border-box;
}

.card-title {
  margin: 0 0 1rem;
  font-size: 1.1rem;
  font-weight: 700;
  color: #102e56;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 0.5rem;
}

.selected-target-badge {
  display: inline-block;
  background: #d8ecfa;
  color: #102e56;
  font-weight: 700;
  padding: 0.35rem 0.75rem;
  border-radius: 6px;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}

.empty-target-hint, .empty-rows-hint {
  color: #64748b;
  font-style: italic;
  font-size: 0.9rem;
}

.clinical-form {
  display: grid;
  gap: 1rem;
}

.form-field {
  display: grid;
  gap: 0.35rem;
}

.form-field label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #18354c;
}

.form-field select, .form-field textarea {
  width: 100%;
  padding: 0.65rem 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
  min-height: 44px;
}

.form-field textarea {
  min-height: 80px;
  resize: vertical;
}

.form-field small {
  font-size: 0.75rem;
  color: #64748b;
}

.form-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.btn-primary {
  min-height: 44px;
  padding: 0.65rem 1.25rem;
  background: #1857b6;
  color: #ffffff;
  border: 1px solid #102e56;
  border-radius: 8px;
  font-weight: 700;
  font-size: 0.9rem;
  cursor: pointer;
  transition: background 0.15s ease;
}
.btn-primary:hover:not(:disabled) { background: #102e56; }
.btn-primary:disabled { opacity: 0.55; cursor: not-allowed; }

.btn-cancel {
  min-height: 44px;
  padding: 0.65rem 1rem;
  background: #f8fafc;
  color: #475569;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
}
.btn-cancel:hover:not(:disabled) { background: #e2e8f0; }
.suggestion-block {
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid #f1f5f9;
  display: grid;
  gap: 0.65rem;
}

.suggestion-title {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 700;
  color: #102e56;
}

.suggestion-status {
  font-size: 0.85rem;
  color: #475569;
  margin: 0;
}

.btn-assign-suggest {
  min-height: 44px;
  padding: 0.6rem 1rem;
  background: #fef3c7;
  border: 1px solid #e5b95e;
  border-radius: 8px;
  color: #92400e;
  font-weight: 700;
  font-size: 0.875rem;
  cursor: pointer;
}
.btn-assign-suggest:hover:not(:disabled) { background: #fde68a; }

.disclaimer-note {
  font-size: 0.75rem;
  color: #64748b;
}

.condition-row-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.85rem;
}

.condition-row-item {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 0.9rem 1rem;
  background: #f8fafc;
  display: grid;
  gap: 0.4rem;
}

.row-main {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.row-target {
  color: #102e56;
  font-size: 0.95rem;
}

.row-condition-label {
  color: #1857b6;
  font-weight: 600;
}

.clinical-note {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  margin: 0;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  color: #334155;
}

.row-time {
  color: #64748b;
  font-size: 0.75rem;
}

.row-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-top: 0.35rem;
}

.row-actions button {
  min-height: 38px;
  padding: 0.45rem 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #18354c;
  cursor: pointer;
}
.row-actions button:hover:not(:disabled) { background: #f1f5f9; }
.row-actions button:disabled { opacity: 0.55; cursor: not-allowed; }

.btn-danger-text {
  color: #b91c1c !important;
  border-color: #fecaca !important;
}
.btn-danger-text:hover:not(:disabled) {
  background: #fef2f2 !important;
}

.delete-confirm-box {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  font-size: 0.8rem;
  color: #991b1b;
}

.btn-danger {
  background: #dc2626 !important;
  color: #ffffff !important;
  border-color: #b91c1c !important;
}
</style>
