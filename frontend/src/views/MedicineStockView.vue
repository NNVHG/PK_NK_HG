<template>
  <section v-if="auth.role === 'ADMIN'" class="medicine-stock" aria-labelledby="medicine-stock-title">
    <h1 id="medicine-stock-title">Nhập và tồn thuốc</h1>
    <p>Nhập số lượng thuốc đã nhận thực tế theo đúng đơn vị danh mục. Phiếu chỉ cộng tồn sau xác nhận, không sửa giá bán hoặc hóa đơn.</p>
    <p>Thuốc được kiểm và trừ tồn khi xác nhận giao cho bệnh nhân. Không tự cộng lại kho khi hoàn tác bàn giao hoặc hoàn tiền.</p>
    <router-link to="/management/admin/medicines">Quản lý tên, đơn vị và giá thuốc</router-link>
    <form class="filters" @submit.prevent="search(1)">
      <label>Tìm thuốc<input v-model="keyword" maxlength="200" :disabled="busy || !!store.pending" /></label>
      <button :disabled="busy || listLoading || !!store.pending">Tìm / Tải danh mục</button>
    </form>
    <p v-if="listLoading || busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <p v-if="store.pending" role="alert">Phiếu đang chờ xác minh kết quả. Giữ nguyên thuốc và nội dung; gửi lại cùng mã để tránh cộng tồn hai lần.</p>
    <ul class="medicine-list"><li v-for="medicine in medicines" :key="medicine.id">
      <button :disabled="busy || !!store.pending" @click="select(medicine.id)">{{ medicine.code }} · {{ medicine.name }} · {{ medicine.unit }}{{ medicine.isActive ? '' : ' · Ngừng dùng' }}</button>
    </li></ul>
    <p v-if="!listLoading && searched && medicines.length === 0">Không có thuốc phù hợp.</p>
    <nav aria-label="Phân trang chọn thuốc">
      <button :disabled="busy || listLoading || !!store.pending || page <= 1" @click="search(page - 1)">Trang trước</button>
      <span>Trang {{ page }}</span><button :disabled="busy || listLoading || !!store.pending || !hasNext" @click="search(page + 1)">Trang sau</button>
    </nav>
    <section v-if="detail" class="detail" aria-labelledby="medicine-detail-title">
      <h2 id="medicine-detail-title" ref="heading" tabindex="-1">{{ detail.code }} · {{ detail.name }}</h2>
      <p><strong>Tồn hiện tại: {{ detail.onHand }} {{ detail.unit }}</strong></p>
      <button :disabled="busy" @click="loadDetail(detail.medicineId)">Tải lại tồn và phiếu mới</button>
      <p v-if="!detail.isActive">Thuốc đang ngừng dùng. Admin cần kiểm tra giá và kích hoạt trong danh mục trước khi nhập.</p>
      <form v-if="detail.isActive" class="receipt" @submit.prevent="receive">
        <h3>Nhập thuốc thực tế</h3>
        <label>Số lượng nhập ({{ detail.unit }})<input v-model="quantity" inputmode="numeric" pattern="[0-9]+" required maxlength="10" :disabled="busy || !!store.pending" @input="confirmed = false" /></label>
        <label>Nhà cung cấp<input v-model="supplier" required maxlength="200" :disabled="busy || !!store.pending" @input="confirmed = false" /></label>
        <label>Ghi chú (tùy chọn)<textarea v-model="notes" maxlength="500" :disabled="busy || !!store.pending" @input="confirmed = false" /></label>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy || !!store.pending" /> Tôi đã đối chiếu đúng thuốc, đơn vị và lượng thực nhận.</label>
        <button :disabled="busy || !!store.pending || !confirmed">Xác nhận nhập thuốc</button>
      </form>
      <h3>Lịch sử nhập thuốc</h3>
      <p v-if="detail.receipts.length === 0">Chưa có phiếu nhập phù hợp.</p>
      <ul><li v-for="receipt in detail.receipts" :key="receipt.id">
        <strong>{{ receipt.code }} · +{{ receipt.quantity }} {{ receipt.unit }}</strong>
        <p>{{ receipt.medicineCode }} · {{ receipt.medicineName }} · {{ receipt.supplierName }}</p>
        <p>{{ date(receipt.confirmedAt) }} · {{ receipt.confirmedByName }}</p><p v-if="receipt.notes">{{ receipt.notes }}</p>
      </li></ul>
      <button v-if="detail.hasMore && detail.receipts.length" :disabled="busy" @click="loadDetail(detail.medicineId, detail.receipts[detail.receipts.length - 1]!.id)">Xem phiếu cũ hơn</button>
    </section>
    <button v-if="store.pending" :disabled="busy" @click="retry">Gửi lại phiếu đang chờ với cùng mã</button>
  </section>
  <p v-else role="alert">Chỉ Admin được nhập và xem sổ nhập thuốc.</p>
