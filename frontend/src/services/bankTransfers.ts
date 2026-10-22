import { apiClient } from '@/services/api';

export interface BankTransferRequest { addInfo: string; amount: number; transactionReference: string; }
export interface ManualBankTransferRequest { requestId: string; amount: number; transactionReference: string;
  expectedRemainingAmount: number; confirmed: true; }
export interface BankTransferResponse { id: number; invoiceId: number; amount: number; bankReceivedAmount: number;
  changeAmount: number; transactionReference: string; source: string; note: string | null; paidAt: string; }
export interface BankTransferInfo { invoiceId: number; invoiceCode: string; remainingAmount: number; content: string;
  qrUrl: string | null; accountName: string | null; simulationEnabled: boolean; configurationMessage: string | null; }
export const bankTransfersApi = {
  async state(invoiceId: number): Promise<{ id: number; invoiceCode: string; status: number; paidAmount: number; remainingAmount: number }> {
    return (await apiClient.get(`/invoices/${invoiceId}`)).data;
  },
  async info(invoiceId: number): Promise<BankTransferInfo> { return (await apiClient.get(`/invoices/${invoiceId}/bank-transfer-info`)).data; },
  async confirmManual(invoiceId: number, request: ManualBankTransferRequest): Promise<BankTransferResponse> {
    return (await apiClient.post(`/invoices/${invoiceId}/bank-transfer/confirm`, request)).data;
  },
  async simulate(request: BankTransferRequest): Promise<BankTransferResponse> {
    return (await apiClient.post('/webhooks/payment/bank-transfer/simulate', {
      invoiceCode: request.addInfo.replace(/^PKNK /, ''), amount: request.amount, transactionReference: request.transactionReference
    })).data;
  }
};
