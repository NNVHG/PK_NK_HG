<template>
  <section class="rooms-page" aria-labelledby="rooms-title">
    <header class="page-header">
      <div>
        <h1 id="rooms-title">Phòng điều trị &amp; Trạng thái</h1>
        <p class="subtitle">Theo dõi nha sĩ, phụ tá và tình trạng phục vụ của từng phòng điều trị.</p>
      </div>
      <div class="header-actions">
        <router-link to="/management/staff/workspace" class="button button-secondary">
          Phân bổ phòng làm việc →
        </router-link>
        <button type="button" class="button button-secondary" :disabled="saving || loading" @click="load(store.result?.page ?? 1)">
          Làm mới
        </button>
        <button type="button" class="button button-primary" :disabled="saving || loading || needsReview" @click="openCreate">
          <span aria-hidden="true">+</span> Thêm phòng
        </button>
      </div>
    </header>

    <div class="summary-strip" aria-label="Tổng quan trạng thái phòng">
      <div class="summary-card">
        <span class="summary-label">Phòng trên trang</span>
        <strong class="summary-val">{{ summaryCounts.total }}</strong>
      </div>
      <div class="summary-card summary-card--ready">
        <span class="summary-label">Sẵn sàng</span>
        <strong class="summary-val">{{ summaryCounts.ready }}</strong>
      </div>
      <div class="summary-card summary-card--busy">
        <span class="summary-label">Đang phục vụ / Đã nhận</span>
        <strong class="summary-val">{{ summaryCounts.busy }}</strong>
      </div>
      <div class="summary-card summary-card--maint">
        <span class="summary-label">Bảo trì / Ngừng dùng</span>
        <strong class="summary-val">{{ summaryCounts.inactive }}</strong>
      </div>
    </div>

    <div v-if="error && !formOpen && !quickStatusOpen" class="alert alert-error" role="alert">{{ error }}</div>
    <p v-if="liveStatusUnavailable" class="alert alert-warning" role="status">Chưa đồng bộ được trạng thái phòng. Hệ thống sẽ tự thử lại.</p>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">
      Dữ liệu phòng vừa được cập nhật. Bấm <button class="link-btn" @click="load(1)">Tải lại danh sách</button> để đối chiếu trước khi thao tác tiếp.
    </div>

    <section class="panel" aria-labelledby="room-list-title">
      <div class="panel-header">
        <div>
          <h2 id="room-list-title" class="panel-title">Danh sách phòng điều trị</h2>
          <p class="panel-note">Trạng thái và nhân sự tự cập nhật.</p>
        </div>
        <span class="count-badge">{{ store.result?.totalCount ?? 0 }} phòng phù hợp</span>
      </div>
      <div class="filters-panel">
        <form class="filters-grid" @submit.prevent="load(1)">
          <div class="filter-field">
            <label for="chair-search">Tìm kiếm phòng điều trị</label>
            <input id="chair-search" v-model="keyword" type="search" placeholder="Tên hoặc mã phòng" maxlength="100" :disabled="saving" />
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

      <div class="panel-body">
        <div v-if="loading" class="loading-state">Đang tải danh mục phòng...</div>

        <div v-else-if="store.result?.items && store.result.items.length > 0" class="chairs-grid">
          <article
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
                <h3 class="room-name">{{ chair.roomName }}</h3>
                <span class="chair-number">Mã: {{ chair.chairNumber }}</span>
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
                <span v-if="liveStatus(chair.id)?.hasActiveVisit || chair.status === 2" class="live-tag live-tag--busy">Đang phục vụ ca khám</span>
                <span v-else-if="liveStatus(chair.id)?.isAssigned" class="live-tag live-tag--assigned">Đã có nha sĩ nhận phòng</span>
              </div>
              <dl class="room-staff">
                <div>
                  <dt>Nha sĩ</dt>
                  <dd v-if="liveStatus(chair.id)?.dentistName">{{ liveStatus(chair.id)?.dentistName }}</dd>
                  <dd v-else-if="liveStatus(chair.id)?.isAssigned">Đã có người nhận phòng</dd>
                  <dd v-else>{{ liveStatus(chair.id) ? 'Chưa có nha sĩ' : 'Chưa đồng bộ' }}</dd>
                </div>
                <div>
                  <dt>Phụ tá</dt>
                  <dd v-if="liveStatus(chair.id)?.assistants?.length">
                    <ul>
                      <li v-for="assistant in liveStatus(chair.id)?.assistants" :key="assistant.userId">{{ assistant.fullName }}</li>
                    </ul>
                  </dd>
                  <dd v-else>{{ liveStatus(chair.id) ? 'Chưa có phụ tá' : 'Chưa đồng bộ' }}</dd>
                </div>
              </dl>
            </div>

            <div class="chair-card-actions">
              <span v-if="chair.status === 2 || liveStatus(chair.id)?.hasActiveVisit" class="busy-hint">
                Tự động giải phóng khi kết thúc ca
              </span>
              <template v-else>
                <button
                  type="button"
                  class="button button-secondary btn-sm"
                  :disabled="saving || loading || needsReview || Boolean(liveStatus(chair.id)?.isAssigned)"
                  :title="liveStatus(chair.id)?.isAssigned ? 'Đã có nha sĩ nhận phòng hôm nay; không thể đổi trạng thái nhanh' : 'Đổi nhanh trạng thái phòng'"
                  @click="openQuickStatus(chair)"
                >
                  Đổi trạng thái
                </button>
                <button
                  type="button"
                  class="button button-secondary btn-sm"
                  :disabled="saving || loading || needsReview"
                  @click="openEdit(chair)"
                >
                  Cấu hình
                </button>
              </template>
            </div>
          </article>
        </div>

        <div v-else class="empty-state">Không tìm thấy phòng điều trị phù hợp.</div>

        <div v-if="store.result && store.result.totalCount > 0" class="pagination">
          <span class="page-info">Trang {{ store.result.page }} (Tổng {{ store.result.totalCount }} phòng)</span>
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
    </section>
    <dialog v-if="formOpen" :ref="openDialog" class="modal-card" aria-labelledby="room-form-title" @cancel.prevent="closeForm">
      <div class="modal-header">
        <h2 id="room-form-title">{{ selected ? 'Cấu hình phòng: ' + selected.roomName : 'Thêm phòng điều trị' }}</h2>
        <button class="close-btn" type="button" aria-label="Đóng hộp thoại" :disabled="saving" @click="closeForm">✕</button>
      </div>

      <form class="modal-form" @submit.prevent="save">
        <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
        <div class="form-grid">
          <div class="form-field">
            <label for="room-name">Tên phòng điều trị <b>*</b></label>
            <input id="room-name" v-model="roomName" maxlength="100" required placeholder="VD: Phòng điều trị 1" :disabled="saving" autofocus />
          </div>

          <div class="form-field">
            <label for="room-code">Mã phòng <b>*</b></label>
            <input id="room-code" v-model="chairNumber" maxlength="50" required placeholder="VD: P-01" :disabled="saving || selected !== null" />
            <span v-if="selected" class="form-hint">Mã thiết lập không thể sửa sau khi khai báo.</span>
          </div>

          <div class="form-field form-field--wide">
            <label for="room-form-status">Trạng thái vận hành <b>*</b></label>
            <select id="room-form-status" v-model.number="formStatus" :disabled="saving">
              <option :value="1">Sẵn sàng phục vụ</option>
              <option :value="3">Bảo trì kỹ thuật</option>
              <option :value="4">Ngừng sử dụng</option>
            </select>
            <p v-if="formStatus === 4" class="form-hint text-warning">
              Phòng ngừng sử dụng sẽ bị ẩn khỏi danh sách chỉ định ca khám nhưng vẫn bảo tồn lịch sử.
            </p>
          </div>

          <div class="form-field form-field--wide">
            <label for="room-description">Ghi chú trang thiết bị</label>
            <textarea id="room-description" v-model="description" rows="2" maxlength="500" :disabled="saving"></textarea>
          </div>

          <div class="form-field form-field--wide">
            <div class="confirm-box">
              <label class="checkbox-label text-warning">
                <input v-model="confirmed" type="checkbox" :disabled="saving || needsReview" />
                <span>Tôi đã kiểm tra thông tin và xác nhận trạng thái phòng.</span>
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
    </dialog>

    <!-- Modal đổi nhanh trạng thái phòng -->
    <dialog v-if="quickStatusOpen && quickChair" :ref="openDialog" class="modal-card modal-card--sm" aria-labelledby="quick-room-title" @cancel.prevent="closeQuickStatus">
      <div class="modal-header">
        <h2 id="quick-room-title">Đổi trạng thái: {{ quickChair.roomName }}</h2>
        <button class="close-btn" type="button" aria-label="Đóng hộp thoại" :disabled="saving" @click="closeQuickStatus">✕</button>
      </div>
      <form class="modal-form" @submit.prevent="saveQuickStatus">
        <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
        <div class="form-grid">
          <div class="form-field form-field--wide">
            <label for="quick-chair-status">Trạng thái mới <b>*</b></label>
            <select id="quick-chair-status" v-model.number="quickTargetStatus" :disabled="saving" autofocus>
              <option :value="1">Sẵn sàng phục vụ</option>
              <option :value="3">Bảo trì kỹ thuật</option>
              <option :value="4">Ngừng sử dụng</option>
            </select>
          </div>
          <div class="form-field form-field--wide confirm-box">
            <label class="checkbox-label">
              <input v-model="quickConfirmed" type="checkbox" :disabled="saving || needsReview" />
              <span>Xác nhận cập nhật trạng thái phòng {{ quickChair.roomName }} ({{ quickChair.chairNumber }}).</span>
            </label>
          </div>
        </div>
        <div class="modal-actions">
          <button class="button button-secondary" type="button" :disabled="saving" @click="closeQuickStatus">Hủy</button>
          <button class="button button-primary" type="submit" :disabled="saving || needsReview || !quickConfirmed || quickTargetStatus === quickChair.status">
            {{ saving ? 'Đang cập nhật...' : 'Xác nhận đổi trạng thái' }}
          </button>
        </div>
      </form>
    </dialog>
  </section>
