<template>
  <main class="fdi-page">
    <div class="breadcrumb-bar">
      <RouterLink :to="clinicalBackLocation" class="back-link primary-back">{{ clinicalBackLabel }}</RouterLink>
      <template v-if="!busy && visitId > 0">
        <RouterLink :to="{ path: `${portalPrefix}/clinical/visits/${visitId}/prescription`, query: visitNavQuery }" class="quick-nav-pill">Đơn thuốc</RouterLink>
        <RouterLink v-if="['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="{ path: `${portalPrefix}/clinical/visits/${visitId}/images`, query: visitNavQuery }" class="quick-nav-pill">Ảnh khám</RouterLink>
        <RouterLink v-if="['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="{ path: `${portalPrefix}/clinical/visits/${visitId}/invoice-draft`, query: visitNavQuery }" class="quick-nav-pill alt">Hóa đơn nháp</RouterLink>
      </template>
      <span class="breadcrumb-current">Lâm sàng › Sơ đồ răng FDI &amp; Chỉ định dịch vụ</span>
    </div>

    <div v-if="visit?.status === 'Correcting'" class="correcting-alert">
      <span>Đang đính chính hồ sơ ngoài ca khám.</span>
      <RouterLink :to="{ path: `${portalPrefix}/clinical/visits/${visitId}/invoice-draft`, query: visitNavQuery }">Trở lại để hoàn tất và khóa đính chính</RouterLink>
    </div>

    <div class="page-header-row">
      <div>
        <h1 class="page-title">Sơ đồ răng FDI &amp; Chỉ định dịch vụ · Lần khám #{{ visitId }}</h1>
        <p v-if="visit" class="page-subtitle">Bệnh nhân #{{ visit.patientId }} · Trạng thái: <strong>{{ visit.status }}</strong></p>
      </div>
      <div class="header-actions">
        <RouterLink v-if="!busy && ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="{ path: `${portalPrefix}/clinical/visits/${visitId}/invoice-draft`, query: visitNavQuery }" class="btn-secondary">Hóa đơn nháp →</RouterLink>
        <button type="button" class="btn-refresh" :disabled="busy || serviceBusy" @click="load">Tải lại hồ sơ</button>
      </div>
    </div>

    <p v-if="busy || serviceBusy" role="status" class="status-msg">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status" class="success-msg">{{ success }}</p>
    <template v-if="visit">
      <SafetyAlertBanner v-if="isStaff" :patient-id="visit.patientId" />

      <p v-if="!canEdit" class="readonly-notice">Chỉ Admin hoặc nha sĩ phụ trách được ghi tình trạng răng và chỉ định dịch vụ khi lần khám đang diễn ra và chưa khóa.</p>
      <p class="guide-text">
        {{ activeTab === 'services'
          ? 'Chọn một hoặc nhiều răng trên sơ đồ để chỉ định dịch vụ, hoặc bật "Dịch vụ toàn hàm / tổng quát". Màu tình trạng răng vẫn hiển thị để đối chiếu.'
          : 'Chọn răng hoặc nhiều mặt trên cùng một chiếc răng để ghi nhận tình trạng và tra cứu lịch sử qua các lần khám.' }}
      </p>
      <div class="chart-section-card">
        <FdiDentalChart
          v-model="selection"
          v-model:dentition-type="dentition"
          :locked-dentition="patientDentition"
          :latest-conditions="latestConditions"
          :multiple="activeTab === 'services'"
          :multi-surface="activeTab === 'conditions'"
          :readonly="busy || serviceBusy || (activeTab === 'services' && general)"
          @select="handleChartSelect"
          @update:model-value="clearSuggestion"
        />
      </div>

      <div class="workspace-tabs" role="tablist" aria-label="Phân hệ làm việc trên sơ đồ răng">
        <button
          type="button"
          role="tab"
          :aria-selected="activeTab === 'conditions'"
          :class="['workspace-tab-btn', { active: activeTab === 'conditions' }]"
          @click="switchTab('conditions')"
        >
          <span>Tình trạng răng &amp; Ghi chú</span>
          <span class="tab-count-badge">{{ rows.length }}</span>
        </button>
        <button
          type="button"
          role="tab"
          :aria-selected="activeTab === 'services'"
          :class="['workspace-tab-btn', { active: activeTab === 'services' }]"
          @click="switchTab('services')"
        >
          <span>Chỉ định dịch vụ &amp; Viện phí</span>
          <span class="tab-count-badge">{{ serviceRows.length }}</span>
          <span v-if="serviceRows.length" class="tab-amount-badge">{{ money(totalAssignedAmount) }}</span>
        </button>
      </div>

      <ToothHistoryDrawer ref="history" :patient-id="visit.patientId">
        <template #actions="{toothNumber,allJaw}">
          <RouterLink v-if="toothNumber && ['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role)" :to="{name:auth.role === 'PATIENT' ? 'patient-portal-visit-images' : 'management-tooth-images',params:{visitId},query:{...visitNavQuery,toothNumber:allJaw?undefined:toothNumber}}" class="drawer-action-link">{{ allJaw ? 'Xem ảnh hồ sơ bệnh nhân' : `Xem ảnh răng ${toothNumber}` }}</RouterLink>
        </template>
      </ToothHistoryDrawer>

      <div v-if="activeTab === 'conditions' && selection[0] && ['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role)" class="image-link-wrap">
        <RouterLink :to="{name:auth.role === 'PATIENT' ? 'patient-portal-visit-images' : 'management-tooth-images',params:{visitId},query:{...visitNavQuery,toothNumber:selection[0].toothNumber}}" class="btn-tooth-images">Xem ảnh răng {{ selection[0].toothNumber }} qua các lần khám</RouterLink>
      </div>

      <div v-show="activeTab === 'conditions'" class="clinical-work-grid">
        <section v-if="canEdit" class="card form-card" aria-label="Ghi nhận tình trạng răng">
          <h2 class="card-title">{{ editingId ? 'Sửa tình trạng răng đã chọn' : 'Ghi nhận tình trạng mới' }}</h2>
          <p v-if="selectedToothNumber" class="selected-target-badge">Răng {{ selectedToothNumber }} · {{ selectedSurfaceLabel }}</p>
          <p v-else class="empty-target-hint">Chọn một răng hoặc nhiều mặt trên cùng một răng trên sơ đồ.</p>

          <form @submit.prevent="save" class="clinical-form">
            <div class="form-field">
              <label for="fdi-condition-code">Tình trạng bệnh lý răng miệng</label>
              <select id="fdi-condition-code" v-model="code" :disabled="busy || serviceBusy" required @change="clearSuggestion">
                <option v-for="(label, value) in FDI_CONDITIONS" :key="value" :value="value">{{ label }}</option>
              </select>
            </div>

            <div class="form-field">
              <label for="fdi-clinical-note">Ghi chú lâm sàng theo răng (không bắt buộc)</label>
              <textarea id="fdi-clinical-note" v-model="note" :disabled="busy || serviceBusy" maxlength="500" rows="3" placeholder="Ghi nhận chuyên môn cho răng/mặt răng đã chọn" />
              <small>{{ note.length }}/500 ký tự · Lưu cùng bản ghi tình trạng mới, giữ lịch sử cũ.</small>
            </div>

            <div class="form-actions">
              <button type="submit" class="btn-primary" :disabled="busy || serviceBusy || !selection.length">{{ editingId ? 'Lưu chỉnh sửa tình trạng' : 'Ghi nhận tình trạng sau điều trị' }}</button>
              <button v-if="editingId" type="button" class="btn-cancel" :disabled="busy || serviceBusy" @click="cancelEditing">Hủy chỉnh sửa</button>
            </div>
          </form>

          <div class="suggestion-block" aria-labelledby="suggested-service-title">
            <h3 id="suggested-service-title" class="suggestion-title">Gợi ý dịch vụ theo tình trạng răng</h3>
            <button type="button" class="btn-secondary" :disabled="busy || serviceBusy || !selection.length" @click="loadSuggestion">Tải gợi ý dịch vụ</button>
            <p v-if="suggestionMessage" role="status" class="suggestion-status">{{ suggestionMessage }}</p>
            <button v-if="suggestedService" type="button" class="btn-assign-suggest" :disabled="busy || serviceBusy || !selection.length" @click="assignSuggestion">
              Chỉ định {{ suggestedService.label }} cho răng {{ selectedToothNumber }}
              <template v-if="selectedSurfaceCode"> · {{ selectedSurfaceLabel }}</template>
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
                <button type="button" :disabled="busy || serviceBusy" @click="locate(row)">Xem trên sơ đồ</button>
                <button type="button" :disabled="busy || serviceBusy" @click="openHistory({ toothNumber: row.toothNumber })">Lịch sử răng</button>
                <template v-if="canEdit">
                  <button type="button" :disabled="busy || serviceBusy" @click="edit(row)">Sửa bản ghi</button>
                  <button v-if="pendingDeleteId !== row.id" type="button" class="btn-danger-text" :disabled="busy || serviceBusy" @click="pendingDeleteId = row.id">Xóa bản ghi</button>
                  <span v-else class="delete-confirm-box" role="group" :aria-label="`Xác nhận xóa tình trạng răng ${row.toothNumber}`">
                    <span>Chỉ xóa bản ghi trong lần khám đang mở này.</span>
                    <button type="button" class="btn-danger" :disabled="busy || serviceBusy" @click="remove(row)">Xác nhận xóa</button>
                    <button type="button" :disabled="busy || serviceBusy" @click="pendingDeleteId = null">Giữ lại</button>
                  </span>
                </template>
              </div>
            </li>
          </ul>
        </section>
      </div>

      <div v-show="activeTab === 'services'" :class="['clinical-work-grid', { 'single-column': !canEdit }]">
        <section v-if="canEdit" class="card form-card" aria-label="Biểu mẫu chỉ định dịch vụ">
          <h2 class="card-title">Thêm chỉ định dịch vụ</h2>
          <div class="general-toggle-row">
            <label class="checkbox-label">
              <input v-model="general" type="checkbox" :disabled="busy || serviceBusy" @change="onGeneralChange" />
              <span>Dịch vụ toàn hàm / tổng quát</span>
            </label>
          </div>
          <p v-if="general" class="selected-target-badge">Áp dụng toàn hàm / tổng quát (không chọn răng riêng)</p>
          <p v-else-if="selectedTeeth.length" class="selected-target-badge">Răng được chỉ định: {{ selectedTeeth.join(', ') }} (mỗi răng 1 dòng)</p>
          <p v-else class="empty-target-hint">Nhấp chọn một hoặc nhiều răng trên sơ đồ phía trên, hoặc bật "Dịch vụ toàn hàm / tổng quát".</p>

          <form aria-label="Tìm dịch vụ để chỉ định" class="service-search-form" @submit.prevent="searchCatalog">
            <label for="service-keyword" class="search-field-label">Tìm dịch vụ theo tên hoặc mã (tối đa 100 ký tự)</label>
            <div class="search-input-group">
              <input id="service-keyword" v-model="serviceKeyword" maxlength="100" placeholder="VD: Trám răng, Nhổ răng, Cạo vôi..." :disabled="busy || serviceBusy" />
              <button type="submit" class="btn-secondary" :disabled="busy || serviceBusy">Tìm dịch vụ</button>
              <button type="button" class="btn-cancel" :disabled="busy || serviceBusy" @click="clearServiceSearch">Xóa lọc</button>
            </div>
            <p v-if="searchError" class="error" role="alert">{{ searchError }}</p>
            <p v-if="appliedKeyword" class="applied-filter-tag">Đang lọc theo: <strong>{{ appliedKeyword }}</strong></p>
          </form>

          <form class="clinical-form" @submit.prevent="assignService">
            <div class="form-field">
              <label for="service-select">Dịch vụ</label>
              <select id="service-select" v-model="serviceId" :disabled="busy || serviceBusy || Boolean(catalogError)" required>
                <option :value="null" disabled>Chọn dịch vụ</option>
                <option v-for="service in catalog" :key="service.dentalServiceId" :value="service.dentalServiceId" :disabled="service.currentPrice === null">
                  {{ service.code }} · {{ service.name }} · {{ service.currentPrice === null ? 'Chưa có giá hiệu lực' : money(service.currentPrice) }}
                </option>
              </select>
            </div>

            <div class="catalog-pagination">
              <button type="button" :disabled="busy || serviceBusy || catalogPage <= 1" @click="loadCatalog(catalogPage - 1)">Trang trước</button>
              <span class="page-indicator">{{ catalogPage }}/{{ Math.max(catalogPages, 1) }}</span>
              <button type="button" :disabled="busy || serviceBusy || catalogPage >= catalogPages" @click="loadCatalog(catalogPage + 1)">Trang sau</button>
              <button type="button" :disabled="busy || serviceBusy" @click="loadCatalog(catalogPage)">Tải lại danh mục</button>
            </div>

            <p v-if="catalogError" class="error" role="alert">{{ catalogError }}</p>
            <p v-if="!busy && !serviceBusy && !catalogError && !catalog.length" class="empty-rows-hint">Không có dịch vụ phù hợp. Thử tên/mã khác hoặc xóa lọc.</p>

            <div v-if="!general" class="form-field">
              <label for="service-surface-select">Mặt áp dụng cho mọi răng đã chọn</label>
              <select id="service-surface-select" v-model="serviceSurface" :disabled="busy || serviceBusy">
                <option :value="null">Toàn răng</option>
                <option v-for="item in SURFACE_METADATA" :key="item.code" :value="item.code">{{ item.nameVi }}</option>
              </select>
            </div>

            <div class="form-field">
              <label for="service-quantity-input">Số lượng trên mỗi dòng</label>
              <input id="service-quantity-input" v-model.number="serviceQuantity" :disabled="busy || serviceBusy" type="number" min="1" max="100" step="1" required />
            </div>

            <small class="disclaimer-note">Đơn giá hiển thị để tham khảo; hệ thống chốt giá đang hiệu lực khi lưu chỉ định và đồng bộ vào hóa đơn nháp.</small>
            <div class="form-actions">
              <button type="submit" class="btn-primary" :disabled="busy || serviceBusy || !serviceId || Boolean(catalogError) || (!general && !selectedTeeth.length)">Lưu chỉ định dịch vụ</button>
            </div>
          </form>
        </section>

        <section class="card history-card" aria-label="Dịch vụ đã chỉ định">
          <div class="card-title-row">
            <h2 class="card-title no-border">Dịch vụ đã chỉ định ({{ serviceRows.length }})</h2>
            <strong v-if="serviceRows.length" class="assigned-grand-total">Tạm tính: {{ money(totalAssignedAmount) }}</strong>
          </div>
          <p v-if="!serviceRows.length" class="empty-rows-hint">Chưa có dịch vụ được chỉ định trong lần khám này.</p>
          <ul v-else class="condition-row-list">
            <li v-for="row in serviceRows" :key="row.id" class="condition-row-item">
              <div class="row-header-split">
                <strong class="row-target">{{ row.serviceCode }} · {{ row.serviceName }}</strong>
                <span class="service-total-pill">{{ money(row.totalAmount) }}</span>
              </div>
              <p class="service-tooth-meta">{{ row.toothNumber === null ? 'Toàn hàm / tổng quát' : `Răng ${row.toothNumber} · ${row.surface || 'Toàn răng'}` }}</p>
              <p class="service-calc">{{ row.quantity }} × {{ money(row.unitPrice) }} = {{ money(row.totalAmount) }} (giá đã chốt)</p>
              <small class="row-time">{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>

              <div v-if="canEdit" class="row-actions">
                <button v-if="deleteServiceId !== row.id" type="button" class="btn-danger-text" :disabled="busy || serviceBusy" @click="beginDeleteService(row.id)">Xóa chỉ định</button>
                <form v-else class="service-delete-form" @submit.prevent="confirmDeleteService">
                  <p class="service-delete-warning">Xác nhận xóa dòng chỉ định này. Hóa đơn chưa thu sẽ giảm tương ứng; thao tác được lưu nhật ký.</p>
                  <label class="search-field-label">
                    Lý do xóa (bắt buộc, không nhập thông tin cá nhân bệnh nhân)
                    <textarea v-model="deleteServiceReason" required maxlength="500" rows="2" :disabled="busy || serviceBusy" />
                  </label>
                  <div class="form-actions">
                    <button type="submit" class="btn-danger" :disabled="busy || serviceBusy || !deleteServiceReason.trim()">Xác nhận xóa</button>
                    <button type="button" class="btn-cancel" :disabled="busy || serviceBusy" @click="deleteServiceId = null">Hủy</button>
                  </div>
                </form>
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
import { visitServicesApi, type AssignedService, type CatalogOption } from '@/services/visitServices';

const route = useRoute();
const auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
const isStaff = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'].includes(auth.role));
const visitId = computed(() => Number(route.params.visitId));
const queueEntryIdParam = computed(() => {
  const raw = route.query?.queueEntryId;
  return typeof raw === 'string' && /^\d+$/.test(raw) ? raw : null;
});
const visitNavQuery = computed(() => queueEntryIdParam.value ? { queueEntryId: queueEntryIdParam.value } : undefined);
const clinicalBackLocation = computed(() => {
  if (['ADMIN', 'DENTIST'].includes(auth.role)) {
    return queueEntryIdParam.value && Number.isInteger(visitId.value) && visitId.value > 0
      ? { path: '/management/clinical/diagnosis', query: { queueEntryId: queueEntryIdParam.value, visitId: String(visitId.value) } }
      : '/management/clinical/diagnosis';
  }
  if (auth.role === 'PATIENT') return '/patient/my-record';
  return '/management/queue/today';
});
const clinicalBackLabel = computed(() => {
  if (['ADMIN', 'DENTIST'].includes(auth.role)) return '← Quay lại khám bệnh';
  if (auth.role === 'PATIENT') return '← Hồ sơ của tôi';
  return '← Hàng đợi hôm nay';
});
const visit = ref<VisitDetails | null>(null);
const fdiStore = useFdiStore();
const { rows } = storeToRefs(fdiStore);
const activeTab = ref<'conditions' | 'services'>(route.query?.tab === 'services' ? 'services' : 'conditions');
const selection = ref<SelectedToothSurface[]>([]);
const latestConditions = ref<Awaited<ReturnType<typeof fdiService.latestForPatient>>>([]);
const dentition = ref<DentitionType>('adult');
const patientDentition = ref<'adult' | 'child' | null>(null);
let dentitionVisitId = 0;
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

// State gộp từ Chỉ định dịch vụ (F_FDI_04)
const serviceRows = ref<AssignedService[]>([]);
const serviceBusy = ref(false);
const general = ref(false);
const serviceSurface = ref<string | null>(null);
const serviceQuantity = ref(1);
const serviceId = ref<number | null>(null);
const catalog = ref<CatalogOption[]>([]);
const catalogPage = ref(1);
const catalogPages = ref(1);
const catalogError = ref('');
const serviceKeyword = ref('');
const appliedKeyword = ref('');
const searchError = ref('');
const deleteServiceId = ref<number | null>(null);
const deleteServiceReason = ref('');

const selectedTeeth = computed(() => [...new Set(selection.value.map(x => x.toothNumber))].sort((a, b) => a - b));
const totalAssignedAmount = computed(() => serviceRows.value.reduce((sum, item) => sum + (item.totalAmount || 0), 0));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);

