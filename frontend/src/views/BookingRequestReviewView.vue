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

    <Transition name="review-page-swap" mode="out-in">
      <ol v-if="rows.length" :key="'review-page-' + page" class="requests-grid" aria-label="Danh sách yêu cầu chờ xác nhận">
        <li
          v-for="row in rows"
          :key="row.id"
          class="request-card"
          :class="{ 'request-card--active': approvingId === row.id || selected?.id === row.id }"
        >
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
              📋 Đối soát &amp; Duyệt lịch
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
        </li>
      </ol>
    </Transition>

    <dialog
      v-if="approvingId !== null || selected !== null"
      :ref="bindReviewDialog"
      class="review-modal-dialog"
      :aria-labelledby="approvingId !== null ? 'review-approve-title' : 'review-reject-title'"
      @cancel="onReviewDialogCancel"
    >
      <div class="review-modal-shell">
        <header class="review-modal-header" :class="{ 'review-modal-header--danger': selected !== null }">
          <div>
            <h2 v-if="approvingId !== null" id="review-approve-title">
              Đối soát &amp; Duyệt yêu cầu #YC-{{ approvingId }}
            </h2>
            <h2 v-else-if="selected" id="review-reject-title">
              Từ chối yêu cầu #YC-{{ selected.id }}
            </h2>
            <p v-if="approvingRow" class="review-modal-sub">
              {{ approvingRow.fullName }} · 📅 {{ approvingRow.appointmentDate }} ⏰ {{ approvingRow.slotTime.slice(0, 5) }}
            </p>
            <p v-else-if="selected" class="review-modal-sub">
              {{ selected.fullName }} · 📅 {{ selected.appointmentDate }} ⏰ {{ selected.slotTime.slice(0, 5) }}
            </p>
          </div>
          <button
            type="button"
            class="review-modal-close"
            aria-label="Đóng hộp thoại"
            :disabled="approvalBusy || busy"
            @click="closeReviewModal"
          >
            ✕
          </button>
        </header>

        <div class="review-modal-body">
          <BookingRequestApproval
            v-if="approvingId !== null"
            :key="approvingId"
            class="approval-panel"
            :request-id="approvingId"
            @approved="approved"
            @working="approvalBusy = $event"
          />

          <form v-if="selected" class="rejection-form" @submit.prevent="reject">
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
                autofocus
                required
              ></textarea>
            </div>
            <label class="confirm-checkbox">
              <input v-model="confirmed" type="checkbox" :disabled="approvalBusy || busy || needsReview" />
              <span>Tôi đã đối chiếu đúng yêu cầu đặt lịch và xác nhận từ chối.</span>
            </label>
            <div class="rejection-actions">
              <button
                type="button"
                class="btn btn-outline"
                :disabled="approvalBusy || busy"
                @click="closeReviewModal"
              >
                Hủy
              </button>
              <button
                class="btn btn-danger"
                type="submit"
                :disabled="approvalBusy || busy || needsReview || !confirmed"
              >
                Xác nhận từ chối yêu cầu
              </button>
            </div>
          </form>
        </div>

        <footer v-if="approvingId !== null" class="review-modal-footer">
          <button
            type="button"
            class="btn btn-outline"
            :disabled="approvalBusy || busy"
            @click="closeReviewModal"
          >
            Đóng hộp thoại
          </button>
        </footer>
      </div>
    </dialog>

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
import{computed,onMounted,onUnmounted,ref,watch}from'vue';import{useAuthStore}from'@/stores/auth';import{bookingRequestsService,type BookingRequestRow}from'@/services/bookingRequests';
import BookingRequestApproval from '@/components/BookingRequestApproval.vue';
import type {BookingApprovalResult} from '@/services/bookingRequests';
const approvingId=ref<number|null>(null),approvalBusy=ref(false);
const auth=useAuthStore(),rows=ref<BookingRequestRow[]>([]),page=ref(1),hasNext=ref(false),busy=ref(false),error=ref(''),message=ref(''),selected=ref<BookingRequestRow|null>(null),reason=ref(''),confirmed=ref(false),needsReview=ref(false);let generation=0,disposed=false;
let reviewDialogEl:HTMLDialogElement|null=null;
const approvingRow=computed(()=>rows.value.find(r=>r.id===approvingId.value)??null);
const identity=()=>`${auth.user?.userId}/${auth.role}`,allowed=()=>['ADMIN','RECEPTIONIST'].includes(auth.role);
function bindReviewDialog(element:unknown){if(typeof HTMLDialogElement!=='undefined'&&element instanceof HTMLDialogElement){reviewDialogEl=element;if(!element.open)element.showModal();}else if(element===null){reviewDialogEl=null;}}
function closeReviewModal(){if(approvalBusy.value||busy.value)return;reviewDialogEl?.close();approvingId.value=null;selected.value=null;reason.value='';confirmed.value=false;}
function onReviewDialogCancel(event:Event){if(event.target!==reviewDialogEl)return;event.preventDefault();closeReviewModal();}
function clear(){reviewDialogEl?.close();rows.value=[];selected.value=null;approvingId.value=null;reason.value='';confirmed.value=false;hasNext.value=false;}
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
watch(identity,()=>{generation++;busy.value=false;clear();error.value='';message.value='';if(!disposed)void load();});onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;reviewDialogEl?.close();reviewDialogEl=null;clear();message.value='';});
</script>
<style scoped>
.review { max-width: 1040px; margin: 0 auto; padding: 1.5rem; color: #1e293b; box-sizing: border-box; }
.header-banner { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.5rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles { flex: 1; min-width: 260px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }

.requests-grid { display: flex; flex-direction: column; gap: 1rem; list-style: none; padding: 0; margin: 0 0 1.5rem; }
.request-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.25rem; box-shadow: 0 1px 3px rgba(0,0,0,0.04); transition: border-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease; }
.request-card:hover { border-color: #cbd5e1; box-shadow: 0 6px 18px rgba(15, 23, 42, 0.06); transform: translateY(-1px); }
.request-card--active { border-color: #0284c7; box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.16); }
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

.approval-panel { margin-top: 0; }
.rejection-form { display: flex; flex-direction: column; gap: 0.75rem; }
.rejection-sub { margin: 0; font-size: 0.85rem; color: #7f1d1d; background: #fef2f2; border: 1px solid #fecaca; border-radius: 8px; padding: 0.65rem 0.85rem; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #334155; }
.required { color: #dc2626; }
textarea { width: 100%; border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.65rem; font-family: inherit; font-size: 0.95rem; box-sizing: border-box; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.85rem; color: #334155; }
.rejection-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }

.review-modal-dialog {
  box-sizing: border-box;
  margin: auto;
  padding: 0;
  border: 1px solid #dbe5ed;
  border-radius: 16px;
  width: min(700px, calc(100% - 1.25rem));
  max-height: calc(100dvh - 1.5rem);
  overflow: hidden;
  background: #ffffff;
  color: #1e293b;
  box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
}
.review-modal-dialog:modal { overscroll-behavior: contain; }
.review-modal-dialog[open] { animation: reviewDialogPop 0.22s cubic-bezier(0.22, 1, 0.36, 1) both; }
.review-modal-dialog::backdrop {
  background: rgba(15, 23, 42, 0.56);
  backdrop-filter: blur(4px);
  overflow: hidden;
  overscroll-behavior: contain;
}
.review-modal-shell {
  display: flex;
  flex-direction: column;
  max-height: calc(100dvh - 1.5rem);
  min-width: 0;
}
.review-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid #e2e8f0;
  background: #f8fafc;
}
.review-modal-header--danger { background: #fff5f5; border-bottom-color: #fecaca; }
.review-modal-header h2 { margin: 0; font-size: 1.15rem; color: #0f2d59; }
.review-modal-header--danger h2 { color: #991b1b; }
.review-modal-sub { margin: 0.2rem 0 0; font-size: 0.85rem; color: #64748b; }
.review-modal-close {
  flex: 0 0 40px;
  min-width: 40px;
  min-height: 40px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #ffffff;
  color: #334155;
  font-size: 1rem;
  font-weight: 700;
  cursor: pointer;
}
.review-modal-close:hover:not(:disabled) { background: #f1f5f9; }
.review-modal-close:disabled { opacity: 0.55; cursor: not-allowed; }
.review-modal-body { flex: 1 1 auto; min-height: 0; overflow-y: auto; overscroll-behavior: contain; padding: 1.25rem; }
.review-modal-footer { display: flex; justify-content: flex-end; padding: 0.85rem 1.25rem; border-top: 1px solid #e2e8f0; background: #f8fafc; }

@keyframes reviewDialogPop {
  from { opacity: 0; transform: translateY(10px) scale(0.97); }
  to { opacity: 1; transform: translateY(0) scale(1); }
}

.review-page-swap-enter-active,
.review-page-swap-leave-active {
  transition: opacity 0.2s ease, transform 0.2s cubic-bezier(0.22, 1, 0.36, 1);
}
.review-page-swap-enter-from { opacity: 0; transform: translateX(10px); }
.review-page-swap-leave-to { opacity: 0; transform: translateX(-10px); }

@media (prefers-reduced-motion: reduce) {
  .review-modal-dialog[open] { animation: none; }
  .review-page-swap-enter-active,
  .review-page-swap-leave-active,
  .request-card { transition: none; }
}

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
