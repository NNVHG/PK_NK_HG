<template>
  <main class="catalog-page">
    <h1>Dịch vụ & bảng giá</h1>
    <p>Giá mới được ghi thêm vào lịch sử. Các chỉ định và hóa đơn cũ giữ nguyên đơn giá đã ghi nhận.</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <p v-if="busy || reading" role="status">Đang xử lý…</p>
    <form @submit.prevent="load(1)">
      <label>Tìm mã / tên <input v-model="keyword" maxlength="100" :disabled="busy || reading" /></label>
      <label>Trạng thái <select v-model="activeFilter" :disabled="busy || reading"><option value="all">Tất cả</option><option value="true">Đang dùng</option><option value="false">Ngừng dùng</option></select></label>
      <button :disabled="busy || reading">Tìm / tải lại</button>
    </form>
    <button type="button" :disabled="busy || reading || needsReload" @click="startCreate">Thêm dịch vụ</button>
    <button v-if="needsReload" type="button" :disabled="busy || reading" @click="load(page)">Tải lại để đối chiếu kết quả</button>
    <p v-if="pageResult && !pageResult.items.length">Không có dịch vụ phù hợp.</p>
    <ul class="service-list"><li v-for="item in pageResult?.items ?? []" :key="item.dentalServiceId">
      <button type="button" :disabled="busy || reading || needsReload" @click="select(item.dentalServiceId)">{{ item.code }} · {{ item.name }}</button>
      <span>{{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }} · {{ item.currentPrice === null ? 'Chưa có giá hiện hành' : money(item.currentPrice) }}</span>
    </li></ul>
    <div v-if="pageResult">
      <button :disabled="busy || reading || page <= 1" @click="load(page - 1)">Trang trước</button>
      Trang {{ page }} / {{ Math.max(1, pageResult.totalPages) }} · {{ pageResult.totalCount }} dịch vụ
      <button :disabled="busy || reading || page >= pageResult.totalPages" @click="load(page + 1)">Trang sau</button>
    </div>
    <section v-if="mode" aria-label="Thông tin dịch vụ">
      <h2>{{ mode === 'create' ? 'Thêm dịch vụ' : `${detail?.code} · ${detail?.name}` }}</h2>
      <form @submit.prevent="saveService">
        <fieldset :disabled="busy || reading || needsReload">
          <legend>Thông tin danh mục</legend>
          <label>Mã dịch vụ <input v-model="form.code" maxlength="50" :readonly="mode !== 'create'" required /></label>
          <label>Tên dịch vụ <input v-model="form.name" maxlength="200" required /></label>
          <label>Mô tả <textarea v-model="form.description" maxlength="1000" /></label>
          <label>Thời lượng (phút) <input v-model="form.durationMinutes" type="number" min="1" max="2147483647" step="1" required /></label>
          <template v-if="mode === 'create'">
            <label>Giá ban đầu (VND nguyên đồng) <input v-model="form.initialPrice" type="number" min="0" step="1" required /></label>
            <label>Có hiệu lực từ (giờ Việt Nam) <input v-model="form.effectiveFrom" type="datetime-local" :min="todayStart" required /></label>
          </template>
          <label><input v-model="serviceConfirmed" type="checkbox" /> Tôi xác nhận lưu thông tin dịch vụ</label>
          <button :disabled="!validService || !serviceConfirmed">{{ mode === 'create' ? 'Tạo dịch vụ' : 'Lưu thông tin' }}</button>
        </fieldset>
      </form>
    </section>
    <section v-if="detail" aria-label="Bảng giá và trạng thái">
      <h2>Lịch sử giá · {{ detail.code }}</h2>
      <p>Giá hiện hành: {{ detail.currentPrice === null ? 'Chưa có giá hiện hành' : money(detail.currentPrice) }}</p>
      <ul><li v-for="price in detail.priceHistory ?? []" :key="price.servicePriceId">
        {{ money(price.amount) }} · Từ {{ date(price.effectiveFrom) }} · Người tạo #{{ price.createdByUserId }}
      </li></ul>
      <form @submit.prevent="addPrice">
        <fieldset :disabled="busy || reading || needsReload">
          <legend>Thêm giá mới</legend>
          <label>Giá (VND nguyên đồng) <input v-model="priceForm.amount" type="number" min="0" step="1" required /></label>
          <label>Có hiệu lực từ (giờ Việt Nam) <input v-model="priceForm.effectiveFrom" type="datetime-local" :min="todayStart" required /></label>
          <p>Ngày hiệu lực từ hôm nay và không trước mốc giá đã lên lịch gần nhất: {{ latestPriceDate ? date(latestPriceDate) : 'Chưa có' }}.</p>
          <label><input v-model="priceConfirmed" type="checkbox" /> Tôi xác nhận thêm bản ghi giá mới</label>
          <button :disabled="!validPrice || !priceConfirmed">Thêm giá</button>
        </fieldset>
      </form>
      <fieldset :disabled="busy || reading || needsReload">
        <legend>Trạng thái dịch vụ</legend>
        <p>{{ detail.isActive ? 'Đang sử dụng' : 'Đã ngừng sử dụng' }}</p>
        <label><input v-model="statusConfirmed" type="checkbox" /> Tôi xác nhận {{ detail.isActive ? 'ngừng' : 'kích hoạt lại' }} dịch vụ</label>
        <button :disabled="!statusConfirmed" @click="toggleActive">{{ detail.isActive ? 'Ngừng sử dụng' : 'Kích hoạt lại' }}</button>
      </fieldset>
    </section>
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
.catalog-page { max-width: 960px; margin: 1rem auto; }
label { display: block; margin: .75rem 0; }
input, select, textarea { max-width: 100%; }
textarea { display: block; width: 100%; }
button { margin: .3rem; padding: .5rem; }
section { margin-top: 1.5rem; }
.error { color: #b91c1c; }
.service-list { padding-left: 1rem; }
</style>
