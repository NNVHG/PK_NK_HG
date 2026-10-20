import { apiClient } from './api';
export interface FamilyAccountCandidate { userId:number; fullName:string; phone:string; dateOfBirth:string|null }
export interface PatientAccountLinkContext { patientId:number; patientName:string; dateOfBirth:string; phone:string; isLinked:boolean; account:FamilyAccountCandidate|null; reviewStamp:string|null }
export interface PatientAccountLinked { patientId:number; accountUserId:number }
export const patientAccountLinkService = {
  async read(patientId:number) { return (await apiClient.get<PatientAccountLinkContext>(`/patients/${patientId}/family-account`)).data; },
  async link(patientId:number, accountUserId:number, reviewStamp:string) { return (await apiClient.post<PatientAccountLinked>(`/patients/${patientId}/family-account`, {accountUserId,reviewStamp,confirmed:true})).data; }
};
