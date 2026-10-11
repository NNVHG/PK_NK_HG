<template>
  <main class="invoice-page">
    <RouterLink to="/">← Trang chủ</RouterLink>
    <h1>Hóa đơn nháp · Lần khám #{{ visitId }}</h1>
    <button :disabled="busy" @click="load">Tải lại hồ sơ</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <template v-if="visit">
      <p>Trạng thái khám: {{ visit.status }}</p>
      <template v-if="canComplete">
        <RouterLink :to="`/clinical/visits/${visitId}/services`">Kiểm tra dịch vụ đã chỉ định</RouterLink>
        <p>Kết thúc khám sẽ khóa hồ sơ và chốt hóa đơn nháp từ các dịch vụ đã ghi nhận.</p>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy" /> Tôi đã kiểm tra chỉ định và xác nhận hoàn tất khám</label>
        <button :disabled="busy || !confirmed" @click="complete">Kết thúc khám và tạo hóa đơn nháp</button>
      </template>
      <p v-else-if="!invoice">Lần khám chưa có hóa đơn nháp. Nha sĩ phụ trách cần hoàn tất khám.</p>
    </template>
    <section v-if="invoice" aria-label="Chi tiết hóa đơn nháp">
      <h2>{{ invoice.invoiceCode }}</h2>
      <p>Trạng thái: {{ statuses[invoice.status] || invoice.status }}</p>
      <p>Ngày tạo: {{ new Date(invoice.createdAt).toLocaleString('vi-VN') }}</p>
      <p v-if="!invoice.items.length">Không có dịch vụ tính phí được ghi nhận trong lần khám.</p>
      <ul>
        <li v-for="(item, index) in invoice.items" :key="index">
          <strong>{{ item.code }} · {{ item.name }}</strong>
          <p>{{ item.toothNumber ? `Răng ${item.toothNumber} · ${item.surface || 'Toàn răng'}` : 'Toàn hàm / tổng quát' }}</p>
          <p>{{ item.quantity }} × {{ money(item.unitPrice) }} = {{ money(item.totalAmount) }}</p>
        </li>
      </ul>
      <strong>Tổng tiền: {{ money(invoice.totalAmount) }}</strong>
      <p>Đã thu: {{ money(invoice.paidAmount) }} · Còn lại: {{ money(invoice.remainingAmount) }}</p>
      <p>Dòng dịch vụ giữ nguyên giá đã ghi nhận khi chỉ định.</p>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { storeToRefs } from 'pinia';
import axios from 'axios';
import { visitsService } from '@/services/visits';
import { invoiceDraftApi } from '@/services/invoiceDraft';
import { useInvoiceDraftStore } from '@/stores/invoiceDraft';
import { useAuthStore } from '@/stores/auth';
import type { VisitDetails } from '@/services/patients';

const route = useRoute();
const auth = useAuthStore();
const store = useInvoiceDraftStore();
const { invoice } = storeToRefs(store);
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const busy = ref(false);
const error = ref('');
const success = ref('');
const confirmed = ref(false);
const statuses: Record<number, string> = { 0: 'Nháp', 1: 'Đã phát hành', 2: 'Thanh toán một phần', 3: 'Đã thanh toán', 4: 'Đã hủy' };
const canComplete = computed(() => visit.value?.status === 'InProgress' && !visit.value.isLocked &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value.dentistId === auth.user?.userId));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
function message(cause: unknown) {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể tải hoặc tạo hóa đơn nháp.' : 'Không thể xử lý yêu cầu.';
}
async function load() {
  const ticket = ++version;
  busy.value = true; error.value = ''; visit.value = null; store.replace(null); confirmed.value = false;
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) { error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return; }
  try {
    const details = await visitsService.getById(visitId.value);
    if (ticket !== version) return;
    visit.value = details;
    try {
      const data = await invoiceDraftApi.get(visitId.value);
      if (ticket === version) store.replace(data);
    } catch (cause) {
      if (!axios.isAxiosError(cause) || cause.response?.status !== 404) throw cause;
    }
  } catch (cause) { if (ticket === version) { error.value = message(cause); visit.value = null; } }
  finally { if (ticket === version) busy.value = false; }
}
async function complete() {
  if (!confirmed.value || !canComplete.value || busy.value) return;
  const ticket = version; busy.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.complete(visitId.value);
    if (ticket !== version) return;
    await load();
    success.value = 'Đã kết thúc khám và tạo hóa đơn nháp. Hồ sơ đã được khóa.';
  } catch (cause) {
    if (ticket !== version) return;
    const failureMessage = message(cause);
    await load(); error.value = failureMessage;
  } finally { if (ticket === version) busy.value = false; }
}
watch(visitId, load, { immediate: true });
onUnmounted(() => { version++; store.replace(null); });
</script>

<style scoped>
.invoice-page { max-width: 900px; margin: 2rem auto; padding: 1rem; }
button { margin: .6rem; padding: .6rem; }
label { display: block; margin: 1rem 0; }
li { margin: 1rem 0; }
.error { color: #b91c1c; }
</style>
