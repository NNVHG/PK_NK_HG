<template>
  <section v-if="auth.role === 'ADMIN'" class="catalog">
    <h1>Danh mục vật tư tiêu hao</h1>
    <p>Quản lý danh mục vật tư tiêu hao (mã vật tư, tên, đơn vị tính, ngưỡng tồn tối thiểu chuẩn bị cho cảnh báo Phase 2). Giai đoạn 1 chỉ quản lý danh mục, không bao gồm đơn giá hay giao dịch nhập/xuất kho.</p>
    <form class="search" @submit.prevent="load(1)">
      <label>Tìm mã / tên / đơn vị tính<input v-model="keyword" maxlength="200" :disabled="saving" /></label>
      <label><input v-model="includeInactive" type="checkbox" :disabled="saving" /> Bao gồm vật tư ngừng dùng</label>
      <button :disabled="saving || loading">Tìm / Làm mới</button>
    </form>
    <p v-if="error" role="alert" class="error">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <button :disabled="saving || loading || needsReview" @click="open()">Thêm vật tư</button>
    <div class="table-wrap">
      <table>
        <thead>
          <tr><th>Mã / Tên vật tư</th><th>Đơn vị tính</th><th>Ngưỡng tối thiểu</th><th>Mô tả / Quy cách</th><th>Trạng thái</th><th>Thao tác</th></tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.id">
            <td><strong>{{ item.code }}</strong><br />{{ item.name }}</td>
            <td>{{ item.unit }}</td>
            <td>{{ item.minimumStockThreshold }}</td>
            <td>{{ item.description || '—' }}</td>
            <td>{{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}</td>
            <td><button :disabled="saving || loading || needsReview" @click="open(item)">Sửa / đổi trạng thái</button></td>
          </tr>
          <tr v-if="!loading && !items.length"><td colspan="6">Không có vật tư phù hợp.</td></tr>
        </tbody>
      </table>
    </div>
    <p v-if="loading">Đang tải…</p><p>{{ total }} vật tư · Trang {{ page }}</p>
    <button :disabled="loading || saving || page <= 1" @click="load(page - 1)">Trang trước</button>
    <button :disabled="loading || saving || !hasNext" @click="load(page + 1)">Trang sau</button>
    <form v-if="editor" class="editor" @submit.prevent="save">
      <h2>{{ editId === null ? 'Thêm vật tư tiêu hao' : 'Sửa vật tư tiêu hao' }}</h2>
      <label>Mã vật tư (ví dụ: VT-GANGTAY)<input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" :disabled="saving" /></label>
      <label>Tên vật tư<input v-model="form.name" required maxlength="200" :disabled="saving" /></label>
      <label>Đơn vị tính<input v-model="form.unit" required maxlength="50" list="supply-units" :disabled="saving" /></label>
      <datalist id="supply-units"><option v-for="unit in units" :key="unit" :value="unit" /></datalist>
      <label>Ngưỡng tồn tối thiểu (số nguyên &ge; 0)<input v-model.number="form.minimumStockThreshold" type="number" min="0" max="1000000" step="1" required :disabled="saving" /></label>
      <label>Mô tả / quy cách (tùy chọn)<textarea v-model="form.description" maxlength="500" :disabled="saving" /></label>
      <label><input v-model="form.isActive" type="checkbox" :disabled="saving" /> Đang sử dụng</label>
      <p>Ngừng dùng sẽ ẩn vật tư khỏi danh sách đang hoạt động nhưng vẫn bảo toàn lịch sử danh mục.</p>
      <button :disabled="saving || needsReview">{{ saving ? 'Đang lưu…' : 'Lưu vật tư' }}</button>
      <button v-if="editId !== null && originalActive" type="button" :disabled="saving || loading || needsReview" @click="deactivate">Ngừng dùng vật tư này</button>
      <button type="button" :disabled="saving" @click="close">Quay lại</button>
      <button v-if="needsReview" type="button" :disabled="saving || loading" @click="load(page)">Tải lại để đối chiếu</button>
    </form>
  </section>
  <p v-else role="alert">Chỉ Admin được quản lý danh mục vật tư tiêu hao.</p>
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
.catalog { max-width: 70rem; margin: 1rem auto; } p { line-height: 1.6; } form { padding: 1rem; margin: 1rem 0; border: 1px solid #cbd5e1; border-radius: 6px; } .search { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; } .editor { display: grid; gap: .7rem; max-width: 40rem; } label { display: grid; gap: .3rem; } input, textarea, button, select { padding: .5rem; } button { cursor: pointer; } button:disabled { opacity: .55; cursor: default; } .table-wrap { overflow-x: auto; margin-top: 1rem; } table { width: 100%; border-collapse: collapse; } th, td { padding: .7rem; border-bottom: 1px solid #cbd5e1; text-align: left; vertical-align: top; } th { background: #f1f5f9; } .error { color: #b91c1c; }
</style>
