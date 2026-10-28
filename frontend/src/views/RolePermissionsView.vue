<template>
  <main v-if="auth.role === 'ADMIN'" class="permissions-page">
    <nav aria-label="Quản lý tài khoản và quyền">
      <RouterLink to="/management/admin/staff">Tài khoản nhân viên</RouterLink>
      <span aria-hidden="true"> · </span>
      <RouterLink to="/management/admin/role-permissions" aria-current="page">Ma trận quyền (chỉ đọc)</RouterLink>
    </nav>
    <h1>Ma trận quyền theo vai trò</h1>
    <p>Phase 1 sử dụng 5 vai trò cố định. Bảng này chỉ đọc; gán vai trò cho nhân viên tại <RouterLink to="/management/admin/staff">Tài khoản nhân viên</RouterLink>.</p>
    <p>“Có theo vai trò” vẫn cần đáp ứng điều kiện của hồ sơ, nha sĩ phụ trách và trạng thái nghiệp vụ. Quyền của từng bước chuyển hàng đợi được kiểm tra riêng.</p>
    <button :disabled="loading" @click="load">{{ loading ? 'Đang tải…' : 'Tải lại ma trận quyền' }}</button>
    <p v-if="loading" role="status">Đang đọc cấu hình quyền hiện hành…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <template v-if="matrix">
      <label>Lọc module <select v-model="moduleFilter"><option value="">Tất cả</option><option v-for="module in modules" :key="module" :value="module">{{ module }}</option></select></label>
      <p>{{ visiblePermissions.length }} nhóm quyền · {{ matrix.roles.length }} vai trò</p>
      <div class="table-container" tabindex="0" aria-label="Ma trận quyền có thể cuộn ngang">
        <table>
          <caption>Quyền cố định đang áp dụng trong hệ thống</caption>
          <thead><tr><th scope="col">Nghiệp vụ</th><th v-for="role in matrix.roles" :key="role.code" scope="col">{{ role.name }}</th><th scope="col">Điều kiện bổ sung</th></tr></thead>
          <tbody><tr v-for="permission in visiblePermissions" :key="permission.code">
            <th scope="row">{{ permission.name }}<small>{{ permission.module }}</small></th>
            <td v-for="role in matrix.roles" :key="role.code">{{ permission.allowedRoles.includes(role.code) ? 'Có theo vai trò' : '—' }}</td>
            <td>{{ permission.conditions }}</td>
          </tr></tbody>
        </table>
      </div>
      <p v-if="!visiblePermissions.length">Không có nhóm quyền trong module đang chọn.</p>
    </template>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { storeToRefs } from 'pinia';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { useRolePermissionsStore } from '@/stores/rolePermissions';
import { rolePermissionsApi } from '@/services/rolePermissions';

const auth = useAuthStore(), store = useRolePermissionsStore();
const { matrix } = storeToRefs(store);
const loading = ref(false), error = ref(''), moduleFilter = ref('');
const modules = computed(() => [...new Set(matrix.value?.permissions.map(x => x.module) ?? [])].sort());
const visiblePermissions = computed(() => matrix.value?.permissions.filter(x => !moduleFilter.value || x.module === moduleFilter.value) ?? []);
let version = 0;
async function load() {
  if (auth.role !== 'ADMIN') { version++; store.replace(null); loading.value = false; error.value = ''; return; }
  if (loading.value) return;
  const ticket = ++version; loading.value = true; error.value = ''; moduleFilter.value = ''; store.replace(null);
  try {
    const result = await rolePermissionsApi.get();
    if (ticket !== version || auth.role !== 'ADMIN') return;
    if (!result.isReadOnly || result.roles.length !== 5 || !result.permissions.length) throw new Error('Invalid matrix');
    store.replace(result);
  } catch (cause) {
    if (ticket === version) error.value = axios.isAxiosError(cause)
      ? cause.response?.data?.message || 'Không thể tải ma trận quyền. Vui lòng tải lại.' : 'Cấu hình ma trận quyền chưa sẵn sàng. Vui lòng tải lại.';
  } finally { if (ticket === version) loading.value = false; }
}
onMounted(load);
onUnmounted(() => { version++; store.replace(null); moduleFilter.value = ''; error.value = ''; });
</script>

<style scoped>
.permissions-page { max-width: 1250px; margin: 1rem auto; }
button, select { padding: .5rem; margin: .5rem; }
.table-container { overflow-x: auto; }
table { border-collapse: collapse; min-width: 900px; width: 100%; }
caption { text-align: left; font-weight: 600; margin: .75rem 0; }
th, td { border: 1px solid #cbd5e1; padding: .65rem; text-align: left; vertical-align: top; }
thead th { background: #eef2f7; }
small { display: block; color: #475569; margin-top: .3rem; }
.error { color: #b91c1c; }
</style>
