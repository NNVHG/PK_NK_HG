<template>
  <main class="invoice-page">
    <div class="page-header">
      <div class="header-nav">
        <RouterLink to="/" class="nav-link">← Trang chủ</RouterLink>
        <RouterLink v-if="!busy && !discountBusy" :to="`${portalPrefix}/clinical/visits/${visitId}/prescription`" class="nav-link alt">
          💊 Xem / kiểm tra đơn thuốc
        </RouterLink>
      </div>
      <div class="header-main">
        <div class="header-titles">
          <span class="eyebrow">TÀI CHÍNH & HÓA ĐƠN CA KHÁM</span>
          <h1>Hóa đơn viện phí · Lần khám #{{ visitId }}</h1>
          <div v-if="visit" class="visit-status-tag">
            <span>Trạng thái ca khám:</span>
            <strong :class="['badge-status', isCorrection ? 'status-correcting' : 'status-normal']">
              {{ isCorrection ? 'Đang đính chính' : visit.status }}
            </strong>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" :disabled="busy || discountBusy" @click="load">
            🔄 Tải lại hồ sơ
          </button>
        </div>
      </div>
    </div>

    <div v-if="busy" class="alert alert-info" role="status">Đang xử lý…</div>
    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="success" class="alert alert-success" role="status">{{ success }}</div>
    <div v-if="invoice?.status === 0" class="alert alert-tip">
      💡 Hóa đơn nháp tự cập nhật dịch vụ khi mở hoặc tải lại hồ sơ. Có thể tiếp tục chỉ định trước khi kết thúc khám.
    </div>

    <template v-if="visit">
      <section v-if="canUnlock" class="action-card unlock-card" aria-label="Mở đính chính hồ sơ">
        <div class="card-header">
          <h2>Mở đính chính hồ sơ</h2>
          <span class="badge badge-warning">Quyền Admin</span>
        </div>
        <p class="card-desc">Hóa đơn chưa thu tiền hiện tại sẽ được hủy và giữ trong lịch sử. Đính chính không chiếm ghế, không hiện TV và không chặn ca đang khám. Hóa đơn mới dùng mã mới.</p>
        <div class="form-group">
          <label for="unlock-reason">Lý do mở khóa (bắt buộc, 10–500 ký tự)</label>
          <textarea
            id="unlock-reason"
            v-model="unlockReason"
            minlength="10"
            maxlength="500"
            placeholder="Nhập lý do nghiệp vụ cần mở đính chính..."
            :disabled="busy || discountBusy"
          />
        </div>
        <button
          class="btn btn-warning"
          :disabled="busy || discountBusy || !ready || unlockReason.trim().length < 10 || unlockReason.trim().length > 500"
          @click="unlock"
        >
          Xác nhận mở khóa và hủy hóa đơn chưa thu
        </button>
      </section>

      <section v-if="isCorrection" class="action-card correction-card" aria-label="Đính chính hồ sơ ngoài ca khám">
        <div class="card-header">
          <h2>Đính chính hồ sơ</h2>
          <span class="badge badge-amber">Chế độ đính chính</span>
        </div>
        <p class="card-desc">Chế độ sửa hồ sơ riêng: không chiếm ghế, không gọi bệnh nhân trên TV và không chặn ca khám khác.</p>
        <form v-if="canCorrect" class="correction-form" @submit.prevent="saveCorrection">
          <div class="form-group">
            <label for="corr-diag">Chẩn đoán</label>
            <textarea id="corr-diag" v-model="correctionDiagnosis" required maxlength="1000" :disabled="busy || discountBusy" />
          </div>
          <div class="form-group">
            <label for="corr-notes">Ghi chú lâm sàng</label>
            <textarea id="corr-notes" v-model="correctionNotes" maxlength="2000" :disabled="busy || discountBusy" />
          </div>
          <div v-if="correctionDirty" class="alert alert-warning">
            Có thay đổi chưa lưu. Lưu hoặc tải lại trước khi hoàn tất và chuyển màn hình.
          </div>
          <div class="form-actions">
            <button class="btn btn-primary" :disabled="busy || discountBusy || !correctionDirty">
              Lưu đính chính chẩn đoán
            </button>
            <RouterLink v-if="canCorrect && !busy && !correctionDirty" :to="`${portalPrefix}/clinical/visits/${visitId}/fdi`" class="btn btn-secondary">
              🦷 Đính chính tình trạng răng
            </RouterLink>
          </div>
        </form>
        <div v-if="correctionNeedsReview" class="alert alert-error" role="alert">
          Tải lại hồ sơ và đối chiếu trước khi tiếp tục đính chính.
        </div>
      </section>

      <section v-if="canComplete" class="action-card complete-card" aria-label="Hoàn tất ca khám">
        <div class="card-header">
          <h2>{{ isCorrection ? 'Hoàn tất đính chính hồ sơ' : 'Kết thúc ca khám & Lập hóa đơn' }}</h2>
          <RouterLink v-if="!busy && !discountBusy" :to="`${portalPrefix}/clinical/visits/${visitId}/services`" class="btn-link">
            Kiểm tra dịch vụ đã chỉ định →
          </RouterLink>
        </div>
        <p class="card-desc">
          {{ isCorrection ? 'Hoàn tất đính chính sẽ khóa lại hồ sơ và chốt hóa đơn mới.' : 'Kết thúc khám sẽ khóa hồ sơ và chốt hóa đơn nháp từ các dịch vụ đã ghi nhận.' }}
        </p>
        <div class="checkbox-confirm">
          <label class="checkbox-label">
            <input v-model="confirmed" type="checkbox" :disabled="busy || discountBusy" />
            <span>{{ isCorrection ? 'Tôi đã kiểm tra nội dung và xác nhận hoàn tất đính chính' : 'Tôi đã kiểm tra chỉ định và xác nhận hoàn tất khám' }}</span>
          </label>
        </div>
        <button class="btn btn-success" :disabled="busy || discountBusy || !confirmed" @click="complete">
          {{ isCorrection ? 'Hoàn tất đính chính và khóa hồ sơ' : 'Kết thúc khám và tạo hóa đơn nháp' }}
        </button>
      </section>
      <div v-else-if="!invoice" class="empty-state">
        <p>Lần khám chưa có hóa đơn nháp. Nha sĩ phụ trách cần hoàn tất khám.</p>
      </div>
    </template>
    <section v-if="invoice" class="invoice-container" aria-label="Chi tiết hóa đơn nháp">
      <div class="invoice-header">
        <div class="code-and-status">
          <span class="invoice-type-label">HÓA ĐƠN VIỆN PHÍ</span>
          <h2 class="invoice-code">{{ invoice.invoiceCode }}</h2>
          <span class="status-pill" :data-status="invoice.status">
            {{ statuses[invoice.status] || invoice.status }}
          </span>
        </div>
        <div class="invoice-meta">
          <div class="meta-row">
            <span class="meta-label">Ngày tạo:</span>
            <span class="meta-value">{{ new Date(invoice.createdAt).toLocaleString('vi-VN') }}</span>
          </div>
        </div>
      </div>

      <div class="items-section">
        <h3 class="items-title">Danh mục dịch vụ & thủ thuật</h3>
        <div v-if="!invoice.items.length" class="empty-items">
          Không có dịch vụ tính phí được ghi nhận trong lần khám.
        </div>
        <div v-else class="items-table-wrap">
          <table class="items-table">
            <thead>
              <tr>
                <th>Dịch vụ / Thủ thuật</th>
                <th>Vị trí điều trị</th>
                <th class="text-right">Đơn giá</th>
                <th class="text-center">Số lượng</th>
                <th class="text-right">Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in invoice.items" :key="index">
                <td>
                  <div class="item-name">{{ item.name }}</div>
                  <div class="item-code">Mã: {{ item.code }}</div>
                </td>
                <td>
                  <span class="tooth-tag">
                    {{ item.toothNumber ? `Răng ${item.toothNumber} · ${item.surface || 'Toàn răng'}` : 'Toàn hàm / tổng quát' }}
                  </span>
                </td>
                <td class="text-right">{{ money(item.unitPrice) }}</td>
                <td class="text-center">
                  <strong>{{ item.quantity }}</strong>
                  <span v-if="item.refundedQuantity" class="refund-sub">
                    (Đã hủy {{ item.refundedQuantity }} / còn {{ item.quantity - item.refundedQuantity }})
                  </span>
                </td>
                <td class="text-right item-total">{{ money(item.totalAmount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="financial-summary-box">
        <div class="summary-rows">
          <div class="summary-line">
            <span class="lbl">Tổng giá niêm yết:</span>
            <span class="val">{{ money(invoice.totalAmount) }}</span>
          </div>
          <div class="summary-line discount">
            <span class="lbl">
              Giảm giá:
              <small v-if="invoice.discountReason"> ({{ invoice.discountReason }})</small>
            </span>
            <span class="val">-{{ money(invoice.discountAmount) }}</span>
          </div>
          <div class="summary-line final-due">
            <span class="lbl">Phải thu sau giảm:</span>
            <span class="val highlight">{{ money(invoice.finalAmount) }}</span>
          </div>
          <div class="summary-divider"></div>
          <div class="summary-line paid">
            <span class="lbl">Đã thu:</span>
            <span class="val">{{ money(invoice.paidAmount) }}</span>
          </div>
          <div class="summary-line remaining">
            <span class="lbl">Còn lại cần thu:</span>
            <span class="val due-val">{{ money(invoice.remainingAmount) }}</span>
          </div>
        </div>
        <p class="summary-note">Dòng gốc giữ nguyên giá đã ghi nhận. Sau hoàn, tổng tiền phía dưới là phần còn lại.</p>
      </div>
      <section v-if="canCancelDraft" class="cancel-draft-panel" aria-labelledby="cancel-draft-title">
        <h3 id="cancel-draft-title" class="cancel-title">Hủy hóa đơn nháp</h3>
        <p class="cancel-desc">Chỉ hủy hóa đơn chưa phát sinh khoản thu. Hóa đơn và lý do được giữ trong lịch sử. Nếu lần khám còn mở, khi xem lại hệ thống tiếp tục tạo bản nháp mới từ dịch vụ hiện tại.</p>
        <form class="cancel-form" @submit.prevent="cancelDraft">
          <div class="form-group">
            <label for="cancel-draft-reason">Lý do hủy (bắt buộc, tối đa 500 ký tự)</label>
            <textarea
              id="cancel-draft-reason"
              v-model="cancelDraftReason"
              required
              maxlength="500"
              placeholder="Nhập lý do nghiệp vụ hủy hóa đơn..."
              :disabled="busy || discountBusy"
            />
            <small class="helper-text">Chỉ ghi lý do nghiệp vụ; không nhập CCCD, số điện thoại hoặc thông tin y tế.</small>
          </div>
          <div class="checkbox-confirm">
            <label class="checkbox-label">
              <input v-model="cancelDraftConfirmed" type="checkbox" :disabled="busy || discountBusy" />
              <span>Tôi đã đối chiếu đúng mã hóa đơn và xác nhận hủy hóa đơn nháp.</span>
            </label>
          </div>
          <button
            type="submit"
            class="btn btn-danger"
            :disabled="busy || discountBusy || !cancelDraftConfirmed || !cancelDraftReason.trim()"
          >
            {{ cancellingDraft ? 'Đang hủy…' : 'Xác nhận hủy hóa đơn nháp' }}
          </button>
        </form>
      </section>
      <InvoiceDiscountPanel v-if="invoice.status !== 4 && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST')"
        :invoice="invoice" :role="auth.role" @applied="load" @refresh="load" @busy="discountBusy = $event" />
      <InvoiceCashPayment v-if="invoice.status !== 4 && !discountBusy && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST')"
        :invoice-id="invoice.id" :remaining-amount="invoice.remainingAmount" :status="invoice.status" @paid="onPaymentReceived" />
      <InvoiceManualBankTransfer v-if="!discountBusy && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST') && invoice.status !== 4"
        :invoice-id="invoice.id" @paid="onBankReceived" />
      <InvoiceBankTransfer v-if="!discountBusy && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST') && [1, 2, 5].includes(invoice.status) && invoice.remainingAmount > 0"
        :invoice-id="invoice.id" @paid="onBankReceived" />
    </section>
    <InvoiceRefundProposalPanel v-if="invoice && invoice.status !== 4 && (auth.role === 'ADMIN' || auth.role === 'RECEPTIONIST')" :invoice="invoice" @changed="load" />
    <InvoiceRefundPanel v-if="invoice && invoice.status !== 4 && auth.role === 'ADMIN'" :invoice-id="invoice.id" :blocked="busy || discountBusy" @changed="load" />
    <section v-if="auth.role === 'ADMIN' && unlockHistory.length" class="history-card" aria-label="Lịch sử mở khóa">
      <div class="card-header">
        <h2>Lịch sử mở khóa ca khám</h2>
        <span class="badge badge-info">{{ unlockHistory.length }} lần</span>
      </div>
      <ul class="history-list">
        <li v-for="record in unlockHistory" :key="record.id" class="history-item">
          <div class="history-meta">
            <span class="time">{{ new Date(record.createdAt).toLocaleString('vi-VN') }}</span>
            <span class="actor">Admin #{{ record.actorUserId }}</span>
            <span v-if="record.cancelledInvoiceId" class="cancelled-tag">
              Hóa đơn đã hủy #{{ record.cancelledInvoiceId }}
            </span>
          </div>
          <div class="history-reason">{{ record.reason }}</div>
        </li>
      </ul>
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
import type { VisitUnlockHistory } from '@/services/invoiceDraft';
import InvoiceRefundPanel from '@/components/InvoiceRefundPanel.vue';
import InvoiceRefundProposalPanel from '@/components/InvoiceRefundProposalPanel.vue';
import InvoiceCashPayment from '@/components/InvoiceCashPayment.vue';
import InvoiceBankTransfer from '@/components/InvoiceBankTransfer.vue';
import InvoiceManualBankTransfer from '@/components/InvoiceManualBankTransfer.vue';
import InvoiceDiscountPanel from '@/components/InvoiceDiscountPanel.vue';
import type { BankTransferResponse } from '@/services/bankTransfers';
import type { CashPaymentResponse } from '@/services/cashPayments';
import { useInvoiceDraftStore } from '@/stores/invoiceDraft';
import { useAuthStore } from '@/stores/auth';
import type { VisitDetails } from '@/services/patients';

const route = useRoute();
const auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
const store = useInvoiceDraftStore();
const { invoice } = storeToRefs(store);
const visitId = computed(() => Number(route.params.visitId));
const visit = ref<VisitDetails | null>(null);
const busy = ref(false);
const ready = ref(false);
const discountBusy = ref(false);
const cancellingDraft = ref(false);
const cancelDraftReason = ref('');
const cancelDraftConfirmed = ref(false);
const error = ref('');
const success = ref('');
const confirmed = ref(false);
const unlockReason = ref('');
const correctionDiagnosis = ref(''), correctionNotes = ref(''), correctionNeedsReview = ref(false);
const isCorrection = computed(() => visit.value?.status === 'Correcting');
const correctionDirty = computed(() => isCorrection.value && (correctionDiagnosis.value !== (visit.value?.diagnosis ?? '') || correctionNotes.value !== (visit.value?.clinicalNotes ?? '')));
const canCorrect = computed(() => isCorrection.value && !visit.value?.isLocked && !correctionNeedsReview.value &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId));
const unlockHistory = ref<VisitUnlockHistory[]>([]);
const canUnlock = computed(() => ready.value && auth.role === 'ADMIN' && visit.value?.status === 'Completed' && visit.value.isLocked &&
  (!invoice.value || invoice.value.paidAmount === 0 && [0, 1].includes(invoice.value.status)));
const canCancelDraft = computed(() => auth.role === 'ADMIN' && invoice.value?.status === 0 && invoice.value.paidAmount === 0);
const statuses: Record<number, string> = { 0: 'Nháp', 1: 'Chờ thanh toán', 2: 'Thanh toán một phần', 3: 'Đã thanh toán', 4: 'Đã hủy', 5: 'Đã hoàn một phần', 6: 'Đã hoàn toàn bộ' };
const canComplete = computed(() => ['InProgress', 'Correcting'].includes(visit.value?.status ?? '') && !visit.value?.isLocked && !correctionDirty.value && !correctionNeedsReview.value &&
  (auth.role === 'ADMIN' || auth.role === 'DENTIST' && visit.value?.dentistId === auth.user?.userId));
const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
let version = 0;
function message(cause: unknown) {
  return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể tải hoặc tạo hóa đơn nháp.' : 'Không thể xử lý yêu cầu.';
}
async function load() {
  const ticket = ++version;
  ready.value = false; busy.value = true; cancellingDraft.value = false; cancelDraftReason.value = ''; cancelDraftConfirmed.value = false;
  error.value = ''; success.value = ''; visit.value = null; store.replace(null); confirmed.value = false; unlockReason.value = ''; unlockHistory.value = []; correctionDiagnosis.value = ''; correctionNotes.value = ''; correctionNeedsReview.value = false;
  if (!Number.isInteger(visitId.value) || visitId.value <= 0) { error.value = 'Mã lần khám không hợp lệ.'; busy.value = false; return; }
  try {
    const details = await visitsService.getById(visitId.value);
    if (ticket !== version) return;
    visit.value = details; correctionDiagnosis.value = details.diagnosis ?? ''; correctionNotes.value = details.clinicalNotes ?? '';
    try {
      const data = await invoiceDraftApi.get(visitId.value);
      if (ticket === version) store.replace(data);
    } catch (cause) {
      if (!axios.isAxiosError(cause) || cause.response?.status !== 404) throw cause;
    }
    if (ticket === version && auth.role === 'ADMIN') {
      const records = await invoiceDraftApi.unlockHistory(visitId.value);
      if (ticket === version) unlockHistory.value = records;
    }
    if (ticket === version) ready.value = true;
  } catch (cause) { if (ticket === version) { error.value = message(cause); ready.value = false; visit.value = null; store.replace(null); unlockHistory.value = []; } }
  finally { if (ticket === version) busy.value = false; }
}
async function cancelDraft() {
  const current = invoice.value;
  const reason = cancelDraftReason.value.trim();
  if (!current || !canCancelDraft.value || busy.value || discountBusy.value || !cancelDraftConfirmed.value || !reason || reason.length > 500) return;
  const ticket = version;
  busy.value = true; cancellingDraft.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.cancelDraft(current.id, reason);
    if (ticket !== version) return;
    store.replace({ ...current, status: 4 });
    cancelDraftReason.value = ''; cancelDraftConfirmed.value = false;
    success.value = `Đã hủy hóa đơn ${current.invoiceCode}; lý do và lịch sử được giữ lại.`;
  } catch (cause) {
    if (ticket === version) error.value = message(cause);
  } finally {
    if (ticket === version) { busy.value = false; cancellingDraft.value = false; }
  }
}
async function onPaymentReceived(payment: CashPaymentResponse) {
  const expectedVersion = version + 1;
  await load();
  if (version === expectedVersion)
    success.value = `Đã ghi nhận khoản thu #${payment.id}: ${money(payment.amount)}. Tiền thối: ${money(payment.changeAmount ?? 0)}.`;
}
async function onBankReceived(payment: BankTransferResponse | null) {
  const expectedVersion = version + 1; await load();
  if (version === expectedVersion) success.value = payment
    ? `${payment.source === 'Simulation' ? 'Đã ghi nhận MÔ PHỎNG' : 'Đã đối chiếu chuyển khoản'} #${payment.id}: ${money(payment.amount)} vào hóa đơn. ${payment.note ?? ''}`
    : 'Đã có khoản thu mới. Đối chiếu hóa đơn và lịch sử khoản thu bên dưới.';
}
async function unlock() {
  const reason = unlockReason.value.trim();
  if (!canUnlock.value || busy.value || discountBusy.value || reason.length < 10 || reason.length > 500) return;
  const ticket = version; const oldCode = invoice.value?.invoiceCode;
  busy.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.unlock(visitId.value, reason);
    if (ticket !== version) return;
    const refreshVersion = version + 1; await load();
    if (refreshVersion === version && ready.value && visit.value?.status === 'Correcting')
      success.value = oldCode ? `Đã mở đính chính và hủy hóa đơn ${oldCode}. Hóa đơn mới dùng mã mới.` : 'Đã mở đính chính hồ sơ.';
  } catch (cause) {
    if (ticket !== version) return;
    const failureMessage = message(cause); const attemptedReason = unlockReason.value; const refreshVersion = version + 1;
    await load();
    if (refreshVersion === version) {
      error.value = failureMessage;
      if (ready.value && visit.value?.status === 'Completed') unlockReason.value = attemptedReason;
    }
  } finally { if (ticket === version) busy.value = false; }
}
async function saveCorrection() {
  if (!canCorrect.value || !correctionDirty.value || busy.value || discountBusy.value) return;
  if (!correctionDiagnosis.value.trim() || correctionDiagnosis.value.length > 1000 || correctionNotes.value.length > 2000) {
    error.value = 'Nhập chẩn đoán tối đa 1000 ký tự và ghi chú tối đa 2000 ký tự.'; return;
  }
  const ticket = version, expectedId = visitId.value;
  busy.value = true; confirmed.value = false; error.value = ''; success.value = '';
  try {
    const result = await visitsService.updateDiagnosis(expectedId, { diagnosis: correctionDiagnosis.value.trim(), clinicalNotes: correctionNotes.value.trim() || null });
    if (ticket !== version) return;
    if (result.visitId !== expectedId || result.status !== 'Correcting' || result.isLocked) throw new Error('Correction changed');
    visit.value = result; correctionDiagnosis.value = result.diagnosis ?? ''; correctionNotes.value = result.clinicalNotes ?? '';
    success.value = 'Đã lưu đính chính chẩn đoán. Kiểm tra dịch vụ trước khi hoàn tất.';
  } catch (cause) {
    if (ticket === version) { error.value = message(cause); correctionNeedsReview.value = true; }
  } finally { if (ticket === version) busy.value = false; }
}
async function complete() {
  if (!confirmed.value || !canComplete.value || busy.value || discountBusy.value) return;
  const ticket = version, completingCorrection = isCorrection.value; busy.value = true; error.value = ''; success.value = '';
  try {
    await invoiceDraftApi.complete(visitId.value);
    if (ticket !== version) return;
    const refreshVersion = version + 1;
    await load();
    if (refreshVersion === version && invoice.value && visit.value?.status === 'Completed' && visit.value.isLocked) success.value = completingCorrection ? 'Đã hoàn tất đính chính, khóa hồ sơ và chốt hóa đơn chờ thanh toán.' : 'Đã kết thúc khám và tạo hóa đơn chờ thanh toán. Hồ sơ đã được khóa.';
  } catch (cause) {
    if (ticket !== version) return;
    const failureMessage = message(cause);
    const refreshVersion = version + 1;
    await load(); if (refreshVersion === version) error.value = failureMessage;
  } finally { if (ticket === version) busy.value = false; }
}
watch(visitId, load, { immediate: true });
watch(() => [auth.role, auth.user?.userId], load);
onUnmounted(() => { version++; ready.value = false; store.replace(null); visit.value = null; correctionDiagnosis.value = ''; correctionNotes.value = ''; unlockReason.value = ''; unlockHistory.value = []; error.value = ''; success.value = ''; confirmed.value = false; });
</script>

