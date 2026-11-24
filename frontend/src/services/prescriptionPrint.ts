import { apiClient } from '@/services/api';
export interface PrescriptionPrintDocument {
 prescriptionId: number; visitId: number; version: number; prescriptionCode: string;
 clinicName: string; clinicAddress: string; clinicPhone: string; patientName: string; dateOfBirth: string; gender: string | null; patientAddress: string | null; patientPhone: string;
 diagnosis: string; items: { medicineName: string; strength: string | null; unit: string; quantity: number; usageInstructions: string; treatmentDays: number | null }[];
 doctorAdvice: string | null; revisitDate: string | null; prescribedDate: string; doctorName: string; doctorTitle: string;
 relativeLookupPath: string; receiverName: string | null; receiverPhone: string | null;
}
export const prescriptionPrintService = {
 async prepare(visitId: number, prescriptionId: number, expectedVersion: number): Promise<PrescriptionPrintDocument> { return (await apiClient.post('/visits/' + visitId + '/prescription/' + prescriptionId + '/print', { expectedVersion })).data; }
};
export function MOD_RX_validPrint(d: PrescriptionPrintDocument, visitId: number, prescriptionId: number, version: number) {
 return d.visitId === visitId && d.prescriptionId === prescriptionId && d.version === version && /^RX-[0-9]{8}-[0-9]{4}$/.test(d.prescriptionCode) &&
  d.relativeLookupPath === '/portal/prescriptions/' + d.prescriptionCode &&
  [d.clinicName,d.clinicAddress,d.clinicPhone,d.patientName,d.doctorName,d.doctorTitle,d.diagnosis].every(s => typeof s === 'string' && s.trim().length > 0) &&
  /^\d{4}-\d{2}-\d{2}$/.test(d.dateOfBirth) && /^\d{4}-\d{2}-\d{2}$/.test(d.prescribedDate) &&
  Array.isArray(d.items) && d.items.length >= 1 && d.items.length <= 10 && d.items.every(i => Number.isInteger(i.quantity) && i.quantity >= 1 && i.quantity <= 100 && [i.medicineName,i.unit,i.usageInstructions].every(s => typeof s === 'string' && s.trim().length > 0));
}