</template>
<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useDentalChairsStore } from '@/stores/dentalChairs';
import { dentalChairsService, type ChairQuery, type DentalChair } from '@/services/dentalChairs';
import { chairStatusService, type ChairStatusRow } from '@/services/chairStatus';

const auth = useAuthStore(), store = useDentalChairsStore();
const keyword = ref(''), status = ref(0), active = ref('all'), loading = ref(false), saving = ref(false), error = ref(''), message = ref(''), needsReview = ref(false);
const formOpen = ref(false), selected = ref<DentalChair | null>(null), chairNumber = ref(''), roomName = ref(''), formStatus = ref(1), description = ref(''), confirmed = ref(false);
const liveStatusMap = ref<Record<number, ChairStatusRow>>({});
const liveStatusUnavailable = ref(false);
const quickStatusOpen = ref(false), quickChair = ref<DentalChair | null>(null), quickTargetStatus = ref(1), quickConfirmed = ref(false);
let version = 0, life = 0;
let appliedQuery: ChairQuery | undefined;
let refreshing = false;
let refreshTimer: ReturnType<typeof setInterval> | undefined;
let activeDialog: HTMLDialogElement | null = null;
const loadError = 'Không thể tải danh sách phòng điều trị. Vui lòng thử lại.';

function liveStatus(id: number): ChairStatusRow | undefined {
  return liveStatusMap.value[id];
}

