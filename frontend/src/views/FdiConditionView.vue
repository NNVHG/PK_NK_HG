<template>
  <main class="fdi-page">
    <RouterLink to="/clinical/diagnosis">← Khám và chẩn đoán</RouterLink>
    <h1>Tình trạng răng · Lần khám #{{ visitId }}</h1>
    <button :disabled="busy" @click="load">Tải lại hồ sơ</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <template v-if="visit">
      <p>Trạng thái lần khám: {{ visit.status }}</p>
      <p v-if="!canEdit">Chỉ Admin hoặc nha sĩ phụ trách được ghi tình trạng khi lần khám đang diễn ra và chưa khóa.</p>
      <FdiDentalChart v-model="selection" v-model:dentition-type="dentition" :multiple="false" :readonly="busy || !canEdit" />
      <form v-if="canEdit" @submit.prevent="save">
        <p v-if="selection[0]">Răng {{ selection[0].toothNumber }} · {{ selection[0].surface ? SURFACE_METADATA[selection[0].surface].nameVi : 'Toàn răng' }}</p>
        <p v-else>Chọn một răng hoặc mặt răng trên sơ đồ.</p>
        <label>Tình trạng
          <select v-model="code" :disabled="busy" required>
            <option v-for="(label, value) in FDI_CONDITIONS" :key="value" :value="value">{{ label }}</option>
          </select>
        </label>
        <button type="submit" :disabled="busy || !selection.length">Ghi nhận tình trạng</button>
      </form>
      <h2>Tình trạng đã ghi trong lần khám</h2>
      <p v-if="!rows.length">Chưa ghi nhận tình trạng răng.</p>
      <ul>
        <li v-for="row in rows" :key="row.id">
          <strong>Răng {{ row.toothNumber }} · {{ row.surface || 'Toàn răng' }}</strong>
          — {{ FDI_CONDITIONS[row.conditionCode] || row.conditionCode }}
          <small>{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>
          <button type="button" :disabled="busy" @click="locate(row)">Xem trên sơ đồ</button>
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
import { FDI_CONDITIONS, fdiService, type ConditionCode, type ToothCondition } from '@/services/fdi';
import { useFdiStore } from '@/stores/fdi';
import { visitsService } from '@/services/visits';
import type { VisitDetails } from '@/services/patients';
import { SURFACE_METADATA, ALL_SURFACES, type SelectedToothSurface, type DentitionType } from '@/types/fdi';
import { useAuthStore } from '@/stores/auth';

const route = useRoute();
const auth = useAuthStore();
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const fdiStore = useFdiStore();
const { rows } = storeToRefs(fdiStore);
const selection = ref<SelectedToothSurface[]>([]);
const dentition = ref<DentitionType>('adult');
const code = ref<ConditionCode>('CARIES');
const busy = ref(false);
const error = ref('');
const success = ref('');
const canEdit = computed(() => visit.value?.status === 'InProgress' && !visit.value?.isLocked &&
  (auth.role === 'ADMIN' || (auth.role === 'DENTIST' && visit.value.dentistId === auth.user?.userId)));

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
let loadVersion = 0;
async function load() {
  const version = ++loadVersion;
  busy.value = true; error.value = ''; success.value = ''; visit.value = null; fdiStore.reset(); selection.value = [];
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) {
    error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return;
  }
  try {
    const [details, conditions] = await Promise.all([visitsService.getById(visitId.value), fdiService.get(visitId.value)]);
    if (version !== loadVersion) return;
    visit.value = details; fdiStore.replace(conditions);
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
  const id = visitId.value;
  const version = loadVersion;
  busy.value = true; error.value = ''; success.value = '';
  try {
    const surface = selected.surface ? SURFACE_METADATA[selected.surface].code : null;
    const saved = await fdiService.add(id, selected.toothNumber, surface, code.value);
    if (id !== visitId.value || version !== loadVersion) return;
    fdiStore.prepend(saved); selection.value = [];
    success.value = 'Đã ghi nhận tình trạng răng. Có thể tải lại để xem dữ liệu đã lưu.';
  } catch (cause) { if (id === visitId.value && version === loadVersion) error.value = message(cause); }
  finally { if (id === visitId.value && version === loadVersion) busy.value = false; }
}
watch(visitId, load, { immediate: true });
onUnmounted(() => { loadVersion++; fdiStore.reset(); });
</script>

<style scoped>
.fdi-page { max-width: 1100px; margin: 2rem auto; padding: 1rem; }
form { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; margin: 1rem 0; }
select, button { padding: .6rem; }
li { margin: .8rem 0; }
small { display: block; color: #64748b; }
.error { color: #b91c1c; }
</style>
