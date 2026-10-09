import { apiClient } from '@/services/api';

export interface AppointmentResponse {
  appointmentId: number;
  patientId: number;
  patientCode: string;
  patientName: string;
  appointmentDate: string;
  slotTime: string;
  status: string;
  notes: string | null;
}

export interface CreateAppointmentPayload {
  patientId: number;
  appointmentDate: string;
  slotTime: string;
  notes: string | null;
}

export const appointmentsService = {
  async create(payload: CreateAppointmentPayload): Promise<AppointmentResponse> {
    const response = await apiClient.post<AppointmentResponse>('/appointments', payload);
    return response.data;
  }
};
