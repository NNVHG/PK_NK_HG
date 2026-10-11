import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { CashPaymentRequest } from '@/services/cashPayments';

export const useCashPaymentsStore = defineStore('MOD_BIL_cash', () => {
  // Retain the same request on a retry across route remounts; never create a new charge for an uncertain response.
  const storageKey = 'MOD_BIL_cash_pending';
  let restored: Record<string, CashPaymentRequest> = {};
  try {
    const saved: unknown = JSON.parse(sessionStorage.getItem(storageKey) || '{}');
    if (saved && typeof saved === 'object' && !Array.isArray(saved)) {
      for (const [key, value] of Object.entries(saved)) {
        const request = value as Partial<CashPaymentRequest> | null;
        if (request && Number.isSafeInteger(request.amount) && Number(request.amount) > 0 &&
          Number.isSafeInteger(request.amountTendered) && Number(request.amountTendered) >= Number(request.amount) &&
          request.paymentMethod === 'Cash' && typeof request.requestId === 'string' &&
          /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(request.requestId)) {
          restored[key] = request as CashPaymentRequest;
        }
      }
    }
  } catch { /* Keep the in-memory fallback. */ }
  const pending = ref<Record<string, CashPaymentRequest>>(restored);
  function persist() { try { sessionStorage.setItem(storageKey, JSON.stringify(pending.value)); } catch { /* Do not lose the live request when storage is unavailable. */ } }
  function put(key: string, request: CashPaymentRequest) { pending.value[key] = request; persist(); }
  function clear(key: string) { delete pending.value[key]; persist(); }
  return { pending, put, clear };
});
