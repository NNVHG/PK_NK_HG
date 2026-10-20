import { onMounted, onUnmounted, ref } from 'vue';
import { appointmentsService } from '@/services/appointments';

export function useBookingSchedule(autoLoad = true) {
  const slots = ref<string[]>([]), revision = ref(''), duration = ref(0), loading = ref(false), error = ref('');
  let generation = 0;
  async function load() {
    const current = ++generation; loading.value = true; error.value = ''; slots.value = []; revision.value = ''; duration.value = 0;
    try {
      const data = await appointmentsService.bookingSchedule();
      if (current !== generation) return;
      if (!Array.isArray(data.slots) || data.slots.length === 0 || data.slots.length > 1440 || data.slots.some(x => !/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(x)) || !Number.isInteger(data.slotDurationMinutes) || data.slotDurationMinutes < 1 || data.slotDurationMinutes > 1440 || !/^[A-F0-9]{64}$/.test(data.revision)) throw new Error('Invalid schedule');
      slots.value = data.slots; revision.value = data.revision; duration.value = data.slotDurationMinutes;
    } catch { if (current === generation) error.value = 'Không tải được khung giờ hiện hành. Tải lại trước khi đặt hoặc đổi lịch.'; }
    finally { if (current === generation) loading.value = false; }
  }
  if (autoLoad) onMounted(load);
  onUnmounted(() => { generation++; slots.value = []; revision.value = ''; duration.value = 0; });
  return { slots, revision, duration, loading, error, load };
}
