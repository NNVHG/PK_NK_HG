import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type { RefundRequest } from '@/services/invoiceRefunds';
export const useInvoiceRefundStore = defineStore('MOD_BIL_refund', () => {
 const pending = ref<Record<string, RefundRequest>>({});
 const auth = useAuthStore();
 // Keep uncertain requests across route remounts in memory only; reasons never enter browser storage.
 watch(()=>`${auth.user?.userId}/${auth.role}`,()=>{pending.value={};});
 function put(key: string,value: RefundRequest) { pending.value[key]=JSON.parse(JSON.stringify(value)) as RefundRequest; }
 function clear(key: string) { delete pending.value[key]; }
 return { pending, put, clear };
});
