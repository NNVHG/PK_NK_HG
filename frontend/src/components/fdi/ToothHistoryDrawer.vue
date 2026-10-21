<template>
  <dialog ref="dialog" class="history-drawer" aria-labelledby="history-title" @cancel="close">
    <header><h2 id="history-title">{{ allJaw ? 'Lịch sử toàn bộ cung hàm' : `Lịch sử răng #${tooth}` }}</h2>
      <button type="button" @click="close">Đóng</button></header>
    <label><input v-model="allJaw" type="checkbox" @change="load(1)" /> {{ allJaw ? 'Toàn bộ cung hàm' : 'Chỉ răng này · Bật để xem toàn bộ cung hàm' }}</label>
    <p>Lịch sử ghi nhận tình trạng và chỉ định dịch vụ. Chỉ định không xác nhận thủ thuật đã thực hiện.</p>
    <slot name="actions" :tooth-number="tooth" :all-jaw="allJaw" />
    <button type="button" :disabled="loading" @click="load(page)">Tải lại</button>
    <p v-if="loading" role="status">Đang tải lịch sử…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="!loading && !error && !items.length">Chưa có lịch sử trong phạm vi đã chọn.</p>
    <ol v-if="!loading && !error" class="timeline">
      <li v-for="item in items" :key="`${item.eventType}-${item.id}`">
        <strong>{{ item.toothNumber === null ? 'Toàn hàm / tổng quát' : `Răng ${item.toothNumber}` }} · {{ formatSurfaceCodes(item.surface) }}</strong>
        <p>{{ new Date(item.createdAt).toLocaleString('vi-VN') }} · Lần khám #{{ item.visitId }}</p>
        <p>Ngày khám: {{ new Date(item.visitDate).toLocaleString('vi-VN') }} · Nha sĩ: {{ item.dentistName || 'Chưa được phân công' }}</p>
        <p v-if="item.conditionCode">Tình trạng: {{ conditionLabel(item.conditionCode) }}</p>
        <p v-if="item.note" class="note">Ghi chú: {{ item.note }}</p>
        <p v-for="service in item.services" :key="service.assignmentId">Chỉ định: {{ service.code }} · {{ service.name }} · {{ service.quantity }} × {{ money(service.unitPrice) }}</p>
      </li>
    </ol>
    <footer v-if="!error"><button type="button" :disabled="loading || page <= 1" @click="load(page - 1)">Trang trước</button>
      <span>{{ page }}/{{ Math.max(totalPages, 1) }}</span>
      <button type="button" :disabled="loading || page >= totalPages" @click="load(page + 1)">Trang sau</button></footer>
  </dialog>
</template>
<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';
import axios from 'axios';
import { toothHistoryApi, type ToothHistoryItem } from '@/services/toothHistory';
import { FDI_CONDITIONS } from '@/services/fdi';
import { formatSurfaceCodes } from '@/types/fdi';
const props = defineProps<{ patientId: number }>();
const dialog = ref<HTMLDialogElement | null>(null);
const tooth = ref<number | null>(null), allJaw = ref(false), visible = ref(false);
const items = ref<ToothHistoryItem[]>([]), loading = ref(false), error = ref('');
const page = ref(1), totalPages = ref(0);
let version = 0;
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const conditionLabel = (code: string) => FDI_CONDITIONS[code as keyof typeof FDI_CONDITIONS] || code;
function validTooth(n: number) { return Number.isInteger(n) && (n / 10 >= 1 && n / 10 < 5 && n % 10 >= 1 && n % 10 <= 8 || n / 10 >= 5 && n / 10 < 9 && n % 10 >= 1 && n % 10 <= 5); }
async function open(number: number) {
  if (!Number.isInteger(props.patientId) || props.patientId <= 0 || !validTooth(number)) return;
  tooth.value = number; allJaw.value = false; visible.value = true;
  if (!dialog.value?.open) dialog.value?.showModal();
  await load(1);
}
async function load(nextPage: number) {
  if (!visible.value || tooth.value === null || !Number.isInteger(nextPage) || nextPage < 1 || nextPage > 1000000) return;
  const ticket = ++version; loading.value = true; error.value = ''; items.value = []; page.value = nextPage; totalPages.value = 0;
  try {
    const result = await toothHistoryApi.get(props.patientId, allJaw.value ? null : tooth.value, nextPage);
    if (ticket !== version || !visible.value) return;
    items.value = result.items; page.value = result.page; totalPages.value = result.totalPages;
  } catch (cause) {
    if (ticket === version && visible.value) error.value = axios.isAxiosError(cause)
      ? cause.response?.data?.message || 'Không tải được lịch sử. Hãy tải lại.' : 'Không tải được lịch sử. Hãy tải lại.';
  } finally { if (ticket === version) loading.value = false; }
}
function close() { version++; visible.value = false; items.value = []; error.value = ''; loading.value = false; tooth.value = null; page.value = 1; totalPages.value = 0; dialog.value?.close(); }
watch(() => props.patientId, close);
onUnmounted(close);
defineExpose({ open, close });
</script>
<style scoped>
.history-drawer { box-sizing: border-box; position: fixed; inset: 0 0 0 auto; margin: 0; width: min(600px, 95vw); height: 100dvh; max-height: 100dvh; border: 0; padding: 1.25rem; overflow-y: auto; }
.history-drawer::backdrop { background: #0006; }
header, footer { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
button { padding: .6rem; } .timeline { padding-left: 1.5rem; } li { padding: 1rem 0; border-bottom: 1px solid #ddd; }
.note { white-space: pre-wrap; overflow-wrap: anywhere; } .error { color: #b91c1c; }
</style>