function getServicesApi() {
  try {
    return visitServicesApi ?? null;
  } catch {
    return null;
  }
}

function isValidFdiTooth(toothNumber: number): boolean {
  const quadrant = Math.floor(toothNumber / 10);
  const tooth = toothNumber % 10;
  return (quadrant >= 1 && quadrant <= 4 && tooth >= 1 && tooth <= 8) ||
    (quadrant >= 5 && quadrant <= 8 && tooth >= 1 && tooth <= 5);
}

function openHistory(selected: { toothNumber: number }) { if (!busy.value && !serviceBusy.value) void history.value?.open(selected.toothNumber); }
function handleChartSelect(selected: { toothNumber: number }) {
  if (activeTab.value === 'conditions') openHistory(selected);
}
function clearSuggestion() { suggestedService.value = null; suggestionMessage.value = ''; }
const canEdit = computed(() => ['InProgress', 'Correcting'].includes(visit.value?.status ?? '') && !visit.value?.isLocked &&
  (auth.role === 'ADMIN' || (auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId)));

const selectedToothNumber = computed(() => selection.value[0]?.toothNumber ?? null);
const selectedSurfacesList = computed(() => {
  const tooth = selectedToothNumber.value;
  if (!tooth) return [];
  const items = selection.value.filter(item => item.toothNumber === tooth);
  if (items.some(item => !item.surface)) return [];
  const chosen = new Set(items.map(item => item.surface).filter((s): s is NonNullable<typeof s> => Boolean(s && s in SURFACE_METADATA)));
  return ALL_SURFACES.filter(surface => chosen.has(surface));
});
const selectedSurfaceCode = computed(() => {
  const list = selectedSurfacesList.value;
  if (!list.length || list.length >= 5) return null;
  return list.map(surface => SURFACE_METADATA[surface].code).join('');
});
const selectedSurfaceLabel = computed(() => {
  const list = selectedSurfacesList.value;
  if (!list.length || list.length >= 5) return 'Toàn răng';
  return list.map(surface => `${SURFACE_METADATA[surface].nameVi} (${SURFACE_METADATA[surface].code})`).join(', ');
});

