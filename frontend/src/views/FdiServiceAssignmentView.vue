<template>
  <main class="assignment-page">
    <RouterLink :to="`${portalPrefix}/clinical/visits/${visitId}/fdi`">← Sơ đồ và tình trạng răng</RouterLink>
    <p v-if="visit?.status === 'Correcting'">Đang đính chính hồ sơ ngoài ca khám. <RouterLink :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`">Trở lại để hoàn tất và khóa đính chính</RouterLink></p>
    <h1>Chỉ định dịch vụ · Lần khám #{{ visitId }}</h1>
    <RouterLink v-if="!busy && ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`">Hoàn tất khám / Xem hóa đơn nháp</RouterLink>
    <button type="button" :disabled="busy" @click="load">Tải lại hồ sơ</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <template v-if="visit">
      <p>Trạng thái: {{ visit.status }}</p>
      <p v-if="!canEdit">Chỉ Admin hoặc nha sĩ phụ trách được chỉ định khi lần khám đang diễn ra và chưa khóa.</p>
      <template v-if="canEdit">
        <label><input v-model="general" type="checkbox" :disabled="busy" /> Dịch vụ toàn hàm / tổng quát</label>
        <FdiDentalChart v-model="selection" :multiple="true" :readonly="busy || general" />
        <p v-if="!general">Răng được chỉ định: {{ selectedTeeth.join(', ') || 'Chưa chọn' }}. Mỗi răng tạo một dòng dịch vụ; mặt điều trị chọn bên dưới.</p>
        <form aria-label="Tìm dịch vụ để chỉ định" @submit.prevent="searchCatalog">
          <label for="service-keyword">Tìm dịch vụ theo tên hoặc mã (tối đa 100 ký tự)</label>
          <input id="service-keyword" v-model="keyword" maxlength="100" :disabled="busy" />
          <div class="pagination">
            <button type="submit" :disabled="busy">Tìm dịch vụ</button>
            <button type="button" :disabled="busy" @click="clearSearch">Xóa tìm kiếm</button>
          </div>
          <p v-if="searchError" class="error" role="alert">{{ searchError }}</p>
          <p v-if="appliedKeyword">Đang lọc theo: {{ appliedKeyword }}</p>
        </form>
        <form @submit.prevent="assign">
          <label>Dịch vụ
            <select v-model="serviceId" :disabled="busy || Boolean(catalogError)" required>
              <option :value="null" disabled>Chọn dịch vụ</option>
              <option v-for="service in catalog" :key="service.dentalServiceId" :value="service.dentalServiceId" :disabled="service.currentPrice === null">
                {{ service.code }} · {{ service.name }} · {{ service.currentPrice === null ? 'Chưa có giá hiệu lực' : money(service.currentPrice) }}
              </option>
            </select>
          </label>
          <div class="pagination">
            <button type="button" :disabled="busy || catalogPage <= 1" @click="loadCatalog(catalogPage - 1)">Trang dịch vụ trước</button>
            <span>{{ catalogPage }}/{{ Math.max(catalogPages, 1) }}</span>
            <button type="button" :disabled="busy || catalogPage >= catalogPages" @click="loadCatalog(catalogPage + 1)">Trang dịch vụ sau</button>
            <button type="button" :disabled="busy" @click="loadCatalog(catalogPage)">Tải lại dịch vụ</button>
          </div>
          <p v-if="catalogError" class="error" role="alert">{{ catalogError }}</p>
          <p v-if="!busy && !catalogError && !catalog.length">Không có dịch vụ phù hợp. Thử tên/mã khác hoặc xóa tìm kiếm.</p>
          <label v-if="!general">Mặt áp dụng cho mọi răng đã chọn
            <select v-model="surface" :disabled="busy">
              <option :value="null">Toàn răng</option>
              <option v-for="item in SURFACE_METADATA" :key="item.code" :value="item.code">{{ item.nameVi }}</option>
            </select>
          </label>
          <label>Số lượng trên mỗi dòng
            <input v-model.number="quantity" :disabled="busy" type="number" min="1" max="100" step="1" required />
          </label>
          <p>Đơn giá hiển thị để tham khảo; hệ thống chốt giá đang hiệu lực khi lưu chỉ định.</p>
          <button type="submit" :disabled="busy || !serviceId || Boolean(catalogError) || (!general && !selectedTeeth.length)">Lưu chỉ định</button>
        </form>
      </template>
      <h2>Dịch vụ đã chỉ định</h2>
      <p v-if="!rows.length">Chưa có dịch vụ được chỉ định.</p>
      <ul>
        <li v-for="row in rows" :key="row.id">
          <strong>{{ row.serviceCode }} · {{ row.serviceName }}</strong>
          <p>{{ row.toothNumber === null ? 'Toàn hàm / tổng quát' : `Răng ${row.toothNumber} · ${row.surface || 'Toàn răng'}` }}</p>
          <p>{{ row.quantity }} × {{ money(row.unitPrice) }} = {{ money(row.totalAmount) }} (giá đã chốt)</p>
          <small>{{ new Date(row.createdAt).toLocaleString('vi-VN') }}</small>
          <button v-if="canEdit" type="button" :disabled="busy" @click="beginDelete(row.id)">Xóa chỉ định</button>
          <form v-if="deleteId === row.id && canEdit" @submit.prevent="remove">
            <p>Xác nhận xóa dòng chỉ định này. Hóa đơn chưa thu sẽ giảm tương ứng; thao tác được lưu nhật ký.</p>
            <label>Lý do xóa (không nhập thông tin cá nhân bệnh nhân)
              <textarea v-model="deleteReason" required maxlength="500" :disabled="busy" />
            </label>
            <button type="submit" :disabled="busy || !deleteReason.trim()">Xác nhận xóa</button>
            <button type="button" :disabled="busy" @click="deleteId = null">Hủy</button>
          </form>
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
import { SURFACE_METADATA, ADULT_TEETH, CHILD_TEETH, type SelectedToothSurface } from '@/types/fdi';
import { visitsService } from '@/services/visits';
import { visitServicesApi, type CatalogOption } from '@/services/visitServices';
import type { VisitDetails } from '@/services/patients';
import { useAuthStore } from '@/stores/auth';
import { useVisitServicesStore } from '@/stores/visitServices';

const route = useRoute();
const auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
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
.assignment-page { max-width: 1100px; margin: 2rem auto; padding: 1rem; }
form, label { display: grid; gap: .6rem; margin: 1rem 0; }
button, select, input { padding: .6rem; }
.pagination { display: flex; flex-wrap: wrap; align-items: center; gap: .6rem; }
li { margin: 1rem 0; }
.error { color: #b91c1c; }
small { color: #64748b; }
</style>
