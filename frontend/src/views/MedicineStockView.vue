<template>
  <section v-if="auth.role === 'ADMIN'" class="page-container" aria-labelledby="medicine-stock-title">
    <div class="page-header">
      <div>
        <p class="eyebrow">QUẢN KHO DƯỢC</p>
        <h1 id="medicine-stock-title">Nhập kho & Tồn thuốc thực tế</h1>
        <p class="subtitle">Kiểm kê tồn kho, lập phiếu nhập thuốc và đối soát lịch sử nhập dược phẩm.</p>
      </div>
      <div class="header-actions">
        <router-link to="/management/admin/medicines" class="button button-secondary">
          <span class="btn-icon">📋</span> Danh mục & giá thuốc
        </router-link>
      </div>
    </div>

    <div class="info-banner">
      <div class="banner-icon">🛡️</div>
      <div class="banner-content">
        <strong>Quy định bảo toàn kho dược:</strong> Nhập số lượng thực tế theo đúng đơn vị. Phiếu chỉ cộng tồn sau khi xác nhận. Thuốc trừ tồn khi giao bệnh nhân; hoàn tác không tự cộng lại kho.
      </div>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="store.pending" class="alert alert-warning" role="alert">
      Phiếu đang chờ xác minh kết quả. Bấm nút dưới để gửi lại với cùng mã giao dịch tránh cộng tồn hai lần.
      <div class="mt-2">
        <button class="button button-primary" :disabled="busy" @click="retry">Gửi lại phiếu đang chờ</button>
      </div>
    </div>

    <div class="stock-layout">
      <!-- Cột trái: Tìm và chọn thuốc -->
      <div class="panel stock-list-col">
        <div class="panel-header">
          <h2 class="panel-title">Chọn thuốc kiểm tồn</h2>
          <span class="count-badge">{{ medicines.length }} mục</span>
        </div>

        <div class="panel-body">
          <form class="stock-search-form" @submit.prevent="search(1)">
            <div class="input-icon-wrapper">
              <span class="field-icon">🔍</span>
              <input v-model="keyword" placeholder="Mã hoặc tên thuốc..." maxlength="200" :disabled="busy || !!store.pending" />
            </div>
            <button class="button button-secondary" type="submit" :disabled="busy || listLoading || !!store.pending">
              {{ listLoading ? '...' : 'Tìm' }}
            </button>
          </form>

          <div v-if="listLoading" class="loading-state">Đang tải thuốc...</div>

          <div v-else-if="medicines.length > 0" class="medicine-item-list">
            <button
              v-for="medicine in medicines"
              :key="medicine.id"
              class="med-select-btn"
              :class="{ 'med-select-btn--active': detail?.medicineId === medicine.id }"
              :disabled="busy || !!store.pending"
              @click="select(medicine.id)"
            >
              <div class="med-btn-top">
                <span class="med-btn-code">{{ medicine.code }}</span>
                <span v-if="!medicine.isActive" class="med-btn-inactive">Ngừng dùng</span>
              </div>
              <div class="med-btn-name">{{ medicine.name }}</div>
              <div class="med-btn-unit">Đơn vị: {{ medicine.unit }}</div>
            </button>
          </div>

          <div v-else-if="searched" class="empty-state">Không tìm thấy thuốc.</div>

          <div class="mini-pagination">
            <button class="button button-quiet" :disabled="busy || listLoading || !!store.pending || page <= 1" @click="search(page - 1)">
              ← Trước
            </button>
            <span class="mini-page-text">Trang {{ page }}</span>
            <button class="button button-quiet" :disabled="busy || listLoading || !!store.pending || !hasNext" @click="search(page + 1)">
              Sau →
            </button>
          </div>
        </div>
      </div>
      <!-- Cột phải: Chi tiết tồn và form nhập thuốc -->
      <div class="panel stock-detail-col">
        <div v-if="detail" class="detail-container">
          <div class="detail-hero">
            <div>
              <span class="code-badge">{{ detail.code }}</span>
              <h2 id="medicine-detail-title" ref="heading" tabindex="-1" class="detail-name">{{ detail.name }}</h2>
              <p class="detail-status">
                Trạng thái: 
                <span :class="detail.isActive ? 'text-success' : 'text-danger'">
                  {{ detail.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                </span>
              </p>
            </div>
            <div class="stock-counter-card">
              <span class="stock-label">TỒN KHO HIỆN TẠI</span>
              <span class="stock-number">{{ detail.onHand.toLocaleString('vi-VN') }}</span>
              <span class="stock-unit">{{ detail.unit }}</span>
            </div>
          </div>

          <div class="detail-toolbar">
            <button class="button button-quiet" :disabled="busy" @click="loadDetail(detail.medicineId)">
              🔄 Cập nhật tồn & phiếu mới
            </button>
          </div>

          <!-- Form nhập kho -->
          <div v-if="detail.isActive" class="receipt-box">
            <h3 class="box-title">Lập phiếu nhập thuốc thực tế</h3>
            <form class="receipt-form" @submit.prevent="receive">
              <div class="form-grid">
                <div class="form-field">
                  <label>Số lượng thực nhận ({{ detail.unit }}) <b>*</b></label>
                  <input v-model="quantity" inputmode="numeric" pattern="[0-9]+" required maxlength="10" placeholder="VD: 50, 100..." :disabled="busy || !!store.pending" @input="confirmed = false" />
                </div>

                <div class="form-field">
                  <label>Nhà cung cấp <b>*</b></label>
                  <input v-model="supplier" required maxlength="200" placeholder="VD: Dược Hậu Giang..." :disabled="busy || !!store.pending" @input="confirmed = false" />
                </div>

                <div class="form-field form-field--wide">
                  <label>Ghi chú nhập kho (Số lô, HSD)</label>
                  <textarea v-model="notes" rows="2" maxlength="500" placeholder="Số lô, hạn sử dụng..." :disabled="busy || !!store.pending" @input="confirmed = false"></textarea>
                </div>

                <div class="form-field form-field--wide">
                  <div class="confirm-box">
                    <label class="checkbox-label text-warning">
                      <input v-model="confirmed" type="checkbox" :disabled="busy || !!store.pending" />
                      <span>Tôi xác nhận đúng thuốc, đơn vị và đủ số lượng thực nhận từ NCC.</span>
                    </label>
                  </div>
                </div>
              </div>

              <div class="form-actions">
                <button class="button button-primary" type="submit" :disabled="busy || !!store.pending || !confirmed">
                  {{ busy ? 'Đang ghi nhận...' : 'Xác nhận nhập kho (+ Tồn)' }}
                </button>
              </div>
            </form>
          </div>

          <div v-else class="alert alert-warning mt-3">
            Thuốc này đang ngừng dùng. Kích hoạt lại trong danh mục thuốc trước khi nhập tồn.
          </div>

          <!-- Lịch sử nhập -->
          <div class="receipt-history">
            <h3 class="box-title">Lịch sử phiếu nhập gần đây</h3>
            <div v-if="detail.receipts.length === 0" class="empty-state">Chưa có phiếu nhập nào cho thuốc này.</div>
            <div v-else class="history-list">
              <div v-for="receipt in detail.receipts" :key="receipt.id" class="history-item">
                <div class="history-header">
                  <div class="history-code-group">
                    <span class="receipt-code">{{ receipt.code }}</span>
                    <span class="history-supplier">{{ receipt.supplierName }}</span>
                  </div>
                  <span class="receipt-qty">+{{ receipt.quantity }} {{ receipt.unit }}</span>
                </div>
                <div class="history-meta">
                  <span>Xác nhận: <b>{{ receipt.confirmedByName }}</b></span>
                  <span>Thời gian: {{ date(receipt.confirmedAt) }}</span>
                </div>
                <div v-if="receipt.notes" class="history-notes">Ghi chú: {{ receipt.notes }}</div>
              </div>
            </div>

            <button v-if="detail.hasMore && detail.receipts.length" class="button button-secondary w-full mt-3" :disabled="busy" @click="loadDetail(detail.medicineId, detail.receipts[detail.receipts.length - 1]!.id)">
              Tải thêm phiếu cũ hơn
            </button>
          </div>
        </div>

        <div v-else class="empty-detail-placeholder">
          <div class="placeholder-icon">📦</div>
          <h3>Chưa chọn loại thuốc</h3>
          <p>Vui lòng chọn thuốc ở danh sách bên trái để kiểm tra số lượng tồn hiện tại và lập phiếu nhập kho.</p>
        </div>
      </div>
    </div>
  </section>
  <div v-else class="alert alert-error" role="alert">Chỉ Admin mới có quyền nhập và kiểm kê kho thuốc.</div>
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
.page-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
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
}

.info-banner {
  display: flex;
  align-items: flex-start;
  gap: 0.85rem;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 10px;
  padding: 0.85rem 1.15rem;
  font-size: 0.85rem;
  color: #0369a1;
  line-height: 1.45;
}

.banner-icon {
  font-size: 1.2rem;
  flex-shrink: 0;
}

/* 2-column Layout */
.stock-layout {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 1.25rem;
  align-items: start;
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

.panel-body {
  padding: 1rem;
}

/* Stock search form */
.stock-search-form {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.85rem;
}

.input-icon-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  flex: 1;
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

input, select, textarea {
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

textarea {
  height: auto;
  padding: 0.65rem 0.85rem;
}

input:focus, select:focus, textarea:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

/* Medicine select list */
.medicine-item-list {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  max-height: 520px;
  overflow-y: auto;
  padding-right: 0.25rem;
}

.med-select-btn {
  display: block;
  width: 100%;
  text-align: left;
  padding: 0.7rem 0.85rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #f8fafc;
  cursor: pointer;
  transition: all 0.15s ease;
}

.med-select-btn:hover:not(:disabled) {
  border-color: #0284c7;
  background: #ffffff;
}

.med-select-btn--active {
  border-color: #0284c7 !important;
  background: #f0f9ff !important;
  box-shadow: 0 0 0 1px #0284c7;
}

.med-btn-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.2rem;
}

.med-btn-code {
  font-family: monospace;
  font-size: 0.75rem;
  font-weight: 750;
  color: #0369a1;
}

.med-btn-inactive {
  font-size: 0.68rem;
  color: #dc2626;
  background: #fef2f2;
  padding: 0.05rem 0.35rem;
  border-radius: 4px;
}

.med-btn-name {
  font-size: 0.85rem;
  font-weight: 700;
  color: #0f2d59;
  line-height: 1.3;
}

.med-btn-unit {
  font-size: 0.75rem;
  color: #64748b;
  margin-top: 0.2rem;
}

.mini-pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 0.85rem;
  padding-top: 0.65rem;
  border-top: 1px solid #f1f5f9;
}

.mini-page-text {
  font-size: 0.78rem;
  color: #64748b;
  font-weight: 600;
}

/* Detail Column */
.detail-container {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.detail-hero {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  padding-bottom: 1.15rem;
  border-bottom: 1px solid #f1f5f9;
}

.detail-name {
  font-size: 1.35rem;
  font-weight: 850;
  color: #0f2d59;
  margin: 0.35rem 0 0.15rem;
}

.detail-status {
  font-size: 0.825rem;
  color: #64748b;
  margin: 0;
}

.text-success { color: #059669; font-weight: 700; }
.text-danger { color: #dc2626; font-weight: 700; }

.code-badge {
  font-family: monospace;
  font-size: 0.78rem;
  font-weight: 750;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.stock-counter-card {
  background: #f8fafc;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  padding: 0.65rem 1.25rem;
  text-align: center;
  min-width: 140px;
}

.stock-label {
  display: block;
  font-size: 0.68rem;
  font-weight: 800;
  color: #64748b;
  letter-spacing: 0.05em;
}

.stock-number {
  display: block;
  font-size: 1.75rem;
  font-weight: 900;
  color: #0f2d59;
  line-height: 1.1;
  font-family: monospace;
}

.stock-unit {
  font-size: 0.78rem;
  color: #0284c7;
  font-weight: 700;
}

/* Receipt box */
.receipt-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1.15rem;
}

.box-title {
  font-size: 1rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0 0 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.form-field {
  display: grid;
  gap: 0.35rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
}

.form-field b { color: #dc2626; }
.form-field--wide { grid-column: 1 / -1; }

.confirm-box {
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 0.75rem 0.85rem;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.55rem;
  font-size: 0.825rem;
  font-weight: 600;
  cursor: pointer;
}

.text-warning { color: #92400e; }

.form-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 1rem;
}

/* History */
.receipt-history {
  margin-top: 0.5rem;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.history-item {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.75rem 1rem;
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.35rem;
}

.receipt-code {
  font-family: monospace;
  font-size: 0.78rem;
  font-weight: 750;
  color: #0369a1;
  margin-right: 0.5rem;
}

.history-supplier {
  font-size: 0.85rem;
  font-weight: 700;
  color: #334155;
}

.receipt-qty {
  font-family: monospace;
  font-size: 0.95rem;
  font-weight: 850;
  color: #059669;
  background: #ecfdf5;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
}

.history-meta {
  display: flex;
  gap: 1.25rem;
  font-size: 0.78rem;
  color: #64748b;
}

.history-notes {
  margin-top: 0.35rem;
  font-size: 0.78rem;
  color: #475569;
  font-style: italic;
  background: #f8fafc;
  padding: 0.35rem 0.55rem;
  border-radius: 4px;
}

/* Empty placeholder */
.empty-detail-placeholder {
  padding: 4rem 1.5rem;
  text-align: center;
  color: #64748b;
}

.placeholder-icon {
  font-size: 3rem;
  margin-bottom: 0.75rem;
}

.empty-detail-placeholder h3 {
  font-size: 1.15rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0 0 0.35rem;
}

.empty-detail-placeholder p {
  font-size: 0.875rem;
  margin: 0;
  max-width: 360px;
  margin: 0 auto;
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
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-primary { background: #0284c7; color: #ffffff; }
.button-primary:hover:not(:disabled) { background: #0369a1; }
.button-secondary { background: #ffffff; color: #334155; border-color: #cbd5e1; }
.button-secondary:hover:not(:disabled) { background: #f1f5f9; border-color: #94a3b8; }
.button-quiet { background: #ffffff; border-color: #cbd5e1; color: #475569; height: 32px; padding: 0 0.85rem; font-size: 0.8rem; }
.button-quiet:hover:not(:disabled) { background: #f1f5f9; color: #0f2d59; }
.button:disabled { cursor: not-allowed; opacity: 0.55; }
.btn-icon { font-size: 1.1rem; line-height: 1; }
.w-full { width: 100%; }
.mt-2 { margin-top: 0.5rem; }
.mt-3 { margin-top: 0.75rem; }

/* Alerts */
.alert { padding: 0.85rem 1.15rem; border-radius: 8px; font-size: 0.875rem; font-weight: 600; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-warning { background: #fffbeb; color: #92400e; border: 1px solid #fef3c7; }
.empty-state, .loading-state { text-align: center; padding: 2rem 1rem; color: #64748b; font-size: 0.875rem; }

@media (max-width: 860px) {
  .stock-layout {
    grid-template-columns: 1fr;
  }
}
</style>
