import { apiClient } from '@/services/api';

export interface MedicineReceiptRequest { requestId: string; quantity: number; expectedMedicineVersion: number; supplierName: string; notes: string | null; confirmed: boolean; }
export interface MedicineReceipt { id: number; code: string; medicineId: number; medicineCode: string; medicineName: string; unit: string; quantity: number; supplierName: string; notes: string | null; confirmedAt: string; confirmedByName: string; }
export interface MedicineStockDetail { medicineId: number; code: string; name: string; unit: string; medicineVersion: number; isActive: boolean; onHand: number; receipts: MedicineReceipt[]; hasMore: boolean; }
export interface MedicineAvailability { medicineId: number; code: string; name: string; unit: string; onHand: number; isActive: boolean; }
export const medicineStockService = {
  async read(id: number, beforeId = 0): Promise<MedicineStockDetail> { return (await apiClient.get<MedicineStockDetail>(`/medicine-stock/${id}`, { params: { beforeId } })).data; },
  async receive(id: number, input: MedicineReceiptRequest): Promise<{ receipt: MedicineReceipt; replayed: boolean }> { return (await apiClient.post<{ receipt: MedicineReceipt; replayed: boolean }>(`/medicine-stock/${id}/receipts`, input)).data; },
  async availability(visitId: number, ids: number[]): Promise<MedicineAvailability[]> {
    const params = new URLSearchParams(); ids.forEach(id => params.append('medicineIds', String(id)));
    return (await apiClient.get<MedicineAvailability[]>(`/visits/${visitId}/prescription/stock`, { params })).data;
  }
};
