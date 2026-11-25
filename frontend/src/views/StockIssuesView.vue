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
const activeLeftTab = ref<'stock' | 'history'>('stock');
const visitPickerCollapsed = ref(false);

const locked = computed(() => store.busy || !!store.pending);
const visitId = computed(() => store.pending?.visitId ?? selected.value?.visitId ?? store.saved?.visitId ?? 0);
const totalIssueUnits = computed(() => lines.value.reduce((sum, x) => sum + (Number.isFinite(x.quantity) ? x.quantity : 0), 0));

function isAdded(id: number) {
  return lines.value.some(x => x.medicalSupplyId === id);
}

let targetGeneration = 0, stockGeneration = 0, historyGeneration = 0, identityGeneration = 0, active = true;
function clearIdentity() {
  targetGeneration++; stockGeneration++; historyGeneration++;
  targets.value = []; items.value = []; history.value = []; selected.value = null; lines.value = [];
  notes.value = ''; confirmed.value = false;
  targetBusy.value = false; stockBusy.value = false; historyBusy.value = false;
  targetMore.value = false; stockMore.value = false; historyMore.value = false;
  targetError.value = ''; stockError.value = ''; historyError.value = ''; formError.value = '';
  activeLeftTab.value = 'stock'; visitPickerCollapsed.value = false;
}
watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { identityGeneration++; clearIdentity(); });
onUnmounted(() => { active = false; targetGeneration++; stockGeneration++; historyGeneration++; });

async function loadTargets(more = false) {
  if (locked.value || more && targetBusy.value) return;
  if (visitFilter.value !== '' && (!Number.isInteger(visitFilter.value) || visitFilter.value <= 0 || visitFilter.value > 2147483647)) {
    targetError.value = 'Mã ca phải là số nguyên dương.';
    return;
  }
  const current = ++targetGeneration; targetBusy.value = true; targetError.value = '';
  try {
    const result = await stockIssuesService.targets(visitFilter.value === '' ? undefined : visitFilter.value, more ? targets.value.at(-1)?.visitId ?? 0 : 0);
    if (current !== targetGeneration) return;
    targets.value = more ? [...targets.value, ...result.items.filter(x => !targets.value.some(t => t.visitId === x.visitId))] : result.items;
    targetMore.value = result.hasMore;
  } catch {
    if (current === targetGeneration) targetError.value = 'Không tải được danh sách ca hoàn tất.';
  } finally {
    if (current === targetGeneration) targetBusy.value = false;
  }
}

async function loadStock(next = 1) {
  const current = ++stockGeneration; stockBusy.value = true; stockError.value = '';
  try {
    const result = await stockReceiptsService.stock(keyword.value, next);
    if (current !== stockGeneration) return;
    items.value = result.items; page.value = result.page; stockMore.value = result.hasNextPage;
  } catch {
    if (current === stockGeneration) stockError.value = 'Không tải được tồn kho.';
  } finally {
    if (current === stockGeneration) stockBusy.value = false;
  }
}

async function loadHistory(more = false) {
  if (visitId.value <= 0 || more && historyBusy.value) return;
  const current = ++historyGeneration, visit = visitId.value; historyBusy.value = true; historyError.value = '';
  try {
    const result = await stockIssuesService.list(visit, more ? history.value.at(-1)?.id ?? 0 : 0);
    if (current !== historyGeneration || visit !== visitId.value) return;
    history.value = more ? [...history.value, ...result.items.filter(x => !history.value.some(h => h.id === x.id))] : result.items;
    historyMore.value = result.hasMore;
  } catch {
    if (current === historyGeneration) historyError.value = 'Không tải được lịch sử tiêu hao của ca.';
  } finally {
    if (current === historyGeneration) historyBusy.value = false;
  }
}

function choose(target: StockIssueTarget) {
  if (locked.value) return;
  selected.value = target;
  lines.value = [];
  notes.value = '';
  confirmed.value = false;
  store.saved = null;
  history.value = [];
  historyMore.value = false;
  activeLeftTab.value = 'stock';
  void loadHistory();
}

