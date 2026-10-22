import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { ManualBankTransferRequest } from '@/services/bankTransfers';

export const useManualBankTransfersStore = defineStore('MOD_BIL_manual_bank', () => {
  const storageKey = 'MOD_BIL_manual_bank_pending';
  const pending = ref<Record<string, ManualBankTransferRequest>>({});
  try {
    const saved: unknown = JSON.parse(sessionStorage.getItem(storageKey) || '{}');
    if (saved && typeof saved === 'object' && !Array.isArray(saved)) for (const [key, value] of Object.entries(saved)) {
      const r = value as Partial<ManualBankTransferRequest> | null;
      if (r && r.confirmed === true && Number.isSafeInteger(r.amount) && Number(r.amount) > 0 &&
        Number.isSafeInteger(r.expectedRemainingAmount) && Number(r.expectedRemainingAmount) > 0 &&
        typeof r.requestId === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(r.requestId) &&
        typeof r.transactionReference === 'string' && /^[A-Za-z0-9_-]{6,20}$/.test(r.transactionReference))
        pending.value[key] = r as ManualBankTransferRequest;
    }
  } catch { /* Keep the live retry request if storage is unavailable. */ }
  function persist() { try { sessionStorage.setItem(storageKey, JSON.stringify(pending.value)); } catch { /* In-memory fallback. */ } }
  function put(key: string, request: ManualBankTransferRequest) { pending.value[key] = request; persist(); }
  function clear(key: string) { delete pending.value[key]; persist(); }
  return { pending, put, clear };
});
