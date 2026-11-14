<template>
  <div class="booking-page">
    <header class="booking-header">
      <div class="header-inner">
        <RouterLink to="/" class="brand-link" aria-label="Nha khoa Hoàng Gia - Trang chủ">
          <img src="/logo-icon.svg" alt="" class="brand-logo" width="36" height="36" />
          <span>Nha khoa <strong>Hoàng Gia</strong></span>
        </RouterLink>
        <nav class="header-actions" aria-label="Điều hướng công khai">
          <RouterLink to="/" class="header-link">Trang chủ</RouterLink>
          <RouterLink to="/login" class="login-link">Đăng nhập</RouterLink>
        </nav>
      </div>
    </header>

    <section class="booking-content" aria-labelledby="booking-title">
      <div class="intro">
        <RouterLink to="/" class="back-link">← Về trang chủ</RouterLink>
        <p class="eyebrow">Nha khoa Hoàng Gia</p>
        <h1 id="booking-title">Đặt lịch khám</h1>
        <p class="intro-copy">Chọn ngày và khung giờ phù hợp. Không cần đăng nhập hoặc mã OTP để gửi yêu cầu.</p>
      </div>

      <div class="booking-layout">
        <div class="form-card">
          <div class="card-heading">
            <div><p class="card-kicker">Yêu cầu khám</p><h2>Thông tin đặt lịch</h2></div>
            <span class="step-label">Trực tuyến</span>
          </div>

          <div class="card-body">
            <p class="status-message" :class="{ 'status-visible': receipt }" role="status" aria-live="polite">{{receipt?(receipt.status==='Pending'?'Đã nhận yêu cầu và giữ chỗ. Vui lòng chờ Lễ tân xác nhận lịch.':receipt.status==='Approved'?'Lễ tân đã xác nhận lịch. Liên hệ phòng khám nếu cần đối chiếu thêm.':'Yêu cầu đã bị từ chối và chỗ đã được nhả. Vui lòng liên hệ phòng khám.'):''}}</p>
            <p v-if="error" class="alert-message" role="alert">{{error}}</p>
            <p v-if="profilesError" class="alert-message" role="alert">{{profilesError}}</p>
            <p v-if="busy && !catalog" class="loading-message" role="status">Đang tải khung hẹn…</p>
            <button v-if="!catalog" class="secondary-button" type="button" :disabled="busy" @click="load">Tải lại khung hẹn</button>

            <form v-if="catalog&&!receipt" @submit.prevent="submit()">
              <fieldset :disabled="busy||!!retryBody">
                <legend class="sr-only">Thông tin đặt lịch</legend>
                <p v-if="patient" class="patient-notice">Đặt lịch cho tài khoản cá nhân: <strong>{{patient.fullName}}</strong> · {{patient.patientCode}}</p>
                <div class="field-grid">
                  <label class="field field-wide"><span>Họ và tên <span class="required" aria-hidden="true">*</span></span><input v-model="form.fullName" :readonly="patientId>0" maxlength="100" autocomplete="name" required /></label>
                  <label class="field"><span>Số điện thoại liên hệ <span class="required" aria-hidden="true">*</span></span><input v-model="form.phone" type="tel" inputmode="numeric" maxlength="10" autocomplete="tel" required /><span class="field-hint">Phòng khám dùng số này để liên hệ khi cần.</span></label>
                  <label class="field"><span>Ngày sinh <span class="required" aria-hidden="true">*</span></span><input v-model="form.dateOfBirth" :readonly="patientId>0" type="date" :max="catalog.today" required /></label>
                  <label class="field"><span>Giới tính <span class="required" aria-hidden="true">*</span></span><select v-model="form.gender" required><option value="">Chọn giới tính</option><option value="Male">Nam</option><option value="Female">Nữ</option><option value="Other">Khác</option></select></label>
                  <label class="field"><span>Email <span class="optional">(không bắt buộc)</span></span><input v-model="form.email" type="email" maxlength="100" autocomplete="email" /></label>
                </div>

                <div v-if="isChild" class="guardian-section">
                  <div class="child-badge-notice">
                    <span>👶 <strong>Đối tượng: Trẻ em (&lt; 14 tuổi)</strong></span>
                    <p class="field-hint">Theo quy định (§1.6), trẻ dưới 14 tuổi không yêu cầu CCCD. Vui lòng cung cấp thông tin người giám hộ đi cùng.</p>
                  </div>
                  <div class="field-grid">
                    <label class="field"><span>Họ tên người giám hộ <span class="required" aria-hidden="true">*</span></span><input v-model="guardianForm.fullName" maxlength="100" placeholder="Họ tên cha/mẹ/người giám hộ" :required="isChild" /></label>
                    <label class="field"><span>Quan hệ với trẻ <span class="required" aria-hidden="true">*</span></span><select v-model="guardianForm.relationship" :required="isChild"><option value="Bố/Mẹ">Bố/Mẹ</option><option value="Ông/Bà">Ông/Bà</option><option value="Người giám hộ hợp pháp">Người giám hộ hợp pháp</option></select></label>
                  </div>
                </div>

                <div class="section-divider"></div>
                <div class="section-heading"><div><p class="card-kicker">Thời gian phù hợp</p><h3>Chọn lịch khám</h3></div></div>
                <div class="field-grid">
                  <label class="field"><span>Ngày khám <span class="required" aria-hidden="true">*</span></span><input v-model="form.appointmentDate" type="date" :min="catalog.today" required /></label>
                  <label class="field"><span>Giờ khám <span class="required" aria-hidden="true">*</span></span><select v-model="form.slot" required><option value="">Chọn khung giờ</option><option v-for="s in catalog.slots" :key="s">{{s}}</option></select></label>
                  <label class="field field-wide"><span>Nội dung đặt lịch <span class="optional">(không bắt buộc)</span></span><textarea v-model="form.notes" maxlength="500" rows="3" placeholder="Bạn có thể ghi chú thêm cho phòng khám" /></label>
                </div>
              </fieldset>

              <div class="form-footer">
                <label class="consent"><input v-model="confirmed" type="checkbox" :disabled="busy||!!retryBody" required /><span>Tôi đã kiểm tra thông tin và đồng ý gửi yêu cầu</span></label>
                <button v-if="!retryBody" class="primary-button" :disabled="busy||!confirmed">{{busy?'Đang gửi…':'Gửi yêu cầu đặt lịch'}}</button>
                <button v-if="retryBody" class="primary-button" type="button" :disabled="busy" @click="submit">Gửi lại cùng yêu cầu để đối chiếu kết quả</button>
              </div>
            </form>

            <div v-if="receipt" class="receipt-card">
              <p class="receipt-label">Mã tiếp nhận</p><p class="receipt-code">{{receipt.requestId}}</p>
              <RouterLink to="/" class="secondary-link">Về trang chủ</RouterLink>
            </div>
          </div>
        </div>

        <aside class="booking-aside" aria-label="Thông tin lịch hẹn">
          <div v-if="catalog&&!receipt" class="summary-card">
            <h2>Tóm tắt lịch hẹn</h2>
            <p>Kiểm tra lại trước khi gửi yêu cầu.</p>
            <dl>
              <div><dt>Ngày khám</dt><dd>{{form.appointmentDate ? form.appointmentDate.split('-').reverse().join('/') : 'Chưa chọn'}}</dd></div>
              <div><dt>Giờ khám</dt><dd>{{form.slot || 'Chưa chọn'}}</dd></div>
            </dl>
          </div>
          <div class="info-card">
            <span class="info-icon" aria-hidden="true">i</span>
            <div><h2>Trước khi đến khám</h2><p>Yêu cầu hợp lệ sẽ giữ chỗ trong khi chờ Lễ tân xác nhận. Phòng khám sẽ sắp xếp nha sĩ phù hợp.</p><p>Khi đến, vui lòng mang CCCD hoặc hộ chiếu để đối chiếu thông tin.</p></div>
          </div>
        </aside>
      </div>
    </section>
  </div>
