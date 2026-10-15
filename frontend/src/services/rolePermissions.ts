import { apiClient } from '@/services/api';

export interface PermissionRole { code: string; name: string; }
export interface RolePermission { code: string; name: string; module: string; allowedRoles: string[]; conditions: string; }
export interface RolePermissionMatrix { roles: PermissionRole[]; permissions: RolePermission[]; isReadOnly: boolean; }
export const rolePermissionsApi = {
  async get(): Promise<RolePermissionMatrix> {
    return (await apiClient.get<RolePermissionMatrix>('/admin/role-permissions')).data;
  },
};
