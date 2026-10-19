import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { MedicineReceiptRequest } from '@/services/medicineStock';

export const useMedicineStockStore = defineStore('MOD_RX_medicine_stock', () => {
  const pending = ref<{ actor: string; medicineId: number; request: MedicineReceiptRequest } | null>(null);
  function start(actor: string, medicineId: number, request: MedicineReceiptRequest) { if (!pending.value) pending.value = { actor, medicineId, request: { ...request } }; }
  function reset() { pending.value = null; }
  return { pending, start, reset };
});
