import { apiClient } from '@/services/api';
import { patientsService, type VisitDetails } from '@/services/patients';
import { isUnderFourteen } from '@/services/patientAge';
import type { DentitionType } from '@/types/fdi';

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
  async getDefaultDentition(visit: VisitDetails): Promise<DentitionType> {
    const { dateOfBirth } = await patientsService.getById(visit.patientId);
    const visitDate = new Date(visit.startedAt ?? visit.createdAt).toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
    return isUnderFourteen(dateOfBirth, visitDate) ? 'child' : 'adult';
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