function openDialog(element: unknown) {
  if (!(element instanceof HTMLDialogElement)) return;
  activeDialog = element;
  if (!element.open) element.showModal();
}

function dismissDialog() {
  activeDialog?.close();
  activeDialog = null;
}

const summaryCounts = computed(() => {
  const items = store.result?.items ?? [];
  let ready = 0, busy = 0, inactive = 0;
  for (const c of items) {
    const live = liveStatusMap.value[c.id];
    if (c.status === 2 || live?.hasActiveVisit || live?.isAssigned) busy++;
    else if (c.status === 1 && c.isActive) ready++;
    else inactive++;
  }
  return { total: items.length, ready, busy, inactive };
});

function clearForm() { if (formOpen.value) dismissDialog(); formOpen.value = false; selected.value = null; chairNumber.value = ''; roomName.value = ''; formStatus.value = 1; description.value = ''; confirmed.value = false; }
function closeForm() { if (!saving.value) clearForm(); }
function closeQuickStatus() { if (!saving.value) { if (quickStatusOpen.value) dismissDialog(); quickStatusOpen.value = false; quickChair.value = null; quickConfirmed.value = false; } }

async function load(page = 1) {
  if (auth.role !== 'ADMIN' || saving.value || !Number.isInteger(page) || page < 1 || page > 1_000_000 || keyword.value.length > 100 || ![0, 1, 2, 3, 4].includes(status.value) || !['all', 'true', 'false'].includes(active.value)) return;
  appliedQuery = {
    page, pageSize: 20, keyword: keyword.value.trim() || undefined,
    status: status.value || undefined, isActive: active.value === 'all' ? undefined : active.value === 'true'
  };
  loading.value = true; error.value = ''; store.reset(); liveStatusMap.value = {}; clearForm(); closeQuickStatus();
  await readPage(appliedQuery, false);
}

