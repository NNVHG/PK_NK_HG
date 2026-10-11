import { apiClient } from '@/services/api';

export interface InvoiceItem {
  itemType: string; code: string; name: string; toothNumber: number | null; surface: string | null;
  quantity: number; unitPrice: number; totalAmount: number;
}
export interface InvoiceDraft {
  id: number; visitId: number; invoiceCode: string; status: number; totalAmount: number;
  paidAmount: number; remainingAmount: number; createdAt: string; items: InvoiceItem[];
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
  async complete(visitId: number): Promise<void> {
    await apiClient.put(`/visits/${visitId}/complete`);
  }
};
