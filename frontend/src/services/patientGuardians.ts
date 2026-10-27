import { apiClient } from '@/services/api';

export interface PatientGuardianSummary {
  patientGuardianId: number;
  guardianPatientId: number;
  guardianPatientCode: string;
  guardianFullName: string;
  wardPatientId: number;
  wardPatientCode: string;
  wardFullName: string;
  relationship: string;
  isActive: boolean;
  assignedAt: string;
  assignedByUserId: number;
  assignedByUserName: string;
  revokedAt: string | null;
  revokedByUserId: number | null;
  revokedByUserName: string | null;
  notes: string | null;
}

export interface WardSummary {
  wardPatientId: number;
  wardPatientCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string | null;
  relationship: string;
  assignedAt: string;
}

export interface AssignGuardianPayload {
  guardianPatientId: number;
  wardPatientId: number;
  relationship: string;
  notes?: string | null;
}

export interface PromoteWardPayload {
  rawCitizenId: string;
  phone?: string | null;
  notes?: string | null;
}

export const patientGuardiansService = {
  async assign(payload: AssignGuardianPayload): Promise<PatientGuardianSummary> {
    return (await apiClient.post<PatientGuardianSummary>('/patient-guardians/assign', payload)).data;
  },

  async revoke(wardPatientId: number, current: PatientGuardianSummary, notes?: string | null): Promise<PatientGuardianSummary> {
    return (await apiClient.post<PatientGuardianSummary>(`/patient-guardians/wards/${wardPatientId}/revoke`, {
      patientGuardianId: current.patientGuardianId,
      expectedAssignedAt: current.assignedAt,
      notes
    })).data;
  },

  async promoteIndependent(wardPatientId: number, payload: PromoteWardPayload): Promise<any> {
    return (await apiClient.post(`/patient-guardians/wards/${wardPatientId}/promote-independent`, payload)).data;
  },

  async getActiveGuardian(wardPatientId: number): Promise<PatientGuardianSummary> {
    return (await apiClient.get<PatientGuardianSummary>(`/patient-guardians/wards/${wardPatientId}/active`)).data;
  },

  async getHistory(wardPatientId: number): Promise<PatientGuardianSummary[]> {
    return (await apiClient.get<PatientGuardianSummary[]>(`/patient-guardians/wards/${wardPatientId}/history`)).data;
  },

  async getMyWards(): Promise<WardSummary[]> {
    return (await apiClient.get<WardSummary[]>('/patient-guardians/my-wards')).data;
  }
};
