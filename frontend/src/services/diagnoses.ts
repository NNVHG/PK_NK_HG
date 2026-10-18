import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface Diagnosis { id: number; code: string; name: string; description: string | null; category: string; isActive: boolean; version: number; }
export interface DiagnosisWrite { code: string; name: string; description: string | null; category: string; isActive: boolean; expectedVersion: number; }
export const diagnosesService = {
  async list(keyword = '', page = 1, includeInactive = false): Promise<PagedResult<Diagnosis>> {
    return (await apiClient.get<PagedResult<Diagnosis>>('/master/diagnoses', { params: { keyword, page, pageSize: 20, includeInactive } })).data;
  },
  async save(id: number | null, payload: DiagnosisWrite): Promise<Diagnosis> {
    return id === null ? (await apiClient.post<Diagnosis>('/master/diagnoses', payload)).data : (await apiClient.put<Diagnosis>(`/master/diagnoses/${id}`, payload)).data;
  }
};
