import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface ToothHistoryItem {
  id: number; eventType: string; visitId: number; visitDate: string; createdAt: string;
  dentistName: string | null; toothNumber: number | null; conditionCode: string | null; surface: string | null; note: string | null;
  services: { assignmentId: number; code: string; name: string; quantity: number; unitPrice: number; surface: string | null }[];
}
export const toothHistoryApi = {
  async get(patientId: number, toothNumber: number | null, page = 1): Promise<PagedResult<ToothHistoryItem>> {
    const path = toothNumber === null ? `/patients/${patientId}/treatment-timeline` : `/patients/${patientId}/teeth/${toothNumber}/history`;
    return (await apiClient.get<PagedResult<ToothHistoryItem>>(path, { params: { page, pageSize: 20 } })).data;
  }
};
