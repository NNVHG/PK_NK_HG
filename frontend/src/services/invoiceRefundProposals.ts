import { apiClient } from '@/services/api';
import type { RefundSelection, RefundPreview, RefundRecord } from '@/services/invoiceRefunds';
export interface RefundProposalLine { invoiceItemId: number; itemType: string; code: string; name: string; quantity: number; unitPrice: string }
export interface RefundProposal { id: number; invoiceId: number; invoiceCode: string; snapshot: string; requestId: string; status: 'Pending'|'Approved'|'Rejected'|'Stale'; createdAt: string; requesterName: string; reasonCode: string; reasonNotes: string; items: RefundProposalLine[]; refundId: number|null; decisionNotes: string|null }
export interface RefundProposalCreate { items: RefundSelection[]; reasonCode: string; reasonNotes: string }
export interface RefundProposalDecision { paymentMethod: string; expectedPreview: string; confirmed: boolean; lateApproval: boolean; reserveConfirmed: boolean }
export const invoiceRefundProposalsApi = {
 async create(invoiceId: number, body: RefundProposalCreate) { return (await apiClient.post<RefundProposal>(`/invoices/${invoiceId}/refund-proposals`,body)).data; },
 async list(invoiceId: number) { return (await apiClient.get<RefundProposal[]>(`/invoices/${invoiceId}/refund-proposals`)).data; },
 async preview(id: number, paymentMethod: string) { return (await apiClient.post<RefundPreview>(`/refund-proposals/${id}/preview`,{paymentMethod})).data; },
 async approve(id: number, body: RefundProposalDecision) { return (await apiClient.post<RefundRecord>(`/refund-proposals/${id}/approve`,body)).data; },
 async reject(id: number, decisionNotes: string | null) { return (await apiClient.post(`/refund-proposals/${id}/reject`,{decisionNotes})).data; }
};