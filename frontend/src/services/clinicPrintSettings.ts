import { apiClient } from '@/services/api';
export interface ClinicPrintSettings { address: string | null; phone: string | null; revision: string }
export const clinicPrintSettingsService = {
 async read(): Promise<ClinicPrintSettings> { return (await apiClient.get('/clinic/print-settings')).data; },
 async save(address: string, phone: string, expectedRevision: string): Promise<ClinicPrintSettings> { return (await apiClient.put('/clinic/print-settings', { address, phone, expectedRevision })).data; }
};
