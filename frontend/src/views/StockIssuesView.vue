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
    <div class="header-banner">
      <div class="header-icon">📉</div>
      <div class="header-titles">
        <h1>Vật tư tiêu hao theo ca khám</h1>
        <p class="subtitle">Ghi nhận lượng vật tư y tế thực tế sử dụng cho ca điều trị đã kết thúc và xác nhận trừ tồn kho.</p>
      </div>
      <div class="header-actions">
        <RouterLink class="btn btn-outline" to="/management/inventory/receipts">📦 Xem tồn & Nhập kho</RouterLink>
      </div>
    </div>

    <div class="rules-card">
      <div class="rule-item">
        <span class="rule-icon">💡</span>
        <span>Hệ thống <strong>chỉ trừ tồn kho thực tế</strong> sau khi người dùng bấm xác nhận tiêu hao theo ca. Chưa áp dụng định mức tự động.</span>
      </div>
    </div>

    <!-- Step 1: Select Finished Visit -->
    <div class="card visit-selection-card">
      <div class="card-header">
        <h2>1. Chọn ca khám đã hoàn tất</h2>
      </div>
      <form class="search-form" @submit.prevent="loadTargets()">
        <div class="form-group flex-1">
          <label for="visit-filter-input">Mã ca khám (để trống để xem các ca gần nhất)</label>
          <div class="input-with-icon">
            <span class="input-icon">🔍</span>
            <input
              id="visit-filter-input"
              v-model.number="visitFilter"
              type="number"
              min="1"
              max="2147483647"
              step="1"
              placeholder="Nhập mã lượt khám..."
              :disabled="locked"
            />
          </div>
        </div>
        <button class="btn btn-primary" :disabled="locked || targetBusy">
          <span v-if="targetBusy">Đang tìm…</span>
          <span v-else>Tìm ca khám</span>
        </button>
      </form>

      <div v-if="targetError" class="alert-box alert-error" role="alert">⚠️ {{ targetError }}</div>
      <div v-if="targetBusy" class="loading-state" role="status">
        <div class="spinner"></div>
        <p>Đang tải danh sách ca khám…</p>
      </div>

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
          <div class="target-badge">Ca #{{ target.visitId }}</div>
          <div class="target-patient"><strong>{{ target.patientName }}</strong> ({{ target.patientCode }})</div>
          <div class="target-time">🕒 Kết thúc: {{ date(target.endedAt) }}</div>
        </button>
      </div>

      <div v-if="!targetBusy && targets.length === 0" class="empty-state">
        <p>Không có ca khám hoàn tất nào phù hợp với bộ lọc.</p>
      </div>

      <div v-if="targetMore" class="load-more-wrap">
        <button class="btn btn-outline" :disabled="locked || targetBusy" @click="loadTargets(true)">Xem ca cũ hơn ↓</button>
      </div>
    </div>
    <template v-if="visitId > 0">
      <div class="card active-visit-banner">
        <div class="active-visit-header">
          <div>
            <span class="badge-visit">Ca khám đang chọn</span>
            <h2>Ca #{{ visitId }} <span v-if="selected?.visitId === visitId">— {{ selected.patientName }} ({{ selected.patientCode }})</span></h2>
          </div>
        </div>

        <div class="history-subcard">
          <div class="subcard-header">
            <h3>📜 Tiêu hao đã xác nhận của ca này</h3>
            <button class="btn btn-outline-sm" :disabled="historyBusy" @click="loadHistory()">🔄 Tải lại lịch sử</button>
          </div>
          <p class="history-hint">Đối chiếu danh sách dưới đây để tránh ghi trùng lượng vật tư đã trừ tồn kho trước đó.</p>

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
                    • <strong>{{ line.code }}</strong> — {{ line.name }}: <strong>{{ line.quantity }}</strong> {{ line.unit }}
                  </li>
                </ul>
              </div>
            </details>
          </div>

          <p v-if="!historyBusy && !historyError && history.length === 0" class="empty-history-text">Ca này chưa có phiếu tiêu hao nào.</p>
          <div v-if="historyMore" class="load-more-wrap">
            <button class="btn btn-outline-sm" :disabled="historyBusy" @click="loadHistory(true)">Xem phiếu cũ hơn ↓</button>
          </div>
        </div>
      </div>
      <div class="card table-card">
        <div class="card-header">
          <h2>2. Chọn vật tư y tế thực tế</h2>
        </div>
        <form class="search-form" @submit.prevent="loadStock()">
          <div class="form-group flex-1">
            <div class="input-with-icon">
              <span class="input-icon">🔍</span>
              <input v-model="keyword" placeholder="Tìm theo mã hoặc tên vật tư y tế..." maxlength="200" />
            </div>
          </div>
          <button class="btn btn-primary" :disabled="stockBusy">Tìm vật tư</button>
        </form>

        <div v-if="stockError" class="alert-box alert-error" role="alert">⚠️ {{ stockError }}</div>
        <div v-if="stockBusy" class="loading-state-sm" role="status">Đang tải danh sách tồn kho…</div>

        <div class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Vật tư y tế</th>
                <th scope="col">Đơn vị</th>
                <th scope="col" class="text-right">Tồn hiện tại</th>
                <th scope="col">Trạng thái</th>
                <th scope="col" class="text-center">Thao tác</th>
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
                <td>
                  <span v-if="!item.isActive" class="badge badge-disabled">Ngừng dùng</span>
                  <span v-else-if="item.onHand <= item.minimumStockThreshold" class="badge badge-warning">⚠️ Tồn thấp</span>
                  <span v-else class="badge badge-success">🟢 Đang dùng</span>
                </td>
                <td class="text-center">
                  <button
                    type="button"
                    class="btn btn-action-sm"
                    :disabled="locked || !item.isActive || item.onHand <= 0 || lines.length >= 50 || lines.some(x => x.medicalSupplyId === item.medicalSupplyId)"
                    :aria-label="`Chọn ${item.name}`"
                    @click="add(item)"
                  >
                    + Chọn vật tư
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
            <button class="btn btn-outline" :disabled="stockBusy || page <= 1" @click="loadStock(page - 1)">← Trang trước</button>
            <button class="btn btn-outline" :disabled="stockBusy || !stockMore" @click="loadStock(page + 1)">Trang sau →</button>
          </div>
        </div>
      </div>
      <div class="card form-card">
        <div class="card-header">
          <h2>3. Kiểm tra danh sách & Xác nhận trừ tồn</h2>
        </div>

        <div v-if="store.pending" class="alert-box alert-warning" role="status">
          ⏳ Phiếu cho ca #{{ store.pending.visitId }} đang chờ xác định kết quả. Vui lòng bấm <strong>Gửi lại phiếu</strong>.
        </div>
        <div v-if="store.saved" class="alert-box alert-success" role="status">
          ✅ Đã xác nhận thành công phiếu <strong>{{ store.saved.code }}</strong> cho ca #{{ store.saved.visitId }}.
        </div>
        <div v-if="store.error" class="alert-box alert-error" role="alert">⚠️ {{ store.error }}</div>
        <div v-if="formError" class="alert-box alert-error" role="alert">⚠️ {{ formError }}</div>

        <form class="issue-form" @submit.prevent="submit">
          <fieldset class="form-fieldset" :disabled="locked">
            <div class="lines-section">
              <h3>Vật tư tiêu hao thực tế ({{ lines.length }}/50)</h3>
              <p v-if="lines.length === 0" class="empty-lines-prompt">
                👆 Chọn vật tư từ bảng tồn kho phía trên để nhập số lượng sử dụng thực tế.
              </p>

              <div v-else class="lines-list">
                <div v-for="line in lines" :key="line.medicalSupplyId" class="issue-line-item">
                  <div class="line-meta">
                    <span class="line-name">{{ line.name }}</span>
                    <span class="line-code">({{ line.code }}) · ĐVT: <strong>{{ line.unit }}</strong> · Tồn kho: {{ line.onHand }}</span>
                  </div>
                  <div class="line-qty-wrap">
                    <label :for="`qty-${line.medicalSupplyId}`">Số thực tế:</label>
                    <input
                      :id="`qty-${line.medicalSupplyId}`"
                      v-model.number="line.quantity"
                      type="number"
                      min="1"
                      :max="line.onHand"
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

            <div class="form-group">
              <label for="issue-notes-input">Ghi chú lâm sàng / tiêu hao</label>
              <textarea
                id="issue-notes-input"
                v-model="notes"
                placeholder="Ghi chú chi tiết sử dụng vật tư cho ca điều trị..."
                maxlength="500"
                @input="confirmed = false"
              ></textarea>
            </div>

            <label class="confirm-checkbox">
              <input v-model="confirmed" type="checkbox" required />
              <span>Tôi đã đối chiếu đúng ca điều trị, đơn vị tính và số lượng thực tế. Xác nhận sẽ chính thức <strong>trừ tồn kho và lưu phiếu</strong>.</span>
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
      </div>
    </template>
  </section>
