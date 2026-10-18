import QRCode from 'qrcode';
import { apiClient } from '@/services/api';
export interface PrescriptionLookupLink { prescriptionCode: string; version: number; relativePath: string }
export const prescriptionQrService = {
 async link(visitId: number): Promise<PrescriptionLookupLink> { return (await apiClient.get('/visits/' + visitId + '/prescription/lookup-link')).data; }
};
export async function MOD_RX_lookupQr(relativePath: string, origin: string) {
 if (relativePath.length !== 38 || !/^\/portal\/prescriptions\/RX-[0-9]{8}-[0-9]{4}$/.test(relativePath)) throw Error('Invalid lookup path');
 const base = new URL(origin);
 if (!['https:', 'http:'].includes(base.protocol) || base.username || base.password || base.origin !== origin) throw Error('Invalid app origin');
 const url = new URL(relativePath, base).href;
 const image = await QRCode.toDataURL(url, { errorCorrectionLevel: 'M', margin: 4, scale: 6 });
 if (!/^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(image)) throw Error('Invalid QR image');
 return { url, image };
}
