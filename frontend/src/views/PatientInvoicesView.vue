<template>
  <main class="page">
    <h1>Hóa đơn của tôi</h1>
    <button :disabled="profilesBusy" @click="loadProfiles">Tải lại hồ sơ</button>
    <p v-if="profileError" role="alert">{{ profileError }}</p>
    <label v-if="profiles.length > 1">Chọn hồ sơ để xem hóa đơn
      <select v-model.number="patientId" :disabled="profilesBusy || invoicesBusy" @change="selectProfile">
        <option v-for="profile in profiles" :key="profile.patientId" :value="profile.patientId">
          {{ profile.fullName }} · {{ profile.patientCode }}
        </option>
      </select>
    </label>
    <p v-if="validProfile && selectedProfile">Hồ sơ đã chọn: {{ selectedProfile.fullName }} · {{ birth(selectedProfile.dateOfBirth) }} · {{ selectedProfile.patientCode }}{{ selectedProfile.isActive ? '' : ' · Đã ngừng hoạt động' }}</p>
    <p v-if="!profilesBusy && !profileError && !profiles.length">Tài khoản chưa có hồ sơ bệnh nhân.</p>
    <form @submit.prevent="searchInvoices">
      <label>Trạng thái
        <select v-model="status" :disabled="invoicesBusy || !validProfile">
          <option value="">Tất cả</option><option v-for="(name, index) in statuses" :key="index" :value="String(index)">{{ name }}</option>
        </select>
      </label>
      <label>Từ ngày lập (giờ Việt Nam)
        <input v-model="fromDate" type="date" min="0001-01-02" max="9999-12-31" :disabled="invoicesBusy || !validProfile" />
      </label>
      <label>Đến ngày lập (bao gồm cả ngày)
        <input v-model="toDate" type="date" min="0001-01-01" max="9999-12-30" :disabled="invoicesBusy || !validProfile" />
      </label>
      <button :disabled="invoicesBusy || !validProfile">Lọc hóa đơn</button>
      <button type="button" :disabled="invoicesBusy || !validProfile" @click="resetInvoiceFilters">Xóa bộ lọc</button>
    </form>
    <button :disabled="invoicesBusy || !validProfile" @click="loadInvoices(1)">Tải hóa đơn của tôi</button>
    <p v-if="invoicesLoaded">Đã áp dụng: {{ applied.status == null ? 'Tất cả trạng thái' : statuses[applied.status] }} ·
      Từ {{ applied.fromDate ? birth(applied.fromDate) : 'không giới hạn' }} đến {{ applied.toDate ? birth(applied.toDate) : 'không giới hạn' }} (giờ Việt Nam)</p>
    <p v-if="invoicesBusy" role="status">Đang tải hóa đơn…</p>
    <p v-if="invoiceError" role="alert">{{ invoiceError }}</p>
    <p v-if="invoicesLoaded && !invoices.length">Không có hóa đơn trong trang và bộ lọc này.</p>
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
import { invoicePortalApi, type PortalPayment, type PortalInvoiceItem, type PortalInvoiceProfile, type PortalInvoiceFilters } from '@/services/invoicePortal';
import type { InvoiceLookupResponse } from '@/services/invoiceLookup';
const auth = useAuthStore();
const allowed = computed(() => auth.role === 'PATIENT');
const profiles = ref<PortalInvoiceProfile[]>([]);
const patientId = ref(0), profilesBusy = ref(false), profileError = ref('');
const selectedProfile = computed(() => profiles.value.find(p => p.patientId === patientId.value));
const validProfile = computed(() => allowed.value && Number.isInteger(patientId.value) && patientId.value > 0 && !!selectedProfile.value);
const invoices = ref<InvoiceLookupResponse[]>([]), invoicePage = ref(1), invoicePages = ref(0), invoiceTotal = ref(0);
const invoicesBusy = ref(false), invoicesLoaded = ref(false), invoiceError = ref('');
const status = ref(''), fromDate = ref(''), toDate = ref('');
const applied = ref<PortalInvoiceFilters>({});
const selected = ref<InvoiceLookupResponse | null>(null), payments = ref<PortalPayment[]>([]);
const paymentPage = ref(1), paymentPages = ref(0), paymentTotal = ref(0), paymentsBusy = ref(false), paymentsLoaded = ref(false), paymentError = ref('');
const items = ref<PortalInvoiceItem[]>([]), itemPage = ref(1), itemPages = ref(0), itemTotal = ref(0);
const itemsBusy = ref(false), itemsLoaded = ref(false), itemError = ref('');
const statuses = ['Nháp', 'Chờ thanh toán', 'Thanh toán một phần', 'Đã thanh toán', 'Đã hủy'];
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
const birth = (value: string) => value.split('-').reverse().join('/');
const date = (value: string) => new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
let profileVersion = 0, invoiceVersion = 0, paymentVersion = 0, itemVersion = 0;
function clearItems() { itemVersion++; items.value = []; itemsBusy.value = false; itemsLoaded.value = false; itemPage.value = 1; itemPages.value = 0; itemTotal.value = 0; itemError.value = ''; }
function clearPayments() { clearItems(); paymentVersion++; selected.value = null; payments.value = []; paymentsBusy.value = false; paymentsLoaded.value = false; paymentTotal.value = 0; paymentPages.value = 0; paymentPage.value = 1; paymentError.value = ''; }
function clearInvoices() { invoiceVersion++; invoices.value = []; invoicesBusy.value = false; invoicesLoaded.value = false; invoiceTotal.value = 0; invoicePages.value = 0; invoicePage.value = 1; invoiceError.value = ''; clearPayments(); }
function clearFilters() { status.value = ''; fromDate.value = ''; toDate.value = ''; applied.value = {}; }
function selectProfile() { clearFilters(); clearInvoices(); }
function validDate(value: string) {
  if (!value) return true;
  if (!/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(value) || value < '0001-01-01') return false;
  try { return new Date(value + 'T00:00:00Z').toISOString().slice(0, 10) === value; } catch { return false; }
}
async function searchInvoices() {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value) return;
  clearInvoices();
  if (!['', '0', '1', '2', '3', '4'].includes(status.value)) { invoiceError.value = 'Chọn trạng thái hóa đơn hợp lệ.'; return; }
  if (!validDate(fromDate.value) || !validDate(toDate.value) ||
      (fromDate.value && fromDate.value < '0001-01-02') || (toDate.value && toDate.value > '9999-12-30') ||
      (fromDate.value && toDate.value && fromDate.value > toDate.value)) {
    invoiceError.value = 'Nhập khoảng ngày lập hóa đơn hợp lệ; từ ngày không sau đến ngày.'; return;
  }
  applied.value = { ...(status.value !== '' ? { status: Number(status.value) } : {}),
    ...(fromDate.value ? { fromDate: fromDate.value } : {}), ...(toDate.value ? { toDate: toDate.value } : {}) };
  await loadInvoices(1);
}
async function resetInvoiceFilters() {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value) return;
  clearFilters(); await loadInvoices(1);
}
async function loadProfiles() { clearFilters();
  const ticket = ++profileVersion; profiles.value = []; patientId.value = 0; profileError.value = ''; clearInvoices();
  if (!allowed.value) { profilesBusy.value = false; return; }
  profilesBusy.value = true;
  try {
    const rows = await invoicePortalApi.profiles();
    if (ticket === profileVersion && allowed.value) {
      if (rows.length > 0 && new Set(rows.map(p => p.patientId)).size !== rows.length)
        profileError.value = 'Danh sách hồ sơ không hợp lệ. Vui lòng tải lại.';
      else if (rows.length > 0) {
        profiles.value = rows.map(p => ({ patientId: p.patientId, patientCode: p.patientCode, fullName: p.fullName, dateOfBirth: p.dateOfBirth, isActive: p.isActive }));
        patientId.value = rows[0].patientId;
      }
    }
  } catch { if (ticket === profileVersion) profileError.value = 'Không tải được hồ sơ. Vui lòng tải lại.'; }
  finally { if (ticket === profileVersion) profilesBusy.value = false; }
}
async function loadInvoices(target = 1) {
  if (!validProfile.value || profilesBusy.value || invoicesBusy.value || !Number.isInteger(target) || target < 1 || target > 1000000 ||
      (target !== 1 && (!invoicesLoaded.value || target > invoicePages.value))) return;
  const requestedPatient = patientId.value; const ticket = ++invoiceVersion;
  clearPayments(); invoices.value = []; invoicesLoaded.value = false; invoicesBusy.value = true; invoiceError.value = '';
  try {
    const result = await invoicePortalApi.list(requestedPatient, target, { ...applied.value });
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
onUnmounted(() => { profileVersion++; profiles.value = []; patientId.value = 0; clearFilters(); clearInvoices(); });
</script>
<style scoped>
.page { max-width: 900px; margin: auto; padding: 1rem; }
label { display: block; margin: 1rem 0; } button, select { padding: .5rem; margin: .3rem; }
ul { list-style: none; padding: 0; } li { border: 1px solid #ddd; padding: 1rem; margin: .7rem 0; }
</style>
