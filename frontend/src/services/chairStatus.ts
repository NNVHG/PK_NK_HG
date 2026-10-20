import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';

export interface ChairStatusRow {
  id: number; chairNumber: string; roomName: string; status: number; statusText: string;
  isActive: boolean; version: number; isAssigned: boolean; hasActiveVisit: boolean;
}
export interface ChairStatusQuery { keyword?: string; status?: number; page: number; pageSize: number; }
export interface ChangeChairStatus { status: number; expectedVersion: number; confirmed: boolean; }
export const chairStatusService = {
  async getPage(query: ChairStatusQuery): Promise<PagedResult<ChairStatusRow>> {
    return (await apiClient.get<PagedResult<ChairStatusRow>>('/chair-status', { params: query })).data;
  },
  async change(id: number, input: ChangeChairStatus): Promise<ChairStatusRow> {
    return (await apiClient.put<ChairStatusRow>(`/chair-status/${id}`, input)).data;
  }
};
