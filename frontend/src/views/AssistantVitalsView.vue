<template>
  <section class="clinical-page">
    <div class="clinical-topbar">
      <div class="topbar-left">
        <RouterLink to="/management/queue/today" class="back-queue-link">← Hàng đợi hôm nay</RouterLink>
        <h1>Đo sinh hiệu &amp; Tiền khám</h1>
        <span v-if="selected" class="sync-pill">
          Nha sĩ khám: <strong>{{ selected.dentistName || 'Chưa phân công' }}</strong> · {{ selected.roomName || 'Chưa nhận phòng' }}
        </span>
      </div>
    </div>

    <div v-if="actionError" class="alert-box alert-error" role="alert">{{ actionError }}</div>
    <div v-if="visitError" class="alert-box alert-error" role="alert">{{ visitError }}</div>

    <!-- Khi mở trực tiếp từ thanh điều hướng chưa có query lượt khám -->
    <section v-if="!selected && !loadingVisit" class="panel empty-selection-placeholder">
      <template v-if="loadingCandidates">
        <p role="status">Đang tìm lượt khám đang mời hoặc đang khám…</p>
      </template>
      <template v-else-if="quickCandidates.length">
        <h2>Chọn bệnh nhân đang chờ đo sinh hiệu</h2>
        <p>Danh sách lượt đang mời vào phòng hoặc đang khám hôm nay để đồng bộ trực tiếp với màn hình của nha sĩ.</p>
        <div class="candidate-grid">
          <button
            v-for="item in quickCandidates"
            :key="item.queueEntryId"
            type="button"
            class="candidate-card"
            @click="selectCandidate(item)"
          >
            <div class="candidate-top">
              <span class="candidate-ticket">{{ numberLabel(item.queueNumber) }}</span>
              <span class="queue-status-tag" :data-status="item.status">{{ item.statusText }}</span>
            </div>
            <strong>{{ item.patientName }}</strong>
            <span class="candidate-meta">{{ item.patientCode }} · Nha sĩ: {{ item.dentistName || 'Chưa phân công' }}</span>
            <span class="candidate-meta">Phòng: {{ item.roomName || 'Chưa nhận phòng' }}</span>
          </button>
        </div>
        <RouterLink to="/management/queue/today" class="btn btn-primary go-queue-btn">← Quay lại Hàng đợi hôm nay</RouterLink>
      </template>
      <template v-else>
        <h2>Chưa chọn bệnh nhân nào để đo sinh hiệu</h2>
        <p>Vào <strong>Hàng đợi hôm nay</strong>, bấm <strong>Gọi số &amp; đo sinh hiệu</strong> hoặc chọn <strong>Đo sinh hiệu →</strong> trên lượt khám của bệnh nhân.</p>
        <RouterLink to="/management/queue/today" class="btn btn-primary go-queue-btn">← Mở Hàng đợi hôm nay</RouterLink>
      </template>
    </section>

    <section v-else-if="loadingVisit" class="panel detail-panel" role="status">
      <p class="loading-state-sm">Đang tải hồ sơ tiền khám…</p>
    </section>

    <section v-else-if="selected" class="panel detail-panel">
      <header class="patient-header">
        <div class="patient-identity">
          <h2>{{ patientDisplayName(patient?.fullName || selected.patientName) }}</h2>
          <div class="patient-submeta">
            <span>{{ selected.patientCode }}</span>
            <span v-if="patientAge !== null">{{ patientAge }} tuổi</span>
            <span v-if="isChildPatient" class="child-pill">👶 Trẻ &lt;14 tuổi</span>
            <span v-if="patient">{{ formatPatientGender(patient.gender) }}</span>
            <span>Lượt số {{ numberLabel(selected.queueNumber) }}</span>
            <span v-if="visit">Lần khám #{{ visit.visitId }}</span>
            <span>{{ selected.roomName || 'Chưa nhận phòng' }}</span>
          </div>
        </div>
        <div class="patient-header-right">
          <span class="queue-status-tag" :data-status="selected.status">{{ selected.statusText }}</span>
          <span v-if="visit" class="visit-status-tag">{{ visitStatusLabel }}</span>
          <button type="button" class="btn" :disabled="busy" @click="reloadAll">↻ Tải lại</button>
        </div>
      </header>

      <p v-if="visit" class="handoff-banner" role="note">
        Bệnh nhân này đang gắn cùng <strong>Lần khám #{{ visit.visitId }}</strong> với <strong>{{ selected.dentistName || 'nha sĩ phụ trách' }}</strong>.
        Chỉ số sinh hiệu và tiền sử bạn lưu tại đây sẽ hiển thị ngay trên màn hình Khám bệnh &amp; chẩn đoán lâm sàng của nha sĩ.
      </p>

      <SafetyAlertBanner :patient-id="selected.patientId" :refresh-key="alertRefreshKey" />

      <div v-if="successMessage" class="alert-box alert-success" role="status">✅ {{ successMessage }}</div>

      <div class="clinical-cockpit">
        <!-- Cột trái: Đo sinh hiệu & danh sách sinh hiệu của lần khám -->
        <div class="clinical-main-col">
          <div class="pre-exam-card">
            <div class="form-head">
              <h3>Sinh hiệu mới nhất trong lần khám</h3>
              <span v-if="hasHighBp" class="warn-badge">⚠️ Huyết áp cao cần lưu ý</span>
            </div>

            <div v-if="vitalSignsError" class="alert-box alert-error" role="alert">{{ vitalSignsError }}</div>
            <div v-else-if="latestVisitVital" class="vitals-grid" :class="{ 'vitals-grid--warn': hasHighBp }">
              <div class="vital-tile" :class="{ 'vital-tile--warn': hasHighBp }">
                <span class="vital-label">Huyết áp</span>
                <strong>{{ latestVisitVital.systolicBp ?? '—' }} / {{ latestVisitVital.diastolicBp ?? '—' }}</strong>
                <span class="vital-unit">mmHg</span>
              </div>
              <div class="vital-tile">
                <span class="vital-label">Mạch</span>
                <strong>{{ latestVisitVital.pulseBpm ?? '—' }}</strong>
                <span class="vital-unit">lần/phút</span>
              </div>
              <div class="vital-tile">
                <span class="vital-label">Thân nhiệt</span>
                <strong>{{ latestVisitVital.temperatureC ?? '—' }}</strong>
                <span class="vital-unit">°C</span>
              </div>
            </div>
            <p v-else class="muted-note">Chưa có sinh hiệu cho lần khám này.</p>
            <p class="pre-exam-hint">Chỉ số tham khảo lâm sàng; nha sĩ quyết định chỉ định điều trị sau khi tiếp nhận bệnh nhân.</p>
          </div>

          <form v-if="canWriteVisit" class="diagnosis-form" @submit.prevent="saveVitalSigns">
            <div class="form-head">
              <h3>Ghi nhận chỉ số sinh hiệu</h3>
              <span class="form-sub">Lần khám #{{ visit?.visitId }}</span>
            </div>

            <div class="vital-inputs-grid">
              <div class="form-group">
                <label for="vital-systolic">Huyết áp tâm thu (mmHg)</label>
                <input id="vital-systolic" v-model="vitalForm.systolicBp" type="number" min="50" max="260" step="1" placeholder="VD: 120" :disabled="savingVitalSigns" />
              </div>
              <div class="form-group">
                <label for="vital-diastolic">Huyết áp tâm trương (mmHg)</label>
                <input id="vital-diastolic" v-model="vitalForm.diastolicBp" type="number" min="30" max="160" step="1" placeholder="VD: 80" :disabled="savingVitalSigns" />
              </div>
              <div class="form-group">
                <label for="vital-pulse">Mạch (lần/phút)</label>
                <input id="vital-pulse" v-model="vitalForm.pulseBpm" type="number" min="20" max="220" step="1" placeholder="VD: 75" :disabled="savingVitalSigns" />
              </div>
              <div class="form-group">
                <label for="vital-temp">Thân nhiệt (°C)</label>
                <input id="vital-temp" v-model="vitalForm.temperatureC" type="number" min="30" max="43" step="0.1" placeholder="VD: 36.8" :disabled="savingVitalSigns" />
              </div>
            </div>

            <div class="form-group">
              <label for="vital-note">Ghi chú đo sinh hiệu</label>
              <textarea id="vital-note" v-model="vitalForm.note" rows="2" maxlength="2000" placeholder="Ghi nhận tình trạng mệt, lo âu, vừa vận động..." :disabled="savingVitalSigns" />
            </div>

            <div v-if="vitalFormError" class="alert-box alert-error" role="alert">{{ vitalFormError }}</div>

            <div class="diagnosis-footer">
              <button type="submit" class="btn btn-primary" :disabled="savingVitalSigns || loadingVisit">
                {{ savingVitalSigns ? 'Đang lưu sinh hiệu…' : '💾 Lưu sinh hiệu' }}
              </button>
              <button type="button" class="btn" :disabled="savingVitalSigns" @click="resetVitalForm">Xóa trắng</button>
            </div>
          </form>

          <div v-else class="read-only-diagnosis" role="status">
            <strong>Chế độ chỉ xem</strong>
            <p class="muted-note">Lần khám hiện đã kết thúc, bị khóa hoặc bạn không có quyền ghi mới sinh hiệu trên ca này.</p>
          </div>

          <div class="pre-exam-card">
            <div class="form-head">
              <h3>Các lần đo trong lần khám #{{ visit?.visitId ?? '—' }}</h3>
              <span class="form-sub">{{ visitVitals.length }} bản ghi</span>
            </div>
            <p v-if="!visitVitals.length" class="muted-note">Chưa có bản ghi sinh hiệu nào trong lần khám này.</p>
            <ul v-else class="vital-history-list">
              <li v-for="record in visitVitals" :key="record.vitalSignRecordId" class="vital-history-item">
                <div class="history-item-top">
                  <strong>{{ formatDateTime(record.createdAt) }}</strong>
                  <span>HA: {{ record.systolicBp ?? '—' }}/{{ record.diastolicBp ?? '—' }} mmHg · Mạch: {{ record.pulseBpm ?? '—' }} · {{ record.temperatureC ?? '—' }}°C</span>
                </div>
                <p v-if="record.note" class="history-item-note">{{ record.note }}</p>
              </li>
            </ul>
            <p v-if="otherVisitVitalsCount > 0" class="muted-note">
              Hồ sơ còn {{ otherVisitVitalsCount }} bản ghi sinh hiệu từ các lần khám trước (xem thêm ở Hồ sơ bệnh nhân).
            </p>
          </div>
        </div>

        <!-- Cột phải: Tiền sử & dị ứng + Lối tắt thao tác -->
        <aside class="clinical-side-col">
          <div class="side-card">
            <div class="form-head">
              <h3>Tiền sử bệnh &amp; dị ứng</h3>
              <button v-if="canWriteVisit && !medicalHistoryFormVisible" type="button" class="btn btn-sm" @click="openMedicalHistoryForm">
                Cập nhật tiền sử
              </button>
            </div>

            <div v-if="historyError" class="alert-box alert-error" role="alert">{{ historyError }}</div>

            <form v-if="medicalHistoryFormVisible && canWriteVisit" class="history-form" @submit.prevent="saveMedicalHistory">
              <p class="form-sub">Lưu bản chụp tiền sử mới cho Lần khám #{{ visit?.visitId }}.</p>
              <div v-for="(item, index) in medicalHistoryForm.items" :key="item.rowId" class="history-edit-row">
                <div class="history-row-fields">
                  <select v-model="item.type" :aria-label="`Loại mục ${index + 1}`" :disabled="savingMedicalHistory">
                    <option value="Allergy">Dị ứng</option>
                    <option value="Condition">Bệnh nền</option>
                  </select>
                  <input v-model="item.name" maxlength="200" placeholder="Tên dị ứng / bệnh nền *" required :disabled="savingMedicalHistory" />
                </div>
                <input v-model="item.detail" maxlength="1000" placeholder="Chi tiết mức độ, thuốc gây dị ứng..." :disabled="savingMedicalHistory" />
                <div class="history-row-footer">
                  <label class="critical-check">
                    <input v-model="item.isCritical" type="checkbox" :disabled="savingMedicalHistory" /> Quan trọng (cần cảnh báo)
                  </label>
                  <button type="button" class="link-danger" :disabled="savingMedicalHistory" @click="removeMedicalHistoryItem(index)">Xóa dòng</button>
                </div>
              </div>

              <button type="button" class="btn btn-sm" :disabled="savingMedicalHistory || medicalHistoryForm.items.length >= 50" @click="addMedicalHistoryItem">
                + Thêm mục
              </button>

              <div class="form-group">
                <label for="mh-note">Ghi chú tiền sử</label>
                <textarea id="mh-note" v-model="medicalHistoryForm.note" rows="2" maxlength="2000" :disabled="savingMedicalHistory" />
              </div>

              <div v-if="medicalHistoryFormError" class="alert-box alert-error" role="alert">{{ medicalHistoryFormError }}</div>

              <div class="diagnosis-actions">
                <button type="submit" class="btn btn-primary btn-sm" :disabled="savingMedicalHistory">
                  {{ savingMedicalHistory ? 'Đang lưu…' : 'Lưu tiền sử mới' }}
                </button>
                <button type="button" class="btn btn-sm" :disabled="savingMedicalHistory" @click="medicalHistoryFormVisible = false">Hủy</button>
              </div>
            </form>

            <template v-else-if="latestHistory">
              <p class="form-sub">Cập nhật lúc {{ formatDateTime(latestHistory.createdAt) }} · Lần khám #{{ latestHistory.visitId }}</p>
              <p v-if="latestHistory.note" class="record-val">{{ latestHistory.note }}</p>
              <ul v-if="latestHistory.items.length" class="mh-summary-list">
                <li v-for="item in latestHistory.items" :key="item.itemId" :class="{ 'mh-critical': item.isCritical }">
                  <div class="mh-top">
                    <span class="mh-kind">{{ item.type === 'Allergy' ? 'Dị ứng' : item.type === 'Condition' ? 'Bệnh nền' : item.type }}</span>
                    <strong>{{ item.name }}</strong>
                    <span v-if="item.isCritical" class="critical-pill">Cảnh báo</span>
                  </div>
                  <p v-if="item.detail">{{ item.detail }}</p>
                </li>
              </ul>
              <p v-else class="muted-note">Bản chụp mới nhất không ghi nhận mục bệnh nền/dị ứng.</p>
            </template>
            <p v-else class="muted-note">Chưa có tiền sử bệnh/dị ứng được ghi nhận.</p>
          </div>

          <div v-if="visit" class="side-card care-links">
            <h3>Hồ sơ &amp; X-quang của lần khám</h3>
            <div class="care-links-grid">
              <RouterLink
                class="care-tile"
                :to="{ name: 'management-patient-detail', params: { id: selected.patientId }, query: { tab: 'images', visitId: visit.visitId } }"
              >
                📷 Ảnh hồ sơ / X-quang
              </RouterLink>
              <RouterLink
                class="care-tile"
                :to="{ name: 'management-patient-detail', params: { id: selected.patientId }, query: { visitId: visit.visitId } }"
              >
                👤 Hồ sơ bệnh nhân
              </RouterLink>
            </div>
            <RouterLink to="/management/queue/today" class="care-finish-btn secondary">
              ← Quay lại Hàng đợi hôm nay
            </RouterLink>
          </div>
        </aside>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import axios from 'axios';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';
