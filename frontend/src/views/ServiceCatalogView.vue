<template>
  <main class="catalog-page">
    <header class="page-heading">
      <div>
        <p class="eyebrow">DANH MỤC KỸ THUẬT & TÀI CHÍNH</p>
        <h1>Dịch vụ & Bảng giá điều trị</h1>
        <p class="subtitle">
          Quản lý danh mục kỹ thuật nha khoa và lịch sử bảng giá. Giá mới được lưu bản ghi độc lập, bảo toàn đơn giá các hồ sơ bệnh án và hóa đơn trước đó.
        </p>
      </div>
      <button
        type="button"
        class="button button-primary"
        :disabled="busy || reading || needsReload"
        @click="startCreate"
      >
        <span class="btn-icon">＋</span> Thêm dịch vụ mới
      </button>
    </header>

    <p v-if="error" role="alert" class="alert alert-error">{{ error }}</p>
    <p v-if="success" role="status" class="alert alert-success">{{ success }}</p>
    <p v-if="busy || reading" role="status" class="alert alert-info">Đang xử lý dữ liệu dịch vụ…</p>

    <!-- Thanh tìm kiếm & lọc -->
    <form class="panel filters-panel" @submit.prevent="load(1)">
      <div class="filters-grid">
        <label class="filter-field search-field">
          <span>Tìm kiếm theo mã hoặc tên</span>
          <div class="input-icon-wrapper">
            <span class="field-icon">🔍</span>
            <input
              v-model="keyword"
              type="search"
              maxlength="100"
              placeholder="Nhập mã kỹ thuật hoặc tên dịch vụ..."
              :disabled="busy || reading"
            />
          </div>
        </label>
        <label class="filter-field">
          <span>Trạng thái sử dụng</span>
          <select v-model="activeFilter" :disabled="busy || reading">
            <option value="all">Tất cả trạng thái</option>
            <option value="true">Đang sử dụng</option>
            <option value="false">Đã ngừng dùng</option>
          </select>
        </label>
        <div class="filter-actions">
          <button class="button button-secondary" :disabled="busy || reading">
            <span class="btn-icon">🔄</span> Tìm kiếm / Tải lại
          </button>
          <button
            v-if="needsReload"
            type="button"
            class="button button-warning"
            :disabled="busy || reading"
            @click="load(page)"
          >
            Tải lại đối chiếu
          </button>
        </div>
      </div>
    </form>

    <!-- Layout Master - Detail 2 cột -->
    <div class="catalog-layout">
      <!-- Cột trái: Danh sách dịch vụ Master -->
      <section class="panel list-panel" aria-label="Danh sách dịch vụ kỹ thuật">
        <div class="panel-header">
          <h2 class="panel-title">Danh mục kỹ thuật nha khoa</h2>
          <span v-if="pageResult" class="count-badge">{{ pageResult.totalCount }} dịch vụ</span>
        </div>

        <div v-if="pageResult && !pageResult.items.length" class="empty-state">
          Không tìm thấy dịch vụ nha khoa phù hợp.
        </div>

        <div v-else class="services-list">
          <div
            v-for="item in pageResult?.items ?? []"
            :key="item.dentalServiceId"
            class="service-item"
            :class="{ 'service-item--active': detail?.dentalServiceId === item.dentalServiceId }"
            @click="select(item.dentalServiceId)"
          >
            <div class="service-main">
              <div class="service-code-row">
                <span class="service-code">{{ item.code }}</span>
                <span
                  class="status-pill"
                  :class="item.isActive ? 'status-pill--active' : 'status-pill--inactive'"
                >
                  {{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                </span>
              </div>
              <div class="service-name">{{ item.name }}</div>
            </div>
            <div class="service-price">
              {{ item.currentPrice === null ? 'Chưa áp giá' : money(item.currentPrice) }}
            </div>
          </div>
        </div>

        <!-- Phân trang -->
        <footer v-if="pageResult" class="pagination">
          <span class="page-info">Trang {{ page }} / {{ Math.max(1, pageResult.totalPages) }}</span>
          <div class="page-btns">
            <button
              class="button button-quiet"
              type="button"
              :disabled="busy || reading || page <= 1"
              @click="load(page - 1)"
            >
              Trước
            </button>
            <button
              class="button button-quiet"
              type="button"
              :disabled="busy || reading || page >= pageResult.totalPages"
              @click="load(page + 1)"
            >
              Sau
            </button>
          </div>
        </footer>
      </section>
      <!-- Cột phải: Form biên tập & Lịch sử giá Detail -->
      <section class="detail-column">
        <!-- Form thông tin dịch vụ -->
        <section v-if="mode" class="panel detail-panel" aria-label="Thông tin dịch vụ">
          <div class="panel-header">
            <h2 class="panel-title">{{ mode === 'create' ? 'Tạo mới dịch vụ kỹ thuật' : `Chi tiết: ${detail?.code} · ${detail?.name}` }}</h2>
          </div>
          <form class="detail-form" @submit.prevent="saveService">
            <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
              <div class="form-grid">
                <label class="form-field">
                  <span>Mã dịch vụ <b>*</b></span>
                  <input v-model="form.code" maxlength="50" :readonly="mode !== 'create'" placeholder="VD: NHAKHOA_01" required />
                </label>
                <label class="form-field">
                  <span>Thời lượng ca (phút) <b>*</b></span>
                  <input v-model="form.durationMinutes" type="number" min="1" max="2147483647" step="1" required />
                </label>
                <label class="form-field form-field--wide">
                  <span>Tên dịch vụ kỹ thuật <b>*</b></span>
                  <input v-model="form.name" maxlength="200" placeholder="VD: Cạo vôi răng & Đánh bóng" required />
                </label>
                <label class="form-field form-field--wide">
                  <span>Mô tả lâm sàng & chỉ định</span>
                  <textarea v-model="form.description" rows="3" maxlength="1000" placeholder="Nhập quy trình tóm tắt hoặc ghi chú..."></textarea>
                </label>
                <template v-if="mode === 'create'">
                  <label class="form-field">
                    <span>Đơn giá khởi tạo (VNĐ) <b>*</b></span>
                    <input v-model="form.initialPrice" type="number" min="0" step="1" placeholder="VD: 250000" required />
                  </label>
                  <label class="form-field">
                    <span>Hiệu lực từ ngày giờ <b>*</b></span>
                    <input v-model="form.effectiveFrom" type="datetime-local" :min="todayStart" required />
                  </label>
                </template>
              </div>

              <div class="confirm-box">
                <label class="checkbox-label">
                  <input v-model="serviceConfirmed" type="checkbox" />
                  <span>Tôi xác nhận các thông tin kỹ thuật của dịch vụ này là chính xác</span>
                </label>
              </div>

              <div class="form-actions">
                <button class="button button-primary" :disabled="!validService || !serviceConfirmed">
                  <span class="btn-icon">💾</span> {{ mode === 'create' ? 'Tạo dịch vụ' : 'Lưu thông tin' }}
                </button>
              </div>
            </fieldset>
          </form>
        </section>
        <!-- Khối lịch sử giá & trạng thái khi xem chi tiết -->
        <template v-if="detail">
          <!-- Bảng lịch sử giá -->
          <section class="panel detail-panel" aria-label="Lịch sử giá dịch vụ">
            <div class="panel-header">
              <h3 class="panel-title">Lịch sử bảng giá</h3>
              <div class="current-price-badge">
                Giá hiện hành: <strong>{{ detail.currentPrice === null ? 'Chưa áp giá' : money(detail.currentPrice) }}</strong>
              </div>
            </div>

            <div class="table-wrap">
              <table class="price-table">
                <thead>
                  <tr>
                    <th>Mức giá (VNĐ)</th>
                    <th>Thời điểm áp dụng</th>
                    <th>Người lập</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="price in detail.priceHistory ?? []" :key="price.servicePriceId">
                    <td><strong class="price-val">{{ money(price.amount) }}</strong></td>
                    <td>{{ date(price.effectiveFrom) }}</td>
                    <td><code>#{{ price.createdByUserId }}</code></td>
                  </tr>
                  <tr v-if="!(detail.priceHistory?.length)">
                    <td colspan="3" class="empty-cell">Chưa có lịch sử biến động giá.</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <form class="price-form" @submit.prevent="addPrice">
              <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
                <legend class="fieldset-legend">Thiết lập mốc giá mới (Bảo toàn lịch sử)</legend>
                <div class="form-grid">
                  <label class="form-field">
                    <span>Mức giá mới (VNĐ) <b>*</b></span>
                    <input v-model="priceForm.amount" type="number" min="0" step="1" placeholder="VD: 300000" required />
                  </label>
                  <label class="form-field">
                    <span>Có hiệu lực từ (UTC+7) <b>*</b></span>
                    <input v-model="priceForm.effectiveFrom" type="datetime-local" :min="todayStart" required />
                  </label>
                </div>
                <p class="form-hint">
                  Mốc hiệu lực từ hôm nay và không được trước mốc đã lên lịch gần nhất: <strong>{{ latestPriceDate ? date(latestPriceDate) : 'Chưa có' }}</strong>.
                </p>
                <div class="confirm-box">
                  <label class="checkbox-label">
                    <input v-model="priceConfirmed" type="checkbox" />
                    <span>Tôi xác nhận thêm bản ghi đơn giá mới vào danh mục</span>
                  </label>
                </div>
                <div class="form-actions">
                  <button class="button button-primary" :disabled="!validPrice || !priceConfirmed">
                    <span class="btn-icon">➕</span> Cập nhật mức giá mới
                  </button>
                </div>
              </fieldset>
            </form>
          </section>

          <!-- Khối bật/tắt trạng thái sử dụng -->
          <section class="panel detail-panel" aria-label="Quản lý trạng thái hoạt động">
            <div class="panel-header">
              <h3 class="panel-title">Trạng thái kỹ thuật</h3>
              <span
                class="status-pill"
                :class="detail.isActive ? 'status-pill--active' : 'status-pill--inactive'"
              >
                {{ detail.isActive ? 'Đang phục vụ điều trị' : 'Đã tạm ngừng sử dụng' }}
              </span>
            </div>
            <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
              <div class="confirm-box">
                <label class="checkbox-label">
                  <input v-model="statusConfirmed" type="checkbox" />
                  <span>Tôi xác nhận {{ detail.isActive ? 'tạm ngừng cung cấp' : 'kích hoạt phục vụ trở lại' }} dịch vụ này</span>
                </label>
              </div>
              <div class="form-actions">
                <button
                  type="button"
                  class="button"
                  :class="detail.isActive ? 'button-danger' : 'button-success'"
                  :disabled="!statusConfirmed"
                  @click="toggleActive"
                >
                  {{ detail.isActive ? 'Ngừng sử dụng dịch vụ' : 'Kích hoạt lại dịch vụ' }}
                </button>
              </div>
            </fieldset>
          </section>
        </template>
      </section>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { useServiceCatalogStore } from '@/stores/serviceCatalog';
import { serviceCatalogApi, clinicDateTimeInput, clinicInputToIso, validCatalogPrice, validCatalogEffective } from '@/services/serviceCatalog';
import type { CatalogDetail } from '@/services/serviceCatalog';

const auth = useAuthStore(), store = useServiceCatalogStore();
const { pageResult, detail } = storeToRefs(store);
const busy = ref(false), reading = ref(false), error = ref(''), success = ref(''), needsReload = ref(false);
const keyword = ref(''), activeFilter = ref('all'), page = ref(1), mode = ref('');
const serviceConfirmed = ref(false), priceConfirmed = ref(false), statusConfirmed = ref(false);
const emptyForm = () => ({ code: '', name: '', description: '', durationMinutes: '30', initialPrice: '', effectiveFrom: clinicDateTimeInput() });
const form = ref(emptyForm()), priceForm = ref({ amount: '', effectiveFrom: clinicDateTimeInput() });
let version = 0;
const isAdmin = computed(() => auth.role === 'ADMIN');
const todayStart = computed(() => clinicDateTimeInput().slice(0, 10) + 'T00:00');
const latestPriceDate = computed(() => [...(detail.value?.priceHistory ?? [])].sort((a, b) => new Date(b.effectiveFrom).getTime() - new Date(a.effectiveFrom).getTime())[0]?.effectiveFrom);
const validService = computed(() => form.value.code.trim().length > 0 && form.value.code.length <= 50 &&
  form.value.name.trim().length > 0 && form.value.name.length <= 200 && form.value.description.length <= 1000 &&
  /^\d+$/.test(form.value.durationMinutes) && Number(form.value.durationMinutes) > 0 && Number(form.value.durationMinutes) <= 2147483647 &&
  (mode.value !== 'create' || validCatalogPrice(form.value.initialPrice) && validCatalogEffective(form.value.effectiveFrom)));
const validPrice = computed(() => validCatalogPrice(priceForm.value.amount) && validCatalogEffective(priceForm.value.effectiveFrom, latestPriceDate.value));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const date = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
function failure(cause: unknown): string {
  if (!axios.isAxiosError(cause)) return 'Không thể xử lý danh mục.';
  const errors = cause.response?.data?.errors;
  const messages = Array.isArray(errors) ? errors.map((x: { message: string }) => x.message).join(' ') : '';
  return messages || cause.response?.data?.message || 'Chưa xác định kết quả. Tải lại để đối chiếu trước khi thử lại.';
}
function activeParam() { return activeFilter.value === 'all' ? undefined : activeFilter.value === 'true'; }
function resetConfirmations() { serviceConfirmed.value = false; priceConfirmed.value = false; statusConfirmed.value = false; }
function showDetail(data: CatalogDetail) {
  store.selectDetail(data); mode.value = 'edit';
  form.value = { ...emptyForm(), code: data.code, name: data.name, description: data.description ?? '', durationMinutes: String(data.durationMinutes) };
  priceForm.value = { amount: '', effectiveFrom: clinicDateTimeInput() }; resetConfirmations();
}
async function load(target = 1) {
  if (!isAdmin.value || busy.value || !Number.isInteger(target) || target < 1 || target > 1000000 || keyword.value.trim().length > 100 || !['all', 'true', 'false'].includes(activeFilter.value)) return;
  const ticket = ++version; reading.value = true; error.value = ''; success.value = ''; needsReload.value = false;
  store.reset(); mode.value = ''; resetConfirmations();
  try { const data = await serviceCatalogApi.list(target, keyword.value.trim(), activeParam()); if (ticket === version) { store.replacePage(data); page.value = target; } }
  catch (cause) { if (ticket === version) error.value = failure(cause); }
  finally { if (ticket === version) reading.value = false; }
}
async function select(id: number) {
  if (!isAdmin.value || busy.value || reading.value || needsReload.value || !Number.isInteger(id) || id <= 0) return;
  const ticket = ++version; reading.value = true; store.selectDetail(null); mode.value = ''; error.value = ''; success.value = '';
  try { const data = await serviceCatalogApi.detail(id); if (ticket === version) showDetail(data); }
  catch (cause) { if (ticket === version) error.value = failure(cause); }
  finally { if (ticket === version) reading.value = false; }
}
function startCreate() {
  if (!isAdmin.value || busy.value || reading.value || needsReload.value) return;
  version++; mode.value = 'create'; store.selectDetail(null); form.value = emptyForm(); resetConfirmations(); error.value = ''; success.value = '';
}
async function write(action: () => Promise<number>, message: string) {
  if (!isAdmin.value || busy.value || reading.value || needsReload.value) return;
  const ticket = version; busy.value = true; error.value = ''; success.value = '';
  try {
    const id = await action();
    if (ticket !== version) return;
    const [record, list] = await Promise.all([serviceCatalogApi.detail(id), serviceCatalogApi.list(page.value, keyword.value.trim(), activeParam())]);
    if (ticket === version) { showDetail(record); store.replacePage(list); success.value = message; }
  } catch (cause) { if (ticket === version) { error.value = failure(cause); needsReload.value = true; } }
  finally { if (ticket === version) { busy.value = false; resetConfirmations(); } }
}
async function saveService() {
  if (!validService.value || !serviceConfirmed.value || !['create', 'edit'].includes(mode.value) || mode.value === 'edit' && !detail.value) return;
  const metadata = { name: form.value.name.trim(), description: form.value.description.trim() || null, durationMinutes: Number(form.value.durationMinutes) };
  const id = detail.value?.dentalServiceId;
  await write(async () => (mode.value === 'create' ? await serviceCatalogApi.create({ ...metadata, code: form.value.code.trim(), initialPrice: Number(form.value.initialPrice), effectiveFrom: clinicInputToIso(form.value.effectiveFrom)! })
    : await serviceCatalogApi.update(id!, metadata)).dentalServiceId, 'Đã lưu dịch vụ.');
}
async function addPrice() {
  if (!detail.value || !validPrice.value || !priceConfirmed.value) return;
  const id = detail.value.dentalServiceId, amount = Number(priceForm.value.amount), instant = clinicInputToIso(priceForm.value.effectiveFrom)!;
  await write(async () => { await serviceCatalogApi.addPrice(id, amount, instant); return id; }, 'Đã thêm giá mới vào lịch sử.');
}
async function toggleActive() {
  if (!detail.value || !statusConfirmed.value) return;
  const id = detail.value.dentalServiceId, active = !detail.value.isActive;
  await write(async () => { await serviceCatalogApi.setActive(id, active); return id; }, active ? 'Đã kích hoạt dịch vụ.' : 'Đã ngừng sử dụng dịch vụ.');
}
watch(form, () => { serviceConfirmed.value = false; }, { deep: true });
watch(priceForm, () => { priceConfirmed.value = false; }, { deep: true });
onMounted(() => load());
onUnmounted(() => { version++; store.reset(); form.value = emptyForm(); mode.value = ''; resetConfirmations(); });
</script>

<style scoped>
.catalog-page {
  max-width: 1300px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1.25rem;
}

.eyebrow {
  margin: 0 0 0.25rem;
  color: #0284c7;
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

h1 {
  font-size: 1.65rem;
  font-weight: 850;
  color: #0f2d59;
  margin: 0;
  line-height: 1.25;
}

.subtitle {
  margin: 0.35rem 0 0;
  color: #64748b;
  font-size: 0.885rem;
  line-height: 1.45;
  max-width: 820px;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid #f1f5f9;
  background: #f8fafc;
  border-radius: 12px 12px 0 0;
}

.panel-title {
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
}

.count-badge {
  background: #e2e8f0;
  color: #475569;
  font-size: 0.78rem;
  font-weight: 750;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
}

/* Filters */
.filters-panel {
  padding: 1.15rem 1.25rem;
}

.filters-grid {
  display: grid;
  grid-template-columns: minmax(280px, 1.8fr) minmax(180px, 1fr) auto;
  align-items: end;
  gap: 1rem;
}

.filter-field {
  display: grid;
  gap: 0.35rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
}

.input-icon-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.field-icon {
  position: absolute;
  left: 0.75rem;
  color: #94a3b8;
  font-size: 0.85rem;
  pointer-events: none;
}

.input-icon-wrapper input {
  padding-left: 2.25rem !important;
}

.filter-field input,
.filter-field select,
.form-field input,
.form-field select,
.form-field textarea {
  box-sizing: border-box;
  width: 100%;
  height: 40px;
  padding: 0 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #f8fafc;
  color: #1e293b;
  font: inherit;
  font-size: 0.875rem;
  outline: none;
  transition: all 0.15s ease;
}

.form-field textarea {
  height: auto;
  padding: 0.65rem 0.85rem;
}

.filter-field input:focus,
.filter-field select:focus,
.form-field input:focus,
.form-field select:focus,
.form-field textarea:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.filter-actions {
  display: flex;
  gap: 0.5rem;
}

/* Master-detail layout */
.catalog-layout {
  display: grid;
  grid-template-columns: 420px 1fr;
  gap: 1.25rem;
  align-items: start;
}

.list-panel {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.services-list {
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 350px);
  overflow-y: auto;
}

.service-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.9rem 1.15rem;
  border-bottom: 1px solid #f1f5f9;
  cursor: pointer;
  transition: all 0.15s ease;
}

.service-item:hover {
  background: #f8fafc;
}

.service-item--active {
  background: #f0f9ff;
  border-left: 4px solid #0284c7;
}

.service-main {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.service-code-row {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.service-code {
  font-family: monospace;
  font-size: 0.76rem;
  font-weight: 750;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.status-pill {
  font-size: 0.72rem;
  font-weight: 750;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
}

.status-pill--active {
  background: #ecfdf5;
  color: #059669;
}

.status-pill--inactive {
  background: #fef2f2;
  color: #dc2626;
}

.service-name {
  font-weight: 700;
  font-size: 0.885rem;
  color: #0f2d59;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.service-price {
  font-weight: 800;
  font-size: 0.885rem;
  color: #059669;
  white-space: nowrap;
  text-align: right;
  margin-left: 0.75rem;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.85rem 1.15rem;
  border-top: 1px solid #e2e8f0;
  background: #f8fafc;
}

.page-info {
  font-size: 0.825rem;
  color: #64748b;
  font-weight: 600;
}

.page-btns {
  display: flex;
  gap: 0.5rem;
}

/* Detail Column */
.detail-column {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.detail-panel {
  padding: 1.25rem;
}

.detail-panel .panel-header {
  margin: -1.25rem -1.25rem 1.25rem;
}

.current-price-badge {
  font-size: 0.85rem;
  color: #475569;
}

.current-price-badge strong {
  color: #059669;
  font-size: 1.05rem;
}

.form-fieldset {
  border: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.fieldset-legend {
  font-size: 0.95rem;
  font-weight: 800;
  color: #0f2d59;
  margin-bottom: 0.85rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.85rem;
}

.form-field {
  display: grid;
  gap: 0.35rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
}

.form-field b {
  color: #dc2626;
}

.form-field--wide {
  grid-column: 1 / -1;
}

.form-hint {
  font-size: 0.8rem;
  color: #64748b;
  margin: 0;
  line-height: 1.45;
}

.confirm-box {
  background: #fffbeb;
  border: 1px solid #fef3c7;
  border-radius: 8px;
  padding: 0.75rem 1rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  font-size: 0.825rem;
  font-weight: 700;
  color: #92400e;
  cursor: pointer;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}

/* Table styles */
.table-wrap {
  overflow-x: auto;
  margin-bottom: 1.25rem;
}

.price-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.price-table th,
.price-table td {
  padding: 0.75rem 0.9rem;
  border-bottom: 1px solid #f1f5f9;
  text-align: left;
}

.price-table thead th {
  background: #f8fafc;
  color: #475569;
  font-size: 0.775rem;
  font-weight: 750;
  text-transform: uppercase;
  border-bottom: 1px solid #e2e8f0;
}

.price-val {
  color: #059669;
  font-size: 0.95rem;
}

.empty-cell {
  text-align: center;
  color: #94a3b8;
  padding: 2rem !important;
}

/* Buttons */
.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  height: 40px;
  padding: 0 1.15rem;
  border: 1px solid transparent;
  border-radius: 8px;
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
}

.button-primary:hover:not(:disabled) {
  background: #0369a1;
}

.button-secondary {
  background: #ffffff;
  color: #334155;
  border-color: #cbd5e1;
}

.button-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.button-warning {
  background: #fef3c7;
  color: #92400e;
  border-color: #fde68a;
}

.button-warning:hover:not(:disabled) {
  background: #fde68a;
}

.button-quiet {
  background: #ffffff;
  border-color: #cbd5e1;
  color: #475569;
  height: 32px;
  padding: 0 0.85rem;
  font-size: 0.8rem;
}

.button-quiet:hover:not(:disabled) {
  background: #f1f5f9;
}

.button-danger {
  background: #dc2626;
  color: #ffffff;
}

.button-danger:hover:not(:disabled) {
  background: #b91c1c;
}

.button-success {
  background: #059669;
  color: #ffffff;
}

.button-success:hover:not(:disabled) {
  background: #047857;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.btn-icon {
  font-size: 0.95rem;
}

.empty-state {
  padding: 3rem 1rem;
  color: #64748b;
  text-align: center;
  font-size: 0.95rem;
}

.alert {
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 600;
  margin: 0;
}

.alert-error {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fee2e2;
}

.alert-success {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.alert-info {
  background: #f0f9ff;
  color: #0369a1;
  border: 1px solid #bae6fd;
}

@media (max-width: 960px) {
  .catalog-layout {
    grid-template-columns: 1fr;
  }

  .filters-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
