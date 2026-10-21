import { apiClient } from './api';
export interface CounterPhoneSuggestion { requestId: string; appointmentId: number; newPhone: string; createdAtUtc: string }
export function isCounterPhoneSuggestion(value: any): value is CounterPhoneSuggestion {
  return value && typeof value.requestId === 'string' && /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(value.requestId) &&
    Number.isInteger(value.appointmentId) && value.appointmentId > 0 && typeof value.newPhone === 'string' &&
    value.newPhone.length === 10 && /^0[0-9]{9}$/.test(value.newPhone) && typeof value.createdAtUtc === 'string' && Number.isFinite(Date.parse(value.createdAtUtc));
}
export const counterPatientPhone = {
  async create(appointmentId: number, newPhone: string) {
    return (await apiClient.post(`/appointments/${appointmentId}/patient-phone-change`, { newPhone, confirmedAtCounter: true })).data;
  },
  async own() { return (await apiClient.get('/auth/patient-phone-change/counter-request')).data; }
};
