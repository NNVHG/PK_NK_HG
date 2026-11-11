<template>
  <section v-if="auth.role === 'ADMIN'" class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">DANH MỤC VẬT TƯ</p>
        <h1>Danh mục vật tư tiêu hao</h1>
        <p class="subtitle">Quản lý mã vật tư, đơn vị tính và thiết lập ngưỡng tồn an toàn trong điều trị.</p>
      </div>
      <div class="header-actions">
        <button class="button button-primary" :disabled="saving || loading || needsReview" @click="open()">
          <span class="btn-icon">+</span> Thêm vật tư mới
        </button>
      </div>
    </div>

    <div class="info-banner">
      <div class="banner-icon">📦</div>
      <div class="banner-content">
        <strong>Lưu ý quản lý vật tư:</strong> Ngưỡng an toàn dùng để cảnh báo khi kho còn ít hơn mức quy định. Ngừng dùng sẽ ẩn vật tư khỏi danh sách tiêu hao ca khám nhưng vẫn bảo toàn chứng từ lịch sử.
      </div>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">
      Dữ liệu chưa đối soát. Bấm <button class="link-btn" @click="load(page)">Tải lại danh mục</button> trước khi thao tác tiếp.
    </div>

    <div class="panel filters-panel">
      <form class="filters-grid" @submit.prevent="load(1)">
        <div class="filter-field">
          <label for="supply-search">Tìm kiếm vật tư</label>
          <div class="input-icon-wrapper">
            <span class="field-icon">🔍</span>
            <input id="supply-search" v-model="keyword" placeholder="Nhập mã, tên hoặc đơn vị tính..." maxlength="200" :disabled="saving" />
          </div>
        </div>

        <div class="filter-field filter-checkbox">
          <label class="checkbox-label">
            <input v-model="includeInactive" type="checkbox" :disabled="saving" />
            <span>Bao gồm vật tư đã ngừng dùng</span>
          </label>
        </div>

        <div class="filter-actions">
          <button class="button button-secondary" type="submit" :disabled="saving || loading">
            {{ loading ? 'Đang tải...' : 'Tìm kiếm' }}
          </button>
        </div>
      </form>
    </div>

    <div class="panel table-panel">
      <div class="panel-header">
        <h2 class="panel-title">Danh mục vật tư tiêu hao phòng khám</h2>
        <span class="count-badge">{{ total }} vật tư</span>
      </div>

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width: 140px;">Mã vật tư</th>
              <th>Tên vật tư y tế</th>
              <th style="width: 110px;">Đơn vị tính</th>
              <th style="width: 140px; text-align: center;">Ngưỡng an toàn</th>
              <th>Mô tả / Quy cách</th>
              <th style="width: 120px; text-align: center;">Trạng thái</th>
              <th style="width: 100px; text-align: right;">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.id">
              <td><span class="code-badge">{{ item.code }}</span></td>
              <td><span class="supply-name">{{ item.name }}</span></td>
              <td><span class="unit-badge">{{ item.unit }}</span></td>
              <td style="text-align: center;">
                <span class="threshold-badge">{{ item.minimumStockThreshold }} {{ item.unit }}</span>
              </td>
              <td class="desc-cell">{{ item.description || '—' }}</td>
              <td style="text-align: center;">
                <span class="status-pill" :class="item.isActive ? 'status-pill--active' : 'status-pill--inactive'">
                  {{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                </span>
              </td>
              <td style="text-align: right;">
                <button class="button button-quiet" :disabled="saving || loading || needsReview" @click="open(item)">
                  Sửa
                </button>
              </td>
            </tr>
            <tr v-if="!loading && !items.length">
              <td colspan="7" class="empty-state">Không có vật tư nào phù hợp.</td>
            </tr>
            <tr v-if="loading">
              <td colspan="7" class="loading-state">Đang tải danh mục vật tư...</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="pagination">
        <span class="page-info">Trang {{ page }} (Tổng {{ total }} vật tư)</span>
        <div class="page-btns">
          <button class="button button-secondary" :disabled="loading || saving || page <= 1" @click="load(page - 1)">
            ← Trang trước
          </button>
          <button class="button button-secondary" :disabled="loading || saving || !hasNext" @click="load(page + 1)">
            Trang sau →
          </button>
        </div>
      </div>
    </div>
    <div v-if="editor" class="modal-overlay" @click.self="close">
      <div class="modal-card">
        <div class="modal-header">
          <h2>{{ editId === null ? 'Thêm vật tư tiêu hao mới' : 'Chỉnh sửa vật tư tiêu hao' }}</h2>
          <button class="close-btn" type="button" :disabled="saving" @click="close">✕</button>
        </div>

        <form class="modal-form" @submit.prevent="save">
          <div class="form-grid">
            <div class="form-field">
              <label>Mã vật tư <b>*</b></label>
              <input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" placeholder="VD: VT-GANGTAY-M" :disabled="saving" />
              <span class="form-hint">Mã viết liền không dấu, số, gạch nối.</span>
            </div>

            <div class="form-field">
              <label>Đơn vị tính <b>*</b></label>
              <input v-model="form.unit" required maxlength="50" list="supply-units" placeholder="Chọn hoặc nhập ĐVT..." :disabled="saving" />
              <datalist id="supply-units"><option v-for="unit in units" :key="unit" :value="unit" /></datalist>
            </div>

            <div class="form-field form-field--wide">
              <label>Tên vật tư tiêu hao <b>*</b></label>
              <input v-model="form.name" required maxlength="200" placeholder="VD: Găng tay cao su y tế không bột (Size M)..." :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Ngưỡng tồn cảnh báo tối thiểu <b>*</b></label>
              <input v-model.number="form.minimumStockThreshold" type="number" min="0" max="1000000" step="1" required placeholder="0" :disabled="saving" />
              <span class="form-hint">Hệ thống báo vàng khi tồn kho &le; ngưỡng này.</span>
            </div>

            <div class="form-field">
              <label>Quy cách đóng gói / Mô tả</label>
              <input v-model="form.description" maxlength="500" placeholder="VD: Hộp 50 đôi, Tiệt trùng..." :disabled="saving" />
            </div>

            <div class="form-field form-field--wide">
              <div class="confirm-box">
                <label class="checkbox-label">
                  <input v-model="form.isActive" type="checkbox" :disabled="saving" />
                  <span>Kích hoạt sử dụng vật tư này trong điều trị</span>
                </label>
                <p class="box-hint">Ngừng dùng sẽ ẩn vật tư khỏi gợi ý ca khám nhưng vẫn bảo tồn lịch sử.</p>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button v-if="editId !== null && originalActive" class="button button-danger mr-auto" type="button" :disabled="saving || loading || needsReview" @click="deactivate">
              Ngừng dùng vật tư
            </button>
            <button class="button button-secondary" type="button" :disabled="saving" @click="close">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving || needsReview">
              {{ saving ? 'Đang lưu...' : 'Lưu vật tư' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </section>
  <div v-else class="alert alert-error" role="alert">Chỉ Admin mới có quyền quản lý danh mục vật tư tiêu hao.</div>
</template>
<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, watch } from 'vue';
import { medicalSuppliesService, type MedicalSupply } from '@/services/medicalSupplies';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const items = ref<MedicalSupply[]>([]), keyword = ref(''), includeInactive = ref(false), page = ref(1), hasNext = ref(false), total = ref(0);
const loading = ref(false), saving = ref(false), error = ref(''), message = ref(''), editor = ref(false), editId = ref<number | null>(null), needsReview = ref(false), originalActive = ref(false);
const empty = () => ({ code: '', name: '', unit: 'Cái', minimumStockThreshold: 0, description: '', isActive: true, expectedVersion: 0 });
const form = reactive(empty());
const units = ['Cái', 'Hộp', 'Gói', 'Chai', 'Cuộn', 'Ống', 'Tuýp', 'Bộ'];
let generation = 0, disposed = false;
const actorKey = () => `${auth.user?.userId}/${auth.role}`;
function close() { if (saving.value) return; editor.value = false; editId.value = null; Object.assign(form, empty()); }
async function load(target = 1) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || keyword.value.length > 200 || !Number.isInteger(target) || target <= 0 || target > 1000000) return;
  const ticket = ++generation, actor = actorKey(), term = keyword.value, inactive = includeInactive.value;
  loading.value = true; error.value = ''; close(); items.value = []; hasNext.value = false; total.value = 0;
  try {
    const data = await medicalSuppliesService.list(term.trim(), target, inactive);
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN' || term !== keyword.value || inactive !== includeInactive.value) return;
    if (!Array.isArray(data.items) || data.page !== target || !Number.isInteger(data.totalCount)) throw new Error('Invalid catalog');
    items.value = data.items; page.value = data.page; total.value = data.totalCount; hasNext.value = data.hasNextPage; needsReview.value = false;
  } catch { if (!disposed && ticket === generation && actor === actorKey()) { error.value = 'Không tải được danh mục vật tư. Tải lại để đối chiếu.'; needsReview.value = true; } }
  finally { if (!disposed && ticket === generation) loading.value = false; }
}
function open(item?: MedicalSupply) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || loading.value || needsReview.value) return;
  const stored = item ? items.value.find(i => i.id === item.id) : undefined;
  if (item && !stored) return;
  editId.value = stored?.id ?? null; originalActive.value = stored?.isActive ?? false;
  Object.assign(form, stored ? { code: stored.code, name: stored.name, unit: stored.unit, minimumStockThreshold: stored.minimumStockThreshold, description: stored.description ?? '', isActive: stored.isActive, expectedVersion: stored.version } : empty());
  editor.value = true; error.value = ''; message.value = '';
}
function valid() {
  if (!/^[A-Za-z0-9._-]{1,50}$/.test(form.code.trim()) || !form.name.trim() || form.name.length > 200 || !form.unit.trim() || form.unit.length > 50 || !Number.isInteger(form.minimumStockThreshold) || form.minimumStockThreshold < 0 || form.minimumStockThreshold > 1000000 || form.description.length > 500) {
    error.value = 'Kiểm tra mã, tên, đơn vị tính, ngưỡng tối thiểu (0–1.000.000) và độ dài mô tả.'; return false;
  }
  return true;
}
async function write(operation: () => Promise<MedicalSupply>, deactivating = false) {
  const ticket = generation, actor = actorKey(), id = editId.value, expected = form.expectedVersion;
  saving.value = true; error.value = '';
  try {
    const data = await operation();
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN') return;
    if (!Number.isInteger(data.id) || data.id <= 0 || id !== null && data.id !== id || data.version !== (id === null ? 1 : expected + 1) || deactivating && data.isActive) throw new Error('Invalid write response');
    message.value = deactivating ? 'Đã ngừng dùng vật tư; danh mục vẫn được giữ nguyên.' : 'Đã lưu danh mục vật tư.';
    saving.value = false; close(); await load(page.value);
  } catch { if (!disposed && ticket === generation && actor === actorKey()) { error.value = 'Chưa xác minh kết quả lưu. Tải lại để đối chiếu trước khi gửi tiếp.'; needsReview.value = true; } }
  finally { if (!disposed && ticket === generation) saving.value = false; }
}
async function save() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || saving.value || loading.value || needsReview.value || !valid()) return;
  const payload = { ...form, code: form.code.trim(), name: form.name.trim(), unit: form.unit.trim(), description: form.description.trim() || null };
  await write(() => medicalSuppliesService.save(editId.value, payload));
}
async function deactivate() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || editId.value === null || !originalActive.value || saving.value || loading.value || needsReview.value) return;
  const id = editId.value, version = form.expectedVersion;
  await write(() => medicalSuppliesService.deactivate(id, version), true);
}
watch(actorKey, () => { generation++; items.value = []; total.value = 0; hasNext.value = false; saving.value = false; loading.value = false; needsReview.value = true; message.value = ''; error.value = ''; close(); });
onMounted(() => load());
onUnmounted(() => { disposed = true; generation++; items.value = []; Object.assign(form, empty()); message.value = ''; error.value = ''; });
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
  padding: 1rem 1.25rem;
}

