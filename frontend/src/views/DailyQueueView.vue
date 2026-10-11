<template>
  <main class="page">
    <h1>Hàng đợi hôm nay</h1>
    <p>Khách hẹn đúng giờ được xếp theo giờ hẹn; khách thường hoặc trễ hẹn theo thời điểm check-in.</p>
    <label for="status">Trạng thái</label>
    <select id="status" v-model.number="status" @change="load(1)">
      <option :value="0">Tất cả</option><option :value="1">Đang chờ</option><option :value="2">Đang khám</option>
      <option :value="5">Đang chụp ảnh</option><option :value="3">Khám xong</option><option :value="4">Đã hủy</option>
    </select>
    <button :disabled="loading" @click="load(page)">Tải lại</button>
    <p v-if="loading" role="status">Đang tải hàng đợi…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="!loading && !error && !rows.length">Không có lượt tiếp đón phù hợp hôm nay.</p>
    <p v-if="!loading && !error">{{ total }} lượt · Trang {{ page }} · Cập nhật {{ updatedAt }}</p>
    <ol class="entries"><li v-for="row in rows" :key="row.queueEntryId" :class="{ priority: row.isPriority }">
      <strong>{{ numberLabel(row.queueNumber) }} · {{ row.patientName }}</strong>
      <p>{{ row.patientCode }} · {{ row.statusText }} · {{ row.queueDate }}</p>
      <p>{{ row.isPriority ? 'Ưu tiên lịch hẹn đúng giờ' : 'Chờ thông thường' }} · {{ row.dentistName || 'Chưa phân nha sĩ' }}</p>
    </li></ol>
    <nav aria-label="Phân trang hàng đợi">
      <button :disabled="loading || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { queueService, type QueueEntryResponse } from '@/services/queue';
const rows = ref<QueueEntryResponse[]>([]), status = ref(1), page = ref(1), total = ref(0), hasNext = ref(false);
const loading = ref(false), error = ref(''), updatedAt = ref('');
let version = 0;
async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1_000_000 || ![0, 1, 2, 3, 4, 5].includes(status.value)) return;
  const ticket = ++version; loading.value = true; rows.value = []; error.value = ''; updatedAt.value = ''; total.value = 0; hasNext.value = false;
  try {
    const result = await queueService.getToday(targetPage, status.value === 0 ? undefined : status.value);
    if (ticket !== version) return;
    rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage;
    updatedAt.value = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date());
  } catch { if (ticket === version) error.value = 'Không thể tải hàng đợi. Vui lòng tải lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function numberLabel(value: number) { return `A-${String(value).padStart(3, '0')}`; }
onMounted(() => load());
onUnmounted(() => { version++; rows.value = []; updatedAt.value = ''; total.value = 0; });
</script>
<style scoped>
.page { max-width: 900px; margin: auto; padding: 1.5rem; }
button, select { font: inherit; margin: .5rem; padding: .5rem; }
.entries { list-style: none; padding: 0; } li { padding: 1rem; margin: .6rem 0; border: 1px solid #ddd; border-radius: 8px; }
.priority { border-left: 5px solid #13795b; } [role=alert] { color: #b91c1c; }
</style>
