import { apiClient } from '@/services/api';

export interface PhoneScopeProfile { patientId: number; fullName: string; dateOfBirth: string; isActive: boolean; willUpdate: boolean }
export interface PhoneScopePreview { currentPhone: string; newPhone: string; reviewStamp: string; profiles: PhoneScopeProfile[] }
export interface PhoneChangePending { challengeId: string; newPhone: string; expiresAtUtc: string }
const root = '/auth/patient-phone-change';
export function isPhoneScopePreview(value: any, newPhone: string, currentPhone?: string): value is PhoneScopePreview {
  return !!value && typeof value.currentPhone === 'string' && value.currentPhone.length === 10 && typeof value.reviewStamp === 'string' && value.reviewStamp.length === 64 && value.newPhone === newPhone && /^0[0-9]{9}$/.test(value.currentPhone) &&
    (!currentPhone || value.currentPhone === currentPhone) && /^[A-F0-9]{64}$/.test(value.reviewStamp) &&
    Array.isArray(value.profiles) && value.profiles.every((p: any) => p && Number.isInteger(p.patientId) && p.patientId > 0 &&
      typeof p.fullName === 'string' && !!p.fullName.trim() && typeof p.dateOfBirth === 'string' && p.dateOfBirth.length === 10 && /^\d{4}-\d{2}-\d{2}$/.test(p.dateOfBirth) &&
      Number.isFinite(Date.parse(p.dateOfBirth)) && typeof p.isActive === 'boolean' && typeof p.willUpdate === 'boolean') &&
    new Set(value.profiles.map((p: PhoneScopeProfile) => p.patientId)).size === value.profiles.length;
}
export const patientPhoneChange = {
  async preview(newPhone: string) { return (await apiClient.post(root + '/preview', { newPhone })).data; },
  async request(newPhone: string, reviewStamp: string) { return (await apiClient.post(root + '/request', { newPhone, reviewStamp, confirmed: true })).data; },
  async confirm(challengeId: string, code: string, reviewStamp: string) {
    return (await apiClient.post(root + '/confirm', { challengeId, code, reviewStamp, confirmed: true })).data;
  }
};
