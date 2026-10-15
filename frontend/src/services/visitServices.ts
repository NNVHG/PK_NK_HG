import { apiClient } from '@/services/api';

export interface AssignedService {
  id: number; visitId: number; serviceId: number; servicePriceId: number;
  serviceCode: string; serviceName: string; toothNumber: number | null; surface: string | null;
  quantity: number; unitPrice: number; totalAmount: number; createdAt: string;
}
export interface CatalogOption {
  dentalServiceId: number; code: string; name: string; currentPrice: number | null;
}
export interface CatalogPage { items: CatalogOption[]; totalPages: number; }
export interface AssignServicesPayload {
  serviceId: number; toothNumbers: number[] | null; surface: string | null; quantity: number;
}
export const visitServicesApi = {
  async remove(visitId: number, assignmentId: number, reason: string): Promise<void> {
    await apiClient.delete(`/visits/${visitId}/services/${assignmentId}`, { data: { reason } });
  },
  async get(visitId: number): Promise<AssignedService[]> {
    return (await apiClient.get<AssignedService[]>(`/visits/${visitId}/services`)).data;
  },
  async assign(visitId: number, payload: AssignServicesPayload): Promise<AssignedService[]> {
    return (await apiClient.post<AssignedService[]>(`/visits/${visitId}/services`, payload)).data;
  },
  async catalog(page: number): Promise<CatalogPage> {
    return (await apiClient.get<CatalogPage>('/services', { params: { isActive: true, page, pageSize: 20 } })).data;
  },
  async findActiveByCode(code: string): Promise<CatalogOption | null> {
    const result = (await apiClient.get<CatalogPage>('/services', {
      params: { keyword: code, isActive: true, page: 1, pageSize: 100 }
    })).data;
    return result.items.find(item => item.code === code && item.currentPrice !== null) ?? null;
  }
};
