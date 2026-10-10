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

export interface AppointmentPage {
  items: AppointmentResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export const appointmentsService = {
  async create(payload: CreateAppointmentPayload): Promise<AppointmentResponse> {
    const response = await apiClient.post<AppointmentResponse>('/appointments', payload);
    return response.data;
  },

  async getScheduledForPatientOnDate(patientId: number, date: string): Promise<AppointmentPage> {
    const response = await apiClient.get<AppointmentPage>('/appointments', {
      params: { patientId, date, status: 'Scheduled', page: 1, pageSize: 100 }
    });
    return response.data;
  }
};
