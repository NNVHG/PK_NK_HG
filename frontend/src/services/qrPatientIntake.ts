import { apiClient } from '@/services/api';
import type { PatientDetails, PatientPayload } from './patients';
export interface ScannedIdentity { citizenIdHash: string; citizenIdMasked: string; fullName: string; dateOfBirth: string; gender: 'Male' | 'Female'; address: string }
export interface QrExistingPatient { patientId: number; patientCode: string; fullName: string; masked: string; isActive: boolean }
const invalid = () => Error('Mã QR không đúng định dạng CCCD gắn chip. Vui lòng quét lại hoặc nhập tay.');
function isoDate(value: string): string {
 if (!/^\d{8}$/.test(value)) throw invalid();
 const day=Number(value.slice(0,2)), month=Number(value.slice(2,4)), year=Number(value.slice(4));
 const date=new Date(Date.UTC(year,month-1,day));
 if (year<1900 || date.getUTCFullYear()!==year || date.getUTCMonth()!==month-1 || date.getUTCDate()!==day) throw invalid();
 return value.slice(4)+'-'+value.slice(2,4)+'-'+value.slice(0,2);
}
export function validQrBirthDate(value:string):boolean {
 if(!/^\d{4}-\d{2}-\d{2}$/.test(value))return false;
 try {if(isoDate(value.slice(8)+value.slice(5,7)+value.slice(0,4))!==value)return false;}catch{return false;}
 const today=new Date().toLocaleDateString('sv-SE',{timeZone:'Asia/Ho_Chi_Minh'});
 const oldestYear=Number(today.slice(0,4))-120,month=Number(today.slice(5,7)),day=Number(today.slice(8));
 const lastDay=new Date(Date.UTC(oldestYear,month,0)).getUTCDate();
 const earliest=String(oldestYear).padStart(4,'0')+'-'+today.slice(5,7)+'-'+String(Math.min(day,lastDay)).padStart(2,'0');
 return value>=earliest&&value<=today;
}
export async function decodeCitizenQr(raw: string): Promise<ScannedIdentity> {
 if (raw.length>1024 || /[\u0000-\u001f\u007f]/.test(raw)) throw invalid();
 const fields=raw.trim().split('|').map(v=>v.trim().normalize('NFC'));
 if (fields.length!==7) throw invalid();
 const [id,oldId,name,birth,gender,address,issued]=fields as [string,string,string,string,string,string,string];
 if (!/^[0-9]{12}$/.test(id) || oldId && !/^[0-9]{9}$/.test(oldId) || !name || name.length>100 || !address || address.length>500 || !['Nam','Nữ'].includes(gender)) throw invalid();
 const dateOfBirth=isoDate(birth), today=new Date().toLocaleDateString('sv-SE',{timeZone:'Asia/Ho_Chi_Minh'});
 if (!validQrBirthDate(dateOfBirth)) throw invalid();
 if (issued && (isoDate(issued)<dateOfBirth || isoDate(issued)>today)) throw invalid();
 if (!globalThis.crypto?.subtle) throw Error('Trình duyệt cần HTTPS hoặc localhost để bảo vệ CCCD. Có thể chuyển sang nhập tay.');
 const digest=await globalThis.crypto.subtle.digest('SHA-256',new TextEncoder().encode(id));
 const citizenIdHash=Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('').toUpperCase();
 return { citizenIdHash,citizenIdMasked:id.slice(0,3)+'******'+id.slice(-3),fullName:name,dateOfBirth,gender:gender==='Nam'?'Male':'Female',address };
}
export const qrPatientIntakeService={
 async lookup(citizenIdHash: string): Promise<{existing:QrExistingPatient|null}> { return (await apiClient.post('/patients/check-cccd',{citizenIdHash})).data; },
 async create(identity:ScannedIdentity,patient:PatientPayload): Promise<PatientDetails> {
  return (await apiClient.post('/patients/qr-intake',{citizenIdHash:identity.citizenIdHash,citizenIdMasked:identity.citizenIdMasked,patient,confirmed:true})).data;
 }
};
