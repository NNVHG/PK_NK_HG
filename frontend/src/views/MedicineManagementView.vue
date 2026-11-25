<template>
  <section v-if="auth.role === 'ADMIN'" class="page-container" aria-labelledby="medicine-management-title">
    <header class="page-header">
      <div>
        <h1 id="medicine-management-title">Thuốc &amp; Kho dược phẩm</h1>
        <p class="subtitle">
          Quản lý danh mục thuốc, đơn giá bán niêm yết (giá &gt; 0 trước khi kê đơn) kết hợp kiểm tồn và lập phiếu nhập kho trực tiếp trên một màn hình.
        </p>
      </div>
      <div class="header-actions">
        <span class="count-pill">{{ total }} thuốc</span>
        <button class="button button-primary" :disabled="saving || loading || needsReview || Boolean(store.pending)" @click="open()">
          ＋ Thêm thuốc mới
        </button>
      </div>
    </header>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">
      Dữ liệu chưa đối soát. Bấm <button class="link-btn" @click="load(page)">Tải lại danh mục</button> trước khi thao tác tiếp.
    </div>
    <div v-if="store.pending" class="alert alert-warning alert-row" role="alert">
      <span>Phiếu nhập đang chờ xác minh kết quả (Mã thuốc #{{ store.pending.medicineId }}). Bấm để gửi lại với cùng mã giao dịch tránh cộng tồn hai lần.</span>
      <button class="button button-primary button-sm" :disabled="stockBusy" @click="retry">Gửi lại phiếu đang chờ</button>
    </div>

    <div class="unified-grid">
      <!-- Cột trái: Danh mục thuốc & giá bán -->
      <section class="panel catalog-col" aria-label="Danh mục thuốc phòng khám">
        <form class="catalog-toolbar" @submit.prevent="load(1)">
          <div class="toolbar-search">
            <input
              id="med-search"
              v-model="keyword"
              placeholder="Tìm mã, tên thuốc, hoạt chất, hàm lượng..."
              maxlength="200"
              aria-label="Tìm kiếm thuốc"
              :disabled="saving || Boolean(store.pending)"
            />
          </div>
          <label class="checkbox-label">
            <input v-model="includeInactive" type="checkbox" :disabled="saving || Boolean(store.pending)" />
            <span>Kể cả ngừng dùng</span>
          </label>
          <button class="button button-secondary" type="submit" :disabled="saving || loading || Boolean(store.pending)">
            {{ loading ? 'Đang tải…' : '🔍 Tìm' }}
          </button>
        </form>

        <div class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th>Mã / Tên thuốc</th>
                <th>Hoạt chất</th>
                <th>ĐVT</th>
                <th class="text-right">Giá bán</th>
                <th class="text-center">Trạng thái</th>
                <th class="text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="item in items"
                :key="item.id"
                class="catalog-row"
                :class="{ 'catalog-row--selected': stockDetail?.medicineId === item.id }"
                @click="selectStock(item.id)"
              >
                <td>
                  <div class="med-top-cell">
                    <span class="code-badge">{{ item.code }}</span>
                    <span v-if="item.strength" class="strength-pill">{{ item.strength }}</span>
                  </div>
                  <div class="med-name">{{ item.name }}</div>
                  <div v-if="item.defaultUsageInstruction" class="med-usage-hint" :title="item.defaultUsageInstruction">
                    HD: {{ item.defaultUsageInstruction }}
                  </div>
                </td>
                <td class="active-sub">{{ item.activeIngredient || '—' }}</td>
                <td><span class="unit-badge">{{ item.unit }}</span></td>
                <td class="text-right">
                  <span :class="item.unitPrice === '0' ? 'price-unset' : 'price-val'">
                    {{ item.unitPrice === '0' ? 'Chưa đặt giá' : money(item.unitPrice) }}
                  </span>
                </td>
                <td class="text-center">
                  <span class="status-pill" :class="item.isActive ? 'status-pill--active' : 'status-pill--inactive'">
                    {{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                  </span>
                </td>
                <td class="text-right actions-cell" @click.stop>
                  <button
                    type="button"
                    class="button button-quiet"
                    :disabled="saving || loading || needsReview || Boolean(store.pending)"
                    @click="open(item)"
                  >
                    Sửa
                  </button>
                  <button
                    type="button"
                    class="button button-stock"
                    :disabled="stockBusy || Boolean(store.pending)"
                    @click="selectStock(item.id)"
                  >
                    Kho
                  </button>
                </td>
              </tr>
              <tr v-if="!loading && !items.length">
                <td colspan="6" class="empty-state">Không có thuốc phù hợp.</td>
              </tr>
              <tr v-if="loading">
                <td colspan="6" class="empty-state">Đang tải danh mục thuốc…</td>
              </tr>
            </tbody>
          </table>
        </div>

        <footer class="pagination">
          <span class="page-info">Trang {{ page }} (Tổng {{ total }} thuốc)</span>
          <div class="page-btns">
            <button class="button button-quiet" type="button" :disabled="loading || saving || page <= 1" @click="load(page - 1)">← Trước</button>
            <button class="button button-quiet" type="button" :disabled="loading || saving || !hasNext" @click="load(page + 1)">Sau →</button>
          </div>
        </footer>
      </section>

      <!-- Cột phải sticky: Tồn kho thực tế & Nhập kho -->
      <aside class="panel stock-col" aria-label="Tồn kho và nhập thuốc thực tế">
        <div v-if="stockDetail" class="stock-container">
          <div class="stock-hero">
            <div class="stock-identity">
              <span class="code-badge">{{ stockDetail.code }}</span>
              <h2 id="medicine-detail-title" ref="heading" tabindex="-1">{{ stockDetail.name }}</h2>
              <div class="stock-meta-row">
                <span :class="stockDetail.isActive ? 'text-success' : 'text-danger'">
                  {{ stockDetail.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                </span>
                <button
                  type="button"
                  class="link-btn"
                  :disabled="stockBusy"
                  @click="loadStockDetail(stockDetail.medicineId)"
                >
                  ↻ Làm mới tồn
                </button>
              </div>
            </div>

            <div class="stock-counter-card">
              <span class="stock-label">TỒN HIỆN TẠI</span>
              <strong class="stock-number">{{ stockDetail.onHand.toLocaleString('vi-VN') }}</strong>
              <span class="stock-unit">{{ stockDetail.unit }}</span>
            </div>
          </div>

          <div class="tab-bar" role="tablist" aria-label="Chức năng kho thuốc">
            <button
              type="button"
              role="tab"
              :aria-selected="stockTab === 'receive'"
              class="tab-btn"
              :class="{ active: stockTab === 'receive' }"
              @click="stockTab = 'receive'"
            >
              Nhập kho (+ Tồn)
            </button>
            <button
              type="button"
              role="tab"
              :aria-selected="stockTab === 'history'"
              class="tab-btn"
              :class="{ active: stockTab === 'history' }"
              @click="stockTab = 'history'"
            >
              Lịch sử nhập ({{ stockDetail.receipts.length }})
            </button>
          </div>

          <div v-if="stockTab === 'receive'">
            <form v-if="stockDetail.isActive" class="receipt-form" @submit.prevent="receive">
              <div class="form-grid">
                <label class="form-field">
                  <span>Số lượng thực nhận ({{ stockDetail.unit }}) <b>*</b></span>
                  <input
                    v-model="quantity"
                    inputmode="numeric"
                    pattern="[0-9]+"
                    required
                    maxlength="10"
                    placeholder="VD: 50, 100..."
                    :disabled="stockBusy || Boolean(store.pending)"
                    @input="stockConfirmed = false"
                  />
                </label>

                <label class="form-field">
                  <span>Nhà cung cấp <b>*</b></span>
                  <input
                    v-model="supplier"
                    required
                    maxlength="200"
                    placeholder="VD: Dược Hậu Giang..."
                    :disabled="stockBusy || Boolean(store.pending)"
                    @input="stockConfirmed = false"
                  />
                </label>

                <label class="form-field form-field--wide">
                  <span>Ghi chú nhập kho (Số lô, HSD)</span>
                  <textarea
                    v-model="notes"
                    rows="2"
                    maxlength="500"
                    placeholder="Số lô, hạn sử dụng..."
                    :disabled="stockBusy || Boolean(store.pending)"
                    @input="stockConfirmed = false"
                  ></textarea>
                </label>
              </div>

              <div class="confirm-box">
                <label class="checkbox-label text-warning">
                  <input v-model="stockConfirmed" type="checkbox" :disabled="stockBusy || Boolean(store.pending)" />
                  <span>Tôi xác nhận đúng thuốc, đơn vị và đủ số lượng thực nhận từ NCC.</span>
                </label>
              </div>

              <p class="stock-policy-note">Thuốc trừ tồn khi giao bệnh nhân; hoàn tác không tự cộng lại kho.</p>

              <button class="button button-primary w-full" type="submit" :disabled="stockBusy || Boolean(store.pending) || !stockConfirmed">
                {{ stockBusy ? 'Đang ghi nhận…' : '✅ Xác nhận nhập kho (+ Tồn)' }}
              </button>
            </form>

            <div v-else class="alert alert-warning">
              Thuốc này đang ngừng dùng. Bấm <strong>Sửa</strong> ở danh sách để kích hoạt lại trước khi nhập tồn.
            </div>
          </div>

          <div v-else class="receipt-history">
            <div v-if="stockDetail.receipts.length === 0" class="empty-state">Chưa có phiếu nhập nào cho thuốc này.</div>
            <div v-else class="history-list">
              <div v-for="receipt in stockDetail.receipts" :key="receipt.id" class="history-item">
                <div class="history-header">
                  <div>
                    <span class="receipt-code">{{ receipt.code }}</span>
                    <span class="history-supplier">{{ receipt.supplierName }}</span>
                  </div>
                  <span class="receipt-qty">+{{ receipt.quantity }} {{ receipt.unit }}</span>
                </div>
                <div class="history-meta">
                  <span>👤 {{ receipt.confirmedByName }}</span>
                  <span>🕒 {{ date(receipt.confirmedAt) }}</span>
                </div>
                <div v-if="receipt.notes" class="history-notes">{{ receipt.notes }}</div>
              </div>
            </div>

            <button
              v-if="stockDetail.hasMore && stockDetail.receipts.length"
              type="button"
              class="button button-secondary w-full"
              :disabled="stockBusy"
              @click="loadStockDetail(stockDetail.medicineId, stockDetail.receipts[stockDetail.receipts.length - 1]!.id)"
            >
              Tải thêm phiếu cũ hơn
            </button>
          </div>
        </div>

        <div v-else class="empty-stock-placeholder">
          <div class="placeholder-icon" aria-hidden="true">💊</div>
          <h3>Chọn một loại thuốc ở bảng bên trái</h3>
          <p>Nhấn vào dòng thuốc hoặc nút <strong>Kho</strong> để xem tồn kho thực tế, lịch sử nhập và lập phiếu nhập mới.</p>
        </div>
      </aside>
    </div>

    <!-- Modal Thêm / Sửa thuốc -->
    <div v-if="editor" class="modal-overlay" @click.self="close">
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="med-modal-title">
        <div class="modal-header">
          <h2 id="med-modal-title">{{ editId === null ? 'Thêm thuốc mới vào danh mục' : 'Chỉnh sửa thông tin thuốc' }}</h2>
          <button class="close-btn" type="button" :disabled="saving" aria-label="Đóng" @click="close">✕</button>
        </div>

        <form class="modal-form" @submit.prevent="save">
          <div class="form-grid">
            <div class="form-field">
              <label>Mã thuốc nội bộ <b>*</b></label>
              <input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" placeholder="VD: TH-AMOX-500" :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Tên thương mại thuốc <b>*</b></label>
              <input v-model="form.name" required maxlength="200" placeholder="VD: Amoxicillin 500mg..." :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Hoạt chất chính</label>
              <input v-model="form.activeIngredient" maxlength="200" placeholder="VD: Amoxicillin trihydrate..." :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Hàm lượng / Nồng độ</label>
              <input v-model="form.strength" maxlength="50" placeholder="VD: 500mg, 100ml..." :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Đơn vị tính <b>*</b></label>
              <select v-model="form.unit" :disabled="saving">
                <option v-for="unit in units" :key="unit" :value="unit">{{ unit }}</option>
              </select>
            </div>

            <div class="form-field">
              <label>Đơn giá bán niêm yết (VNĐ) <b>*</b></label>
              <input v-model="form.unitPrice" inputmode="numeric" pattern="[0-9]+" maxlength="18" required placeholder="Nhập số tiền..." :disabled="saving" />
            </div>

            <div class="form-field form-field--wide">
              <label>Hướng dẫn sử dụng mặc định</label>
              <textarea v-model="form.defaultUsageInstruction" rows="2" maxlength="500" placeholder="VD: Uống sau ăn, ngày 2 lần..." :disabled="saving"></textarea>
            </div>

            <div class="form-field form-field--wide">
              <div class="confirm-box">
                <label class="checkbox-label mb-2">
                  <input v-model="form.isActive" type="checkbox" :disabled="saving" />
                  <span>Kích hoạt dùng thuốc này khi kê đơn (yêu cầu giá &gt; 0)</span>
                </label>
                <label class="checkbox-label text-warning">
                  <input v-model="confirmed" type="checkbox" :disabled="saving" />
                  <span>Tôi đã kiểm tra thông tin, đơn vị và đơn giá bán thuốc.</span>
                </label>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button v-if="editId !== null && originalActive" class="button button-danger mr-auto" type="button" :disabled="saving || loading || needsReview || !confirmed" @click="deactivate">
              Ngừng dùng thuốc
            </button>
            <button class="button button-secondary" type="button" :disabled="saving" @click="close">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving || needsReview || !confirmed">
              {{ saving ? 'Đang lưu…' : 'Lưu thuốc' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </section>
  <div v-else class="alert alert-error" role="alert">Chỉ Admin mới có quyền quản lý thuốc và kho dược.</div>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useMedicineStockStore } from '@/stores/medicineStock';
import { medicinesService, type Medicine } from '@/services/medicines';
import { medicineStockService, type MedicineStockDetail } from '@/services/medicineStock';

const auth = useAuthStore();
const store = useMedicineStockStore();

const items = ref<Medicine[]>([]), keyword = ref(''), includeInactive = ref(true), page = ref(1), hasNext = ref(false), total = ref(0);
const loading = ref(false), saving = ref(false), error = ref(''), message = ref('');
const editor = ref(false), editId = ref<number | null>(null), needsReview = ref(false), confirmed = ref(false), originalActive = ref(false);
const empty = () => ({ code: '', name: '', activeIngredient: '', strength: '', unit: 'Viên', unitPrice: '0', defaultUsageInstruction: '', isActive: false, expectedVersion: 0 });
const form = reactive(empty());
const units = ['Viên', 'Vỉ', 'Gói', 'Chai', 'Ống', 'Tuýp', 'Hộp'];

const stockDetail = ref<MedicineStockDetail | null>(null), stockBusy = ref(false), stockTab = ref<'receive' | 'history'>('receive');
const quantity = ref(''), supplier = ref(''), notes = ref(''), stockConfirmed = ref(false), heading = ref<HTMLElement | null>(null);

let generation = 0, stockGeneration = 0, disposed = false;
const actorKey = () => `${auth.user?.userId}/${auth.role}`;
const stockAlive = (n: number, actor: string) => !disposed && n === stockGeneration && actor === actorKey() && auth.role === 'ADMIN';

function close() {
  if (saving.value) return;
  editor.value = false; confirmed.value = false; editId.value = null;
  Object.assign(form, empty());
}

function clearStockForm() {
  quantity.value = ''; supplier.value = ''; notes.value = ''; stockConfirmed.value = false;
}

async function load(target = 1) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || keyword.value.length > 200 || !Number.isInteger(target) || target <= 0 || target > 1_000_000) return;
  const ticket = ++generation, actor = actorKey(), term = keyword.value, inactive = includeInactive.value;
  loading.value = true; error.value = ''; close(); items.value = []; hasNext.value = false; total.value = 0;
  try {
    const data = await medicinesService.list(term.trim(), target, inactive);
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN' || term !== keyword.value || inactive !== includeInactive.value) return;
    if (!Array.isArray(data.items) || data.page !== target || !Number.isInteger(data.totalCount)) throw new Error('Invalid catalog');
    items.value = data.items; page.value = data.page; total.value = data.totalCount; hasNext.value = data.hasNextPage; needsReview.value = false;
  } catch {
    if (!disposed && ticket === generation && actor === actorKey()) {
      error.value = 'Không tải được danh mục thuốc. Tải lại để đối chiếu.';
      needsReview.value = true;
    }
  } finally {
    if (!disposed && ticket === generation) loading.value = false;
  }
}

function open(item?: Medicine) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || loading.value || needsReview.value || store.pending) return;
  const stored = item ? items.value.find(i => i.id === item.id) : undefined;
  if (item && !stored) return;
  editId.value = stored?.id ?? null; originalActive.value = stored?.isActive ?? false;
  Object.assign(form, stored
    ? {
        code: stored.code, name: stored.name, activeIngredient: stored.activeIngredient ?? '',
        strength: stored.strength ?? '', unit: stored.unit, unitPrice: stored.unitPrice,
        defaultUsageInstruction: stored.defaultUsageInstruction ?? '', isActive: stored.isActive, expectedVersion: stored.version
      }
    : empty());
  editor.value = true; confirmed.value = false; error.value = ''; message.value = '';
}

function valid() {
  if (!/^[A-Za-z0-9._-]{1,50}$/.test(form.code.trim()) || !form.name.trim() || form.name.length > 200 || form.activeIngredient.length > 200 || form.strength.length > 50 || form.defaultUsageInstruction.length > 500 || !units.includes(form.unit) || !/^(0|[1-9][0-9]{0,17})$/.test(form.unitPrice) || (form.isActive && BigInt(form.unitPrice) <= 0n)) {
    error.value = 'Kiểm tra mã, tên, đơn vị và giá nguyên đồng; đặt giá lớn hơn 0 trước khi kích hoạt.';
    return false;
  }
  return true;
}

async function write(operation: () => Promise<Medicine>, deactivating = false) {
  const ticket = generation, actor = actorKey(), id = editId.value, expected = form.expectedVersion;
  saving.value = true; error.value = '';
  try {
    const data = await operation();
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN') return;
    if (!Number.isInteger(data.id) || data.id <= 0 || (id !== null && data.id !== id) || data.version !== (id === null ? 1 : expected + 1) || (deactivating && data.isActive)) {
      throw new Error('Invalid write response');
    }
    message.value = deactivating ? 'Đã ngừng dùng thuốc; lịch sử được giữ nguyên.' : 'Đã lưu danh mục thuốc.';
    saving.value = false; close();
    await load(page.value);
    if (stockDetail.value?.medicineId === data.id) {
      await loadStockDetail(data.id);
    }
  } catch {
    if (!disposed && ticket === generation && actor === actorKey()) {
      error.value = 'Chưa xác minh kết quả lưu. Tải lại để đối chiếu trước khi gửi tiếp.';
      needsReview.value = true; confirmed.value = false;
    }
  } finally {
    if (!disposed && ticket === generation) saving.value = false;
  }
}

async function save() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || saving.value || loading.value || needsReview.value || !confirmed.value || !valid()) return;
  const payload = {
    ...form,
    code: form.code.trim(),
    name: form.name.trim(),
    activeIngredient: form.activeIngredient.trim() || null,
    strength: form.strength.trim() || null,
    defaultUsageInstruction: form.defaultUsageInstruction.trim() || null
  };
  await write(() => medicinesService.save(editId.value, payload));
}

