import { apiClient } from '@/services/api';
export interface HandoverStock {
 needsDeduction: boolean; legacyHandover: boolean; canFulfill: boolean;
 lines: { medicineId: number; code: string; name: string; unit: string; quantity: number; onHand: number; unitMatches: boolean }[];
}
export interface HandoverStatus {
 prescriptionId: number; visitId: number; version: number; prescriptionCode: string; patientName: string;
 receiverRequired: boolean; expiresAt: string; dispensedAt: string | null; dispensedByName: string | null;
 receivedByProxy: boolean; receiverName: string | null; receiverPhone: string | null;
 canDispense: boolean; canUndo: boolean; canOverrideExpired: boolean; blockReason: string | null;
 stock?: HandoverStock | null;
}
export interface DispensePayload { expectedVersion: number; confirmed: boolean; receivedByProxy: boolean; receiverName: string | null; receiverPhone: string | null; overrideExpired: boolean; overrideReason: string | null }
export function MOD_RX_validHandover(s: HandoverStatus, id: number, visitId: number): boolean {
 return !!s && s.prescriptionId === id && s.visitId === visitId && Number.isInteger(s.version) && s.version > 0 && s.version <= 2147483647 &&
  typeof s.prescriptionCode === 'string' && /^RX-\d{8}-\d{4,}$/.test(s.prescriptionCode) && typeof s.patientName === 'string' && !!s.patientName.trim() &&
  ['receiverRequired','receivedByProxy','canDispense','canUndo','canOverrideExpired'].every(k => typeof s[k as keyof HandoverStatus] === 'boolean') &&
  Number.isFinite(Date.parse(s.expiresAt)) && (s.dispensedAt === null || Number.isFinite(Date.parse(s.dispensedAt))) &&
  ['receiverName','receiverPhone','dispensedByName','blockReason'].every(k => s[k as keyof HandoverStatus] === null || typeof s[k as keyof HandoverStatus] === 'string') &&
  (s.stock === undefined || s.stock === null || MOD_RX_validStock(s.stock));
}
export function MOD_RX_validStock(s: HandoverStock): boolean {
 return !!s && ['needsDeduction','legacyHandover','canFulfill'].every(k => typeof s[k as keyof HandoverStock] === 'boolean') &&
  !(s.needsDeduction && s.legacyHandover) && Array.isArray(s.lines) && s.lines.length <= 10 &&
  new Set(s.lines.map(l => l.medicineId)).size === s.lines.length && s.lines.every(l => Number.isInteger(l.medicineId) && l.medicineId > 0 &&
   Number.isInteger(l.quantity) && l.quantity > 0 && Number.isInteger(l.onHand) && l.onHand >= 0 && l.onHand <= 2147483647 &&
   typeof l.code === 'string' && typeof l.name === 'string' && typeof l.unit === 'string' && typeof l.unitMatches === 'boolean');
}
export const prescriptionHandoverService = {
 async read(id: number): Promise<HandoverStatus> { return (await apiClient.get(`/prescriptions/${id}/dispense`)).data; },
 async dispense(id: number, payload: DispensePayload): Promise<HandoverStatus> { return (await apiClient.post(`/prescriptions/${id}/dispense`, payload)).data; },
 async undo(id: number, expectedVersion: number, reason: string): Promise<HandoverStatus> { return (await apiClient.post(`/prescriptions/${id}/undo-dispense`, { expectedVersion, confirmed: true, reason })).data; }
};
