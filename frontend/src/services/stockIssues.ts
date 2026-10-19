import { apiClient } from '@/services/api';
export interface StockIssueTarget { visitId: number; patientId: number; patientCode: string; patientName: string; endedAt: string | null }
export interface IssueRequest { requestId: string; confirmed: boolean; visitId: number; notes: string | null; lines: { medicalSupplyId: number; quantity: number; expectedSupplyVersion: number }[] }
export interface StockIssue { id: number; code: string; visitId: number; notes: string | null; confirmedAt: string; confirmedByUserId: number; confirmedByName: string; lines: { medicalSupplyId: number; code: string; name: string; unit: string; quantity: number }[] }
export const stockIssuesService = {
  async targets(visitId?: number, beforeId = 0): Promise<{ items: StockIssueTarget[]; hasMore: boolean }> { return (await apiClient.get('/inventory/issues/targets', { params: { visitId, beforeId } })).data; },
  async list(visitId: number, beforeId = 0): Promise<{ items: StockIssue[]; hasMore: boolean }> { return (await apiClient.get('/inventory/issues', { params: { visitId, beforeId } })).data; },
  async confirm(payload: IssueRequest): Promise<{ issue: StockIssue; replayed: boolean }> { return (await apiClient.post('/inventory/issues', payload)).data; }
};
