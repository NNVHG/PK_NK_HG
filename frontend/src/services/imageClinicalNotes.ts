import { apiClient } from './api';
import type { PagedResult } from './patients';
export interface ImageClinicalContext {
 imageId:number;patientId:number;visitId:number|null;version:number;clinicalNotes:string|null;doctorDiagnosis:string|null;
 updatedByUserId:number|null;updatedByName:string|null;updatedAt:string|null;canReplace:boolean;canAppend:boolean;
}
export interface ImageClinicalAddendum {id:number;imageId:number;imageVersion:number;createdByUserId:number;authorName:string;authorRole:string;createdAt:string;note:string}
export const imageClinicalService={
 async read(image:number):Promise<ImageClinicalContext>{return(await apiClient.get(`/dental-images/${image}/clinical-notes`)).data;},
 async history(image:number,page=1):Promise<PagedResult<ImageClinicalAddendum>>{return(await apiClient.get(`/dental-images/${image}/clinical-notes/addenda`,{params:{page,pageSize:20}})).data;},
 async replace(image:number,expectedVersion:number,clinicalNotes:string,doctorDiagnosis:string):Promise<ImageClinicalContext>{return(await apiClient.put(`/dental-images/${image}/clinical-notes`,{expectedVersion,clinicalNotes,doctorDiagnosis,confirmed:true})).data;},
 async append(image:number,expectedVersion:number,note:string,requestId:string):Promise<ImageClinicalAddendum>{return(await apiClient.post(`/dental-images/${image}/clinical-notes/addenda`,{expectedVersion,note,requestId,confirmed:true})).data;}
};
