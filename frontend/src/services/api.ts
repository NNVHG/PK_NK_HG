import axios from 'axios';

let accessToken: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function setUnauthorizedHandler(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

export const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json'
  }
});

apiClient.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const requestAuthorization = error.config?.headers?.Authorization;
    if (error.response?.status === 401 && accessToken && requestAuthorization === `Bearer ${accessToken}`) {
      setAccessToken(null);
      window.dispatchEvent(new Event('auth:expired'));
      unauthorizedHandler?.();
    }
    return Promise.reject(error);
  }
);

