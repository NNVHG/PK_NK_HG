<template>
  <section v-if="role === 'RECEPTIONIST' && canPropose" class="refund-proposal" aria-label="Đề xuất hoàn tiền">
    <h2>Đề xuất hoàn tiền</h2>
    <p>Chọn đúng dòng và số lượng cần hoàn. Đề xuất chưa làm thay đổi hóa đơn; Admin sẽ duyệt đúng phạm vi đã gửi.</p>
    <form @submit.prevent="submit">
      <fieldset :disabled="busy"><legend>Dòng đề nghị</legend>
        <label v-for="item in invoice.items.filter(x => x.quantity - (x.refundedQuantity ?? 0) > 0)" :key="item.id">
          {{ item.code }} · {{ item.name }} · còn {{ item.quantity - (item.refundedQuantity ?? 0) }}
          <input v-model.number="quantities[item.id]" type="number" min="0" :max="item.quantity - (item.refundedQuantity ?? 0)" step="1" :aria-label="`Số lượng đề xuất hoàn ${item.code}`" />
        </label>
      </fieldset>
      <label>Lý do
        <select v-model="reasonCode" required :disabled="busy">
          <option value="CANCEL_BEFORE_TREATMENT">Chưa thực hiện dịch vụ</option>
          <option value="MEDICAL_CONTRAINDICATION">Chống chỉ định y khoa</option>
          <option value="BILLING_ERROR">Sai sót hóa đơn</option>
          <option value="PATIENT_REQUEST">Bệnh nhân yêu cầu</option>
          <option value="OTHER">Khác</option>
        </select>
      </label>
      <label>Ghi chú lý do (5–500 ký tự)
        <textarea v-model="reasonNotes" required minlength="5" maxlength="500" :disabled="busy" />
      </label>
      <p v-if="error" role="alert">{{ error }}</p>
      <p v-if="created" role="status">Đã gửi đề xuất #{{ created.id }}. Dòng, số lượng và lý do đã khóa; chờ Admin duyệt.</p>
      <button :disabled="busy || !selection.length || reasonNotes.trim().length < 5">Gửi đề xuất cho Admin</button>
    </form>
  </section>

  <section v-else-if="role === 'ADMIN'" class="refund-proposal" aria-label="Đề xuất hoàn tiền cần duyệt">
    <h2>Đề xuất hoàn tiền của Lễ tân</h2>
    <button :disabled="busy" @click="load">Tải lại đề xuất</button>
    <p v-if="busy" role="status">Đang xử lý…</p><p v-if="error" role="alert">{{ error }}</p>
    <p v-if="!busy && !proposals.length">Không có đề xuất đang chờ hoặc cần đối chiếu cho hóa đơn này.</p>
    <article v-for="proposal in proposals" :key="proposal.id" class="proposal-card">
      <h3>Đề xuất #{{ proposal.id }} · {{ statusLabel(proposal.status) }}</h3>
      <p>Người gửi: {{ proposal.requesterName }} · {{ new Date(proposal.createdAt).toLocaleString('vi-VN') }}</p>
      <p>Lý do: {{ reasonLabel(proposal.reasonCode) }} — {{ proposal.reasonNotes }}</p>
      <ul><li v-for="line in proposal.items" :key="line.invoiceItemId">{{ line.code }} · {{ line.name }} — {{ line.quantity }} × {{ money(line.unitPrice) }}</li></ul>
      <p v-if="proposal.status === 'Stale'" role="alert">Hóa đơn đã thay đổi sau khi gửi. Cần Lễ tân tạo đề xuất mới.</p>
      <p v-if="proposal.status === 'Rejected'">Admin từ chối: {{ proposal.decisionNotes }}</p>
      <template v-if="proposal.status === 'Pending'">
        <label>Phương thức chi
          <select v-model="decisions[proposal.id].method" :disabled="busy" @change="clearPreview(proposal.id)"><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option></select>
        </label>
        <button :disabled="busy" @click="preview(proposal)">Xem trước đúng phạm vi đề xuất</button>
        <section v-if="decisions[proposal.id].preview" aria-label="Xem trước đề xuất">
          <p>Hoàn trả khách: <strong>{{ money(decisions[proposal.id].preview!.refundAmount) }}</strong></p>
          <p>Giảm công nợ: {{ money(decisions[proposal.id].preview!.debtReduction) }} · Còn nợ: {{ money(decisions[proposal.id].preview!.remainingAfter) }}</p>
          <label v-if="decisions[proposal.id].preview!.requiresLateApproval"><input v-model="decisions[proposal.id].late" type="checkbox" :disabled="busy" /> Admin xác nhận ngoại lệ quá hạn 7 ngày</label>
          <label v-if="decisions[proposal.id].preview!.requiresReserve"><input v-model="decisions[proposal.id].reserve" type="checkbox" :disabled="busy" /> Admin xác nhận xuất quỹ dự phòng</label>
          <label><input v-model="decisions[proposal.id].confirmed" type="checkbox" :disabled="busy" /> Tôi đã đối chiếu và duyệt đúng các dòng/số lượng trên</label>
          <button :disabled="busy || !decisions[proposal.id].confirmed || decisions[proposal.id].preview!.requiresLateApproval && !decisions[proposal.id].late || decisions[proposal.id].preview!.requiresReserve && !decisions[proposal.id].reserve" @click="approve(proposal)">Duyệt đúng phạm vi và ghi hoàn</button>
        </section>
        <details><summary>Từ chối đề xuất</summary>
          <label>Ghi chú từ chối (không bắt buộc, tối đa 500 ký tự)<textarea v-model="decisions[proposal.id].rejectNotes" maxlength="500" :disabled="busy" /></label>
          <button :disabled="busy" @click="reject(proposal)">Từ chối</button>
        </details>
      </template>
    </article>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import type { InvoiceDraft } from '@/services/invoiceDraft';
