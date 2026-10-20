import { defineStore } from 'pinia';
import { ref } from 'vue';
import { dentalImagesService, validImageTooth, type DentalImage } from '@/services/dentalImages';
export const useDentalImagesStore = defineStore('MOD_IMG', () => {
  const items = ref<DentalImage[]>([]), hasMore = ref(false), loading = ref(false);
  let generation = 0, scope = '';
  function clear() { generation++; scope = ''; items.value = []; hasMore.value = false; loading.value = false; }
  async function load(patientId: number, more = false, toothNumber?: number | null) {
    if (!Number.isSafeInteger(patientId) || patientId <= 0 || toothNumber != null && !validImageTooth(toothNumber)) { clear(); throw new Error('Invalid image filter'); }
    if (loading.value && more) return;
    const requestedScope = `${patientId}/${toothNumber ?? ''}`, append = more && scope === requestedScope;
    if (!append) { items.value = []; hasMore.value = false; }
    scope = requestedScope;
    const current = ++generation; loading.value = true;
    try {
      const page = await dentalImagesService.list(patientId, append ? items.value.at(-1)?.id ?? 0 : 0, toothNumber);
      if (current !== generation) return;
      if (toothNumber != null && (!Array.isArray(page.items) || page.items.length > 20 || typeof page.hasMore !== 'boolean' || page.items.some(i => i.patientId !== patientId || i.toothNumber !== toothNumber))) { items.value = []; hasMore.value = false; throw new Error('Unexpected image scope'); }
      items.value = append ? [...items.value, ...page.items.filter(x => !items.value.some(old => old.id === x.id))] : page.items;
      hasMore.value = page.hasMore;
    } finally { if (current === generation) loading.value = false; }
  }
  return { items, hasMore, loading, clear, load };
});
