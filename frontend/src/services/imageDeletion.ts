import { apiClient } from './api';
export interface ImageDeletionItem {
  id:number; patientId:number; visitId:number|null; version:number; originalFileName:string;
  category:string; toothNumber:number|null; createdAt:string; canDelete:boolean; unavailableReason:string|null;
}
export interface ImageDeletionPage { items:ImageDeletionItem[]; hasMore:boolean }
export const imageDeletionService = {
  async list(patientId:number,beforeId=0):Promise<ImageDeletionPage> {
    return (await apiClient.get('/dental-images/deletion',{params:{patientId,beforeId}})).data;
  },
  async remove(imageId:number,expectedVersion:number,reason:string):Promise<number> {
    return (await apiClient.delete(`/dental-images/${imageId}`,{data:{expectedVersion,reason,confirmed:true}})).status;
  }
};
