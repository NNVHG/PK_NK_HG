import {apiClient} from './api';
export interface BookingIntent {fullName:string;phone:string;dateOfBirth:string;gender:string;email:string|null;appointmentDate:string;slotTime:string;dentistId:number|null;notes:string|null;scheduleRevision:string;patientId:number|null}
export interface BookingCatalog {today:string;slots:string[];durationMinutes:number;revision:string}
export interface BookingSubmission {intent:BookingIntent;clientRequestId:string;confirmed:boolean}
export interface BookingReceipt {requestId:string;status:string}
export interface BookingRequestRow {id:number;fullName:string;phone:string;dateOfBirth:string;appointmentDate:string;slotTime:string;slotDurationMinutes:number;dentistId:number|null;dentistName:string|null;notes:string|null;isReturning:boolean;createdAt:string;version:number}
export interface BookingRequestPage {items:BookingRequestRow[];totalCount:number;page:number;pageSize:number;hasNextPage:boolean}
export interface BookingApprovalResult {appointmentId:number;patientId:number;createdAccount:boolean;requiresPhoneActivation:boolean}
export interface BookingApprovalContext {id:number;version:number;status:string;fullName:string;phone:string;dateOfBirth:string;gender:string;appointmentDate:string;slotTime:string;slotDurationMinutes:number;dentistName:string|null;verifiedPatientId:number|null;account:{userId:number;fullName:string}|null;patients:{patientId:number;fullName:string;dateOfBirth:string;canSelect:boolean}[];requiresCounterConfirmation:boolean;blockReason:string|null;reviewStamp:string;result:BookingApprovalResult|null}
export interface ApproveBookingRequest {expectedVersion:number;reviewStamp:string;patientId:number|null;confirmedNewPatient:boolean;confirmedAtCounter:boolean;confirmed:boolean}
export const bookingRequestsService={
 async catalog(){return(await apiClient.get<BookingCatalog>('/appointment-requests/catalog')).data;},
 async submit(body:BookingSubmission){return(await apiClient.post<BookingReceipt>('/appointment-requests',body)).data;},
 async list(page=1){return(await apiClient.get<BookingRequestPage>('/appointment-requests',{params:{page}})).data;},
 async reject(id:number,expectedVersion:number,reason:string){await apiClient.post(`/appointment-requests/${id}/reject`,{expectedVersion,reason,confirmed:true});},
 async approval(id:number){return(await apiClient.get<BookingApprovalContext>(`/appointment-requests/${id}/approval`)).data;},
 async approve(id:number,body:ApproveBookingRequest){return(await apiClient.post<BookingApprovalResult>(`/appointment-requests/${id}/approve`,body)).data;}
};
