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
const activeLeftTab = ref<'stock' | 'history'>('stock');
let generation = 0, historyGeneration = 0;

const locked = computed(() => store.busy || !!store.pending);
const lowStockCount = computed(() => items.value.filter(x => x.isActive && x.onHand <= x.minimumStockThreshold).length);
const totalReceiptUnits = computed(() => lines.value.reduce((sum, x) => sum + (Number.isFinite(x.quantity) ? x.quantity : 0), 0));

function isAdded(id: number) {
  return lines.value.some(x => x.medicalSupplyId === id);
}

watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => {
  generation++; historyGeneration++;
  items.value = []; receipts.value = []; lines.value = [];
  supplier.value = ''; notes.value = '';
  loading.value = false; historyLoading.value = false;
  hasNext.value = false; hasMore.value = false;
  loadError.value = ''; historyError.value = ''; confirmed.value = false;
  activeLeftTab.value = 'stock';
});
onUnmounted(() => { generation++; historyGeneration++; });

async function loadStock(nextPage = 1) {
  const current = ++generation; loading.value = true; loadError.value = '';
  try {
    const result = await stockReceiptsService.stock(keyword.value, nextPage);
    if (current !== generation) return;
    items.value = result.items; page.value = result.page; hasNext.value = result.hasNextPage;
  } catch {
    if (current === generation) loadError.value = 'Không tải được tồn kho. Vui lòng thử lại.';
  } finally {
    if (current === generation) loading.value = false;
  }
}

async function loadHistory(more = false) {
  if (!canWrite.value || historyLoading.value) return;
  const current = ++historyGeneration; historyLoading.value = true; historyError.value = '';
  try {
    const result = await stockReceiptsService.list(more ? receipts.value.at(-1)?.id ?? 0 : 0);
    if (current !== historyGeneration) return;
    receipts.value = more ? [...receipts.value, ...result.items.filter(x => !receipts.value.some(old => old.id === x.id))] : result.items;
    hasMore.value = result.hasMore;
  } catch {
    if (current === historyGeneration) historyError.value = 'Không tải được lịch sử phiếu.';
  } finally {
    if (current === historyGeneration) historyLoading.value = false;
  }
}

function add(item: StockItem) {
  if (!locked.value && item.isActive && lines.value.length < 50 && !lines.value.some(x => x.medicalSupplyId === item.medicalSupplyId)) {
    lines.value.push({ ...item, quantity: 1 });
    confirmed.value = false;
  }
}

async function refreshSelection() {
  if (locked.value) return;
  lines.value = []; confirmed.value = false;
  await loadStock();
}

async function submit() {
  formError.value = '';
  if (!store.pending) {
    if (!supplier.value.trim() || supplier.value.trim().length > 200 || /[\u0000-\u001f\u007f]/.test(supplier.value) || notes.value.length > 500 || !confirmed.value || lines.value.length === 0 || lines.value.some(x => !Number.isInteger(x.quantity) || x.quantity < 1 || x.quantity > 2147483647)) {
      formError.value = 'Nhập nhà cung cấp, số lượng nguyên dương và xác nhận phiếu (tối đa 50 dòng).';
      return;
    }
    await store.submit({
      requestId: crypto.randomUUID(),
      confirmed: true,
      supplierName: supplier.value.trim(),
      notes: notes.value.trim() || null,
      lines: lines.value.map(x => ({ medicalSupplyId: x.medicalSupplyId, quantity: x.quantity, expectedSupplyVersion: x.supplyVersion }))
    });
  } else {
    await store.submit();
  }
  if (store.saved) {
    supplier.value = ''; notes.value = ''; lines.value = []; confirmed.value = false;
    await Promise.all([loadStock(page.value), loadHistory()]);
  }
}

function date(value: string) {
  return new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
}

onMounted(() => { void loadStock(); void loadHistory(); });
</script>

