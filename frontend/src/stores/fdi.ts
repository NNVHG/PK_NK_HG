import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { ToothCondition } from '@/services/fdi';

export const useFdiStore = defineStore('MOD_FDI', () => {
  const rows = ref<ToothCondition[]>([]);
  function replace(records: ToothCondition[]) { rows.value = records; }
  function prepend(record: ToothCondition) { rows.value.unshift(record); }
  function reset() { rows.value = []; }
  return { rows, replace, prepend, reset };
});