async function deactivate() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || editId.value === null || !originalActive.value || saving.value || loading.value || needsReview.value || !confirmed.value) return;
  const id = editId.value, version = form.expectedVersion;
  await write(() => medicinesService.deactivate(id, version), true);
}

async function selectStock(id: number) {
  if (stockBusy.value || store.pending || !items.value.some(m => m.id === id)) return;
  clearStockForm(); stockTab.value = 'receive'; message.value = '';
  await loadStockDetail(id);
  await nextTick();
  heading.value?.focus();
}

async function loadStockDetail(id: number, beforeId = 0) {
  if (disposed || auth.role !== 'ADMIN' || stockBusy.value || !Number.isInteger(id) || id <= 0 || !Number.isInteger(beforeId) || beforeId < 0 || (store.pending && store.pending.medicineId !== id)) return;
  const n = ++stockGeneration, actor = actorKey();
  stockBusy.value = true; stockDetail.value = null; error.value = ''; stockConfirmed.value = false;
  try {
    const result = await medicineStockService.read(id, beforeId);
    if (!stockAlive(n, actor)) return;
    if (!result || result.medicineId !== id || !Number.isInteger(result.onHand) || result.onHand < 0 || result.onHand > 2147483647 || !Number.isInteger(result.medicineVersion) || result.medicineVersion <= 0 || result.medicineVersion > 2147483647 || typeof result.isActive !== 'boolean' || !Array.isArray(result.receipts) || result.receipts.length > 20) {
      throw new Error('Invalid stock detail');
    }
    stockDetail.value = result;
  } catch {
    if (stockAlive(n, actor)) error.value = 'Chưa tải được tồn thuốc. Tải lại trước khi lập phiếu mới.';
  } finally {
    if (stockAlive(n, actor)) stockBusy.value = false;
  }
}

