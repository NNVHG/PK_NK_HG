import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';

export interface CheckInPayload {
  patientId: number;
  appointmentId: number | null;
  dentistId: number | null;
  notes: string | null;
  citizenId: string | null;
  phone: string;
  documentType?: 1 | 2 | null;
  nationalityCode?: string | null;
  guardianFullName?: string | null;
  guardianRelationship?: string | null;
  guardianVerifiedAtCounter?: boolean;
}

export interface QueueEntryResponse {
  queueEntryId: number;
  patientId: number;
  patientCode: string;
  patientName: string;
  appointmentId: number | null;
  dentistId: number | null;
  dentistName: string | null;
  chairNumber: string | null;
  roomName: string | null;
  visitId: number | null;
  queueDate: string;
  queueNumber: number;
  isPriority: boolean;
  isEmergency?: boolean;
  status: number;
  statusText: string;
  canMarkAbsent: boolean;
  canCancelConsultation: boolean;
  patientAccountCreated?: boolean;
  notes: string | null;
  patientPhone?: string;
  patientDateOfBirth?: string | null;
  isChildPatient?: boolean;
  missingGuardianAccount?: boolean;
}

export type OpenCheckInEntry = Pick<QueueEntryResponse, 'patientId' | 'queueEntryId' | 'queueNumber' | 'queueDate'>;

export interface PublicQueueItemResponse {
  queueNumberDisplay: string;
  maskedPatientName: string;
  status: number;
  roomName: string | null;
  expiresAtUtc: string;
}

export interface PublicQueueMonitorResponse {
  inConsultation: PublicQueueItemResponse[];
  waiting: PublicQueueItemResponse[];
}

export interface CallNextPayload {
  dentistId?: number | null;
}

export interface TransferDentistPayload {
  targetDentistId: number;
  expectedDentistId: number;
  expectedStatus: number;
  confirmed: boolean;
  reason: string | null;
}

export interface CallingDentist { dentistId: number; dentistName: string; chairNumber: string | null; roomName: string | null; }

export interface OutstandingDebtResponse {
  totalOutstandingDebt: number;
  invoiceCodes: string[];
}

export const queueService = {
  async getCallingDentists(): Promise<CallingDentist[]> {
    return (await apiClient.get<CallingDentist[]>('/queue/call-dentists')).data;
  },
  async transferDentist(queueEntryId: number, payload: TransferDentistPayload): Promise<QueueEntryResponse> {
    return (await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/transfer`, payload)).data;
  },
  async getToday(page = 1, status?: number, dentistId?: number): Promise<PagedResult<QueueEntryResponse>> {
    const response = await apiClient.get<PagedResult<QueueEntryResponse>>('/queue', {
      params: { page, pageSize: 20, status, dentistId }
    });
    return response.data;
  },

  async getEntry(queueEntryId: number): Promise<QueueEntryResponse> {
    const response = await apiClient.get<{ entry: QueueEntryResponse }>('/queue/' + queueEntryId);
    return response.data.entry;
  },

  async startConsultation(queueEntryId: number, dentistId: number): Promise<QueueEntryResponse> {
    const response = await apiClient.put<QueueEntryResponse>(`/queue/${queueEntryId}/status`, {
      newStatus: 2, dentistId
    });
    return response.data;
  },

  async callNext(payload?: CallNextPayload): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>('/queue/call-next', payload ?? {});
    return response.data;
  },

  async callOnTv(queueEntryId: number): Promise<QueueEntryResponse> {
    return (await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/call-on-tv`)).data;
  },

  async routeImaging(queueEntryId: number, newStatus: 1 | 5, reason: string | null): Promise<QueueEntryResponse> {
    const response = await apiClient.put<QueueEntryResponse>(`/queue/${queueEntryId}/status`, { newStatus, reason });
    return response.data;
  },

  async changeAbsence(queueEntryId: number, recall: boolean, reason: string | null): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/${recall ? 'recall' : 'skip'}`, {
      confirmed: true, reason
    });
    return response.data;
  },

  async cancelConsultation(queueEntryId: number, reason: string): Promise<QueueEntryResponse> {
    return (await apiClient.put<QueueEntryResponse>(`/queue/${queueEntryId}/status`, { newStatus: 4, reason })).data;
  },

  async cancelWaiting(queueEntryId: number, reason: string | null): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/cancel-waiting`, {
      confirmed: true, reason
    });
    return response.data;
  },

  async pauseVisit(queueEntryId: number, reason: string | null): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/pause`, {
      confirmed: true, reason
    });
    return response.data;
  },

  async resumeVisit(queueEntryId: number, dentistId: number | null, reason: string | null): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>(`/queue/${queueEntryId}/resume`, {
      confirmed: true, dentistId, reason
    });
    return response.data;
  },

  async getPublicDisplay(signal: AbortSignal): Promise<PublicQueueMonitorResponse> {
    const response = await apiClient.get<PublicQueueMonitorResponse>('/queue/display', { signal, timeout: 4000 });
    return response.data;
  },

  async getPublicCallAudio(queueNumber: string, expiresAtUtc: string, signal: AbortSignal): Promise<ArrayBuffer> {
    const response = await apiClient.get<ArrayBuffer>('/queue/display', {
      params: { queueNumber, expiresAtUtc },
      headers: { Accept: 'audio/mpeg' },
      responseType: 'arraybuffer',
      signal,
      timeout: 4000,
    });
    const contentType = String(response.headers['content-type'] ?? '').split(';')[0]?.trim().toLowerCase();
    if (contentType !== 'audio/mpeg' || !response.data.byteLength) throw new Error('Invalid queue audio');
    return response.data;
  },

  async checkIn(payload: CheckInPayload): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>('/queue/check-in', payload);
    return response.data;
  },

  async getOpenCheckInEntries(patientIds: number[]): Promise<OpenCheckInEntry[]> {
    if (!patientIds.length) return [];
    const params = new URLSearchParams();
    for (const id of new Set(patientIds)) params.append('patientIds', String(id));
    return (await apiClient.get<OpenCheckInEntry[]>('/queue/check-in/open-entries', { params })).data;
  },

  async getOutstandingDebt(patientId: number): Promise<OutstandingDebtResponse> {
    const response = await apiClient.get<OutstandingDebtResponse>(`/queue/check-in/${patientId}/outstanding-debt`);
    return response.data;
  }
};