function add(item: StockItem) {
  if (!locked.value && visitId.value > 0 && item.isActive && item.onHand > 0 && lines.value.length < 50 && !lines.value.some(x => x.medicalSupplyId === item.medicalSupplyId)) {
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
  const identity = identityGeneration;
  formError.value = '';
  if (!store.pending) {
    if (visitId.value <= 0 || !confirmed.value || notes.value.length > 500 || lines.value.length === 0 || lines.value.some(x => !Number.isInteger(x.quantity) || x.quantity <= 0 || x.quantity > x.onHand)) {
      formError.value = 'Chọn ca, nhập số nguyên dương không vượt tồn và xác nhận lượng thực tế.';
      return;
    }
    await store.submit({
      requestId: crypto.randomUUID(),
      confirmed: true,
      visitId: visitId.value,
      notes: notes.value.trim() || null,
      lines: lines.value.map(x => ({ medicalSupplyId: x.medicalSupplyId, quantity: x.quantity, expectedSupplyVersion: x.supplyVersion }))
    });
  } else {
    await store.submit();
  }
  if (!active || identity !== identityGeneration) return;
  if (store.saved) {
    lines.value = []; notes.value = ''; confirmed.value = false;
    await Promise.all([loadStock(page.value), loadHistory()]);
  }
}

function date(value: string | null) {
  return value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa ghi thời điểm';
}

onMounted(() => { void loadTargets(); void loadStock(); if (visitId.value > 0) void loadHistory(); });
</script>

<template>
  <section class="issue-page">
    <header class="page-topbar">
      <div class="topbar-main">
        <div class="header-icon" aria-hidden="true">📉</div>
        <div>
          <h1>Vật tư tiêu hao theo ca khám</h1>
          <p class="subtitle">Chỉ trừ tồn kho thực tế sau khi bấm xác nhận; chưa áp dụng định mức tự động.</p>
        </div>
      </div>

      <div class="topbar-right">
        <span v-if="visitId > 0" class="kpi-pill kpi-pill--accent">
          Ca #{{ visitId }}<template v-if="selected?.visitId === visitId"> · {{ selected.patientName }}</template>
        </span>
        <span v-if="visitId > 0" class="kpi-pill">Đã chọn: <strong>{{ lines.length }}/50</strong></span>
      </div>
    </header>

    <!-- Strip 1: Chọn ca khám hoàn tất gọn nhẹ -->
    <div class="card visit-strip-card">
      <div class="visit-strip-head">
        <div class="visit-strip-title">
          <h2>1. Chọn ca khám hoàn tất</h2>
          <span v-if="selected && visitPickerCollapsed" class="selected-inline">
            Đang chọn: <strong>Ca #{{ selected.visitId }} — {{ selected.patientName }} ({{ selected.patientCode }})</strong>
          </span>
        </div>

        <div class="visit-strip-controls">
          <form class="inline-search" @submit.prevent="loadTargets()">
            <div class="input-with-icon">
              <span class="input-icon" aria-hidden="true">🔍</span>
              <input
                id="visit-filter-input"
                v-model.number="visitFilter"
                type="number"
                min="1"
                max="2147483647"
                step="1"
                placeholder="Mã ca..."
                aria-label="Lọc theo mã ca khám"
                :disabled="locked"
              />
            </div>
            <button class="btn btn-primary btn-sm" :disabled="locked || targetBusy">{{ targetBusy ? 'Đang tìm…' : 'Tìm ca' }}</button>
          </form>
          <button
            v-if="visitId > 0"
            type="button"
            class="btn btn-outline-sm"
            @click="visitPickerCollapsed = !visitPickerCollapsed"
          >
            {{ visitPickerCollapsed ? 'Đổi ca khám ▼' : 'Thu gọn ▲' }}
          </button>
        </div>
      </div>

      <div v-if="targetError" class="alert-box alert-error" role="alert">⚠️ {{ targetError }}</div>

      <template v-if="!visitPickerCollapsed || visitId <= 0">
        <div v-if="targetBusy" class="loading-state-sm" role="status">Đang tải danh sách ca khám…</div>
        <div v-else class="targets-grid">
          <button
            v-for="target in targets"
            :key="target.visitId"
            type="button"
            class="target-card-btn"
            :class="{ active: selected?.visitId === target.visitId }"
            :disabled="locked"
            :aria-pressed="selected?.visitId === target.visitId"
            @click="choose(target)"
          >
            <div class="target-top">
              <span class="target-badge">Ca #{{ target.visitId }}</span>
              <span class="target-code">{{ target.patientCode }}</span>
            </div>
            <div class="target-patient">{{ target.patientName }}</div>
            <div class="target-time">🕒 {{ date(target.endedAt) }}</div>
          </button>
        </div>

        <p v-if="!targetBusy && targets.length === 0" class="empty-inline">Không có ca khám hoàn tất nào phù hợp với bộ lọc.</p>

        <div v-if="targetMore" class="load-more-wrap">
          <button class="btn btn-outline-sm" :disabled="locked || targetBusy" @click="loadTargets(true)">Xem ca cũ hơn ↓</button>
        </div>
      </template>
    </div>

    <!-- Hướng dẫn khi chưa chọn ca -->
    <div v-if="visitId <= 0" class="card empty-guide-card" role="status">
      <strong>Chọn một ca khám ở danh sách phía trên</strong> để xem lịch sử vật tư đã trừ của ca đó và lập phiếu tiêu hao thực tế.
    </div>

    <!-- Workspace 2 cột khi đã chọn ca -->
    <div v-else class="workspace-grid">
      <!-- Cột trái: Vật tư & Lịch sử tiêu hao ca -->
      <div class="card workspace-main">
        <div class="active-visit-strip">
          <div>
            <strong>Ca #{{ visitId }}</strong>
            <span v-if="selected?.visitId === visitId"> — {{ selected.patientName }} ({{ selected.patientCode }})</span>
          </div>
          <span class="history-count-badge">Đã có {{ history.length }} phiếu tiêu hao</span>
        </div>
        <p class="history-hint">Mỗi phiếu mới chỉ ghi phần bổ sung; hãy đối chiếu lịch sử tiêu hao của ca để tránh ghi trùng lượng đã trừ tồn kho trước đó.</p>

        <div class="tab-bar" role="tablist" aria-label="Chọn vật tư hoặc xem lịch sử tiêu hao">
          <button
            type="button"
            role="tab"
            :aria-selected="activeLeftTab === 'stock'"
            class="tab-btn"
            :class="{ active: activeLeftTab === 'stock' }"
            @click="activeLeftTab = 'stock'"
          >
            2. Chọn vật tư tiêu hao
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="activeLeftTab === 'history'"
            class="tab-btn"
            :class="{ active: activeLeftTab === 'history' }"
            @click="activeLeftTab = 'history'"
          >
            Lịch sử tiêu hao ca này ({{ history.length }})
          </button>
        </div>

        <template v-if="activeLeftTab === 'stock'">
          <div class="main-toolbar">
            <form class="inline-search full-search" @submit.prevent="loadStock()">
              <div class="input-with-icon flex-1">
                <span class="input-icon" aria-hidden="true">🔍</span>
                <input v-model="keyword" placeholder="Tìm theo mã hoặc tên vật tư y tế..." maxlength="200" aria-label="Tìm vật tư tiêu hao" />
              </div>
              <button class="btn btn-primary btn-sm" :disabled="stockBusy">{{ stockBusy ? 'Đang tìm…' : 'Tìm vật tư' }}</button>
              <button type="button" class="btn btn-outline-sm" :disabled="stockBusy" @click="loadStock(page)">↻ Tải lại</button>
            </form>
          </div>

          <div v-if="stockError" class="alert-box alert-error" role="alert">⚠️ {{ stockError }}</div>
          <div v-if="stockBusy" class="loading-state-sm" role="status">Đang tải danh sách tồn kho…</div>

          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Vật tư y tế</th>
                  <th scope="col">ĐVT</th>
                  <th scope="col" class="text-right">Tồn</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col" class="text-center">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in items" :key="item.medicalSupplyId" :class="{ 'row-selected': isAdded(item.medicalSupplyId) }">
                  <td>
                    <div class="item-name">{{ item.name }}</div>
                    <div class="item-code">{{ item.code }}</div>
                  </td>
                  <td><span class="unit-tag">{{ item.unit }}</span></td>
                  <td class="text-right font-bold">{{ item.onHand.toLocaleString('vi-VN') }}</td>
                  <td>
                    <span v-if="!item.isActive" class="badge badge-disabled">Ngừng dùng</span>
                    <span v-else-if="item.onHand <= item.minimumStockThreshold" class="badge badge-warning">⚠️ Tồn thấp</span>
                    <span v-else class="badge badge-success">🟢 Đang dùng</span>
                  </td>
                  <td class="text-center">
                    <button
                      type="button"
                      class="btn btn-action-sm"
                      :class="{ 'btn-action-done': isAdded(item.medicalSupplyId) }"
                      :disabled="locked || !item.isActive || item.onHand <= 0 || lines.length >= 50 || isAdded(item.medicalSupplyId)"
                      :aria-label="`Chọn ${item.name}`"
                      @click="add(item)"
                    >
                      {{ isAdded(item.medicalSupplyId) ? '✓ Đã chọn' : '+ Chọn' }}
                    </button>
                  </td>
                </tr>
                <tr v-if="!stockBusy && items.length === 0">
                  <td colspan="5" class="empty-cell">Không tìm thấy vật tư y tế phù hợp.</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="pagination-bar">
            <span class="page-info">Trang <strong>{{ page }}</strong></span>
            <div class="pagination-controls">
              <button class="btn btn-outline-sm" :disabled="stockBusy || page <= 1" @click="loadStock(page - 1)">← Trang trước</button>
              <button class="btn btn-outline-sm" :disabled="stockBusy || !stockMore" @click="loadStock(page + 1)">Trang sau →</button>
            </div>
          </div>
        </template>

        <template v-else>
          <div class="main-toolbar">
            <h2>📜 Tiêu hao đã xác nhận của ca #{{ visitId }}</h2>
            <button class="btn btn-outline-sm" :disabled="historyBusy" @click="loadHistory()">🔄 Tải lại lịch sử</button>
          </div>

          <div v-if="historyError" class="alert-box alert-error" role="alert">⚠️ {{ historyError }}</div>
          <div v-if="historyBusy" class="loading-state-sm" role="status">Đang đối chiếu lịch sử…</div>

          <div v-else class="history-list">
            <details v-for="entry in history" :key="entry.id" class="history-item">
              <summary class="history-summary">
                <span class="entry-code">{{ entry.code }}</span>
                <span class="entry-date">🕒 {{ date(entry.confirmedAt) }}</span>
                <span class="entry-actor">👤 {{ entry.confirmedByName }}</span>
              </summary>
              <div class="history-body">
                <p v-if="entry.notes" class="entry-notes">Ghi chú: <em>{{ entry.notes }}</em></p>
                <ul class="entry-lines">
                  <li v-for="line in entry.lines" :key="line.medicalSupplyId">
                    <span><strong>{{ line.code }}</strong> — {{ line.name }}:</span>
                    <span><strong>{{ line.quantity }}</strong> {{ line.unit }}</span>
                  </li>
                </ul>
              </div>
            </details>
          </div>

          <p v-if="!historyBusy && !historyError && history.length === 0" class="empty-inline">Ca này chưa có phiếu tiêu hao nào.</p>
          <div v-if="historyMore" class="load-more-wrap">
            <button class="btn btn-outline-sm" :disabled="historyBusy" @click="loadHistory(true)">Xem phiếu cũ hơn ↓</button>
          </div>
        </template>
      </div>

      <!-- Cột phải sticky: Kiểm tra & Xác nhận trừ tồn -->
      <aside class="card workspace-side" aria-label="Biểu mẫu xác nhận tiêu hao vật tư">
        <div class="side-header">
          <h2>3. Danh sách tiêu hao &amp; Xác nhận</h2>
          <span class="draft-counter">{{ lines.length }}/50 dòng · {{ totalIssueUnits.toLocaleString('vi-VN') }} ĐV</span>
        </div>

        <div v-if="store.pending" class="alert-box alert-warning" role="status">
          ⏳ Phiếu cho ca #{{ store.pending.visitId }} đang chờ xác định kết quả. Bấm <strong>Gửi lại phiếu tiêu hao</strong>.
        </div>
        <div v-if="store.saved" class="alert-box alert-success" role="status">
          ✅ Đã xác nhận phiếu <strong>{{ store.saved.code }}</strong> cho ca #{{ store.saved.visitId }}.
        </div>
        <div v-if="store.error" class="alert-box alert-error" role="alert">⚠️ {{ store.error }}</div>
        <div v-if="formError" class="alert-box alert-error" role="alert">⚠️ {{ formError }}</div>

        <form class="issue-form" @submit.prevent="submit">
          <fieldset class="form-fieldset" :disabled="locked">
            <div class="lines-section">
              <div class="lines-head">
                <h3>Vật tư tiêu hao thực tế ({{ lines.length }}/50)</h3>
                <button v-if="lines.length > 0" type="button" class="link-reset" @click="refreshSelection">Xóa danh sách</button>
              </div>

              <p v-if="lines.length === 0" class="empty-lines-prompt">
                👈 Chọn vật tư từ bảng bên trái để nhập số lượng sử dụng thực tế.
              </p>

              <div v-else class="lines-list-scroll">
                <div v-for="line in lines" :key="line.medicalSupplyId" class="issue-line-item">
                  <div class="line-meta">
                    <span class="line-name">{{ line.name }}</span>
                    <span class="line-code">{{ line.code }} · {{ line.unit }} · Tồn: <strong>{{ line.onHand }}</strong></span>
                  </div>
                  <div class="line-controls">
                    <input
                      :id="`qty-${line.medicalSupplyId}`"
                      v-model.number="line.quantity"
                      type="number"
                      min="1"
                      :max="line.onHand"
                      step="1"
                      required
                      class="qty-input"
                      :aria-label="`Số thực tế ${line.name}`"
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

            <div class="form-group">
              <label for="issue-notes-input">Ghi chú lâm sàng / tiêu hao</label>
              <textarea
                id="issue-notes-input"
                v-model="notes"
                rows="2"
                placeholder="Ghi chú chi tiết sử dụng vật tư cho ca điều trị..."
                maxlength="500"
                @input="confirmed = false"
              ></textarea>
            </div>

            <label class="confirm-checkbox">
              <input v-model="confirmed" type="checkbox" required />
              <span>Tôi đã đối chiếu ca điều trị, đơn vị tính và số lượng thực tế. Xác nhận sẽ chính thức <strong>trừ tồn kho và lưu phiếu</strong>.</span>
            </label>
          </fieldset>

          <button
            type="submit"
            class="btn btn-primary btn-submit"
            :disabled="store.busy || (!store.pending && (!confirmed || lines.length === 0))"
          >
            {{ store.busy ? 'Đang gửi xử lý…' : store.pending ? '🔄 Gửi lại phiếu tiêu hao' : '✅ Xác nhận tiêu hao ca khám' }}
          </button>
        </form>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.issue-page {
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

.kpi-pill--accent {
  background: #e0f2fe;
  border-color: #bae6fd;
  color: #0369a1;
}

.card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1rem;
  min-width: 0;
}

.visit-strip-card {
  margin-bottom: 1rem;
}

.visit-strip-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-bottom: 0.65rem;
}

