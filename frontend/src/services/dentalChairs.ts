import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface DentalChair { id: number; chairNumber: string; roomName: string; status: number; statusText: string; description: string | null; isActive: boolean; version: number; }
export interface ChairQuery { keyword?: string; status?: number; isActive?: boolean; page: number; pageSize: number; }
export interface ChairInput { roomName: string; status: number; description: string | null; confirmed: boolean; }
export const dentalChairsService = {
  async getPage(query: ChairQuery): Promise<PagedResult<DentalChair>> { return (await apiClient.get<PagedResult<DentalChair>>('/master/chairs', { params: query })).data; },
  async create(input: ChairInput & { chairNumber: string }): Promise<DentalChair> { return (await apiClient.post<DentalChair>('/master/chairs', input)).data; },
  async update(id: number, input: ChairInput & { expectedVersion: number }): Promise<DentalChair> { return (await apiClient.put<DentalChair>(`/master/chairs/${id}`, input)).data; }
};
