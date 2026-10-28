import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface WorkspaceSnapshot { userId: number; fullName: string; roleCode: string; isActive: boolean; version: number; workingDentistId: number | null; dentistName: string | null; chairId: number | null; chairNumber: string | null; roomName: string | null; isLocationUsable: boolean; canChange: boolean; }
export interface WorkspaceChair { id: number; chairNumber: string; roomName: string; status: number; isActive: boolean; claimedByUserId: number | null; }
export interface WorkspaceDentist { dentistId: number; dentistName: string; chairId: number | null; chairNumber: string | null; roomName: string | null; assistantCount: number; isReady: boolean; }
export interface WorkspaceOptions { chairs: WorkspaceChair[]; dentists: WorkspaceDentist[]; }
export interface WorkspaceTarget { userId: number; fullName: string; roleCode: string; isActive: boolean; }
export interface ChangeWorkspace { chairId: number | null; dentistId: number | null; expectedVersion: number; confirmed: boolean; }
export const workspacesService = {
  async get(id: number): Promise<WorkspaceSnapshot> { return (await apiClient.get<WorkspaceSnapshot>(`/workspaces/${id}`)).data; },
  async options(): Promise<WorkspaceOptions> { return (await apiClient.get<WorkspaceOptions>('/workspaces/options')).data; },
  async targets(page = 1, keyword?: string): Promise<PagedResult<WorkspaceTarget>> { return (await apiClient.get<PagedResult<WorkspaceTarget>>('/workspaces/staff', { params: { page, keyword } })).data; },
  async change(id: number, input: ChangeWorkspace): Promise<WorkspaceSnapshot> { return (await apiClient.put<WorkspaceSnapshot>(`/workspaces/${id}`, input)).data; }
};
