import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
import { isAxiosError } from 'axios';
import { useAuthStore } from '@/stores/auth';
import { schedulingSettingsService, type SchedulingSettings, type SchedulingSettingsWrite } from '@/services/schedulingSettings';
export const useSchedulingSettingsStore = defineStore('MOD_MST_SCHEDULING', () => {
  const auth = useAuthStore(), current = ref<SchedulingSettings | null>(null), busy = ref(false), error = ref(''), message = ref(''), needsReload = ref(false);
  let generation = 0;
  watch(() => `${auth.user?.userId ?? ''}/${auth.role}`, () => { generation++; current.value = null; busy.value = false; error.value = ''; message.value = ''; needsReload.value = false; });
  async function load() {
    if (busy.value) return;
    const ticket = ++generation; busy.value = true; error.value = ''; message.value = '';
    try { const data = await schedulingSettingsService.read(); if (ticket === generation) { current.value = data; needsReload.value = false; } }
    catch { if (ticket === generation) { needsReload.value = true; error.value = 'Không tải được cấu hình. Vui lòng thử lại.'; } }
    finally { if (ticket === generation) busy.value = false; }
  }
  async function save(payload: SchedulingSettingsWrite) {
    if (busy.value || needsReload.value || !current.value) return;
    const ticket = ++generation; busy.value = true; error.value = ''; message.value = '';
    try { const data = await schedulingSettingsService.save(payload); if (ticket === generation) { current.value = data; message.value = 'Đã lưu giờ và thời lượng slot. Lịch đã hẹn được giữ nguyên.'; } }
    catch (cause) {
      if (ticket !== generation) return;
      const response = isAxiosError(cause) ? cause.response : undefined;
      needsReload.value = response?.status !== 400;
      error.value = response?.status === 409 ? 'Cấu hình đã đổi. Tải lại và đối chiếu trước khi lưu tiếp.' : response?.status === 400 ? response.data?.message ?? 'Giờ hoặc thời lượng chưa hợp lệ.' : 'Chưa xác định kết quả lưu. Tải lại cấu hình để đối chiếu trước khi sửa tiếp.';
    } finally { if (ticket === generation) busy.value = false; }
  }
  return { current, busy, error, message, needsReload, load, save };
});