<template>
  <section class="stock-page">
    <header class="page-topbar">
      <div class="topbar-main">
        <div class="header-icon" aria-hidden="true">📦</div>
        <div>
          <h1>Tồn kho &amp; phiếu nhập vật tư</h1>
          <p class="subtitle">Chỉ cộng tồn kho thực tế sau khi bấm xác nhận phiếu nhập; hỗ trợ chọn nhanh vật tư ngay trên một màn hình.</p>
        </div>
      </div>

      <div class="topbar-right">
        <div class="kpi-strip" aria-label="Tóm tắt nhanh">
          <span class="kpi-pill">Hiển thị: <strong>{{ items.length }}</strong></span>
          <span class="kpi-pill" :class="{ 'kpi-pill--warn': lowStockCount > 0 }">Tồn thấp: <strong>{{ lowStockCount }}</strong></span>
          <span v-if="canWrite" class="kpi-pill kpi-pill--accent">Đã chọn nhập: <strong>{{ lines.length }}/50</strong></span>
        </div>
        <RouterLink v-if="auth.role === 'ADMIN'" class="btn btn-outline-sm" to="/management/admin/medical-supplies">⚙️ Danh mục vật tư</RouterLink>
      </div>
    </header>

    <div class="workspace-grid" :class="{ 'workspace-grid--single': !canWrite }">
      <!-- Cột trái: Bảng tồn kho & Lịch sử phiếu nhập -->
      <div class="card workspace-main">
        <div v-if="canWrite" class="tab-bar" role="tablist" aria-label="Chế độ xem kho vật tư">
          <button
            type="button"
            role="tab"
            :aria-selected="activeLeftTab === 'stock'"
            class="tab-btn"
            :class="{ active: activeLeftTab === 'stock' }"
            @click="activeLeftTab = 'stock'"
          >
            Bảng tồn kho hiện tại
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="activeLeftTab === 'history'"
            class="tab-btn"
            :class="{ active: activeLeftTab === 'history' }"
            @click="activeLeftTab = 'history'"
          >
            Lịch sử phiếu nhập ({{ receipts.length }})
          </button>
        </div>

        <template v-if="!canWrite || activeLeftTab === 'stock'">
          <div class="main-toolbar">
            <div class="toolbar-title">
              <h2>Bảng tồn kho hiện tại</h2>
              <span class="caption-hint">Cảnh báo cam khi tồn chạm hoặc dưới ngưỡng tối thiểu.</span>
            </div>
            <form class="inline-search" @submit.prevent="loadStock(1)">
              <div class="input-with-icon">
                <span class="input-icon" aria-hidden="true">🔍</span>
                <input
                  id="stock-search-input"
                  v-model="keyword"
                  placeholder="Tìm mã hoặc tên vật tư..."
                  maxlength="200"
                  aria-label="Tìm kiếm vật tư"
                />
              </div>
              <button class="btn btn-primary btn-sm" :disabled="loading">{{ loading ? 'Đang tìm…' : 'Tìm kiếm' }}</button>
              <button type="button" class="btn btn-outline-sm" :disabled="loading" @click="loadStock(page)">↻ Tải lại</button>
            </form>
          </div>

          <div v-if="loadError" class="alert-box alert-error" role="alert">⚠️ {{ loadError }}</div>
          <div v-if="loading" class="loading-state-sm" role="status">Đang tải dữ liệu tồn kho…</div>

          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã / Tên vật tư</th>
                  <th scope="col">ĐVT</th>
                  <th scope="col" class="text-right">Tồn</th>
                  <th scope="col" class="text-right">Ngưỡng</th>
                  <th scope="col">Trạng thái</th>
                  <th v-if="canWrite" scope="col" class="text-center">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in items"
                  :key="item.medicalSupplyId"
                  :class="{ 'row-selected': canWrite && isAdded(item.medicalSupplyId) }"
                >
                  <td>
                    <div class="item-name">{{ item.name }}</div>
                    <div class="item-code">{{ item.code }}</div>
                  </td>
                  <td><span class="unit-tag">{{ item.unit }}</span></td>
                  <td class="text-right font-bold">{{ item.onHand.toLocaleString('vi-VN') }}</td>
                  <td class="text-right text-muted">{{ item.minimumStockThreshold.toLocaleString('vi-VN') }}</td>
                  <td>
                    <span v-if="!item.isActive" class="badge badge-disabled">Ngừng dùng</span>
                    <span v-else-if="item.onHand <= item.minimumStockThreshold" class="badge badge-warning">⚠️ Tồn thấp</span>
                    <span v-else class="badge badge-success">🟢 Bình thường</span>
                  </td>
                  <td v-if="canWrite" class="text-center">
                    <button
                      type="button"
                      class="btn btn-action-sm"
                      :class="{ 'btn-action-done': isAdded(item.medicalSupplyId) }"
                      :disabled="locked || !item.isActive || lines.length >= 50 || isAdded(item.medicalSupplyId)"
                      :aria-label="`Thêm ${item.name} vào phiếu nhập`"
                      @click="add(item)"
                    >
                      {{ isAdded(item.medicalSupplyId) ? '✓ Đã thêm' : '+ Thêm' }}
                    </button>
                  </td>
                </tr>
                <tr v-if="!loading && items.length === 0">
                  <td :colspan="canWrite ? 6 : 5" class="empty-cell">Không có vật tư nào phù hợp với bộ lọc.</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="pagination-bar">
            <span class="page-info">Trang <strong>{{ page }}</strong></span>
            <div class="pagination-controls">
              <button class="btn btn-outline-sm" :disabled="loading || page <= 1" @click="loadStock(page - 1)">← Trang trước</button>
              <button class="btn btn-outline-sm" :disabled="loading || !hasNext" @click="loadStock(page + 1)">Trang sau →</button>
            </div>
          </div>
        </template>

        <!-- Tab Lịch sử phiếu nhập -->
        <template v-else-if="canWrite && activeLeftTab === 'history'">
          <div class="main-toolbar">
            <div class="toolbar-title">
              <h2>📜 Lịch sử phiếu đã xác nhận</h2>
              <span class="caption-hint">Nội dung phiếu được lưu trữ bất biến tại thời điểm xác nhận nhập kho.</span>
            </div>
            <button class="btn btn-outline-sm" :disabled="historyLoading" @click="loadHistory()">🔄 Tải lại lịch sử</button>
          </div>

          <div v-if="historyError" class="alert-box alert-error" role="alert">⚠️ {{ historyError }}</div>
          <div v-if="historyLoading && receipts.length === 0" class="loading-state-sm" role="status">Đang tải lịch sử phiếu…</div>

          <div class="receipts-accordion">
            <details v-for="receipt in receipts" :key="receipt.id" class="receipt-detail-item">
              <summary class="receipt-summary">
                <div class="summary-left">
                  <span class="receipt-code">{{ receipt.code }}</span>
                  <span class="receipt-date">🕒 {{ date(receipt.confirmedAt) }}</span>
                </div>
                <div class="summary-right">
                  <span class="receipt-supplier">🏢 {{ receipt.supplierName }}</span>
                </div>
              </summary>
              <div class="receipt-detail-body">
                <div class="detail-meta">
                  <div>Người xác nhận: <strong>{{ receipt.confirmedByName }}</strong></div>
                  <div v-if="receipt.notes">Ghi chú: <em>{{ receipt.notes }}</em></div>
                </div>
                <ul class="detail-lines">
                  <li v-for="line in receipt.lines" :key="line.medicalSupplyId">
                    <span class="line-desc"><strong>{{ line.code }}</strong> — {{ line.name }}:</span>
                    <span class="line-val"><strong>{{ line.quantity.toLocaleString('vi-VN') }}</strong> {{ line.unit }}</span>
                  </li>
                </ul>
              </div>
            </details>
          </div>

          <p v-if="!historyLoading && receipts.length === 0" class="empty-history">Chưa có phiếu nhập kho nào được xác nhận.</p>
          <div v-if="hasMore" class="load-more-wrap">
            <button class="btn btn-outline-sm" :disabled="historyLoading" @click="loadHistory(true)">Xem phiếu cũ hơn ↓</button>
          </div>
        </template>
      </div>

      <!-- Cột phải sticky: Phiếu nhập kho -->
      <aside v-if="canWrite" class="card workspace-side" aria-label="Biểu mẫu lập phiếu nhập kho">
        <div class="side-header">
          <h2>📝 Lập phiếu nhập kho</h2>
          <span class="draft-counter">{{ lines.length }}/50 dòng · {{ totalReceiptUnits.toLocaleString('vi-VN') }} ĐV</span>
        </div>

        <div v-if="store.pending" class="alert-box alert-warning" role="status">
          ⏳ Phiếu đang chờ xác định kết quả. Bấm <strong>Gửi lại phiếu nhập</strong>; giữ nguyên dữ liệu.
        </div>
        <div v-if="store.saved" class="alert-box alert-success" role="status">
          ✅ Đã cộng tồn thành công cho phiếu <strong>{{ store.saved.code }}</strong>.
        </div>
        <div v-if="store.error" class="alert-box alert-error" role="alert">⚠️ {{ store.error }}</div>
        <div v-if="formError" class="alert-box alert-error" role="alert">⚠️ {{ formError }}</div>

        <form class="receipt-form" @submit.prevent="submit">
          <fieldset class="form-fieldset" :disabled="locked">
            <div class="form-group">
              <label for="supplier-input">Nhà cung cấp <span class="required">*</span></label>
              <input id="supplier-input" v-model="supplier" placeholder="Nhập tên đơn vị cung ứng..." required maxlength="200" @input="confirmed = false" />
            </div>

            <div class="form-group">
              <label for="receipt-notes-input">Ghi chú phiếu nhập</label>
              <textarea id="receipt-notes-input" v-model="notes" rows="2" placeholder="Số hóa đơn đỏ, số lô, ghi chú đợt nhập..." maxlength="500" @input="confirmed = false"></textarea>
            </div>

            <div class="lines-section">
              <div class="lines-head">
                <h3>Vật tư nhập ({{ lines.length }}/50)</h3>
                <button v-if="lines.length > 0" type="button" class="link-reset" @click="refreshSelection">Xóa tất cả</button>
              </div>

              <p v-if="lines.length === 0" class="empty-lines-prompt">
                👈 Bấm <strong>+ Thêm</strong> ở bảng bên trái để đưa vật tư vào phiếu nhập.
              </p>

              <div v-else class="lines-list-scroll">
                <div v-for="line in lines" :key="line.medicalSupplyId" class="receipt-line-item">
                  <div class="line-meta">
                    <span class="line-name">{{ line.name }}</span>
                    <span class="line-code">{{ line.code }} · ĐVT: <strong>{{ line.unit }}</strong> · Tồn: {{ line.onHand }}</span>
                  </div>
                  <div class="line-controls">
                    <input
                      :id="`quantity-${line.medicalSupplyId}`"
                      v-model.number="line.quantity"
                      type="number"
                      min="1"
                      max="2147483647"
                      step="1"
                      required
                      class="qty-input"
                      :aria-label="`Số lượng nhập ${line.name}`"
                      @input="confirmed = false"
                    />
                    <button
                      type="button"
                      class="btn-remove"
                      :aria-label="`Bỏ ${line.name}`"
                      title="Bỏ dòng"
                      @click="lines = lines.filter(x => x.medicalSupplyId !== line.medicalSupplyId); confirmed = false"
                    >
                      ×
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <label class="confirm-checkbox">
              <input v-model="confirmed" type="checkbox" required />
              <span>Tôi đã kiểm tra vật tư, bao gồm tên, đơn vị tính và số lượng thực tế. Bấm xác nhận sẽ chính thức <strong>cộng tồn kho và sinh phiếu nhập</strong>.</span>
            </label>
          </fieldset>

          <button
            type="submit"
            class="btn btn-primary btn-submit"
            :disabled="store.busy || (!store.pending && (!confirmed || lines.length === 0))"
          >
            {{ store.busy ? 'Đang gửi xử lý…' : store.pending ? '🔄 Gửi lại phiếu nhập' : '✅ Xác nhận nhập kho' }}
          </button>
        </form>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.stock-page {
  max-width: 1440px;
  margin: 0 auto;
  padding: 1rem 1.25rem;
  color: #1e293b;
}

