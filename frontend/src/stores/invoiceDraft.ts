import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { InvoiceDraft } from '@/services/invoiceDraft';

export const useInvoiceDraftStore = defineStore('MOD_BIL_draft', () => {
  const invoice = ref<InvoiceDraft | null>(null);
  function replace(value: InvoiceDraft | null) { invoice.value = value; }
  return { invoice, replace };
});
