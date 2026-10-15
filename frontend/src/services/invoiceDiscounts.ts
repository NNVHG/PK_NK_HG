import { apiClient } from '@/services/api';

export type DiscountType = 'Percentage' | 'FixedAmount';
export interface DiscountPreset {
  id: number; code: string; name: string; discountType: DiscountType; value: number; isActive: boolean;
}
export type DiscountPresetInput = Omit<DiscountPreset, 'id'>;
export interface ApplyInvoiceDiscountRequest {
  mode: 'Preset' | 'Custom' | 'Remove'; presetId: number | null; discountType: DiscountType | null;
  value: number | null; reason: string; expectedTotalAmount: number;
  expectedCurrentDiscountAmount: number; expectedDiscountAmount: number;
}
export const invoiceDiscountApi = {
  async presets(): Promise<DiscountPreset[]> {
    return (await apiClient.get<DiscountPreset[]>('/invoices/discount-presets')).data;
  },
  async savePreset(id: number | null, request: DiscountPresetInput): Promise<DiscountPreset> {
    return (id === null ? await apiClient.post<DiscountPreset>('/invoices/discount-presets', request)
      : await apiClient.put<DiscountPreset>(`/invoices/discount-presets/${id}`, request)).data;
  },
  async apply(invoiceId: number, request: ApplyInvoiceDiscountRequest): Promise<void> {
    await apiClient.post(`/invoices/${invoiceId}/discounts`, request);
  },
};
