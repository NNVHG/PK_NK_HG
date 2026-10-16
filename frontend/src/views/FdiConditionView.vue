<template>
  <main class="fdi-page">
    <RouterLink to="/clinical/diagnosis">← Khám và chẩn đoán</RouterLink>
    <p v-if="visit?.status === 'Correcting'">Đang đính chính hồ sơ ngoài ca khám. <RouterLink :to="`/clinical/visits/${visitId}/invoice-draft`">Trở lại để hoàn tất và khóa đính chính</RouterLink></p>
    <h1>Tình trạng răng · Lần khám #{{ visitId }}</h1>
    <RouterLink v-if="!busy" :to="`/clinical/visits/${visitId}/services`">Chỉ định dịch vụ theo răng</RouterLink>
    <button :disabled="busy" @click="load">Tải lại hồ sơ</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <template v-if="visit">
      <p>Trạng thái lần khám: {{ visit.status }}</p>
      <p v-if="!canEdit">Chỉ Admin hoặc nha sĩ phụ trách được ghi tình trạng khi lần khám đang diễn ra và chưa khóa.</p>
      <p>Chọn răng trên sơ đồ để tra cứu lịch sử qua các lần khám.</p>
      <FdiDentalChart v-model="selection" v-model:dentition-type="dentition" :latest-conditions="latestConditions" :multiple="false" :readonly="busy" @select="openHistory" @update:model-value="clearSuggestion" />
      <ToothHistoryDrawer ref="history" :patient-id="visit.patientId" />
      <form v-if="canEdit" @submit.prevent="save">
        <p v-if="selection[0]">Răng {{ selection[0].toothNumber }} · {{ selection[0].surface ? SURFACE_METADATA[selection[0].surface].nameVi : 'Toàn răng' }}</p>
        <p v-else>Chọn một răng hoặc mặt răng trên sơ đồ.</p>
        <label>Tình trạng
          <select v-model="code" :disabled="busy" required @change="clearSuggestion">
            <option v-for="(label, value) in FDI_CONDITIONS" :key="value" :value="value">{{ label }}</option>
          </select>
        </label>
        <label>Ghi chú lâm sàng theo răng (không bắt buộc)
          <textarea v-model="note" :disabled="busy" maxlength="500" rows="3" placeholder="Ghi nhận chuyên môn cho răng/mặt răng đã chọn" />
          <small>{{ note.length }}/500 ký tự · Lưu cùng bản ghi tình trạng mới, giữ lịch sử cũ.</small>
        </label>
        <button type="submit" :disabled="busy || !selection.length">{{ editingId ? 'Lưu chỉnh sửa tình trạng' : 'Ghi nhận tình trạng sau điều trị' }}</button>
        <button v-if="editingId" type="button" :disabled="busy" @click="cancelEditing">Hủy chỉnh sửa</button>
      </form>
      <section v-if="canEdit" aria-labelledby="suggested-service-title">
        <h2 id="suggested-service-title">Gợi ý dịch vụ theo tình trạng răng</h2>
        <button type="button" :disabled="busy || !selection.length" @click="loadSuggestion">Tải gợi ý dịch vụ</button>
        <p v-if="suggestionMessage" role="status">{{ suggestionMessage }}</p>
        <button v-if="suggestedService" type="button" :disabled="busy || !selection.length" @click="assignSuggestion">
          Chỉ định {{ suggestedService.label }} cho răng {{ selection[0]?.toothNumber }}
          <template v-if="selection[0]?.surface"> · {{ SURFACE_METADATA[selection[0].surface].nameVi }}</template>
        </button>
        <small>Chỉ định dịch vụ không xác nhận thủ thuật đã thực hiện.</small>
      </section>
      <h2>Tình trạng đã ghi trong lần khám</h2>
      <p v-if="!rows.length">Chưa ghi nhận tình trạng răng.</p>
      <ul>
        <li v-for="row in rows" :key="row.id">
          <strong>Răng {{ row.toothNumber }} · {{ row.surface || 'Toàn răng' }}</strong>
          — {{ FDI_CONDITIONS[row.conditionCode] || row.conditionCode }}
          <p v-if="row.note" class="clinical-note">Ghi chú: {{ row.note }}</p>
          <small>{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>
          <button type="button" :disabled="busy" @click="locate(row)">Xem trên sơ đồ</button>
          <button type="button" :disabled="busy" @click="openHistory({ toothNumber: row.toothNumber })">Lịch sử răng</button>
          <template v-if="canEdit">
            <button type="button" :disabled="busy" @click="edit(row)">Sửa bản ghi</button>
            <button v-if="pendingDeleteId !== row.id" type="button" :disabled="busy" @click="pendingDeleteId = row.id">Xóa bản ghi</button>
            <span v-else role="group" :aria-label="`Xác nhận xóa tình trạng răng ${row.toothNumber}`">
              <span>Chỉ xóa bản ghi trong lần khám đang mở này.</span>
              <button type="button" :disabled="busy" @click="remove(row)">Xác nhận xóa</button>
              <button type="button" :disabled="busy" @click="pendingDeleteId = null">Giữ lại</button>
            </span>
          </template>
        </li>
      </ul>
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
import { FDI_CONDITIONS, fdiService, type ConditionCode, type ToothCondition } from '@/services/fdi';
import { useFdiStore } from '@/stores/fdi';
import { visitsService } from '@/services/visits';
import type { VisitDetails } from '@/services/patients';
import { SURFACE_METADATA, ALL_SURFACES, type SelectedToothSurface, type DentitionType } from '@/types/fdi';
import { useAuthStore } from '@/stores/auth';
import { visitServicesApi, type CatalogOption } from '@/services/visitServices';

const route = useRoute();
const auth = useAuthStore();
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
.fdi-page { max-width: 1100px; margin: 2rem auto; padding: 1rem; }
form { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; margin: 1rem 0; }
select, button { padding: .6rem; }
li { margin: .8rem 0; }
small { display: block; color: #64748b; }
.error { color: #b91c1c; }
.clinical-note { white-space: pre-wrap; overflow-wrap: anywhere; }
textarea { display: block; width: min(450px, 75vw); padding: .6rem; font: inherit; }
</style>
