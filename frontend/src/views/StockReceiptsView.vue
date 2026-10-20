<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useStockReceiptsStore } from '@/stores/stockReceipts';
import { stockReceiptsService, type StockItem, type StockReceipt } from '@/services/stockReceipts';
const auth = useAuthStore(), store = useStockReceiptsStore();
const canWrite = computed(() => ['ADMIN', 'ASSISTANT'].includes(auth.role));
const items = ref<StockItem[]>([]), page = ref(1), hasNext = ref(false), keyword = ref(''), loading = ref(false), loadError = ref('');
const receipts = ref<StockReceipt[]>([]), hasMore = ref(false), historyLoading = ref(false), historyError = ref('');
const supplier = ref(''), notes = ref(''), confirmed = ref(false), formError = ref('');
const lines = ref<(StockItem & { quantity: number })[]>([]);
let generation = 0, historyGeneration = 0;
const locked = computed(() => store.busy || !!store.pending);
watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { generation++; historyGeneration++; items.value = []; receipts.value = []; lines.value = []; supplier.value = ''; notes.value = ''; loading.value = false; historyLoading.value = false; hasNext.value = false; hasMore.value = false; loadError.value = ''; historyError.value = ''; confirmed.value = false; });
onUnmounted(() => { generation++; historyGeneration++; });
async function loadStock(nextPage = 1) {
  const current = ++generation; loading.value = true; loadError.value = '';
  try { const result = await stockReceiptsService.stock(keyword.value, nextPage); if (current !== generation) return; items.value = result.items; page.value = result.page; hasNext.value = result.hasNextPage; }
  catch { if (current === generation) loadError.value = 'Không tải được tồn kho. Vui lòng thử lại.'; }
  finally { if (current === generation) loading.value = false; }
}
async function loadHistory(more = false) {
  if (!canWrite.value || historyLoading.value) return;
  const current = ++historyGeneration; historyLoading.value = true; historyError.value = '';
  try { const result = await stockReceiptsService.list(more ? receipts.value.at(-1)?.id ?? 0 : 0); if (current !== historyGeneration) return; receipts.value = more ? [...receipts.value, ...result.items.filter(x => !receipts.value.some(old => old.id === x.id))] : result.items; hasMore.value = result.hasMore; }
  catch { if (current === historyGeneration) historyError.value = 'Không tải được lịch sử phiếu.'; }
  finally { if (current === historyGeneration) historyLoading.value = false; }
}
function add(item: StockItem) { if (!locked.value && item.isActive && lines.value.length < 50 && !lines.value.some(x => x.medicalSupplyId === item.medicalSupplyId)) { lines.value.push({ ...item, quantity: 1 }); confirmed.value = false; } }
async function refreshSelection() { if (locked.value) return; lines.value = []; confirmed.value = false; await loadStock(); }
async function submit() {
  formError.value = '';
  if (!store.pending) {
    if (!supplier.value.trim() || supplier.value.trim().length > 200 || /[\u0000-\u001f\u007f]/.test(supplier.value) || notes.value.length > 500 || !confirmed.value || lines.value.length === 0 || lines.value.some(x => !Number.isInteger(x.quantity) || x.quantity < 1 || x.quantity > 2147483647)) { formError.value = 'Nhập nhà cung cấp, số lượng nguyên dương và xác nhận phiếu (tối đa 50 dòng).'; return; }
    await store.submit({ requestId: crypto.randomUUID(), confirmed: true, supplierName: supplier.value.trim(), notes: notes.value.trim() || null, lines: lines.value.map(x => ({ medicalSupplyId: x.medicalSupplyId, quantity: x.quantity, expectedSupplyVersion: x.supplyVersion })) });
  } else await store.submit();
  if (store.saved) { supplier.value = ''; notes.value = ''; lines.value = []; confirmed.value = false; await Promise.all([loadStock(page.value), loadHistory()]); }
}
function date(value: string) { return new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }); }
onMounted(() => { void loadStock(); void loadHistory(); });
</script>