import { invoiceRefundProposalsApi, type RefundProposal } from '@/services/invoiceRefundProposals';
import { MOD_BIL_refundMoney, MOD_BIL_validPreview } from '@/services/invoiceRefunds';
const props = defineProps<{ invoice: InvoiceDraft }>();
const emit = defineEmits<{ changed: [] }>();
const auth = useAuthStore();
const role = computed(() => auth.role ?? '');
const canPropose = computed(() => [2, 3, 5].includes(props.invoice.status));
const quantities = reactive<Record<number, number>>({});
const reasonCode = ref('BILLING_ERROR'), reasonNotes = ref('');
const proposals = ref<RefundProposal[]>([]), created = ref<RefundProposal | null>(null);
const busy = ref(false), error = ref('');
const decisions = reactive<Record<number, { method: string; preview: import('@/services/invoiceRefunds').RefundPreview | null; confirmed: boolean; late: boolean; reserve: boolean; rejectNotes: string }>>({});
const selection = computed(() => props.invoice.items.map(x => ({ invoiceItemId: x.id, quantity: quantities[x.id] ?? 0 })).filter(x => Number.isSafeInteger(x.quantity) && x.quantity > 0));
const money = (value: string) => MOD_BIL_refundMoney(value);
const labels: Record<string, string> = { CANCEL_BEFORE_TREATMENT: 'Chưa thực hiện dịch vụ', MEDICAL_CONTRAINDICATION: 'Chống chỉ định y khoa', BILLING_ERROR: 'Sai sót hóa đơn', PATIENT_REQUEST: 'Bệnh nhân yêu cầu', OTHER: 'Khác' };
function reasonLabel(value: string) { return labels[value] ?? value; }
function statusLabel(value: string) { return ({ Pending: 'Chờ duyệt', Approved: 'Đã duyệt', Rejected: 'Đã từ chối', Stale: 'Cần gửi lại' } as Record<string,string>)[value] ?? value; }
function failure(cause: unknown) { return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không thể xử lý đề xuất hoàn tiền.' : 'Không thể xử lý đề xuất hoàn tiền.'; }
function state(id: number) { return decisions[id] ??= { method: 'Cash', preview: null, confirmed: false, late: false, reserve: false, rejectNotes: '' }; }
function clearPreview(id: number) { const row=state(id); row.preview=null; row.confirmed=false; row.late=false; row.reserve=false; }
async function submit() {
 if (busy.value || !canPropose.value || !selection.value.length || reasonNotes.value.trim().length < 5) return;
 busy.value=true;error.value='';
 try { created.value=await invoiceRefundProposalsApi.create(props.invoice.id,{items:selection.value,reasonCode:reasonCode.value,reasonNotes:reasonNotes.value.trim()});Object.keys(quantities).forEach(k=>delete quantities[Number(k)]);reasonNotes.value=''; }
 catch(e){error.value=failure(e);}finally{busy.value=false;}
}
async function load() {
 if (role.value!=='ADMIN') return;
 busy.value=true;error.value='';
 try { proposals.value=await invoiceRefundProposalsApi.list(props.invoice.id);proposals.value.forEach(p=>state(p.id)); }
 catch(e){error.value=failure(e);}finally{busy.value=false;}
}
async function preview(proposal: RefundProposal) {
 if (busy.value || proposal.status!=='Pending') return;
 busy.value=true;error.value='';clearPreview(proposal.id);
 try {const p=await invoiceRefundProposalsApi.preview(proposal.id,state(proposal.id).method);if(!MOD_BIL_validPreview(p))throw Error('Invalid preview');state(proposal.id).preview=p;}
 catch(e){error.value=failure(e);await load();}finally{busy.value=false;}
}
async function approve(proposal: RefundProposal) {
 const row=state(proposal.id),p=row.preview;if(busy.value||!p||!row.confirmed)return;
 busy.value=true;error.value='';
 try {await invoiceRefundProposalsApi.approve(proposal.id,{paymentMethod:row.method,expectedPreview:p.preview,confirmed:true,lateApproval:row.late,reserveConfirmed:row.reserve});emit('changed');await load();}
 catch(e){error.value=failure(e);await load();}finally{busy.value=false;}
}
async function reject(proposal: RefundProposal) {
 const row=state(proposal.id);if(busy.value)return;
 busy.value=true;error.value='';
 try {await invoiceRefundProposalsApi.reject(proposal.id,row.rejectNotes.trim() || null);await load();}
 catch(e){error.value=failure(e);await load();}finally{busy.value=false;}
}
watch(()=>[props.invoice.id,role.value],()=>{created.value=null;error.value='';void load();},{immediate:true});
onMounted(()=>{props.invoice.items.forEach(i=>{quantities[i.id]=0;});});
</script>

<style scoped>
.refund-proposal {
  border: 1px solid #e2e8f0;
  padding: 1.25rem;
  margin: 1.25rem 0;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

.refund-proposal h3,
.refund-proposal h4 {
  color: #0f2d59;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

.refund-proposal label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

.refund-proposal input:not([type="checkbox"]):not([type="radio"]),
.refund-proposal select,
.refund-proposal textarea {
  display: block;
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

.refund-proposal textarea {
  min-height: 80px;
  resize: vertical;
}

.refund-proposal input:focus-visible,
.refund-proposal select:focus-visible,
.refund-proposal textarea:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

.refund-proposal input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  vertical-align: middle;
  margin-right: 0.4rem;
}

.refund-proposal button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #0f2d59;
  color: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  margin: 0.4rem 0.4rem 0.4rem 0;
}

.refund-proposal button:hover:not(:disabled) {
  background: #1e3a8a;
}

.refund-proposal button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.refund-proposal button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.proposal-card {
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 1rem;
  margin-top: 1rem;
  background: #f8fafc;
}

@media (max-width: 640px) {
  .refund-proposal button {
    width: 100%;
    margin-right: 0;
  }
}
</style>