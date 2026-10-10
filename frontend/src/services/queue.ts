import { apiClient } from '@/services/api';

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
  visitId: number | null;
  queueDate: string;
  queueNumber: number;
  isPriority: boolean;
  status: number;
  statusText: string;
  notes: string | null;
}

export const queueService = {
  async checkIn(payload: CheckInPayload): Promise<QueueEntryResponse> {
    const response = await apiClient.post<QueueEntryResponse>('/queue/check-in', payload);
    return response.data;
  }
};
