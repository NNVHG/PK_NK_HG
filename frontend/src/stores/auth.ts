import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { apiClient, setAccessToken } from '@/services/api';

export interface UserInfo {
  userId: number;
  fullName: string;
  roleCode: string;
  roleName: string;
  phone?: string;
  email?: string;
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

  async function login(phone: string, password: string) {
    const res = await apiClient.post('/auth/login', { phone, password });
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
      logout();
    }
  }

  async function logout() {
    try {
      if (token.value) {
        await apiClient.post('/auth/logout');
      }
    } catch {
      // Ignored
    } finally {
      token.value = null;
      user.value = null;
      setAccessToken(null);
    }
  }

  return {
    token,
    user,
    isAuthenticated,
    role,
    login,
    fetchMe,
    logout
  };
});
