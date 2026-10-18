<template>
 <section v-if="auth.role === 'ADMIN'" class="refund-panel" aria-label="Hoàn tiền và giảm công nợ">
  <h2 ref="heading" tabindex="-1">Hoàn tiền và giảm công nợ</h2>
  <p>Chọn số lượng hủy từ các dòng hóa đơn gốc. Hệ thống giảm nợ trước, chỉ hoàn phần đã thu vượt số phải thu mới. Thuốc đã giao phải được hoàn tác giao hợp lệ trước khi hoàn.</p>
  <button :disabled="busy || blocked" @click="load">Tải lại lịch sử hoàn / điều chỉnh</button>
  <p v-if="busy" role="status">Đang xử lý…</p><p v-if="error" role="alert">{{ error }}</p><p v-if="success" role="status">{{ success }}</p>
  <p v-if="pending" role="alert">Kết quả lần trước chưa rõ. Đối chiếu lịch sử hoặc kiểm tra lại cùng yêu cầu; không chi tiền thêm lần nữa.</p>
  <button v-if="pending" :disabled="busy || blocked" @click="save">Kiểm tra lại cùng yêu cầu</button>
  <form v-if="context && [2,3,5].includes(context.status) && !pending" @submit.prevent="getPreview">
   <fieldset :disabled="busy || blocked"><legend>Các dòng cần hủy</legend>
    <label v-for="item in context.items.filter(i=>i.remainingQuantity>0)" :key="item.id">
     {{ item.code }} · {{ item.name }} · Đơn giá gốc {{ money(item.unitPrice) }} · Còn {{ item.remainingQuantity }}/{{ item.originalQuantity }}
     <input v-model.number="quantities[item.id]" type="number" min="0" :max="item.remainingQuantity" step="1" :aria-label="`Số lượng hủy ${item.code}`" />
    </label>
    <label>Phương thức hoàn <select v-model="method"><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option></select></label>
    <label>Lý do <select v-model="reasonCode"><option v-for="reason in reasons" :key="reason[0]" :value="reason[0]">{{ reason[1] }}</option></select></label>
    <label>Ghi chú lý do (bắt buộc, 5–500 ký tự)<textarea v-model="notes" required minlength="5" maxlength="500" /></label>
    <button :disabled="!validSelection">Xem trước số tiền và công nợ</button>
   </fieldset>
  </form>
  <section v-if="preview && !pending" aria-label="Đối chiếu trước khi hoàn">
   <h3>Xem trước điều chỉnh</h3>
   <p>Giảm công nợ: <strong>{{ money(preview.debtReduction) }}</strong></p>
   <p>Hoàn trả khách: <strong>{{ money(preview.refundAmount) }}</strong></p>
   <p>Tổng còn lại {{ money(preview.totalAfter) }} · Giảm giá còn lại {{ money(preview.discountAfter) }} · Đã thu còn lại {{ money(preview.paidAfter) }} · Còn nợ {{ money(preview.remainingAfter) }}</p>
   <p v-if="preview.refundAmount === '0'">Chỉ điều chỉnh giảm nợ hoặc hủy phần miễn phí; không phát sinh chi tiền và không tạo giao dịch 0 đồng.</p>
   <label v-if="preview.requiresLateApproval"><input v-model="late" type="checkbox" :disabled="busy || blocked" /> Admin phê duyệt ngoại lệ quá 7 ngày kể từ lần thu dương đầu tiên, theo lý do đã ghi.</label>
   <label v-if="preview.requiresReserve"><input v-model="reserve" type="checkbox" :disabled="busy || blocked" /> Số hoàn vượt tiền mặt thu trong ngày của tôi. Xác nhận xuất quỹ dự phòng.</label>
   <label><input v-model="confirmed" type="checkbox" :disabled="busy || blocked" /> Tôi đã đối chiếu khoản hủy, số tiền và xác nhận {{ preview.refundAmount === '0' ? 'điều chỉnh công nợ' : 'chi hoàn đúng một lần theo phương thức đã chọn' }}.</label>
   <button :disabled="busy || blocked || !confirmed || preview.requiresLateApproval && !late || preview.requiresReserve && !reserve" @click="save">Xác nhận hoàn / điều chỉnh</button>
  </section>
  <section v-if="context?.history.length" aria-label="Lịch sử hoàn và giảm nợ"><h3>Lịch sử đã ghi nhận</h3>
   <ul><li v-for="record in context.history" :key="record.id">
    <strong>{{ record.voucherCode || `Điều chỉnh #${record.id} — không chi tiền` }}</strong> · {{ new Date(record.createdAt).toLocaleString('vi-VN') }} · {{ record.actorName }}
    <p>Hoàn {{ money(record.refundAmount) }} · Giảm nợ {{ money(record.debtReduction) }} · Còn nợ sau điều chỉnh {{ money(record.remainingAfter) }}</p><p>{{ record.reasonNotes }}</p>
    <button v-if="record.paymentId" :disabled="busy || blocked" @click="print(record)">Chuẩn bị phiếu chi K80</button>
   </li></ul>
  </section>
 </section>
 <Teleport to="body"><dialog ref="printDialog" v-if="voucher" class="mod-bil-refund-root" aria-label="Phiếu chi hoàn tiền" @cancel.prevent="closeVoucher" @keydown.tab="trapFocus">
  <div class="refund-print-controls"><button ref="closePrint" @click="closeVoucher">Đóng phiếu chi</button><button @click="printVoucher">In K80</button><p>Đã ghi lượt chuẩn bị in. Hủy hộp thoại hoặc lỗi máy in vẫn giữ lịch sử lượt in.</p></div>
  <article class="refund-voucher"><h2>{{ voucher.clinicName }}</h2><p>{{ voucher.clinicAddress || 'Chưa cấu hình địa chỉ' }}<br />{{ voucher.clinicPhone || 'Chưa cấu hình hotline' }}</p>
   <h3>PHIẾU CHI HOÀN TIỀN VIỆN PHÍ</h3><p>{{ voucher.record.voucherCode }} · Lượt in {{ voucher.printNumber }}</p><p>Hóa đơn gốc: {{ voucher.invoiceCode }}</p>
   <p>Bệnh nhân: {{ voucher.patientName }}<br />Admin duyệt: {{ voucher.record.actorName }}<br />Ngày chi: {{ new Date(voucher.record.createdAt).toLocaleString('vi-VN') }}</p>
   <p><strong>Số tiền chi: −{{ money(voucher.record.refundAmount) }}</strong><br />{{ voucher.record.paymentMethod === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản' }}</p>
   <p>Lý do: {{ voucher.record.reasonCode }}<br />{{ voucher.record.reasonNotes }}</p>
   <p class="refund-signatures">Thu ngân chi tiền<br /><br /><br />Bệnh nhân / Người nhận tiền<br />(Ký, ghi rõ họ tên)<br /><br /><br /></p>
  </article>
 </dialog></Teleport>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import { useInvoiceRefundStore } from '@/stores/invoiceRefunds';
import { invoiceRefundsApi, MOD_BIL_validRefundContext, MOD_BIL_validPreview, MOD_BIL_validRefundRecord, MOD_BIL_refundMoney as money, type RefundContext, type RefundPreview, type RefundRecord, type RefundVoucher } from '@/services/invoiceRefunds';
const props=defineProps<{invoiceId:number;blocked:boolean}>(),emit=defineEmits<{changed:[]}>(),auth=useAuthStore(),store=useInvoiceRefundStore();
const context=ref<RefundContext|null>(null),preview=ref<RefundPreview|null>(null),voucher=ref<RefundVoucher|null>(null),quantities=ref<Record<number,number>>({});
const busy=ref(false),error=ref(''),success=ref(''),method=ref('Cash'),reasonCode=ref('CANCEL_BEFORE_TREATMENT'),notes=ref(''),confirmed=ref(false),late=ref(false),reserve=ref(false);
const printDialog=ref<HTMLDialogElement|null>(null);
const heading=ref<HTMLElement|null>(null),closePrint=ref<HTMLButtonElement|null>(null);
const reasons=[['CANCEL_BEFORE_TREATMENT','Hủy trước điều trị'],['MEDICAL_CONTRAINDICATION','Chống chỉ định / dị ứng'],['BILLING_ERROR','Sai sót viện phí'],['PATIENT_REQUEST','Bệnh nhân yêu cầu'],['OTHER','Khác']];
const key=()=>`${auth.user?.userId}/${props.invoiceId}`,identity=()=>`${key()}/${auth.role}`;
const pending=computed(()=>store.pending[key()]);
const selection=computed(()=>Object.entries(quantities.value).filter(([,q])=>Number(q)>0).map(([id,q])=>({invoiceItemId:Number(id),quantity:Number(q)})));
const validSelection=computed(()=>selection.value.length>0 && !!context.value && Object.entries(quantities.value).every(([id,q])=>Number.isSafeInteger(q) && q>=0 && q<=(context.value!.items.find(i=>i.id===Number(id))?.remainingQuantity??-1)));
let generation=0,disposed=false;
function alive(n:number,id:string){return !disposed && generation===n && id===identity() && auth.role==='ADMIN' && !props.blocked;}
function clearPreview(){preview.value=null;confirmed.value=false;late.value=false;reserve.value=false;}
function closeVoucher(){printDialog.value?.close();voucher.value=null;document.body.classList.remove('mod-bil-refund-printing');heading.value?.focus();}
function clear(){context.value=null;quantities.value={};notes.value='';success.value='';error.value='';clearPreview();closeVoucher();}
function failure(e:unknown){return axios.isAxiosError(e)?e.response?.data?.message||'Chưa xác định kết quả. Tải lại để đối chiếu lịch sử.':'Không thể xử lý. Tải lại để đối chiếu lịch sử.';}
async function load(){
 const n=++generation,id=identity();clear();busy.value=true;
 if(auth.role!=='ADMIN' || props.blocked || !Number.isSafeInteger(props.invoiceId) || props.invoiceId<=0){busy.value=false;return;}
 try{const data=await invoiceRefundsApi.read(props.invoiceId);if(!alive(n,id))return;if(!MOD_BIL_validRefundContext(data,props.invoiceId))throw Error('Invalid refund context');context.value=data;
  quantities.value=Object.fromEntries(data.items.map(i=>[i.id,0]));if(pending.value && data.history.some(r=>r.requestId===pending.value.requestId))store.clear(key());
 }catch(e){if(alive(n,id))error.value=failure(e);}finally{if(generation===n)busy.value=false;}
}
async function getPreview(){
 if(busy.value || props.blocked || auth.role!=='ADMIN' || !validSelection.value || !context.value || pending.value)return;
 const n=++generation,id=identity();clearPreview();busy.value=true;error.value='';
 try{const result=await invoiceRefundsApi.preview(props.invoiceId,context.value.snapshot,selection.value,method.value);if(!alive(n,id))return;if(!MOD_BIL_validPreview(result))throw Error('Invalid preview');preview.value=result;}
 catch(e){if(alive(n,id))error.value=failure(e);}finally{if(generation===n)busy.value=false;}
}
async function save(){
 if(busy.value || props.blocked || auth.role!=='ADMIN')return;
 if(!pending.value && (!preview.value || !context.value || !validSelection.value || !confirmed.value || preview.value.requiresLateApproval&&!late.value || preview.value.requiresReserve&&!reserve.value || notes.value.trim().length<5 || notes.value.trim().length>500 || /[\u0000-\u001f\u007f]/.test(notes.value)))return;
 const n=++generation,id=identity(),requestKey=key();
 const request=pending.value??{snapshot:context.value!.snapshot,items:selection.value,paymentMethod:method.value,expectedPreview:preview.value!.preview,requestId:crypto.randomUUID(),reasonCode:reasonCode.value,reasonNotes:notes.value.trim(),confirmed:true,lateApproval:late.value,reserveConfirmed:reserve.value};
 store.put(requestKey,request);busy.value=true;error.value='';
 try{const result=await invoiceRefundsApi.refund(props.invoiceId,request);if(!alive(n,id))return;if(!MOD_BIL_validRefundRecord(result) || result.requestId!==request.requestId)throw Error('Invalid refund result');store.clear(requestKey);busy.value=false;await load();if(disposed || id!==identity() || props.blocked)return;success.value='Đã ghi nhận hoàn / giảm nợ. Đối chiếu lịch sử trước khi chi thêm tiền.';await nextTick();heading.value?.focus();emit('changed');}
 catch(e){if(!alive(n,id))return;if(axios.isAxiosError(e)&&[400,403,404,409].includes(e.response?.status??0))store.clear(requestKey);error.value=failure(e);clearPreview();}
 finally{if(generation===n)busy.value=false;}
}
async function print(record:RefundRecord){
 if(busy.value || props.blocked || auth.role!=='ADMIN' || !record.paymentId)return;
 const n=++generation,id=identity();busy.value=true;error.value='';
 try{const data=await invoiceRefundsApi.print(props.invoiceId,record.id);if(!alive(n,id))return;if(!MOD_BIL_validRefundRecord(data.record) || data.record.id!==record.id || data.record.requestId!==record.requestId || data.invoiceCode!==context.value?.invoiceCode || !Number.isSafeInteger(data.printNumber) || data.printNumber<1)throw Error('Invalid voucher');voucher.value=data;await nextTick();printDialog.value?.showModal();closePrint.value?.focus();}
 catch(e){if(alive(n,id))error.value=failure(e);}finally{if(generation===n)busy.value=false;}
}
function trapFocus(event:KeyboardEvent){
 const root=(event.currentTarget as HTMLElement),buttons=Array.from(root.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'));
 const first=buttons[0],last=buttons.at(-1);if(event.shiftKey && document.activeElement===first){event.preventDefault();last?.focus();}else if(!event.shiftKey && document.activeElement===last){event.preventDefault();first?.focus();}
}
function printVoucher(){if(voucher.value && auth.role==='ADMIN' && !props.blocked){document.body.classList.add('mod-bil-refund-printing');window.print();}}
watch(()=>[quantities.value,method.value,reasonCode.value,notes.value],clearPreview,{deep:true});
watch(()=>`${identity()}/${props.blocked}`,load,{immediate:true});
if(typeof window!=='undefined')window.addEventListener('afterprint',closeVoucher);
onUnmounted(()=>{disposed=true;generation++;clear();window.removeEventListener('afterprint',closeVoucher);});
</script>
<style>
.refund-panel{border-top:1px solid #bbb;margin-top:1.5rem;padding-top:1rem}.refund-panel label{display:block;margin:.8rem 0}.refund-panel input[type=number],.refund-panel select,.refund-panel textarea{display:block;padding:.5rem;max-width:100%;box-sizing:border-box}.refund-panel textarea{width:100%;min-height:5rem}.refund-panel button{padding:.6rem;margin:.4rem}.refund-panel :focus-visible{outline:3px solid #125c97;outline-offset:3px}
.mod-bil-refund-root{position:fixed;inset:0;z-index:2000;background:#eee;overflow:auto;padding:1rem;box-sizing:border-box;width:100vw;height:100vh;max-width:none;max-height:none;margin:0;border:0}.refund-voucher{background:white;color:black;width:72mm;max-width:100%;margin:auto;padding:4mm;font:13px Arial}.refund-voucher h2,.refund-voucher h3{text-align:center}.refund-voucher p{overflow-wrap:anywhere}.refund-print-controls{max-width:36rem;margin:0 auto 1rem}.refund-signatures{margin-top:2rem;text-align:center}
@page mod-bil-refund{size:80mm auto;margin:4mm}
@media print{.mod-bil-refund-root{display:none!important}body.mod-bil-refund-printing>*:not(.mod-bil-refund-root){display:none!important}body.mod-bil-refund-printing>.mod-bil-refund-root{display:block!important;position:static;padding:0;background:white;overflow:visible}body.mod-bil-refund-printing .refund-print-controls{display:none!important}body.mod-bil-refund-printing .refund-voucher{page:mod-bil-refund;width:100%;padding:0;margin:0}body.mod-bil-refund-printing{margin:0;padding:0}}
</style>