.visit-strip-title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.65rem;
}

.visit-strip-title h2 {
  margin: 0;
  font-size: 1.02rem;
  color: #0f172a;
}

.selected-inline {
  font-size: 0.85rem;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
}

.visit-strip-controls {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.45rem;
}

.inline-search {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.full-search {
  width: 100%;
}

.flex-1 { flex: 1; min-width: 200px; }

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
  width: 100%;
  min-width: 140px;
  height: 36px;
  padding: 0.35rem 0.65rem 0.35rem 2rem;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  font-size: 0.88rem;
}

.targets-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.55rem;
  max-height: 158px;
  overflow-y: auto;
  padding: 0.15rem;
}

.target-card-btn {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.55rem 0.7rem;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.target-card-btn:hover:not(:disabled) {
  border-color: #38bdf8;
  background: #f0f9ff;
}

.target-card-btn.active {
  background: #e0f2fe;
  border-color: #0284c7;
  box-shadow: inset 0 0 0 1px #0284c7;
}

.target-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.4rem;
}

.target-badge { font-weight: 700; color: #0284c7; font-size: 0.82rem; }
.target-code { font-size: 0.74rem; color: #64748b; }
.target-patient { margin: 0.15rem 0; font-size: 0.88rem; font-weight: 600; color: #0f172a; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.target-time { font-size: 0.74rem; color: #64748b; }

.empty-guide-card {
  text-align: center;
  padding: 1.5rem;
  color: #475569;
  font-size: 0.92rem;
  background: #f8fafc;
}

.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(350px, 440px);
  gap: 1rem;
  align-items: start;
}

.workspace-side {
  position: sticky;
  top: 0.85rem;
  border-color: #cbd5e1;
  box-shadow: 0 4px 16px rgba(15, 23, 42, 0.05);
}

.active-visit-strip {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  font-size: 0.9rem;
  color: #0c4a6e;
  margin-bottom: 0.4rem;
}

.history-count-badge {
  font-size: 0.76rem;
  font-weight: 700;
  background: #fff;
  color: #0369a1;
  padding: 0.16rem 0.5rem;
  border-radius: 999px;
  border: 1px solid #bae6fd;
}

.history-hint {
  font-size: 0.79rem;
  color: #64748b;
  margin: 0 0 0.7rem;
}

.tab-bar {
  display: flex;
  gap: 0.4rem;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 0.6rem;
  margin-bottom: 0.75rem;
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
  gap: 0.5rem;
  margin-bottom: 0.7rem;
}

.main-toolbar h2 {
  margin: 0;
  font-size: 1.02rem;
  color: #0f172a;
}

.table-wrap { overflow-x: auto; margin-bottom: 0.65rem; }
.data-table { width: 100%; border-collapse: collapse; text-align: left; }
.data-table th { background: #f8fafc; padding: 0.5rem 0.65rem; font-size: 0.78rem; font-weight: 700; color: #475569; border-bottom: 1.5px solid #e2e8f0; white-space: nowrap; }
.data-table td { padding: 0.5rem 0.65rem; border-bottom: 1px solid #f1f5f9; font-size: 0.86rem; vertical-align: middle; }
.row-selected { background: #f0f9ff; }
.item-name { font-weight: 600; color: #0f172a; }
.item-code { font-size: 0.76rem; color: #64748b; }
.unit-tag { display: inline-block; padding: 0.12rem 0.45rem; border-radius: 6px; background: #f1f5f9; font-size: 0.78rem; color: #334155; }
.badge { display: inline-block; padding: 0.18rem 0.5rem; border-radius: 9999px; font-size: 0.74rem; font-weight: 600; white-space: nowrap; }
.badge-success { background: #dcfce7; color: #15803d; }
.badge-warning { background: #fef3c7; color: #b45309; }
.badge-disabled { background: #f1f5f9; color: #64748b; }
.text-right { text-align: right; }
.text-center { text-align: center; }
.font-bold { font-weight: 700; }
.empty-cell { text-align: center; padding: 1.5rem !important; color: #64748b; }

.btn { height: 38px; padding: 0 0.9rem; border-radius: 7px; font-weight: 600; font-size: 0.86rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; text-decoration: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-sm { height: 36px; padding: 0 0.85rem; }
.btn-outline-sm { height: 36px; padding: 0 0.75rem; border-radius: 7px; background: #fff; color: #334155; border: 1px solid #cbd5e1; font-size: 0.82rem; font-weight: 600; cursor: pointer; display: inline-flex; align-items: center; }
.btn-action-sm { height: 30px; padding: 0 0.65rem; border-radius: 6px; background: #f0f9ff; color: #0284c7; border: 1px solid #bae6fd; font-size: 0.8rem; font-weight: 600; white-space: nowrap; }
.btn-action-done { background: #dcfce7; color: #15803d; border-color: #bbf7d0; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }

.pagination-bar { display: flex; justify-content: space-between; align-items: center; padding-top: 0.5rem; }
.pagination-controls { display: flex; gap: 0.4rem; }
.page-info { font-size: 0.84rem; color: #475569; }

.history-list { display: flex; flex-direction: column; gap: 0.45rem; }
.history-item { border: 1px solid #e2e8f0; border-radius: 8px; background: #fff; overflow: hidden; }
.history-summary { padding: 0.6rem 0.8rem; background: #f8fafc; cursor: pointer; display: flex; gap: 0.75rem; flex-wrap: wrap; font-size: 0.85rem; }
.entry-code { font-weight: 700; color: #0284c7; }
.history-body { padding: 0.7rem 0.85rem; border-top: 1px solid #e2e8f0; font-size: 0.84rem; }
.entry-notes { margin: 0 0 0.4rem; color: #475569; }
.entry-lines { margin: 0; padding-left: 1rem; display: flex; flex-direction: column; gap: 0.2rem; }
.entry-lines li { display: flex; justify-content: space-between; gap: 0.5rem; }
.empty-inline { text-align: center; color: #64748b; font-size: 0.86rem; padding: 1rem 0; margin: 0; }
.load-more-wrap { display: flex; justify-content: center; margin-top: 0.65rem; }

.side-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  padding-bottom: 0.6rem;
  border-bottom: 1px solid #f1f5f9;
}

.side-header h2 { margin: 0; font-size: 1.05rem; color: #0f172a; }
.draft-counter { font-size: 0.78rem; font-weight: 700; color: #0369a1; background: #e0f2fe; padding: 0.2rem 0.55rem; border-radius: 999px; }

.alert-box { padding: 0.55rem 0.75rem; border-radius: 8px; font-size: 0.84rem; margin-bottom: 0.7rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }
.loading-state-sm { font-size: 0.85rem; color: #64748b; padding: 0.5rem 0; }

.form-fieldset { border: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.7rem; }
.form-group { display: flex; flex-direction: column; gap: 0.25rem; }
.form-group label { font-size: 0.8rem; font-weight: 600; color: #475569; }
.form-group textarea { padding: 0.45rem 0.65rem; border: 1px solid #cbd5e1; border-radius: 7px; font-size: 0.88rem; font-family: inherit; }

.lines-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 9px; padding: 0.75rem; }
.lines-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
.lines-head h3 { margin: 0; font-size: 0.88rem; color: #0f172a; }
.link-reset { background: none; border: none; color: #dc2626; font-size: 0.78rem; font-weight: 600; cursor: pointer; padding: 0; }
.empty-lines-prompt { margin: 0; font-size: 0.82rem; color: #64748b; text-align: center; padding: 0.75rem 0.25rem; }

.lines-list-scroll { max-height: 290px; overflow-y: auto; display: flex; flex-direction: column; gap: 0.4rem; padding-right: 0.15rem; }
.issue-line-item { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; background: #fff; border: 1px solid #e2e8f0; border-radius: 7px; padding: 0.45rem 0.6rem; }
.line-meta { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.line-name { font-size: 0.84rem; font-weight: 600; color: #0f172a; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.line-code { font-size: 0.74rem; color: #64748b; }
.line-controls { display: flex; align-items: center; gap: 0.35rem; flex-shrink: 0; }
.qty-input { width: 76px; height: 32px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0.2rem 0.4rem; text-align: right; font-size: 0.86rem; }
.btn-remove { background: #fee2e2; border: 1px solid #fecaca; color: #991b1b; width: 30px; height: 32px; border-radius: 6px; font-weight: 700; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; }

.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.45rem; font-size: 0.8rem; color: #334155; line-height: 1.35; }
.btn-submit { width: 100%; height: 42px; margin-top: 0.75rem; font-size: 0.94rem; }

@media (max-width: 1080px) {
  .workspace-grid { grid-template-columns: minmax(0, 1fr); }
  .workspace-side { position: static; }
}

@media (max-width: 640px) {
  .issue-page { padding: 0.85rem; }
  .visit-strip-controls, .inline-search { width: 100%; }
}
</style>