async function readPage(query: ChairQuery, background: boolean) {
  const ticket = ++version;
  try {
    const [chairsRes, statusRes] = await Promise.allSettled([
      dentalChairsService.getPage(query),
      chairStatusService.getPage(query)
    ]);
    if (ticket !== version || auth.role !== 'ADMIN' || saving.value) return;
    if (chairsRes.status === 'rejected') throw chairsRes.reason;
    store.replace(chairsRes.value);
    if (statusRes.status === 'fulfilled') {
      liveStatusMap.value = Object.fromEntries(statusRes.value.items.map(row => [row.id, row]));
    }
    liveStatusUnavailable.value = statusRes.status === 'rejected';
    if (error.value === loadError) error.value = '';
    if (!background) needsReview.value = false;
  } catch {
    if (ticket !== version) return;
    if (background) liveStatusUnavailable.value = true;
    else error.value = loadError;
  } finally {
    if (!background && ticket === version) loading.value = false;
  }
}

async function refreshPage() {
  if (!appliedQuery || auth.role !== 'ADMIN' || document.hidden || loading.value || saving.value || refreshing || needsReview.value) return;
  refreshing = true;
  try { await readPage(appliedQuery, true); }
  finally { refreshing = false; }
}
function openCreate() {
  if (auth.role !== 'ADMIN' || loading.value || saving.value || needsReview.value) return;
  clearForm(); closeQuickStatus(); formOpen.value = true; error.value = ''; message.value = '';
}
function openEdit(chair: DentalChair) {
  if (auth.role !== 'ADMIN' || loading.value || saving.value || needsReview.value || !Number.isInteger(chair.id) || chair.id <= 0 || chair.status === 2 || !store.result?.items.some(c => c.id === chair.id && c.version === chair.version)) return;
  clearForm(); closeQuickStatus(); selected.value = { ...chair }; chairNumber.value = chair.chairNumber; roomName.value = chair.roomName; formStatus.value = chair.status; description.value = chair.description ?? ''; formOpen.value = true; error.value = ''; message.value = '';
}
function openQuickStatus(chair: DentalChair) {
  const live = liveStatusMap.value[chair.id];
  if (auth.role !== 'ADMIN' || loading.value || saving.value || needsReview.value || chair.status === 2 || live?.hasActiveVisit || live?.isAssigned) return;
  clearForm(); quickChair.value = { ...chair }; quickTargetStatus.value = chair.status; quickConfirmed.value = false; quickStatusOpen.value = true; error.value = ''; message.value = '';
}
async function saveQuickStatus() {
  const old = quickChair.value;
  if (auth.role !== 'ADMIN' || !quickStatusOpen.value || !old || loading.value || saving.value || needsReview.value || !quickConfirmed.value) return;
  if (liveStatus(old.id)?.isAssigned || liveStatus(old.id)?.hasActiveVisit) {
    closeQuickStatus();
    error.value = 'Phòng vừa có nhân viên nhận hoặc có ca khám.';
    return;
  }
  if (![1, 3, 4].includes(quickTargetStatus.value) || quickTargetStatus.value === old.status) return;
  const ticket = life; saving.value = true; error.value = ''; message.value = '';
  try {
    await chairStatusService.change(old.id, { status: quickTargetStatus.value, expectedVersion: old.version, confirmed: true });
    if (ticket !== life || auth.role !== 'ADMIN') return;
    message.value = `Đã cập nhật trạng thái phòng ${old.roomName}.`;
    saving.value = false;
    closeQuickStatus();
    needsReview.value = true;
    await load(store.result?.page ?? 1);
  } catch (err: unknown) {
    if (ticket !== life || auth.role !== 'ADMIN') return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Không thể đổi trạng thái phòng. Vui lòng tải lại để đối chiếu.';
    quickConfirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) saving.value = false; }
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
    message.value = 'Đã lưu thông tin phòng điều trị.'; clearForm(); needsReview.value = true; saving.value = false; await load(1);
  } catch (err: unknown) {
    if (ticket !== life || auth.role !== 'ADMIN') return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả lưu. Tải lại để đối chiếu.'; confirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) saving.value = false; }
}
onMounted(() => {
  if (typeof document !== 'undefined') {
    refreshTimer = setInterval(refreshPage, 5000);
    document.addEventListener('visibilitychange', refreshPage);
  }
  return load();
});
onUnmounted(() => {
  clearInterval(refreshTimer);
  if (typeof document !== 'undefined') document.removeEventListener('visibilitychange', refreshPage);
  life++; version++; clearForm(); closeQuickStatus(); store.reset(); error.value = ''; message.value = '';
});
</script>
<style scoped>
.rooms-page {
  --ink: #122747;
  --muted: #475569;
  --line: #dce5ef;
  --accent: #075b85;
  box-sizing: border-box;
  container: rooms / inline-size;
  display: grid;
  gap: 1.25rem;
  width: 100%;
  max-width: 1240px;
  min-width: 0;
  margin: 0 auto;
  padding: clamp(0.75rem, 2vw, 1.5rem);
  color: var(--ink);
}

