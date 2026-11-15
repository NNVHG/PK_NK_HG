<template>
  <main class="page">
    <div class="header-banner">
      <div class="header-icon">🏥</div>
      <div>
        <h1>Phòng làm việc nha sĩ & phụ tá</h1>
        <p class="subtitle">Nha sĩ chọn phòng điều trị làm việc trong ngày; phụ tá kết nối theo nha sĩ để đồng bộ phòng. Vị trí lưu giữ xuyên suốt ca làm việc.</p>
      </div>
    </div>

    <section v-if="auth.role === 'ADMIN'" class="card admin-dispatch-card">
      <div class="card-header">
        <h2>👥 Điều phối nhân sự (Admin)</h2>
      </div>
      <form class="search-form" @submit.prevent="loadTargets(1)">
        <div class="input-wrap">
          <input v-model="keyword" placeholder="Nhập tên nhân sự cần tìm..." maxlength="100" :disabled="saving" />
        </div>
        <button class="btn btn-primary" :disabled="saving || targetsLoading">Tìm kiếm</button>
      </form>
      <div v-if="targetError" class="alert alert-error" role="alert">⚠️ {{ targetError }}</div>
      <ul class="person-list">
        <li v-for="person in targets?.items ?? []" :key="person.userId">
          <button class="person-item-btn" :disabled="saving || loading" @click="load(person.userId)">
            <span class="person-name">{{ person.fullName }}</span>
            <span class="badge-role">{{ person.roleCode }}</span>
            <span v-if="!person.isActive" class="badge-locked">Đã khóa</span>
          </button>
        </li>
      </ul>
      <nav v-if="targets" class="pagination-bar">
        <button class="btn btn-outline" :disabled="saving || targetsLoading || targets.page <= 1" @click="loadTargets(targets.page - 1)">Trang trước</button>
        <span class="page-info">Trang <strong>{{ targets.page }}</strong> · Tổng <strong>{{ targets.totalCount }}</strong> người</span>
        <button class="btn btn-outline" :disabled="saving || targetsLoading || !targets.hasNextPage" @click="loadTargets(targets.page + 1)">Trang sau</button>
      </nav>
    </section>

    <div v-if="loading" class="status-box" role="status">
      <div class="spinner"></div>
      <p>Đang đối chiếu dữ liệu vị trí…</p>
    </div>
    <div v-if="error" class="alert alert-error" role="alert">⚠️ {{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">✅ {{ message }}</div>
    <div v-if="needsReview" class="alert alert-warning" role="alert">ℹ️ Đã cập nhật thành công. Vui lòng đối chiếu vị trí trước khi thao tác tiếp.</div>

    <div v-if="targetId" class="refresh-wrap">
      <button class="btn btn-outline" :disabled="loading || saving" @click="load(targetId)">🔄 Tải lại vị trí</button>
    </div>
    <section v-if="store.snapshot" class="card workspace-detail-card" aria-labelledby="workspace-title">
      <div class="profile-header">
        <div>
          <h2 id="workspace-title">{{ store.snapshot.fullName }}</h2>
          <span class="badge-role-lg">{{ store.snapshot.roleCode }}</span>
        </div>
        <div v-if="store.snapshot.workingDentistId && store.snapshot.roleCode === 'ASSISTANT'" class="coworker-chip">
          👨‍⚕️ Nha sĩ đồng hành: <strong>{{ store.snapshot.dentistName }}</strong> (ID: {{ store.snapshot.workingDentistId }})
        </div>
      </div>
      
      <div v-if="store.snapshot.roleCode === 'DENTIST'" class="location-status">
        <div v-if="store.snapshot.isLocationUsable && store.snapshot.chairId" class="location-card location-active">
          <div class="loc-icon">🟢</div>
          <div class="loc-info">
            <div class="loc-label">Phòng điều trị đã xác nhận hôm nay</div>
            <div class="loc-val">{{ store.snapshot.roomName || 'Chưa có phòng' }}</div>
          </div>
        </div>
        <div v-else class="unconfirmed-warning" role="alert">
          ⚠️ Chưa xác nhận phòng làm việc hôm nay. Vui lòng chọn phòng làm việc bên dưới và xác nhận để bắt đầu đón bệnh nhân.
        </div>
      </div>

      <div v-else-if="store.snapshot.roleCode === 'ASSISTANT'" class="location-status">
        <template v-if="store.snapshot.workingDentistId">
          <div v-if="store.snapshot.isLocationUsable && store.snapshot.roomName" class="location-card location-active">
            <div class="loc-icon">🟢</div>
            <div class="loc-info">
              <div class="loc-label">Phòng làm việc theo nha sĩ hôm nay</div>
              <div class="loc-val">{{ store.snapshot.roomName }}</div>
            </div>
          </div>
          <div v-else class="unconfirmed-warning" role="alert">
            ⚠️ Nha sĩ ({{ store.snapshot.dentistName }}) chưa xác nhận phòng làm việc hôm nay. Phòng sẽ tự cập nhật ngay khi nha sĩ xác nhận.
          </div>
        </template>
        <div v-else class="empty-dentist-prompt">
          Chưa liên kết với nha sĩ nào. Vui lòng chọn nha sĩ làm cùng trong ngày bên dưới.
        </div>
      </div>

      <div v-else class="location-card">
        <div class="loc-info">
          <div class="loc-label">Phòng làm việc hiện tại</div>
          <div class="loc-val">{{ store.snapshot.roomName || 'Chưa có phòng' }}</div>
        </div>
      </div>

      <div v-if="!store.snapshot.canChange" class="alert alert-warning" role="alert">
        🔒 Nha sĩ đang có ca khám hoặc lời mời chờ tiếp nhận. Vui lòng hoàn tất lượt khám trước khi đổi phòng làm việc.
      </div>

      <form class="workspace-form" @submit.prevent="save">
        <div v-if="store.snapshot.isActive && store.snapshot.roleCode === 'DENTIST'" class="form-group">
          <label for="chair-choice-select">Chọn phòng điều trị làm việc hôm nay</label>
          <select id="chair-choice-select" v-model.number="chairChoice" :disabled="saving || loading || needsReview || !store.snapshot.canChange" @change="markDirty">
            <option :value="0">❌ Bỏ chọn phòng làm việc hiện tại</option>
            <option v-for="chair in options?.chairs ?? []" :key="chair.id" :value="chair.id" :disabled="!chair.isActive || chair.status !== 1 || (chair.claimedByUserId !== null && chair.claimedByUserId !== store.snapshot.userId)">
              {{ chair.roomName }}{{ chair.status !== 1 ? ' · [Không sẵn sàng]' : '' }}{{ chair.claimedByUserId !== null && chair.claimedByUserId !== store.snapshot.userId ? ' · [Đã có nha sĩ chọn]' : '' }}
            </option>
          </select>
        </div>

        <div v-else-if="store.snapshot.isActive && store.snapshot.roleCode === 'ASSISTANT'" class="form-group">
          <label for="dentist-choice-select">Chọn nha sĩ hỗ trợ hôm nay</label>
          <select id="dentist-choice-select" v-model.number="dentistChoice" :disabled="saving || loading || needsReview || !store.snapshot.canChange" @change="markDirty">
            <option :value="0">❌ Bỏ liên kết nha sĩ</option>
            <option v-for="dentist in options?.dentists ?? []" :key="dentist.dentistId" :value="dentist.dentistId" :disabled="!dentist.isReady">
              {{ dentist.dentistName }} · {{ dentist.roomName || 'Chưa nhận phòng' }} (Đã có {{ dentist.assistantCount }} phụ tá){{ !dentist.isReady ? ' · [Chưa sẵn sàng]' : '' }}
            </option>
          </select>
          <p v-if="dentistChoice" class="sync-preview">
            📍 Phòng sẽ tự đồng bộ theo nha sĩ: <strong>{{ selectedDentist?.roomName || 'Chưa nhận phòng' }}</strong>
          </p>
        </div>

        <div v-else class="text-muted">
          Tài khoản đã khóa hoặc đã đổi vai trò. Admin chỉ có thể bỏ liên kết vị trí cũ trước khi gán người khác.
        </div>

        <label class="confirm-checkbox">
          <input v-model="confirmed" type="checkbox" :disabled="saving || loading || needsReview || !store.snapshot.canChange" />
          <span>Tôi đã kiểm tra đúng nhân sự và xác nhận vị trí/nhóm làm việc cho ngày hôm nay.</span>
        </label>

        <button class="btn btn-primary btn-save" :disabled="saving || loading || needsReview || !confirmed || !store.snapshot.canChange">
          {{ saving ? 'Đang lưu…' : 'Xác nhận cập nhật vị trí' }}
        </button>
      </form>
    </section>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useWorkspacesStore } from '@/stores/workspaces';
import { workspacesService, type WorkspaceOptions, type WorkspaceTarget } from '@/services/workspaces';
import type { PagedResult } from '@/services/patients';
const auth = useAuthStore(), store = useWorkspacesStore();
const targetId = ref(0), options = ref<WorkspaceOptions | null>(null), targets = ref<PagedResult<WorkspaceTarget> | null>(null), keyword = ref('');
const loading = ref(false), saving = ref(false), targetsLoading = ref(false), error = ref(''), targetError = ref(''), message = ref(''), needsReview = ref(false);
const chairChoice = ref(0), dentistChoice = ref(0), confirmed = ref(false), dirty = ref(false);
const selectedDentist = computed(() => options.value?.dentists.find(d => d.dentistId === dentistChoice.value));
let life = 0, version = 0, targetVersion = 0, timer: ReturnType<typeof setInterval> | undefined;
const permitted = () => ['ADMIN', 'DENTIST', 'ASSISTANT'].includes(auth.role);
function markDirty() { dirty.value = true; confirmed.value = false; }
async function loadTargets(page = 1) {
  if (auth.role !== 'ADMIN' || saving.value || !Number.isInteger(page) || page < 1 || page > 1_000_000 || keyword.value.length > 100) return;
  const ticket = ++targetVersion; targetsLoading.value = true; targets.value = null; targetError.value = '';
  try { const result = await workspacesService.targets(page, keyword.value.trim() || undefined); if (ticket === targetVersion && auth.role === 'ADMIN') targets.value = result; }
  catch { if (ticket === targetVersion) targetError.value = 'Không thể tải nhân sự. Vui lòng thử lại.'; }
  finally { if (ticket === targetVersion) targetsLoading.value = false; }
}
async function load(id: number, automatic = false) {
  if (!permitted() || saving.value || !Number.isInteger(id) || id <= 0 || (auth.role !== 'ADMIN' && id !== auth.user?.userId) || (automatic && (loading.value || dirty.value || needsReview.value))) return;
  const ticket = ++version; targetId.value = id; loading.value = true; error.value = ''; confirmed.value = false;
  if (!automatic) { store.reset(); options.value = null; dirty.value = false; chairChoice.value = 0; dentistChoice.value = 0; }
  try {
    const [snapshot, choices] = await Promise.all([workspacesService.get(id), workspacesService.options()]);
    if (ticket !== version || !permitted() || (auth.role !== 'ADMIN' && id !== auth.user?.userId)) return;
    if (snapshot.userId !== id) throw new Error('Invalid workspace response');
    store.replace(snapshot); options.value = choices;
    chairChoice.value = snapshot.roleCode === 'DENTIST' && snapshot.isActive ? snapshot.chairId ?? 0 : 0;
    dentistChoice.value = snapshot.roleCode === 'ASSISTANT' && snapshot.isActive ? snapshot.workingDentistId ?? 0 : 0;
    dirty.value = false; needsReview.value = false;
  } catch { if (ticket === version) { store.reset(); options.value = null; error.value = 'Không thể đối chiếu vị trí mới nhất. Tải lại trước khi thao tác.'; needsReview.value = true; } }
  finally { if (ticket === version) loading.value = false; }
}
async function save() {
  const snapshot = store.snapshot;
  if (!permitted() || !snapshot || saving.value || loading.value || needsReview.value || !confirmed.value || !snapshot.canChange ||
      (auth.role !== 'ADMIN' && snapshot.userId !== auth.user?.userId) || !Number.isInteger(snapshot.version) || snapshot.version < 0) return;
  let chairId: number | null = null, dentistId: number | null = null;
  if (snapshot.isActive && snapshot.roleCode === 'DENTIST') {
    if (!Number.isInteger(chairChoice.value) || chairChoice.value < 0) return;
    if (chairChoice.value) { const chair = options.value?.chairs.find(c => c.id === chairChoice.value); if (!chair || !chair.isActive || chair.status !== 1 || (chair.claimedByUserId !== null && chair.claimedByUserId !== snapshot.userId)) { error.value = 'Chọn phòng sẵn sàng chưa có nha sĩ khác sử dụng.'; return; } chairId = chairChoice.value; }
  } else if (snapshot.isActive && snapshot.roleCode === 'ASSISTANT') {
    if (!Number.isInteger(dentistChoice.value) || dentistChoice.value < 0) return;
    if (dentistChoice.value) { const dentist = selectedDentist.value; if (!dentist || !dentist.isReady) { error.value = 'Chọn nha sĩ đang hoạt động.'; return; } dentistId = dentistChoice.value; }
  } else if (auth.role !== 'ADMIN') return;
  const ticket = life, principal = auth.user?.userId; saving.value = true; error.value = ''; message.value = '';
  try {
    const result = await workspacesService.change(snapshot.userId, { chairId, dentistId, expectedVersion: snapshot.version, confirmed: true });
    if (ticket !== life || !permitted() || principal !== auth.user?.userId) return;
    if (result.userId !== snapshot.userId || result.version < snapshot.version) throw new Error('Invalid workspace result');
    message.value = 'Đã lưu phòng/nhóm làm việc thành công. Phụ tá lấy phòng trực tiếp từ nha sĩ.';
    confirmed.value = false; dirty.value = false; needsReview.value = true; saving.value = false; await load(snapshot.userId);
  } catch (err: unknown) {
    if (ticket !== life || principal !== auth.user?.userId) return;
    const response = err as { response?: { data?: { message?: string } } };
    error.value = response?.response?.data?.message || 'Chưa xác minh được kết quả lưu. Tải lại để đối chiếu.'; confirmed.value = false; needsReview.value = true;
  } finally { if (ticket === life) saving.value = false; }
}
onMounted(async () => {
  if (!permitted()) return;
  const ticket = life;
  if (auth.role === 'ADMIN') await loadTargets(); else await load(auth.user?.userId ?? 0);
  if (ticket !== life || !permitted()) return;
  timer = setInterval(() => { if (targetId.value) void load(targetId.value, true); }, 15000);
});
onUnmounted(() => { life++; version++; targetVersion++; if (timer !== undefined) clearInterval(timer); store.reset(); options.value = null; targets.value = null; confirmed.value = false; chairChoice.value = 0; dentistChoice.value = 0; error.value = ''; targetError.value = ''; message.value = ''; });
</script>
<style scoped>
.page { max-width: 1040px; margin: auto; padding: 1.5rem; box-sizing: border-box; color: #1e293b; }
.header-banner { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-banner h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }

.card { border: 1px solid #e2e8f0; border-radius: 12px; margin: 1.25rem 0; padding: 1.5rem; box-sizing: border-box; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.card-header h2 { margin: 0 0 1rem; font-size: 1.15rem; color: #0f172a; }

.search-form { display: flex; gap: 0.75rem; align-items: center; margin-bottom: 1rem; }
.input-wrap { flex: 1; }
.input-wrap input { width: 100%; height: 44px; padding: 0.5rem 0.85rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; box-sizing: border-box; }
.input-wrap input:focus { border-color: #0284c7; outline: none; box-shadow: 0 0 0 3px rgba(2,132,199,0.15); }

.person-list { list-style: none; padding: 0; margin: 0.75rem 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 0.75rem; }
.person-item-btn { width: 100%; min-height: 52px; display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; padding: 0.75rem 1rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; cursor: pointer; text-align: left; }
.person-item-btn:hover:not(:disabled) { background: #f0f9ff; border-color: #bae6fd; }
.person-name { font-weight: 600; color: #1e293b; font-size: 0.95rem; }
.badge-role { font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.5rem; border-radius: 4px; background: #e0f2fe; color: #0369a1; }
.badge-locked { font-size: 0.75rem; padding: 0.2rem 0.5rem; border-radius: 4px; background: #fee2e2; color: #b91c1c; }

.pagination-bar { display: flex; justify-content: space-between; align-items: center; margin-top: 1rem; padding-top: 0.75rem; border-top: 1px solid #f1f5f9; flex-wrap: wrap; gap: 0.5rem; }
.page-info { font-size: 0.875rem; color: #64748b; }

.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0369a1; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-outline:hover:not(:disabled) { background: #f8fafc; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }

.alert { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin: 0.75rem 0; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.alert-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; }

.status-box { text-align: center; padding: 2rem; background: #f8fafc; border-radius: 12px; color: #64748b; margin: 1rem 0; }
.spinner { width: 28px; height: 28px; border: 3px solid #e2e8f0; border-top-color: #0284c7; border-radius: 50%; animation: spin 0.8s linear infinite; margin: 0 auto 0.5rem; }
@keyframes spin { to { transform: rotate(360deg); } }

.refresh-wrap { display: flex; justify-content: flex-end; margin-bottom: 0.75rem; }

.profile-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; border-bottom: 1px solid #f1f5f9; padding-bottom: 1rem; margin-bottom: 1.25rem; }
.profile-header h2 { margin: 0; font-size: 1.35rem; color: #0f172a; }
.badge-role-lg { display: inline-block; font-size: 0.8rem; font-weight: 700; padding: 0.25rem 0.65rem; border-radius: 6px; background: #0284c7; color: #fff; margin-top: 0.35rem; }
.coworker-chip { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; padding: 0.5rem 0.85rem; border-radius: 8px; font-size: 0.9rem; }

.location-status { margin: 1rem 0 1.25rem; }
.location-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; display: flex; align-items: center; gap: 0.75rem; }
.location-active { background: #f0fdf4; border-color: #86efac; }
.loc-icon { font-size: 1.25rem; }
.loc-label { font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.05em; color: #64748b; font-weight: 600; }
.loc-val { font-size: 1.1rem; font-weight: 700; color: #0f172a; margin-top: 0.15rem; }
.unconfirmed-warning { background: #fffbeb; border: 1px solid #fde68a; color: #92400e; padding: 0.85rem 1rem; border-radius: 8px; font-weight: 500; font-size: 0.95rem; }
.empty-dentist-prompt { background: #f1f5f9; padding: 0.85rem 1rem; border-radius: 8px; color: #475569; font-size: 0.9rem; }

.workspace-form { display: flex; flex-direction: column; gap: 1.25rem; }
.form-group { display: flex; flex-direction: column; gap: 0.4rem; }
.form-group label { font-size: 0.9rem; font-weight: 600; color: #334155; }
.form-group select { height: 44px; padding: 0.5rem 0.85rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; background: #fff; }
.sync-preview { font-size: 0.85rem; color: #0284c7; margin: 0.25rem 0 0; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.875rem; color: #334155; cursor: pointer; }
.confirm-checkbox input { margin-top: 2px; width: 18px; height: 18px; }
.btn-save { width: 100%; height: 48px; font-size: 1rem; }

@media (max-width: 640px) {
  .page { padding: 1rem; }
  .card { padding: 1rem; }
  .search-form { flex-direction: column; }
  .btn { width: 100%; }
  .profile-header { flex-direction: column; align-items: flex-start; }
}
</style>
