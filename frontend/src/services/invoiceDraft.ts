import { apiClient } from '@/services/api';

export interface InvoiceItem {
  itemType: string; code: string; name: string; toothNumber: number | null; surface: string | null;
  quantity: number; unitPrice: number; totalAmount: number;
}
export interface InvoiceDraft {
  id: number; visitId: number; invoiceCode: string; status: number; totalAmount: number;
  paidAmount: number; remainingAmount: number; createdAt: string; items: InvoiceItem[];
}
export const invoiceDraftApi = {
  async get(visitId: number): Promise<InvoiceDraft> {
    return (await apiClient.get<InvoiceDraft>(`/visits/${visitId}/invoice-draft`)).data;
  },
  async complete(visitId: number): Promise<void> {
    await apiClient.post(`/visits/${visitId}/complete`);
  }
};
