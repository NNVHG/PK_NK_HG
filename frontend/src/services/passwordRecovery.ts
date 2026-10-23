import { apiClient } from '@/services/api';

export interface RecoveryChallenge { challengeId: string; expiresInSeconds: number; resendAfterSeconds: number }
export const passwordRecoveryService = {
  async request(phone: string, citizenId?: string): Promise<RecoveryChallenge> {
    return (await apiClient.post<RecoveryChallenge>('/auth/password-recovery/request', { phone, citizenId: citizenId || null })).data;
  },
  async reset(challengeId: string, phone: string, code: string, newPassword: string, citizenId?: string): Promise<void> {
    await apiClient.post('/auth/password-recovery/reset', { challengeId, phone, code, newPassword, citizenId: citizenId || null });
  }
};
