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
  visitId: number | null;
  queueDate: string;
  queueNumber: number;
  isPriority: boolean;
  status: number;
  statusText: string;
  notes: string | null;
}

export const queueService = {
  async getToday(page = 1): Promise<PagedResult<QueueEntryResponse>> {
    const response = await apiClient.get<PagedResult<QueueEntryResponse>>('/queue', {
      params: { page, pageSize: 20 }
    });
    return response.data;
  },

  async startConsultation(queueEntryId: number, dentistId: number): Promise<QueueEntryResponse> {
    const response = await apiClient.put<QueueEntryResponse>(`/queue/${queueEntryId}/status`, {
      newStatus: 2, dentistId
    });
    return response.data;
  },

  async checkIn(payload: CheckInPayload): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>('/queue/check-in', payload);
    return response.data;
  }
};
