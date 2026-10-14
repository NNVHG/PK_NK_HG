<template>
  <section aria-label="Lịch sử hóa đơn tại quầy">
    <h2>Danh sách hóa đơn</h2>
    <form @submit.prevent="search">
      <label>Mã bệnh nhân (để trống xem tất cả)
        <input v-model="patientCode" maxlength="12" autocomplete="off" :disabled="busy || disabled" placeholder="BN000001" />
      </label>
      <label>Trạng thái
        <select v-model="status" :disabled="busy || disabled">
          <option value="">Tất cả</option><option v-for="(name, index) in statuses" :key="index" :value="String(index)">{{ name }}</option>
        </select>
      </label>
      <button :disabled="busy || disabled || !allowed">Tìm hóa đơn</button>
    </form>
    <p v-if="busy" role="status">Đang tải danh sách…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="loaded && !rows.length">Không có hóa đơn trong trang và bộ lọc này.</p>
    <ul>
      <li v-for="row in rows" :key="row.invoiceId">
        <strong>{{ row.invoiceCode }}</strong> · {{ row.patientCode }} · {{ row.patientName }}
        <p>{{ new Date(row.createdAt).toLocaleString('vi-VN') }} · {{ statuses[row.status] ?? 'Không xác định' }}</p>
        <p>Tổng: {{ money(row.totalAmount) }} · Đã trả: {{ money(row.paidAmount) }} · Nợ: {{ money(row.remainingAmount) }}</p>
        <button :disabled="busy || disabled || !allowed" @click="selectInvoice(row)">Đối chiếu / xem các lần thu</button>
      </li>
    </ul>
    <nav v-if="loaded" aria-label="Phân trang hóa đơn">
      <p>Trang {{ page }} / {{ totalPages }} · {{ totalCount }} hóa đơn theo bộ lọc đã áp dụng</p>
      <button :disabled="busy || disabled || page <= 1" @click="loadPage(page - 1)">Trang trước</button>
      <button :disabled="busy || disabled || page >= totalPages" @click="loadPage(page + 1)">Trang sau</button>
    </nav>
  </section>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { invoiceHistoryApi, type InvoiceHistoryQuery } from '@/services/invoiceHistory';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
const props = defineProps<{ disabled?: boolean }>();
const emit = defineEmits<{ selected: [code: string] }>();
const auth = useAuthStore();
const allowed = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role));
const patientCode = ref('');
const status = ref('');
const rows = ref<InvoiceLookupResponse[]>([]);
const page = ref(1);
const totalPages = ref(0);
const totalCount = ref(0);
const busy = ref(false);
const loaded = ref(false);
const error = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let applied: Omit<InvoiceHistoryQuery, 'page'> = { pageSize: 20 };
let version = 0;
function validCode(value: string) {
  if (!value) return true;
  if (!/^BN[0-9]{6,10}$/.test(value)) return false;
  const number = Number(value.slice(2));
  return number > 0 && number <= 2147483647 && value === 'BN' + String(number).padStart(6, '0');
}
async function search() {
  if (!allowed.value || props.disabled || busy.value) return;
  rows.value = []; loaded.value = false; totalCount.value = 0; totalPages.value = 0; error.value = '';
  const code = patientCode.value.trim().toUpperCase();
  if (!validCode(code) || !['', '0', '1', '2', '3', '4'].includes(status.value)) {
    error.value = 'Nhập đúng mã bệnh nhân đầy đủ và trạng thái hợp lệ.'; return;
  }
  patientCode.value = code;
  applied = { pageSize: 20, ...(code ? { patientCode: code } : {}), ...(status.value !== '' ? { status: Number(status.value) } : {}) };
  await loadPage(1);
}
async function loadPage(target: number) {
  if (!allowed.value || props.disabled || busy.value || !Number.isInteger(target) || target < 1 || target > 1000000) return;
  if (target !== 1 && (!loaded.value || target > totalPages.value)) return;
  const ticket = ++version; busy.value = true; error.value = ''; rows.value = []; loaded.value = false;
  try {
    const result = await invoiceHistoryApi.get({ ...applied, page: target });
    if (ticket !== version || !allowed.value) return;
    rows.value = result.items; page.value = result.page; totalPages.value = result.totalPages; totalCount.value = result.totalCount; loaded.value = true;
  } catch { if (ticket === version) { totalPages.value = 0; totalCount.value = 0; error.value = 'Không tải được danh sách. Tìm lại để đối chiếu.'; } }
  finally { if (ticket === version) busy.value = false; }
}
function selectInvoice(row: InvoiceLookupResponse) {
  if (!allowed.value || props.disabled || busy.value || !loaded.value || !rows.value.some(x => x.invoiceId === row.invoiceId && x.invoiceCode === row.invoiceCode)) return;
  emit('selected', row.invoiceCode);
}
onUnmounted(() => { version++; rows.value = []; patientCode.value = ''; status.value = ''; totalCount.value = 0; totalPages.value = 0; loaded.value = false; applied = { pageSize: 20 }; });
</script>
<style scoped>
section { border: 1px solid #ccc; padding: 1rem; margin: 1rem 0; }
label { display: block; margin: .6rem 0; }
input, select, button { padding: .5rem; }
ul { list-style: none; padding: 0; }
li { border-bottom: 1px solid #ddd; padding: .8rem 0; }
</style>