.filters-grid {
  display: grid;
  grid-template-columns: minmax(280px, 1.8fr) auto auto;
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

.filter-checkbox {
  align-self: center;
  margin-top: 1rem;
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
.form-field input:focus,
.form-field select:focus,
.form-field textarea:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.55rem;
  font-size: 0.85rem;
  font-weight: 600;
  color: #334155;
  cursor: pointer;
}

/* Data Table */
.table-panel {
  overflow: hidden;
}

.table-wrap {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
  text-align: left;
}

.data-table th,
.data-table td {
  padding: 0.85rem 1.15rem;
  border-bottom: 1px solid #f1f5f9;
  vertical-align: middle;
}

.data-table thead th {
  background: #f8fafc;
  color: #475569;
  font-size: 0.775rem;
  font-weight: 750;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  border-bottom: 1px solid #e2e8f0;
}

.code-badge {
  font-family: monospace;
  font-size: 0.78rem;
  font-weight: 750;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.supply-name {
  font-weight: 700;
  color: #0f2d59;
}

.unit-badge {
  font-size: 0.78rem;
  font-weight: 650;
  color: #475569;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.threshold-badge {
  font-family: monospace;
  font-size: 0.85rem;
  font-weight: 750;
  color: #d97706;
  background: #fef3c7;
  padding: 0.15rem 0.55rem;
  border-radius: 6px;
}

.desc-cell {
  color: #64748b;
  font-size: 0.825rem;
  max-width: 300px;
}

.status-pill {
  display: inline-block;
  font-size: 0.72rem;
  font-weight: 750;
  padding: 0.15rem 0.5rem;
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

.empty-state, .loading-state {
  text-align: center;
  padding: 3rem 1rem !important;
  color: #64748b;
  font-size: 0.925rem;
}

/* Pagination */
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

.button-danger {
  background: #fee2e2;
  color: #b91c1c;
  border-color: #fca5a5;
}

.button-danger:hover:not(:disabled) {
  background: #fecaca;
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
  color: #0f2d59;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.btn-icon {
  font-size: 1.1rem;
  line-height: 1;
}

.link-btn {
  background: none;
  border: none;
  color: #0284c7;
  text-decoration: underline;
  cursor: pointer;
  padding: 0;
  font: inherit;
  font-weight: 700;
}

/* Alerts */
.alert {
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 600;
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

.alert-warning {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fef3c7;
}

/* Modal */
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
  max-width: 620px;
  width: 100%;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
  overflow: hidden;
  border: 1px solid #e2e8f0;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.15rem 1.25rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}

.modal-header h2 {
  font-size: 1.15rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
}

.close-btn {
  background: none;
  border: none;
  font-size: 1.2rem;
  color: #64748b;
  cursor: pointer;
  padding: 0.25rem;
}

.modal-form {
  padding: 1.25rem;
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

.form-field b {
  color: #dc2626;
}

.form-field--wide {
  grid-column: 1 / -1;
}

.form-hint {
  font-size: 0.775rem;
  color: #64748b;
}

.confirm-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.85rem 1rem;
}

.box-hint {
  margin: 0.35rem 0 0;
  font-size: 0.785rem;
  color: #64748b;
  line-height: 1.4;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1.25rem;
  padding-top: 1.15rem;
  border-top: 1px solid #f1f5f9;
}

.mr-auto {
  margin-right: auto;
}

@media (max-width: 768px) {
  .filters-grid {
    grid-template-columns: 1fr;
  }
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