import { useAuthStore } from '@/stores/auth';
import { queueService, type QueueEntryResponse } from '@/services/queue';
import { visitsService } from '@/services/visits';
import {
  patientsService,
  type MedicalHistoryPayload,
  type MedicalHistoryRecord,
  type PatientDetails,
  type VisitDetails,
  type VitalSignRecord
} from '@/services/patients';
import { workspacesService } from '@/services/workspaces';
import { isUnderFourteen } from '@/services/patientAge';
import { formatPatientGender, patientDisplayName } from '@/utils/patientDisplay';

interface VitalFormState {
  systolicBp: string;
  diastolicBp: string;
  pulseBpm: string;
  temperatureC: string;
  note: string;
}

interface MedicalHistoryFormItem {
  rowId: number;
  type: 'Allergy' | 'Condition';
  name: string;
  detail: string;
  isCritical: boolean;
}

const route = useRoute();
const auth = useAuthStore();

const selected = ref<QueueEntryResponse | null>(null);
const visit = ref<VisitDetails | null>(null);
const patient = ref<PatientDetails | null>(null);
const vitalSigns = ref<VitalSignRecord[]>([]);
const latestHistory = ref<MedicalHistoryRecord | null>(null);
const quickCandidates = ref<QueueEntryResponse[]>([]);

