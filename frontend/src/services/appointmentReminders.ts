import { apiClient } from '@/services/api';
import type { AppointmentPage } from '@/services/appointments';

export interface ReminderLog {
  id: number; appointmentId: number; appointmentDate: string; slotTime: string;
  notificationType: string; channel: string; status: string; sentAtUtc: string; messageContent: string;
}
export interface ReminderScan {
  eligibleCount: number; createdCount: number; alreadyRemindedCount: number; windowStart: string; windowEnd: string;
}
export interface ReminderPage extends Omit<AppointmentPage, 'items'> { items: ReminderLog[]; }
export const appointmentRemindersService = {
  async getLogs(page: number, appointmentId?: number): Promise<ReminderPage> {
    return (await apiClient.get<ReminderPage>('/appointments/reminder-logs', { params: { page, pageSize: 20, appointmentId } })).data;
  },
  async simulate(): Promise<ReminderScan> {
    return (await apiClient.post<ReminderScan>('/appointments/simulate-reminder', { confirmed: true })).data;
  }
};
