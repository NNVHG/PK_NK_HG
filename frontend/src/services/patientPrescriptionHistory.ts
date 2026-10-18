import { apiClient } from '@/services/api';
import type { PagedResult } from '@/services/patients';
export interface PrescriptionPrintLine { medicineName: string; strength: string | null; unit: string; quantity: number; usageInstructions: string; treatmentDays: number | null }
export interface PatientPrescriptionSummary {
 id: number; patientId: number; visitId: number; version: number; prescriptionCode: string; createdAt: string;
 doctorName: string; visitDiagnosis: string; medicineCount: number; isCancelled: boolean; dispensedAt: string | null; canPrint: boolean;
}
export interface PatientPrescriptionDetail extends Omit<PatientPrescriptionSummary,'medicineCount'> {
 diagnosis: string; doctorAdvice: string | null; revisitDate: string | null; items: PrescriptionPrintLine[];
}
export function MOD_RX_validHistoryRow(row: PatientPrescriptionSummary, patientId: number): boolean {
 return !!row && row.patientId === patientId && [row.id,row.visitId,row.version].every(n => Number.isInteger(n) && n > 0 && n <= 2147483647) &&
  typeof row.prescriptionCode === 'string' && /^RX-\d{8}-\d{4,}$/.test(row.prescriptionCode) && Number.isFinite(Date.parse(row.createdAt)) &&
  typeof row.doctorName === 'string' && typeof row.visitDiagnosis === 'string' && Number.isInteger(row.medicineCount) && row.medicineCount >= 0 &&
  typeof row.isCancelled === 'boolean' && typeof row.canPrint === 'boolean' && (!row.isCancelled || !row.canPrint) &&
  (row.dispensedAt === null || Number.isFinite(Date.parse(row.dispensedAt)));
}
export function MOD_RX_validHistoryDetail(data: PatientPrescriptionDetail, patientId: number, id: number): boolean {
 return !!data && Array.isArray(data.items) && MOD_RX_validHistoryRow({...data,medicineCount:data.items.length},patientId) && data.id === id &&
  typeof data.diagnosis === 'string' && (data.doctorAdvice === null || typeof data.doctorAdvice === 'string') &&
  (data.revisitDate === null || /^\d{4}-\d{2}-\d{2}$/.test(data.revisitDate)) && data.items.every(i =>
   i && typeof i.medicineName === 'string' && typeof i.unit === 'string' && Number.isInteger(i.quantity) && i.quantity > 0 &&
   typeof i.usageInstructions === 'string' && (i.strength === null || typeof i.strength === 'string') &&
   (i.treatmentDays === null || Number.isInteger(i.treatmentDays) && i.treatmentDays > 0));
}
export const patientPrescriptionHistoryService = {
 async list(patientId: number, page = 1): Promise<PagedResult<PatientPrescriptionSummary>> {
  return (await apiClient.get(`/patients/${patientId}/prescriptions`, { params:{page,pageSize:10} })).data;
 },
 async detail(patientId: number, id: number): Promise<PatientPrescriptionDetail> {
  return (await apiClient.get(`/patients/${patientId}/prescriptions/${id}`)).data;
 }
};
