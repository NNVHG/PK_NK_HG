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
    <div class="header-banner">
      <div class="header-icon">📦</div>
      <div class="header-titles">
        <h1>Tồn kho & phiếu nhập vật tư</h1>
        <p class="subtitle">Quản lý tồn kho hiện hành, cảnh báo chạm ngưỡng và lập phiếu nhập kho vật tư y tế.</p>
      </div>
      <div v-if="canWrite" class="header-actions">
        <RouterLink class="btn btn-outline" to="/management/inventory/issues">📋 Ghi tiêu hao ca khám</RouterLink>
        <RouterLink v-if="auth.role === 'ADMIN'" class="btn btn-outline" to="/management/admin/medical-supplies">⚙️ Quản lý danh mục</RouterLink>
      </div>
    </div>

    <div class="rules-card">
      <div class="rule-item">
        <span class="rule-icon">💡</span>
        <span>Số lượng tính nguyên theo đơn vị danh mục. Hệ thống <strong>chỉ cộng tồn kho thực tế</strong> sau khi người dùng bấm xác nhận phiếu nhập.</span>
      </div>
    </div>

    <!-- Search Toolbar -->
    <form class="filters-card" @submit.prevent="loadStock(1)">
      <div class="filter-group">
        <label for="stock-search-input">Tìm kiếm vật tư</label>
        <div class="input-with-icon">
          <span class="input-icon">🔍</span>
          <input id="stock-search-input" v-model="keyword" placeholder="Nhập mã hoặc tên vật tư y tế..." maxlength="200" />
        </div>
      </div>
      <button class="btn btn-primary" :disabled="loading">
        <span v-if="loading">Đang tìm…</span>
        <span v-else>Tìm kiếm</span>
      </button>
    </form>

    <div v-if="loadError" class="alert-box alert-error" role="alert">⚠️ {{ loadError }}</div>
    <div v-if="loading" class="loading-state" role="status">
      <div class="spinner"></div>
      <p>Đang tải dữ liệu tồn kho…</p>
    </div>
    <div class="card table-card">
      <div class="card-header">
        <h2>Bảng tồn kho hiện tại</h2>
        <span class="caption-hint">Tự động cảnh báo màu cam khi số lượng tồn chạm hoặc dưới ngưỡng tối thiểu.</span>
      </div>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Mã / Tên vật tư</th>
              <th scope="col">Đơn vị</th>
              <th scope="col" class="text-right">Tồn hiện tại</th>
              <th scope="col" class="text-right">Ngưỡng tối thiểu</th>
              <th scope="col">Trạng thái tồn</th>
              <th v-if="canWrite" scope="col" class="text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.medicalSupplyId">
              <td>
                <div class="item-name">{{ item.name }}</div>
                <div class="item-code">{{ item.code }}</div>
              </td>
              <td><span class="unit-tag">{{ item.unit }}</span></td>
              <td class="text-right font-bold">{{ item.onHand.toLocaleString('vi-VN') }}</td>
              <td class="text-right text-muted">{{ item.minimumStockThreshold.toLocaleString('vi-VN') }}</td>
              <td>
                <span v-if="!item.isActive" class="badge badge-disabled">Ngừng dùng</span>
                <span v-else-if="item.onHand <= item.minimumStockThreshold" class="badge badge-warning">⚠️ Cảnh báo tồn thấp</span>
                <span v-else class="badge badge-success">🟢 Bình thường</span>
              </td>
              <td v-if="canWrite" class="text-center">
                <button
                  type="button"
                  class="btn btn-action-sm"
                  :disabled="locked || !item.isActive || lines.length >= 50 || lines.some(x => x.medicalSupplyId === item.medicalSupplyId)"
                  :aria-label="`Thêm ${item.name} vào phiếu nhập`"
                  @click="add(item)"
                >
                  + Thêm vào phiếu
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
          <button class="btn btn-outline" :disabled="loading || page <= 1" @click="loadStock(page - 1)">← Trang trước</button>
          <button class="btn btn-outline" :disabled="loading || !hasNext" @click="loadStock(page + 1)">Trang sau →</button>
        </div>
      </div>
    </div>
    <template v-if="canWrite">
      <div class="card form-card">
        <div class="card-header">
          <h2>📝 Lập phiếu nhập kho vật tư</h2>
        </div>

        <div v-if="store.pending" class="alert-box alert-warning" role="status">
          ⏳ Phiếu đang chờ xác định kết quả. Vui lòng bấm <strong>Gửi lại phiếu</strong>; giữ nguyên dữ liệu và mã gửi.
        </div>
        <div v-if="store.saved" class="alert-box alert-success" role="status">
          ✅ Đã ghi nhận và cộng tồn thành công cho phiếu <strong>{{ store.saved.code }}</strong>.
        </div>
        <div v-if="store.error" class="alert-box alert-error" role="alert">⚠️ {{ store.error }}</div>
        <div v-if="formError" class="alert-box alert-error" role="alert">⚠️ {{ formError }}</div>

        <form class="receipt-form" @submit.prevent="submit">
          <fieldset class="form-fieldset" :disabled="locked">
            <div class="form-grid">
              <div class="form-group">
                <label for="supplier-input">Nhà cung cấp <span class="required">*</span></label>
                <input id="supplier-input" v-model="supplier" placeholder="Nhập tên đơn vị cung ứng..." required maxlength="200" @input="confirmed = false" />
              </div>
              <div class="form-group full-width">
                <label for="receipt-notes-input">Ghi chú phiếu nhập</label>
                <textarea id="receipt-notes-input" v-model="notes" placeholder="Ghi chú đợt nhập, số hóa đơn đỏ, số lô..." maxlength="500" @input="confirmed = false"></textarea>
              </div>
            </div>

            <div class="lines-section">
              <h3>Danh sách vật tư nhập kho ({{ lines.length }}/50)</h3>
              <p v-if="lines.length === 0" class="empty-lines-prompt">
                👆 Chọn vật tư từ bảng phía trên để đưa vào danh sách nhập kho.
              </p>
              
              <div v-else class="lines-list">
                <div v-for="line in lines" :key="line.medicalSupplyId" class="receipt-line-item">
                  <div class="line-meta">
                    <span class="line-name">{{ line.name }}</span>
                    <span class="line-code">({{ line.code }}) · ĐVT: <strong>{{ line.unit }}</strong></span>
                  </div>
                  <div class="line-qty-wrap">
                    <label :for="`quantity-${line.medicalSupplyId}`">Số lượng nhập:</label>
                    <input
                      :id="`quantity-${line.medicalSupplyId}`"
                      v-model.number="line.quantity"
                      type="number"
                      min="1"
                      max="2147483647"
                      step="1"
                      required
                      class="qty-input"
                      @input="confirmed = false"
                    />
                  </div>
                  <button
                    type="button"
                    class="btn-remove"
                    :aria-label="`Bỏ ${line.name}`"
                    @click="lines = lines.filter(x => x.medicalSupplyId !== line.medicalSupplyId); confirmed = false"
                  >
                    🗑️ Bỏ
                  </button>
                </div>

                <div class="lines-actions">
                  <button type="button" class="btn btn-outline-sm" @click="refreshSelection">🔄 Tải lại và xóa tất cả dòng đã chọn</button>
                </div>
              </div>
            </div>

            <label class="confirm-checkbox">
              <input v-model="confirmed" type="checkbox" required />
              <span>Tôi đã kiểm tra kỹ tên vật tư, đơn vị tính và số lượng thực tế. Bấm xác nhận sẽ chính thức <strong>cộng tồn kho và sinh phiếu nhập</strong>.</span>
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
      </div>
      <!-- History Section -->
      <div class="card history-card">
        <div class="card-header history-header">
          <h2>📜 Lịch sử phiếu đã xác nhận</h2>
          <button class="btn btn-outline-sm" :disabled="historyLoading" @click="loadHistory()">🔄 Tải lại lịch sử</button>
        </div>
        <p class="history-subtitle">Nội dung phiếu được lưu trữ bất biến tại thời điểm xác nhận nhập kho.</p>

        <div v-if="historyError" class="alert-box alert-error" role="alert">⚠️ {{ historyError }}</div>

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
                  <span class="line-dot">•</span>
                  <span class="line-desc"><strong>{{ line.code }}</strong> — {{ line.name }}:</span>
                  <span class="line-val"><strong>{{ line.quantity.toLocaleString('vi-VN') }}</strong> {{ line.unit }}</span>
                </li>
              </ul>
            </div>
          </details>
        </div>

        <p v-if="!historyLoading && receipts.length === 0" class="empty-history">Chưa có phiếu nhập kho nào được xác nhận.</p>
        <div v-if="hasMore" class="load-more-wrap">
          <button class="btn btn-outline" :disabled="historyLoading" @click="loadHistory(true)">Xem phiếu cũ hơn ↓</button>
        </div>
      </div>
    </template>
  </section>
