import { apiClient } from './api';

export interface ActivationChallenge { challengeId: string; expiresInSeconds: number; resendAfterSeconds: number; developmentCode?: string | null }
export interface ActivationGrant { completionToken: string; expiresInSeconds: number; mustChangePassword: boolean }
export const patientActivationService = {
  async request(phone: string, password: string) {
    return (await apiClient.post<ActivationChallenge>('/auth/activation/request', { phone, password })).data;
  },
  async verify(challengeId: string, code: string) {
    return (await apiClient.post<ActivationGrant>('/auth/activation/verify', { challengeId, code })).data;
  },
  async complete(completionToken: string, keepDefaultPassword: boolean, newPassword: string | null) {
    return (await apiClient.post<{ completed: boolean }>('/auth/activation/complete', { completionToken, keepDefaultPassword, newPassword, confirmed: true })).data;
  }
};