function message(cause: unknown, fallback = 'Không thể tải hoặc lưu tình trạng răng. Vui lòng thử lại.') {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || fallback : 'Không thể xử lý yêu cầu.';
}

function onGeneralChange() {
  if (general.value) {
    selection.value = [];
    serviceSurface.value = null;
  }
}

function switchTab(tab: 'conditions' | 'services') {
  if (activeTab.value === tab) return;
  activeTab.value = tab;
  error.value = '';
  success.value = '';
  if (tab === 'services') {
    editingId.value = null;
    pendingDeleteId.value = null;
    clearSuggestion();
    if (!general.value && selection.value.length) {
      const uniqueTeeth = [...new Set(selection.value.map(item => item.toothNumber))];
      selection.value = uniqueTeeth.map(toothNumber => ({ toothNumber }));
    }
    const api = getServicesApi();
    if (canEdit.value && !catalog.value.length && !catalogError.value && typeof api?.catalog === 'function') {
      void loadCatalog(1);
    }
  } else {
    general.value = false;
    deleteServiceId.value = null;
    deleteServiceReason.value = '';
    const firstTooth = selection.value[0]?.toothNumber;
    const uniqueTeeth = new Set(selection.value.map(item => item.toothNumber));
    if (firstTooth && uniqueTeeth.size > 1) {
      selection.value = [{ toothNumber: firstTooth }];
    }
  }
}