.page-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
  margin-bottom: 0.9rem;
}

.topbar-main {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
}

.header-icon {
  font-size: 1.55rem;
  background: #e0f2fe;
  width: 2.75rem;
  height: 2.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 10px;
  flex-shrink: 0;
}

.topbar-main h1 {
  margin: 0;
  font-size: 1.35rem;
  color: #0f172a;
  font-weight: 700;
  line-height: 1.25;
}

.subtitle {
  margin: 0.15rem 0 0;
  color: #64748b;
  font-size: 0.85rem;
}

.topbar-right {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.kpi-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.kpi-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.28rem 0.65rem;
  border-radius: 999px;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  font-size: 0.8rem;
  color: #334155;
}

.kpi-pill--warn {
  background: #fef3c7;
  border-color: #fde68a;
  color: #92400e;
}

.kpi-pill--accent {
  background: #e0f2fe;
  border-color: #bae6fd;
  color: #0369a1;
}

.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(350px, 440px);
  gap: 1rem;
  align-items: start;
}

.workspace-grid--single {
  grid-template-columns: minmax(0, 1fr);
}

.card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1rem;
  min-width: 0;
}

.workspace-side {
  position: sticky;
  top: 0.85rem;
  border-color: #cbd5e1;
  box-shadow: 0 4px 16px rgba(15, 23, 42, 0.05);
}

