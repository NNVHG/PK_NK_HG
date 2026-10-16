<template>
  <main class="page">
    <h1>Danh mục phòng và ghế nha khoa</h1>
    <p>Khai báo mã ghế và tên phòng. Ghế bảo trì/ngừng sử dụng không phục vụ ca khám. Trạng thái Đang phục vụ do ca khám quản lý.</p>
    <form class="filters" @submit.prevent="load(1)">
      <label>Tìm mã ghế hoặc phòng <input v-model="keyword" maxlength="100" :disabled="saving" /></label>
      <label>Trạng thái <select v-model.number="status" :disabled="saving"><option :value="0">Tất cả</option><option :value="1">Sẵn sàng</option><option :value="2">Đang phục vụ</option><option :value="3">Bảo trì</option><option :value="4">Ngừng sử dụng</option></select></label>
      <label>Hoạt động <select v-model="active" :disabled="saving"><option value="all">Tất cả</option><option value="true">Đang dùng</option><option value="false">Ngừng dùng</option></select></label>
      <button :disabled="saving || loading">Tải danh sách</button>
      <button type="button" :disabled="saving || loading || needsReview" @click="openCreate">Thêm ghế</button>
    </form>
    <p v-if="loading" role="status">Đang tải danh mục…</p>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <p v-if="needsReview" role="alert">Tải lại danh sách thành công rồi mở lại ghế cần sửa trước khi xác nhận.</p>
    <p v-if="!loading && store.result && !store.result.items.length">Chưa có ghế phù hợp. Admin có thể khai báo ghế mới.</p>
    <ul class="chairs"><li v-for="chair in store.result?.items ?? []" :key="chair.id">
      <strong>{{ chair.chairNumber }} · {{ chair.roomName }}</strong><p>{{ chair.statusText }} · {{ chair.isActive ? 'Đang dùng' : 'Ngừng dùng' }}</p>
      <p v-if="chair.description">{{ chair.description }}</p>
      <button :disabled="saving || loading || needsReview || chair.status === 2" @click="openEdit(chair)">Sửa / đổi trạng thái</button>
      <span v-if="chair.status === 2"> Cần kết thúc ca khám trước khi sửa ghế.</span>
    </li></ul>
    <nav v-if="store.result" aria-label="Phân trang ghế">
      <p>{{ store.result.totalCount }} ghế · Trang {{ store.result.page }}</p>
      <button :disabled="saving || loading || store.result.page <= 1" @click="load(store.result.page - 1)">Trang trước</button>
      <button :disabled="saving || loading || !store.result.hasNextPage" @click="load(store.result.page + 1)">Trang sau</button>
    </nav>
    <section v-if="formOpen" class="editor" aria-labelledby="chair-title">
      <h2 id="chair-title">{{ selected ? 'Sửa ghế ' + selected.chairNumber : 'Khai báo ghế mới' }}</h2>
      <form @submit.prevent="save">
        <label>Mã ghế <input v-model="chairNumber" maxlength="50" required :disabled="saving || selected !== null" /></label>
        <label>Tên phòng <input v-model="roomName" maxlength="100" required :disabled="saving" /></label>
        <label>Trạng thái quản lý <select v-model.number="formStatus" :disabled="saving"><option :value="1">Sẵn sàng</option><option :value="3">Bảo trì</option><option :value="4">Ngừng sử dụng</option></select></label>
        <label>Ghi chú <textarea v-model="description" maxlength="500" :disabled="saving" /></label>
        <p v-if="formStatus === 4">Ngừng sử dụng giữ bản ghi ghế và lịch sử; ghế không được chọn để phục vụ.</p>
        <label><input v-model="confirmed" type="checkbox" :disabled="saving || needsReview" /> Tôi đã đối chiếu đúng ghế/phòng và xác nhận lưu trạng thái.</label>
        <button :disabled="saving || loading || needsReview || !confirmed">{{ saving ? 'Đang lưu…' : 'Xác nhận lưu' }}</button>
        <button type="button" :disabled="saving" @click="closeForm">Quay lại</button>
      </form>
    </section>
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
.page { max-width: 980px; margin: auto; padding: 1.5rem; } .filters { display: flex; gap: .75rem; flex-wrap: wrap; align-items: end; }
label { display: grid; gap: .35rem; } input, select, textarea, button { font: inherit; padding: .5rem; } button { cursor: pointer; }
.chairs { list-style: none; padding: 0; } li, .editor { border: 1px solid #cbd5e1; border-radius: .5rem; padding: 1rem; margin: .8rem 0; }
.editor form { display: grid; gap: .8rem; } [role=alert] { color: #b91c1c; } [role=status] { color: #166534; } nav { display: flex; gap: .75rem; align-items: center; }
</style>
