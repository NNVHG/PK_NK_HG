<template>
  <section class="chair-status" aria-labelledby="chair-status-title">
    <div class="header-banner">
      <div class="header-icon">🏥</div>
      <div>
        <h1 id="chair-status-title">Trạng thái phòng điều trị</h1>
        <p class="subtitle">Quản lý khả năng sẵn sàng, bảo trì hoặc ngừng sử dụng của từng phòng điều trị theo thời gian thực.</p>
      </div>
    </div>

    <div class="rules-card">
      <div class="rule-item">
        <span class="rule-icon">ℹ️</span>
        <span>Phòng đã được nha sĩ chọn nhận ca cần được nha sĩ/Admin đổi hoặc bỏ phòng trước. <router-link to="/management/staff/workspace" class="link-btn">Mở phân bổ phòng làm việc →</router-link></span>
      </div>
      <div class="rule-item">
        <span class="rule-icon">🔒</span>
        <span>Phòng <strong>Đang phục vụ</strong> chỉ được giải phóng tự động khi ca khám hoàn tất hoặc bị hủy hợp lệ.</span>
      </div>
    </div>

    <form class="filters-card" @submit.prevent="load(1)">
      <div class="filter-group">
        <label for="chair-search-input">Tìm phòng điều trị</label>
        <div class="input-with-icon">
          <span class="input-icon">🔍</span>
          <input id="chair-search-input" v-model="keyword" placeholder="Nhập tên phòng điều trị..." maxlength="100" :disabled="saving" />
        </div>
      </div>
      <div class="filter-group">
        <label for="chair-status-filter">Trạng thái phòng</label>
        <select id="chair-status-filter" v-model.number="filterStatus" :disabled="saving">
          <option :value="0">Tất cả trạng thái</option>
          <option :value="1">🟢 Sẵn sàng</option>
          <option :value="2">🔵 Đang phục vụ</option>
          <option :value="3">🟠 Bảo trì</option>
          <option :value="4">⚫ Ngừng sử dụng</option>
        </select>
      </div>
      <button class="btn btn-primary" :disabled="loading || saving">
        <span v-if="loading">Đang tải…</span>
        <span v-else>Tải lại danh sách</span>
      </button>
    </form>

    <div v-if="error" class="alert-box alert-error" role="alert">⚠️ {{ error }}</div>
    <div v-if="message" class="alert-box alert-success" role="status">✅ {{ message }}</div>
    <div v-if="needsReview" class="alert-box alert-warning" role="alert">ℹ️ Đã tải lại danh sách. Vui lòng đối chiếu trạng thái trước khi thực hiện thao tác kế tiếp.</div>

    <div v-if="loading" class="loading-state" role="status">
      <div class="spinner"></div>
      <p>Đang tải danh sách phòng điều trị…</p>
    </div>

    <div v-else-if="store.result && !store.result.items.length" class="empty-state">
      <p>Không tìm thấy phòng điều trị nào phù hợp với bộ lọc hiện tại.</p>
    </div>
    <div v-else class="chairs-grid">
      <article
        v-for="chair in store.result?.items ?? []"
        :key="chair.id"
        class="chair-card"
        :class="{
          'status-ready': chair.status === 1,
          'status-busy': chair.status === 2 || chair.hasActiveVisit,
          'status-maintenance': chair.status === 3,
          'status-disabled': chair.status === 4
        }"
      >
        <div class="chair-card-header">
          <div class="chair-badge-id">Phòng: {{ chair.roomName }}</div>
          <span
            class="chair-status-badge"
            :class="{
              'badge-ready': chair.status === 1,
              'badge-busy': chair.status === 2 || chair.hasActiveVisit,
              'badge-maintenance': chair.status === 3,
              'badge-disabled': chair.status === 4
            }"
          >
            {{ chair.statusText }}
          </span>
        </div>

        <div class="chair-room-name">🏷️ Mã thiết lập: {{ chair.chairNumber }}</div>

        <div class="chair-card-notes">
          <p v-if="chair.status === 2 || chair.hasActiveVisit" class="note-busy">
            🔒 Phòng đang phục vụ ca khám thực tế; không thể đổi thủ công.
          </p>
          <p v-else-if="chair.isAssigned" class="note-assigned">
            👤 Đã có nha sĩ nhận phòng hôm nay. Cần đổi phòng trước khi thay đổi trạng thái.
          </p>
          <p v-else class="note-ready">
            ✨ Sẵn sàng tiếp nhận bệnh nhân hoặc điều chỉnh trạng thái bảo trì.
          </p>
        </div>

        <div class="chair-card-footer">
          <button
            type="button"
            class="btn btn-action"
            :disabled="!canEdit(chair)"
            :aria-label="'Đổi trạng thái phòng ' + chair.roomName"
            @click="open(chair, $event)"
          >
            ⚙️ Đổi trạng thái
          </button>
        </div>
      </article>
    </div>

    <nav v-if="store.result && store.result.totalCount > 0" class="pagination-bar" aria-label="Phân trang trạng thái phòng điều trị">
      <div class="pagination-info">
        Tổng số: <strong>{{ store.result.totalCount }}</strong> phòng · Trang <strong>{{ store.result.page }}</strong>
      </div>
      <div class="pagination-controls">
        <button class="btn btn-outline" :disabled="loading || saving || store.result.page <= 1" @click="load(store.result.page - 1)">
          ← Trang trước
        </button>
        <button class="btn btn-outline" :disabled="loading || saving || !store.result.hasNextPage" @click="load(store.result.page + 1)">
          Trang sau →
        </button>
      </div>
    </nav>

    <section v-if="selected" class="editor-overlay" @click.self="close">
      <div class="editor-card" role="dialog" aria-labelledby="chair-edit-title" aria-modal="true">
        <div class="editor-header">
          <h2 id="chair-edit-title" ref="editorHeading" tabindex="-1">
            Đổi trạng thái: {{ selected.roomName }}
          </h2>
          <button type="button" class="btn-close" :disabled="saving" @click="close" aria-label="Đóng">✕</button>
        </div>

        <form class="editor-form" @submit.prevent="save">
          <div class="current-meta">
            <div>Phòng điều trị: <strong>{{ selected.roomName }}</strong> (Mã: {{ selected.chairNumber }})</div>
            <div>Trạng thái hiện tại: <span class="badge-current">{{ selected.statusText }}</span></div>
          </div>

          <div class="form-group">
            <label for="target-status-select">Chọn trạng thái mới</label>
            <select id="target-status-select" v-model.number="targetStatus" :disabled="saving || needsReview" @change="confirmed = false">
              <option :value="1">🟢 Sẵn sàng</option>
              <option :value="3">🟠 Bảo trì</option>
              <option :value="4">⚫ Ngừng sử dụng</option>
            </select>
          </div>

          <label class="confirmation-label">
            <input v-model="confirmed" type="checkbox" :disabled="saving || needsReview" />
            <span>Tôi đã kiểm tra đúng phòng điều trị và xác nhận chuyển sang trạng thái mới.</span>
          </label>

          <div class="editor-actions">
            <button
              class="btn btn-primary"
              :disabled="saving || loading || needsReview || !confirmed || targetStatus === selected.status"
            >
              {{ saving ? 'Đang cập nhật…' : 'Xác nhận đổi trạng thái' }}
            </button>
            <button type="button" class="btn btn-outline" :disabled="saving" @click="close">Hủy bỏ</button>
          </div>
        </form>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useChairStatusStore } from '@/stores/chairStatus';
