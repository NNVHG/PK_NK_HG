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

export const AUTH_STORAGE_KEY = 'dental_auth_tab_session_v1';
export const PATIENT_IDLE_TIMEOUT_MS = 15 * 60 * 1000;
const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'touchstart', 'scroll', 'click', 'mousemove'] as const;
const ACTIVITY_PERSIST_THROTTLE_MS = 5000;
const MAX_TIMER_DELAY_MS = 2_147_483_647;

interface PersistedAuthSession {
  token: string;
  user: Pick<UserInfo, 'userId' | 'fullName' | 'roleCode' | 'roleName'>;
  lastActiveAt: number;
}

function getStorage(): Storage | null {
  try {
    if (typeof window !== 'undefined' && window.sessionStorage) {
      return window.sessionStorage;
    }
  } catch {}
  return null;
}

function readPersistedSession(): PersistedAuthSession | null {
  const storage = getStorage();
  if (!storage) return null;
  try {
    const raw = storage.getItem(AUTH_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<PersistedAuthSession> | null;
    if (
      !parsed ||
      typeof parsed.token !== 'string' ||
      !parsed.token ||
      !parsed.user ||
      typeof parsed.user !== 'object' ||
      typeof parsed.user.roleCode !== 'string' ||
      !parsed.user.roleCode ||
      typeof parsed.lastActiveAt !== 'number' ||
      !Number.isFinite(parsed.lastActiveAt) ||
      parsed.lastActiveAt <= 0
    ) {
      storage.removeItem(AUTH_STORAGE_KEY);
      return null;
    }
    return {
      token: parsed.token,
      user: parsed.user,
      lastActiveAt: parsed.lastActiveAt
    };
  } catch {
    try {
      storage.removeItem(AUTH_STORAGE_KEY);
    } catch {}
    return null;
  }
}

function readTokenExpiry(accessToken: string): number {
  try {
    const parts = accessToken.split('.');
    if (parts.length !== 3) return 0;
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const { exp } = JSON.parse(atob(payload));
    return typeof exp === 'number' && Number.isFinite(exp * 1000) ? exp * 1000 : 0;
  } catch {
    return 0;
  }
}

function writePersistedSession(session: PersistedAuthSession) {
  const storage = getStorage();
  if (!storage) return;
  try {
    storage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
  } catch {}
}

function removePersistedSession() {
  const storage = getStorage();
  if (!storage) return;
  try {
    storage.removeItem(AUTH_STORAGE_KEY);
  } catch {}
}

function emitSessionExpiredEvent() {
  if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function' && typeof Event === 'function') {
    window.dispatchEvent(new Event('auth:expired'));
  }
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null);
  const user = ref<UserInfo | null>(null);
  const lastActiveAt = ref<number>(0);

  let idleTimer: ReturnType<typeof setTimeout> | null = null;
  let lastPersistedActivityAt = 0;
  let tokenExpiresAt = 0;

  const isAuthenticated = computed(() => !!token.value);
  const role = computed(() => user.value?.roleCode || '');

  function isPatientRole(roleCode = user.value?.roleCode): boolean {
    return roleCode === 'PATIENT';
  }

  function clearIdleTimer() {
    if (idleTimer !== null) {
      clearTimeout(idleTimer);
      idleTimer = null;
    }
  }

  function persistSession(now = lastActiveAt.value || Date.now()) {
    if (!token.value || !user.value) return;
    const { userId, fullName, roleCode, roleName } = user.value;
    writePersistedSession({
      token: token.value,
      user: { userId, fullName, roleCode, roleName },
      lastActiveAt: now
    });
    lastPersistedActivityAt = now;
  }

  function expireSession() {
    const hadSession = !!token.value || !!user.value;
    clearSession();
    if (hadSession) {
      emitSessionExpiredEvent();
    }
  }

  function scheduleExpiryCheck(now = Date.now()) {
    clearIdleTimer();
    if (!token.value) return;

    const expiresAt = isPatientRole()
      ? Math.min(tokenExpiresAt, lastActiveAt.value + PATIENT_IDLE_TIMEOUT_MS)
      : tokenExpiresAt;
    const remaining = expiresAt - now;
    if (remaining <= 0) {
      expireSession();
      return;
    }

    idleTimer = setTimeout(() => {
      if (!checkSessionExpiry()) scheduleExpiryCheck();
    }, Math.min(remaining, MAX_TIMER_DELAY_MS));
    if (idleTimer && typeof idleTimer === 'object' && 'unref' in idleTimer) {
      (idleTimer as { unref?: () => void }).unref?.();
    }
  }

  function checkSessionExpiry(now = Date.now()): boolean {
    if (!token.value) return false;
    if (tokenExpiresAt <= now || (isPatientRole() && now - lastActiveAt.value >= PATIENT_IDLE_TIMEOUT_MS)) {
      expireSession();
      return true;
    }
    return false;
  }

  function recordActivity(now = Date.now()) {
    if (!token.value || checkSessionExpiry(now) || !isPatientRole()) return;

    lastActiveAt.value = now;
    if (now - lastPersistedActivityAt >= ACTIVITY_PERSIST_THROTTLE_MS) {
      persistSession(now);
    }
  }

  function hydrateSession(now = Date.now()) {
    const stored = readPersistedSession();
    if (!stored) return;
    const expiry = readTokenExpiry(stored.token);
    if (expiry <= now || stored.lastActiveAt > now ||
        (isPatientRole(stored.user.roleCode) && now - stored.lastActiveAt >= PATIENT_IDLE_TIMEOUT_MS)) {
      removePersistedSession();
      return;
    }

    token.value = stored.token;
    user.value = stored.user;
    tokenExpiresAt = expiry;
    lastActiveAt.value = stored.lastActiveAt;
    lastPersistedActivityAt = stored.lastActiveAt;
    setAccessToken(stored.token);
    scheduleExpiryCheck(now);
  }

  function handleWindowActivity() {
    recordActivity();
  }

  function handleWindowFocusOrVisible() {
    if (typeof document !== 'undefined' && document.hidden) return;
    checkSessionExpiry();
  }

  if (typeof window !== 'undefined' && typeof window.addEventListener === 'function') {
    // Dọn khóa dùng trong bản phát triển cũ.
    try { window.localStorage.removeItem('dental_auth_session_v1'); } catch {}
    window.addEventListener('auth:expired', () => {
      clearSession();
    });
    for (const eventName of ACTIVITY_EVENTS) {
      window.addEventListener(eventName, handleWindowActivity, { passive: true });
    }
    window.addEventListener('focus', handleWindowFocusOrVisible);
  }
  if (typeof document !== 'undefined' && typeof document.addEventListener === 'function') {
    document.addEventListener('visibilitychange', handleWindowFocusOrVisible);
  }

  hydrateSession();

  async function login(phone: string, password: string, documentType?: number | null, nationalityCode?: string | null) {
    const payload: Record<string, any> = { phone, password };
    if (documentType) payload.documentType = documentType;
    if (nationalityCode) payload.nationalityCode = nationalityCode;

    const res = await apiClient.post('/auth/login', payload);
    const data = res.data;
    const now = Date.now();
    const expiry = readTokenExpiry(data.accessToken);
    if (expiry <= now) throw new Error('Máy chủ trả về phiên đăng nhập không hợp lệ.');
    tokenExpiresAt = expiry;
    token.value = data.accessToken;
    setAccessToken(data.accessToken);
    user.value = {
      userId: data.userId,
      fullName: data.fullName,
      roleCode: data.roleCode,
      roleName: data.roleName
    };
    lastActiveAt.value = now;
    persistSession(now);
    scheduleExpiryCheck(now);

    return data;
  }

  async function fetchMe() {
    const sessionToken = token.value;
    if (!sessionToken || checkSessionExpiry()) return;
    try {
      const res = await apiClient.get('/auth/me');
      if (token.value !== sessionToken || checkSessionExpiry()) return;
      user.value = res.data;
      persistSession();
      scheduleExpiryCheck();
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (token.value === sessionToken && (status === 401 || status === 403)) {
        expireSession();
      }
    }
  }

  async function updateProfile(profile: ProfileUpdatePayload) {
    const sessionToken = token.value;
    const res = await apiClient.put('/auth/profile', profile);
    if (token.value === sessionToken && sessionToken && !checkSessionExpiry()) {
      user.value = res.data;
      persistSession();
    }
    return res.data;
  }

  function clearSession() {
    clearIdleTimer();
    token.value = null;
    user.value = null;
    lastActiveAt.value = 0;
    lastPersistedActivityAt = 0;
    tokenExpiresAt = 0;
    removePersistedSession();
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