const loadingVisit = ref(false);
const loadingCandidates = ref(false);
const savingVitalSigns = ref(false);
const savingMedicalHistory = ref(false);

const visitError = ref('');
const actionError = ref('');
const vitalSignsError = ref('');
const vitalFormError = ref('');
const historyError = ref('');
const medicalHistoryFormError = ref('');
const successMessage = ref('');
const alertRefreshKey = ref(0);

const vitalForm = ref<VitalFormState>({ systolicBp: '', diastolicBp: '', pulseBpm: '', temperatureC: '', note: '' });
const medicalHistoryFormVisible = ref(false);
const medicalHistoryForm = ref<{ items: MedicalHistoryFormItem[]; note: string }>({ items: [], note: '' });

let life = 0;
let loadVersion = 0;
let nextHistoryRowId = 0;

const busy = computed(() => loadingVisit.value || savingVitalSigns.value || savingMedicalHistory.value);
const canWriteVisit = computed(() =>
  ['ADMIN', 'ASSISTANT'].includes(auth.role) &&
  Boolean(visit.value) &&
  !visit.value!.isLocked &&
  ['Created', 'InProgress'].includes(visit.value!.status)
);
const visitVitals = computed(() => {
  const activeId = visit.value?.visitId;
  return activeId ? vitalSigns.value.filter(record => record.visitId === activeId) : [];
});
const latestVisitVital = computed(() => visitVitals.value[0] ?? null);
const otherVisitVitalsCount = computed(() => Math.max(0, vitalSigns.value.length - visitVitals.value.length));
const hasHighBp = computed(() => (latestVisitVital.value?.systolicBp ?? 0) >= 140 || (latestVisitVital.value?.diastolicBp ?? 0) >= 90);
const isChildPatient = computed(() => Boolean(patient.value?.dateOfBirth && isUnderFourteen(patient.value.dateOfBirth)));
const patientAge = computed(() => {
  const dob = patient.value?.dateOfBirth?.slice(0, 10);
  if (!dob || !/^\d{4}-\d{2}-\d{2}$/.test(dob)) return null;
  const today = new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
  const [by, bm, bd] = dob.split('-').map(Number);
  const [ty, tm, td] = today.split('-').map(Number);
  let age = ty - by;
  if (tm < bm || (tm === bm && td < bd)) age -= 1;
  return age >= 0 && age <= 130 ? age : null;
});
const visitStatusLabel = computed(() => (
  { Created: 'Chưa bắt đầu', InProgress: 'Đang khám', Completed: 'Đã kết thúc', Cancelled: 'Đã hủy', Correcting: 'Đang đính chính' }[visit.value?.status ?? '']
  ?? visit.value?.status
  ?? ''
));

