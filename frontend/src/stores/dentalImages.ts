import { defineStore } from 'pinia';
import { ref } from 'vue';
import { dentalImagesService, type DentalImage } from '@/services/dentalImages';
export const useDentalImagesStore = defineStore('MOD_IMG', () => {
  const items = ref<DentalImage[]>([]), hasMore = ref(false), loading = ref(false);
  let generation = 0;
  function clear() { generation++; items.value = []; hasMore.value = false; loading.value = false; }
  async function load(patientId: number, more = false) {
    if (loading.value && more) return;
    const current = ++generation; loading.value = true;
    try {
      const page = await dentalImagesService.list(patientId, more ? items.value.at(-1)?.id ?? 0 : 0);
      if (current !== generation) return;
      items.value = more ? [...items.value, ...page.items.filter(x => !items.value.some(old => old.id === x.id))] : page.items;
      hasMore.value = page.hasMore;
    } finally { if (current === generation) loading.value = false; }
  }
  return { items, hasMore, loading, clear, load };
});
