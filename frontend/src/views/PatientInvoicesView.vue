<template>
  <main class="page">
    <h1>Hóa đơn gia đình</h1>
    <button :disabled="profilesBusy" @click="loadProfiles">Tải lại hồ sơ</button>
    <p v-if="profileError" role="alert">{{ profileError }}</p>
    <label>Hồ sơ
      <select v-model.number="patientId" :disabled="profilesBusy" @change="changeProfile">
        <option :value="0">Chọn hồ sơ</option>
        <option v-for="profile in profiles" :key="profile.patientId" :value="profile.patientId">{{ profile.fullName }} · {{ profile.patientCode }}</option>
      </select>
    </label>
    <p v-if="!profilesBusy && !profileError && !profiles.length">Chưa có hồ sơ hoạt động thuộc tài khoản.</p>
    <button :disabled="invoicesBusy || !validProfile" @click="loadInvoices(1)">Tải lại hóa đơn</button>
    <p v-if="invoicesBusy" role="status">Đang tải hóa đơn…</p>
    <p v-if="invoiceError" role="alert">{{ invoiceError }}</p>
    <p v-if="invoicesLoaded && !invoices.length">Hồ sơ này chưa có hóa đơn.</p>
    <ul><li v-for="row in invoices" :key="row.invoiceId">
      <strong>{{ row.invoiceCode }}</strong> · {{ row.patientName }} · {{ row.patientCode }}
      <p>{{ date(row.createdAt) }} · {{ statuses[row.status] ?? 'Không xác định' }}</p>
      <p>Tổng tiền {{ money(row.totalAmount) }} · Đã trả {{ money(row.paidAmount) }} · Còn nợ {{ money(row.remainingAmount) }}</p>
      <button :disabled="invoicesBusy" @click="selectInvoice(row.invoiceId)">Xem chi tiết và thanh toán</button>
    </li></ul>
    <nav v-if="invoicesLoaded" aria-label="Phân trang hóa đơn">
      <span>{{ invoiceTotal }} hóa đơn · Trang {{ invoicePage }}</span>
      <button :disabled="invoicesBusy || invoicePage <= 1" @click="loadInvoices(invoicePage - 1)">Trước</button>
      <button :disabled="invoicesBusy || invoicePage >= invoicePages" @click="loadInvoices(invoicePage + 1)">Sau</button>
    </nav>
    <section v-if="selected" aria-label="Chi tiết hóa đơn">
      <h2>{{ selected.invoiceCode }} · Chi tiết hóa đơn</h2>
      <p>Tên dịch vụ và giá được ghi tại thời điểm lập hóa đơn.</p>
      <button :disabled="itemsBusy" @click="loadItems(1)">Tải lại chi tiết</button>
      <p v-if="itemsBusy" role="status">Đang tải chi tiết…</p>
      <p v-if="itemError" role="alert">{{ itemError }}</p>
      <p v-if="itemsLoaded && !items.length">Hóa đơn này chưa có dòng dịch vụ.</p>
      <ul><li v-for="item in items" :key="item.itemId">
        <strong>{{ item.name }}</strong> · {{ item.code }}
        <p>Số lượng {{ item.quantity }} · Đơn giá {{ money(item.unitPrice) }} · Thành tiền {{ money(item.totalAmount) }}</p>
      </li></ul>
      <nav v-if="itemsLoaded" aria-label="Phân trang chi tiết hóa đơn">
        <span>{{ itemTotal }} dòng · Trang {{ itemPage }}</span>
        <button :disabled="itemsBusy || itemPage <= 1" @click="loadItems(itemPage - 1)">Trước</button>
        <button :disabled="itemsBusy || itemPage >= itemPages" @click="loadItems(itemPage + 1)">Sau</button>
      </nav>
    </section>
    <section v-if="selected" aria-label="Các lần thanh toán">
      <h2>{{ selected.invoiceCode }} · Các lần thanh toán</h2>
      <button :disabled="paymentsBusy" @click="loadPayments(1)">Tải lại thanh toán</button>
      <p v-if="paymentsBusy" role="status">Đang tải thanh toán…</p>
      <p v-if="paymentError" role="alert">{{ paymentError }}</p>
      <p v-if="paymentsLoaded && !payments.length">Chưa có khoản thanh toán được ghi nhận.</p>
      <ul><li v-for="payment in payments" :key="payment.paymentId">
        #{{ payment.paymentId }} · {{ date(payment.paidAt) }} · {{ payment.paymentMethod === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản' }}
        <p>Ghi vào hóa đơn: {{ money(payment.amount) }} <strong v-if="payment.isSimulation">· MÔ PHỎNG</strong></p>
      </li></ul>
      <nav v-if="paymentsLoaded" aria-label="Phân trang thanh toán">
        <span>{{ paymentTotal }} khoản · Trang {{ paymentPage }}</span>
        <button :disabled="paymentsBusy || paymentPage <= 1" @click="loadPayments(paymentPage - 1)">Trước</button>
        <button :disabled="paymentsBusy || paymentPage >= paymentPages" @click="loadPayments(paymentPage + 1)">Sau</button>
      </nav>
    </section>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { patientsService, type PatientSummary } from '@/services/patients';