import { chairStatusService, type ChairStatusRow } from '@/services/chairStatus';

const auth = useAuthStore(), store = useChairStatusStore();
const keyword = ref(''), filterStatus = ref(0), loading = ref(false), saving = ref(false);
const error = ref(''), message = ref(''), needsReview = ref(false);
const selected = ref<ChairStatusRow | null>(null), targetStatus = ref(1), confirmed = ref(false);
const editorHeading = ref<HTMLElement | null>(null);
let readVersion = 0, life = 0, returnFocus: HTMLElement | null = null;
const identity = () => `${auth.user?.userId ?? ''}/${auth.role}`;
const allowed = () => ['ADMIN', 'ASSISTANT'].includes(auth.role);
function validRow(row: ChairStatusRow) {
  return row && Number.isInteger(row.id) && row.id > 0 && Number.isInteger(row.version) && row.version > 0 &&
    [1, 2, 3, 4].includes(row.status) && typeof row.chairNumber === 'string' && typeof row.roomName === 'string' &&
    typeof row.statusText === 'string' && typeof row.isActive === 'boolean' && typeof row.isAssigned === 'boolean' && typeof row.hasActiveVisit === 'boolean';
}
function clearForm() { selected.value = null; targetStatus.value = 1; confirmed.value = false; }
function close() { if (saving.value) return; clearForm(); returnFocus?.focus(); returnFocus = null; }
function canEdit(row: ChairStatusRow) {
  return allowed() && !loading.value && !saving.value && !needsReview.value && validRow(row) &&
    row.status !== 2 && !row.isAssigned && !row.hasActiveVisit;
}
async function load(page = 1) {
  if (!allowed() || saving.value || !Number.isInteger(page) || page < 1 || page > 1_000_000 ||
    keyword.value.length > 100 || ![0, 1, 2, 3, 4].includes(filterStatus.value)) return;
  const ticket = ++readVersion, actor = identity(); loading.value = true; error.value = ''; clearForm(); store.reset();
  try {
    const result = await chairStatusService.getPage({ page, pageSize: 20, keyword: keyword.value.trim() || undefined, status: filterStatus.value || undefined });
    if (ticket !== readVersion || actor !== identity() || !allowed()) return;
    if (!result || !Array.isArray(result.items) || !result.items.every(validRow) || result.page !== page || !Number.isInteger(result.totalCount) || result.totalCount < 0)
      throw new Error('Invalid chair list');
    store.replace(result); needsReview.value = false;
  } catch { if (ticket === readVersion && actor === identity()) error.value = 'Không thể tải trạng thái ghế. Vui lòng thử lại.'; }
  finally { if (ticket === readVersion && actor === identity()) loading.value = false; }
}
async function open(row: ChairStatusRow, event?: Event) {
  if (!canEdit(row) || !store.result?.items.some(x => x.id === row.id && x.version === row.version)) return;
  selected.value = { ...row }; targetStatus.value = row.status; confirmed.value = false; error.value = ''; message.value = '';
  returnFocus = (event?.currentTarget as HTMLElement | null) ?? null;
  await nextTick(); editorHeading.value?.focus();
}
async function save() {
  const old = selected.value;
  if (!old || !canEdit(old) || !confirmed.value) return;
  if (![1, 3, 4].includes(targetStatus.value) || targetStatus.value === old.status) { error.value = 'Chọn trạng thái mới hợp lệ.'; return; }
  const ticket = life, actor = identity(), status = targetStatus.value;
  saving.value = true; error.value = ''; message.value = '';
  try {
    const row = await chairStatusService.change(old.id, { status, expectedVersion: old.version, confirmed: true });
    if (ticket !== life || actor !== identity() || !allowed()) return;
    if (!validRow(row) || row.id !== old.id || row.status !== status || row.version <= old.version || row.isActive !== (status !== 4) || row.isAssigned || row.hasActiveVisit)
      throw new Error('Unverified chair change');
    message.value = 'Đã cập nhật trạng thái ghế.'; clearForm(); needsReview.value = true; saving.value = false;
    await load(store.result?.page ?? 1);
  } catch (err: unknown) {
    if (ticket !== life || actor !== identity()) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response.response?.data?.message || 'Chưa xác minh được kết quả lưu. Tải lại danh sách để đối chiếu.';
    confirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life && actor === identity()) saving.value = false; }
}
function resetSession() {
  life++; readVersion++; store.reset(); clearForm(); loading.value = false; saving.value = false;
  error.value = ''; message.value = ''; needsReview.value = false; returnFocus = null;
}
watch(identity, () => { resetSession(); if (allowed()) void load(); }, { flush: 'sync' });
onMounted(() => load());
onUnmounted(resetSession);
</script>

