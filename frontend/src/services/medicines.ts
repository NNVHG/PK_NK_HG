import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface Medicine { id: number; code: string; name: string; activeIngredient: string | null; strength: string | null; unit: string; unitPrice: string; defaultUsageInstruction: string | null; isActive: boolean; version: number; createdAt: string; updatedAt: string | null }
export interface MedicineWrite { code: string; name: string; activeIngredient: string | null; strength: string | null; unit: string; unitPrice: string; defaultUsageInstruction: string | null; isActive: boolean; expectedVersion: number }
export const medicinesService = {
  async list(keyword = '', page = 1, includeInactive = true): Promise<PagedResult<Medicine>> {
    return (await apiClient.get<PagedResult<Medicine>>('/medicines', { params: { keyword, page, pageSize: 20, includeInactive } })).data;
  },
  async save(id: number | null, payload: MedicineWrite): Promise<Medicine> {
    return id === null ? (await apiClient.post<Medicine>('/medicines', payload)).data : (await apiClient.put<Medicine>(`/medicines/${id}`, payload)).data;
  },
  async deactivate(id: number, expectedVersion: number): Promise<Medicine> {
    return (await apiClient.delete<Medicine>(`/medicines/${id}`, { params: { expectedVersion } })).data;
  }
};
