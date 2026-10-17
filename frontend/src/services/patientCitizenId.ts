import { apiClient } from '@/services/api';
export interface CitizenIdStatus { patientId: number; masked: string | null; version: number }
export const patientCitizenIdService = {
 async read(id: number): Promise<CitizenIdStatus> { return (await apiClient.get<CitizenIdStatus>('/patients/' + id + '/citizen-id')).data; },
 async save(id: number, citizenId: string, expectedVersion: number): Promise<CitizenIdStatus> { return (await apiClient.put<CitizenIdStatus>('/patients/' + id + '/citizen-id', { citizenId, expectedVersion })).data; }
};
