import { apiClient } from '@/services/api';

export interface InvoiceItem {
  id: number; itemType: string; code: string; name: string; toothNumber: number | null; surface: string | null;
  refundedQuantity?: number; quantity: number; unitPrice: number; totalAmount: number;
}
export interface InvoiceDraft {
  id: number; visitId: number; invoiceCode: string; status: number; totalAmount: number;
  paidAmount: number; remainingAmount: number; createdAt: string; items: InvoiceItem[];
  discountAmount: number; finalAmount: number; discountType: string | null; discountValue: number | null;
  discountPresetId: number | null; discountReason: string | null;
}
export interface VisitUnlockHistory { id: number; actorUserId: number; cancelledInvoiceId: number | null; reason: string; createdAt: string; }
export const invoiceDraftApi = {
  async unlockHistory(visitId: number): Promise<VisitUnlockHistory[]> {
    return (await apiClient.get<VisitUnlockHistory[]>(`/visits/${visitId}/unlock-history`)).data;
  },
  async unlock(visitId: number, reason: string): Promise<void> {
    await apiClient.post(`/visits/${visitId}/unlock`, { unlockReason: reason });
  },
  async get(visitId: number): Promise<InvoiceDraft> {
    return (await apiClient.get<InvoiceDraft>(`/invoices/by-visit/${visitId}`)).data;
  },
  async cancelDraft(invoiceId: number, reason: string): Promise<void> {
    await apiClient.post(`/invoices/${invoiceId}/cancel`, { reason });
  },
  async complete(visitId: number): Promise<void> {
    await apiClient.put(`/visits/${visitId}/complete`);
  }
};
