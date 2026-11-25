<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <h1>Danh mục chẩn đoán &amp; bệnh lý</h1>
        <p class="subtitle">Quản lý mã bệnh lý, tên chẩn đoán và nhóm phân loại gợi ý khi khám bệnh.</p>
      </div>
      <div class="header-actions">
        <span class="count-pill">{{ total }} mục</span>
        <button class="button button-primary" :disabled="saving || loading || needsReview" @click="open()">
          ＋ Thêm chẩn đoán
        </button>
      </div>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">
      Dữ liệu chưa đối soát. Vui lòng bấm <button class="link-btn" @click="load(page)">Tải lại</button> trước khi thao tác tiếp.
    </div>

    <div class="panel table-panel">
      <form class="table-toolbar" @submit.prevent="load(1)">
        <div class="toolbar-search">
          <input
            id="diag-keyword"
            v-model="keyword"
            placeholder="Tìm mã hoặc tên chẩn đoán..."
            maxlength="200"
            aria-label="Tìm kiếm từ khóa"
            :disabled="saving"
          />
        </div>
        <label class="checkbox-label">
          <input v-model="includeInactive" type="checkbox" :disabled="saving" />
          <span>Bao gồm mục ngừng dùng</span>
        </label>
        <button class="button button-secondary" type="submit" :disabled="saving || loading">
          {{ loading ? 'Đang tải…' : '🔍 Tìm kiếm' }}
        </button>
      </form>

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width: 130px;">Mã bệnh</th>
              <th>Tên chẩn đoán lâm sàng</th>
              <th style="width: 160px;">Nhóm phân loại</th>
              <th>Mô tả / Ghi chú</th>
              <th style="width: 120px; text-align: center;">Trạng thái</th>
              <th style="width: 90px; text-align: right;">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.id">
              <td><span class="code-badge">{{ item.code }}</span></td>
              <td><span class="diag-name">{{ item.name }}</span></td>
              <td><span class="category-pill">{{ item.category }}</span></td>
              <td class="desc-cell">{{ item.description || '—' }}</td>
              <td style="text-align: center;">
                <span class="status-pill" :class="item.isActive ? 'status-pill--active' : 'status-pill--inactive'">
                  {{ item.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                </span>
              </td>
              <td style="text-align: right;">
                <button class="button button-quiet" :disabled="saving || loading || needsReview" @click="open(item)">
                  Sửa
                </button>
              </td>
            </tr>
            <tr v-if="!loading && !items.length">
              <td colspan="6" class="empty-state">Không có chẩn đoán phù hợp.</td>
            </tr>
            <tr v-if="loading">
              <td colspan="6" class="loading-state">Đang tải danh mục…</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="pagination">
        <span class="page-info">Trang {{ page }} (Tổng {{ total }} mục)</span>
        <div class="page-btns">
          <button class="button button-quiet" :disabled="loading || saving || page <= 1" @click="load(page - 1)">
            ← Trước
          </button>
          <button class="button button-quiet" :disabled="loading || saving || !hasNext" @click="load(page + 1)">
            Sau →
          </button>
        </div>
      </div>
    </div>

    <div v-if="editor" class="modal-overlay" @click.self="editor = false">
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="diag-modal-title">
        <div class="modal-header">
          <h2 id="diag-modal-title">{{ editId === null ? 'Thêm chẩn đoán mới' : 'Chỉnh sửa chẩn đoán' }}</h2>
          <button class="close-btn" type="button" :disabled="saving" aria-label="Đóng" @click="editor = false">✕</button>
        </div>

        <form class="modal-form" @submit.prevent="save">
          <div class="form-grid">
            <div class="form-field">
              <label>Mã chẩn đoán <b>*</b></label>
              <input v-model="form.code" required maxlength="50" pattern="[A-Za-z0-9._-]+" placeholder="VD: K02.1" :disabled="saving" />
            </div>

            <div class="form-field">
              <label>Nhóm bệnh lý <b>*</b></label>
              <input v-model="form.category" required maxlength="100" list="diag-cats" placeholder="Chọn nhóm..." :disabled="saving" />
              <datalist id="diag-cats"><option v-for="name in categories" :key="name" :value="name" /></datalist>
            </div>

            <div class="form-field form-field--wide">
              <label>Tên chẩn đoán <b>*</b></label>
              <input v-model="form.name" required maxlength="200" placeholder="VD: Sâu men răng mặt nhai..." :disabled="saving" />
            </div>

            <div class="form-field form-field--wide">
              <label>Mô tả / Ghi chú</label>
              <textarea v-model="form.description" rows="2" maxlength="500" placeholder="Triệu chứng hoặc lưu ý..." :disabled="saving"></textarea>
            </div>

            <div class="form-field form-field--wide">
              <div class="confirm-box">
                <label class="checkbox-label">
                  <input v-model="form.isActive" type="checkbox" :disabled="saving" />
                  <span>Kích hoạt sử dụng để gợi ý khi khám bệnh</span>
                </label>
                <p class="box-hint">Mục ngừng dùng sẽ ẩn khỏi gợi ý; hồ sơ cũ đã dùng vẫn giữ nguyên.</p>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button class="button button-secondary" type="button" :disabled="saving" @click="editor = false">Hủy</button>
            <button class="button button-primary" type="submit" :disabled="saving || needsReview">
              {{ saving ? 'Đang lưu…' : 'Lưu chẩn đoán' }}
            </button>
          </div>
        </form>
      </div>
    </div>
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
  try {
    const data = await diagnosesService.list(keyword.value.trim(), target, includeInactive.value);
    if (ticket !== version) return;
    items.value = data.items; page.value = data.page; total.value = data.totalCount; hasNext.value = data.hasNextPage; needsReview.value = false;
  } catch {
    if (ticket === version) { error.value = 'Không tải được danh mục. Hãy tải lại để đối chiếu.'; needsReview.value = true; }
  } finally {
    if (ticket === version) loading.value = false;
  }
}

function open(item?: Diagnosis) {
  if (auth.role !== 'ADMIN' || saving.value || loading.value || needsReview.value || item && !items.value.some(i => i.id === item.id)) return;
  editId.value = item?.id ?? null;
  Object.assign(form, {
    code: item?.code ?? '', name: item?.name ?? '', category: item?.category ?? '',
    description: item?.description ?? '', isActive: item?.isActive ?? true, expectedVersion: item?.version ?? 0
  });
  editor.value = true; error.value = ''; message.value = '';
}

async function save() {
  if (auth.role !== 'ADMIN' || !editor.value || saving.value || loading.value || needsReview.value) return;
  if (!/^[A-Za-z0-9._-]{1,50}$/.test(form.code.trim()) || !form.name.trim() || form.name.length > 200 || !form.category.trim() || form.category.length > 100 || form.description.length > 500) {
    error.value = 'Kiểm tra mã, tên, nhóm và độ dài nội dung.';
    return;
  }
  const ticket = life; saving.value = true; error.value = '';
  try {
    const data = await diagnosesService.save(editId.value, {
      ...form,
      code: form.code.trim(),
      name: form.name.trim(),
      category: form.category.trim(),
      description: form.description.trim() || null
    });
    if (ticket !== life) return;
    if (editId.value !== null && data.id !== editId.value) throw new Error('Invalid catalog response');
    message.value = 'Đã lưu danh mục.'; editor.value = false; saving.value = false; await load(page.value);
  } catch {
    if (ticket === life) { error.value = 'Chưa xác minh kết quả lưu. Tải lại danh mục để đối chiếu trước khi gửi tiếp.'; needsReview.value = true; }
  } finally {
    if (ticket === life) saving.value = false;
  }
}

onMounted(() => load());
onUnmounted(() => { life++; version++; items.value = []; Object.assign(form, { code: '', name: '', category: '', description: '' }); });
</script>

<style scoped>
.page-container {
  max-width: 1480px;
  margin: 0 auto;
  padding: 0.9rem 1.25rem 1.5rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

h1 {
  font-size: 1.38rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
  line-height: 1.2;
}

.subtitle {
  margin: 0.2rem 0 0;
  color: #64748b;
  font-size: 0.84rem;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.55rem;
}

.count-pill {
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
  font-size: 0.78rem;
  font-weight: 700;
  padding: 0.25rem 0.65rem;
  border-radius: 999px;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.table-panel {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.table-toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.65rem;
  padding: 0.75rem 1rem;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
}

.toolbar-search {
  flex: 1 1 260px;
}

.toolbar-search input,
.form-field input,
.form-field textarea {
  box-sizing: border-box;
  width: 100%;
  height: 36px;
  padding: 0 0.7rem;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  background: #ffffff;
  color: #1e293b;
  font: inherit;
  font-size: 0.85rem;
}

.form-field textarea {
  height: auto;
  padding: 0.5rem 0.7rem;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.82rem;
  font-weight: 600;
  color: #334155;
  cursor: pointer;
}

.table-wrap {
  overflow: auto;
  max-height: calc(100vh - 245px);
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
  text-align: left;
}

.data-table th,
.data-table td {
  padding: 0.5rem 0.85rem;
  border-bottom: 1px solid #f1f5f9;
  vertical-align: middle;
}

.data-table thead th {
  position: sticky;
  top: 0;
  z-index: 1;
  background: #f8fafc;
  color: #475569;
  font-size: 0.74rem;
  font-weight: 750;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  border-bottom: 1px solid #e2e8f0;
}

.code-badge { font-family: monospace; font-size: 0.75rem; font-weight: 750; color: #0369a1; background: #e0f2fe; padding: 0.12rem 0.42rem; border-radius: 4px; }
.diag-name { font-weight: 700; color: #0f2d59; }
.category-pill { font-size: 0.75rem; font-weight: 650; background: #f1f5f9; color: #475569; padding: 0.15rem 0.5rem; border-radius: 6px; }
.desc-cell { color: #64748b; font-size: 0.81rem; max-width: 380px; }
.status-pill { display: inline-block; font-size: 0.71rem; font-weight: 750; padding: 0.12rem 0.45rem; border-radius: 999px; }
.status-pill--active { background: #ecfdf5; color: #059669; }
.status-pill--inactive { background: #fef2f2; color: #dc2626; }
.empty-state, .loading-state { text-align: center; padding: 2rem 1rem !important; color: #64748b; font-size: 0.9rem; }

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.65rem 1rem;
  border-top: 1px solid #e2e8f0;
  background: #f8fafc;
}
.page-info { font-size: 0.8rem; color: #64748b; font-weight: 600; }
.page-btns { display: flex; gap: 0.4rem; }

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  height: 36px;
  padding: 0 0.95rem;
  border: 1px solid transparent;
  border-radius: 7px;
  font: inherit;
  font-size: 0.84rem;
  font-weight: 700;
  cursor: pointer;
}
.button-primary { background: #0284c7; color: #ffffff; }
.button-primary:hover:not(:disabled) { background: #0369a1; }
.button-secondary { background: #ffffff; color: #334155; border-color: #cbd5e1; }
.button-quiet { background: #ffffff; border-color: #cbd5e1; color: #475569; height: 30px; padding: 0 0.7rem; font-size: 0.78rem; }
.button:disabled { cursor: not-allowed; opacity: 0.55; }
.link-btn { background: none; border: none; color: #0284c7; text-decoration: underline; cursor: pointer; padding: 0; font: inherit; font-weight: 700; }

.alert { padding: 0.65rem 0.9rem; border-radius: 8px; font-size: 0.84rem; font-weight: 600; }
.alert-error { background: #fef2f2; color: #991b1b; border: 1px solid #fee2e2; }
.alert-success { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
.alert-warning { background: #fffbeb; color: #92400e; border: 1px solid #fef3c7; }

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
.modal-card { background: #ffffff; border-radius: 12px; max-width: 600px; width: 100%; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.14); overflow: hidden; border: 1px solid #e2e8f0; }
.modal-header { display: flex; justify-content: space-between; align-items: center; padding: 0.95rem 1.15rem; background: #f8fafc; border-bottom: 1px solid #e2e8f0; }
.modal-header h2 { font-size: 1.1rem; font-weight: 800; color: #0f2d59; margin: 0; }
.close-btn { background: none; border: none; font-size: 1.15rem; color: #64748b; cursor: pointer; }
.modal-form { padding: 1.15rem; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-field { display: grid; gap: 0.25rem; font-size: 0.8rem; font-weight: 650; color: #334155; }
.form-field b { color: #dc2626; }
.form-field--wide { grid-column: 1 / -1; }
.confirm-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.65rem 0.85rem; }
.box-hint { margin: 0.25rem 0 0; font-size: 0.76rem; color: #64748b; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 1rem; padding-top: 0.85rem; border-top: 1px solid #f1f5f9; }

@media (max-width: 600px) {
  .form-grid { grid-template-columns: 1fr; }
}
</style>
