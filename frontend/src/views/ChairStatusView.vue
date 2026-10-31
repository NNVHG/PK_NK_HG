<template>
  <section class="chair-status" aria-labelledby="chair-status-title">
    <h1 id="chair-status-title">Trạng thái ghế và phòng điều trị</h1>
    <p>Chọn ghế để chuyển Sẵn sàng, Bảo trì hoặc Ngừng sử dụng. Phòng hiển thị theo từng ghế.</p>
    <p>Ghế đã được nha sĩ chọn cần được nha sĩ/Admin bỏ hoặc đổi vị trí trước.
      <router-link to="/management/staff/workspace">Mở vị trí nha sĩ/phụ tá</router-link>
    </p>
    <p>Ghế Đang phục vụ chỉ được giải phóng khi kết thúc hoặc hủy ca hợp lệ.</p>
    <form class="filters" @submit.prevent="load(1)">
      <label>Tìm mã ghế hoặc phòng <input v-model="keyword" maxlength="100" :disabled="saving" /></label>
      <label>Trạng thái <select v-model.number="filterStatus" :disabled="saving">
        <option :value="0">Tất cả</option><option :value="1">Sẵn sàng</option><option :value="2">Đang phục vụ</option>
        <option :value="3">Bảo trì</option><option :value="4">Ngừng sử dụng</option>
      </select></label>
      <button :disabled="loading || saving">Tải lại danh sách</button>
    </form>
    <p v-if="loading" role="status">Đang tải trạng thái ghế…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
    <p v-if="needsReview" role="alert">Tải lại thành công và đối chiếu trạng thái trước khi xác nhận lần nữa.</p>
    <p v-if="store.result && !loading && !store.result.items.length">Không có ghế phù hợp với bộ lọc.</p>
    <ul class="chairs">
      <li v-for="chair in store.result?.items ?? []" :key="chair.id">
        <h2>{{ chair.chairNumber }} · {{ chair.roomName }}</h2>
        <p>{{ chair.statusText }}</p>
        <p v-if="chair.status === 2 || chair.hasActiveVisit">Ghế đang phục vụ ca khám; không thể đổi thủ công.</p>
        <p v-else-if="chair.isAssigned">Ghế đã được nha sĩ chọn. Nha sĩ/Admin cần bỏ hoặc đổi vị trí trước.</p>
        <button type="button" :disabled="!canEdit(chair)" :aria-label="'Đổi trạng thái ghế ' + chair.chairNumber" @click="open(chair, $event)">Đổi trạng thái</button>
      </li>
    </ul>
    <nav v-if="store.result" aria-label="Phân trang trạng thái ghế">
      <p>{{ store.result.totalCount }} ghế · Trang {{ store.result.page }}</p>
      <button :disabled="loading || saving || store.result.page <= 1" @click="load(store.result.page - 1)">Trang trước</button>
      <button :disabled="loading || saving || !store.result.hasNextPage" @click="load(store.result.page + 1)">Trang sau</button>
    </nav>
    <section v-if="selected" class="editor" aria-labelledby="chair-edit-title">
      <h2 id="chair-edit-title" ref="editorHeading" tabindex="-1">Đổi trạng thái {{ selected.chairNumber }} · {{ selected.roomName }}</h2>
      <form @submit.prevent="save">
        <p>Hiện tại: {{ selected.statusText }}</p>
        <label>Trạng thái mới <select v-model.number="targetStatus" :disabled="saving || needsReview" @change="confirmed = false">
          <option :value="1">Sẵn sàng</option><option :value="3">Bảo trì</option><option :value="4">Ngừng sử dụng</option>
        </select></label>
        <label class="confirmation"><input v-model="confirmed" type="checkbox" :disabled="saving || needsReview" /> Tôi đã kiểm tra đúng ghế và xác nhận trạng thái mới.</label>
        <div class="actions">
          <button :disabled="saving || loading || needsReview || !confirmed || targetStatus === selected.status">{{ saving ? 'Đang lưu…' : 'Xác nhận đổi trạng thái' }}</button>
          <button type="button" :disabled="saving" @click="close">Quay lại</button>
        </div>
      </form>
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
.chair-status { max-width: 980px; margin: auto; padding: 1.5rem; box-sizing: border-box; }
.filters, .actions, nav { display: flex; flex-wrap: wrap; gap: .75rem; align-items: end; }
label, .editor form { display: grid; gap: .5rem; max-width: 100%; }
input, select, button { font: inherit; padding: .55rem; max-width: 100%; box-sizing: border-box; }
button { cursor: pointer; } button:disabled { cursor: default; }
.chairs { list-style: none; padding: 0; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr)); gap: 1rem; }
.chairs li, .editor { border: 1px solid #cbd5e1; border-radius: .5rem; padding: 1rem; box-sizing: border-box; overflow-wrap: break-word; }
.chairs h2 { font-size: 1.1rem; margin-top: 0; }
.editor { margin-top: 1.5rem; } .confirmation { display: flex; align-items: start; }
[role=alert] { color: #b91c1c; overflow-wrap: break-word; } [role=status] { color: #166534; overflow-wrap: break-word; }

@media (max-width: 480px) {
  .chair-status { padding: 0.75rem; }
  .chairs { grid-template-columns: 1fr; }
  .chairs li, .editor { padding: 0.75rem; }
}
</style>