</template>
<style scoped>
.stock-page { max-width: 1100px; margin: 0 auto; padding: 1.5rem; color: #1e293b; }
.header-banner { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles { flex: 1; min-width: 260px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }
.header-actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
.rules-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem 1rem; margin-bottom: 1.25rem; display: flex; gap: 0.5rem; }
.rule-item { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; }
.rule-icon { font-size: 1.1rem; flex-shrink: 0; }
.filters-card { display: flex; gap: 1rem; align-items: flex-end; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.25rem; margin-bottom: 1.5rem; }
.filter-group { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; }
.filter-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.input-with-icon { position: relative; display: flex; align-items: center; }
.input-icon { position: absolute; left: 0.75rem; font-size: 0.9rem; color: #94a3b8; pointer-events: none; }
.input-with-icon input { width: 100%; height: 44px; padding: 0.5rem 0.75rem 0.5rem 2.25rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; }
.card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; margin-bottom: 1.5rem; padding: 1.25rem; }
.card-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; }
.card-header h2 { margin: 0; font-size: 1.2rem; color: #0f172a; }
.table-wrap { overflow-x: auto; margin-bottom: 1rem; }
.data-table { width: 100%; border-collapse: collapse; text-align: left; }
.data-table th { background: #f8fafc; padding: 0.75rem 1rem; font-size: 0.825rem; font-weight: 700; color: #475569; border-bottom: 2px solid #e2e8f0; }
.data-table td { padding: 0.75rem 1rem; border-bottom: 1px solid #f1f5f9; font-size: 0.9rem; }
.badge { display: inline-block; padding: 0.25rem 0.6rem; border-radius: 9999px; font-size: 0.75rem; font-weight: 600; }
.badge-success { background: #dcfce7; color: #15803d; }
.badge-warning { background: #fef3c7; color: #b45309; }
.badge-disabled { background: #f1f5f9; color: #64748b; }
.text-right { text-align: right; } .text-center { text-align: center; } .font-bold { font-weight: 700; } .text-muted { color: #64748b; }
.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; text-decoration: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-action-sm { height: 36px; padding: 0 0.85rem; border-radius: 6px; background: #f0f9ff; color: #0284c7; border: 1px solid #bae6fd; font-size: 0.85rem; }
.btn-outline-sm { height: 36px; padding: 0 0.85rem; border-radius: 6px; background: #fff; color: #334155; border: 1px solid #cbd5e1; font-size: 0.85rem; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.pagination-bar { display: flex; justify-content: space-between; align-items: center; padding-top: 0.75rem; border-top: 1px solid #f1f5f9; }
.alert-box { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
.loading-state { text-align: center; padding: 2rem; color: #64748b; }
.form-fieldset { border: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 1rem; }
.form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1rem; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group.full-width { grid-column: 1 / -1; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.form-group input, .form-group textarea { padding: 0.5rem 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; }
.form-group input { height: 44px; }
.lines-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; }
.receipt-line-item { display: flex; align-items: center; justify-content: space-between; background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.65rem 1rem; gap: 0.75rem; margin-bottom: 0.5rem; }
.qty-input { width: 100px; height: 38px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0.25rem 0.5rem; text-align: right; }
.btn-remove { background: #fee2e2; border: 1px solid #fecaca; color: #991b1b; height: 38px; padding: 0 0.75rem; border-radius: 6px; cursor: pointer; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; }
.btn-submit { width: 100%; height: 48px; margin-top: 1rem; font-size: 1.05rem; }
.receipt-detail-item { border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; background: #fff; margin-bottom: 0.5rem; }
.receipt-summary { padding: 0.85rem 1rem; background: #f8fafc; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }
.receipt-code { font-weight: 700; color: #0284c7; }
.receipt-detail-body { padding: 1rem; border-top: 1px solid #e2e8f0; }
@media (max-width: 640px) { .stock-page { padding: 1rem; } }
</style>
