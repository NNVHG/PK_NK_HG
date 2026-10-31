<template>
  <main class="page">
    <h1>Vị trí nha sĩ và phụ tá</h1>
    <p>Nha sĩ chọn ghế/phòng hiện tại và xác nhận mỗi ngày; phụ tá chọn nha sĩ làm cùng và tự theo phòng của nha sĩ. Vị trí được giữ đến khi đổi hoặc bỏ chọn.</p>
    <section v-if="auth.role === 'ADMIN'" class="card">
      <h2>Nhân sự cần điều phối</h2>
      <form @submit.prevent="loadTargets(1)"><label>Tìm tên nhân sự <input v-model="keyword" maxlength="100" :disabled="saving" /></label><button :disabled="saving || targetsLoading">Tìm</button></form>
      <p v-if="targetError" role="alert">{{ targetError }}</p>
      <ul><li v-for="person in targets?.items ?? []" :key="person.userId"><button :disabled="saving || loading" @click="load(person.userId)">{{ person.fullName }} · {{ person.roleCode }}{{ person.isActive ? '' : ' · Đã khóa' }}</button></li></ul>
      <nav v-if="targets"><button :disabled="saving || targetsLoading || targets.page <= 1" @click="loadTargets(targets.page - 1)">Trang trước</button><span>Trang {{ targets.page }} · {{ targets.totalCount }} người</span><button :disabled="saving || targetsLoading || !targets.hasNextPage" @click="loadTargets(targets.page + 1)">Trang sau</button></nav>
    </section>
    <p v-if="loading" role="status">Đang đối chiếu vị trí hiện tại…</p>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
    <p v-if="needsReview" role="alert">Tải lại thành công và đối chiếu vị trí trước khi thao tác tiếp.</p>
    <button v-if="targetId" :disabled="loading || saving" @click="load(targetId)">Tải lại vị trí</button>
    <section v-if="store.snapshot" class="card" aria-labelledby="workspace-title">
      <h2 id="workspace-title">{{ store.snapshot.fullName }} · {{ store.snapshot.roleCode }}</h2>
      <p v-if="store.snapshot.workingDentistId && store.snapshot.roleCode === 'ASSISTANT'">Nha sĩ làm cùng: {{ store.snapshot.dentistName }} · #{{ store.snapshot.workingDentistId }}</p>
      
      <div v-if="store.snapshot.roleCode === 'DENTIST'" class="location-status">
        <p v-if="store.snapshot.isLocationUsable && store.snapshot.chairId">Vị trí đã xác nhận hôm nay: <strong>{{ store.snapshot.roomName || 'Chưa có phòng' }} · {{ store.snapshot.chairNumber || 'Chưa có ghế' }}</strong></p>
        <p v-else class="unconfirmed-warning" role="alert">Chưa xác nhận phòng làm việc hôm nay. Vui lòng chọn ghế/phòng bên dưới và xác nhận vị trí cho ngày làm việc.</p>
      </div>
      <div v-else-if="store.snapshot.roleCode === 'ASSISTANT'" class="location-status">
        <template v-if="store.snapshot.workingDentistId">
          <p v-if="store.snapshot.isLocationUsable && store.snapshot.roomName">Vị trí theo nha sĩ hôm nay: <strong>{{ store.snapshot.roomName }} · {{ store.snapshot.chairNumber || 'Chưa có ghế' }}</strong></p>
          <p v-else class="unconfirmed-warning" role="alert">Nha sĩ ({{ store.snapshot.dentistName }}) chưa xác nhận phòng làm việc hôm nay. Phòng sẽ tự động hiển thị sau khi nha sĩ xác nhận vị trí trong ngày.</p>
        </template>
        <p v-else>Chưa liên kết nha sĩ. Vui lòng chọn nha sĩ làm cùng bên dưới.</p>
      </div>
      <p v-else>Vị trí hiện tại: <strong>{{ store.snapshot.roomName || 'Chưa có phòng' }} · {{ store.snapshot.chairNumber || 'Chưa có ghế' }}</strong></p>

      <p v-if="store.snapshot.roleCode === 'ASSISTANT'">Phòng được lấy trực tiếp từ vị trí nha sĩ, cập nhật khi tải lại hoặc đồng bộ định kỳ. Phụ tá không chọn phòng riêng.</p>
      <p v-if="!store.snapshot.canChange" role="alert">Nha sĩ đang có ca khám hoặc lời mời chưa xử lý. Hoàn tất bước hiện tại trước khi đổi vị trí.</p>
      <form @submit.prevent="save">
        <label v-if="store.snapshot.isActive && store.snapshot.roleCode === 'DENTIST'">Ghế/phòng làm việc hôm nay
          <select v-model.number="chairChoice" :disabled="saving || loading || needsReview || !store.snapshot.canChange" @change="markDirty">
            <option :value="0">Bỏ chọn vị trí</option><option v-for="chair in options?.chairs ?? []" :key="chair.id" :value="chair.id" :disabled="!chair.isActive || chair.status !== 1 || (chair.claimedByUserId !== null && chair.claimedByUserId !== store.snapshot.userId)">{{ chair.roomName }} · {{ chair.chairNumber }}{{ chair.status !== 1 ? ' · Không sẵn sàng' : '' }}{{ chair.claimedByUserId !== null && chair.claimedByUserId !== store.snapshot.userId ? ' · Đã có nha sĩ chọn hôm nay' : '' }}</option>
          </select>
        </label>
        <label v-else-if="store.snapshot.isActive && store.snapshot.roleCode === 'ASSISTANT'">Nha sĩ làm cùng
          <select v-model.number="dentistChoice" :disabled="saving || loading || needsReview || !store.snapshot.canChange" @change="markDirty">
            <option :value="0">Bỏ liên kết nha sĩ</option><option v-for="dentist in options?.dentists ?? []" :key="dentist.dentistId" :value="dentist.dentistId" :disabled="!dentist.isReady">{{ dentist.dentistName }} · {{ dentist.roomName || 'Chưa có phòng' }} · {{ dentist.chairNumber || 'Chưa có ghế' }} · {{ dentist.assistantCount }} phụ tá{{ !dentist.isReady ? ' · Chưa sẵn sàng' : '' }}</option>
          </select>
        </label>
        <p v-else>Tài khoản đã khóa hoặc đã đổi vai trò. Admin chỉ có thể bỏ liên kết vị trí cũ trước khi gán người khác.</p>
        <p v-if="store.snapshot.roleCode === 'ASSISTANT' && dentistChoice">Phòng sẽ theo nha sĩ: <strong>{{ selectedDentist?.roomName || 'Chưa có phòng' }} · {{ selectedDentist?.chairNumber || 'Chưa có ghế' }}</strong></p>
        <label><input v-model="confirmed" type="checkbox" :disabled="saving || loading || needsReview || !store.snapshot.canChange" /> Tôi đã đối chiếu đúng nhân sự và xác nhận vị trí/nhóm làm việc cho ngày hôm nay.</label>
        <button :disabled="saving || loading || needsReview || !confirmed || !store.snapshot.canChange">{{ saving ? 'Đang lưu…' : 'Xác nhận lưu' }}</button>
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
    if (chairChoice.value) { const chair = options.value?.chairs.find(c => c.id === chairChoice.value); if (!chair || !chair.isActive || chair.status !== 1 || (chair.claimedByUserId !== null && chair.claimedByUserId !== snapshot.userId)) { error.value = 'Chọn ghế sẵn sàng chưa có nha sĩ khác sử dụng.'; return; } chairId = chairChoice.value; }
  } else if (snapshot.isActive && snapshot.roleCode === 'ASSISTANT') {
    if (!Number.isInteger(dentistChoice.value) || dentistChoice.value < 0) return;
    if (dentistChoice.value) { const dentist = selectedDentist.value; if (!dentist || !dentist.isReady) { error.value = 'Chọn nha sĩ đang hoạt động.'; return; } dentistId = dentistChoice.value; }
  } else if (auth.role !== 'ADMIN') return;
  const ticket = life, principal = auth.user?.userId; saving.value = true; error.value = ''; message.value = '';
  try {
    const result = await workspacesService.change(snapshot.userId, { chairId, dentistId, expectedVersion: snapshot.version, confirmed: true });
    if (ticket !== life || !permitted() || principal !== auth.user?.userId) return;
    if (result.userId !== snapshot.userId || result.version < snapshot.version) throw new Error('Invalid workspace result');
    message.value = 'Đã lưu vị trí/nhóm làm việc. Phụ tá lấy phòng trực tiếp từ nha sĩ.';
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
.page { max-width: 940px; margin: auto; padding: 1.5rem; box-sizing: border-box; }
.card { border: 1px solid #cbd5e1; border-radius: .6rem; margin: 1rem 0; padding: 1rem; box-sizing: border-box; }
.location-status { margin: 0.75rem 0; }
.unconfirmed-warning { color: #b45309; background-color: #fffbeb; border: 1px solid #fde68a; padding: 0.6rem 0.8rem; border-radius: 0.4rem; font-weight: 500; }
form { display: grid; gap: .8rem; } label { display: grid; gap: .35rem; max-width: 100%; }
button, select, input { font: inherit; padding: .5rem; max-width: 100%; box-sizing: border-box; }
button { cursor: pointer; }
nav { display: flex; gap: .7rem; margin-top: .75rem; align-items: center; flex-wrap: wrap; }
ul { list-style: none; padding: 0; margin: 0; }
li { margin: .4rem 0; max-width: 100%; }
li button { width: 100%; text-align: left; white-space: normal; overflow-wrap: break-word; word-break: break-word; }
[role=alert] { color: #b91c1c; overflow-wrap: break-word; }
[role=status] { color: #166534; overflow-wrap: break-word; }

@media (max-width: 480px) {
  .page { padding: 0.75rem; }
  .card { padding: 0.75rem; margin: 0.75rem 0; }
  button, select, input { padding: 0.45rem; }
}
</style>
