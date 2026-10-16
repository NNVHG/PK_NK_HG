import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';

export interface CheckInPayload {
  patientId: number;
  appointmentId: number | null;
  dentistId: number | null;
  notes: string | null;
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
  status: number;
  statusText: string;
  canMarkAbsent: boolean;
  canCancelConsultation: boolean;
  notes: string | null;
}

export interface PublicQueueItemResponse {
  queueEntryId: number;
  queueNumber: number;
  queueNumberDisplay: string;
  maskedPatientName: string;
  dentistName: string | null;
  chairNumber: string | null;
  roomName: string | null;
  status: number;
  statusText: string;
  isPriority: boolean;
}

export interface PublicQueueMonitorResponse {
  inConsultation: PublicQueueItemResponse[];
  waiting: PublicQueueItemResponse[];
}

export interface CallNextPayload {
  dentistId?: number | null;
}

export interface CallingDentist { dentistId: number; dentistName: string; chairNumber: string | null; roomName: string | null; }

export interface QueueCleanupResponse { cancelledCount: number; lastExpiredDate: string; }

export const queueService = {
  async cleanupExpired(): Promise<QueueCleanupResponse> {
    return (await apiClient.post<QueueCleanupResponse>('/queue/cleanup-expired', { confirmed: true })).data;
  },
  async getCallingDentists(): Promise<CallingDentist[]> {
    return (await apiClient.get<CallingDentist[]>('/queue/call-dentists')).data;
  },
  async getToday(page = 1, status?: number): Promise<PagedResult<QueueEntryResponse>> {
    const response = await apiClient.get<PagedResult<QueueEntryResponse>>('/queue', {
      params: { page, pageSize: 20, ...(status === undefined ? {} : { status }) }
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

  async getPublicDisplay(): Promise<PublicQueueMonitorResponse> {
    const response = await apiClient.get<PublicQueueMonitorResponse>('/queue/display');
    return response.data;
  },

  async checkIn(payload: CheckInPayload): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>('/queue/check-in', payload);
    return response.data;
  }
};
