import { apiClient } from '@/services/api';
export interface SchedulingSettings { workStartTime: string; lunchStartTime: string; lunchEndTime: string; workEndTime: string; slotDurationMinutes: number; revision: string; usingFallback: boolean }
export interface SchedulingSettingsWrite { workStartTime: string; lunchStartTime: string; lunchEndTime: string; workEndTime: string; slotDurationMinutes: number; expectedRevision: string }
export function validSchedule(value: SchedulingSettingsWrite): boolean {
  const times = [value.workStartTime, value.lunchStartTime, value.lunchEndTime, value.workEndTime];
  if (times.some(x => !/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(x)) || !Number.isInteger(value.slotDurationMinutes) || value.slotDurationMinutes < 1 || value.slotDurationMinutes > 1440) return false;
  const minute = times.map(x => Number(x.slice(0, 2)) * 60 + Number(x.slice(3)));
  return minute[0]! < minute[1]! && minute[1]! < minute[2]! && minute[2]! < minute[3]! && (minute[1]! - minute[0]! >= value.slotDurationMinutes || minute[3]! - minute[2]! >= value.slotDurationMinutes);
}
export const schedulingSettingsService = {
  async read(): Promise<SchedulingSettings> { return (await apiClient.get('/master/configs')).data; },
  async save(payload: SchedulingSettingsWrite): Promise<SchedulingSettings> { return (await apiClient.put('/master/configs', payload)).data; }
};
