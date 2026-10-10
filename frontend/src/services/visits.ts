import { apiClient } from '@/services/api';
import type { VisitDetails } from '@/services/patients';

export interface DiagnosisPayload {
  diagnosis: string;
  clinicalNotes: string | null;
}

export const visitsService = {
  async getById(visitId: number): Promise<VisitDetails> {
    const response = await apiClient.get<VisitDetails>(`/visits/${visitId}`);
    return response.data;
  },
  async updateDiagnosis(visitId: number, payload: DiagnosisPayload): Promise<VisitDetails> {
    const response = await apiClient.put<VisitDetails>(`/visits/${visitId}/diagnosis`, payload);
    return response.data;
  }
};
