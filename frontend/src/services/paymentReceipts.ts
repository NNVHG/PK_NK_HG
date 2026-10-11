import { apiClient } from '@/services/api';
export interface ReceiptPrintDto {
  invoiceId: number; invoiceCode: string; paymentId: number; clinicName: string;
  clinicAddress: string | null; clinicPhone: string | null; patientName: string; patientCode: string; birthYear: number;
  cashierName: string; paidAt: string; paymentMethod: string; source: string | null;
  amount: number; amountTendered: number | null; changeAmount: number | null; note: string | null;
  finalAmount: number; paidAmount: number; remainingAmount: number; printCount: number; printNumber: number; printedAt: string | null;
  items: { name: string; quantity: number; unitPrice: number; totalAmount: number }[];
}
export const paymentReceiptsApi = {
  async preview(invoiceId: number, paymentId: number): Promise<ReceiptPrintDto> {
    return (await apiClient.get<ReceiptPrintDto>(`/invoices/${invoiceId}/payments/${paymentId}/receipt`)).data;
  },
  async print(invoiceId: number, paymentId: number): Promise<ReceiptPrintDto> {
    return (await apiClient.post<ReceiptPrintDto>(`/invoices/${invoiceId}/payments/${paymentId}/reprint`)).data;
  },
};
