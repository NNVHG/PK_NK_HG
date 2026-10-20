<template>
  <section v-if="auth.role === 'ADMIN'" class="catalog">
    <h1>Danh mục thuốc</h1>
    <router-link to="/admin/medicine-stock">Nhập và xem tồn thuốc thực tế</router-link>
    <p>Thuốc khởi tạo đang ngừng hoạt động và chưa thiết lập giá. Kiểm tra tên sản phẩm, hàm lượng, đơn vị và giá bán rồi kích hoạt để dùng khi kê đơn.</p>
    <form class="search" @submit.prevent="load(1)">
      <label>Tìm mã / tên / hoạt chất / hàm lượng<input v-model="keyword" maxlength="200" :disabled="saving" /></label>
      <label><input v-model="includeInactive" type="checkbox" :disabled="saving" /> Bao gồm thuốc ngừng dùng</label>
      <button :disabled="saving || loading">Tìm / Làm mới</button>
    </form>
    <p v-if="error" role="alert" class="error">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <button :disabled="saving || loading || needsReview" @click="open()">Thêm thuốc</button>
    <div class="table-wrap"><table><thead><tr><th>Mã / Tên thuốc</th><th>Hoạt chất / Hàm lượng</th><th>Đơn vị</th><th>Giá bán (VNĐ)</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
      <tbody><tr v-for="item in items" :key="item.id"><td><strong>{{ item.code }}</strong><br />{{ item.name }}</td><td>{{ item.activeIngredient || '—' }}<br />{{ item.strength || '—' }}</td><td>{{ item.unit }}</td><td>{{ item.unitPrice === '0' ? 'Chưa thiết lập giá' : money(item.unitPrice) }}</td><td>{{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}</td><td><button :disabled="saving || loading || needsReview" @click="open(item)">Sửa / đổi trạng thái</button></td></tr>
      <tr v-if="!loading && !items.length"><td colspan="6">Không có thuốc phù hợp.</td></tr></tbody></table></div>
    <p v-if="loading">Đang tải…</p><p>{{ total }} thuốc · Trang {{ page }}</p>
    <button :disabled="loading || saving || page <= 1" @click="load(page - 1)">Trang trước</button><button :disabled="loading || saving || !hasNext" @click="load(page + 1)">Trang sau</button>
    <form v-if="editor" class="editor" @submit.prevent="save">
      <h2>{{ editId === null ? 'Thêm thuốc' : 'Sửa thuốc' }}</h2>
      <label>Mã nội bộ<input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" :disabled="saving" /></label>
      <label>Tên sản phẩm<input v-model="form.name" required maxlength="200" :disabled="saving" /></label>
      <label>Hoạt chất<input v-model="form.activeIngredient" maxlength="200" :disabled="saving" /></label>
      <label>Hàm lượng / nồng độ<input v-model="form.strength" maxlength="50" :disabled="saving" /></label>
      <label>Đơn vị<select v-model="form.unit" :disabled="saving"><option v-for="unit in units" :key="unit">{{ unit }}</option></select></label>
      <label>Đơn giá theo đơn vị trên (VNĐ nguyên đồng)<input v-model="form.unitPrice" inputmode="numeric" pattern="[0-9]+" maxlength="18" required :disabled="saving" /></label>
      <label>Hướng dẫn sử dụng mặc định (tùy chọn)<textarea v-model="form.defaultUsageInstruction" maxlength="500" :disabled="saving" /></label>
      <label><input v-model="form.isActive" type="checkbox" :disabled="saving" /> Đang dùng để kê đơn</label>
      <p>Giá phải lớn hơn 0 để kích hoạt. Ngừng dùng giữ nguyên danh mục và lịch sử; giá mới không sửa chứng từ đã chụp giá.</p>
      <label><input v-model="confirmed" type="checkbox" :disabled="saving" /> Tôi đã kiểm tra thông tin, đơn vị và giá bán</label>
      <button :disabled="saving || needsReview || !confirmed">{{ saving ? 'Đang lưu…' : 'Lưu thuốc' }}</button>
      <button v-if="editId !== null && originalActive" type="button" :disabled="saving || loading || needsReview || !confirmed" @click="deactivate">Ngừng dùng thuốc này</button>
      <button type="button" :disabled="saving" @click="close">Quay lại</button>
      <button v-if="needsReview" type="button" :disabled="saving || loading" @click="load(page)">Tải lại để đối chiếu</button>
    </form>
  </section>
  <p v-else role="alert">Chỉ Admin được quản lý danh mục thuốc.</p>
</template>
<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, watch } from 'vue';
import { medicinesService, type Medicine } from '@/services/medicines';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const items = ref<Medicine[]>([]), keyword = ref(''), includeInactive = ref(true), page = ref(1), hasNext = ref(false), total = ref(0);
const loading = ref(false), saving = ref(false), error = ref(''), message = ref(''), editor = ref(false), editId = ref<number | null>(null), needsReview = ref(false), confirmed = ref(false), originalActive = ref(false);
const empty = () => ({ code: '', name: '', activeIngredient: '', strength: '', unit: 'Viên', unitPrice: '0', defaultUsageInstruction: '', isActive: false, expectedVersion: 0 });
const form = reactive(empty());
const units = ['Viên', 'Vỉ', 'Gói', 'Chai', 'Ống', 'Tuýp', 'Hộp'];
let generation = 0, disposed = false;
const actorKey = () => `${auth.user?.userId}/${auth.role}`;
function close() { if (saving.value) return; editor.value = false; confirmed.value = false; editId.value = null; Object.assign(form, empty()); }
async function load(target = 1) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || keyword.value.length > 200 || !Number.isInteger(target) || target <= 0 || target > 1000000) return;
  const ticket = ++generation, actor = actorKey(), term = keyword.value, inactive = includeInactive.value;
  loading.value = true; error.value = ''; close(); items.value = []; hasNext.value = false; total.value = 0;
  try {
    const data = await medicinesService.list(term.trim(), target, inactive);
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN' || term !== keyword.value || inactive !== includeInactive.value) return;
    if (!Array.isArray(data.items) || data.page !== target || !Number.isInteger(data.totalCount)) throw new Error('Invalid catalog');
    items.value = data.items; page.value = data.page; total.value = data.totalCount; hasNext.value = data.hasNextPage; needsReview.value = false;
  } catch { if (!disposed && ticket === generation && actor === actorKey()) { error.value = 'Không tải được danh mục. Tải lại để đối chiếu.'; needsReview.value = true; } }
  finally { if (!disposed && ticket === generation) loading.value = false; }
}
function open(item?: Medicine) {
  if (disposed || auth.role !== 'ADMIN' || saving.value || loading.value || needsReview.value) return;
  const stored = item ? items.value.find(i => i.id === item.id) : undefined;
  if (item && !stored) return;
  editId.value = stored?.id ?? null; originalActive.value = stored?.isActive ?? false;
  Object.assign(form, stored ? { code: stored.code, name: stored.name, activeIngredient: stored.activeIngredient ?? '', strength: stored.strength ?? '', unit: stored.unit, unitPrice: stored.unitPrice, defaultUsageInstruction: stored.defaultUsageInstruction ?? '', isActive: stored.isActive, expectedVersion: stored.version } : empty());
  editor.value = true; confirmed.value = false; error.value = ''; message.value = '';
}
function valid() {
  if (!/^[A-Za-z0-9._-]{1,50}$/.test(form.code.trim()) || !form.name.trim() || form.name.length > 200 || form.activeIngredient.length > 200 || form.strength.length > 50 || form.defaultUsageInstruction.length > 500 || !units.includes(form.unit) || !/^(0|[1-9][0-9]{0,17})$/.test(form.unitPrice) || form.isActive && BigInt(form.unitPrice) <= 0n) {
    error.value = 'Kiểm tra mã, tên, đơn vị và giá nguyên đồng; đặt giá lớn hơn 0 trước khi kích hoạt.'; return false;
  }
  return true;
}
async function write(operation: () => Promise<Medicine>, deactivating = false) {
  const ticket = generation, actor = actorKey(), id = editId.value, expected = form.expectedVersion;
  saving.value = true; error.value = '';
  try {
    const data = await operation();
    if (disposed || ticket !== generation || actor !== actorKey() || auth.role !== 'ADMIN') return;
    if (!Number.isInteger(data.id) || data.id <= 0 || id !== null && data.id !== id || data.version !== (id === null ? 1 : expected + 1) || deactivating && data.isActive) throw new Error('Invalid write response');
    message.value = deactivating ? 'Đã ngừng dùng thuốc; lịch sử được giữ nguyên.' : 'Đã lưu danh mục thuốc.';
    saving.value = false; close(); await load(page.value);
  } catch { if (!disposed && ticket === generation && actor === actorKey()) { error.value = 'Chưa xác minh kết quả lưu. Tải lại để đối chiếu trước khi gửi tiếp.'; needsReview.value = true; confirmed.value = false; } }
  finally { if (!disposed && ticket === generation) saving.value = false; }
}
async function save() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || saving.value || loading.value || needsReview.value || !confirmed.value || !valid()) return;
  const payload = { ...form, code: form.code.trim(), name: form.name.trim(), activeIngredient: form.activeIngredient.trim() || null, strength: form.strength.trim() || null, defaultUsageInstruction: form.defaultUsageInstruction.trim() || null };
  await write(() => medicinesService.save(editId.value, payload));
}
async function deactivate() {
  if (disposed || auth.role !== 'ADMIN' || !editor.value || editId.value === null || !originalActive.value || saving.value || loading.value || needsReview.value || !confirmed.value) return;
  const id = editId.value, version = form.expectedVersion;
  await write(() => medicinesService.deactivate(id, version), true);
}
function money(value: string) { try { return BigInt(value).toLocaleString('vi-VN') + ' ₫'; } catch { return 'Giá không hợp lệ'; } }
watch(actorKey, () => { generation++; items.value = []; total.value = 0; hasNext.value = false; saving.value = false; loading.value = false; needsReview.value = true; message.value = ''; error.value = ''; close(); });
watch(() => JSON.stringify(form), () => { confirmed.value = false; });
onMounted(() => load());
onUnmounted(() => { disposed = true; generation++; items.value = []; Object.assign(form, empty()); message.value = ''; error.value = ''; });
</script>
<style scoped>
.catalog { max-width: 70rem; margin: 1rem auto; } p { line-height: 1.6; } form { padding: 1rem; margin: 1rem 0; border: 1px solid #cbd5e1; border-radius: 6px; } .search { display: flex; flex-wrap: wrap; gap: 1rem; align-items: end; } .editor { display: grid; gap: .7rem; max-width: 40rem; } label { display: grid; gap: .3rem; } input, textarea, button, select { padding: .5rem; } button { cursor: pointer; } button:disabled { opacity: .55; cursor: default; } .table-wrap { overflow-x: auto; margin-top: 1rem; } table { width: 100%; border-collapse: collapse; } th, td { padding: .7rem; border-bottom: 1px solid #cbd5e1; text-align: left; vertical-align: top; } th { background: #f1f5f9; } .error { color: #b91c1c; }
</style>