async function receive() {
  const current = stockDetail.value;
  if (disposed || auth.role !== 'ADMIN' || stockBusy.value || store.pending || !current?.isActive || !stockConfirmed.value) return;
  const qty = Number(quantity.value);
  if (!/^[0-9]{1,10}$/.test(quantity.value) || !Number.isInteger(qty) || qty <= 0 || qty > 2147483647 || !supplier.value.trim() || supplier.value.length > 200 || /[\x00-\x1f\x7f]/.test(supplier.value) || notes.value.length > 500) {
    error.value = 'Nhập số nguyên dương, nhà cung cấp tối đa 200 và ghi chú tối đa 500 ký tự.';
    return;
  }
  store.start(actorKey(), current.medicineId, {
    requestId: crypto.randomUUID(),
    quantity: qty,
    expectedMedicineVersion: current.medicineVersion,
    supplierName: supplier.value.trim(),
    notes: notes.value.trim() || null,
    confirmed: true
  });
  await retry();
}

async function retry() {
  const pending = store.pending;
  if (disposed || auth.role !== 'ADMIN' || stockBusy.value || !pending || pending.actor !== actorKey()) return;
  const n = ++stockGeneration, actor = actorKey();
  stockBusy.value = true; error.value = ''; message.value = '';
  try {
    const result = await medicineStockService.receive(pending.medicineId, pending.request);
    if (!stockAlive(n, actor)) return;
    if (!result?.receipt || !Number.isInteger(result.receipt.id) || result.receipt.id <= 0 || result.receipt.medicineId !== pending.medicineId || result.receipt.quantity !== pending.request.quantity || typeof result.replayed !== 'boolean') {
      throw new Error('Unverified receipt');
    }
    store.reset(); clearStockForm();
    message.value = result.replayed ? 'Đã xác minh phiếu cũ; tồn không cộng thêm.' : 'Đã nhập thuốc và ghi lịch sử.';
    stockBusy.value = false;
    await loadStockDetail(pending.medicineId);
  } catch (err: unknown) {
    if (!stockAlive(n, actor)) return;
    const response = err as { response?: { status?: number; data?: { code?: string; message?: string } } };
    const rejected = response.response?.status === 400 || ['MED_STOCK_STALE', 'MED_STOCK_LIMIT', 'GEN_001', 'GEN_002'].includes(response.response?.data?.code ?? '');
    if (rejected) { store.reset(); stockDetail.value = null; stockConfirmed.value = false; }
    error.value = response.response?.data?.message || 'Chưa xác minh được kết quả nhập. Giữ mã hiện tại và gửi lại phiếu đang chờ để đối chiếu.';
  } finally {
    if (stockAlive(n, actor)) stockBusy.value = false;
  }
}

