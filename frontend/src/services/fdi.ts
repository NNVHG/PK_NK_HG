import { apiClient } from '@/services/api';

export const FDI_CONDITIONS = {
  CARIES: 'Sâu răng', RESTORED: 'Đã trám', MISSING: 'Mất răng', CROWN: 'Mão răng',
  PULPITIS: 'Viêm tủy', IMPLANT: 'Implant', CALCULUS: 'Vôi răng', NORMAL: 'Bình thường',
  GINGIVITIS: 'Viêm nướu', PERIODONTITIS: 'Viêm nha chu', IMPACTED: 'Răng mọc lệch/ngầm'
} as const;
export type ConditionCode = keyof typeof FDI_CONDITIONS;
export interface ToothCondition {
  id: number; visitId: number; toothNumber: number; surface: string | null;
  conditionCode: ConditionCode; note: string | null; createdAt: string;
}
export interface ConditionServiceSuggestion { serviceCode: string; label: string; }
export const fdiService = {
  async get(visitId: number): Promise<ToothCondition[]> {
    return (await apiClient.get<ToothCondition[]>(`/visits/${visitId}/tooth-conditions`)).data;
  },
  async add(visitId: number, toothNumber: number, surface: string | null, conditionCode: ConditionCode, note: string | null = null): Promise<ToothCondition> {
    return (await apiClient.post<ToothCondition>(`/visits/${visitId}/tooth-conditions`, { toothNumber, surface, conditionCode, note })).data;
  },
  async update(visitId: number, conditionId: number, toothNumber: number, surface: string | null, conditionCode: ConditionCode, note: string | null = null): Promise<ToothCondition> {
    return (await apiClient.put<ToothCondition>(`/visits/${visitId}/tooth-conditions/${conditionId}`, { toothNumber, surface, conditionCode, note })).data;
  },
  async delete(visitId: number, conditionId: number): Promise<void> {
    await apiClient.delete(`/visits/${visitId}/tooth-conditions/${conditionId}`);
  },
  async serviceSuggestions(conditionCode: ConditionCode): Promise<ConditionServiceSuggestion[]> {
    return (await apiClient.get<ConditionServiceSuggestion[]>('/fdi/condition-service-suggestions', { params: { conditionCode } })).data;
  },
};
