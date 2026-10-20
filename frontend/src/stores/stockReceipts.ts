import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { isAxiosError } from 'axios';
import { useAuthStore } from '@/stores/auth';
import { stockReceiptsService, type ReceiptRequest, type StockReceipt } from '@/services/stockReceipts';

export const useStockReceiptsStore = defineStore('MOD_INV_RECEIPTS', () => {
  const auth = useAuthStore();
  const pending = ref<ReceiptRequest | null>(null), busy = ref(false), error = ref(''), saved = ref<StockReceipt | null>(null);
  let generation = 0;
  watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { generation++; pending.value = null; busy.value = false; error.value = ''; saved.value = null; });
  async function submit(payload?: ReceiptRequest) {
    if (busy.value) return;
    if (!pending.value && payload) pending.value = JSON.parse(JSON.stringify(payload)) as ReceiptRequest;
    if (!pending.value) return;
    const current = generation;
    busy.value = true; error.value = ''; saved.value = null;
    try {
      const result = await stockReceiptsService.confirm(pending.value);
      if (current !== generation) return;
      saved.value = result.receipt; pending.value = null;
    } catch (e) {
      if (current !== generation) return;
      const response = isAxiosError(e) ? e.response : undefined;
      const code = response?.data?.code;
      if (response?.status === 400 || response?.status === 403 || code === 'INV_STALE') {
        pending.value = null;
        error.value = code === 'INV_STALE' ? 'Vật tư đã thay đổi. Tải lại danh mục và chọn lại các dòng trước khi gửi.' : response?.data?.message ?? 'Phiếu bị từ chối. Kiểm tra lại thông tin.';
      } else error.value = 'Chưa xác định kết quả gửi. Bấm Gửi lại phiếu để dùng đúng mã cũ; kiểm tra lịch sử trước khi lập phiếu khác.';
    } finally { if (current === generation) busy.value = false; }
  }
  return { pending, busy, error, saved, submit };
});