function money(value: string) {
  try { return BigInt(value).toLocaleString('vi-VN') + ' ₫'; }
  catch { return 'Giá không hợp lệ'; }
}

function date(value: string) {
  return new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
}

watch(actorKey, () => {
  generation++; stockGeneration++; store.reset();
  items.value = []; total.value = 0; hasNext.value = false;
  stockDetail.value = null; clearStockForm();
  saving.value = false; loading.value = false; stockBusy.value = false;
  needsReview.value = true; message.value = ''; error.value = '';
  close();
});
watch(() => JSON.stringify(form), () => { confirmed.value = false; });

onMounted(() => {
  if (store.pending && store.pending.actor !== actorKey()) store.reset();
  void load();
  if (store.pending) void loadStockDetail(store.pending.medicineId);
});

onUnmounted(() => {
  disposed = true; generation++; stockGeneration++;
  items.value = []; stockDetail.value = null;
  Object.assign(form, empty()); clearStockForm();
  message.value = ''; error.value = '';
});
</script>

<style scoped>
.page-container {
  max-width: 1480px;
  margin: 0 auto;
  padding: 0.9rem 1.25rem 1.5rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

h1 { font-size: 1.38rem; font-weight: 800; color: #0f2d59; margin: 0; line-height: 1.2; }
.subtitle { margin: 0.2rem 0 0; color: #64748b; font-size: 0.84rem; }
.header-actions { display: flex; align-items: center; gap: 0.55rem; }
.count-pill { background: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd; font-size: 0.78rem; font-weight: 700; padding: 0.25rem 0.65rem; border-radius: 999px; }

.unified-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(350px, 440px);
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

.catalog-col { overflow: hidden; display: flex; flex-direction: column; }
.stock-col { position: sticky; top: 0.85rem; padding: 1rem; border-color: #cbd5e1; }

.catalog-toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.55rem;
  padding: 0.75rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}

.toolbar-search { flex: 1 1 210px; }
.toolbar-search input,
.form-field input,
.form-field select,
.form-field textarea {
  box-sizing: border-box;
  width: 100%;
  height: 36px;
  padding: 0 0.65rem;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  background: #ffffff;
  color: #1e293b;
  font: inherit;
  font-size: 0.85rem;
}
.form-field textarea { height: auto; padding: 0.5rem 0.65rem; }

.table-wrap { overflow: auto; max-height: calc(100vh - 255px); }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.84rem; text-align: left; }
.data-table th, .data-table td { padding: 0.5rem 0.65rem; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
.data-table thead th {
  position: sticky; top: 0; z-index: 1; background: #f8fafc; color: #475569;
  font-size: 0.73rem; font-weight: 750; text-transform: uppercase; border-bottom: 1px solid #e2e8f0; white-space: nowrap;
}
.catalog-row { cursor: pointer; }
.catalog-row:hover { background: #f8fafc; }
.catalog-row--selected { background: #f0f9ff !important; }

.med-top-cell { display: flex; align-items: center; gap: 0.35rem; margin-bottom: 0.15rem; }
.code-badge { font-family: monospace; font-size: 0.74rem; font-weight: 750; color: #0369a1; background: #e0f2fe; padding: 0.1rem 0.4rem; border-radius: 4px; }
.strength-pill { font-size: 0.7rem; font-weight: 650; background: #f1f5f9; color: #475569; padding: 0.08rem 0.38rem; border-radius: 4px; }
.med-name { font-weight: 700; color: #0f2d59; }
.med-usage-hint { font-size: 0.73rem; color: #64748b; max-width: 230px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.active-sub { color: #334155; font-size: 0.81rem; }
.unit-badge { font-size: 0.75rem; font-weight: 650; color: #475569; background: #f8fafc; border: 1px solid #e2e8f0; padding: 0.1rem 0.4rem; border-radius: 4px; }
.price-val { font-family: monospace; font-size: 0.86rem; font-weight: 750; color: #0f2d59; white-space: nowrap; }
.price-unset { font-size: 0.75rem; color: #dc2626; background: #fef2f2; padding: 0.1rem 0.4rem; border-radius: 4px; white-space: nowrap; }
.status-pill { display: inline-block; font-size: 0.7rem; font-weight: 750; padding: 0.1rem 0.45rem; border-radius: 999px; white-space: nowrap; }
.status-pill--active { background: #ecfdf5; color: #059669; }
.status-pill--inactive { background: #fef2f2; color: #dc2626; }
.text-right { text-align: right; }
.text-center { text-align: center; }
.actions-cell { white-space: nowrap; display: flex; justify-content: flex-end; gap: 0.3rem; }

.pagination { display: flex; align-items: center; justify-content: space-between; padding: 0.6rem 0.85rem; border-top: 1px solid #e2e8f0; background: #f8fafc; }
.page-info { font-size: 0.8rem; color: #64748b; font-weight: 600; }
.page-btns { display: flex; gap: 0.35rem; }

.stock-container { display: flex; flex-direction: column; gap: 0.85rem; }
.stock-hero { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; padding-bottom: 0.75rem; border-bottom: 1px solid #f1f5f9; }
.stock-identity h2 { margin: 0.25rem 0 0.2rem; font-size: 1.12rem; color: #0f2d59; }
.stock-meta-row { display: flex; align-items: center; gap: 0.75rem; font-size: 0.8rem; }
.text-success { color: #059669; font-weight: 700; }
.text-danger { color: #dc2626; font-weight: 700; }

.stock-counter-card { background: #f0f9ff; border: 1px solid #bae6fd; border-radius: 10px; padding: 0.5rem 0.9rem; text-align: center; min-width: 110px; }
.stock-label { display: block; font-size: 0.65rem; font-weight: 800; color: #0369a1; letter-spacing: 0.04em; }
.stock-number { display: block; font-size: 1.5rem; font-weight: 900; color: #0f2d59; font-family: monospace; line-height: 1.15; }
.stock-unit { font-size: 0.74rem; color: #0284c7; font-weight: 700; }

.tab-bar { display: flex; gap: 0.4rem; border-bottom: 1px solid #e2e8f0; padding-bottom: 0.55rem; }
.tab-btn { padding: 0.36rem 0.75rem; border-radius: 7px; border: 1px solid transparent; background: #f8fafc; color: #475569; font-size: 0.82rem; font-weight: 650; cursor: pointer; }
.tab-btn.active { background: #e0f2fe; border-color: #bae6fd; color: #0369a1; }

.receipt-form { display: flex; flex-direction: column; gap: 0.75rem; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.7rem; }
.form-field { display: grid; gap: 0.22rem; font-size: 0.8rem; font-weight: 650; color: #334155; }
.form-field b { color: #dc2626; }
.form-field--wide { grid-column: 1 / -1; }
.confirm-box { background: #f8fafc; border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.6rem 0.75rem; }
.checkbox-label { display: inline-flex; align-items: center; gap: 0.45rem; font-size: 0.81rem; font-weight: 600; color: #334155; cursor: pointer; }
.text-warning { color: #92400e; }
.mb-2 { margin-bottom: 0.45rem; }
.stock-policy-note { margin: 0; font-size: 0.75rem; color: #64748b; }

.receipt-history { display: flex; flex-direction: column; gap: 0.65rem; }
.history-list { display: flex; flex-direction: column; gap: 0.5rem; max-height: 360px; overflow-y: auto; }
.history-item { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.6rem 0.75rem; font-size: 0.82rem; }
.history-header { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; margin-bottom: 0.2rem; }
.receipt-code { font-family: monospace; font-weight: 750; color: #0369a1; margin-right: 0.4rem; }
.history-supplier { font-weight: 700; color: #334155; }
.receipt-qty { font-family: monospace; font-weight: 800; color: #059669; background: #ecfdf5; padding: 0.1rem 0.45rem; border-radius: 4px; }
.history-meta { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 0.4rem; font-size: 0.75rem; color: #64748b; }
.history-notes { margin-top: 0.25rem; font-size: 0.76rem; color: #475569; font-style: italic; }

.empty-stock-placeholder { padding: 2.5rem 1rem; text-align: center; color: #64748b; }
.placeholder-icon { font-size: 2.4rem; margin-bottom: 0.5rem; }
.empty-stock-placeholder h3 { margin: 0 0 0.35rem; font-size: 1.05rem; color: #0f2d59; }
.empty-stock-placeholder p { margin: 0; font-size: 0.85rem; }
.empty-state { text-align: center; padding: 1.75rem 1rem !important; color: #64748b; font-size: 0.88rem; }

.button {
  display: inline-flex; align-items: center; justify-content: center; gap: 0.35rem;
  height: 36px; padding: 0 0.9rem; border: 1px solid transparent; border-radius: 7px;
  font: inherit; font-size: 0.84rem; font-weight: 700; cursor: pointer;
}
.button-sm { height: 32px; padding: 0 0.75rem; font-size: 0.8rem; }
.button-primary { background: #0284c7; color: #ffffff; }
.button-primary:hover:not(:disabled) { background: #0369a1; }
.button-secondary { background: #ffffff; color: #334155; border-color: #cbd5e1; }
.button-danger { background: #fee2e2; color: #b91c1c; border-color: #fca5a5; }
.button-quiet { background: #ffffff; border-color: #cbd5e1; color: #475569; height: 28px; padding: 0 0.55rem; font-size: 0.76rem; }
.button-stock { background: #f0f9ff; border-color: #bae6fd; color: #0369a1; height: 28px; padding: 0 0.55rem; font-size: 0.76rem; }
.button:disabled { cursor: not-allowed; opacity: 0.55; }
.w-full { width: 100%; }
.mr-auto { margin-right: auto; }
.link-btn { background: none; border: none; color: #0284c7; text-decoration: underline; cursor: pointer; padding: 0; font: inherit; font-weight: 700; font-size: inherit; }

.alert { padding: 0.65rem 0.9rem; border-radius: 8px; font-size: 0.84rem; font-weight: 600; }
.alert-row { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-warning { background: #fffbeb; color: #92400e; border: 1px solid #fef3c7; }

.modal-overlay {
  position: fixed; inset: 0; background: rgba(15, 23, 42, 0.45); backdrop-filter: blur(2px);
  display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 1rem;
}
.modal-card { background: #ffffff; border-radius: 12px; max-width: 620px; width: 100%; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.14); overflow: hidden; border: 1px solid #e2e8f0; }
.modal-header { display: flex; justify-content: space-between; align-items: center; padding: 0.95rem 1.15rem; background: #f8fafc; border-bottom: 1px solid #e2e8f0; }
.modal-header h2 { font-size: 1.1rem; font-weight: 800; color: #0f2d59; margin: 0; }
.close-btn { background: none; border: none; font-size: 1.15rem; color: #64748b; cursor: pointer; }
.modal-form { padding: 1.15rem; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 1rem; padding-top: 0.85rem; border-top: 1px solid #f1f5f9; }

@media (max-width: 1080px) {
  .unified-grid { grid-template-columns: 1fr; }
  .stock-col { position: static; }
}
@media (max-width: 600px) {
  .form-grid { grid-template-columns: 1fr; }
}
</style>
