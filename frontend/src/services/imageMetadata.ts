import {apiClient} from './api';
export interface ImageMetadataContext {
  id:number;patientId:number;visitId:number|null;version:number;originalFileName:string;
  category:string;toothNumber:number|null;notes:string|null;canEdit:boolean;
}
export const imageMetadataService={
  async read(id:number):Promise<ImageMetadataContext>{return(await apiClient.get(`/dental-images/${id}/metadata`)).data;},
  async update(id:number,expectedVersion:number,category:string,toothNumber:number|null,notes:string):Promise<ImageMetadataContext>{
    return(await apiClient.put(`/dental-images/${id}/metadata`,{expectedVersion,category,toothNumber,notes,confirmed:true})).data;
  }
};
