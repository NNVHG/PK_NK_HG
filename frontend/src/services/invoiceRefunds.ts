import { apiClient } from '@/services/api';
export interface RefundSelection { invoiceItemId: number; quantity: number }
export interface RefundLine { id: number; itemType: string; code: string; name: string; originalQuantity: number; refundedQuantity: number; remainingQuantity: number; unitPrice: string }
export interface RefundRecord { id: number; revision: number; requestId: string; paymentId: number | null; voucherCode: string | null; refundAmount: string; debtReduction: string; totalAfter: string; discountAfter: string; paidAfter: string; remainingAfter: string; reasonCode: string; reasonNotes: string; paymentMethod: string; createdAt: string; actorName: string }
export interface RefundContext { invoiceId: number; invoiceCode: string; status: number; snapshot: string; totalAmount: string; discountAmount: string; paidAmount: string; remainingAmount: string; items: RefundLine[]; history: RefundRecord[] }
export interface RefundPreview { preview: string; refundAmount: string; debtReduction: string; totalAfter: string; discountAfter: string; paidAfter: string; remainingAfter: string; fullCancellation: boolean; requiresLateApproval: boolean; requiresReserve: boolean; cashBalance: string }
export interface RefundRequest { snapshot: string; items: RefundSelection[]; paymentMethod: string; expectedPreview: string; requestId: string; reasonCode: string; reasonNotes: string; confirmed: boolean; lateApproval: boolean; reserveConfirmed: boolean }
export interface RefundVoucher { record: RefundRecord; invoiceCode: string; clinicName: string; clinicAddress: string | null; clinicPhone: string | null; patientName: string; printNumber: number }
const money = (s: unknown): s is string => typeof s === 'string' && /^(0|[1-9][0-9]{0,17})$/.test(s);
const positive = (n: unknown): n is number => Number.isSafeInteger(n) && Number(n) > 0;
export const MOD_BIL_validPreview = (p: RefundPreview) => p && /^[A-F0-9]{64}$/.test(p.preview) &&
 [p.refundAmount,p.debtReduction,p.totalAfter,p.discountAfter,p.paidAfter,p.remainingAfter].every(money) &&
 /^-?(0|[1-9][0-9]{0,17})$/.test(p.cashBalance) && [p.fullCancellation,p.requiresLateApproval,p.requiresReserve].every(x=>typeof x==='boolean');
export const MOD_BIL_validRefundRecord = (r: RefundRecord) => r && positive(r.id) && positive(r.revision) && typeof r.requestId==='string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(r.requestId) &&
 typeof r.reasonCode==='string' && typeof r.reasonNotes==='string' && typeof r.actorName==='string' && Number.isFinite(Date.parse(r.createdAt)) && ['Cash','BankTransfer'].includes(r.paymentMethod) &&
 [r.refundAmount,r.debtReduction,r.totalAfter,r.discountAfter,r.paidAfter,r.remainingAfter].every(money) &&
 (r.paymentId===null && r.refundAmount==='0' && r.voucherCode===null || positive(r.paymentId) && r.refundAmount!=='0' && /^PC-\d{8}-\d+$/.test(r.voucherCode??''));
export const MOD_BIL_validRefundContext = (c: RefundContext,id: number) => c && c.invoiceId===id && typeof c.invoiceCode==='string' && Number.isInteger(c.status) && c.status>=0 && c.status<=6 && /^[A-F0-9]{64}$/.test(c.snapshot) &&
 [c.totalAmount,c.discountAmount,c.paidAmount,c.remainingAmount].every(money) && Array.isArray(c.items) && c.items.every(i=>positive(i.id) && positive(i.originalQuantity) && Number.isInteger(i.refundedQuantity) && i.refundedQuantity>=0 &&
 i.remainingQuantity===i.originalQuantity-i.refundedQuantity && i.remainingQuantity>=0 && money(i.unitPrice)) && new Set(c.items.map(i=>i.id)).size===c.items.length &&
 Array.isArray(c.history) && c.history.every(MOD_BIL_validRefundRecord);
export const invoiceRefundsApi = {
 async read(id: number) { return (await apiClient.get<RefundContext>(`/invoices/${id}/refund`)).data; },
 async preview(id: number,snapshot: string,items: RefundSelection[],paymentMethod: string) { return (await apiClient.post<RefundPreview>(`/invoices/${id}/refund/preview`,{snapshot,items,paymentMethod})).data; },
 async refund(id: number,request: RefundRequest) { return (await apiClient.post<RefundRecord>(`/invoices/${id}/refund`,request)).data; },
 async print(id: number,refund: number) { return (await apiClient.post<RefundVoucher>(`/invoices/${id}/refund/${refund}/print`)).data; }
};
export function MOD_BIL_refundMoney(s: string) { return /^(0|[1-9][0-9]{0,17})$/.test(s) ? new Intl.NumberFormat('vi-VN').format(BigInt(s))+' ₫' : '—'; }