<style scoped>
.invoice-page {
  max-width: 980px;
  margin: 1.5rem auto;
  padding: 1.5rem;
  background: #f8fafc;
  color: #1e293b;
  font-family: inherit;
}

.page-header {
  margin-bottom: 1.5rem;
}

.header-nav {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.nav-link {
  display: inline-flex;
  align-items: center;
  font-size: 0.9rem;
  font-weight: 600;
  color: #0284c7;
  text-decoration: none;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  background: #e0f2fe;
  transition: all 0.15s ease-in-out;
}

.nav-link.alt {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.nav-link:hover {
  text-decoration: none;
  filter: brightness(0.95);
}

.header-main {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
}

.header-titles {
  border-left: 4px solid #0284c7;
  padding-left: 1rem;
}

.eyebrow {
  display: block;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: #0284c7;
  text-transform: uppercase;
  margin-bottom: 0.25rem;
}

h1 {
  color: #0f2d59;
  font-size: 1.65rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
  line-height: 1.3;
}

.visit-status-tag {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  color: #64748b;
}

.badge-status {
  padding: 0.2rem 0.55rem;
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 600;
}

.status-normal {
  background: #f1f5f9;
  color: #334155;
  border: 1px solid #cbd5e1;
}

.status-correcting {
  background: #fffbeb;
  color: #b45309;
  border: 1px solid #fde68a;
}

.action-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
  margin: 1.25rem 0;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

.unlock-card {
  border-left: 4px solid #f59e0b;
}

.correction-card {
  border-left: 4px solid #ea580c;
}

.complete-card {
  border-left: 4px solid #10b981;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.card-header h2 {
  color: #0f2d59;
  font-size: 1.2rem;
  font-weight: 700;
  margin: 0;
}

.card-desc {
  color: #64748b;
  font-size: 0.92rem;
  margin: 0.25rem 0 1rem;
  line-height: 1.5;
}

.badge {
  padding: 0.25rem 0.5rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.badge-warning {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fde68a;
}

.badge-amber {
  background: #fff7ed;
  color: #c2410c;
  border: 1px solid #ffedd5;
}

.badge-info {
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  font-size: 0.9rem;
  font-weight: 600;
  color: #334155;
  margin-bottom: 0.4rem;
}

.helper-text {
  display: block;
  font-size: 0.8rem;
  color: #64748b;
  margin-top: 0.25rem;
}

.checkbox-confirm {
  margin: 1rem 0;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.92rem;
  font-weight: 500;
  color: #334155;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  width: 1.15rem;
  height: 1.15rem;
  accent-color: #0284c7;
}

.form-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 1rem;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1.25rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  border: 1px solid transparent;
}

.btn-primary {
  background: #0f2d59;
  color: #ffffff;
}

.btn-primary:hover:not(:disabled) {
  background: #1e3a8a;
}

.btn-secondary {
  background: #ffffff;
  color: #475569;
  border: 1px solid #cbd5e1;
}

.btn-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  color: #1e293b;
}

.btn-warning {
  background: #d97706;
  color: #ffffff;
}

.btn-warning:hover:not(:disabled) {
  background: #b45309;
}

.btn-danger {
  background: #b91c1c;
  color: #ffffff;
}

.btn-danger:hover:not(:disabled) {
  background: #991b1b;
}

.btn-success {
  background: #059669;
  color: #ffffff;
}

.btn-success:hover:not(:disabled) {
  background: #047857;
}

.btn-link {
  font-size: 0.9rem;
  font-weight: 600;
  color: #0284c7;
  text-decoration: none;
}

.btn-link:hover {
  text-decoration: underline;
}

.btn:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

input:not([type="checkbox"]):not([type="radio"]),
select,
textarea {
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

textarea {
  min-height: 84px;
  resize: vertical;
}

input:focus-visible,
select:focus-visible,
textarea:focus-visible {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.2);
}

.empty-state {
  background: #ffffff;
  border: 1px dashed #cbd5e1;
  border-radius: 12px;
  padding: 2rem;
  text-align: center;
  color: #64748b;
  font-size: 0.95rem;
  margin: 1.5rem 0;
}

.invoice-container {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.5rem;
  margin: 1.5rem 0;
  box-shadow: 0 2px 10px rgba(15, 45, 89, 0.05);
}

.invoice-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  border-bottom: 2px solid #e2e8f0;
  padding-bottom: 1.25rem;
  margin-bottom: 1.25rem;
  gap: 1rem;
}

