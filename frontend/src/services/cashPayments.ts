import { apiClient } from '@/services/api';

export interface CashPaymentRequest { amount: number; amountTendered: number; requestId: string; paymentMethod: 'Cash'; }
export interface CashPaymentResponse extends CashPaymentRequest {
  id: number; invoiceId: number; changeAmount: number; cashierId: number; paidAt: string;
}
export const cashPaymentsApi = {
  async receive(invoiceId: number, body: CashPaymentRequest): Promise<CashPaymentResponse> {
    return (await apiClient.post<CashPaymentResponse>(`/invoices/${invoiceId}/payments`, body)).data;
  },
  async history(invoiceId: number): Promise<CashPaymentResponse[]> {
    return (await apiClient.get<CashPaymentResponse[]>(`/invoices/${invoiceId}/payments`)).data;
  }
};