.tab-bar {
  display: flex;
  gap: 0.4rem;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 0.65rem;
  margin-bottom: 0.85rem;
}

.tab-btn {
  padding: 0.4rem 0.85rem;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #f8fafc;
  color: #475569;
  font-size: 0.86rem;
  font-weight: 600;
  cursor: pointer;
}

.tab-btn.active {
  background: #e0f2fe;
  border-color: #bae6fd;
  color: #0369a1;
}

.main-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-bottom: 0.75rem;
}

.toolbar-title h2 {
  margin: 0;
  font-size: 1.05rem;
  color: #0f172a;
}

.caption-hint {
  font-size: 0.78rem;
  color: #64748b;
}

.inline-search {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.input-with-icon {
  position: relative;
  display: flex;
  align-items: center;
}

.input-icon {
  position: absolute;
  left: 0.65rem;
  font-size: 0.85rem;
  color: #94a3b8;
  pointer-events: none;
}

.input-with-icon input {
  width: 240px;
  height: 36px;
  padding: 0.35rem 0.65rem 0.35rem 2rem;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  font-size: 0.88rem;
}

.table-wrap {
  overflow-x: auto;
  margin-bottom: 0.65rem;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.data-table th {
  background: #f8fafc;
  padding: 0.5rem 0.65rem;
  font-size: 0.78rem;
  font-weight: 700;
  color: #475569;
  border-bottom: 1.5px solid #e2e8f0;
  white-space: nowrap;
}

.data-table td {
  padding: 0.5rem 0.65rem;
  border-bottom: 1px solid #f1f5f9;
  font-size: 0.86rem;
  vertical-align: middle;
}

.row-selected {
  background: #f0f9ff;
}

.item-name {
  font-weight: 600;
  color: #0f172a;
}

.item-code {
  font-size: 0.76rem;
  color: #64748b;
}

.unit-tag {
  display: inline-block;
  padding: 0.12rem 0.45rem;
  border-radius: 6px;
  background: #f1f5f9;
  font-size: 0.78rem;
  color: #334155;
}

.badge {
  display: inline-block;
  padding: 0.18rem 0.5rem;
  border-radius: 9999px;
  font-size: 0.74rem;
  font-weight: 600;
  white-space: nowrap;
}

.badge-success { background: #dcfce7; color: #15803d; }
.badge-warning { background: #fef3c7; color: #b45309; }
.badge-disabled { background: #f1f5f9; color: #64748b; }

.text-right { text-align: right; }
.text-center { text-align: center; }
.font-bold { font-weight: 700; }
.text-muted { color: #64748b; }
.empty-cell { text-align: center; padding: 1.5rem !important; color: #64748b; }

.btn {
  height: 38px;
  padding: 0 0.9rem;
  border-radius: 7px;
  font-weight: 600;
  font-size: 0.86rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  text-decoration: none;
}

.btn-primary { background: #0284c7; color: #fff; }
.btn-sm { height: 36px; padding: 0 0.85rem; }
.btn-outline-sm { height: 36px; padding: 0 0.75rem; border-radius: 7px; background: #fff; color: #334155; border: 1px solid #cbd5e1; font-size: 0.82rem; font-weight: 600; cursor: pointer; display: inline-flex; align-items: center; text-decoration: none; }
.btn-action-sm { height: 30px; padding: 0 0.65rem; border-radius: 6px; background: #f0f9ff; color: #0284c7; border: 1px solid #bae6fd; font-size: 0.8rem; font-weight: 600; white-space: nowrap; }
.btn-action-done { background: #dcfce7; color: #15803d; border-color: #bbf7d0; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 0.5rem;
}

.pagination-controls { display: flex; gap: 0.4rem; }
.page-info { font-size: 0.84rem; color: #475569; }

.side-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  padding-bottom: 0.6rem;
  border-bottom: 1px solid #f1f5f9;
}

.side-header h2 {
  margin: 0;
  font-size: 1.05rem;
  color: #0f172a;
}

.draft-counter {
  font-size: 0.78rem;
  font-weight: 700;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
}

.alert-box { padding: 0.55rem 0.75rem; border-radius: 8px; font-size: 0.84rem; margin-bottom: 0.7rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
.loading-state-sm { font-size: 0.85rem; color: #64748b; padding: 0.5rem 0; }

.form-fieldset { border: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.7rem; }
.form-group { display: flex; flex-direction: column; gap: 0.25rem; }
.form-group label { font-size: 0.8rem; font-weight: 600; color: #475569; }
.required { color: #dc2626; }
.form-group input, .form-group textarea { padding: 0.45rem 0.65rem; border: 1px solid #cbd5e1; border-radius: 7px; font-size: 0.88rem; font-family: inherit; }
.form-group input { height: 38px; }

.lines-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 9px; padding: 0.75rem; }
.lines-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
.lines-head h3 { margin: 0; font-size: 0.88rem; color: #0f172a; }
.link-reset { background: none; border: none; color: #dc2626; font-size: 0.78rem; font-weight: 600; cursor: pointer; padding: 0; }
.empty-lines-prompt { margin: 0; font-size: 0.82rem; color: #64748b; text-align: center; padding: 0.75rem 0.25rem; }

.lines-list-scroll {
  max-height: 290px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding-right: 0.15rem;
}

.receipt-line-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 7px;
  padding: 0.45rem 0.6rem;
}

.line-meta { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.line-name { font-size: 0.84rem; font-weight: 600; color: #0f172a; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.line-code { font-size: 0.74rem; color: #64748b; }

.line-controls { display: flex; align-items: center; gap: 0.35rem; flex-shrink: 0; }
.qty-input { width: 76px; height: 32px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0.2rem 0.4rem; text-align: right; font-size: 0.86rem; }
.btn-remove { background: #fee2e2; border: 1px solid #fecaca; color: #991b1b; width: 30px; height: 32px; border-radius: 6px; font-weight: 700; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; }

.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.45rem; font-size: 0.8rem; color: #334155; line-height: 1.35; }
.btn-submit { width: 100%; height: 42px; margin-top: 0.75rem; font-size: 0.94rem; }

.receipts-accordion { display: flex; flex-direction: column; gap: 0.45rem; }
.receipt-detail-item { border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; background: #fff; }
.receipt-summary { padding: 0.65rem 0.85rem; background: #f8fafc; cursor: pointer; display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; font-size: 0.85rem; }
.summary-left, .summary-right { display: flex; align-items: center; gap: 0.65rem; flex-wrap: wrap; }
.receipt-code { font-weight: 700; color: #0284c7; }
.receipt-detail-body { padding: 0.75rem 0.85rem; border-top: 1px solid #e2e8f0; font-size: 0.84rem; }
.detail-meta { display: flex; flex-wrap: wrap; gap: 0.5rem 1.25rem; margin-bottom: 0.45rem; color: #475569; }
.detail-lines { margin: 0; padding-left: 1rem; display: flex; flex-direction: column; gap: 0.25rem; }
.detail-lines li { display: flex; justify-content: space-between; gap: 0.5rem; }
.empty-history { text-align: center; color: #64748b; font-size: 0.88rem; padding: 1.5rem 0; }
.load-more-wrap { display: flex; justify-content: center; margin-top: 0.75rem; }

@media (max-width: 1080px) {
  .workspace-grid {
    grid-template-columns: minmax(0, 1fr);
  }
  .workspace-side {
    position: static;
  }
}

@media (max-width: 640px) {
  .stock-page { padding: 0.85rem; }
  .input-with-icon input { width: 100%; }
  .inline-search { width: 100%; }
}
</style>
