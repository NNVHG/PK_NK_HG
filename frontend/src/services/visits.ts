import { apiClient } from '@/services/api';
import type { VisitDetails } from '@/services/patients';

export interface DiagnosisPayload {
  diagnosis: string;
  clinicalNotes: string | null;
}

export interface PostTreatmentNote {
  id: number;
  visitId: number;
  createdByUserId: number;
  createdByFullName: string;
  createdByRoleName: string;
  note: string;
  createdAt: string;
  requestId?: string | null;
}

export interface CreatePostTreatmentNotePayload {
  note: string;
  requestId?: string | null;
}

export const visitsService = {
  async getById(visitId: number): Promise<VisitDetails> {
    const response = await apiClient.get<VisitDetails>(`/visits/${visitId}`);
    return response.data;
  },
  async updateDiagnosis(visitId: number, payload: DiagnosisPayload): Promise<VisitDetails> {
    const response = await apiClient.put<VisitDetails>(`/visits/${visitId}/diagnosis`, payload);
    return response.data;
  },
  async getPostTreatmentNotes(visitId: number): Promise<PostTreatmentNote[]> {
    const response = await apiClient.get<PostTreatmentNote[]>(`/visits/${visitId}/clinical-notes/addendum`);
    return response.data;
  },
  async addPostTreatmentNote(visitId: number, payload: CreatePostTreatmentNotePayload): Promise<PostTreatmentNote> {
    const response = await apiClient.post<PostTreatmentNote>(`/visits/${visitId}/clinical-notes/addendum`, payload);
    return response.data;
  }
};

