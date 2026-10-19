<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useStockIssuesStore } from '@/stores/stockIssues';
import { stockIssuesService, type StockIssue, type StockIssueTarget } from '@/services/stockIssues';
import { stockReceiptsService, type StockItem } from '@/services/stockReceipts';
const auth = useAuthStore(), store = useStockIssuesStore();
const targets = ref<StockIssueTarget[]>([]), targetMore = ref(false), targetBusy = ref(false), visitFilter = ref<number | ''>(''), targetError = ref('');
const selected = ref<StockIssueTarget | null>(null), lines = ref<(StockItem & { quantity: number })[]>([]), notes = ref(''), confirmed = ref(false), formError = ref('');
const items = ref<StockItem[]>([]), stockBusy = ref(false), stockError = ref(''), keyword = ref(''), page = ref(1), stockMore = ref(false);
const history = ref<StockIssue[]>([]), historyMore = ref(false), historyBusy = ref(false), historyError = ref('');
const locked = computed(() => store.busy || !!store.pending);
const visitId = computed(() => store.pending?.visitId ?? selected.value?.visitId ?? store.saved?.visitId ?? 0);
let targetGeneration = 0, stockGeneration = 0, historyGeneration = 0, identityGeneration = 0, active = true;
function clearIdentity() { targetGeneration++; stockGeneration++; historyGeneration++; targets.value = []; items.value = []; history.value = []; selected.value = null; lines.value = []; notes.value = ''; confirmed.value = false; targetBusy.value = false; stockBusy.value = false; historyBusy.value = false; targetMore.value = false; stockMore.value = false; historyMore.value = false; targetError.value = ''; stockError.value = ''; historyError.value = ''; formError.value = ''; }
watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { identityGeneration++; clearIdentity(); });
onUnmounted(() => { active = false; targetGeneration++; stockGeneration++; historyGeneration++; });
async function loadTargets(more = false) {
  if (locked.value || more && targetBusy.value) return;
  if (visitFilter.value !== '' && (!Number.isInteger(visitFilter.value) || visitFilter.value <= 0 || visitFilter.value > 2147483647)) { targetError.value = 'Mã ca phải là số nguyên dương.'; return; }
  const current = ++targetGeneration; targetBusy.value = true; targetError.value = '';
  try { const result = await stockIssuesService.targets(visitFilter.value === '' ? undefined : visitFilter.value, more ? targets.value.at(-1)?.visitId ?? 0 : 0); if (current !== targetGeneration) return; targets.value = more ? [...targets.value, ...result.items.filter(x => !targets.value.some(t => t.visitId === x.visitId))] : result.items; targetMore.value = result.hasMore; }
  catch { if (current === targetGeneration) targetError.value = 'Không tải được danh sách ca hoàn tất.'; }
  finally { if (current === targetGeneration) targetBusy.value = false; }
}
async function loadStock(next = 1) {
  const current = ++stockGeneration; stockBusy.value = true; stockError.value = '';
  try { const result = await stockReceiptsService.stock(keyword.value, next); if (current !== stockGeneration) return; items.value = result.items; page.value = result.page; stockMore.value = result.hasNextPage; }
  catch { if (current === stockGeneration) stockError.value = 'Không tải được tồn kho.'; }
  finally { if (current === stockGeneration) stockBusy.value = false; }
}
async function loadHistory(more = false) {
  if (visitId.value <= 0 || more && historyBusy.value) return;
  const current = ++historyGeneration, visit = visitId.value; historyBusy.value = true; historyError.value = '';
  try { const result = await stockIssuesService.list(visit, more ? history.value.at(-1)?.id ?? 0 : 0); if (current !== historyGeneration || visit !== visitId.value) return; history.value = more ? [...history.value, ...result.items.filter(x => !history.value.some(h => h.id === x.id))] : result.items; historyMore.value = result.hasMore; }
  catch { if (current === historyGeneration) historyError.value = 'Không tải được lịch sử tiêu hao của ca.'; }
  finally { if (current === historyGeneration) historyBusy.value = false; }
}
function choose(target: StockIssueTarget) { if (locked.value) return; selected.value = target; lines.value = []; notes.value = ''; confirmed.value = false; store.saved = null; history.value = []; historyMore.value = false; void loadHistory(); }
function add(item: StockItem) { if (!locked.value && visitId.value > 0 && item.isActive && item.onHand > 0 && lines.value.length < 50 && !lines.value.some(x => x.medicalSupplyId === item.medicalSupplyId)) { lines.value.push({ ...item, quantity: 1 }); confirmed.value = false; } }
async function refreshSelection() { if (locked.value) return; lines.value = []; confirmed.value = false; await loadStock(); }
async function submit() {
  const identity = identityGeneration;
  formError.value = '';
  if (!store.pending) {
    if (visitId.value <= 0 || !confirmed.value || notes.value.length > 500 || lines.value.length === 0 || lines.value.some(x => !Number.isInteger(x.quantity) || x.quantity <= 0 || x.quantity > x.onHand)) { formError.value = 'Chọn ca, nhập số nguyên dương không vượt tồn và xác nhận lượng thực tế.'; return; }
    await store.submit({ requestId: crypto.randomUUID(), confirmed: true, visitId: visitId.value, notes: notes.value.trim() || null, lines: lines.value.map(x => ({ medicalSupplyId: x.medicalSupplyId, quantity: x.quantity, expectedSupplyVersion: x.supplyVersion })) });
  } else await store.submit();
  if (!active || identity !== identityGeneration) return;
  if (store.saved) { lines.value = []; notes.value = ''; confirmed.value = false; await Promise.all([loadStock(page.value), loadHistory()]); }
}
function date(value: string | null) { return value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa ghi thời điểm'; }
onMounted(() => { void loadTargets(); void loadStock(); if (visitId.value > 0) void loadHistory(); });
</script>
<template>
  <section class="issue-page">
    <h1>Vật tư tiêu hao theo ca</h1><p>Nhập số lượng thực tế cho từng phiếu. Chỉ trừ tồn sau xác nhận; chưa gợi ý định mức.</p>
    <RouterLink to="/inventory/receipts">Xem tồn & nhập kho</RouterLink>
    <h2>1. Chọn ca đã hoàn tất</h2>
    <form class="toolbar" @submit.prevent="loadTargets()"><label>Mã ca (để trống để xem gần nhất)<input v-model.number="visitFilter" type="number" min="1" max="2147483647" step="1" :disabled="locked" /></label><button :disabled="locked || targetBusy">Tìm ca</button></form>
    <p v-if="targetError" role="alert">{{ targetError }}</p><p v-if="targetBusy" role="status">Đang tải ca…</p>
    <ul class="targets"><li v-for="target in targets" :key="target.visitId"><button :disabled="locked" :aria-pressed="selected?.visitId === target.visitId" @click="choose(target)">Ca #{{ target.visitId }} — {{ target.patientCode }} — {{ target.patientName }} — {{ date(target.endedAt) }}</button></li></ul>
    <p v-if="!targetBusy && targets.length === 0">Không có ca hoàn tất phù hợp.</p><button v-if="targetMore" :disabled="locked || targetBusy" @click="loadTargets(true)">Xem ca cũ hơn</button>
    <template v-if="visitId > 0">
      <h2>Ca #{{ visitId }} <span v-if="selected?.visitId === visitId">— {{ selected.patientCode }} — {{ selected.patientName }}</span></h2>
      <h3>Tiêu hao đã xác nhận</h3><p>Đối chiếu lịch sử để tránh ghi lại lượng đã trừ. Mỗi phiếu mới chỉ ghi phần bổ sung.</p><button :disabled="historyBusy" @click="loadHistory()">Tải lại lịch sử</button>
      <p v-if="historyError" role="alert">{{ historyError }}</p><p v-if="historyBusy" role="status">Đang tải lịch sử…</p>
      <details v-for="entry in history" :key="entry.id"><summary>{{ entry.code }} — {{ date(entry.confirmedAt) }} — {{ entry.confirmedByName }}</summary><p v-if="entry.notes">{{ entry.notes }}</p><ul><li v-for="line in entry.lines" :key="line.medicalSupplyId">{{ line.code }} — {{ line.name }}: {{ line.quantity }} {{ line.unit }}</li></ul></details>
      <p v-if="!historyBusy && !historyError && history.length === 0">Chưa có phiếu tiêu hao.</p><button v-if="historyMore" :disabled="historyBusy" @click="loadHistory(true)">Xem phiếu cũ hơn</button>
      <h2>2. Nhập vật tư thực tế</h2>
      <form class="toolbar" @submit.prevent="loadStock()"><label>Tìm mã/tên vật tư<input v-model="keyword" maxlength="200" /></label><button :disabled="stockBusy">Tìm vật tư</button></form>
      <p v-if="stockError" role="alert">{{ stockError }}</p><p v-if="stockBusy" role="status">Đang tải tồn…</p>
      <div class="table-wrap"><table><caption>Tồn kho hiện tại</caption><thead><tr><th scope="col">Vật tư</th><th scope="col">Đơn vị</th><th scope="col">Tồn</th><th scope="col">Trạng thái</th><th scope="col">Chọn</th></tr></thead><tbody><tr v-for="item in items" :key="item.medicalSupplyId"><td>{{ item.code }} — {{ item.name }}</td><td>{{ item.unit }}</td><td>{{ item.onHand }}</td><td>{{ !item.isActive ? 'Ngừng dùng' : item.onHand <= item.minimumStockThreshold ? 'Tồn thấp' : 'Đang dùng' }}</td><td><button :disabled="locked || !item.isActive || item.onHand <= 0 || lines.length >= 50 || lines.some(x => x.medicalSupplyId === item.medicalSupplyId)" :aria-label="`Chọn ${item.name}`" @click="add(item)">Thêm</button></td></tr><tr v-if="!stockBusy && items.length === 0"><td colspan="5">Không có vật tư phù hợp.</td></tr></tbody></table></div>
      <div class="toolbar"><button :disabled="stockBusy || page <= 1" @click="loadStock(page - 1)">Trang trước</button><span>Trang {{ page }}</span><button :disabled="stockBusy || !stockMore" @click="loadStock(page + 1)">Trang sau</button></div>
      <p v-if="store.pending" role="status">Phiếu cho ca #{{ store.pending.visitId }} chưa rõ kết quả. Gửi lại đúng phiếu; nếu tải lại trình duyệt, kiểm tra lịch sử trước khi lập phiếu khác.</p>
      <p v-if="store.saved" role="status">Đã xác nhận {{ store.saved.code }} cho ca #{{ store.saved.visitId }}.</p><p v-if="store.error" role="alert">{{ store.error }}</p><p v-if="formError" role="alert">{{ formError }}</p>
      <form @submit.prevent="submit"><fieldset :disabled="locked"><legend>3. Kiểm tra phiếu và xác nhận</legend>
        <p v-if="lines.length === 0">Chọn vật tư từ bảng tồn kho.</p>
        <div v-for="line in lines" :key="line.medicalSupplyId" class="receipt-line"><span>{{ line.code }} — {{ line.name }} ({{ line.unit }}, tồn lúc chọn: {{ line.onHand }})</span><label :for="`qty-${line.medicalSupplyId}`">Số thực tế</label><input :id="`qty-${line.medicalSupplyId}`" v-model.number="line.quantity" type="number" min="1" :max="line.onHand" step="1" required @input="confirmed = false" /><button type="button" :aria-label="`Bỏ ${line.name}`" @click="lines = lines.filter(x => x.medicalSupplyId !== line.medicalSupplyId); confirmed = false">Bỏ</button></div>
        <label>Ghi chú<textarea v-model="notes" maxlength="500" @input="confirmed = false" /></label><button type="button" @click="refreshSelection">Tải lại tồn và bỏ các dòng đã chọn</button>
        <label class="confirm"><input v-model="confirmed" type="checkbox" required /> Tôi đã đối chiếu ca, đơn vị và lượng thực tế. Xác nhận sẽ trừ tồn và lưu phiếu.</label>
      </fieldset><button type="submit" :disabled="store.busy || (!store.pending && (!confirmed || lines.length === 0))">{{ store.busy ? 'Đang gửi…' : store.pending ? 'Gửi lại phiếu' : 'Xác nhận tiêu hao' }}</button></form>
    </template>
  </section>
</template>
<style scoped>
.issue-page { max-width: 1100px; margin: auto; padding: 1rem; color: #172b3a; }.toolbar, .receipt-line { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; margin-block: 1rem; }.targets { padding-left: 1.25rem; }.targets li { margin-block: .5rem; }.targets button[aria-pressed="true"] { outline: 2px solid #2168aa; }.table-wrap { overflow-x: auto; }table { width: 100%; border-collapse: collapse; }th, td { text-align: left; padding: .75rem; border-bottom: 1px solid #cbd5df; }caption { text-align: left; padding-block: .5rem; }fieldset { display: grid; gap: 1rem; padding: 1rem; border: 1px solid #b8c5cf; margin-block: 1rem; }label { display: grid; gap: .35rem; }input, textarea, button { font: inherit; padding: .5rem; }textarea { min-height: 5rem; }.receipt-line input { width: 8rem; }.confirm { display: flex; gap: .75rem; align-items: start; }button { min-height: 44px; cursor: pointer; }button:disabled { cursor: default; }:focus-visible { outline: 3px solid #2168aa; outline-offset: 3px; }[role="alert"] { color: #9a241d; }details { padding: 1rem; border-bottom: 1px solid #cbd5df; }summary { cursor: pointer; min-height: 32px; }
</style>