.code-and-status {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.invoice-type-label {
  font-size: 0.75rem;
  font-weight: 700;
  color: #64748b;
  letter-spacing: 0.05em;
  background: #f1f5f9;
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
}

.invoice-code {
  color: #0f2d59;
  font-size: 1.4rem;
  font-weight: 700;
  margin: 0;
}

.status-pill {
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.82rem;
  font-weight: 600;
  background: #f1f5f9;
  color: #475569;
}

.status-pill[data-status="0"] { background: #fef3c7; color: #92400e; }
.status-pill[data-status="1"] { background: #e0f2fe; color: #0369a1; }
.status-pill[data-status="2"] { background: #ffedd5; color: #c2410c; }
.status-pill[data-status="3"] { background: #dcfce7; color: #166534; }
.status-pill[data-status="4"] { background: #fee2e2; color: #991b1b; }
.status-pill[data-status="5"] { background: #fef9c3; color: #854d0e; }
.status-pill[data-status="6"] { background: #f3f4f6; color: #374151; }

.invoice-meta {
  font-size: 0.9rem;
  color: #64748b;
}

.meta-label {
  margin-right: 0.35rem;
}

.meta-value {
  font-weight: 600;
  color: #1e293b;
}

.items-section {
  margin-bottom: 1.5rem;
}

.items-title {
  color: #0f2d59;
  font-size: 1.1rem;
  font-weight: 700;
  margin: 0 0 0.75rem;
}

.empty-items {
  padding: 1.25rem;
  background: #f8fafc;
  border-radius: 8px;
  text-align: center;
  color: #64748b;
  font-size: 0.92rem;
}

.items-table-wrap {
  overflow-x: auto;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
}

.items-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.items-table th,
.items-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid #e2e8f0;
  font-size: 0.92rem;
}

.items-table th {
  background: #f8fafc;
  color: #0f2d59;
  font-weight: 600;
}

.items-table tr:last-child td {
  border-bottom: none;
}

.item-name {
  font-weight: 600;
  color: #0f2d59;
}

.item-code {
  font-size: 0.8rem;
  color: #64748b;
}

.tooth-tag {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  background: #e0f2fe;
  color: #0369a1;
  border-radius: 4px;
  font-size: 0.82rem;
  font-weight: 500;
}

.refund-sub {
  display: block;
  font-size: 0.78rem;
  color: #b91c1c;
}

.text-right {
  text-align: right;
}

.text-center {
  text-align: center;
}

.item-total {
  font-weight: 600;
  color: #0f2d59;
}

.financial-summary-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1.25rem;
  margin: 1.5rem 0;
}

.summary-rows {
  max-width: 24rem;
  margin-left: auto;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.summary-line {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.95rem;
}

.summary-line.discount .val {
  color: #059669;
  font-weight: 600;
}

.summary-line.final-due {
  font-size: 1.1rem;
  font-weight: 700;
  color: #0f2d59;
}

.summary-line.final-due .val.highlight {
  color: #0284c7;
}

.summary-divider {
  height: 1px;
  background: #cbd5e1;
  margin: 0.35rem 0;
}

.summary-line.remaining .due-val {
  font-size: 1.15rem;
  font-weight: 700;
  color: #b91c1c;
}

.summary-note {
  font-size: 0.8rem;
  color: #64748b;
  margin: 0.75rem 0 0;
  text-align: right;
}

.cancel-draft-panel {
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 8px;
  padding: 1.25rem;
  margin: 1.5rem 0;
}

.cancel-title {
  color: #991b1b;
  font-size: 1.05rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
}

.cancel-desc {
  font-size: 0.88rem;
  color: #7f1d1d;
  margin: 0 0 1rem;
}

.history-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
  margin: 1.5rem 0;
}

.history-list {
  list-style: none;
  padding: 0;
  margin: 0.75rem 0 0;
}

.history-item {
  padding: 0.75rem 0;
  border-bottom: 1px solid #e2e8f0;
}

.history-item:last-child {
  border-bottom: none;
}

.history-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.82rem;
  color: #64748b;
  margin-bottom: 0.35rem;
}

.history-meta .time {
  font-weight: 600;
  color: #334155;
}

.history-meta .actor {
  background: #f1f5f9;
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
}

.history-meta .cancelled-tag {
  background: #fee2e2;
  color: #991b1b;
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
}

.history-reason {
  font-size: 0.9rem;
  color: #1e293b;
  line-height: 1.5;
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  font-size: 0.95rem;
  margin-bottom: 1rem;
}

.alert-error {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
}

.alert-info {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
}

.alert-success {
  color: #166534;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
}

.alert-warning {
  color: #92400e;
  background: #fffbeb;
  border: 1px solid #fde68a;
}

.alert-tip {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
}

@media (max-width: 640px) {
  .invoice-page {
    padding: 1rem 0.75rem;
  }
  .header-main {
    flex-direction: column;
    align-items: flex-start;
  }
  .header-actions {
    width: 100%;
  }
  .header-actions .btn {
    width: 100%;
  }
  .form-actions {
    flex-direction: column;
  }
  .form-actions .btn {
    width: 100%;
  }
  .btn {
    width: 100%;
  }
}
</style>
