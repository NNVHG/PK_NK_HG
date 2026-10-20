import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { RecoveryChallenge } from '@/services/passwordRecovery';

// Transient challenge metadata only; phone, OTP and passwords stay in the mounted form.
export const usePasswordRecoveryStore = defineStore('MOD_AUTH_passwordRecovery', () => {
  const challenge = ref<RecoveryChallenge | null>(null);
  function set(value: RecoveryChallenge) {
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.challengeId) ||
        value.expiresInSeconds !== 300 || value.resendAfterSeconds !== 60) throw Error('Invalid challenge');
    challenge.value = value;
  }
  function clear() { challenge.value = null; }
  return { challenge, set, clear };
});
