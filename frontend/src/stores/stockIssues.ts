import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { isAxiosError } from 'axios';
import { useAuthStore } from '@/stores/auth';
import { stockIssuesService, type IssueRequest, type StockIssue } from '@/services/stockIssues';
export const useStockIssuesStore = defineStore('MOD_INV_ISSUES', () => {
  const auth = useAuthStore();
  const pending = ref<IssueRequest | null>(null), busy = ref(false), error = ref(''), saved = ref<StockIssue | null>(null);
  let generation = 0;
  watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { generation++; pending.value = null; busy.value = false; error.value = ''; saved.value = null; });
  async function submit(payload?: IssueRequest) {
    if (busy.value) return;
    if (!pending.value && payload) pending.value = JSON.parse(JSON.stringify(payload)) as IssueRequest;
    if (!pending.value) return;
    const current = generation; busy.value = true; error.value = ''; saved.value = null;
    try {
      const result = await stockIssuesService.confirm(pending.value);
      if (current !== generation) return;
      saved.value = result.issue; pending.value = null;
    } catch (e) {
      if (current !== generation) return;
      const response = isAxiosError(e) ? e.response : undefined;
      if (response?.status === 400 || response?.status === 403 || ['INV_STALE', 'INV_VISIT', 'INV_INSUFFICIENT'].includes(response?.data?.code)) {
        pending.value = null; error.value = response?.data?.message ?? 'Phiếu bị từ chối. Tải lại ca và tồn kho để kiểm tra.';
      } else error.value = 'Chưa xác định kết quả. Gửi lại phiếu bằng đúng mã cũ; kiểm tra lịch sử trước khi lập phiếu khác.';
    } finally { if (current === generation) busy.value = false; }
  }
  return { pending, busy, error, saved, submit };
});
