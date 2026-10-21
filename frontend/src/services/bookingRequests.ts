import {apiClient} from './api';
export interface BookingIntent {fullName:string;phone:string;dateOfBirth:string;gender:string;email:string|null;appointmentDate:string;slotTime:string;dentistId:number|null;notes:string|null;scheduleRevision:string;patientId:number|null}
export interface BookingCatalog {today:string;slots:string[];durationMinutes:number;revision:string;dentists:{dentistId:number;fullName:string}[]}
export interface BookingChallenge {challengeId:string;expiresInSeconds:number;resendAfterSeconds:number}
export interface BookingSubmission {intent:BookingIntent;challengeId:string;code:string;clientRequestId:string;confirmed:boolean}
export interface BookingReceipt {requestId:string;status:string}
export interface BookingRequestRow {id:number;fullName:string;phone:string;dateOfBirth:string;appointmentDate:string;slotTime:string;slotDurationMinutes:number;dentistId:number|null;dentistName:string|null;notes:string|null;isReturning:boolean;createdAt:string;version:number}
export interface BookingRequestPage {items:BookingRequestRow[];totalCount:number;page:number;pageSize:number;hasNextPage:boolean}
export const bookingRequestsService={
 async catalog(){return(await apiClient.get<BookingCatalog>('/appointment-requests/catalog')).data;},
 async otp(intent:BookingIntent){return(await apiClient.post<BookingChallenge>('/appointment-requests/otp',{intent})).data;},
 async submit(body:BookingSubmission){return(await apiClient.post<BookingReceipt>('/appointment-requests',body)).data;},
 async list(page=1){return(await apiClient.get<BookingRequestPage>('/appointment-requests',{params:{page}})).data;},
 async reject(id:number,expectedVersion:number,reason:string){await apiClient.post(`/appointment-requests/${id}/reject`,{expectedVersion,reason,confirmed:true});}
};
