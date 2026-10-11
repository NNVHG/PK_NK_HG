import { apiClient } from '@/services/api';

export interface CashPaymentRequest { amount: number; amountTendered: number; requestId: string; paymentMethod: 'Cash'; }
export interface CashPaymentResponse {
  id: number; invoiceId: number; amount: number; amountTendered: number | null; changeAmount: number | null;
  cashierId: number; paidAt: string; requestId: string; paymentMethod: 'Cash' | 'BankTransfer';
  bankReceivedAmount: number | null; transactionReference: string | null; source: string | null; note: string | null;
}
export const cashPaymentsApi = {
  async receive(invoiceId: number, body: CashPaymentRequest): Promise<CashPaymentResponse> {
    return (await apiClient.post<CashPaymentResponse>(`/invoices/${invoiceId}/payments`, body)).data;
  },
  async history(invoiceId: number): Promise<CashPaymentResponse[]> {
    return (await apiClient.get<CashPaymentResponse[]>(`/invoices/${invoiceId}/payments`)).data;
  }
};
