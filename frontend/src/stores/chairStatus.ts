import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { PagedResult } from '@/services/patients';
import type { ChairStatusRow } from '@/services/chairStatus';

export const useChairStatusStore = defineStore('MOD_ASS_chair_status', () => {
  const result = ref<PagedResult<ChairStatusRow> | null>(null);
  function replace(value: PagedResult<ChairStatusRow>) { result.value = value; }
  function reset() { result.value = null; }
  return { result, replace, reset };
});