function locate(row: ToothCondition) {
  activeTab.value = 'conditions';
  if (!patientDentition.value) {
    dentition.value = row.toothNumber >= 51 ? 'child' : 'adult';
  }
  selection.value = row.surface
    ? ALL_SURFACES.filter(surface => row.surface!.includes(SURFACE_METADATA[surface].code))
      .map(surface => ({ toothNumber: row.toothNumber, surface }))
    : [{ toothNumber: row.toothNumber }];
}
function edit(row: ToothCondition) {
  if (!canEdit.value || busy.value || serviceBusy.value) return;
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
  if (!canEdit.value || busy.value || serviceBusy.value || !selection.value[0]) return;
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
  const first = selection.value[0];
  const selected = selection.value.length === 1
    ? first
    : first
      ? { toothNumber: first.toothNumber, surface: undefined }
      : undefined;
  const service = suggestedService.value;
  if (!canEdit.value || busy.value || serviceBusy.value || !selected || !service) return;
  busy.value = true; error.value = ''; success.value = '';
  try {
    const saved = await visitServicesApi.assign(visitId.value, {
      serviceId: service.dentalServiceId, toothNumbers: [selected.toothNumber],
      surface: selected.surface ? SURFACE_METADATA[selected.surface].code : null, quantity: 1
    });
    if (Array.isArray(saved) && saved.length) {
      serviceRows.value = [...saved, ...serviceRows.value];
    }
    success.value = `Đã chỉ định ${service.name} cho răng ${selected.toothNumber}. Đây là chỉ định, không xác nhận thủ thuật đã thực hiện.`;
    suggestionMessage.value = '';
  } catch (cause) { error.value = message(cause); }
  finally { busy.value = false; }
}

