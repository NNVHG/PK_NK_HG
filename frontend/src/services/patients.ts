import { apiClient } from '@/services/api';

export interface PatientSummary {
  patientId?: number;
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
  }
};
