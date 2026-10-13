<template>
  <button :disabled="busy" @click="openPanel">Nhật ký nhắc lịch</button>
  <section v-if="opened" aria-labelledby="reminder-title">
    <h2 id="reminder-title">Nhật ký nhắc lịch</h2>
    <p>Nhắc hẹn được mô phỏng và lưu nội bộ; không gửi SMS thực tế. Hệ thống tự quét mỗi 30 phút khi API đang chạy, trong khoảng 23,5–24,5 giờ tới theo giờ Việt Nam.</p>
    <p v-if="worker">Nhắc tự động: {{ workerState(worker.state) }}.
      <template v-if="worker.lastCompletedAtUtc">Lần quét gần nhất: {{ sentTime(worker.lastCompletedAtUtc) }}.</template>
      <template v-if="worker.createdCount !== null"> Tạo {{ worker.createdCount }} tin; {{ worker.alreadyRemindedCount }} lịch đã được nhắc.</template>
    </p>
    <p v-if="workerError" role="alert">{{ workerError }}</p>
    <button :disabled="busy || statusLoading" @click="loadWorkerStatus">{{ statusLoading ? 'Đang tải trạng thái…' : 'Tải trạng thái nhắc tự động' }}</button>
    <form @submit.prevent="simulate">
      <label><input v-model="confirmed" type="checkbox" :disabled="busy || loading" /> Tôi xác nhận mô phỏng gửi nhắc lịch</label>
      <button :disabled="busy || loading || !confirmed">{{ busy ? 'Đang mô phỏng…' : 'Mô phỏng gửi nhắc lịch' }}</button>
    </form>
    <p v-if="writeError" role="alert">{{ writeError }}</p>
    <p v-if="scan" role="status">Có {{ scan.eligibleCount }} lịch đủ điều kiện; tạo {{ scan.createdCount }} tin mô phỏng; {{ scan.alreadyRemindedCount }} lịch đã được nhắc.
      Cửa sổ: {{ localTime(scan.windowStart) }} → {{ localTime(scan.windowEnd) }} (giờ Việt Nam).</p>
    <form @submit.prevent="load(1)">
      <label for="reminder-appointment">ID lịch hẹn (để trống xem tất cả)</label><input id="reminder-appointment" v-model="appointmentId" type="number" min="1" step="1" :disabled="busy" />
      <button :disabled="busy || loading">Tải nhật ký</button>
    </form>
    <p v-if="loading" role="status">Đang tải nhật ký…</p><p v-if="readError" role="alert">{{ readError }}</p>
    <p v-if="!loading && !readError && !rows.length">Chưa có tin nhắc phù hợp. Lịch đặt cách giờ khám dưới 24 giờ được bỏ qua.</p>
    <ul><li v-for="row in rows" :key="row.id">
      <strong>#{{ row.appointmentId }} · {{ row.appointmentDate }} · {{ row.slotTime.slice(0, 5) }}</strong>
      <p>{{ row.channel }} · {{ row.status }} · {{ sentTime(row.sentAtUtc) }}</p><p class="message">{{ row.messageContent }}</p>
    </li></ul>
    <nav aria-label="Phân trang nhật ký"><span>{{ total }} tin · Trang {{ page }}</span>
      <button :disabled="busy || loading || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="busy || loading || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
    <button :disabled="busy" @click="closePanel">Đóng nhật ký</button>
  </section>