<style scoped>
.chair-status { max-width: 1100px; margin: 0 auto; padding: 1.5rem; box-sizing: border-box; }
.header-banner { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-banner h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }
.rules-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem 1rem; margin-bottom: 1.25rem; display: flex; flex-direction: column; gap: 0.5rem; }
.rule-item { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; }
.rule-icon { font-size: 1rem; flex-shrink: 0; }
.link-btn { color: #0284c7; text-decoration: underline; font-weight: 600; margin-left: 0.25rem; }
.filters-card { display: flex; flex-wrap: wrap; gap: 1rem; align-items: flex-end; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.25rem; margin-bottom: 1.5rem; }
.filter-group { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; min-width: 200px; }
.filter-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.input-with-icon { position: relative; display: flex; align-items: center; }
.input-icon { position: absolute; left: 0.75rem; font-size: 0.9rem; color: #94a3b8; pointer-events: none; }
.input-with-icon input { padding-left: 2.25rem; width: 100%; }
input, select { height: 44px; padding: 0.5rem 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; background: #fff; box-sizing: border-box; }
.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0369a1; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-action { width: 100%; height: 40px; background: #f0f9ff; color: #0284c7; border: 1px solid #bae6fd; font-size: 0.875rem; }
.alert-box { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
.loading-state, .empty-state { text-align: center; padding: 3rem 1rem; background: #f8fafc; border-radius: 12px; border: 1px dashed #cbd5e1; color: #64748b; }
.spinner { width: 32px; height: 32px; border: 3px solid #e2e8f0; border-top-color: #0284c7; border-radius: 50%; animation: spin 0.8s linear infinite; margin: 0 auto 0.75rem; }
@keyframes spin { to { transform: rotate(360deg); } }
.chairs-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
.chair-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.15rem; display: flex; flex-direction: column; justify-content: space-between; border-left: 5px solid #cbd5e1; }
.chair-card.status-ready { border-left-color: #10b981; }
.chair-card.status-busy { border-left-color: #3b82f6; background: #f8fafc; }
.chair-card.status-maintenance { border-left-color: #f59e0b; }
.chair-card.status-disabled { border-left-color: #94a3b8; }
.chair-card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
.chair-badge-id { font-size: 1.15rem; font-weight: 700; color: #0f172a; }
.chair-status-badge { font-size: 0.75rem; font-weight: 600; padding: 0.25rem 0.6rem; border-radius: 9999px; }
.badge-ready { background: #dcfce7; color: #15803d; }
.badge-busy { background: #dbeafe; color: #1d4ed8; }
.badge-maintenance { background: #fef3c7; color: #b45309; }
.badge-disabled { background: #f1f5f9; color: #475569; }
.chair-room-name { font-size: 0.9rem; color: #475569; font-weight: 500; margin-bottom: 0.75rem; }
.chair-card-notes { font-size: 0.8rem; line-height: 1.4; min-height: 44px; margin-bottom: 1rem; }
.chair-card-notes p { margin: 0; }
.note-busy { color: #1e40af; } .note-assigned { color: #b45309; } .note-ready { color: #059669; }
.pagination-bar { display: flex; justify-content: space-between; align-items: center; margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #e2e8f0; }
.editor-overlay { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.5); backdrop-filter: blur(2px); display: flex; align-items: center; justify-content: center; z-index: 50; padding: 1rem; }
.editor-card { background: #fff; border-radius: 14px; max-width: 480px; width: 100%; padding: 1.5rem; }
.editor-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
.editor-header h2 { font-size: 1.2rem; margin: 0; color: #0f172a; outline: none; }
.btn-close { background: transparent; border: none; font-size: 1.25rem; color: #94a3b8; cursor: pointer; }
.editor-form { display: flex; flex-direction: column; gap: 1rem; }
.current-meta { background: #f8fafc; border-radius: 8px; padding: 0.75rem; font-size: 0.875rem; }
.badge-current { font-weight: 600; color: #0284c7; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.confirmation-label { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.85rem; color: #334155; }
.editor-actions { display: flex; gap: 0.75rem; justify-content: flex-end; }
@media (max-width: 640px) {
  .chair-status { padding: 1rem; }
  .filters-card { flex-direction: column; align-items: stretch; }
  .btn { width: 100%; }
  .chairs-grid { grid-template-columns: 1fr; }
}
</style>
