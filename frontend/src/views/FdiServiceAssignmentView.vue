<template>
  <main class="assignment-page">
    <div class="breadcrumb-bar">
      <RouterLink :to="`${portalPrefix}/clinical/visits/${visitId}/fdi`" class="back-link">← Sơ đồ và tình trạng răng</RouterLink>
      <span class="breadcrumb-current">Lâm sàng › Chỉ định dịch vụ theo răng</span>
    </div>

    <div v-if="visit?.status === 'Correcting'" class="correcting-alert">
      <span>Đang đính chính hồ sơ ngoài ca khám.</span>
      <RouterLink :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`">Trở lại để hoàn tất và khóa đính chính</RouterLink>
    </div>

    <div class="page-header-row">
      <div>
        <h1 class="page-title">Chỉ định dịch vụ · Lần khám #{{ visitId }}</h1>
        <p v-if="visit" class="page-subtitle">Bệnh nhân #{{ visit.patientId }} · Trạng thái: <strong>{{ visit.status }}</strong></p>
      </div>
      <div class="header-actions">
        <RouterLink v-if="!busy && ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`" class="btn-secondary">Hoàn tất khám / Xem hóa đơn nháp →</RouterLink>
        <button type="button" class="btn-refresh" :disabled="busy" @click="load">Tải lại hồ sơ</button>
      </div>
    </div>

    <p v-if="busy" role="status" class="status-msg">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status" class="success-msg">{{ success }}</p>
    <template v-if="visit">
      <SafetyAlertBanner v-if="isStaff" :patient-id="visit.patientId" />

      <p v-if="!canEdit" class="readonly-notice">Chỉ Admin hoặc nha sĩ phụ trách được chỉ định khi lần khám đang diễn ra và chưa khóa.</p>
      <template v-if="canEdit">
        <div class="general-toggle-row">
          <label class="checkbox-label"><input v-model="general" type="checkbox" :disabled="busy" /> <span>Dịch vụ toàn hàm / tổng quát</span></label>
        </div>
        <div class="chart-wrapper-card">
          <FdiDentalChart v-model="selection" :multiple="true" :readonly="busy || general" />
        </div>
        <p v-if="!general" class="selected-teeth-hint">Răng được chỉ định: <strong>{{ selectedTeeth.join(', ') || 'Chưa chọn' }}</strong>. Mỗi răng tạo một dòng dịch vụ; mặt điều trị chọn bên dưới.</p>
        <div class="assignment-grid">
          <section class="card form-card" aria-label="Biểu mẫu chỉ định dịch vụ">
            <h2 class="card-title">Thêm chỉ định mới</h2>

            <form aria-label="Tìm dịch vụ để chỉ định" class="search-form" @submit.prevent="searchCatalog">
              <label for="service-keyword" class="field-label">Tìm dịch vụ theo tên hoặc mã (tối đa 100 ký tự)</label>
              <div class="search-input-group">
                <input id="service-keyword" v-model="keyword" maxlength="100" placeholder="VD: Trám răng, Nhổ răng, Cạo vôi..." :disabled="busy" />
                <button type="submit" class="btn-primary" :disabled="busy">Tìm dịch vụ</button>
                <button type="button" class="btn-cancel" :disabled="busy" @click="clearSearch">Xóa tìm kiếm</button>
              </div>
              <p v-if="searchError" class="error" role="alert">{{ searchError }}</p>
              <p v-if="appliedKeyword" class="applied-filter-tag">Đang lọc theo: <strong>{{ appliedKeyword }}</strong></p>
            </form>

            <form class="assign-form" @submit.prevent="assign">
              <div class="form-field">
                <label for="service-select" class="field-label">Dịch vụ</label>
                <select id="service-select" v-model="serviceId" :disabled="busy || Boolean(catalogError)" required>
                  <option :value="null" disabled>Chọn dịch vụ</option>
                  <option v-for="service in catalog" :key="service.dentalServiceId" :value="service.dentalServiceId" :disabled="service.currentPrice === null">
                    {{ service.code }} · {{ service.name }} · {{ service.currentPrice === null ? 'Chưa có giá hiệu lực' : money(service.currentPrice) }}
                  </option>
                </select>
              </div>

              <div class="pagination">
                <button type="button" :disabled="busy || catalogPage <= 1" @click="loadCatalog(catalogPage - 1)">Trang dịch vụ trước</button>
                <span class="page-indicator">{{ catalogPage }}/{{ Math.max(catalogPages, 1) }}</span>
                <button type="button" :disabled="busy || catalogPage >= catalogPages" @click="loadCatalog(catalogPage + 1)">Trang dịch vụ sau</button>
                <button type="button" :disabled="busy" @click="loadCatalog(catalogPage)">Tải lại dịch vụ</button>
              </div>

              <p v-if="catalogError" class="error" role="alert">{{ catalogError }}</p>
              <p v-if="!busy && !catalogError && !catalog.length" class="empty-hint">Không có dịch vụ phù hợp. Thử tên/mã khác hoặc xóa tìm kiếm.</p>

              <div v-if="!general" class="form-field">
                <label for="surface-select" class="field-label">Mặt áp dụng cho mọi răng đã chọn</label>
                <select id="surface-select" v-model="surface" :disabled="busy">
                  <option :value="null">Toàn răng</option>
                  <option v-for="item in SURFACE_METADATA" :key="item.code" :value="item.code">{{ item.nameVi }}</option>
                </select>
              </div>

              <div class="form-field">
                <label for="quantity-input" class="field-label">Số lượng trên mỗi dòng</label>
                <input id="quantity-input" v-model.number="quantity" :disabled="busy" type="number" min="1" max="100" step="1" required />
              </div>

              <p class="price-hint">Đơn giá hiển thị để tham khảo; hệ thống chốt giá đang hiệu lực khi lưu chỉ định.</p>
              <button type="submit" class="btn-primary btn-submit-assign" :disabled="busy || !serviceId || Boolean(catalogError) || (!general && !selectedTeeth.length)">Lưu chỉ định</button>
            </form>
          </section>
          <section class="card rows-card" aria-label="Dịch vụ đã chỉ định">
            <h2 class="card-title">Dịch vụ đã chỉ định ({{ rows.length }})</h2>
            <p v-if="!rows.length" class="empty-hint">Chưa có dịch vụ được chỉ định.</p>
            <ul v-else class="assigned-row-list">
              <li v-for="row in rows" :key="row.id" class="assigned-row-item">
                <div class="row-header">
                  <strong class="row-service-name">{{ row.serviceCode }} · {{ row.serviceName }}</strong>
                  <span class="row-total-price">{{ money(row.totalAmount) }}</span>
                </div>
                <p class="row-tooth-meta">{{ row.toothNumber === null ? 'Toàn hàm / tổng quát' : `Răng ${row.toothNumber} · ${row.surface || 'Toàn răng'}` }}</p>
                <p class="row-calc">{{ row.quantity }} × {{ money(row.unitPrice) }} = {{ money(row.totalAmount) }} (giá đã chốt)</p>
                <small class="row-time">{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>

                <div v-if="canEdit" class="row-action-area">
                  <button v-if="deleteId !== row.id" type="button" class="btn-delete" :disabled="busy" @click="beginDelete(row.id)">Xóa chỉ định</button>
                  <form v-if="deleteId === row.id" class="delete-form" @submit.prevent="remove">
                    <p class="delete-warning">Xác nhận xóa dòng chỉ định này. Hóa đơn chưa thu sẽ giảm tương ứng; thao tác được lưu nhật ký.</p>
                    <label class="field-label">Lý do xóa (không nhập thông tin cá nhân bệnh nhân)
                      <textarea v-model="deleteReason" required maxlength="500" :disabled="busy" />
                    </label>
                    <div class="delete-form-actions">
                      <button type="submit" class="btn-danger" :disabled="busy || !deleteReason.trim()">Xác nhận xóa</button>
                      <button type="button" class="btn-cancel" :disabled="busy" @click="deleteId = null">Hủy</button>
                    </div>
                  </form>
                </div>
              </li>
            </ul>
          </section>
        </div>
      </template>

      <section v-else class="card rows-card" aria-label="Dịch vụ đã chỉ định">
        <h2 class="card-title">Dịch vụ đã chỉ định ({{ rows.length }})</h2>
        <p v-if="!rows.length" class="empty-hint">Chưa có dịch vụ được chỉ định.</p>
        <ul v-else class="assigned-row-list">
          <li v-for="row in rows" :key="row.id" class="assigned-row-item">
            <div class="row-header">
              <strong class="row-service-name">{{ row.serviceCode }} · {{ row.serviceName }}</strong>
              <span class="row-total-price">{{ money(row.totalAmount) }}</span>
            </div>
            <p class="row-tooth-meta">{{ row.toothNumber === null ? 'Toàn hàm / tổng quát' : `Răng ${row.toothNumber} · ${row.surface || 'Toàn răng'}` }}</p>
            <p class="row-calc">{{ row.quantity }} × {{ money(row.unitPrice) }} = {{ money(row.totalAmount) }} (giá đã chốt)</p>
            <small class="row-time">{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>
          </li>
        </ul>
      </section>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useRoute } from 'vue-router';
import axios from 'axios';
import FdiDentalChart from '@/components/fdi/FdiDentalChart.vue';
import SafetyAlertBanner from '@/components/SafetyAlertBanner.vue';
import { SURFACE_METADATA, ADULT_TEETH, CHILD_TEETH, type SelectedToothSurface } from '@/types/fdi';
import { visitsService } from '@/services/visits';
import { visitServicesApi, type CatalogOption } from '@/services/visitServices';
import type { VisitDetails } from '@/services/patients';
import { useAuthStore } from '@/stores/auth';
import { useVisitServicesStore } from '@/stores/visitServices';

const route = useRoute();
const auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
const isStaff = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'].includes(auth.role));
const store = useVisitServicesStore();
const { rows } = storeToRefs(store);
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const busy = ref(false);
const error = ref('');
const success = ref('');
const deleteId = ref<number | null>(null);
const deleteReason = ref('');
function beginDelete(id: number) {
  if (!canEdit.value || busy.value) return;
  deleteId.value = id; deleteReason.value = ''; error.value = ''; success.value = '';
}
async function remove() {
  if (!canEdit.value || busy.value || deleteId.value === null) return;
  const id = deleteId.value, reason = deleteReason.value.trim();
  if (!Number.isInteger(id) || id <= 0 || !reason || reason.length > 500) {
    error.value = 'Nhập lý do xóa từ 1 đến 500 ký tự.'; return;
  }
  const ticket = version, currentVisitId = visitId.value;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await visitServicesApi.remove(currentVisitId, id, reason);
    if (ticket !== version) return;
    store.replace(rows.value.filter(row => row.id !== id)); deleteId.value = null; deleteReason.value = '';
    success.value = 'Đã xóa chỉ định và cập nhật hóa đơn chưa thu. Tải lại để đối chiếu hồ sơ.';
  } catch (cause) {
    if (ticket === version) { error.value = message(cause); deleteId.value = null; deleteReason.value = ''; }
  } finally { if (ticket === version) busy.value = false; }
}
const selection = ref<SelectedToothSurface[]>([]);
const general = ref(false);
const surface = ref<string | null>(null);
const quantity = ref(1);
const serviceId = ref<number | null>(null);
const catalog = ref<CatalogOption[]>([]);
const catalogPage = ref(1);
const catalogPages = ref(1);
const catalogError = ref('');
const keyword = ref(''), appliedKeyword = ref(''), searchError = ref('');
const selectedTeeth = computed(() => [...new Set(selection.value.map(x => x.toothNumber))].sort((a, b) => a - b));
const canEdit = computed(() => ['InProgress', 'Correcting'].includes(visit.value?.status ?? '') && !visit.value?.isLocked &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
function message(cause: unknown) {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể tải hoặc lưu dịch vụ. Vui lòng thử lại.' : 'Không thể xử lý yêu cầu.';
}
let version = 0;
async function fetchCatalog(page: number, ticket: number) {
  catalogError.value = ''; catalog.value = []; serviceId.value = null;
  try {
    const data = await visitServicesApi.catalog(page, appliedKeyword.value);
    if (ticket !== version) return;
    catalog.value = data.items; catalogPage.value = page; catalogPages.value = data.totalPages;
  } catch (cause) { if (ticket === version) catalogError.value = message(cause); }
}
async function searchCatalog() {
  if (busy.value || !canEdit.value) return;
  if (keyword.value.length > 100) { searchError.value = 'Từ khóa không được vượt quá 100 ký tự.'; return; }
  searchError.value = ''; appliedKeyword.value = keyword.value.trim();
  await loadCatalog(1);
}
async function clearSearch() {
  if (busy.value || !canEdit.value) return;
  keyword.value = ''; await searchCatalog();
}
async function loadCatalog(page: number) {
  if (busy.value || !canEdit.value || !Number.isInteger(page) || page < 1 || page > 1_000_000) return;
  const ticket = version; busy.value = true;
  try { await fetchCatalog(page, ticket); }
  finally { if (ticket === version) busy.value = false; }
}
async function load() {
  const ticket = ++version;
  busy.value = true; error.value = ''; success.value = ''; visit.value = null; store.reset();
  selection.value = []; serviceId.value = null; general.value = false; surface.value = null; quantity.value = 1;
  deleteId.value = null; deleteReason.value = '';
  catalog.value = []; catalogError.value = ''; keyword.value = ''; appliedKeyword.value = ''; searchError.value = '';
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) { error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return; }
  try {
    const [details, records] = await Promise.all([visitsService.getById(visitId.value), visitServicesApi.get(visitId.value)]);
    if (ticket !== version) return;
    visit.value = details; store.replace(records);
    if (canEdit.value) await fetchCatalog(1, ticket);
  } catch (cause) { if (ticket === version) error.value = message(cause); }
  finally { if (ticket === version) busy.value = false; }
}
async function assign() {
  if (!canEdit.value || busy.value || catalogError.value) return;
  const option = catalog.value.find(x => x.dentalServiceId === serviceId.value);
  const teeth = general.value ? null : selectedTeeth.value;
  const allowed = [...ADULT_TEETH, ...CHILD_TEETH];
  if (!option || option.currentPrice === null || !Number.isInteger(quantity.value) || quantity.value < 1 || quantity.value > 100 ||
      teeth && (!teeth.length || teeth.length > 52 || teeth.some(x => !allowed.includes(x))) ||
      !general.value && surface.value !== null && !Object.values(SURFACE_METADATA).some(x => x.code === surface.value)) {
    error.value = 'Kiểm tra lại dịch vụ, số lượng và răng/mặt răng.'; return;
  }
  const ticket = version; busy.value = true; error.value = ''; success.value = '';
  try {
    const saved = await visitServicesApi.assign(visitId.value, { serviceId: option.dentalServiceId, toothNumbers: teeth,
      surface: general.value ? null : surface.value, quantity: quantity.value });
    if (ticket !== version) return;
    store.prepend(saved); selection.value = []; serviceId.value = null;
    success.value = `Đã lưu ${saved.length} dòng dịch vụ và chốt đơn giá. Tải lại để xem dữ liệu đã lưu.`;
  } catch (cause) { if (ticket === version) error.value = message(cause); }
  finally { if (ticket === version) busy.value = false; }
}
watch(visitId, load, { immediate: true });
onUnmounted(() => { version++; store.reset(); visit.value = null; catalog.value = []; serviceId.value = null; keyword.value = ''; appliedKeyword.value = ''; searchError.value = ''; selection.value = []; error.value = ''; success.value = ''; deleteId.value = null; deleteReason.value = ''; });
</script>

