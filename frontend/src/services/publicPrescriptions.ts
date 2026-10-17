import axios from 'axios';
// Public lookup has no account JWT interceptor and never expires a staff login.
const client = axios.create({ baseURL: '/api/public/prescriptions', headers: { 'Content-Type': 'application/json' } });
export interface PublicRxSummary { prescriptionCode: string; createdAt: string }
export interface PublicRxSession { token: string; expiresAt: string }
export interface PublicRxDetails extends PublicRxSummary {
 patientName: string; doctorName: string; diagnosis: string; doctorAdvice: string | null; revisitDate: string | null;
 items: { name: string; strength: string | null; unit: string; quantity: number; usageInstructions: string; treatmentDays: number | null }[];
}
export const publicPrescriptionsService = {
 async summary(code: string): Promise<PublicRxSummary> { return (await client.get('/' + encodeURIComponent(code))).data; },
 async verify(code: string, citizenId: string): Promise<PublicRxSession> { return (await client.post('/' + encodeURIComponent(code) + '/verify', { citizenId })).data; },
 async details(code: string, token: string): Promise<PublicRxDetails> { return (await client.get('/' + encodeURIComponent(code) + '/details', { headers: { 'X-Prescription-View-Token': token } })).data; }
};