</template>
<script setup lang="ts">
import {computed,onMounted,onUnmounted,reactive,ref,watch} from 'vue';
import {isAxiosError} from 'axios';
import {useAuthStore} from '@/stores/auth';
import {patientsService,type PatientSummary} from '@/services/patients';
import {bookingRequestsService,type BookingCatalog,type BookingIntent,type BookingSubmission,type BookingReceipt} from '@/services/bookingRequests';
const auth=useAuthStore(),catalog=ref<BookingCatalog|null>(null),patient=ref<PatientSummary|null>(null),patientId=ref(0),profilesError=ref('');
const form=reactive({fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',appointmentDate:'',slot:'',notes:''});
const guardianForm=reactive({fullName:'',relationship:'Bố/Mẹ'});
const isChild=computed(()=>{
 if(!form.dateOfBirth||!/^\d{4}-\d{2}-\d{2}$/.test(form.dateOfBirth))return false;
 const s=catalog.value?.today||new Date().toISOString().slice(0,10);
 const birth=new Date(form.dateOfBirth+'T00:00:00Z');
 const refDay=new Date(s+'T00:00:00Z');
 let age=refDay.getUTCFullYear()-birth.getUTCFullYear();
 const m=refDay.getUTCMonth()-birth.getUTCMonth();
 if(m<0||(m===0&&refDay.getUTCDate()<birth.getUTCDate()))age--;
 return age<14;
});
const busy=ref(false),confirmed=ref(false),error=ref(''),receipt=ref<BookingReceipt|null>(null),retryBody=ref<BookingSubmission|null>(null);let snapshot:BookingIntent|null=null,generation=0,disposed=false;
const identity=()=>`${auth.user?.userId}/${auth.role}`;
function alive(ticket:number,owner:string){return !disposed&&ticket===generation&&owner===identity();}
function clear(){catalog.value=null;patient.value=null;patientId.value=0;Object.assign(form,{fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',appointmentDate:'',slot:'',notes:''});snapshot=null;retryBody.value=null;receipt.value=null;confirmed.value=false;error.value='';profilesError.value='';}
async function load(){
 const ticket=++generation,owner=identity();clear();busy.value=true;
 try{const data=await bookingRequestsService.catalog();if(!alive(ticket,owner))return;
  if(!/^\d{4}-\d{2}-\d{2}$/.test(data.today)||!Array.isArray(data.slots)||!data.slots.length||data.slots.some(s=>!/^\d{2}:\d{2}$/.test(s))||!/^[A-F0-9]{64}$/.test(data.revision))throw Error();
  catalog.value=data;form.appointmentDate=data.today;
  if(auth.role==='PATIENT'){try{const mine=await patientsService.getMyPatient();if(alive(ticket,owner)){patient.value=mine;patientId.value=mine.patientId;Object.assign(form,{fullName:mine.fullName,phone:mine.phone,dateOfBirth:mine.dateOfBirth,gender:mine.gender});}}catch{if(alive(ticket,owner))profilesError.value='Không tải được hồ sơ cá nhân. Tải lại trước khi tiếp tục.';}}
 }catch{if(alive(ticket,owner))error.value='Không tải được khung hẹn. Vui lòng thử lại.';}
 finally{if(alive(ticket,owner))busy.value=false;}
}
function validDay(value:string){const d=new Date(value+'T00:00:00Z');return /^\d{4}-\d{2}-\d{2}$/.test(value)&&Number.isFinite(d.getTime())&&d.toISOString().slice(0,10)===value;}
function intent():BookingIntent|null{
 const s=catalog.value;if(!s)return null;const minBirth=new Date(s.today+'T00:00:00Z'),month=minBirth.getUTCMonth();minBirth.setUTCFullYear(minBirth.getUTCFullYear()-120);if(minBirth.getUTCMonth()!==month)minBirth.setUTCDate(0);
 if(!form.fullName.trim()||form.fullName.length>100||form.fullName.includes('\0')||!/^0\d{9}$/.test(form.phone)||!validDay(form.dateOfBirth)||form.dateOfBirth<minBirth.toISOString().slice(0,10)||form.dateOfBirth>s.today||!['Male','Female','Other'].includes(form.gender)||!validDay(form.appointmentDate)||form.appointmentDate<s.today||!s.slots.includes(form.slot)||form.notes.length>500||form.notes.includes('\0')||form.email.length>100||form.email&&!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)||patientId.value>0&&(!patient.value||patient.value.patientId!==patientId.value||patient.value.fullName!==form.fullName.trim()||patient.value.dateOfBirth!==form.dateOfBirth))return null;
 if(isChild.value&&(!guardianForm.fullName.trim()||guardianForm.fullName.length>100))return null;
 let combinedNotes=form.notes.trim()||'';
 if(isChild.value){
   const guardianNote=`[Giám hộ: ${guardianForm.fullName.trim()} - ${guardianForm.relationship}]`;
   combinedNotes=combinedNotes?`${combinedNotes} ${guardianNote}`:guardianNote;
   if(combinedNotes.length>500)combinedNotes=combinedNotes.slice(0,500);
 }
 return{fullName:form.fullName.trim(),phone:form.phone,dateOfBirth:form.dateOfBirth,gender:form.gender,email:form.email.trim()||null,appointmentDate:form.appointmentDate,slotTime:form.slot+':00',dentistId:null,notes:combinedNotes||null,scheduleRevision:s.revision,patientId:patientId.value||null};
}
async function submit(){
 if(disposed||busy.value||receipt.value)return;
 if(!retryBody.value){const payload=intent();if(!confirmed.value||!payload){error.value='Kiểm tra lại thông tin cá nhân, ngày và khung giờ.';return;}snapshot=payload;retryBody.value={intent:payload,clientRequestId:crypto.randomUUID(),confirmed:true};}
 const body=retryBody.value,ticket=generation,owner=identity();busy.value=true;error.value='';
 try{const data=await bookingRequestsService.submit(body);if(!alive(ticket,owner))return;if(data.requestId!==body.clientRequestId||!['Pending','Rejected','Approved'].includes(data.status))throw Error();receipt.value=data;retryBody.value=null;snapshot=null;confirmed.value=false;Object.assign(form,{fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',notes:''});patient.value=null;patientId.value=0;}
 catch(cause){if(alive(ticket,owner)){
  const status=isAxiosError(cause)?cause.response?.status:undefined;
  if(status&&[400,401,403,404,409].includes(status)){retryBody.value=null;snapshot=null;confirmed.value=false;catalog.value=null;error.value='Yêu cầu chưa được nhận: thông tin hoặc chỗ hẹn không còn hợp lệ. Tải lại khung hẹn trước khi gửi yêu cầu mới.';}
  else error.value='Chưa xác minh kết quả. Gửi lại cùng yêu cầu để đối chiếu; không tạo yêu cầu khác khi chưa rõ kết quả.';
 }}
 finally{if(alive(ticket,owner))busy.value=false;}
}
watch(identity,()=>{generation++;busy.value=false;clear();if(!disposed)void load();});onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();});
</script>
<style scoped>
.guardian-section{margin-top:16px;padding:14px;background:#f0f7ff;border:1px solid #cce3f8;border-radius:10px}
.child-badge-notice{margin-bottom:12px;color:#1857b6}
.child-badge-notice strong{font-size:.95rem}
.booking-page{min-height:100vh;background:var(--hg-canvas,#f4f8fc);color:var(--hg-ink,#18354c);font-family:var(--hg-font,"Segoe UI",system-ui,sans-serif)}
.booking-header{background:#fff;border-bottom:1px solid #e4edf5}
.header-inner{max-width:1160px;min-height:76px;margin:0 auto;padding:12px 24px;display:flex;align-items:center;justify-content:space-between;gap:20px;box-sizing:border-box}
.brand-link{display:inline-flex;align-items:center;gap:10px;text-decoration:none;color:var(--hg-navy,#102e56);font-size:1.05rem;line-height:1.2}.brand-link span{display:flex;flex-direction:column;font-size:.75rem}.brand-link strong{font-size:1.12rem}.brand-logo{flex-shrink:0}
.header-actions{display:flex;align-items:center;gap:18px}.header-link,.back-link{font-size:.9rem;font-weight:600;text-decoration:none;color:#35556f}.login-link,.secondary-link{color:var(--hg-primary,#1857b6);font-weight:700;text-decoration:none}.login-link{border:1px solid #c8deeb;border-radius:9px;padding:9px 14px;white-space:nowrap}
.booking-content{max-width:1160px;margin:0 auto;padding:30px 24px 80px;box-sizing:border-box}.intro{margin-bottom:26px}.back-link{display:inline-block;margin-bottom:23px;color:var(--hg-primary,#1857b6)}.eyebrow,.card-kicker{margin:0 0 6px;font-size:.75rem;font-weight:750;letter-spacing:.07em;text-transform:uppercase;color:var(--hg-primary,#1857b6)}h1,h2,h3,p{margin-top:0}h1,h2,h3{color:var(--hg-navy,#102e56)}h1{font-size:clamp(1.75rem,3vw,2.35rem);line-height:1.2;margin-bottom:9px}.intro-copy{color:#4a6176;line-height:1.6;max-width:660px;margin-bottom:0}
.booking-layout{display:grid;grid-template-columns:minmax(0,1fr) minmax(250px,320px);align-items:start;gap:24px}.form-card,.summary-card{background:#fff;border:1px solid #e0ebf4;border-radius:16px;box-shadow:0 4px 18px rgba(16,46,86,.05);min-width:0}.card-heading{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:22px 26px;border-bottom:1px solid #edf2f7}.card-heading h2,.summary-card h2,.info-card h2{font-size:1.08rem;line-height:1.35;margin:0}.step-label{flex-shrink:0;border-radius:999px;background:#edf6fc;color:#1857b6;font-size:.75rem;font-weight:700;padding:6px 10px}.card-body{padding:24px 26px 28px}fieldset{min-width:0;margin:0;padding:0;border:0}fieldset:disabled{opacity:.72}.field-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px 20px}.field{display:flex;flex-direction:column;gap:8px;min-width:0;font-size:.9rem;font-weight:650;color:#18354c}.field-wide{grid-column:1/-1}.required{color:#b42318}.optional,.field-hint{font-weight:400;color:#536a7e}.field-hint{font-size:.78rem;line-height:1.4}
.field input,.field select,.field textarea{box-sizing:border-box;display:block;width:100%;min-width:0;min-height:46px;border:1px solid #c8d9e7;border-radius:9px;padding:10px 12px;background:#fff;color:#18354c;font:inherit;font-size:1rem}.field textarea{resize:vertical;line-height:1.5}.field input[readonly]{background:#f4f7fa;color:#445d72}.field input::placeholder,.field textarea::placeholder{color:#687e90}.section-divider{height:1px;background:#e9eff5;margin:26px 0}.section-heading{margin-bottom:18px}.section-heading h3{font-size:1rem;margin:0}.patient-notice,.loading-message{padding:12px 14px;background:#eef7fd;border-radius:9px;line-height:1.5;color:#214e72}.patient-notice{margin:0 0 20px;overflow-wrap:anywhere}
.form-footer{border-top:1px solid #e9eff5;margin-top:26px;padding-top:22px}.consent{display:flex;align-items:flex-start;gap:10px;font-size:.9rem;line-height:1.5;color:#29465d}.consent input{width:18px;height:18px;margin:2px 0 0;flex:none;accent-color:#1857b6}.primary-button,.secondary-button{min-height:48px;border:0;border-radius:9px;padding:11px 20px;font:inherit;font-weight:750;cursor:pointer}.primary-button{margin-top:18px;background:var(--hg-primary,#1857b6);color:#fff}.secondary-button{background:#eaf3fb;color:#1857b6}.primary-button:hover:not(:disabled){background:#144c9e}.primary-button:disabled,.secondary-button:disabled{opacity:.55;cursor:not-allowed}.alert-message{color:#9b1c1c;background:#fef2f2;border:1px solid #fecaca;border-radius:9px;padding:12px 14px;line-height:1.5}.status-message:empty{display:none}.status-visible{color:#145c38;background:#e9f8ef;border:1px solid #b8e7c9;border-radius:9px;padding:12px 14px;line-height:1.5}.receipt-card{padding-top:4px}.receipt-label{font-size:.85rem;color:#536a7e;margin-bottom:4px}.receipt-code{font-weight:700;overflow-wrap:anywhere;color:#102e56}
.booking-aside{display:grid;gap:18px}.summary-card{padding:22px}.summary-card>p{color:#536a7e;font-size:.88rem;line-height:1.5;margin:8px 0 18px}.summary-card dl{margin:0}.summary-card dl div{display:flex;justify-content:space-between;gap:12px;padding:12px 0;border-top:1px solid #edf2f7}.summary-card dt{color:#536a7e}.summary-card dd{margin:0;text-align:right;font-weight:700;color:#102e56}.info-card{display:flex;gap:12px;background:#eaf5fc;border:1px solid #cfe6f5;border-radius:14px;padding:20px;color:#31546f}.info-icon{display:grid;place-items:center;flex:none;width:24px;height:24px;border-radius:50%;background:#1857b6;color:white;font-weight:750}.info-card h2{margin-bottom:9px}.info-card p{font-size:.88rem;line-height:1.55;margin:0 0 10px}.info-card p:last-child{margin:0}
.sr-only{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0}a:focus-visible,button:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:3px solid #e5b95e;outline-offset:3px}
@media(max-width:760px){.booking-layout{grid-template-columns:minmax(0,1fr)}.booking-aside{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:540px){.header-inner{min-height:68px;padding:12px 16px;gap:10px}.header-actions{gap:10px}.header-link{display:none}.booking-content{padding:24px 16px 56px}.back-link{margin-bottom:18px}.intro{margin-bottom:20px}.card-heading{padding:19px 18px}.card-body{padding:20px 18px 24px}.field-grid{grid-template-columns:minmax(0,1fr);gap:16px}.field-wide{grid-column:auto}.booking-aside{grid-template-columns:minmax(0,1fr)}.primary-button{width:100%}}
</style>
