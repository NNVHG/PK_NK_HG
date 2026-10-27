import { apiClient } from './api';

export interface ActivationChallenge { challengeId: string; expiresInSeconds: number; resendAfterSeconds: number; developmentCode?: string | null }
export interface ActivationGrant { completionToken: string; expiresInSeconds: number; mustChangePassword: boolean }
export const patientActivationService = {
  async request(phone: string, password: string, documentType?: number | null, nationalityCode?: string | null) {
    const payload: Record<string, any> = { phone, password };
    if (documentType) payload.documentType = documentType;
    if (nationalityCode) payload.nationalityCode = nationalityCode;
    return (await apiClient.post<ActivationChallenge>('/auth/activation/request', payload)).data;
  },
  async verify(challengeId: string, code: string) {
    return (await apiClient.post<ActivationGrant>('/auth/activation/verify', { challengeId, code })).data;
  },
  async complete(completionToken: string, keepDefaultPassword: boolean, newPassword: string | null) {
    return (await apiClient.post<{ completed: boolean }>('/auth/activation/complete', { completionToken, keepDefaultPassword, newPassword, confirmed: true })).data;
  }
};
