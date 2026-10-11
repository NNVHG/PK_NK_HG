export type BankQrDisplay = 'online' | 'offline-demo' | 'unavailable';

/** Demo artwork is permitted only when the API enables Development simulation. */
export function MOD_BIL_bankQrDisplay(qrUrl: string | null, failed: boolean, simulationEnabled: boolean): BankQrDisplay {
  if (qrUrl && !failed) return 'online';
  return simulationEnabled ? 'offline-demo' : 'unavailable';
}
