<template>
 <main class="booking">
  <RouterLink to="/login">Đăng nhập</RouterLink><h1>Gửi yêu cầu đặt lịch</h1>
  <p>Yêu cầu hợp lệ giữ chỗ trong khi chờ Lễ tân xác nhận. Tài khoản mới được hướng dẫn sau khi lịch được xác nhận.</p>
  <p role="status" aria-live="polite">{{receipt?(receipt.status==='Pending'?'Đã nhận yêu cầu và giữ chỗ. Vui lòng chờ Lễ tân xác nhận lịch.':receipt.status==='Approved'?'Lễ tân đã xác nhận lịch. Liên hệ phòng khám nếu cần đối chiếu thêm.':'Yêu cầu đã bị từ chối và chỗ đã được nhả. Vui lòng liên hệ phòng khám.'):''}}</p>
  <p v-if="error" role="alert">{{error}}</p>
  <button v-if="!catalog" type="button" :disabled="busy" @click="load">Tải lại khung hẹn</button>
  <p v-if="profilesError" role="alert">{{profilesError}}</p>
  <form v-if="catalog&&!receipt" @submit.prevent="challenge?submit():requestOtp()">
   <fieldset :disabled="busy||!!challenge||!!retryBody">
    <legend>Thông tin đặt lịch</legend>
    <label v-if="auth.role==='PATIENT'">Hồ sơ đã liên kết<select v-model.number="patientId" @change="chooseProfile"><option :value="0">Nhập thông tin khách mới</option><option v-for="p in profiles" :key="p.patientId" :value="p.patientId">{{p.fullName}} · {{p.dateOfBirth}}</option></select></label>
    <label>Họ và tên<input v-model="form.fullName" :readonly="patientId>0" maxlength="100" autocomplete="name" required /></label>
    <label>SĐT nhận mã xác thực<input v-model="form.phone" type="tel" inputmode="numeric" maxlength="10" autocomplete="tel" required /></label>
    <label>Ngày sinh<input v-model="form.dateOfBirth" :readonly="patientId>0" type="date" :max="catalog.today" required /></label>
    <label>Giới tính<select v-model="form.gender" required><option value="">Chọn giới tính</option><option value="Male">Nam</option><option value="Female">Nữ</option><option value="Other">Khác</option></select></label>
    <label>Email (không bắt buộc)<input v-model="form.email" type="email" maxlength="100" autocomplete="email" /></label>
    <label>Ngày khám<input v-model="form.appointmentDate" type="date" :min="catalog.today" required /></label>
    <label>Giờ khám<select v-model="form.slot" required><option value="">Chọn khung giờ</option><option v-for="s in catalog.slots" :key="s">{{s}}</option></select></label>
    <label>Nha sĩ<select v-model.number="form.dentistId"><option :value="0">Để phòng khám phân công</option><option v-for="d in catalog.dentists" :key="d.dentistId" :value="d.dentistId">{{d.fullName}}</option></select></label>
    <label>Nội dung đặt lịch (không bắt buộc)<textarea v-model="form.notes" maxlength="500" rows="3" /></label>
   </fieldset>
   <label v-if="challenge">Mã xác thực 6 số<input v-model="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="busy||!!retryBody" required /></label>
   <p v-if="challenge">Mã có hiệu lực 5 phút. Thay đổi thông tin cần xin mã mới sau ít nhất 60 giây.</p>
   <label><input v-model="confirmed" type="checkbox" :disabled="busy||!!retryBody" /> Tôi đã kiểm tra thông tin và đồng ý gửi yêu cầu</label>
   <button v-if="!retryBody" :disabled="busy||!confirmed">{{busy?'Đang xử lý…':challenge?'Gửi yêu cầu giữ chỗ':'Nhận mã xác thực'}}</button>
   <button v-if="retryBody" type="button" :disabled="busy" @click="submit">Gửi lại cùng yêu cầu để đối chiếu kết quả</button>
   <button v-if="challenge&&!retryBody" type="button" :disabled="busy" @click="resetChallenge">Sửa thông tin / xin mã mới</button>
  </form>
  <p v-if="receipt">Mã tiếp nhận: {{receipt.requestId}}</p>
 </main>
