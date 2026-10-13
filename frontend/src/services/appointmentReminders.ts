import { apiClient } from '@/services/api';
import type { AppointmentPage } from '@/services/appointments';

export interface ReminderLog {
  id: number; appointmentId: number; appointmentDate: string; slotTime: string;
  notificationType: string; channel: string; status: string; sentAtUtc: string; messageContent: string;
}
export interface ReminderScan {
  eligibleCount: number; createdCount: number; alreadyRemindedCount: number; windowStart: string; windowEnd: string;
}
export interface ReminderWorkerStatus {
  state: string; intervalMinutes: number; lastStartedAtUtc: string | null; lastCompletedAtUtc: string | null;
  createdCount: number | null; alreadyRemindedCount: number | null;
}
export interface ReminderPage extends Omit<AppointmentPage, 'items'> { items: ReminderLog[]; }
export const appointmentRemindersService = {
  async getWorkerStatus(): Promise<ReminderWorkerStatus> {
    return (await apiClient.get<ReminderWorkerStatus>('/appointments/reminder-worker-status')).data;
  },
  async getLogs(page: number, appointmentId?: number): Promise<ReminderPage> {
    return (await apiClient.get<ReminderPage>('/appointments/reminder-logs', { params: { page, pageSize: 20, appointmentId } })).data;
  },
  async simulate(): Promise<ReminderScan> {
    return (await apiClient.post<ReminderScan>('/appointments/simulate-reminder', { confirmed: true })).data;
  }
};
