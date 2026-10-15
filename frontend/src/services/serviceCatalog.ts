import { apiClient } from '@/services/api';

export interface ServicePrice { servicePriceId: number; amount: number; effectiveFrom: string; createdByUserId: number; createdAt: string; }
export interface CatalogService {
  dentalServiceId: number; code: string; name: string; description: string | null; durationMinutes: number;
  isActive: boolean; currentPrice: number | null; currentPriceEffectiveFrom: string | null;
}
export interface CatalogDetail extends CatalogService { priceHistory: ServicePrice[] | null; }
export interface CatalogResult { items: CatalogService[]; totalCount: number; totalPages: number; page: number; }
export interface ServiceInput { code: string; name: string; description: string | null; durationMinutes: number; initialPrice: number; effectiveFrom: string; }
export const serviceCatalogApi = {
  async list(page: number, keyword: string, isActive?: boolean): Promise<CatalogResult> {
    return (await apiClient.get<CatalogResult>('/services', { params: { page, pageSize: 20, keyword: keyword.trim() || undefined, isActive } })).data;
  },
  async detail(id: number): Promise<CatalogDetail> { return (await apiClient.get<CatalogDetail>(`/services/${id}`)).data; },
  async create(request: ServiceInput): Promise<CatalogDetail> { return (await apiClient.post<CatalogDetail>('/services', request)).data; },
  async update(id: number, request: Pick<ServiceInput, 'name' | 'description' | 'durationMinutes'>): Promise<CatalogDetail> {
    return (await apiClient.put<CatalogDetail>(`/services/${id}`, request)).data;
  },
  async addPrice(id: number, amount: number, effectiveFrom: string): Promise<ServicePrice> {
    return (await apiClient.post<ServicePrice>(`/services/${id}/prices`, { amount, effectiveFrom })).data;
  },
  async setActive(id: number, active: boolean): Promise<CatalogDetail> {
    return (await apiClient.post<CatalogDetail>(`/services/${id}/${active ? 'activate' : 'deactivate'}`)).data;
  },
};

export function clinicDateTimeInput(now = new Date()): string {
  return new Date(now.getTime() + 7 * 3600000).toISOString().slice(0, 16);
}
export function clinicInputToIso(value: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return null;
  const parsed = new Date(value + ':00+07:00');
  return Number.isFinite(parsed.getTime()) && clinicDateTimeInput(parsed) === value ? parsed.toISOString() : null;
}
export function validCatalogPrice(value: string): boolean {
  return /^\d+$/.test(value) && Number.isSafeInteger(Number(value)) && Number(value) >= 0;
}
export function validCatalogEffective(value: string, latest?: string | null, now = new Date()): boolean {
  const instant = clinicInputToIso(value);
  return !!instant && value.slice(0, 10) >= clinicDateTimeInput(now).slice(0, 10) &&
    (!latest || new Date(instant).getTime() >= new Date(latest).getTime());
}
