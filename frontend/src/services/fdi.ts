import { apiClient } from '@/services/api';

export const FDI_CONDITIONS = {
  CARIES: 'Sâu răng', RESTORED: 'Đã trám', MISSING: 'Mất răng', CROWN: 'Mão răng',
  PULPITIS: 'Viêm tủy', IMPLANT: 'Implant', CALCULUS: 'Vôi răng', NORMAL: 'Bình thường'
} as const;
export type ConditionCode = keyof typeof FDI_CONDITIONS;
export interface ToothCondition {
  id: number; visitId: number; toothNumber: number; surface: string | null;
  conditionCode: ConditionCode; note: string | null; createdAt: string;
}
export const fdiService = {
  async get(visitId: number): Promise<ToothCondition[]> {
    return (await apiClient.get<ToothCondition[]>(`/visits/${visitId}/tooth-conditions`)).data;
  },
  async add(visitId: number, toothNumber: number, surface: string | null, conditionCode: ConditionCode): Promise<ToothCondition> {
    return (await apiClient.post<ToothCondition>(`/visits/${visitId}/tooth-conditions`, { toothNumber, surface, conditionCode })).data;
  }
};