async function fetchCatalog(page: number, version: number) {
  const api = getServicesApi();
  if (typeof api?.catalog !== 'function') return;
  catalogError.value = ''; catalog.value = []; serviceId.value = null;
  try {
    const data = await api.catalog(page, appliedKeyword.value);
    if (version !== loadVersion) return;
    catalog.value = data.items; catalogPage.value = page; catalogPages.value = data.totalPages;
  } catch (cause) {
    if (version === loadVersion) catalogError.value = message(cause, 'Không thể tải danh mục dịch vụ. Vui lòng thử lại.');
  }
}
async function loadCatalog(page: number) {
  if (busy.value || serviceBusy.value || !canEdit.value || !Number.isInteger(page) || page < 1 || page > 1_000_000) return;
  const version = loadVersion;
  serviceBusy.value = true;
  try { await fetchCatalog(page, version); }
  finally { if (version === loadVersion) serviceBusy.value = false; }
}
async function searchCatalog() {
  if (busy.value || serviceBusy.value || !canEdit.value) return;
  if (serviceKeyword.value.length > 100) { searchError.value = 'Từ khóa không được vượt quá 100 ký tự.'; return; }
  searchError.value = ''; appliedKeyword.value = serviceKeyword.value.trim();
  await loadCatalog(1);
}
async function clearServiceSearch() {
  if (busy.value || serviceBusy.value || !canEdit.value) return;
  serviceKeyword.value = '';
  await searchCatalog();
}
async function assignService() {
  const api = getServicesApi();
  if (!canEdit.value || busy.value || serviceBusy.value || catalogError.value || typeof api?.assign !== 'function') return;
  const option = catalog.value.find(x => x.dentalServiceId === serviceId.value);
  const teeth = general.value ? null : selectedTeeth.value;
  if (!option || option.currentPrice === null || !Number.isInteger(serviceQuantity.value) || serviceQuantity.value < 1 || serviceQuantity.value > 100 ||
      teeth && (!teeth.length || teeth.length > 52 || teeth.some(x => !isValidFdiTooth(x))) ||
      !general.value && serviceSurface.value !== null && !Object.values(SURFACE_METADATA).some(x => x.code === serviceSurface.value)) {
    error.value = 'Kiểm tra lại dịch vụ, số lượng và răng/mặt răng.'; return;
  }
  const version = loadVersion;
  serviceBusy.value = true; error.value = ''; success.value = '';
  try {
    const saved = await api.assign(visitId.value, {
      serviceId: option.dentalServiceId,
      toothNumbers: teeth,
      surface: general.value ? null : serviceSurface.value,
      quantity: serviceQuantity.value
    });
    if (version !== loadVersion) return;
    serviceRows.value = [...saved, ...serviceRows.value];
    selection.value = []; serviceId.value = null;
    success.value = `Đã lưu ${saved.length} dòng dịch vụ và chốt đơn giá vào hóa đơn nháp.`;
  } catch (cause) {
    if (version === loadVersion) error.value = message(cause, 'Không thể lưu chỉ định dịch vụ. Vui lòng thử lại.');
  } finally {
    if (version === loadVersion) serviceBusy.value = false;
  }
}
function beginDeleteService(id: number) {
  if (!canEdit.value || busy.value || serviceBusy.value) return;
  deleteServiceId.value = id; deleteServiceReason.value = ''; error.value = ''; success.value = '';
}
async function confirmDeleteService() {
  const api = getServicesApi();
  if (!canEdit.value || busy.value || serviceBusy.value || deleteServiceId.value === null || typeof api?.remove !== 'function') return;
  const id = deleteServiceId.value, reason = deleteServiceReason.value.trim();
  if (!Number.isInteger(id) || id <= 0 || !reason || reason.length > 500) {
    error.value = 'Nhập lý do xóa từ 1 đến 500 ký tự.'; return;
  }
  const version = loadVersion, currentVisitId = visitId.value;
  serviceBusy.value = true; error.value = ''; success.value = '';
  try {
    await api.remove(currentVisitId, id, reason);
    if (version !== loadVersion || currentVisitId !== visitId.value) return;
    serviceRows.value = serviceRows.value.filter(row => row.id !== id);
    deleteServiceId.value = null; deleteServiceReason.value = '';
    success.value = 'Đã xóa chỉ định dịch vụ và cập nhật hóa đơn nháp.';
  } catch (cause) {
    if (version === loadVersion) {
      error.value = message(cause, 'Không thể xóa chỉ định dịch vụ. Vui lòng thử lại.');
      deleteServiceId.value = null; deleteServiceReason.value = '';
    }
  } finally {
    if (version === loadVersion) serviceBusy.value = false;
  }
}

