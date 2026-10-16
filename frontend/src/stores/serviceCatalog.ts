import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { CatalogDetail, CatalogResult } from '@/services/serviceCatalog';

export const useServiceCatalogStore = defineStore('MOD_MST_catalog', () => {
  const pageResult = ref<CatalogResult | null>(null);
  const detail = ref<CatalogDetail | null>(null);
  function replacePage(value: CatalogResult | null) { pageResult.value = value; }
  function selectDetail(value: CatalogDetail | null) { detail.value = value; }
  function reset() { pageResult.value = null; detail.value = null; }
  return { pageResult, detail, replacePage, selectDetail, reset };
});
