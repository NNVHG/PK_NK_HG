<template>
  <main class="page">
    <h1>Lịch hẹn tại quầy</h1>
    <AppointmentReminderPanel />
    <label for="day">Ngày hẹn</label><input id="day" v-model="date" type="date" :disabled="busy" @change="load(1)" />
    <label for="status">Trạng thái</label><select id="status" v-model="status" :disabled="busy" @change="load(1)">
      <option value="Scheduled">Đang chờ khám</option><option value="">Tất cả</option><option value="Cancelled">Đã hủy</option>
      <option value="CheckedIn">Đã tiếp đón</option><option value="Completed">Hoàn tất</option>
    </select><button :disabled="loading || busy" @click="load(1)">Tra cứu / tải lại</button>
    <p v-if="loading" role="status">Đang tải lịch hẹn…</p><p v-if="error" role="alert">{{ error }}</p><p v-if="success" role="status">{{ success }}</p>
    <p v-if="!loading && !error && !rows.length">Không có lịch hẹn phù hợp.</p>
    <ul><li v-for="group in displayGroups" :key="group.key" :class="{ 'emergency-group': group.isEmergency }">
      <div class="appointment-group">
        <article v-for="row in group.items" :key="row.appointmentId" class="appointment-card" :class="{ 'emergency-card': row.isEmergency }">
          <strong>#{{ row.appointmentId }} · {{ row.patientName }} · {{ row.patientCode }}</strong>
          <span v-if="row.isEmergency" class="emergency-badge">CẤP CỨU</span>
          <p>{{ row.appointmentDate }} · {{ row.slotTime.slice(0, 5) }} · {{ row.status }}</p>
          <p v-if="row.isEmergency && row.emergencyReason" class="emergency-reason">Lý do cấp cứu: {{ row.emergencyReason }}</p>
          <button v-if="row.status === 'Scheduled'" :disabled="busy || loading" @click="select(row)">Hủy lịch này</button>
          <AppointmentRescheduling v-if="row.status === 'Scheduled' && !selected" :appointment-id="row.appointmentId" :patient-name="row.patientName" @rescheduled="onRescheduled" />
        </article>
      </div>
    </li></ul>
    <form v-if="selected" @submit.prevent="cancel">
      <h2>Xác nhận hủy #{{ selected.appointmentId }} · {{ selected.patientName }}</h2>
      <p>{{ selected.appointmentDate }} · {{ selected.slotTime.slice(0, 5) }}</p>
      <label for="reason">Lý do (tùy chọn, tối đa {{ reasonLimit }} ký tự còn lại)</label>
      <textarea id="reason" v-model.trim="reason" :maxlength="reasonLimit" :disabled="busy || reasonLimit === 0" />
      <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Đã đối chiếu lịch và yêu cầu hủy với khách</label>
      <button :disabled="busy || !confirmed">{{ busy ? 'Đang hủy…' : 'Xác nhận hủy lịch' }}</button>
      <button type="button" :disabled="busy" @click="close">Quay lại, giữ lịch hẹn</button>
    </form>
    <nav aria-label="Phân trang lịch hẹn"><span>{{ total }} lịch · Trang {{ page }}</span>
      <button :disabled="loading || busy || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || busy || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
  </main>