</template>
<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import axios from 'axios';
import { appointmentRemindersService, type ReminderLog, type ReminderScan, type ReminderWorkerStatus } from '@/services/appointmentReminders';
const opened = ref(false), busy = ref(false), loading = ref(false), confirmed = ref(false);
const rows = ref<ReminderLog[]>([]), scan = ref<ReminderScan | null>(null), appointmentId = ref<string | number>('');
const readError = ref(''), writeError = ref(''), page = ref(1), total = ref(0), hasNext = ref(false);
const worker = ref<ReminderWorkerStatus | null>(null), workerError = ref(''), statusLoading = ref(false);
let life = 0, readVersion = 0, statusVersion = 0;
async function openPanel() { if (opened.value || busy.value) return; opened.value = true; await Promise.all([load(1), loadWorkerStatus()]); }
async function loadWorkerStatus() {
  if (!opened.value || busy.value) return;
  const ticket = ++statusVersion; statusLoading.value = true; workerError.value = ''; worker.value = null;
  try { const result = await appointmentRemindersService.getWorkerStatus(); if (ticket === statusVersion) worker.value = result; }
  catch { if (ticket === statusVersion) workerError.value = 'Không thể tải trạng thái nhắc tự động. Bạn vẫn có thể kiểm tra nhật ký hoặc mô phỏng thủ công.'; }
  finally { if (ticket === statusVersion) statusLoading.value = false; }
}
async function load(target = 1) {
  if (!opened.value || busy.value) return;
  const id = String(appointmentId.value).trim() ? Number(appointmentId.value) : undefined;
  if (!Number.isInteger(target) || target < 1 || target > 1000000 || (id !== undefined && (!Number.isInteger(id) || id <= 0 || id > 2147483647))) {
    readVersion++; rows.value = []; total.value = 0; hasNext.value = false; loading.value = false;
    readError.value = 'ID lịch hẹn và trang phải là số nguyên dương hợp lệ.'; return;
  }
  const ticket = ++readVersion; loading.value = true; readError.value = ''; rows.value = []; total.value = 0; hasNext.value = false;
  try {
    const result = await appointmentRemindersService.getLogs(target, id);
    if (ticket === readVersion) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === readVersion) readError.value = 'Không thể tải nhật ký. Vui lòng thử lại.'; }
  finally { if (ticket === readVersion) loading.value = false; }
}
async function simulate() {
  if (!opened.value || busy.value || loading.value || !confirmed.value) return;
  const ticket = life; busy.value = true; writeError.value = ''; scan.value = null;
  try { const result = await appointmentRemindersService.simulate(); if (ticket === life) scan.value = result; }
  catch (cause) { if (ticket === life) writeError.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Đối chiếu nhật ký trước khi thử tiếp.' : 'Chưa xác định kết quả. Đối chiếu nhật ký trước khi thử tiếp.'; }
  finally {
    if (ticket === life) { busy.value = false; confirmed.value = false; await load(1); }
  }
}
function reset() { life++; readVersion++; statusVersion++; opened.value = false; loading.value = false; statusLoading.value = false; worker.value = null; workerError.value = ''; confirmed.value = false; rows.value = []; scan.value = null; appointmentId.value = ''; total.value = 0; hasNext.value = false; readError.value = ''; writeError.value = ''; page.value = 1; }
function closePanel() { if (!busy.value) reset(); }
function localTime(value: string) { return value.replace('T', ' ').slice(0, 16); }
function workerState(value: string) { return ({ NotStarted: 'Chưa chạy từ lúc mở máy chủ', Running: 'Đang quét', Success: 'Đã quét thành công', Conflict: 'Có cập nhật đồng thời; kiểm tra nhật ký', Failed: 'Quét thất bại; kiểm tra lại ở lần kế tiếp', Stopped: 'Đã dừng' } as Record<string, string>)[value] ?? 'Chưa xác định'; }
function sentTime(value: string) { return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)); }
onUnmounted(reset);
</script>
<style scoped>
section { border: 1px solid #ddd; border-radius: 8px; padding: 1rem; margin: 1rem 0; }
button, input { font: inherit; padding: .5rem; margin: .3rem; } ul { padding: 0; list-style: none; }
li { border-top: 1px solid #eee; padding: .7rem 0; } .message { white-space: pre-wrap; overflow-wrap: anywhere; } [role=alert] { color: #b91c1c; }
</style>
