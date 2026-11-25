<template>
  <main class="catalog-page">
    <header class="page-heading">
      <div>
        <h1>Dịch vụ &amp; Bảng giá điều trị</h1>
        <p class="subtitle">
          Quản lý kỹ thuật nha khoa và lịch sử bảng giá; mốc giá mới lưu độc lập để giữ nguyên đơn giá chứng từ trước đó.
        </p>
      </div>
      <div class="heading-actions">
        <span v-if="pageResult" class="count-pill">{{ pageResult.totalCount }} dịch vụ</span>
        <button
          type="button"
          class="button button-primary"
          :disabled="busy || reading || needsReload"
          @click="startCreate"
        >
          ＋ Thêm dịch vụ mới
        </button>
      </div>
    </header>

    <p v-if="error" role="alert" class="alert alert-error">{{ error }}</p>
    <p v-if="success" role="status" class="alert alert-success">{{ success }}</p>
    <p v-if="busy || reading" role="status" class="alert alert-info">Đang xử lý dữ liệu dịch vụ…</p>

    <!-- Layout 2 cột gọn -->
    <div class="catalog-layout">
      <!-- Cột trái: Bộ lọc gộp + Danh sách dịch vụ -->
      <section class="panel list-panel" aria-label="Danh sách dịch vụ kỹ thuật">
        <form class="list-filters" @submit.prevent="load(1)">
          <input
            v-model="keyword"
            type="search"
            maxlength="100"
            placeholder="Tìm mã hoặc tên dịch vụ..."
            aria-label="Tìm kiếm theo mã hoặc tên"
            :disabled="busy || reading"
          />
          <select v-model="activeFilter" aria-label="Trạng thái sử dụng" :disabled="busy || reading">
            <option value="all">Tất cả</option>
            <option value="true">Đang dùng</option>
            <option value="false">Ngừng dùng</option>
          </select>
          <button class="button button-secondary" :disabled="busy || reading">Tìm</button>
          <button
            v-if="needsReload"
            type="button"
            class="button button-warning"
            :disabled="busy || reading"
            @click="load(page)"
          >
            Tải lại
          </button>
        </form>

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

        <footer v-if="pageResult" class="pagination">
          <span class="page-info">Trang {{ page }} / {{ Math.max(1, pageResult.totalPages) }}</span>
          <div class="page-btns">
            <button
              class="button button-quiet"
              type="button"
              :disabled="busy || reading || page <= 1"
              @click="load(page - 1)"
            >
              ← Trước
            </button>
            <button
              class="button button-quiet"
              type="button"
              :disabled="busy || reading || page >= pageResult.totalPages"
              @click="load(page + 1)"
            >
              Sau →
            </button>
          </div>
        </footer>
      </section>

      <!-- Cột phải: Chi tiết dịch vụ với 2 tab gọn -->
      <section class="panel detail-panel">
        <template v-if="detail">
          <div class="detail-hero">
            <div>
              <div class="hero-tags">
                <span class="service-code">{{ detail.code }}</span>
                <span
                  class="status-pill"
                  :class="detail.isActive ? 'status-pill--active' : 'status-pill--inactive'"
                >
                  {{ detail.isActive ? 'Đang phục vụ' : 'Ngừng sử dụng' }}
                </span>
                <span class="duration-pill">⏱ {{ detail.durationMinutes }} phút</span>
              </div>
              <h2>{{ detail.name }}</h2>
            </div>
            <div class="current-price-badge">
              <span>Giá hiện hành</span>
              <strong>{{ detail.currentPrice === null ? 'Chưa áp giá' : money(detail.currentPrice) }}</strong>
            </div>
          </div>

          <div class="tab-bar" role="tablist" aria-label="Chi tiết dịch vụ">
            <button
              type="button"
              role="tab"
              :aria-selected="activeDetailTab === 'info'"
              class="tab-btn"
              :class="{ active: activeDetailTab === 'info' }"
              @click="activeDetailTab = 'info'"
            >
              Thông tin &amp; Trạng thái
            </button>
            <button
              type="button"
              role="tab"
              :aria-selected="activeDetailTab === 'prices'"
              class="tab-btn"
              :class="{ active: activeDetailTab === 'prices' }"
              @click="activeDetailTab = 'prices'"
            >
              Bảng giá &amp; Lịch sử giá ({{ detail.priceHistory?.length ?? 0 }})
            </button>
          </div>

          <!-- Tab thông tin & trạng thái -->
          <div v-if="activeDetailTab === 'info'" class="tab-content">
            <form class="detail-form" @submit.prevent="saveService">
              <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
                <div class="form-grid">
                  <label class="form-field">
                    <span>Mã dịch vụ</span>
                    <input v-model="form.code" maxlength="50" readonly />
                  </label>
                  <label class="form-field">
                    <span>Thời lượng ca (phút) <b>*</b></span>
                    <input v-model="form.durationMinutes" type="number" min="1" max="2147483647" step="1" required />
                  </label>
                  <label class="form-field form-field--wide">
                    <span>Tên dịch vụ kỹ thuật <b>*</b></span>
                    <input v-model="form.name" maxlength="200" required />
                  </label>
                  <label class="form-field form-field--wide">
                    <span>Mô tả lâm sàng &amp; chỉ định</span>
                    <textarea v-model="form.description" rows="2" maxlength="1000" placeholder="Nhập quy trình tóm tắt hoặc ghi chú..."></textarea>
                  </label>
                </div>

                <div class="confirm-row">
                  <label class="checkbox-label">
                    <input v-model="serviceConfirmed" type="checkbox" />
                    <span>Tôi xác nhận các thông tin kỹ thuật của dịch vụ này là chính xác</span>
                  </label>
                  <button class="button button-primary" :disabled="!validService || !serviceConfirmed">
                    💾 Lưu thông tin
                  </button>
                </div>
              </fieldset>
            </form>

            <div class="status-box">
              <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
                <div class="confirm-row">
                  <label class="checkbox-label">
                    <input v-model="statusConfirmed" type="checkbox" />
                    <span>Tôi xác nhận {{ detail.isActive ? 'tạm ngừng cung cấp' : 'kích hoạt phục vụ trở lại' }} dịch vụ này</span>
                  </label>
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
            </div>
          </div>

          <!-- Tab bảng giá & lịch sử giá -->
          <div v-else class="tab-content">
            <div class="table-wrap">
              <table class="price-table">
                <thead>
                  <tr>
                    <th>Mức giá (VNĐ)</th>
                    <th>Thời điểm áp dụng (UTC+7)</th>
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
                  Mốc hiệu lực từ hôm nay và không trước mốc gần nhất: <strong>{{ latestPriceDate ? date(latestPriceDate) : 'Chưa có' }}</strong>.
                </p>
                <div class="confirm-row">
                  <label class="checkbox-label">
                    <input v-model="priceConfirmed" type="checkbox" />
                    <span>Tôi xác nhận thêm bản ghi đơn giá mới vào danh mục</span>
                  </label>
                  <button class="button button-primary" :disabled="!validPrice || !priceConfirmed">
                    ➕ Cập nhật mức giá mới
                  </button>
                </div>
              </fieldset>
            </form>
          </div>
        </template>

        <div v-else class="empty-detail">
          <strong>Chọn một dịch vụ ở danh sách bên trái</strong> để xem chi tiết, chỉnh sửa thông tin, quản lý lịch sử giá hoặc bấm <strong>＋ Thêm dịch vụ mới</strong>.
        </div>
      </section>
    </div>

    <!-- Popup Thêm dịch vụ mới -->
    <div v-if="mode === 'create'" class="modal-overlay" @click.self="closeCreateModal">
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="create-service-title">
        <div class="modal-header">
          <h2 id="create-service-title">Thêm dịch vụ kỹ thuật mới</h2>
          <button class="close-btn" type="button" :disabled="busy" aria-label="Đóng" @click="closeCreateModal">✕</button>
        </div>

        <form class="modal-form" @submit.prevent="saveService">
          <fieldset class="form-fieldset" :disabled="busy || reading || needsReload">
            <div class="form-grid">
              <label class="form-field">
                <span>Mã dịch vụ <b>*</b></span>
                <input v-model="form.code" maxlength="50" placeholder="VD: NHAKHOA_01" required />
              </label>
              <label class="form-field">
                <span>Thời lượng ca (phút) <b>*</b></span>
                <input v-model="form.durationMinutes" type="number" min="1" max="2147483647" step="1" required />
              </label>
              <label class="form-field form-field--wide">
                <span>Tên dịch vụ kỹ thuật <b>*</b></span>
                <input v-model="form.name" maxlength="200" placeholder="VD: Cạo vôi răng & Đánh bóng" required />
              </label>
              <label class="form-field">
                <span>Đơn giá khởi tạo (VNĐ) <b>*</b></span>
                <input v-model="form.initialPrice" type="number" min="0" step="1" placeholder="VD: 250000" required />
              </label>
              <label class="form-field">
                <span>Hiệu lực từ ngày giờ <b>*</b></span>
                <input v-model="form.effectiveFrom" type="datetime-local" :min="todayStart" required />
              </label>
              <label class="form-field form-field--wide">
                <span>Mô tả lâm sàng &amp; chỉ định</span>
                <textarea v-model="form.description" rows="2" maxlength="1000" placeholder="Nhập quy trình tóm tắt hoặc ghi chú..."></textarea>
              </label>
            </div>

            <div class="confirm-box">
              <label class="checkbox-label">
                <input v-model="serviceConfirmed" type="checkbox" />
                <span>Tôi xác nhận các thông tin kỹ thuật của dịch vụ này là chính xác</span>
              </label>
            </div>

            <div class="modal-actions">
              <button class="button button-secondary" type="button" :disabled="busy" @click="closeCreateModal">Hủy</button>
              <button class="button button-primary" type="submit" :disabled="!validService || !serviceConfirmed">
                💾 Tạo dịch vụ
              </button>
            </div>
          </fieldset>
        </form>
      </div>
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
const activeDetailTab = ref<'info' | 'prices'>('info');
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

