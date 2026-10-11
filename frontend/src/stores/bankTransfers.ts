import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { BankTransferRequest } from '@/services/bankTransfers';

export const useBankTransfersStore = defineStore('MOD_BIL_bank', () => {
  const pending = ref<Record<string, BankTransferRequest>>({});
  const storageKey = 'MOD_BIL_bank_simulation_pending';
  try {
    const saved: unknown = JSON.parse(sessionStorage.getItem(storageKey) || '{}');
    if (saved && typeof saved === 'object' && !Array.isArray(saved)) for (const [key, value] of Object.entries(saved)) {
      const request = value as Partial<BankTransferRequest> | null;
      if (request && Number.isSafeInteger(request.amount) && Number(request.amount) > 0 &&
        typeof request.addInfo === 'string' && /^PKNK INV-\d{8}-\d{4}$/.test(request.addInfo) &&
        typeof request.transactionReference === 'string' && /^[A-Za-z0-9_-]{6,20}$/.test(request.transactionReference))
        pending.value[key] = request as BankTransferRequest;
    }
  } catch { /* Retry state stays in memory if storage is unavailable. */ }
  function persist() { try { sessionStorage.setItem(storageKey, JSON.stringify(pending.value)); } catch { /* Keep the live request. */ } }
  function put(key: string, request: BankTransferRequest) { pending.value[key] = request; persist(); }
  function clear(key: string) { delete pending.value[key]; persist(); }
  return { pending, put, clear };
});