</template>
<script setup lang="ts">
import {onMounted,onUnmounted,reactive,ref,watch} from 'vue';
import {isAxiosError} from 'axios';
import {useAuthStore} from '@/stores/auth';
import {patientsService,type PatientSummary} from '@/services/patients';
import {bookingRequestsService,type BookingCatalog,type BookingIntent,type BookingChallenge,type BookingSubmission,type BookingReceipt} from '@/services/bookingRequests';
const auth=useAuthStore(),catalog=ref<BookingCatalog|null>(null),profiles=ref<PatientSummary[]>([]),patientId=ref(0),profilesError=ref('');
const form=reactive({fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',appointmentDate:'',slot:'',dentistId:0,notes:''});
const busy=ref(false),confirmed=ref(false),error=ref(''),challenge=ref<BookingChallenge|null>(null),code=ref(''),receipt=ref<BookingReceipt|null>(null),retryBody=ref<BookingSubmission|null>(null);let snapshot:BookingIntent|null=null,generation=0,disposed=false;
const identity=()=>`${auth.user?.userId}/${auth.role}`;
function alive(ticket:number,owner:string){return !disposed&&ticket===generation&&owner===identity();}
function clear(){catalog.value=null;profiles.value=[];patientId.value=0;Object.assign(form,{fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',appointmentDate:'',slot:'',dentistId:0,notes:''});challenge.value=null;code.value='';snapshot=null;retryBody.value=null;receipt.value=null;confirmed.value=false;error.value='';profilesError.value='';}
async function load(){
 const ticket=++generation,owner=identity();clear();busy.value=true;
 try{const data=await bookingRequestsService.catalog();if(!alive(ticket,owner))return;
  if(!/^\d{4}-\d{2}-\d{2}$/.test(data.today)||!Array.isArray(data.slots)||!data.slots.length||data.slots.some(s=>!/^\d{2}:\d{2}$/.test(s))||!Array.isArray(data.dentists)||!/^[A-F0-9]{64}$/.test(data.revision))throw Error();
  catalog.value=data;form.appointmentDate=data.today;
  if(auth.role==='PATIENT'){try{const mine=await patientsService.getMyProfiles();if(alive(ticket,owner))profiles.value=mine;}catch{if(alive(ticket,owner))profilesError.value='Không tải được hồ sơ liên kết. Tải lại trước khi đặt cho hồ sơ cũ.';}}
 }catch{if(alive(ticket,owner))error.value='Không tải được khung hẹn. Vui lòng thử lại.';}
 finally{if(alive(ticket,owner))busy.value=false;}
}
function chooseProfile(){confirmed.value=false;const p=profiles.value.find(p=>p.patientId===patientId.value);if(patientId.value&&!p){patientId.value=0;return;}Object.assign(form,{fullName:p?.fullName??'',dateOfBirth:p?.dateOfBirth??'',gender:p?.gender??'',phone:auth.user?.phone??p?.phone??''});}
function validDay(value:string){const d=new Date(value+'T00:00:00Z');return /^\d{4}-\d{2}-\d{2}$/.test(value)&&Number.isFinite(d.getTime())&&d.toISOString().slice(0,10)===value;}
function intent():BookingIntent|null{
 const s=catalog.value;if(!s)return null;const minBirth=new Date(s.today+'T00:00:00Z'),month=minBirth.getUTCMonth();minBirth.setUTCFullYear(minBirth.getUTCFullYear()-120);if(minBirth.getUTCMonth()!==month)minBirth.setUTCDate(0);
 if(!form.fullName.trim()||form.fullName.length>100||form.fullName.includes('\0')||!/^0\d{9}$/.test(form.phone)||!validDay(form.dateOfBirth)||form.dateOfBirth<minBirth.toISOString().slice(0,10)||form.dateOfBirth>s.today||!['Male','Female','Other'].includes(form.gender)||!validDay(form.appointmentDate)||form.appointmentDate<s.today||!s.slots.includes(form.slot)||form.notes.length>500||form.notes.includes('\0')||form.email.length>100||form.email&&!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)||form.dentistId!==0&&!s.dentists.some(d=>d.dentistId===form.dentistId)||patientId.value>0&&!profiles.value.some(p=>p.patientId===patientId.value&&p.fullName===form.fullName.trim()&&p.dateOfBirth===form.dateOfBirth))return null;
 return{fullName:form.fullName.trim(),phone:form.phone,dateOfBirth:form.dateOfBirth,gender:form.gender,email:form.email.trim()||null,appointmentDate:form.appointmentDate,slotTime:form.slot+':00',dentistId:form.dentistId||null,notes:form.notes.trim()||null,scheduleRevision:s.revision,patientId:patientId.value||null};
}
async function requestOtp(){
 if(disposed||busy.value||!confirmed.value||challenge.value||receipt.value)return;const payload=intent();if(!payload){error.value='Kiểm tra lại thông tin cá nhân, ngày và khung giờ.';return;}
 const ticket=generation,owner=identity();busy.value=true;error.value='';
 try{const data=await bookingRequestsService.otp(payload);if(!alive(ticket,owner))return;if(!/^[0-9a-f-]{36}$/i.test(data.challengeId)||data.expiresInSeconds!==300||data.resendAfterSeconds!==60)throw Error();snapshot=payload;challenge.value=data;code.value='';}
 catch{if(alive(ticket,owner))error.value='Chưa gửi được mã. Nếu vừa xin mã, chờ đủ 60 giây; kênh xác thực có thể chưa sẵn sàng, vui lòng liên hệ phòng khám.';}
 finally{if(alive(ticket,owner))busy.value=false;}
}
async function submit(){
 if(disposed||busy.value||receipt.value)return;
 if(!retryBody.value){if(!confirmed.value||!challenge.value||!snapshot||!/^\d{6}$/.test(code.value)){error.value='Nhập mã xác thực 6 số và xác nhận thông tin.';return;}retryBody.value={intent:snapshot,challengeId:challenge.value.challengeId,code:code.value,clientRequestId:crypto.randomUUID(),confirmed:true};}
 const body=retryBody.value,ticket=generation,owner=identity();busy.value=true;error.value='';code.value='';
 try{const data=await bookingRequestsService.submit(body);if(!alive(ticket,owner))return;if(data.requestId!==body.clientRequestId||!['Pending','Rejected','Approved'].includes(data.status))throw Error();receipt.value=data;retryBody.value=null;snapshot=null;challenge.value=null;confirmed.value=false;Object.assign(form,{fullName:'',phone:'',dateOfBirth:'',gender:'',email:'',notes:''});profiles.value=[];}
 catch(cause){if(alive(ticket,owner)){
  const status=isAxiosError(cause)?cause.response?.status:undefined;
  if(status&&[400,401,403,404,409].includes(status)){retryBody.value=null;challenge.value=null;snapshot=null;confirmed.value=false;catalog.value=null;error.value='Yêu cầu chưa được nhận: mã, thông tin hoặc chỗ hẹn không còn hợp lệ. Tải lại khung hẹn trước khi gửi yêu cầu mới.';}
  else error.value='Chưa xác minh kết quả. Gửi lại cùng yêu cầu để đối chiếu; không tạo yêu cầu khác khi chưa rõ kết quả.';
 }}
 finally{if(alive(ticket,owner))busy.value=false;}
}
function resetChallenge(){if(busy.value||retryBody.value)return;challenge.value=null;snapshot=null;code.value='';confirmed.value=false;}
watch(identity,()=>{generation++;busy.value=false;clear();if(!disposed)void load();});onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();});
</script>
<style scoped>
.booking{max-width:48rem;margin:1rem auto;padding:1rem;}fieldset{display:grid;grid-template-columns:repeat(auto-fit,minmax(16rem,1fr));gap:1rem;}label{display:flex;flex-direction:column;gap:.35rem;margin:.7rem 0;}input,select,textarea,button{font:inherit;padding:.6rem;}input[type=checkbox]{align-self:flex-start;}button{margin:.4rem;}[role=alert]{color:#9f1239;}
</style>
