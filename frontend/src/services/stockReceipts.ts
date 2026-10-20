import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface StockItem { medicalSupplyId: number; code: string; name: string; unit: string; onHand: number; minimumStockThreshold: number; isActive: boolean; supplyVersion: number }
export interface ReceiptLine { medicalSupplyId: number; quantity: number; expectedSupplyVersion: number }
export interface ReceiptRequest { requestId: string; confirmed: boolean; supplierName: string; notes: string | null; lines: ReceiptLine[] }
export interface StockReceipt { id: number; code: string; supplierName: string; notes: string | null; confirmedAt: string; confirmedByUserId: number; confirmedByName: string; lines: { medicalSupplyId: number; code: string; name: string; unit: string; quantity: number }[] }
export const stockReceiptsService = {
  async stock(keyword = '', page = 1): Promise<PagedResult<StockItem>> { return (await apiClient.get('/inventory/stock', { params: { keyword, page } })).data; },
  async list(beforeId = 0): Promise<{ items: StockReceipt[]; hasMore: boolean }> { return (await apiClient.get('/inventory/receipts', { params: { beforeId } })).data; },
  async confirm(payload: ReceiptRequest): Promise<{ receipt: StockReceipt; replayed: boolean }> { return (await apiClient.post('/inventory/receipts', payload)).data; }
};
