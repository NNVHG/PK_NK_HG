<template>
  <main class="page">
    <h1>Lịch hẹn của tôi</h1>
    <p v-if="notice" role="status">{{ notice }}</p>
    <label for="status">Trạng thái</label><select id="status" v-model="status" @change="load(1)">
      <option value="">Tất cả</option><option value="Scheduled">Đã đặt lịch</option><option value="CheckedIn">Đã tiếp đón</option>
      <option value="Cancelled">Đã hủy</option><option value="Completed">Hoàn tất</option>
    </select>
    <button :disabled="loading" @click="refresh">Tải lại</button>
    <p v-if="loading" role="status">Đang tải lịch hẹn…</p><p v-if="error" role="alert">{{ error }}</p>
    <p v-if="!loading && !error && !rows.length">Không có lịch hẹn phù hợp.</p>
    <ul><li v-for="row in rows" :key="row.appointmentId">
      <strong>#{{ row.appointmentId }} · {{ row.patientName }} · {{ row.patientCode }}</strong>
      <p>{{ row.appointmentDate }} · {{ row.slotTime.slice(0, 5) }} · {{ statusLabel(row.status) }}</p>
      <AppointmentRescheduling v-if="row.status === 'Scheduled'" :appointment-id="row.appointmentId" :patient-name="row.patientName" self-service
        @rescheduled="rescheduled" />
      <PatientAppointmentCancellation v-if="row.status === 'Scheduled'" :appointment-id="row.appointmentId" :patient-name="row.patientName"
        :date="row.appointmentDate" :time="row.slotTime" @cancelled="load(page)" />
    </li></ul>
    <nav aria-label="Phân trang lịch hẹn"><span>{{ total }} lịch · Trang {{ page }}</span>
      <button :disabled="loading || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
    <p>Liên hệ Lễ tân phòng khám nếu cần hỗ trợ thay đổi lịch.</p>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import PatientAppointmentCancellation from '@/components/PatientAppointmentCancellation.vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
const status = ref('');
const notice = ref('');
const rows = ref<AppointmentResponse[]>([]), page = ref(1), total = ref(0), hasNext = ref(false), loading = ref(false), error = ref('');
let version = 0;
async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 ||
      !['', 'Scheduled', 'CheckedIn', 'Cancelled', 'Completed'].includes(status.value)) return;
  const ticket = ++version; rows.value = []; error.value = ''; loading.value = true; total.value = 0; hasNext.value = false;
  try {
    const result = await appointmentsService.getMine(targetPage, status.value || undefined);
    if (ticket === version) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function statusLabel(value: string) { return ({ Scheduled: 'Đã đặt lịch', CheckedIn: 'Đã tiếp đón', Cancelled: 'Đã hủy', Completed: 'Hoàn tất' } as Record<string, string>)[value] ?? value; }
async function refresh() { await load(1); }
async function rescheduled() { notice.value = 'Đã đổi lịch. Kiểm tra ngày, giờ mới trong danh sách.'; await load(page.value); }
onMounted(refresh);
onUnmounted(() => { version++; rows.value = []; total.value = 0; notice.value = ''; });
</script>
<style scoped>
.page { max-width: 850px; margin: auto; padding: 1.5rem; } button, select { font: inherit; padding: .5rem; margin: .5rem; }
ul { list-style: none; padding: 0; } li { border: 1px solid #ddd; border-radius: 8px; padding: 1rem; margin: .7rem 0; }
[role=alert] { color: #b91c1c; }
</style>
