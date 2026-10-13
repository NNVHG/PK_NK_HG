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

export interface SelfCancellationEligibility {
  appointmentId: number;
  canCancel: boolean;
  message: string | null;
  reasonMaxLength: number;
}

export const appointmentsService = {
  async selfCancellationEligibility(id: number): Promise<SelfCancellationEligibility> {
    return (await apiClient.get<SelfCancellationEligibility>(`/appointments/${id}/cancellation-eligibility`)).data;
  },
  async getMine(page = 1, patientId?: number, status?: string): Promise<AppointmentPage> {
    return (await apiClient.get<AppointmentPage>('/appointments', { params: { page, pageSize: 20, patientId, status } })).data;
  },
  async getForDesk(date: string, page = 1, status?: string): Promise<AppointmentPage> {
    return (await apiClient.get<AppointmentPage>('/appointments/desk', { params: { date, page, pageSize: 20, status } })).data;
  },
  async cancel(id: number, reason: string | null): Promise<AppointmentResponse> {
    return (await apiClient.put<AppointmentResponse>(`/appointments/${id}/cancel`, { reason })).data;
  },
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
