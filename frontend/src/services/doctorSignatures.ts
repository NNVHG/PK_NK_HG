import { apiClient } from '@/services/api';
export interface DoctorSignatureStatus { doctorId: number; doctorName: string; version: number; signatureImageUrl: string | null; updatedAt: string | null }
export const doctorSignaturesService = {
 async read(id: number): Promise<DoctorSignatureStatus> { return (await apiClient.get('/doctors/' + id + '/signature')).data; },
 async file(id: number, version: number): Promise<Blob> { return (await apiClient.get('/doctors/' + id + '/signature/file', { params: { version }, responseType: 'blob' })).data; },
 async save(id: number, expectedVersion: number, imageBase64: string): Promise<DoctorSignatureStatus> { return (await apiClient.post('/doctors/' + id + '/signature', { expectedVersion, imageBase64 })).data; }
};
export async function MOD_AUTH_signatureFile(file: File) {
 if (file.type !== 'image/png' || file.size <= 0 || file.size > 256 * 1024) throw Error('Chọn PNG tối đa 256 KB.');
 const bytes = new Uint8Array(await file.arrayBuffer());
 if (bytes.length !== file.size || bytes.length < 8 || [137,80,78,71,13,10,26,10].some((n, i) => bytes[i] !== n)) throw Error('Tệp không phải PNG hợp lệ.');
 let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
 return btoa(binary);
}
