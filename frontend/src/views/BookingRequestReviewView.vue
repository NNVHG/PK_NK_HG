<template>
  <section class="review" aria-labelledby="review-title">
    <header class="review-header">
      <div>
        <h1 id="review-title">Yêu cầu đặt lịch chờ xác nhận</h1>
        <p>Hồ sơ đã có lần khám hoàn tất được xếp trước để duyệt.</p>
      </div>
      <button type="button" class="button" :disabled="approvalBusy || busy" @click="load(page)">
        Tải lại
      </button>
    </header>

    <p v-if="error" class="notice error" role="alert">{{ error }}</p>
    <p class="feedback" role="status" aria-live="polite">{{ message }}</p>
    <p v-if="busy && !rows.length" class="notice" role="status">Đang tải yêu cầu…</p>
    <p v-if="!busy && !needsReview && !rows.length" class="notice">Không có yêu cầu chờ xác nhận.</p>

    <ol v-if="rows.length" class="requests" aria-label="Danh sách yêu cầu chờ xác nhận">
      <li v-for="row in rows" :key="row.id" class="request">
        <div class="request-heading">
          <h2>{{ row.fullName }}</h2>
          <span v-if="row.isReturning" class="returning">Đã từng khám</span>
        </div>

        <dl class="details">
          <div>
            <dt>Lịch hẹn</dt>
            <dd><time :datetime="row.appointmentDate">{{ row.appointmentDate }}</time> · {{ row.slotTime.slice(0, 5) }}</dd>
          </div>
          <div><dt>Số điện thoại</dt><dd>{{ row.phone }}</dd></div>
          <div><dt>Ngày sinh</dt><dd><time :datetime="row.dateOfBirth">{{ row.dateOfBirth }}</time></dd></div>
          <div><dt>Nha sĩ</dt><dd>{{ row.dentistName || 'Chờ phân công' }}</dd></div>
        </dl>
        <p v-if="row.notes" class="notes"><strong>Ghi chú</strong> {{ row.notes }}</p>

        <div class="actions">
          <button type="button" class="button primary"
            :aria-expanded="approvingId === row.id"
            :disabled="approvalBusy || busy || needsReview" @click="chooseApproval(row)">
            Đối soát / xác nhận lịch
          </button>
          <button type="button" class="button reject"
            :aria-expanded="selected?.id === row.id"
            :disabled="approvalBusy || busy || needsReview" @click="choose(row)">
            Từ chối yêu cầu
          </button>
        </div>

        <BookingRequestApproval v-if="approvingId === row.id" :key="row.id" class="approval-panel"
          :request-id="row.id" @approved="approved" @working="approvalBusy = $event" />

        <form v-if="selected?.id === row.id" class="rejection" @submit.prevent="reject">
          <h3>Từ chối yêu cầu của {{ row.fullName }}</h3>
          <p>Yêu cầu bị từ chối sẽ nhả chỗ đã giữ.</p>
          <label class="reason">Lý do
            <textarea v-model="reason" rows="3" maxlength="500"
              :disabled="approvalBusy || busy || needsReview" required />
          </label>
          <label class="confirmation">
            <input v-model="confirmed" type="checkbox" :disabled="approvalBusy || busy || needsReview" />
            Tôi đã đối chiếu đúng yêu cầu và lý do
          </label>
          <button class="button danger" type="submit"
            :disabled="approvalBusy || busy || needsReview || !confirmed">
            Xác nhận từ chối
          </button>
        </form>
      </li>
    </ol>

    <nav v-if="rows.length || page > 1" class="pagination" aria-label="Phân trang yêu cầu">
      <button type="button" class="button" :disabled="approvalBusy || busy || page === 1" @click="load(page - 1)">Trang trước</button>
      <span>Trang {{ page }}</span>
      <button type="button" class="button" :disabled="approvalBusy || busy || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
  </section>
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
function approved(result:BookingApprovalResult){if(disposed||!allowed())return;clear();needsReview.value=true;message.value=`Đã xác nhận lịch #${result.appointmentId}. Tài khoản sẽ được xử lý tại quầy khi khách check-in. Tải lại danh sách để tiếp tục.`;}
async function reject(){if(disposed||approvalBusy.value||busy.value||needsReview.value||!allowed()||!selected.value||!confirmed.value||!reason.value.trim()||reason.value.length>500||reason.value.includes('\0'))return;
 const row=selected.value,note=reason.value,t=generation,o=identity();busy.value=true;error.value='';confirmed.value=false;
 try{await bookingRequestsService.reject(row.id,row.version,note);if(!alive(t,o))return;clear();message.value='Đã từ chối yêu cầu và nhả chỗ. Tải lại để tiếp tục.';needsReview.value=true;}
 catch{if(alive(t,o)){error.value='Chưa xác minh kết quả hoặc yêu cầu vừa thay đổi. Tải lại trước khi xử lý tiếp.';needsReview.value=true;reason.value='';}}
 finally{if(alive(t,o))busy.value=false;}}