<style scoped>
.assignment-page {
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

.general-toggle-row {
  margin: 1rem 0;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  color: #102e56;
  cursor: pointer;
  min-height: 44px;
}

.chart-wrapper-card {
  margin-bottom: 1rem;
}

.selected-teeth-hint {
  font-size: 0.9rem;
  color: #475569;
  margin-bottom: 1.25rem;
}
.selected-teeth-hint strong {
  color: #1857b6;
}

.assignment-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
  align-items: start;
}

@media (max-width: 900px) {
  .assignment-grid {
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

.field-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #18354c;
  display: block;
  margin-bottom: 0.35rem;
}

.search-form {
  margin-bottom: 1.25rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #f1f5f9;
}

.search-input-group {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.search-input-group input {
  flex: 1;
  min-width: 180px;
  min-height: 44px;
  padding: 0.6rem 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
}

.applied-filter-tag {
  font-size: 0.85rem;
  color: #102e56;
  background: #d8ecfa;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  display: inline-block;
  margin-top: 0.5rem;
}

.assign-form {
  display: grid;
  gap: 1rem;
}

.form-field {
  display: grid;
  gap: 0.25rem;
}

.form-field select, .form-field input {
  width: 100%;
  padding: 0.65rem 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
  min-height: 44px;
}

.pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.pagination button {
  min-height: 38px;
  padding: 0.45rem 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #f8fafc;
  cursor: pointer;
}
.pagination button:hover:not(:disabled) { background: #e2e8f0; }
.pagination button:disabled { opacity: 0.55; cursor: not-allowed; }

.page-indicator {
  font-size: 0.85rem;
  font-weight: 700;
  color: #18354c;
  padding: 0 0.4rem;
}

.price-hint {
  font-size: 0.8rem;
  color: #64748b;
  margin: 0;
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

.btn-submit-assign {
  width: 100%;
}
.empty-hint {
  color: #64748b;
  font-style: italic;
  font-size: 0.9rem;
}

.assigned-row-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.85rem;
}

.assigned-row-item {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1rem;
  background: #f8fafc;
  display: grid;
  gap: 0.4rem;
}

.row-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.row-service-name {
  color: #102e56;
  font-size: 0.95rem;
}

.row-total-price {
  color: #1857b6;
  font-weight: 700;
  font-size: 1rem;
}

.row-tooth-meta {
  margin: 0;
  font-size: 0.85rem;
  color: #334155;
  font-weight: 600;
}

.row-calc {
  margin: 0;
  font-size: 0.8rem;
  color: #64748b;
}

.row-time {
  font-size: 0.75rem;
  color: #94a3b8;
}

.row-action-area {
  margin-top: 0.4rem;
}

.btn-delete {
  min-height: 38px;
  padding: 0.45rem 0.75rem;
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: 6px;
  border: 1px solid #fecaca;
  background: #fef2f2;
  color: #b91c1c;
  cursor: pointer;
}
.btn-delete:hover:not(:disabled) { background: #fee2e2; }

.delete-form {
  background: #fff5f5;
  border: 1px solid #fecaca;
  border-radius: 8px;
  padding: 0.85rem;
  margin-top: 0.5rem;
  display: grid;
  gap: 0.5rem;
}

.delete-warning {
  margin: 0;
  font-size: 0.85rem;
  color: #991b1b;
}

.delete-form textarea {
  width: 100%;
  min-height: 70px;
  padding: 0.5rem;
  border: 1px solid #fca5a5;
  border-radius: 6px;
  background: #fff;
}

.delete-form-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-danger {
  min-height: 40px;
  padding: 0.5rem 1rem;
  background: #dc2626;
  color: #fff;
  border: 1px solid #b91c1c;
  border-radius: 6px;
  font-weight: 700;
  cursor: pointer;
}
.btn-danger:hover:not(:disabled) { background: #b91c1c; }
.btn-danger:disabled { opacity: 0.55; cursor: not-allowed; }
</style>