let loadVersion = 0;
async function refreshLatestConditions(patientId: number, version: number, visitKey: number, preserveOnError = false) {
  try {
    const latest = await fdiService.latestForPatient(patientId);
    if (version === loadVersion && visitKey === visitId.value) latestConditions.value = latest;
    return true;
  } catch {
    if (!preserveOnError && version === loadVersion && visitKey === visitId.value) latestConditions.value = [];
    return false;
  }
}
async function load() {
  const version = ++loadVersion;
  busy.value = true; error.value = ''; success.value = ''; visit.value = null; fdiStore.reset(); selection.value = [];
  note.value = ''; editingId.value = null; pendingDeleteId.value = null; latestConditions.value = [];
  serviceRows.value = []; serviceId.value = null; general.value = false; serviceSurface.value = null; serviceQuantity.value = 1;
  deleteServiceId.value = null; deleteServiceReason.value = '';
  catalog.value = []; catalogError.value = ''; serviceKeyword.value = ''; appliedKeyword.value = ''; searchError.value = '';
  if (route.query?.tab === 'services') activeTab.value = 'services';
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) {
    error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return;
  }
  try {
    const api = getServicesApi();
    const recordsPromise = typeof api?.get === 'function' ? api.get(visitId.value) : Promise.resolve([] as AssignedService[]);
    const [details, conditions, assigned] = await Promise.all([
      visitsService.getById(visitId.value),
      fdiService.get(visitId.value),
      recordsPromise
    ]);
    if (version !== loadVersion) return;
    if (dentitionVisitId !== details.visitId) {
      const mode = await visitsService.getDefaultDentition(details).catch(() => 'adult' as const);
      if (version !== loadVersion) return;
      const resolvedDentition: 'adult' | 'child' = mode === 'child' ? 'child' : 'adult';
      dentition.value = resolvedDentition;
      patientDentition.value = resolvedDentition;
      dentitionVisitId = details.visitId;
    }
    visit.value = details; fdiStore.replace(conditions); serviceRows.value = assigned;
    const latestPromise = refreshLatestConditions(details.patientId, version, visitId.value);
    const catalogPromise = canEdit.value && typeof api?.catalog === 'function' ? fetchCatalog(1, version) : Promise.resolve();
    const [latestLoaded] = await Promise.all([latestPromise, catalogPromise]);
    if (!latestLoaded && version === loadVersion)
      error.value = 'Không tải được trạng thái răng mới nhất để tô màu sơ đồ.';
  } catch (cause) { if (version === loadVersion) error.value = message(cause); }
  finally { if (version === loadVersion) busy.value = false; }
}
async function save() {
  const selected = selection.value[0];
  if (!selected || !canEdit.value || busy.value || serviceBusy.value) return;
  const invalidItem = selection.value.some(
    item => item.toothNumber !== selected.toothNumber || (item.surface && !(item.surface in SURFACE_METADATA))
  );
  if (!isValidFdiTooth(selected.toothNumber) || invalidItem) {
    error.value = 'Số răng hoặc mặt răng không hợp lệ.'; return;
  }
  if (!(code.value in FDI_CONDITIONS)) { error.value = 'Tình trạng không hợp lệ.'; return; }
  if (note.value.length > 500) { error.value = 'Ghi chú theo răng tối đa 500 ký tự.'; return; }
  const id = visitId.value;
  const version = loadVersion;
  busy.value = true; error.value = ''; success.value = '';
  try {
    const surface = selectedSurfaceCode.value;
    const isEditing = editingId.value !== null;
    const saved = editingId.value
      ? await fdiService.update(id, editingId.value, selected.toothNumber, surface, code.value, note.value.trim() || null)
      : await fdiService.add(id, selected.toothNumber, surface, code.value, note.value.trim() || null);
    if (id !== visitId.value || version !== loadVersion) return;
    if (isEditing) fdiStore.replace(rows.value.map(row => row.id === saved.id ? saved : row));
    else fdiStore.prepend(saved);
    const savedLatest = {
      id: saved.id,
      toothNumber: saved.toothNumber ?? selected.toothNumber,
      surface: saved.surface !== undefined ? saved.surface : surface,
      conditionCode: saved.conditionCode ?? code.value,
      createdAt: saved.createdAt ?? new Date().toISOString()
    };
    latestConditions.value = isEditing
      ? [...latestConditions.value.filter(item => item.id !== saved.id), savedLatest]
      : [...latestConditions.value, savedLatest];
    editingId.value = null; selection.value = [];
    note.value = '';
    const patientId = visit.value?.patientId;
    const latestLoaded = patientId ? await refreshLatestConditions(patientId, version, id, true) : false;
    if (id !== visitId.value || version !== loadVersion) return;
    success.value = latestLoaded
      ? 'Đã lưu tình trạng răng và cập nhật màu trạng thái mới nhất trên sơ đồ.'
      : 'Đã lưu tình trạng răng nhưng không tải lại được màu mới nhất; hãy tải lại hồ sơ.';
  } catch (cause) { if (id === visitId.value && version === loadVersion) error.value = message(cause); }
  finally { if (id === visitId.value && version === loadVersion) busy.value = false; }
}
async function remove(row: ToothCondition) {
  if (!canEdit.value || busy.value || serviceBusy.value || pendingDeleteId.value !== row.id) return;
  const id = visitId.value;
  const version = loadVersion;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await fdiService.delete(id, row.id);
    if (id !== visitId.value || version !== loadVersion) return;
    fdiStore.replace(rows.value.filter(item => item.id !== row.id));
    latestConditions.value = latestConditions.value.filter(item => item.id !== row.id);
    pendingDeleteId.value = null;
    if (editingId.value === row.id) cancelEditing();
    const latestLoaded = visit.value ? await refreshLatestConditions(visit.value.patientId, version, id, true) : false;
    if (id !== visitId.value || version !== loadVersion) return;
    success.value = latestLoaded
      ? 'Đã xóa bản ghi và cập nhật màu trạng thái mới nhất trên sơ đồ.'
      : 'Đã xóa bản ghi nhưng không tải lại được màu mới nhất; hãy tải lại hồ sơ.';
  } catch (cause) { if (id === visitId.value && version === loadVersion) error.value = message(cause); }
  finally { if (id === visitId.value && version === loadVersion) busy.value = false; }
}
watch(() => route.query?.tab, (tab) => {
  switchTab(tab === 'services' ? 'services' : 'conditions');
});
watch(visitId, load, { immediate: true });
onUnmounted(() => {
  loadVersion++; fdiStore.reset(); latestConditions.value = [];
  serviceRows.value = []; catalog.value = []; serviceId.value = null;
  serviceKeyword.value = ''; appliedKeyword.value = ''; searchError.value = '';
  catalogError.value = ''; deleteServiceId.value = null; deleteServiceReason.value = '';
  serviceBusy.value = false;
});
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
  flex-wrap: wrap;
  gap: 0.6rem;
  font-size: 0.85rem;
  margin-bottom: 0.75rem;
}

