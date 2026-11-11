<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">CƠ SỞ VẬT CHẤT</p>
        <h1>Phòng khám & Ghế nha khoa</h1>
        <p class="subtitle">Khai báo danh mục phòng, phân bổ ghế nha khoa và quản lý trạng thái phục vụ.</p>
      </div>
      <div class="header-actions">
        <button class="button button-primary" :disabled="saving || loading || needsReview" @click="openCreate">
          <span class="btn-icon">+</span> Khai báo ghế mới
        </button>
      </div>
    </div>

    <div class="info-banner">
      <div class="banner-icon">🦷</div>
      <div class="banner-content">
        <strong>Lưu ý vận hành phòng khám:</strong> Ghế "Bảo trì" hoặc "Ngừng sử dụng" sẽ bị khóa khỏi luồng điều phối ca khám. Ghế "Đang phục vụ" do ca khám thực tế quản lý; chỉ chỉnh sửa cấu hình sau khi ca khám kết thúc.
      </div>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">
      Dữ liệu ghế vừa được cập nhật. Bấm <button class="link-btn" @click="load(1)">Tải lại danh sách</button> để đối chiếu trước khi thao tác tiếp.
    </div>

    <div class="panel filters-panel">
      <form class="filters-grid" @submit.prevent="load(1)">
        <div class="filter-field">
          <label for="chair-search">Tìm kiếm ghế / phòng</label>
          <div class="input-icon-wrapper">
            <span class="field-icon">🔍</span>
            <input id="chair-search" v-model="keyword" placeholder="Mã ghế hoặc tên phòng..." maxlength="100" :disabled="saving" />
          </div>
        </div>

        <div class="filter-field">
          <label for="chair-status">Trạng thái vận hành</label>
          <select id="chair-status" v-model.number="status" :disabled="saving">
            <option :value="0">Tất cả trạng thái</option>
            <option :value="1">Sẵn sàng phục vụ</option>
            <option :value="2">Đang có ca khám</option>
            <option :value="3">Bảo trì kỹ thuật</option>
            <option :value="4">Ngừng sử dụng</option>
          </select>
        </div>

        <div class="filter-field">
          <label for="chair-active">Hiệu lực danh mục</label>
          <select id="chair-active" v-model="active" :disabled="saving">
            <option value="all">Tất cả hiệu lực</option>
            <option value="true">Đang sử dụng</option>
            <option value="false">Ngừng sử dụng</option>
          </select>
        </div>

        <div class="filter-actions">
          <button class="button button-secondary" type="submit" :disabled="saving || loading">
            {{ loading ? 'Đang tải...' : 'Lọc danh sách' }}
          </button>
        </div>
      </form>
    </div>

    <div class="panel list-panel">
      <div class="panel-header">
        <h2 class="panel-title">Danh sách ghế nha khoa phòng khám</h2>
        <span class="count-badge">{{ store.result?.totalCount ?? 0 }} ghế</span>
      </div>

      <div class="panel-body">
        <div v-if="loading" class="loading-state">Đang tải danh mục ghế...</div>

        <div v-else-if="store.result?.items && store.result.items.length > 0" class="chairs-grid">
          <div
            v-for="chair in store.result.items"
            :key="chair.id"
            class="chair-card"
            :class="[
              chair.status === 1 ? 'chair-card--ready' :
              chair.status === 2 ? 'chair-card--busy' :
              chair.status === 3 ? 'chair-card--maintenance' : 'chair-card--disabled'
            ]"
          >
            <div class="chair-card-header">
              <div class="chair-badge-group">
                <span class="chair-number">{{ chair.chairNumber }}</span>
                <span class="room-pill">{{ chair.roomName }}</span>
              </div>
              <span
                class="status-pill"
                :class="[
                  chair.status === 1 ? 'status-pill--ready' :
                  chair.status === 2 ? 'status-pill--busy' :
                  chair.status === 3 ? 'status-pill--maint' : 'status-pill--disabled'
                ]"
              >
                {{ chair.statusText }}
              </span>
            </div>

            <div class="chair-card-body">
              <p class="chair-desc">{{ chair.description || 'Chưa có ghi chú trang thiết bị.' }}</p>
              <div class="chair-meta">
                <span>Danh mục: <b>{{ chair.isActive ? 'Kích hoạt' : 'Ngừng dùng' }}</b></span>
              </div>
            </div>

            <div class="chair-card-actions">
              <span v-if="chair.status === 2" class="busy-hint">
                🔒 Đang khám bệnh
              </span>
              <button
                v-else
                class="button button-quiet w-full"
                :disabled="saving || loading || needsReview"
                @click="openEdit(chair)"
              >
                Cập nhật cấu hình
              </button>
            </div>
          </div>
        </div>

        <div v-else class="empty-state">Không tìm thấy ghế nha khoa phù hợp.</div>

        <div v-if="store.result && store.result.totalCount > 0" class="pagination">
          <span class="page-info">Trang {{ store.result.page }} (Tổng {{ store.result.totalCount }} ghế)</span>
          <div class="page-btns">
            <button class="button button-secondary" :disabled="saving || loading || store.result.page <= 1" @click="load(store.result.page - 1)">
              ← Trang trước
            </button>
            <button class="button button-secondary" :disabled="saving || loading || !store.result.hasNextPage" @click="load(store.result.page + 1)">
              Trang sau →
            </button>
          </div>
        </div>
      </div>
    </div>
    <div v-if="formOpen" class="modal-overlay" @click.self="closeForm">
      <div class="modal-card">
        <div class="modal-header">
          <h2>{{ selected ? 'Cập nhật cấu hình ghế: ' + selected.chairNumber : 'Khai báo ghế nha khoa mới' }}</h2>
          <button class="close-btn" type="button" :disabled="saving" @click="closeForm">✕</button>
        </div>

        <form class="modal-form" @submit.prevent="save">
          <div class="form-grid">
            <div class="form-field">
              <label>Mã số ghế nha khoa <b>*</b></label>
              <input v-model="chairNumber" maxlength="50" required placeholder="VD: GHE-01" :disabled="saving || selected !== null" />
              <span v-if="selected" class="form-hint">Mã định danh ghế không thể sửa sau khi khai báo.</span>
            </div>

            <div class="form-field">
              <label>Tên phòng chức năng <b>*</b></label>
              <input v-model="roomName" maxlength="100" required placeholder="VD: Phòng điều trị 1..." :disabled="saving" />
            </div>

            <div class="form-field form-field--wide">
              <label>Trạng thái vận hành ghế <b>*</b></label>
              <select v-model.number="formStatus" :disabled="saving">
                <option :value="1">🟢 Sẵn sàng phục vụ bệnh nhân</option>
                <option :value="3">🟡 Đang bảo trì / Kiểm tra kỹ thuật</option>
                <option :value="4">🔴 Ngừng sử dụng (Khóa chọn)</option>
              </select>
              <p v-if="formStatus === 4" class="form-hint text-warning">
                Ghế ngừng sử dụng sẽ bị ẩn khỏi danh sách chỉ định ca khám nhưng vẫn bảo tồn lịch sử.
              </p>
            </div>

            <div class="form-field form-field--wide">
              <label>Ghi chú trang thiết bị / Mô tả</label>
              <textarea v-model="description" rows="2" maxlength="500" placeholder="VD: Ghế trang bị máy cạo vôi siêu âm..." :disabled="saving"></textarea>
            </div>

            <div class="form-field form-field--wide">
              <div class="confirm-box">
                <label class="checkbox-label text-warning">
                  <input v-model="confirmed" type="checkbox" :disabled="saving || needsReview" />
                  <span>Tôi đã kiểm tra đối chiếu đúng thông tin phòng và xác nhận trạng thái ghế.</span>
                </label>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button class="button button-secondary" type="button" :disabled="saving" @click="closeForm">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving || loading || needsReview || !confirmed">
              {{ saving ? 'Đang lưu...' : 'Xác nhận lưu' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </main>
</template>
<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useDentalChairsStore } from '@/stores/dentalChairs';
import { dentalChairsService, type DentalChair } from '@/services/dentalChairs';
const auth = useAuthStore(), store = useDentalChairsStore();
const keyword = ref(''), status = ref(0), active = ref('all'), loading = ref(false), saving = ref(false), error = ref(''), message = ref(''), needsReview = ref(false);
const formOpen = ref(false), selected = ref<DentalChair | null>(null), chairNumber = ref(''), roomName = ref(''), formStatus = ref(1), description = ref(''), confirmed = ref(false);
let version = 0, life = 0;
function clearForm() { formOpen.value = false; selected.value = null; chairNumber.value = ''; roomName.value = ''; formStatus.value = 1; description.value = ''; confirmed.value = false; }
function closeForm() { if (!saving.value) clearForm(); }
async function load(page = 1) {
  if (auth.role !== 'ADMIN' || saving.value || !Number.isInteger(page) || page < 1 || page > 1_000_000 || keyword.value.length > 100 || ![0, 1, 2, 3, 4].includes(status.value) || !['all', 'true', 'false'].includes(active.value)) return;
  const ticket = ++version; loading.value = true; error.value = ''; store.reset(); clearForm();
  try {
    const result = await dentalChairsService.getPage({ page, pageSize: 20, keyword: keyword.value.trim() || undefined,
      status: status.value || undefined, isActive: active.value === 'all' ? undefined : active.value === 'true' });
    if (ticket !== version || auth.role !== 'ADMIN') return;
    store.replace(result); needsReview.value = false;
  } catch { if (ticket === version) error.value = 'Không thể tải danh mục ghế. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function openCreate() {
  if (auth.role !== 'ADMIN' || loading.value || saving.value || needsReview.value) return;
  clearForm(); formOpen.value = true; error.value = ''; message.value = '';
}
function openEdit(chair: DentalChair) {
  if (auth.role !== 'ADMIN' || loading.value || saving.value || needsReview.value || !Number.isInteger(chair.id) || chair.id <= 0 || chair.status === 2 || !store.result?.items.some(c => c.id === chair.id && c.version === chair.version)) return;
  clearForm(); selected.value = { ...chair }; chairNumber.value = chair.chairNumber; roomName.value = chair.roomName; formStatus.value = chair.status; description.value = chair.description ?? ''; formOpen.value = true; error.value = ''; message.value = '';
}
async function save() {
  if (auth.role !== 'ADMIN' || !formOpen.value || saving.value || loading.value || needsReview.value || !confirmed.value) return;
  if (!chairNumber.value.trim() || chairNumber.value.length > 50 || !roomName.value.trim() || roomName.value.length > 100 || description.value.length > 500 || ![1, 3, 4].includes(formStatus.value)) { error.value = 'Nhập mã ghế tối đa 50, tên phòng tối đa 100, ghi chú tối đa 500 ký tự và trạng thái hợp lệ.'; return; }
  const old = selected.value;
  if (old && (old.status === 2 || !Number.isInteger(old.version) || old.version <= 0)) return;
  const ticket = life; saving.value = true; error.value = ''; message.value = '';
  const input = { roomName: roomName.value.trim(), status: formStatus.value, description: description.value.trim() || null, confirmed: true };
  try {
    const result = old ? await dentalChairsService.update(old.id, { ...input, expectedVersion: old.version })
      : await dentalChairsService.create({ ...input, chairNumber: chairNumber.value.trim() });
    if (ticket !== life || auth.role !== 'ADMIN') return;
    if (!result.id || result.status !== input.status || (old && result.id !== old.id)) throw new Error('Invalid chair response');
    message.value = 'Đã lưu danh mục ghế.'; clearForm(); needsReview.value = true; saving.value = false; await load(1);
  } catch (err: unknown) {
    if (ticket !== life || auth.role !== 'ADMIN') return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả lưu. Tải lại để đối chiếu.'; confirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) saving.value = false; }
}
onMounted(() => load());
onUnmounted(() => { life++; version++; clearForm(); store.reset(); error.value = ''; message.value = ''; });
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

.panel-body {
  padding: 1.25rem;
}

/* Filters */
.filters-panel {
  padding: 1rem 1.25rem;
}

.filters-grid {
  display: grid;
  grid-template-columns: minmax(240px, 1.5fr) 1fr 1fr auto;
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

/* Chairs Grid */
.chairs-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.chair-card {
  border-radius: 10px;
  border: 1px solid #e2e8f0;
  background: #ffffff;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  transition: all 0.15s ease;
}

.chair-card--ready {
  border-left: 4px solid #059669;
}

.chair-card--busy {
  border-left: 4px solid #0284c7;
  background: #f0f9ff;
}

.chair-card--maintenance {
  border-left: 4px solid #d97706;
  background: #fffbeb;
}

.chair-card--disabled {
  border-left: 4px solid #94a3b8;
  background: #f8fafc;
  opacity: 0.8;
}

.chair-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.chair-badge-group {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.chair-number {
  font-size: 1.15rem;
  font-weight: 850;
  color: #0f2d59;
}

.room-pill {
  font-size: 0.78rem;
  color: #475569;
  font-weight: 600;
}

.chair-desc {
  font-size: 0.825rem;
  color: #64748b;
  margin: 0;
  line-height: 1.4;
  min-height: 2.4rem;
}

.chair-meta {
  font-size: 0.75rem;
  color: #64748b;
  margin-top: 0.4rem;
}

.chair-card-actions {
  margin-top: auto;
  padding-top: 0.65rem;
  border-top: 1px solid #f1f5f9;
}

.busy-hint {
  display: block;
  text-align: center;
  font-size: 0.78rem;
  font-weight: 750;
  color: #0369a1;
  padding: 0.35rem 0;
}

/* Status Pills */
.status-pill {
  font-size: 0.72rem;
  font-weight: 750;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
}

.status-pill--ready { background: #ecfdf5; color: #059669; }
.status-pill--busy { background: #e0f2fe; color: #0284c7; }
.status-pill--maint { background: #fef3c7; color: #d97706; }
.status-pill--disabled { background: #f1f5f9; color: #64748b; }

/* Pagination */
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 1.25rem;
  padding-top: 1rem;
  border-top: 1px solid #e2e8f0;
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

.button-primary { background: #0284c7; color: #ffffff; }
.button-primary:hover:not(:disabled) { background: #0369a1; }
.button-secondary { background: #ffffff; color: #334155; border-color: #cbd5e1; }
.button-secondary:hover:not(:disabled) { background: #f1f5f9; border-color: #94a3b8; }
.button-quiet { background: #ffffff; border-color: #cbd5e1; color: #475569; height: 32px; padding: 0 0.85rem; font-size: 0.8rem; }
.button-quiet:hover:not(:disabled) { background: #f1f5f9; color: #0f2d59; }
.button:disabled { cursor: not-allowed; opacity: 0.55; }
.btn-icon { font-size: 1.1rem; line-height: 1; }
.w-full { width: 100%; }
.link-btn { background: none; border: none; color: #0284c7; text-decoration: underline; cursor: pointer; padding: 0; font: inherit; font-weight: 700; }

/* Alerts */
.alert { padding: 0.85rem 1.15rem; border-radius: 8px; font-size: 0.875rem; font-weight: 600; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-warning { background: #fffbeb; color: #92400e; border: 1px solid #fef3c7; }
.empty-state, .loading-state { text-align: center; padding: 3rem 1rem; color: #64748b; font-size: 0.925rem; }

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
  max-width: 600px;
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

.modal-header h2 { font-size: 1.15rem; font-weight: 800; color: #0f2d59; margin: 0; }
.close-btn { background: none; border: none; font-size: 1.2rem; color: #64748b; cursor: pointer; padding: 0.25rem; }
.modal-form { padding: 1.25rem; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
.form-field { display: grid; gap: 0.35rem; font-size: 0.825rem; font-weight: 600; color: #334155; }
.form-field b { color: #dc2626; }
.form-field--wide { grid-column: 1 / -1; }
.form-hint { font-size: 0.775rem; color: #64748b; }
.confirm-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.85rem 1rem; }
.checkbox-label { display: inline-flex; align-items: center; gap: 0.55rem; font-size: 0.825rem; font-weight: 600; cursor: pointer; }
.text-warning { color: #92400e; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.25rem; padding-top: 1.15rem; border-top: 1px solid #f1f5f9; }

@media (max-width: 768px) {
  .filters-grid { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
}
</style>