import { invoicePortalApi, type PortalPayment, type PortalInvoiceItem } from '@/services/invoicePortal';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
const auth = useAuthStore();
const allowed = computed(() => auth.role === 'PATIENT');
const profiles = ref<Pick<PatientSummary, 'patientId' | 'patientCode' | 'fullName'>[]>([]);
const patientId = ref(0), profilesBusy = ref(false), profileError = ref('');
const validProfile = computed(() => allowed.value && Number.isInteger(patientId.value) && patientId.value > 0 && profiles.value.some(p => p.patientId === patientId.value));
const invoices = ref<InvoiceLookupResponse[]>([]), invoicePage = ref(1), invoicePages = ref(0), invoiceTotal = ref(0);
const invoicesBusy = ref(false), invoicesLoaded = ref(false), invoiceError = ref('');
const selected = ref<InvoiceLookupResponse | null>(null), payments = ref<PortalPayment[]>([]);
const paymentPage = ref(1), paymentPages = ref(0), paymentTotal = ref(0), paymentsBusy = ref(false), paymentsLoaded = ref(false), paymentError = ref('');
const items = ref<PortalInvoiceItem[]>([]), itemPage = ref(1), itemPages = ref(0), itemTotal = ref(0);
const itemsBusy = ref(false), itemsLoaded = ref(false), itemError = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const date = (value: string) => new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
let profileVersion = 0, invoiceVersion = 0, paymentVersion = 0, itemVersion = 0;
function clearItems() { itemVersion++; items.value = []; itemsBusy.value = false; itemsLoaded.value = false; itemPage.value = 1; itemPages.value = 0; itemTotal.value = 0; itemError.value = ''; }
function clearPayments() { clearItems(); paymentVersion++; selected.value = null; payments.value = []; paymentsBusy.value = false; paymentsLoaded.value = false; paymentTotal.value = 0; paymentPages.value = 0; paymentPage.value = 1; paymentError.value = ''; }
function clearInvoices() { invoiceVersion++; invoices.value = []; invoicesBusy.value = false; invoicesLoaded.value = false; invoiceTotal.value = 0; invoicePages.value = 0; invoicePage.value = 1; invoiceError.value = ''; clearPayments(); }
async function loadProfiles() {
  const ticket = ++profileVersion; profiles.value = []; patientId.value = 0; profileError.value = ''; clearInvoices();
  if (!allowed.value) { profilesBusy.value = false; return; }
  profilesBusy.value = true;
  try {
    const rows = await patientsService.getMyProfiles();
    if (ticket === profileVersion && allowed.value) profiles.value = rows.map(p => ({ patientId: p.patientId, patientCode: p.patientCode, fullName: p.fullName }));
  } catch { if (ticket === profileVersion) profileError.value = 'Không tải được hồ sơ. Tải lại để chọn đúng hồ sơ của tài khoản.'; }
  finally { if (ticket === profileVersion) profilesBusy.value = false; }
}
async function changeProfile() { clearInvoices(); if (validProfile.value) await loadInvoices(1); }
async function loadInvoices(target = 1) {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!invoicesLoaded.value || target > invoicePages.value))) return;
  const requestedPatient = patientId.value; const ticket = ++invoiceVersion;
  clearPayments(); invoices.value = []; invoicesLoaded.value = false; invoicesBusy.value = true; invoiceError.value = '';
  try {
    const result = await invoicePortalApi.list(requestedPatient, target);
    if (ticket !== invoiceVersion || !allowed.value) return;
    invoices.value = result.items; invoicePage.value = result.page; invoicePages.value = result.totalPages; invoiceTotal.value = result.totalCount; invoicesLoaded.value = true;
  } catch { if (ticket === invoiceVersion) { invoiceTotal.value = 0; invoicePages.value = 0; invoiceError.value = 'Không tải được hóa đơn của hồ sơ đã chọn. Vui lòng thử lại.'; } }
  finally { if (ticket === invoiceVersion) invoicesBusy.value = false; }
}
async function selectInvoice(id: number) {
  if (!validProfile.value || invoicesBusy.value || !invoicesLoaded.value) return;
  const row = invoices.value.find(i => i.invoiceId === id); if (!row || ((paymentsBusy.value || itemsBusy.value) && selected.value?.invoiceId === id)) return;
  clearPayments(); selected.value = row; await Promise.all([loadItems(1), loadPayments(1)]);
}
async function loadItems(target = 1) {
  if (!validProfile.value || !selected.value || itemsBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!itemsLoaded.value || target > itemPages.value))) return;
  const ticket = ++itemVersion; const invoiceId = selected.value.invoiceId;
  items.value = []; itemsLoaded.value = false; itemsBusy.value = true; itemError.value = '';
  try {
    const result = await invoicePortalApi.items(patientId.value, invoiceId, target);
    if (ticket !== itemVersion || !allowed.value) return;
    items.value = result.items; itemPage.value = result.page; itemPages.value = result.totalPages; itemTotal.value = result.totalCount; itemsLoaded.value = true;
  } catch { if (ticket === itemVersion) { itemTotal.value = 0; itemPages.value = 0; itemError.value = 'Không tải được chi tiết hóa đơn. Chọn lại hóa đơn hoặc thử lại.'; } }
  finally { if (ticket === itemVersion) itemsBusy.value = false; }
}
async function loadPayments(target = 1) {
  if (!validProfile.value || !selected.value || paymentsBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!paymentsLoaded.value || target > paymentPages.value))) return;
  const ticket = ++paymentVersion; const invoiceId = selected.value.invoiceId;
  payments.value = []; paymentsLoaded.value = false; paymentsBusy.value = true; paymentError.value = '';
  try {
    const result = await invoicePortalApi.payments(patientId.value, invoiceId, target);
    if (ticket !== paymentVersion || !allowed.value) return;
    payments.value = result.items; paymentPage.value = result.page; paymentPages.value = result.totalPages; paymentTotal.value = result.totalCount; paymentsLoaded.value = true;
  } catch { if (ticket === paymentVersion) { paymentTotal.value = 0; paymentPages.value = 0; paymentError.value = 'Không tải được các lần thanh toán. Chọn lại hóa đơn hoặc thử lại.'; } }
  finally { if (ticket === paymentVersion) paymentsBusy.value = false; }
}
watch(() => [auth.role, auth.user?.userId], loadProfiles);
onMounted(loadProfiles);
onUnmounted(() => { profileVersion++; profiles.value = []; patientId.value = 0; clearInvoices(); });
</script>
<style scoped>
.page { max-width: 900px; margin: auto; padding: 1rem; }
label { display: block; margin: 1rem 0; } button, select { padding: .5rem; margin: .3rem; }
ul { list-style: none; padding: 0; } li { border: 1px solid #ddd; padding: 1rem; margin: .7rem 0; }
</style>
