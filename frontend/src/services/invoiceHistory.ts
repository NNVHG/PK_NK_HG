import { apiClient } from '@/services/api';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
export interface InvoiceHistoryQuery { patientCode?: string; status?: number; fromDate?: string; toDate?: string; page: number; pageSize: number; }
export interface InvoiceHistoryPage { items: InvoiceLookupResponse[]; totalCount: number; page: number; pageSize: number; totalPages: number; }
export const invoiceHistoryApi = {
  async get(query: InvoiceHistoryQuery): Promise<InvoiceHistoryPage> {
    return (await apiClient.get<InvoiceHistoryPage>('/invoices', { params: query })).data;
  }
};
