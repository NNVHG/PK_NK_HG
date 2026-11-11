<template>
  <section class="review" aria-labelledby="review-title">
    <div class="header-banner">
      <div class="header-icon">📑</div>
      <div class="header-titles">
        <h1 id="review-title">Yêu cầu đặt lịch chờ xác nhận</h1>
        <p class="subtitle">Duyệt hồ sơ đặt khám trực tuyến. Bệnh nhân từng khám tại phòng khám được ưu tiên xếp trước.</p>
      </div>
      <button type="button" class="btn btn-outline" :disabled="approvalBusy || busy" @click="load(page)">
        🔄 Tải lại
      </button>
    </div>

    <div v-if="error" class="alert-box alert-error" role="alert">⚠️ {{ error }}</div>
    <div v-if="message" class="alert-box alert-success" role="status" aria-live="polite">✅ {{ message }}</div>
    <div v-if="busy && !rows.length" class="loading-state" role="status">
      <div class="spinner"></div>
      <p>Đang tải danh sách yêu cầu đặt lịch…</p>
    </div>
    <div v-if="!busy && !needsReview && !rows.length" class="empty-state">
      <p>Hiện không có yêu cầu đặt lịch nào đang chờ xác nhận.</p>
    </div>

    <ol v-if="rows.length" class="requests-grid" aria-label="Danh sách yêu cầu chờ xác nhận">
      <li v-for="row in rows" :key="row.id" class="request-card">
        <div class="request-heading">
          <div class="patient-main">
            <h2>{{ row.fullName }}</h2>
            <span v-if="row.isReturning" class="badge-returning">✨ Đã từng khám</span>
          </div>
          <span class="request-id">#YC-{{ row.id }}</span>
        </div>

        <dl class="details-grid">
          <div class="detail-item">
            <dt>Lịch hẹn</dt>
            <dd>
              <time :datetime="row.appointmentDate">📅 {{ row.appointmentDate }}</time>
              <span class="time-slot">⏰ <strong>{{ row.slotTime.slice(0, 5) }}</strong></span>
            </dd>
          </div>
          <div class="detail-item">
            <dt>Số điện thoại</dt>
            <dd>📞 {{ row.phone }}</dd>
          </div>
          <div class="detail-item">
            <dt>Ngày sinh</dt>
            <dd>🎂 <time :datetime="row.dateOfBirth">{{ row.dateOfBirth }}</time></dd>
          </div>
          <div class="detail-item">
            <dt>Nha sĩ yêu cầu</dt>
            <dd>👨‍⚕️ {{ row.dentistName || 'Chờ phòng khám phân công' }}</dd>
          </div>
        </dl>

        <div v-if="row.notes" class="request-notes">
          <span class="notes-label">Ghi chú của khách:</span> {{ row.notes }}
        </div>

        <div class="actions-bar">
          <button
            type="button"
            class="btn btn-primary"
            :aria-expanded="approvingId === row.id"
            :disabled="approvalBusy || busy || needsReview"
            @click="chooseApproval(row)"
          >
            📋 Đối soát & Duyệt lịch
          </button>
          <button
            type="button"
            class="btn btn-danger-outline"
            :aria-expanded="selected?.id === row.id"
            :disabled="approvalBusy || busy || needsReview"
            @click="choose(row)"
          >
            ✕ Từ chối
          </button>
        </div>

        <BookingRequestApproval
          v-if="approvingId === row.id"
          :key="row.id"
          class="approval-panel"
          :request-id="row.id"
          @approved="approved"
          @working="approvalBusy = $event"
        />

        <form v-if="selected?.id === row.id" class="rejection-form" @submit.prevent="reject">
          <h3>Từ chối yêu cầu của {{ row.fullName }}</h3>
          <p class="rejection-sub">Yêu cầu khi bị từ chối sẽ ngay lập tức giải phóng khung giờ đã giữ.</p>
          <div class="form-group">
            <label for="reject-reason">Lý do từ chối <span class="required">*</span></label>
            <textarea
              id="reject-reason"
              v-model="reason"
              rows="3"
              maxlength="500"
              placeholder="Nhập lý do cụ thể gửi đến bệnh nhân..."
              :disabled="approvalBusy || busy || needsReview"
              required
            ></textarea>
          </div>
          <label class="confirm-checkbox">
            <input v-model="confirmed" type="checkbox" :disabled="approvalBusy || busy || needsReview" />
            <span>Tôi đã đối chiếu đúng yêu cầu đặt lịch và xác nhận từ chối.</span>
          </label>
          <div class="rejection-actions">
            <button
              class="btn btn-danger"
              type="submit"
              :disabled="approvalBusy || busy || needsReview || !confirmed"
            >
              Xác nhận từ chối yêu cầu
            </button>
          </div>
        </form>
      </li>
    </ol>

    <nav v-if="rows.length || page > 1" class="pagination-bar" aria-label="Phân trang yêu cầu">
      <span class="page-info">Trang <strong>{{ page }}</strong></span>
      <div class="pagination-controls">
        <button type="button" class="btn btn-outline" :disabled="approvalBusy || busy || page === 1" @click="load(page - 1)">← Trang trước</button>
        <button type="button" class="btn btn-outline" :disabled="approvalBusy || busy || !hasNext" @click="load(page + 1)">Trang sau →</button>
      </div>
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
.review { max-width: 1040px; margin: 0 auto; padding: 1.5rem; color: #1e293b; box-sizing: border-box; }
.header-banner { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.5rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles { flex: 1; min-width: 260px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }

.requests-grid { display: flex; flex-direction: column; gap: 1rem; list-style: none; padding: 0; margin: 0 0 1.5rem; }
.request-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }
.request-heading { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.85rem; border-bottom: 1px solid #f1f5f9; padding-bottom: 0.65rem; }
.patient-main { display: flex; align-items: center; gap: 0.5rem; }
.patient-main h2 { margin: 0; font-size: 1.2rem; color: #0f172a; }
.badge-returning { background: #dcfce7; color: #15803d; font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.5rem; border-radius: 9999px; }
.request-id { font-size: 0.85rem; color: #64748b; font-weight: 600; }

.details-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.85rem; margin: 0.75rem 0; }
.detail-item dt { font-size: 0.8rem; color: #64748b; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; }
.detail-item dd { margin: 0.2rem 0 0; font-size: 0.95rem; color: #0f172a; font-weight: 500; display: flex; gap: 0.4rem; align-items: center; }
.time-slot { color: #0284c7; }

.request-notes { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.65rem 0.85rem; font-size: 0.85rem; color: #334155; margin: 0.75rem 0; }
.notes-label { font-weight: 600; color: #475569; }

.actions-bar { display: flex; gap: 0.75rem; margin-top: 1rem; padding-top: 0.85rem; border-top: 1px solid #f1f5f9; }

.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-primary:hover:not(:disabled) { background: #0369a1; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-danger { background: #dc2626; color: #fff; }
.btn-danger-outline { height: 44px; padding: 0 1rem; background: #fff; border: 1px solid #fca5a5; color: #dc2626; border-radius: 8px; font-weight: 600; cursor: pointer; }
.btn-danger-outline:hover:not(:disabled) { background: #fef2f2; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }

.approval-panel { margin-top: 1rem; padding: 1rem; background: #f0f9ff; border: 1px solid #bae6fd; border-radius: 10px; }
.rejection-form { margin-top: 1rem; padding: 1rem; background: #fef2f2; border: 1px solid #fecaca; border-radius: 10px; display: flex; flex-direction: column; gap: 0.75rem; }
.rejection-form h3 { margin: 0; font-size: 1.05rem; color: #991b1b; }
.rejection-sub { margin: 0; font-size: 0.85rem; color: #7f1d1d; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #334155; }
.required { color: #dc2626; }
textarea { width: 100%; border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.65rem; font-family: inherit; font-size: 0.95rem; box-sizing: border-box; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.85rem; color: #334155; }
.rejection-actions { display: flex; justify-content: flex-end; }

.alert-box { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.loading-state, .empty-state { text-align: center; padding: 3rem 1rem; color: #64748b; background: #f8fafc; border-radius: 12px; }
.spinner { width: 30px; height: 30px; border: 3px solid #e2e8f0; border-top-color: #0284c7; border-radius: 50%; animation: spin 0.8s linear infinite; margin: 0 auto 0.75rem; }
@keyframes spin { to { transform: rotate(360deg); } }

.pagination-bar { display: flex; justify-content: space-between; align-items: center; padding-top: 1rem; border-top: 1px solid #e2e8f0; }
.page-info { font-size: 0.875rem; color: #64748b; }
.pagination-controls { display: flex; gap: 0.5rem; }

@media (max-width: 640px) {
  .review { padding: 1rem; }
  .header-banner { flex-direction: column; align-items: flex-start; }
  .actions-bar { flex-direction: column; }
  .btn { width: 100%; }
}
</style>