function closeCreateModal() {
  if (busy.value) return;
  mode.value = detail.value ? 'edit' : '';
  form.value = detail.value
    ? { ...emptyForm(), code: detail.value.code, name: detail.value.name, description: detail.value.description ?? '', durationMinutes: String(detail.value.durationMinutes) }
    : emptyForm();
  resetConfirmations();
}

async function load(target = 1) {
  if (!isAdmin.value || busy.value || !Number.isInteger(target) || target < 1 || target > 1000000 || keyword.value.trim().length > 100 || !['all', 'true', 'false'].includes(activeFilter.value)) return;
  const ticket = ++version; reading.value = true; error.value = ''; success.value = ''; needsReload.value = false;
  store.reset(); mode.value = ''; resetConfirmations();
  try {
    const data = await serviceCatalogApi.list(target, keyword.value.trim(), activeParam());
    if (ticket === version) { store.replacePage(data); page.value = target; }
  } catch (cause) { if (ticket === version) error.value = failure(cause); }
  finally { if (ticket === version) reading.value = false; }
}

async function select(id: number) {
  if (!isAdmin.value || busy.value || reading.value || needsReload.value || !Number.isInteger(id) || id <= 0) return;
  const ticket = ++version; reading.value = true; store.selectDetail(null); mode.value = ''; error.value = ''; success.value = '';
  try {
    const data = await serviceCatalogApi.detail(id);
    if (ticket === version) {
      activeDetailTab.value = 'info';
      showDetail(data);
    }
  } catch (cause) { if (ticket === version) error.value = failure(cause); }
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
  await write(async () => (mode.value === 'create'
    ? await serviceCatalogApi.create({ ...metadata, code: form.value.code.trim(), initialPrice: Number(form.value.initialPrice), effectiveFrom: clinicInputToIso(form.value.effectiveFrom)! })
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
  max-width: 1480px;
  margin: 0 auto;
  padding: 0.9rem 1.25rem 1.5rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.page-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem;
}

h1 {
  font-size: 1.38rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
  line-height: 1.2;
}

.subtitle {
  margin: 0.2rem 0 0;
  color: #64748b;
  font-size: 0.84rem;
}

.heading-actions {
  display: flex;
  align-items: center;
  gap: 0.55rem;
}

.count-pill {
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
  font-size: 0.78rem;
  font-weight: 700;
  padding: 0.25rem 0.65rem;
  border-radius: 999px;
}

.catalog-layout {
  display: grid;
  grid-template-columns: minmax(340px, 430px) minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
  min-width: 0;
}

.list-panel {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.list-filters {
  display: flex;
  gap: 0.4rem;
  padding: 0.75rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}

.list-filters input {
  flex: 1;
  min-width: 0;
}

.list-filters select {
  width: 112px;
}

.list-filters input,
.list-filters select,
.form-field input,
.form-field select,
.form-field textarea {
  box-sizing: border-box;
  height: 36px;
  padding: 0 0.65rem;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  background: #ffffff;
  color: #1e293b;
  font: inherit;
  font-size: 0.85rem;
}

.form-field input,
.form-field select,
.form-field textarea {
  width: 100%;
}

.form-field textarea {
  height: auto;
  padding: 0.5rem 0.65rem;
}

.services-list {
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 260px);
  overflow-y: auto;
}

.service-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.6rem 0.85rem;
  border-bottom: 1px solid #f1f5f9;
  cursor: pointer;
  gap: 0.65rem;
}

.service-item:hover { background: #f8fafc; }
.service-item--active { background: #f0f9ff; border-left: 3px solid #0284c7; }
.service-main { display: flex; flex-direction: column; gap: 0.15rem; min-width: 0; }
.service-code-row { display: flex; align-items: center; gap: 0.35rem; }
.service-code { font-family: monospace; font-size: 0.74rem; font-weight: 750; color: #0369a1; background: #e0f2fe; padding: 0.1rem 0.4rem; border-radius: 4px; }
.status-pill { font-size: 0.7rem; font-weight: 750; padding: 0.08rem 0.45rem; border-radius: 999px; }
.status-pill--active { background: #ecfdf5; color: #059669; }
.status-pill--inactive { background: #fef2f2; color: #dc2626; }
.service-name { font-weight: 700; font-size: 0.85rem; color: #0f2d59; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.service-price { font-weight: 800; font-size: 0.84rem; color: #059669; white-space: nowrap; }

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 0.85rem;
  border-top: 1px solid #e2e8f0;
  background: #f8fafc;
}
.page-info { font-size: 0.8rem; color: #64748b; font-weight: 600; }
.page-btns { display: flex; gap: 0.35rem; }

.detail-panel {
  padding: 1rem 1.15rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.detail-hero {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #f1f5f9;
}
.hero-tags { display: flex; align-items: center; flex-wrap: wrap; gap: 0.4rem; margin-bottom: 0.3rem; }
.duration-pill { font-size: 0.74rem; font-weight: 650; color: #475569; background: #f1f5f9; padding: 0.1rem 0.45rem; border-radius: 999px; }
.detail-hero h2 { margin: 0; font-size: 1.18rem; color: #0f2d59; }
.current-price-badge { text-align: right; background: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 9px; padding: 0.4rem 0.75rem; }
.current-price-badge span { display: block; font-size: 0.7rem; font-weight: 700; color: #047857; text-transform: uppercase; }
.current-price-badge strong { color: #059669; font-size: 1.05rem; }

.tab-bar {
  display: flex;
  gap: 0.4rem;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 0.6rem;
}
.tab-btn {
  padding: 0.38rem 0.85rem;
  border-radius: 7px;
  border: 1px solid transparent;
  background: #f8fafc;
  color: #475569;
  font-size: 0.84rem;
  font-weight: 650;
  cursor: pointer;
}
.tab-btn.active { background: #e0f2fe; border-color: #bae6fd; color: #0369a1; }

.tab-content { display: flex; flex-direction: column; gap: 0.85rem; }
.form-fieldset { border: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.75rem; }
.fieldset-legend { font-size: 0.9rem; font-weight: 800; color: #0f2d59; margin-bottom: 0.5rem; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.7rem; }
.form-field { display: grid; gap: 0.25rem; font-size: 0.8rem; font-weight: 650; color: #334155; }
.form-field b { color: #dc2626; }
.form-field--wide { grid-column: 1 / -1; }
.form-hint { font-size: 0.78rem; color: #64748b; margin: 0; }

.confirm-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.65rem;
  background: #fffbeb;
  border: 1px solid #fef3c7;
  border-radius: 8px;
  padding: 0.6rem 0.85rem;
}

.status-box { margin-top: 0.25rem; }
.confirm-box { background: #fffbeb; border: 1px solid #fef3c7; border-radius: 8px; padding: 0.6rem 0.85rem; }
.checkbox-label { display: flex; align-items: center; gap: 0.5rem; font-size: 0.81rem; font-weight: 700; color: #92400e; cursor: pointer; }

.table-wrap { overflow: auto; max-height: 230px; border: 1px solid #e2e8f0; border-radius: 8px; }
.price-table { width: 100%; border-collapse: collapse; font-size: 0.84rem; text-align: left; }
.price-table th, .price-table td { padding: 0.5rem 0.75rem; border-bottom: 1px solid #f1f5f9; }
.price-table thead th { position: sticky; top: 0; background: #f8fafc; color: #475569; font-size: 0.74rem; font-weight: 750; text-transform: uppercase; border-bottom: 1px solid #e2e8f0; }
.price-val { color: #059669; }
.empty-cell, .empty-state { text-align: center; color: #64748b; padding: 1.5rem; font-size: 0.88rem; }
.empty-detail { text-align: center; color: #64748b; padding: 2.5rem 1rem; font-size: 0.9rem; }

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.45);
  backdrop-filter: blur(2px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}
.modal-card {
  background: #ffffff;
  border-radius: 12px;
  max-width: 640px;
  width: 100%;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.14);
  overflow: hidden;
  border: 1px solid #e2e8f0;
}
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.95rem 1.15rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}
.modal-header h2 { margin: 0; font-size: 1.1rem; font-weight: 800; color: #0f2d59; }
.close-btn { background: none; border: none; font-size: 1.15rem; color: #64748b; cursor: pointer; }
.modal-form { padding: 1.15rem; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.6rem; padding-top: 0.35rem; }

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  height: 36px;
  padding: 0 0.95rem;
  border: 1px solid transparent;
  border-radius: 7px;
  font: inherit;
  font-size: 0.84rem;
  font-weight: 700;
  cursor: pointer;
}
.button-primary { background: #0284c7; color: #ffffff; }
.button-primary:hover:not(:disabled) { background: #0369a1; }
.button-secondary { background: #ffffff; color: #334155; border-color: #cbd5e1; }
.button-warning { background: #fef3c7; color: #92400e; border-color: #fde68a; }
.button-quiet { background: #ffffff; border-color: #cbd5e1; color: #475569; height: 32px; padding: 0 0.7rem; font-size: 0.8rem; }
.button-danger { background: #dc2626; color: #ffffff; }
.button-success { background: #059669; color: #ffffff; }
.button:disabled { cursor: not-allowed; opacity: 0.55; }

.alert { padding: 0.65rem 0.9rem; border-radius: 8px; font-size: 0.84rem; font-weight: 600; margin: 0; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-info { background: #f0f9ff; color: #0369a1; border: 1px solid #bae6fd; }

@media (max-width: 1024px) {
  .catalog-layout { grid-template-columns: 1fr; }
}
@media (max-width: 600px) {
  .form-grid { grid-template-columns: 1fr; }
}
</style>
