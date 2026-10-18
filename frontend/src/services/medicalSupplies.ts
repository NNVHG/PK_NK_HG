import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';

export interface MedicalSupply {
  id: number;
  code: string;
  name: string;
  unit: string;
  minimumStockThreshold: number;
  description: string | null;
  isActive: boolean;
  version: number;
  createdAt: string;
  updatedAt: string | null;
  createdByUserId: number;
  updatedByUserId: number | null;
}

export interface MedicalSupplyWrite {
  code: string;
  name: string;
  unit: string;
  minimumStockThreshold: number;
  description: string | null;
  isActive: boolean;
  expectedVersion: number;
}

export const medicalSuppliesService = {
  async list(keyword = '', page = 1, includeInactive = false): Promise<PagedResult<MedicalSupply>> {
    return (await apiClient.get<PagedResult<MedicalSupply>>('/master/medical-supplies', { params: { keyword, page, pageSize: 20, includeInactive } })).data;
  },
  async save(id: number | null, payload: MedicalSupplyWrite): Promise<MedicalSupply> {
    return id === null
      ? (await apiClient.post<MedicalSupply>('/master/medical-supplies', payload)).data
      : (await apiClient.put<MedicalSupply>(`/master/medical-supplies/${id}`, payload)).data;
  },
  async deactivate(id: number, expectedVersion: number): Promise<MedicalSupply> {
    return (await apiClient.delete<MedicalSupply>(`/master/medical-supplies/${id}`, { params: { expectedVersion } })).data;
  }
};