function numberLabel(value: number) {
  return `A-${String(value).padStart(3, '0')}`;
}

function queryId(value: unknown): number | null {
  if (typeof value !== 'string' || !/^\d+$/.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 && parsed <= 2147483647 ? parsed : null;
}

function formatDateTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh'
  }).format(parsed);
}

function errorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.errors?.[0]?.message ?? error.response?.data?.message ?? fallback;
  }
  return fallback;
}

function resetVitalForm() {
  vitalForm.value = { systolicBp: '', diastolicBp: '', pulseBpm: '', temperatureC: '', note: '' };
  vitalFormError.value = '';
}

function parseOptionalNumber(value: string | number): number | null {
  if (typeof value === 'string' && !value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

function validateVitalForm(): string | null {
  if (!canWriteVisit.value || !visit.value) return 'Lần khám không còn mở để ghi sinh hiệu.';
  const systolic = parseOptionalNumber(vitalForm.value.systolicBp);
  const diastolic = parseOptionalNumber(vitalForm.value.diastolicBp);
  const pulse = parseOptionalNumber(vitalForm.value.pulseBpm);
  const temperature = parseOptionalNumber(vitalForm.value.temperatureC);
  if ([systolic, diastolic, pulse, temperature].some(v => v !== null && !Number.isFinite(v))) return 'Các chỉ số sinh hiệu phải là số hợp lệ.';
  if ([systolic, diastolic, pulse, temperature].every(v => v === null)) return 'Cần nhập ít nhất một chỉ số sinh hiệu.';
  if (systolic !== null && (!Number.isInteger(systolic) || systolic < 50 || systolic > 260)) return 'Huyết áp tâm thu phải là số nguyên từ 50 đến 260 mmHg.';
  if (diastolic !== null && (!Number.isInteger(diastolic) || diastolic < 30 || diastolic > 160)) return 'Huyết áp tâm trương phải là số nguyên từ 30 đến 160 mmHg.';
  if (systolic !== null && diastolic !== null && systolic <= diastolic) return 'Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.';
  if (pulse !== null && (!Number.isInteger(pulse) || pulse < 20 || pulse > 220)) return 'Mạch phải là số nguyên từ 20 đến 220 lần/phút.';
  if (temperature !== null && (temperature < 30 || temperature > 43)) return 'Thân nhiệt phải từ 30 đến 43 °C.';
  if (vitalForm.value.note.length > 2000) return 'Ghi chú không được vượt quá 2000 ký tự.';
  return null;
}

async function saveVitalSigns() {
  vitalFormError.value = '';
  successMessage.value = '';
  const validationError = validateVitalForm();
  if (validationError) {
    vitalFormError.value = validationError;
    return;
  }
  const activeVisitId = visit.value?.visitId;
  const activePatientId = selected.value?.patientId;
  if (!activeVisitId || !activePatientId) return;

  const ticket = life;
  savingVitalSigns.value = true;
  try {
    await patientsService.recordVitalSigns(activeVisitId, {
      systolicBp: parseOptionalNumber(vitalForm.value.systolicBp),
      diastolicBp: parseOptionalNumber(vitalForm.value.diastolicBp),
      pulseBpm: parseOptionalNumber(vitalForm.value.pulseBpm),
      temperatureC: parseOptionalNumber(vitalForm.value.temperatureC),
      note: vitalForm.value.note.trim() || null
    });
    if (ticket !== life) return;
    resetVitalForm();
    successMessage.value = 'Đã lưu chỉ số sinh hiệu và đồng bộ với hồ sơ khám của nha sĩ.';
    alertRefreshKey.value += 1;
    const updatedVitals = await patientsService.getVitalSigns(activePatientId, 1, 20).catch(() => null);
    if (ticket === life && updatedVitals) {
      vitalSigns.value = updatedVitals.items;
    }
  } catch (error: unknown) {
    if (ticket === life) vitalFormError.value = errorMessage(error, 'Không lưu được sinh hiệu. Vui lòng thử lại.');
  } finally {
    if (ticket === life) savingVitalSigns.value = false;
  }
}

function newMedicalHistoryItem(): MedicalHistoryFormItem {
  nextHistoryRowId += 1;
  return { rowId: nextHistoryRowId, type: 'Allergy', name: '', detail: '', isCritical: false };
}

function openMedicalHistoryForm() {
  if (!canWriteVisit.value) return;
  medicalHistoryForm.value = {
    items: latestHistory.value?.items.map(item => ({
      ...newMedicalHistoryItem(),
      type: item.type === 'Condition' ? 'Condition' : 'Allergy',
      name: item.name,
      detail: item.detail ?? '',
      isCritical: item.isCritical
    })) ?? [newMedicalHistoryItem()],
    note: latestHistory.value?.note ?? ''
  };
  medicalHistoryFormError.value = '';
  medicalHistoryFormVisible.value = true;
}

function addMedicalHistoryItem() {
  if (medicalHistoryForm.value.items.length < 50) {
    medicalHistoryForm.value.items.push(newMedicalHistoryItem());
  }
}

function removeMedicalHistoryItem(index: number) {
  medicalHistoryForm.value.items.splice(index, 1);
}

function validateMedicalHistoryForm(): string | null {
  if (!canWriteVisit.value || !visit.value) return 'Lần khám không còn mở để cập nhật tiền sử.';
  if (medicalHistoryForm.value.items.length > 50) return 'Mỗi bản chụp chỉ được có tối đa 50 mục.';
  if (medicalHistoryForm.value.note.length > 2000) return 'Ghi chú không được vượt quá 2000 ký tự.';
  const names = medicalHistoryForm.value.items.map(item => item.name.trim());
  if (names.some(name => !name)) return 'Tên mục tiền sử không được để trống.';
  if (names.some(name => name.length > 200)) return 'Tên mục tiền sử không được vượt quá 200 ký tự.';
  if (new Set(names.map(name => name.toLocaleLowerCase('vi-VN'))).size !== names.length) {
    return 'Tên mục không được trùng trong cùng một bản chụp.';
  }
  return null;
}

async function saveMedicalHistory() {
  medicalHistoryFormError.value = '';
  successMessage.value = '';
  const validationError = validateMedicalHistoryForm();
  if (validationError) {
    medicalHistoryFormError.value = validationError;
    return;
  }
  const activeVisitId = visit.value?.visitId;
  const activePatientId = selected.value?.patientId;
  if (!activeVisitId || !activePatientId) return;

  const payload: MedicalHistoryPayload = {
    note: medicalHistoryForm.value.note.trim() || null,
    items: medicalHistoryForm.value.items.map(item => ({
      type: item.type,
      name: item.name.trim(),
      isCritical: item.isCritical,
      detail: item.detail.trim() || null
    }))
  };

  const ticket = life;
  savingMedicalHistory.value = true;
  try {
    await patientsService.recordMedicalHistory(activeVisitId, payload);
    if (ticket !== life) return;
    medicalHistoryFormVisible.value = false;
    alertRefreshKey.value += 1;
    successMessage.value = 'Đã lưu bản chụp tiền sử & dị ứng mới.';
    const refreshed = await patientsService.getLatestMedicalHistory(activePatientId).catch(() => null);
    if (ticket === life) latestHistory.value = refreshed;
  } catch (error: unknown) {
    if (ticket === life) medicalHistoryFormError.value = errorMessage(error, 'Không lưu được tiền sử. Vui lòng thử lại.');
  } finally {
    if (ticket === life) savingMedicalHistory.value = false;
  }
}

async function loadEntryDetails(entry: QueueEntryResponse) {
  const ticket = ++loadVersion;
  loadingVisit.value = true;
  visitError.value = '';
  actionError.value = '';
  vitalSignsError.value = '';
  historyError.value = '';
  medicalHistoryFormVisible.value = false;
  try {
    if (!entry.visitId) throw new Error('Missing visitId');
    const [visitDetails, currentEntry, patientDetails, vitalsPage, latestMh] = await Promise.all([
      visitsService.getById(entry.visitId),
      queueService.getEntry(entry.queueEntryId),
      patientsService.getById(entry.patientId).catch(() => null),
      patientsService.getVitalSigns(entry.patientId, 1, 20).catch(() => null),
      patientsService.getLatestMedicalHistory(entry.patientId).catch((err: unknown) => {
        if (axios.isAxiosError(err) && err.response?.status === 404) return null;
        throw err;
      })
    ]);
    if (ticket !== loadVersion) return;
    if (currentEntry.visitId !== visitDetails.visitId || currentEntry.patientId !== visitDetails.patientId) {
      throw new Error('Visit mismatch');
    }
    selected.value = currentEntry;
    visit.value = visitDetails;
    patient.value = patientDetails;
    vitalSigns.value = vitalsPage?.items ?? [];
    if (!vitalsPage) vitalSignsError.value = 'Không tải được lịch sử sinh hiệu.';
    latestHistory.value = latestMh;
  } catch (error: unknown) {
    if (ticket !== loadVersion) return;
    visit.value = null;
    visitError.value = errorMessage(error, 'Không tải được hồ sơ tiền khám của lượt đã chọn.');
  } finally {
    if (ticket === loadVersion) loadingVisit.value = false;
  }
}

async function selectCandidate(entry: QueueEntryResponse) {
  successMessage.value = '';
  resetVitalForm();
  await loadEntryDetails(entry);
}

async function reloadAll() {
  if (!selected.value || busy.value) return;
  await loadEntryDetails(selected.value);
}

async function openInitialContext() {
  const ticket = ++loadVersion;
  const entryId = queryId(route.query.queueEntryId);
  const visitId = queryId(route.query.visitId);
  visitError.value = '';
  actionError.value = '';
  successMessage.value = '';
  selected.value = null;
  visit.value = null;
  patient.value = null;
  vitalSigns.value = [];
  latestHistory.value = null;
  quickCandidates.value = [];

  if (route.query.queueEntryId !== undefined || route.query.visitId !== undefined) {
    if (!entryId || !visitId) {
      actionError.value = 'Liên kết lượt khám không hợp lệ. Quay lại Hàng đợi hôm nay để chọn lại.';
      return;
    }
    loadingVisit.value = true;
    try {
      const linked = await queueService.getEntry(entryId);
      if (ticket !== loadVersion) return;
      const today = new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
      if (linked.visitId !== visitId || linked.queueDate !== today) {
        throw new Error('Invalid queue entry link');
      }
      await loadEntryDetails(linked);
    } catch {
      if (ticket === loadVersion) {
        loadingVisit.value = false;
        actionError.value = 'Không mở được lượt khám được liên kết. Quay lại Hàng đợi hôm nay để chọn lại.';
      }
    }
    return;
  }

  loadingCandidates.value = true;
  try {
    let dentistFilter: number | undefined;
    if (auth.role === 'ASSISTANT' && auth.user?.userId) {
      const ws = await workspacesService.get(auth.user.userId).catch(() => null);
      if (ws?.workingDentistId) dentistFilter = ws.workingDentistId;
    }
    const [calling, inProgress] = await Promise.all([
      queueService.getToday(1, 6, dentistFilter),
      queueService.getToday(1, 2, dentistFilter)
    ]);
    if (ticket !== loadVersion) return;

    const candidates = [...calling.items, ...inProgress.items].filter(x => Boolean(x.visitId));
    quickCandidates.value = candidates;
    if (calling.totalCount === 1 && calling.items[0]?.visitId) {
      await loadEntryDetails(calling.items[0]);
    } else if (calling.totalCount === 0 && inProgress.totalCount === 1 && inProgress.items[0]?.visitId) {
      await loadEntryDetails(inProgress.items[0]);
    }
  } catch {
    if (ticket === loadVersion) actionError.value = 'Không tải được danh sách lượt đang mời hôm nay.';
  } finally {
    if (ticket === loadVersion) loadingCandidates.value = false;
  }
}

onMounted(async () => { await openInitialContext(); });
watch(() => [route.query.queueEntryId, route.query.visitId], () => { void openInitialContext(); });
onUnmounted(() => { life++; loadVersion++; });
</script>

<style scoped>
.clinical-page {
  container-type: inline-size;
  max-width: 1320px;
  margin: 0 auto;
  color: #172b42;
}

.clinical-topbar { margin-bottom: 0.9rem; }
.topbar-left { display: flex; flex-wrap: wrap; align-items: center; gap: 0.65rem 1rem; }

.back-queue-link {
  display: inline-flex;
  align-items: center;
  min-height: 36px;
  padding: 0.3rem 0.75rem;
  border: 1px solid #c7d7e4;
  border-radius: 8px;
  background: #fff;
  color: #1e5273;
  font-size: 0.86rem;
  font-weight: 700;
  text-decoration: none;
}
.back-queue-link:hover { background: #f4fafb; border-color: #87bdb9; }
.clinical-topbar h1 { margin: 0; font-size: clamp(1.35rem, 2.4vw, 1.8rem); line-height: 1.2; color: #102e56; }

.sync-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.28rem 0.7rem;
  border-radius: 999px;
  background: #e8f5f3;
  border: 1px solid #bde0dc;
  color: #0f5c59;
  font-size: 0.82rem;
}

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
  gap: 0.85rem;
  padding: 2rem 1rem;
}
.empty-selection-placeholder p { color: #51647a; font-size: 0.94rem; max-width: 58ch; }
.go-queue-btn { text-decoration: none; display: inline-flex; align-items: center; }

.candidate-grid {
  width: 100%;
  max-width: 820px;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 0.7rem;
  text-align: left;
}
.candidate-card {
  display: grid;
  gap: 0.28rem;
  padding: 0.85rem 0.95rem;
  border: 1px solid #d1dfe9;
  border-radius: 11px;
  background: #f8fbfd;
  cursor: pointer;
  text-align: left;
}
.candidate-card:hover { border-color: #176864; background: #eef8f7; }
.candidate-top { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
.candidate-ticket { font-weight: 800; color: #176864; font-size: 0.92rem; }
.candidate-meta { font-size: 0.8rem; color: #51647a; }

.patient-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
  padding-bottom: 0.85rem;
  border-bottom: 1px solid #e5edf2;
}
.patient-identity { min-width: 0; }
.patient-header h2 { font-size: clamp(1.25rem, 2.6cqw, 1.55rem); color: #102e56; overflow-wrap: anywhere; }
.patient-submeta { display: flex; flex-wrap: wrap; align-items: center; gap: 0.35rem 0.8rem; margin-top: 0.3rem; color: #51647a; font-size: 0.88rem; }
.child-pill { padding: 0.14rem 0.5rem; border-radius: 999px; background: #e0f2fe; color: #0369a1; font-size: 0.78rem; font-weight: 700; }
.patient-header-right { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }

.queue-status-tag, .visit-status-tag {
  padding: 0.28rem 0.65rem;
  border-radius: 999px;
  background: #edf2f6;
  color: #3f5369;
  font-size: 0.8rem;
  font-weight: 700;
}
.queue-status-tag[data-status="2"], .queue-status-tag[data-status="6"] { background: #def2ef; color: #144f4d; }
.visit-status-tag { background: #eef5fb; color: #1e5273; }

.handoff-banner {
  padding: 0.65rem 0.85rem;
  border-radius: 10px;
  background: #f0f9f8;
  border: 1px solid #bfe3de;
  color: #0e4f4c;
  font-size: 0.88rem;
  line-height: 1.45;
}

.clinical-cockpit {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}
.clinical-main-col, .clinical-side-col { display: grid; gap: 0.85rem; min-width: 0; }

.pre-exam-card, .side-card, .diagnosis-form, .read-only-diagnosis {
  display: grid;
  gap: 0.75rem;
  padding: 0.95rem;
  border: 1px solid #e1e9ef;
  border-radius: 12px;
  background: #fcfdff;
  min-width: 0;
}
.pre-exam-hint { font-size: 0.84rem; color: #3c5468; background: #f0f7fb; padding: 0.55rem 0.7rem; border-radius: 8px; }
.loading-state-sm, .muted-note { color: #51647a; font-size: 0.86rem; }

.vitals-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.55rem; }
.vital-tile {
  display: grid;
  gap: 0.15rem;
  padding: 0.55rem 0.65rem;
  border: 1px solid #d9e6f0;
  border-radius: 9px;
  background: #fff;
  text-align: center;
}
.vital-tile--warn { border-color: #f59e0b; background: #fffbeb; }
.vital-label { font-size: 0.74rem; color: #5b7184; font-weight: 650; }
.vital-tile strong { font-size: 1.05rem; color: #102e56; line-height: 1.2; }
.vital-unit { font-size: 0.72rem; color: #6c8091; }

.form-head { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; flex-wrap: wrap; }
.form-sub { font-size: 0.8rem; color: #51647a; }
.warn-badge { padding: 0.18rem 0.55rem; border-radius: 999px; background: #fef3c7; color: #92400e; font-size: 0.75rem; font-weight: 700; }

.vital-inputs-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.7rem;
}
.form-group { display: grid; gap: 0.3rem; min-width: 0; }
.form-group label { font-weight: 650; font-size: 0.86rem; }
.form-group input, .form-group textarea {
  width: 100%;
  min-width: 0;
  padding: 0.5rem 0.65rem;
  border: 1px solid #b9cedd;
  border-radius: 8px;
  font: inherit;
  font-size: 0.92rem;
  background: #fff;
}
.form-group input { min-height: 38px; }
.form-group textarea { resize: vertical; }
.diagnosis-footer { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
.diagnosis-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }

.vital-history-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
.vital-history-item { padding: 0.55rem 0.7rem; border: 1px solid #e2eaf0; border-radius: 8px; background: #fff; display: grid; gap: 0.2rem; }
.history-item-top { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.3rem 0.75rem; font-size: 0.84rem; }
.history-item-note { font-size: 0.82rem; color: #475569; }

.history-form { display: grid; gap: 0.6rem; }
.history-edit-row { display: grid; gap: 0.4rem; padding: 0.6rem; border: 1px solid #dce6ef; border-radius: 8px; background: #fff; }
.history-row-fields { display: grid; grid-template-columns: 110px 1fr; gap: 0.4rem; }
.history-row-fields select, .history-row-fields input, .history-edit-row > input {
  padding: 0.4rem 0.55rem;
  border: 1px solid #b9cedd;
  border-radius: 6px;
  font: inherit;
  font-size: 0.86rem;
}
.history-row-footer { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; font-size: 0.82rem; }
.critical-check { display: inline-flex; align-items: center; gap: 0.35rem; font-weight: 600; color: #71401a; }
.link-danger { background: none; border: none; color: #b91c1c; font-size: 0.8rem; font-weight: 600; cursor: pointer; padding: 0; }

.mh-summary-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
.mh-summary-list li { padding: 0.55rem 0.7rem; border: 1px solid #e2eaf0; border-radius: 8px; background: #fff; }
.mh-summary-list li.mh-critical { border-color: #f3c46b; background: #fffbeb; }
.mh-top { display: flex; flex-wrap: wrap; align-items: center; gap: 0.4rem; font-size: 0.88rem; }
.mh-kind { padding: 0.12rem 0.45rem; border-radius: 999px; background: #e8f1f8; color: #1e4b6d; font-size: 0.74rem; font-weight: 700; }
.critical-pill { padding: 0.12rem 0.45rem; border-radius: 999px; background: #fde68a; color: #78350f; font-size: 0.72rem; font-weight: 700; }
.mh-summary-list p { margin-top: 0.25rem; font-size: 0.82rem; color: #475569; }
.record-val { white-space: pre-wrap; overflow-wrap: anywhere; font-size: 0.86rem; color: #334155; }

.care-links { display: grid; gap: 0.55rem; }
.care-links-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }
.care-tile {
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  min-height: 44px;
  padding: 0.5rem 0.65rem;
  border: 1px solid #cddce8;
  border-radius: 9px;
  background: #fff;
  color: #18496a;
  font-size: 0.84rem;
  font-weight: 700;
  text-decoration: none;
}
.care-tile:hover { background: #eef7fb; border-color: #176864; color: #0f524f; }
.care-finish-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  min-height: 42px;
  padding: 0.5rem 0.85rem;
  border-radius: 9px;
  background: #255c81;
  color: #fff;
  font-size: 0.86rem;
  font-weight: 700;
  text-decoration: none;
}
.care-finish-btn:hover { background: #1d4a69; }

.btn {
  border: 1px solid #bccddc;
  border-radius: 8px;
  padding: 0.45rem 0.8rem;
  min-height: 38px;
  background: #fff;
  color: #254965;
  font: inherit;
  font-size: 0.86rem;
  font-weight: 700;
  cursor: pointer;
}
.btn-sm { min-height: 34px; padding: 0.3rem 0.65rem; font-size: 0.82rem; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.btn-primary { background: #176864; border-color: #176864; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0f524f; border-color: #0f524f; }

.alert-box { display: flex; align-items: center; gap: 0.5rem; border-radius: 8px; padding: 0.6rem 0.8rem; font-size: 0.86rem; }
.alert-error { color: #8d2624; background: #fff0ed; border: 1px solid #fecaca; }
.alert-success { color: #215732; background: #e8f6ed; border: 1px solid #bbf7d0; }

@container (min-width: 820px) {
  .clinical-cockpit { grid-template-columns: minmax(0, 1.3fr) minmax(0, 0.95fr); }
}

@media (max-width: 560px) {
  .vital-inputs-grid, .vitals-grid, .care-links-grid, .history-row-fields { grid-template-columns: 1fr; }
}
</style>
