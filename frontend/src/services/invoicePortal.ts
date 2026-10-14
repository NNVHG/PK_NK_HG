import { apiClient } from '@/services/api';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
export interface PortalPage<T> { items: T[]; page: number; totalCount: number; totalPages: number; }
export interface PortalPayment { paymentId: number; invoiceId: number; amount: number; paymentMethod: string; paidAt: string; isSimulation: boolean; }
export interface PortalInvoiceItem { itemId: number; code: string; name: string; quantity: number; unitPrice: number; totalAmount: number; }
export const invoicePortalApi = {
  async list(patientId: number, page: number): Promise<PortalPage<InvoiceLookupResponse>> {
    return (await apiClient.get<PortalPage<InvoiceLookupResponse>>('/portal/invoices', { params: { patientId, page, pageSize: 20 } })).data;
  },
  async items(patientId: number, invoiceId: number, page: number): Promise<PortalPage<PortalInvoiceItem>> {
    return (await apiClient.get<PortalPage<PortalInvoiceItem>>('/portal/invoices/' + invoiceId + '/items', { params: { patientId, page, pageSize: 20 } })).data;
  },
  async payments(patientId: number, invoiceId: number, page: number): Promise<PortalPage<PortalPayment>> {
    return (await apiClient.get<PortalPage<PortalPayment>>('/portal/invoices/' + invoiceId + '/payments', { params: { patientId, page, pageSize: 20 } })).data;
  }
};