</template>
<script setup lang="ts">
import AppointmentReminderPanel from '@/components/AppointmentReminderPanel.vue';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
const date = ref(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()));
const status = ref('Scheduled'), rows = ref<AppointmentResponse[]>([]), page = ref(1), total = ref(0), hasNext = ref(false);
const selected = ref<AppointmentResponse | null>(null), reason = ref(''), confirmed = ref(false);
const busy = ref(false), loading = ref(false), error = ref(''), success = ref('');
const reasonLimit = computed(() => Math.max(0, 500 - (selected.value?.notes?.length ?? 0) - (selected.value?.notes ? 1 : 0) - '[Lý do hủy: ]'.length));
const displayGroups = computed(() => {
  const grouped = new Map<string, AppointmentResponse[]>();
  for (const row of rows.value) {
    const key = row.dentistId
      ? `${row.appointmentDate}:${row.slotTime}:${row.dentistId}`
      : `appointment:${row.appointmentId}`;
    const items = grouped.get(key) ?? [];
    items.push(row);
    grouped.set(key, items);
  }
  return Array.from(grouped, ([key, items]) => ({ key, items, isEmergency: items.some(row => row.isEmergency) }));
});
let version = 0;
function close() { selected.value = null; reason.value = ''; confirmed.value = false; }
async function load(targetPage = 1) {
  if (busy.value) return;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date.value) || !Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 ||
      !['', 'Scheduled', 'Cancelled', 'CheckedIn', 'Completed'].includes(status.value)) { error.value = 'Kiểm tra ngày và trạng thái tra cứu.'; return; }
  const ticket = ++version; close(); rows.value = []; error.value = ''; success.value = ''; loading.value = true; hasNext.value = false; total.value = 0;
  try {
    const result = await appointmentsService.getForDesk(date.value, targetPage, status.value || undefined);
    if (ticket === version) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function select(row: AppointmentResponse) {
  if (busy.value || loading.value || row.status !== 'Scheduled' || !rows.value.some(item => item.appointmentId === row.appointmentId)) return;
  close(); selected.value = row; error.value = ''; success.value = '';
}
async function cancel() {
  if (busy.value || loading.value || !confirmed.value || !selected.value || selected.value.status !== 'Scheduled') return;
  if (reason.value.trim().length > reasonLimit.value) { error.value = 'Lý do hủy vượt số ký tự còn lại.'; return; }
  const ticket = version, id = selected.value.appointmentId; busy.value = true; error.value = ''; success.value = '';
  try {
    await appointmentsService.cancel(id, reason.value.trim() || null);
    if (ticket !== version) return;
    busy.value = false;
    await load(page.value);
    if (version === ticket + 1) success.value = `Đã hủy lịch #${id}.`;
  } catch (cause) {
    if (ticket === version) { confirmed.value = false; error.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Tải lại lịch để đối chiếu trước khi thử tiếp.' : 'Chưa xác định kết quả. Tải lại lịch để đối chiếu trước khi thử tiếp.'; }
  } finally { if (ticket === version) busy.value = false; }
}
async function onRescheduled() {
  const expected = version + 1;
  await load(page.value);
  if (version === expected) success.value = 'Đã đổi lịch. Tra cứu ngày mới để xem lịch cập nhật.';
}
onMounted(() => load()); onUnmounted(() => { version++; close(); rows.value = []; });
</script>
<style scoped>
.page { max-width: 850px; margin: auto; padding: 1.5rem; } button, input, select, textarea { font: inherit; padding: .5rem; margin: .4rem; }
ul { list-style: none; padding: 0; } ul > li { padding: 0; margin: .7rem 0; }
.appointment-group { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 280px), 1fr)); gap: .75rem; }
.appointment-card, form { border: 1px solid #ddd; border-radius: 8px; padding: 1rem; margin: 0; }
form { display: grid; } [role=alert] { color: #b91c1c; } [role=status] { color: #13795b; }
.emergency-group .appointment-card { border-color: #b91c1c; }
.emergency-card { border-left: 5px solid #b91c1c; background: #fff8f8; }
.emergency-badge { display: inline-block; margin-left: .5rem; padding: .2rem .55rem; border-radius: 999px; color: #fff; background: #b91c1c; font-size: .8rem; font-weight: 800; }
.emergency-reason { color: #7f1d1d; }
@media (prefers-reduced-motion: no-preference) { .emergency-badge { animation: emergency-pulse 1.2s ease-in-out infinite alternate; } }
@keyframes emergency-pulse { from { box-shadow: 0 0 0 0 #b91c1c33; } to { box-shadow: 0 0 0 5px #b91c1c55; } }
</style>
