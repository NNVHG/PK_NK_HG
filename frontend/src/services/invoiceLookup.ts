import { apiClient } from '@/services/api';
export interface InvoiceLookupResponse {
  invoiceId: number; invoiceCode: string; status: number; totalAmount: number; paidAmount: number;
  remainingAmount: number; patientCode: string; patientName: string; createdAt: string;
}
export const invoiceLookupApi = {
  async get(code: string): Promise<InvoiceLookupResponse> {
    return (await apiClient.get<InvoiceLookupResponse>('/invoices/lookup', { params: { code: code.trim().toUpperCase() } })).data;
  }
};
