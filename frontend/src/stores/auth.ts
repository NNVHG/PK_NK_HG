import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { apiClient, setAccessToken } from '@/services/api';

export interface UserInfo {
  phoneVerified?: boolean;
  userId: number;
  fullName: string;
  roleCode: string;
  roleName: string;
  phone?: string;
  email?: string;
  dateOfBirth?: string | null;
  gender?: string | null;
}

export interface ProfileUpdatePayload {
  fullName: string;
  phone: string;
  email: string | null;
  dateOfBirth: string | null;
  gender: string | null;
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null);
  const user = ref<UserInfo | null>(null);

  window.addEventListener('auth:expired', () => {
    token.value = null;
    user.value = null;
  });

  const isAuthenticated = computed(() => !!token.value);
  const role = computed(() => user.value?.roleCode || '');

  async function login(phone: string, password: string, documentType?: number | null, nationalityCode?: string | null) {
    const payload: Record<string, any> = { phone, password };
    if (documentType) payload.documentType = documentType;
    if (nationalityCode) payload.nationalityCode = nationalityCode;

    const res = await apiClient.post('/auth/login', payload);
    const data = res.data;
    token.value = data.accessToken;
    setAccessToken(data.accessToken);
    user.value = {
      userId: data.userId,
      fullName: data.fullName,
      roleCode: data.roleCode,
      roleName: data.roleName
    };

    return data;
  }

  async function fetchMe() {
    if (!token.value) return;
    try {
      const res = await apiClient.get('/auth/me');
      user.value = res.data;
    } catch {
      await logout().catch(() => undefined);
    }
  }

  async function updateProfile(profile: ProfileUpdatePayload) {
    const res = await apiClient.put('/auth/profile', profile);
    user.value = res.data;
    return res.data;
  }

  function clearSession() {
    token.value = null;
    user.value = null;
    setAccessToken(null);
  }

  async function logout() {
    const sessionToken = token.value;
    const releasesWorkspace = role.value === 'DENTIST' || role.value === 'ASSISTANT';
    try {
      if (sessionToken) {
        await apiClient.post('/auth/logout');
      }
    } catch (error) {
      if (releasesWorkspace && token.value === sessionToken) {
        const response = error as { response?: { status?: number } };
        if (response?.response?.status !== 401) throw error;
      }
    }
    if (token.value === sessionToken) clearSession();
  }

  return {
    token,
    user,
    isAuthenticated,
    role,
    login,
    fetchMe,
    updateProfile,
    logout,
    clearSession
  };
});
