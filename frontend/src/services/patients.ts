import { apiClient } from '@/services/api';

export interface PatientSummary {
  patientId: number;
  patientCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string | null;
  phone: string;
}

export interface PatientDetails extends PatientSummary {
  patientId: number;
  email: string | null;
  address: string | null;
  isActive: boolean;
}

export interface PatientDuplicateInfo {
  patientCode: string;
  fullName: string;
  dateOfBirth: string;
  phone: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PatientQuery {
  keyword?: string;
  page: number;
  pageSize: number;
}

export interface PatientPayload {
  fullName: string;
  dateOfBirth: string;
  gender: string | null;
  phone: string;
  email: string | null;
  address: string | null;
  confirmNotDuplicate?: boolean;
}

export interface PatientApiProblem {
  code?: string;
  message?: string;
  errors?: Array<{ field?: string; message?: string }>;
  duplicates?: PatientDuplicateInfo[];
}

export interface PatientSafetyAlert {
  type: string;
  name: string;
  detail: string | null;
  recordedAt: string;
  visitId: number;
}

export interface PatientSafetyAlerts {
  hasHistory: boolean;
  alerts: PatientSafetyAlert[];
}

export interface PatientTimelineItem {
  visitId: number;
  status: string;
  startedAt: string | null;
  endedAt: string | null;
  dentistName: string | null;
  hasMedicalHistory: boolean;
  hasVitalSigns: boolean;
}

export interface MedicalHistoryItem {
  itemId: number;
  type: string;
  name: string;
  isCritical: boolean;
  detail: string | null;
}

export interface MedicalHistoryRecord {
  recordId: number;
  patientId: number;
  visitId: number;
  note: string | null;
  recordedByUserId: number;
  createdAt: string;
  items: MedicalHistoryItem[];
}

export interface MedicalHistoryItemPayload {
  type: 'Allergy' | 'Condition';
  name: string;
  isCritical: boolean;
  detail: string | null;
}

export interface MedicalHistoryPayload {
  note: string | null;
  items: MedicalHistoryItemPayload[];
}

export interface VitalSignRecord {
  vitalSignRecordId: number;
  patientId: number;
  visitId: number;
  systolicBp: number | null;
  diastolicBp: number | null;
  pulseBpm: number | null;
  temperatureC: number | null;
  note: string | null;
  recordedByUserId: number;
  createdAt: string;
}

export interface VisitDetails {
  visitId: number;
  patientId: number;
  status: string;
  startedAt: string | null;
  endedAt: string | null;
  dentistId: number | null;
  createdByUserId: number;
  createdAt: string;
}

export const patientsService = {
  async search(query: PatientQuery): Promise<PagedResult<PatientSummary>> {
    const response = await apiClient.get<PagedResult<PatientSummary>>('/patients', { params: query });
    return response.data;
  },

  async getById(patientId: number): Promise<PatientDetails> {
    const response = await apiClient.get<PatientDetails>(`/patients/${patientId}`);
    return response.data;
  },

  async create(payload: PatientPayload): Promise<PatientDetails> {
    const response = await apiClient.post<PatientDetails>('/patients', payload);
    return response.data;
  },

  async update(patientId: number, payload: PatientPayload): Promise<PatientDetails> {
    const response = await apiClient.put<PatientDetails>(`/patients/${patientId}`, payload);
    return response.data;
  },

  async getSafetyAlerts(patientId: number): Promise<PatientSafetyAlerts> {
    const response = await apiClient.get<PatientSafetyAlerts>(`/patients/${patientId}/safety-alerts`);
    return response.data;
  },

  async getTimeline(patientId: number, page: number, pageSize: number): Promise<PagedResult<PatientTimelineItem>> {
    const response = await apiClient.get<PagedResult<PatientTimelineItem>>(`/patients/${patientId}/timeline`, {
      params: { page, pageSize }
    });
    return response.data;
  },

  async createVisit(patientId: number): Promise<VisitDetails> {
    const response = await apiClient.post<VisitDetails>(`/patients/${patientId}/visits`);
    return response.data;
  },

  async getVisits(patientId: number, page = 1, pageSize = 100): Promise<PagedResult<VisitDetails>> {
    const response = await apiClient.get<PagedResult<VisitDetails>>(`/patients/${patientId}/visits`, {
      params: { page, pageSize }
    });
    return response.data;
  },

  async recordMedicalHistory(visitId: number, payload: MedicalHistoryPayload): Promise<MedicalHistoryRecord> {
    const response = await apiClient.post<MedicalHistoryRecord>(`/visits/${visitId}/medical-history`, payload);
    return response.data;
  },

  async getLatestMedicalHistory(patientId: number): Promise<MedicalHistoryRecord | null> {
    const response = await apiClient.get<MedicalHistoryRecord | null>(`/patients/${patientId}/medical-history/latest`);
    return response.data;
  },

  async getMedicalHistory(patientId: number, page: number, pageSize: number): Promise<PagedResult<MedicalHistoryRecord>> {
    const response = await apiClient.get<PagedResult<MedicalHistoryRecord>>(`/patients/${patientId}/medical-history`, {
      params: { page, pageSize }
    });
    return response.data;
  },

  async recordVitalSigns(visitId: number, payload: {
    systolicBp: number | null;
    diastolicBp: number | null;
    pulseBpm: number | null;
    temperatureC: number | null;
    note: string | null;
  }): Promise<VitalSignRecord> {
    const response = await apiClient.post<VitalSignRecord>(`/visits/${visitId}/vital-signs`, payload);
    return response.data;
  },

  async getVitalSigns(patientId: number, page = 1, pageSize = 1): Promise<PagedResult<VitalSignRecord>> {
    const response = await apiClient.get<PagedResult<VitalSignRecord>>(`/patients/${patientId}/vital-signs`, {
      params: { page, pageSize }
    });
    return response.data;
  }
};
