<template>
 <main class="booking">
  <div class="booking-brand">
    <RouterLink to="/" class="brand-link">
      <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="32" height="32" />
      <span>Nha Khoa Hoàng Gia</span>
    </RouterLink>
  </div>
  <RouterLink to="/login">Đăng nhập</RouterLink><h1>Đặt lịch khám</h1>
  <p>Không cần đăng nhập hoặc mã OTP để gửi yêu cầu. Yêu cầu hợp lệ giữ chỗ trong khi chờ Lễ tân xác nhận; tài khoản được xử lý khi khách đến phòng khám.</p>
  <p role="status" aria-live="polite">{{receipt?(receipt.status==='Pending'?'Đã nhận yêu cầu và giữ chỗ. Vui lòng chờ Lễ tân xác nhận lịch.':receipt.status==='Approved'?'Lễ tân đã xác nhận lịch. Liên hệ phòng khám nếu cần đối chiếu thêm.':'Yêu cầu đã bị từ chối và chỗ đã được nhả. Vui lòng liên hệ phòng khám.'):''}}</p>
  <p v-if="error" role="alert">{{error}}</p>
  <button v-if="!catalog" type="button" :disabled="busy" @click="load">Tải lại khung hẹn</button>
  <p v-if="profilesError" role="alert">{{profilesError}}</p>
  <form v-if="catalog&&!receipt" @submit.prevent="submit()">
   <fieldset :disabled="busy||!!retryBody">
    <legend>Thông tin đặt lịch</legend>
    <p v-if="patient">Đặt lịch cho tài khoản cá nhân: <strong>{{patient.fullName}}</strong> · {{patient.patientCode}}</p>
    <label>Họ và tên<input v-model="form.fullName" :readonly="patientId>0" maxlength="100" autocomplete="name" required /></label>
    <label>Số điện thoại liên hệ<input v-model="form.phone" type="tel" inputmode="numeric" maxlength="10" autocomplete="tel" required /></label>
    <label>Ngày sinh<input v-model="form.dateOfBirth" :readonly="patientId>0" type="date" :max="catalog.today" required /></label>
    <label>Giới tính<select v-model="form.gender" required><option value="">Chọn giới tính</option><option value="Male">Nam</option><option value="Female">Nữ</option><option value="Other">Khác</option></select></label>
    <label>Email (không bắt buộc)<input v-model="form.email" type="email" maxlength="100" autocomplete="email" /></label>
    <label>Ngày khám<input v-model="form.appointmentDate" type="date" :min="catalog.today" required /></label>
    <label>Giờ khám<select v-model="form.slot" required><option value="">Chọn khung giờ</option><option v-for="s in catalog.slots" :key="s">{{s}}</option></select></label>
    <label>Nội dung đặt lịch (không bắt buộc)<textarea v-model="form.notes" maxlength="500" rows="3" /></label>
   </fieldset>
   <label><input v-model="confirmed" type="checkbox" :disabled="busy||!!retryBody" required /> Tôi đã kiểm tra thông tin và đồng ý gửi yêu cầu</label>
   <button v-if="!retryBody" :disabled="busy||!confirmed">{{busy?'Đang gửi…':'Gửi yêu cầu đặt lịch'}}</button>
   <button v-if="retryBody" type="button" :disabled="busy" @click="submit">Gửi lại cùng yêu cầu để đối chiếu kết quả</button>
  </form>
  <p v-if="receipt">Mã tiếp nhận: {{receipt.requestId}}</p>
 </main>
</template>
<script setup lang="ts">
import {onMounted,onUnmounted,reactive,ref,watch} from 'vue';
import {isAxiosError} from 'axios';
import {useAuthStore} from '@/stores/auth';
import {patientsService,type PatientSummary} from '@/services/patients';
import {bookingRequestsService,type BookingCatalog,type BookingIntent,type BookingSubmission,type BookingReceipt} from '@/services/bookingRequests';
const auth=useAuthStore(),catalog=ref<BookingCatalog|null>(null),patient=ref<PatientSummary|null>(null),patientId=ref(0),profilesError=ref('');
const form=reactive({fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',appointmentDate:'',slot:'',notes:''});
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
 return{fullName:form.fullName.trim(),phone:form.phone,dateOfBirth:form.dateOfBirth,gender:form.gender,email:form.email.trim()||null,appointmentDate:form.appointmentDate,slotTime:form.slot+':00',dentistId:null,notes:form.notes.trim()||null,scheduleRevision:s.revision,patientId:patientId.value||null};
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
.booking{max-width:48rem;margin:1rem auto;padding:1rem;}
.booking-brand{margin-bottom:.75rem;}
.brand-link{display:inline-flex;align-items:center;gap:.5rem;text-decoration:none;font-weight:700;color:#164e63;}
.brand-logo{flex-shrink:0;}
fieldset{display:grid;grid-template-columns:repeat(auto-fit,minmax(16rem,1fr));gap:1rem;}label{display:flex;flex-direction:column;gap:.35rem;margin:.7rem 0;}input,select,textarea,button{font:inherit;padding:.6rem;}input[type=checkbox]{align-self:flex-start;}button{margin:.4rem;}[role=alert]{color:#9f1239;}
</style>
