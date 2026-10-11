import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { AssignedService } from '@/services/visitServices';

export const useVisitServicesStore = defineStore('MOD_FDI_services', () => {
  const rows = ref<AssignedService[]>([]);
  function replace(records: AssignedService[]) { rows.value = records; }
  function prepend(records: AssignedService[]) { rows.value.unshift(...records); }
  function reset() { rows.value = []; }
  return { rows, replace, prepend, reset };
});
