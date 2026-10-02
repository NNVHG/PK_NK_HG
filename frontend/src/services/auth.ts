import { apiClient } from '@/services/api';

export interface RegisterPayload {
  fullName: string;
  phone: string;
  password: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female' | 'Other';
  email: string | null;
}

export interface RegisterResponse {
  userId: number;
  message: string;
}

export async function registerPatient(payload: RegisterPayload): Promise<RegisterResponse> {
  const response = await apiClient.post<RegisterResponse>('/auth/register', payload);
  return response.data;
}