</template>
<style scoped>
.issue-page { max-width: 1100px; margin: 0 auto; padding: 1.5rem; color: #1e293b; }
.header-banner { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles { flex: 1; min-width: 260px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }
.header-actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
.rules-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem 1rem; margin-bottom: 1.25rem; display: flex; gap: 0.5rem; }
.rule-item { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; }
.rule-icon { font-size: 1.1rem; flex-shrink: 0; }
.card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; margin-bottom: 1.5rem; padding: 1.25rem; }
.card-header h2 { margin: 0 0 1rem; font-size: 1.2rem; color: #0f172a; }
.search-form { display: flex; gap: 1rem; align-items: flex-end; margin-bottom: 1rem; }
.flex-1 { flex: 1; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.input-with-icon { position: relative; display: flex; align-items: center; }
.input-icon { position: absolute; left: 0.75rem; font-size: 0.9rem; color: #94a3b8; pointer-events: none; }
.input-with-icon input { width: 100%; height: 44px; padding: 0.5rem 0.75rem 0.5rem 2.25rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; }
.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; text-decoration: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-action-sm { height: 36px; padding: 0 0.85rem; border-radius: 6px; background: #f0f9ff; color: #0284c7; border: 1px solid #bae6fd; font-size: 0.85rem; }
.btn-outline-sm { height: 36px; padding: 0 0.85rem; border-radius: 6px; background: #fff; color: #334155; border: 1px solid #cbd5e1; font-size: 0.85rem; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.targets-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 0.75rem; margin: 1rem 0; }
.target-card-btn { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.85rem 1rem; text-align: left; cursor: pointer; }
.target-card-btn.active { background: #e0f2fe; border-color: #0284c7; outline: 2px solid #0284c7; }
.target-badge { font-weight: 700; color: #0284c7; font-size: 0.9rem; }
.target-patient { margin: 0.25rem 0; font-size: 0.95rem; color: #0f172a; }
.target-time { font-size: 0.8rem; color: #64748b; }
.empty-state { text-align: center; padding: 2rem; color: #64748b; }
.load-more-wrap { display: flex; justify-content: center; margin-top: 1rem; }
.active-visit-banner { border-left: 5px solid #0284c7; }
.badge-visit { font-size: 0.75rem; text-transform: uppercase; font-weight: 700; color: #0284c7; }
.active-visit-header h2 { margin: 0.25rem 0 1rem; font-size: 1.3rem; }
.history-subcard { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; }
.subcard-header { display: flex; justify-content: space-between; align-items: center; }
.history-hint { font-size: 0.825rem; color: #64748b; margin: 0.25rem 0 0.75rem; }
.history-item { border: 1px solid #e2e8f0; border-radius: 6px; background: #fff; margin-bottom: 0.5rem; }
.history-summary { padding: 0.65rem 0.85rem; cursor: pointer; display: flex; gap: 1rem; font-size: 0.875rem; }
.entry-code { font-weight: 700; color: #0284c7; }
.history-body { padding: 0.75rem 0.85rem; border-top: 1px solid #e2e8f0; font-size: 0.85rem; }
.table-wrap { overflow-x: auto; margin-bottom: 1rem; }
.data-table { width: 100%; border-collapse: collapse; text-align: left; }
.data-table th { background: #f8fafc; padding: 0.75rem 1rem; font-size: 0.825rem; font-weight: 700; color: #475569; border-bottom: 2px solid #e2e8f0; }
.data-table td { padding: 0.75rem 1rem; border-bottom: 1px solid #f1f5f9; font-size: 0.9rem; }
.badge { display: inline-block; padding: 0.25rem 0.6rem; border-radius: 9999px; font-size: 0.75rem; font-weight: 600; }
.badge-success { background: #dcfce7; color: #15803d; }
.badge-warning { background: #fef3c7; color: #b45309; }
.badge-disabled { background: #f1f5f9; color: #64748b; }
.pagination-bar { display: flex; justify-content: space-between; align-items: center; padding-top: 0.75rem; border-top: 1px solid #f1f5f9; }
.form-fieldset { border: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 1rem; }
.lines-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; }
.issue-line-item { display: flex; align-items: center; justify-content: space-between; background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.65rem 1rem; gap: 0.75rem; margin-bottom: 0.5rem; }
.qty-input { width: 90px; height: 38px; border: 1px solid #cbd5e1; border-radius: 6px; padding: 0.25rem 0.5rem; text-align: right; }
.btn-remove { background: #fee2e2; border: 1px solid #fecaca; color: #991b1b; height: 38px; padding: 0 0.75rem; border-radius: 6px; cursor: pointer; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; }
.btn-submit { width: 100%; height: 48px; margin-top: 1rem; font-size: 1.05rem; }
@media (max-width: 640px) { .issue-page { padding: 1rem; } .search-form { flex-direction: column; } .btn { width: 100%; } }
</style>
