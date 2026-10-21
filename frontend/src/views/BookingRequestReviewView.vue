<template>
 <main class="review"><h1>Yêu cầu đặt lịch chờ xác nhận</h1><p>Hồ sơ đã có lần khám hoàn tất được xếp trước để duyệt. Chỗ đã giữ và hàng đợi khám được giữ nguyên.</p>
  <button type="button" :disabled="approvalBusy||busy" @click="load(page)">Tải lại</button><p v-if="error" role="alert">{{error}}</p><p role="status" aria-live="polite">{{message}}</p>
  <p v-if="!busy&&!needsReview&&!rows.length">Không có yêu cầu chờ xác nhận.</p>
  <ol><li v-for="row in rows" :key="row.id"><strong>{{row.fullName}}</strong><span v-if="row.isReturning"> · Đã từng khám</span><p>{{row.dateOfBirth}} · {{row.phone}} · {{row.appointmentDate}} {{row.slotTime.slice(0,5)}} · {{row.dentistName||'Chờ phân công nha sĩ'}}</p><p>{{row.notes}}</p><button :disabled="approvalBusy||busy||needsReview" @click="chooseApproval(row)">Đối soát / xác nhận lịch</button> <button :disabled="approvalBusy||busy||needsReview" @click="choose(row)">Từ chối yêu cầu</button></li></ol>
  <BookingRequestApproval v-if="approvingId" :key="approvingId" :request-id="approvingId" @approved="approved" @working="approvalBusy=$event" />
  <button type="button" :disabled="approvalBusy||busy||page===1" @click="load(page-1)">Trang trước</button><span>Trang {{page}}</span><button type="button" :disabled="approvalBusy||busy||!hasNext" @click="load(page+1)">Trang sau</button>
  <form v-if="selected" @submit.prevent="reject"><h2>Từ chối yêu cầu của {{selected.fullName}}</h2><p>Yêu cầu bị từ chối sẽ nhả chỗ đã giữ.</p><label>Lý do<textarea v-model="reason" rows="3" maxlength="500" :disabled="approvalBusy||busy||needsReview" required /></label><label><input v-model="confirmed" type="checkbox" :disabled="approvalBusy||busy||needsReview" /> Tôi đã đối chiếu đúng yêu cầu và lý do</label><button :disabled="approvalBusy||busy||needsReview||!confirmed">Xác nhận từ chối</button></form>
 </main>
</template>
<script setup lang="ts">
import{onMounted,onUnmounted,ref,watch}from'vue';import{useAuthStore}from'@/stores/auth';import{bookingRequestsService,type BookingRequestRow}from'@/services/bookingRequests';
import BookingRequestApproval from '@/components/BookingRequestApproval.vue';
import type {BookingApprovalResult} from '@/services/bookingRequests';
const approvingId=ref<number|null>(null),approvalBusy=ref(false);
const auth=useAuthStore(),rows=ref<BookingRequestRow[]>([]),page=ref(1),hasNext=ref(false),busy=ref(false),error=ref(''),message=ref(''),selected=ref<BookingRequestRow|null>(null),reason=ref(''),confirmed=ref(false),needsReview=ref(false);let generation=0,disposed=false;
const identity=()=>`${auth.user?.userId}/${auth.role}`,allowed=()=>['ADMIN','RECEPTIONIST'].includes(auth.role);function clear(){rows.value=[];selected.value=null;approvingId.value=null;reason.value='';confirmed.value=false;hasNext.value=false;}
function alive(t:number,o:string){return !disposed&&t===generation&&o===identity()&&allowed();}
async function load(next=1){if(disposed||approvalBusy.value||busy.value||!allowed())return;const t=++generation,o=identity();clear();busy.value=true;error.value='';message.value='';needsReview.value=true;
 try{const data=await bookingRequestsService.list(next);if(!alive(t,o))return;if(data.page!==next||data.pageSize!==20||!Array.isArray(data.items)||data.items.length>20||data.items.some(x=>!Number.isSafeInteger(x.id)||x.id<1||!Number.isSafeInteger(x.version)||x.version<1||typeof x.isReturning!=='boolean'))throw Error();rows.value=data.items;page.value=next;hasNext.value=data.hasNextPage;needsReview.value=false;}
 catch{if(alive(t,o))error.value='Không tải được danh sách yêu cầu hoặc bạn không có quyền.';}finally{if(alive(t,o))busy.value=false;}}
function choose(row:BookingRequestRow){if(disposed||approvalBusy.value||busy.value||needsReview.value||!allowed()||!rows.value.some(x=>x.id===row.id&&x.version===row.version))return;approvingId.value=null;selected.value=row;reason.value='';confirmed.value=false;message.value='';}
function chooseApproval(row:BookingRequestRow){if(disposed||approvalBusy.value||busy.value||needsReview.value||!allowed()||!rows.value.some(x=>x.id===row.id&&x.version===row.version))return;selected.value=null;reason.value='';confirmed.value=false;approvingId.value=row.id;message.value='';}
function approved(result:BookingApprovalResult){if(disposed||!allowed())return;clear();needsReview.value=true;message.value=`Đã xác nhận lịch #${result.appointmentId}. ${result.requiresPhoneActivation?'Tài khoản cần xác minh OTP lần đầu trước khi đăng nhập.':''} Tải lại danh sách để tiếp tục.`;}
async function reject(){if(disposed||approvalBusy.value||busy.value||needsReview.value||!allowed()||!selected.value||!confirmed.value||!reason.value.trim()||reason.value.length>500||reason.value.includes('\0'))return;
 const row=selected.value,note=reason.value,t=generation,o=identity();busy.value=true;error.value='';confirmed.value=false;
 try{await bookingRequestsService.reject(row.id,row.version,note);if(!alive(t,o))return;clear();message.value='Đã từ chối yêu cầu và nhả chỗ. Tải lại để tiếp tục.';needsReview.value=true;}
 catch{if(alive(t,o)){error.value='Chưa xác minh kết quả hoặc yêu cầu vừa thay đổi. Tải lại trước khi xử lý tiếp.';needsReview.value=true;reason.value='';}}
 finally{if(alive(t,o))busy.value=false;}}
watch(identity,()=>{generation++;busy.value=false;clear();error.value='';message.value='';if(!disposed)void load();});onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();message.value='';});
</script>
<style scoped>
.review{max-width:70rem;margin:1rem auto;padding:1rem;}li,form{padding:1rem;border:1px solid #cbd5e1;margin:.8rem 0;}label{display:block;margin:.8rem 0;}textarea{display:block;width:min(100%,35rem);}button,textarea{font:inherit;padding:.6rem;}[role=alert]{color:#9f1239;}
</style>