<template>
  <section class="stock-page">
    <h1>Tồn kho & phiếu nhập</h1>
    <p>Số lượng nguyên theo đơn vị danh mục. Chỉ cộng tồn sau khi xác nhận phiếu.</p>
    <p v-if="auth.role === 'ADMIN'"><RouterLink to="/admin/medical-supplies">Quản lý danh mục vật tư</RouterLink></p>
    <form class="toolbar" @submit.prevent="loadStock(1)"><label>Tìm mã/tên vật tư <input v-model="keyword" maxlength="200" /></label><button :disabled="loading">Tìm kiếm</button></form>
    <p v-if="loadError" role="alert">{{ loadError }}</p>
    <p v-if="loading" role="status">Đang tải tồn kho…</p>
    <div class="table-wrap"><table><caption>Tồn hiện tại (cảnh báo khi tồn không vượt ngưỡng)</caption><thead><tr><th scope="col">Mã / vật tư</th><th scope="col">Đơn vị</th><th scope="col">Tồn</th><th scope="col">Ngưỡng</th><th scope="col">Trạng thái</th><th v-if="canWrite" scope="col">Chọn nhập</th></tr></thead><tbody>
      <tr v-for="item in items" :key="item.medicalSupplyId"><td>{{ item.code }} — {{ item.name }}</td><td>{{ item.unit }}</td><td>{{ item.onHand }}</td><td>{{ item.minimumStockThreshold }}</td><td>{{ !item.isActive ? 'Ngừng dùng' : item.onHand <= item.minimumStockThreshold ? 'Tồn thấp' : 'Đang dùng' }}</td><td v-if="canWrite"><button :disabled="locked || !item.isActive || lines.length >= 50 || lines.some(x => x.medicalSupplyId === item.medicalSupplyId)" :aria-label="`Thêm ${item.name} vào phiếu nhập`" @click="add(item)">Thêm</button></td></tr>
      <tr v-if="!loading && items.length === 0"><td :colspan="canWrite ? 6 : 5">Chưa có vật tư phù hợp.</td></tr>
    </tbody></table></div>
    <div class="toolbar"><button :disabled="loading || page <= 1" @click="loadStock(page - 1)">Trang trước</button><span>Trang {{ page }}</span><button :disabled="loading || !hasNext" @click="loadStock(page + 1)">Trang sau</button></div>
    <template v-if="canWrite">
      <h2>Lập phiếu nhập</h2>
      <p v-if="store.pending" role="status">Phiếu đang chờ xác định kết quả. Dùng Gửi lại phiếu; giữ nguyên dữ liệu và mã gửi. Nếu tải lại trang, hãy kiểm tra lịch sử trước khi lập phiếu khác.</p>
      <p v-if="store.saved" role="status">Đã ghi nhận phiếu {{ store.saved.code }}.</p>
      <p v-if="store.error" role="alert">{{ store.error }}</p><p v-if="formError" role="alert">{{ formError }}</p>
      <form @submit.prevent="submit">
        <fieldset :disabled="locked"><legend>Thông tin phiếu</legend>
          <label>Nhà cung cấp <input v-model="supplier" required maxlength="200" @input="confirmed = false" /></label>
          <label>Ghi chú <textarea v-model="notes" maxlength="500" @input="confirmed = false" /></label>
          <p v-if="lines.length === 0">Chọn vật tư từ bảng tồn phía trên.</p>
          <div v-for="line in lines" :key="line.medicalSupplyId" class="receipt-line"><span>{{ line.code }} — {{ line.name }} ({{ line.unit }})</span><label :for="`quantity-${line.medicalSupplyId}`">Số lượng</label><input :id="`quantity-${line.medicalSupplyId}`" v-model.number="line.quantity" type="number" min="1" max="2147483647" step="1" required @input="confirmed = false" /><button type="button" :aria-label="`Bỏ ${line.name}`" @click="lines = lines.filter(x => x.medicalSupplyId !== line.medicalSupplyId); confirmed = false">Bỏ</button></div>
          <button type="button" @click="refreshSelection">Tải lại danh mục và bỏ các dòng đã chọn</button>
          <label class="confirm"><input v-model="confirmed" type="checkbox" required /> Tôi đã kiểm tra vật tư, đơn vị và số lượng. Xác nhận sẽ cộng tồn và lưu phiếu.</label>
        </fieldset>
        <button type="submit" :disabled="store.busy || (!store.pending && (!confirmed || lines.length === 0))">{{ store.busy ? 'Đang gửi…' : store.pending ? 'Gửi lại phiếu' : 'Xác nhận nhập kho' }}</button>
      </form>
      <h2>Phiếu đã xác nhận</h2><p>Phiếu giữ nguyên nội dung tại thời điểm nhập.</p><button :disabled="historyLoading" @click="loadHistory()">Tải lại lịch sử</button><p v-if="historyError" role="alert">{{ historyError }}</p>
      <details v-for="receipt in receipts" :key="receipt.id"><summary>{{ receipt.code }} — {{ date(receipt.confirmedAt) }} — {{ receipt.supplierName }}</summary><p>Người xác nhận: {{ receipt.confirmedByName }}</p><p v-if="receipt.notes">{{ receipt.notes }}</p><ul><li v-for="line in receipt.lines" :key="line.medicalSupplyId">{{ line.code }} — {{ line.name }}: {{ line.quantity }} {{ line.unit }}</li></ul></details>
      <p v-if="!historyLoading && receipts.length === 0">Chưa có phiếu nhập.</p><button v-if="hasMore" :disabled="historyLoading" @click="loadHistory(true)">Xem phiếu cũ hơn</button>
    </template>
  </section>
</template>
<style scoped>
.stock-page { max-width: 1100px; margin: auto; padding: 1rem; color: #172b3a; }
.toolbar, .receipt-line { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; margin-block: 1rem; }
.table-wrap { overflow-x: auto; } table { width: 100%; border-collapse: collapse; } th, td { text-align: left; padding: .75rem; border-bottom: 1px solid #cbd5df; } caption { text-align: left; padding-block: .5rem; }
fieldset { display: grid; gap: 1rem; padding: 1rem; border: 1px solid #b8c5cf; margin-block: 1rem; } label { display: grid; gap: .35rem; } input, textarea, button { font: inherit; padding: .5rem; } textarea { min-height: 5rem; } .receipt-line input { width: 8rem; } .confirm { display: flex; gap: .75rem; align-items: start; }
button { min-height: 44px; cursor: pointer; } button:disabled { cursor: default; } :focus-visible { outline: 3px solid #2168aa; outline-offset: 3px; } [role="alert"] { color: #9a241d; } details { padding: 1rem; border-bottom: 1px solid #cbd5df; } summary { cursor: pointer; min-height: 32px; }
</style>