.back-link {
  color: #1857b6;
  text-decoration: none;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  min-height: 36px;
  padding: 0.3rem 0.7rem;
  border-radius: 6px;
}
.back-link.primary-back {
  background: #176864;
  color: #ffffff;
}
.back-link:hover { filter: brightness(0.95); text-decoration: none; }
.quick-nav-pill {
  display: inline-flex;
  align-items: center;
  min-height: 36px;
  padding: 0.3rem 0.65rem;
  border-radius: 6px;
  background: #e0f2fe;
  color: #0284c7;
  font-weight: 600;
  text-decoration: none;
}
.quick-nav-pill.alt {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}
.quick-nav-pill:hover { filter: brightness(0.95); text-decoration: none; }

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

.form-field select, .form-field textarea, .form-field input {
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

.workspace-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin: 0.9rem 0 0.75rem;
  border-bottom: 2px solid #e2e8f0;
  padding-bottom: 0.6rem;
}

.workspace-tab-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  min-height: 44px;
  padding: 0.55rem 1.05rem;
  border: 1px solid #cbd5e1;
  border-radius: 9px;
  background: #f8fafc;
  color: #334155;
  font-weight: 700;
  font-size: 0.92rem;
  cursor: pointer;
  transition: all 0.15s ease;
}

.workspace-tab-btn:hover {
  background: #eff6ff;
  border-color: #93c5fd;
  color: #1d4ed8;
}

