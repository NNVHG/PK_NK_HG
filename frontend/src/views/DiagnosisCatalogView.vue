<template>
  <main><h1>Danh mục chẩn đoán</h1><RouterLink to="/">Trang chủ</RouterLink>
    <form @submit.prevent="load(1)"><label>Từ khóa <input v-model="keyword" maxlength="200" :disabled="saving" /></label>
      <label><input v-model="includeInactive" type="checkbox" :disabled="saving" /> Bao gồm mục ngừng dùng</label><button :disabled="saving || loading">Tìm</button>
    </form>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <button :disabled="saving || loading" @click="open()">Thêm chẩn đoán</button>
    <ul><li v-for="item in items" :key="item.id"><strong>{{ item.code }} · {{ item.name }}</strong> · {{ item.category }} · {{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }} <button :disabled="saving || loading" @click="open(item)">Sửa / đổi trạng thái</button></li></ul>
    <p v-if="loading">Đang tải…</p><p>{{ total }} mục · Trang {{ page }}</p>
    <button :disabled="loading || saving || page <= 1" @click="load(page - 1)">Trang trước</button><button :disabled="loading || saving || !hasNext" @click="load(page + 1)">Trang sau</button>
    <form v-if="editor" @submit.prevent="save">
      <h2>{{ editId === null ? 'Thêm chẩn đoán' : 'Sửa chẩn đoán' }}</h2>
      <label>Mã nội bộ <input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" :disabled="saving" /></label>
      <label>Tên <input v-model="form.name" required maxlength="200" :disabled="saving" /></label>
      <label>Nhóm <input v-model="form.category" required maxlength="100" list="diagnosis-categories" :disabled="saving" /></label>
      <datalist id="diagnosis-categories"><option v-for="name in categories" :key="name" :value="name" /></datalist>
      <label>Mô tả <textarea v-model="form.description" maxlength="500" :disabled="saving" /></label>
      <label><input v-model="form.isActive" type="checkbox" :disabled="saving" /> Đang dùng để gợi ý</label>
      <p>Ngừng dùng sẽ ẩn khỏi gợi ý; chẩn đoán đã lưu trong hồ sơ vẫn được giữ.</p>
      <button :disabled="saving || needsReview">{{ saving ? 'Đang lưu…' : 'Lưu danh mục' }}</button><button type="button" :disabled="saving" @click="editor = false">Quay lại</button>
      <button v-if="needsReview" type="button" :disabled="saving || loading" @click="load(page)">Tải lại để đối chiếu</button>
    </form>
  </main>
</template>
<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue';
import { diagnosesService, type Diagnosis } from '@/services/diagnoses';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const items = ref<Diagnosis[]>([]), keyword = ref(''), includeInactive = ref(false), page = ref(1), hasNext = ref(false), total = ref(0);
const loading = ref(false), saving = ref(false), error = ref(''), message = ref(''), editor = ref(false), editId = ref<number | null>(null), needsReview = ref(false);
const form = reactive({ code: '', name: '', category: '', description: '', isActive: true, expectedVersion: 0 });
const categories = ['Sâu răng', 'Bệnh lý tủy', 'Nha chu', 'Chấn thương', 'Răng khôn', 'Khác'];
let version = 0, life = 0;
async function load(target = 1) {
  if (auth.role !== 'ADMIN' || saving.value || keyword.value.length > 200 || !Number.isInteger(target) || target <= 0) return;
  const ticket = ++version; loading.value = true; error.value = ''; editor.value = false; items.value = [];
  try { const data = await diagnosesService.list(keyword.value.trim(), target, includeInactive.value); if (ticket !== version) return;
    items.value = data.items; page.value = data.page; total.value = data.totalCount; hasNext.value = data.hasNextPage; needsReview.value = false;
  } catch { if (ticket === version) { error.value = 'Không tải được danh mục. Hãy tải lại để đối chiếu.'; needsReview.value = true; } }
  finally { if (ticket === version) loading.value = false; }
}
function open(item?: Diagnosis) {
  if (auth.role !== 'ADMIN' || saving.value || loading.value || needsReview.value || item && !items.value.some(i => i.id === item.id)) return;
  editId.value = item?.id ?? null; Object.assign(form, { code: item?.code ?? '', name: item?.name ?? '', category: item?.category ?? '', description: item?.description ?? '', isActive: item?.isActive ?? true, expectedVersion: item?.version ?? 0 });
  editor.value = true; error.value = ''; message.value = '';
}
async function save() {
  if (auth.role !== 'ADMIN' || !editor.value || saving.value || loading.value || needsReview.value) return;
  if (!/^[A-Za-z0-9._-]{1,50}$/.test(form.code.trim()) || !form.name.trim() || form.name.length > 200 || !form.category.trim() || form.category.length > 100 || form.description.length > 500) { error.value = 'Kiểm tra mã, tên, nhóm và độ dài nội dung.'; return; }
  const ticket = life; saving.value = true; error.value = '';
  try { const data = await diagnosesService.save(editId.value, { ...form, code: form.code.trim(), name: form.name.trim(), category: form.category.trim(), description: form.description.trim() || null });
    if (ticket !== life) return; if (editId.value !== null && data.id !== editId.value) throw new Error('Invalid catalog response');
    message.value = 'Đã lưu danh mục.'; editor.value = false; saving.value = false; await load(page.value);
  } catch { if (ticket === life) { error.value = 'Chưa xác minh kết quả lưu. Tải lại danh mục để đối chiếu trước khi gửi tiếp.'; needsReview.value = true; } }
  finally { if (ticket === life) saving.value = false; }
}
onMounted(() => load()); onUnmounted(() => { life++; version++; items.value = []; Object.assign(form, { code:'', name:'', category:'', description:'' }); });
</script>
<style scoped>main { max-width:60rem; margin:2rem auto; padding:1rem; } form { display:grid; gap:.7rem; border:1px solid #ddd; padding:1rem; margin:1rem 0; } label { display:grid; gap:.3rem; } input,textarea,button { padding:.5rem; } li { padding:.5rem 0; } [role=alert] { color:#a11; }</style>
