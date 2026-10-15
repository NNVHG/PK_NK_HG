import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { RolePermissionMatrix } from '@/services/rolePermissions';

export const useRolePermissionsStore = defineStore('MOD_AUTH_permissions', () => {
  const matrix = ref<RolePermissionMatrix | null>(null);
  function replace(value: RolePermissionMatrix | null) { matrix.value = value; }
  return { matrix, replace };
});