.page-header {
  display: grid;
  gap: 1rem 1.5rem;
  padding: clamp(1rem, 4cqi, 1.75rem);
  border: 1px solid #cfe3f0;
  border-radius: 16px;
  background: linear-gradient(140deg, #fff 30%, #eef7fc);
}

h1 { margin: 0; font-size: clamp(1.45rem, 4.5cqi, 2rem); line-height: 1.2; }
.subtitle { margin: 0.5rem 0 0; color: var(--muted); line-height: 1.5; }
.header-actions, .chair-card-actions, .pagination, .page-btns, .modal-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  align-items: center;
}

.summary-strip {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: clamp(0.55rem, 2cqi, 1rem);
}

.summary-card {
  display: grid;
  align-content: space-between;
  gap: 0.55rem;
  min-width: 0;
  padding: clamp(0.65rem, 2cqi, 1rem);
  border: 1px solid var(--line);
  border-radius: 12px;
  border-top: 3px solid #64748b;
  background: #fff;
}

.summary-card--ready { border-top-color: #12805c; }
.summary-card--busy { border-top-color: #1674a6; }
.summary-card--maint { border-top-color: #a85a05; }
.summary-label { font-size: 0.85rem; color: var(--muted); line-height: 1.4; }
.summary-val { font-size: 1.5rem; line-height: 1; font-variant-numeric: tabular-nums; }

.panel { background: #fff; border: 1px solid var(--line); border-radius: 16px; min-width: 0; }
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.65rem 1rem;
  padding: clamp(0.85rem, 2.5cqi, 1.35rem);
}
.panel-title { margin: 0; font-size: 1.1rem; line-height: 1.35; }
.panel-note { margin: 0.2rem 0 0; color: var(--muted); font-size: 0.85rem; }
.count-badge, .status-pill, .live-tag {
  max-width: 100%;
  overflow-wrap: anywhere;
  border-radius: 999px;
  padding: 0.25rem 0.65rem;
  font-size: 0.78rem;
  line-height: 1.4;
  font-weight: 700;
}

.count-badge { background: #edf3fa; color: #334155; }
.filters-panel, .panel-body { padding: clamp(0.85rem, 2.5cqi, 1.35rem); }
.filters-panel { border-block: 1px solid var(--line); background: #f7fafd; }
.filters-grid { display: grid; grid-template-columns: minmax(0, 1fr); align-items: end; gap: 0.85rem; margin: 0; }
.filter-field, .form-field {
  display: grid;
  align-content: start;
  min-width: 0;
  gap: 0.4rem;
  color: #334155;
  font-size: 0.85rem;
  font-weight: 650;
}
.filter-actions .button { width: 100%; }
input:not([type='checkbox']), select, textarea {
  box-sizing: border-box;
  min-width: 0;
  width: 100%;
  min-height: 44px;
  padding: 0.55rem 0.7rem;
  border: 1px solid #b9c9db;
  border-radius: 8px;
  background: #fff;
  color: #1e293b;
  font: inherit;
  font-size: 1rem;
}

textarea { resize: vertical; }
:where(button, a, input, select, textarea):focus-visible { outline: 2px solid #0673af; outline-offset: 2px; }

.chairs-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr)); gap: 1rem; }
.chair-card {
  --room-accent: #64748b;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  min-width: 0;
  padding: clamp(0.75rem, 2cqi, 1rem);
  border: 1px solid var(--line);
  border-top: 3px solid var(--room-accent);
  border-radius: 12px;
  overflow-wrap: anywhere;
}
.chair-card--ready { --room-accent: #12805c; }
.chair-card--busy { --room-accent: #1674a6; }
.chair-card--maintenance { --room-accent: #a85a05; }
.chair-card--disabled { background: #f8fafc; }
.chair-card-header { display: grid; justify-items: start; gap: 0.55rem; }
.chair-badge-group { min-width: 0; }
.room-name { margin: 0; font-size: 1.05rem; line-height: 1.35; }
.chair-number { display: block; margin-top: 0.2rem; font-size: 0.8rem; color: var(--muted); }
.chair-desc { margin: 0; font-size: 0.875rem; color: var(--muted); line-height: 1.5; white-space: pre-wrap; }
.chair-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 0.4rem; margin-top: 0.6rem; font-size: 0.8rem; color: var(--muted); }
.room-staff {
  display: grid;
  gap: 0.5rem;
  margin: 0.75rem 0 0;
  padding: 0.65rem;
  border-radius: 8px;
  background: #f1f6fb;
  font-size: 0.85rem;
  line-height: 1.4;
}
.room-staff > div { display: grid; grid-template-columns: minmax(0, 3.45rem) minmax(0, 1fr); gap: 0.5rem; }
.room-staff dt { color: var(--muted); }
.room-staff dd { margin: 0; font-weight: 650; }
.room-staff ul { list-style: none; margin: 0; padding: 0; }
.room-staff li + li { margin-top: 0.25rem; }
.chair-card-actions { margin-top: auto; padding-top: 0.75rem; border-top: 1px solid var(--line); }
.chair-card-actions .button { flex: 1 1 6.75rem; }
.busy-hint { font-size: 0.8rem; line-height: 1.4; color: var(--accent); font-weight: 650; }
.status-pill--ready { background: #e6f7ef; color: #136341; }
.status-pill--busy, .live-tag--busy { background: #e2f2fc; color: #07577e; }
.status-pill--maint { background: #fff0d4; color: #874600; }
.status-pill--disabled { background: #eaf0f6; color: #475569; }
.live-tag--assigned { background: #ede9fe; color: #5b21b6; }

.pagination { justify-content: space-between; margin-top: 1rem; padding-top: 1rem; border-top: 1px solid var(--line); }
.page-info { font-size: 0.85rem; color: var(--muted); }
.page-btns { flex: 0 1 auto; }
.page-btns .button { flex: 1 1 auto; }
.button {
  box-sizing: border-box;
  display: inline-flex;
  justify-content: center;
  align-items: center;
  gap: 0.35rem;
  max-width: 100%;
  min-height: 44px;
  padding: 0.55rem 0.8rem;
  border: 1px solid transparent;
  border-radius: 8px;
  font-family: inherit;
  font-size: 0.875rem;
  line-height: 1.35;
  font-weight: 700;
  text-align: center;
  text-decoration: none;
  white-space: normal;
  cursor: pointer;
}
.header-actions .button { flex: 1 1 auto; }
.button-primary { background: var(--accent); color: white; }
.button-primary:hover:not(:disabled) { background: #064867; }
.button-secondary { background: #fff; border-color: #b9c9db; color: #233752; }
.button-secondary:hover:not(:disabled) { background: #edf4fa; }
.button:disabled { cursor: not-allowed; opacity: 0.6; }
.link-btn { background: none; border: 0; color: var(--accent); text-decoration: underline; cursor: pointer; padding: 0; font: inherit; }

.alert { margin: 0; padding: 0.75rem 0.85rem; border-radius: 8px; font-size: 0.875rem; line-height: 1.45; overflow-wrap: anywhere; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-warning { background: #fffbeb; color: #85400b; border: 1px solid #fef3c7; }
.empty-state, .loading-state { padding: 2.5rem 0.5rem; text-align: center; color: var(--muted); }

.modal-card {
  box-sizing: border-box;
  width: min(600px, calc(100% - 1.5rem));
  max-height: calc(100dvh - 1.5rem);
  margin: auto;
  padding: 0;
  border: 1px solid var(--line);
  border-radius: 14px;
  overflow: auto;
  overscroll-behavior: contain;
  background: #fff;
  color: var(--ink);
  box-shadow: 0 20px 70px #10213d35;
}
.modal-card--sm { width: min(480px, calc(100% - 1.5rem)); }
.modal-card::backdrop { background: #0b1b34a6; overflow: hidden; overscroll-behavior: contain; }
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem clamp(0.85rem, 4vw, 1.25rem);
  border-bottom: 1px solid var(--line);
  background: #f7fafd;
}
.modal-header h2 { margin: 0; font-size: 1.12rem; line-height: 1.35; overflow-wrap: anywhere; }
.close-btn {
  flex: 0 0 44px;
  min-height: 44px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #fff;
  color: var(--ink);
  font: inherit;
  cursor: pointer;
}
.modal-form { display: grid; gap: 1rem; padding: clamp(0.85rem, 4vw, 1.25rem); }
.form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 210px), 1fr)); gap: 0.85rem; }
.form-field b { color: #b91c1c; }
.form-field--wide { grid-column: 1 / -1; }
.form-hint { margin: 0; font-size: 0.8rem; color: var(--muted); }
.confirm-box { background: #f1f6fb; border: 1px solid var(--line); border-radius: 8px; padding: 0.7rem; }
.checkbox-label { display: flex; align-items: flex-start; gap: 0.65rem; cursor: pointer; line-height: 1.45; overflow-wrap: anywhere; }
.checkbox-label input { flex: 0 0 auto; inline-size: 1.15rem; block-size: 1.15rem; margin: 0.1rem 0 0; accent-color: var(--accent); }
.text-warning { color: #85400b; }
.modal-actions { justify-content: flex-end; border-top: 1px solid var(--line); padding-top: 0.85rem; }
.modal-actions .button { flex: 0 1 auto; }

@container rooms (min-width: 34rem) {
  .summary-strip { grid-template-columns: repeat(4, minmax(0, 1fr)); }
  .filters-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .header-actions .button { flex: 0 1 auto; }
}
@container rooms (min-width: 54rem) {
  .page-header { grid-template-columns: minmax(0, 1fr) auto; align-items: center; }
  .header-actions { justify-content: flex-end; }
  .filters-grid { grid-template-columns: minmax(0, 1.45fr) repeat(2, minmax(0, 1fr)) auto; }
}
</style>