watch(identity,()=>{generation++;busy.value=false;clear();error.value='';message.value='';if(!disposed)void load();});onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();message.value='';});
</script>
<style scoped>
.review {
  max-width: 68rem;
  margin-inline: auto;
  color: #18354c;
  overflow-wrap: anywhere;
}
.review-header, .request-heading, .actions, .pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: .75rem;
}
.review-header { justify-content: space-between; margin-bottom: 1.5rem; }
.review-header h1 { margin: 0; font-size: clamp(1.5rem, 3vw, 2rem); color: #102e56; }
.review-header p { margin: .45rem 0 0; color: #496274; }
.requests { display: grid; gap: 1rem; list-style: none; margin: 0; padding: 0; }
.request, .notice, .feedback:not(:empty) {
  min-width: 0;
  padding: clamp(1rem, 3vw, 1.5rem);
  border: 1px solid #cddfeb;
  border-radius: .85rem;
  background: #fff;
}
.request-heading h2 { margin: 0; font-size: 1.18rem; color: #102e56; }
.returning { padding: .25rem .65rem; border-radius: 1rem; background: #d8ecfa; color: #174474; font-size: .8rem; }
.details { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 9rem), 1fr)); gap: 1rem; margin: 1.1rem 0; }
.details dt { color: #496274; font-size: .85rem; }
.details dd { margin: .3rem 0 0; font-weight: 600; }
.notes { margin-block: 1rem; white-space: pre-wrap; }
.notes strong { display: block; font-size: .85rem; }
.actions { padding-top: 1rem; border-top: 1px solid #dfeaf2; }
.button, .approval-panel :deep(button) {
  box-sizing: border-box;
  min-height: 44px;
  max-width: 100%;
  padding: .55rem .9rem;
  border: 1px solid #adc8db;
  border-radius: .55rem;
  background: #fff;
  color: #1857b6;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}
.button.primary { background: #1857b6; border-color: #1857b6; color: #fff; }
.button.reject { color: #8f2638; border-color: #e1bdc4; }
.button.danger { background: #8f2638; color: #fff; border-color: #8f2638; }
.button:disabled, .approval-panel :deep(button:disabled) { opacity: .55; cursor: default; }
.review :deep(:focus-visible) { outline: 3px solid #1857b6; outline-offset: 2px; }
.approval-panel, .rejection { margin: 1rem 0 0; padding: 1rem; border-radius: .65rem; background: #f4faff; }
.approval-panel :deep(h2), .rejection h3 { margin: 0 0 .75rem; font-size: 1.1rem; }
.approval-panel :deep(select), .rejection textarea { box-sizing: border-box; max-width: 100%; font: inherit; }
.rejection { border: 1px solid #e1bdc4; background: #fffafa; }
.reason { display: grid; gap: .4rem; font-weight: 600; }
.reason textarea { width: 100%; padding: .65rem; border: 1px solid #9ab1c1; border-radius: .4rem; resize: vertical; }
.confirmation { display: flex; align-items: baseline; gap: .5rem; margin-block: .9rem; }
.pagination { justify-content: flex-end; margin-top: 1rem; }
.notice.error { color: #84283b; border-color: #e1bdc4; background: #fff5f6; }
.feedback:empty { margin: 0; }
.feedback:not(:empty) { border-color: #a0c9b6; color: #185237; background: #f0f9f5; }
@media (max-width: 32rem) {
  .actions { align-items: stretch; flex-direction: column; }
  .pagination { justify-content: flex-start; }
}
</style>