.workspace-tab-btn.active {
  background: #102e56;
  border-color: #102e56;
  color: #ffffff;
}

.tab-count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.45rem;
  height: 1.45rem;
  padding: 0 0.35rem;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.25);
  font-size: 0.78rem;
  font-weight: 700;
}

.workspace-tab-btn.active .tab-count-badge {
  background: rgba(255, 255, 255, 0.22);
  color: #ffffff;
}

.tab-amount-badge {
  font-size: 0.8rem;
  font-weight: 700;
  padding: 0.12rem 0.45rem;
  border-radius: 999px;
  background: #dcfce7;
  color: #166534;
}

.clinical-work-grid.single-column {
  grid-template-columns: 1fr;
}

.general-toggle-row {
  margin-bottom: 0.75rem;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  color: #102e56;
  cursor: pointer;
}

.checkbox-label input {
  width: 18px;
  height: 18px;
}

.service-search-form {
  display: grid;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.85rem;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
}

.search-field-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #18354c;
  display: grid;
  gap: 0.35rem;
}

.search-field-label textarea {
  width: 100%;
  padding: 0.5rem 0.7rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
}

.search-input-group {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.search-input-group input {
  flex: 1 1 200px;
  min-height: 44px;
  padding: 0.55rem 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
}

.applied-filter-tag {
  margin: 0;
  font-size: 0.82rem;
  color: #1857b6;
}

.catalog-pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.catalog-pagination button {
  min-height: 36px;
  padding: 0.35rem 0.7rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  background: #ffffff;
  color: #18354c;
  font-size: 0.82rem;
  font-weight: 600;
  cursor: pointer;
}

.catalog-pagination button:hover:not(:disabled) { background: #f1f5f9; }
.catalog-pagination button:disabled { opacity: 0.5; cursor: not-allowed; }

.page-indicator {
  font-size: 0.82rem;
  font-weight: 700;
  color: #475569;
  padding: 0 0.25rem;
}

.card-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.5rem;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 0.5rem;
  margin-bottom: 1rem;
}

.card-title.no-border {
  border-bottom: 0;
  padding-bottom: 0;
  margin-bottom: 0;
}

.assigned-grand-total {
  font-size: 0.92rem;
  color: #0f766e;
}

.row-header-split {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.service-total-pill {
  font-weight: 700;
  color: #0f766e;
  font-size: 0.95rem;
}

.service-tooth-meta,
.service-calc {
  margin: 0;
  font-size: 0.85rem;
  color: #334155;
}

.service-delete-form {
  width: 100%;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 8px;
  padding: 0.75rem;
  display: grid;
  gap: 0.5rem;
}

.service-delete-warning {
  margin: 0;
  font-size: 0.82rem;
  color: #991b1b;
  font-weight: 600;
}
</style>
