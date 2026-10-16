import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { DentalChair } from '@/services/dentalChairs';
import type { PagedResult } from '@/services/patients';
export const useDentalChairsStore = defineStore('MOD_MST_chairs', () => {
  const result = ref<PagedResult<DentalChair> | null>(null);
  function replace(value: PagedResult<DentalChair> | null) { result.value = value; }
  function reset() { result.value = null; }
  return { result, replace, reset };
});