</template>
<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useMedicineStockStore } from '@/stores/medicineStock';
import { medicinesService, type Medicine } from '@/services/medicines';
import { medicineStockService, type MedicineStockDetail } from '@/services/medicineStock';
const auth = useAuthStore(), store = useMedicineStockStore();
const medicines = ref<Medicine[]>([]), detail = ref<MedicineStockDetail | null>(null), keyword = ref(''), page = ref(1), hasNext = ref(false), searched = ref(false);
const busy = ref(false), listLoading = ref(false), error = ref(''), message = ref('');
const quantity = ref(''), supplier = ref(''), notes = ref(''), confirmed = ref(false), heading = ref<HTMLElement | null>(null);
let generation = 0, listGeneration = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}`;
const alive = (n: number, actor: string) => !disposed && n === generation && actor === identity() && auth.role === 'ADMIN';
const date = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
function clearForm() { quantity.value = ''; supplier.value = ''; notes.value = ''; confirmed.value = false; }
async function search(target = 1) {
  if (disposed || auth.role !== 'ADMIN' || busy.value || store.pending || keyword.value.length > 200 || !Number.isInteger(target) || target < 1 || target > 1_000_000) return;
  const n = ++listGeneration, actor = identity(); listLoading.value = true; error.value = ''; medicines.value = [];
  try {
    const result = await medicinesService.list(keyword.value.trim(), target, true);
    if (disposed || n !== listGeneration || actor !== identity() || auth.role !== 'ADMIN') return;
    if (!Array.isArray(result.items) || result.page !== target) throw new Error('Invalid catalog');
    medicines.value = result.items; page.value = target; hasNext.value = result.hasNextPage; searched.value = true;
  } catch { if (!disposed && n === listGeneration && actor === identity()) error.value = 'Không tải được danh mục thuốc. Vui lòng thử lại.'; }
  finally { if (!disposed && n === listGeneration) listLoading.value = false; }
}
async function select(id: number) {
  if (busy.value || store.pending || !medicines.value.some(m => m.id === id)) return;
  clearForm(); message.value = ''; await loadDetail(id); await nextTick(); heading.value?.focus();
}
async function loadDetail(id: number, beforeId = 0) {
  if (disposed || auth.role !== 'ADMIN' || busy.value || !Number.isInteger(id) || id <= 0 || !Number.isInteger(beforeId) || beforeId < 0 || store.pending && store.pending.medicineId !== id) return;
  const n = ++generation, actor = identity(); busy.value = true; detail.value = null; error.value = ''; confirmed.value = false;
  try {
    const result = await medicineStockService.read(id, beforeId);
    if (!alive(n, actor)) return;
    if (!result || result.medicineId !== id || !Number.isInteger(result.onHand) || result.onHand < 0 || result.onHand > 2147483647 || !Number.isInteger(result.medicineVersion) || result.medicineVersion <= 0 || result.medicineVersion > 2147483647 || typeof result.isActive !== 'boolean' || !Array.isArray(result.receipts) || result.receipts.length > 20) throw new Error('Invalid stock detail');
    detail.value = result;
  } catch { if (alive(n, actor)) error.value = 'Chưa tải được tồn thuốc. Tải lại trước khi lập phiếu mới.'; }
  finally { if (alive(n, actor)) busy.value = false; }
}
async function receive() {
  const current = detail.value;
  if (disposed || auth.role !== 'ADMIN' || busy.value || store.pending || !current?.isActive || !confirmed.value) return;
  const qty = Number(quantity.value);
  if (!/^[0-9]{1,10}$/.test(quantity.value) || !Number.isInteger(qty) || qty <= 0 || qty > 2147483647 || !supplier.value.trim() || supplier.value.length > 200 || /[\x00-\x1f\x7f]/.test(supplier.value) || notes.value.length > 500) { error.value = 'Nhập số nguyên dương, nhà cung cấp tối đa 200 và ghi chú tối đa 500 ký tự.'; return; }
  store.start(identity(), current.medicineId, { requestId: crypto.randomUUID(), quantity: qty, expectedMedicineVersion: current.medicineVersion, supplierName: supplier.value.trim(), notes: notes.value.trim() || null, confirmed: true });
  await retry();
}
async function retry() {
  const pending = store.pending;
  if (disposed || auth.role !== 'ADMIN' || busy.value || !pending || pending.actor !== identity()) return;
  const n = ++generation, actor = identity(); busy.value = true; error.value = ''; message.value = '';
  try {
    const result = await medicineStockService.receive(pending.medicineId, pending.request);
    if (!alive(n, actor)) return;
    if (!result?.receipt || !Number.isInteger(result.receipt.id) || result.receipt.id <= 0 || result.receipt.medicineId !== pending.medicineId || result.receipt.quantity !== pending.request.quantity || typeof result.replayed !== 'boolean') throw new Error('Unverified receipt');
    store.reset(); clearForm(); message.value = result.replayed ? 'Đã xác minh phiếu cũ; tồn không cộng thêm.' : 'Đã nhập thuốc và ghi lịch sử.';
    busy.value = false; await loadDetail(pending.medicineId);
  } catch (err: unknown) {
    if (!alive(n, actor)) return;
    const response = err as { response?: { status?: number; data?: { code?: string; message?: string } } };
    const rejected = response.response?.status === 400 || ['MED_STOCK_STALE', 'MED_STOCK_LIMIT', 'GEN_001', 'GEN_002'].includes(response.response?.data?.code ?? '');
    if (rejected) { store.reset(); detail.value = null; confirmed.value = false; }
    error.value = response.response?.data?.message || 'Chưa xác minh được kết quả nhập. Giữ mã hiện tại và gửi lại phiếu đang chờ để đối chiếu.';
  } finally { if (alive(n, actor)) busy.value = false; }
}
watch(identity, () => { generation++; listGeneration++; store.reset(); detail.value = null; medicines.value = []; clearForm(); busy.value = false; listLoading.value = false; error.value = ''; message.value = ''; });
onMounted(() => { if (store.pending && store.pending.actor !== identity()) store.reset(); if (store.pending) void loadDetail(store.pending.medicineId); else void search(); });
onUnmounted(() => { disposed = true; generation++; listGeneration++; detail.value = null; medicines.value = []; clearForm(); });
</script>
<style scoped>
.medicine-stock{max-width:1000px;margin:auto;padding:1.5rem}.filters,nav{display:flex;gap:.75rem;flex-wrap:wrap;align-items:end}label,.receipt{display:grid;gap:.5rem}.receipt{max-width:560px}.receipt label:has(input[type=checkbox]){display:block}input,select,textarea,button{font:inherit;padding:.5rem}.medicine-list{list-style:none;padding:0;display:flex;gap:.5rem;flex-wrap:wrap}.detail{border:1px solid #94a3b8;padding:1rem;margin:1rem 0}[role=alert]{color:#b91c1c}[role=status]{color:#166534}li{margin:.5rem 0}
</style>
