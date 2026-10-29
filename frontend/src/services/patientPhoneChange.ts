import { apiClient } from '@/services/api';

export interface PatientPhonePreview { patientId: number; patientName: string; currentPhone: string; newPhone: string; contactWillUpdate: boolean; reviewStamp: string }
const root = '/auth/patient-phone-change';
export function isPatientPhonePreview(value: any, newPhone: string, currentPhone?: string): value is PatientPhonePreview {
  return !!value && typeof value.currentPhone === 'string' && value.currentPhone.length === 10 && typeof value.reviewStamp === 'string' && value.reviewStamp.length === 64 && value.newPhone === newPhone && /^0[0-9]{9}$/.test(value.currentPhone) &&
    (!currentPhone || value.currentPhone === currentPhone) && /^[A-F0-9]{64}$/.test(value.reviewStamp) && Number.isSafeInteger(value.patientId) &&
    value.patientId > 0 && typeof value.patientName === 'string' && !!value.patientName.trim() && typeof value.contactWillUpdate === 'boolean';
}
export const patientPhoneChange = {
  async preview(newPhone: string) { return (await apiClient.post(root + '/preview', { newPhone })).data; },
  async change(newPhone: string, reviewStamp: string, currentPassword: string) {
    return (await apiClient.post(root + '/change', { newPhone, reviewStamp, confirmed: true, currentPassword })).data;
  }
};
