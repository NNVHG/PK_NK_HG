import { apiClient } from './api';
export interface DentalImage {
  id: number; patientId: number; visitId: number | null; toothNumber: number | null; category: string;
  originalFileName: string; contentType: string; sizeBytes: number; width: number; height: number; createdAt: string; version: number; notes: string | null;
}
export interface ImageVisitTarget { visitId: number; status: string }
export interface DentalImagePage { items: DentalImage[]; hasMore: boolean }
export interface ImageMetadata { patientId: number; visitId: number | null; toothNumber: number | null; category: string; notes?: string }
export const dentalImagesService = {
  async list(patientId: number, beforeId = 0) { return (await apiClient.get<DentalImagePage>('/dental-images', { params: { patientId, beforeId } })).data; },
  async targets(patientId: number) { return (await apiClient.get<ImageVisitTarget[]>('/dental-images/targets', { params: { patientId } })).data; },
  async upload(metadata: ImageMetadata, file: File) {
    const form = new FormData();
    form.append('patientId', String(metadata.patientId)); form.append('category', metadata.category);
    if (metadata.notes) form.append('notes', metadata.notes);
    if (metadata.visitId !== null) form.append('visitId', String(metadata.visitId));
    if (metadata.toothNumber !== null) form.append('toothNumber', String(metadata.toothNumber));
    form.append('file', file);
    return (await apiClient.post<DentalImage>('/dental-images/upload', form, { headers: { 'Content-Type': undefined } })).data;
  },
  async attach(image: DentalImage, visitId: number) { return (await apiClient.post<DentalImage>(`/dental-images/${image.id}/attach`, { visitId, expectedVersion: image.version })).data; }
};
export function imageFileError(file: Pick<File, 'size' | 'name' | 'type'> | null): string {
  if (!file) return 'Chọn một tệp ảnh.';
  if (file.size < 12 || file.size > 10 * 1024 * 1024) return 'Ảnh phải có dung lượng tối đa 10MB.';
  const ext = file.name.split('.').pop()?.toLowerCase();
  if (!(file.type === 'image/jpeg' && ['jpg', 'jpeg'].includes(ext ?? '') || file.type === 'image/png' && ext === 'png' || file.type === 'image/webp' && ext === 'webp')) return 'Chọn ảnh JPG, PNG hoặc WebP có định dạng khớp tên tệp.';
  return '';
}
export function validImageTooth(tooth: number | null): boolean {
  return tooth === null || Number.isInteger(tooth) && (Math.floor(tooth / 10) >= 1 && Math.floor(tooth / 10) <= 4 && tooth % 10 >= 1 && tooth % 10 <= 8 || Math.floor(tooth / 10) >= 5 && Math.floor(tooth / 10) <= 8 && tooth % 10 >= 1 && tooth % 10 <= 5);
}
