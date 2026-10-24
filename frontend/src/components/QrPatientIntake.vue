<template>
 <section v-if="allowed" class="qr-intake" aria-label="Tiếp nhận bằng QR CCCD">
  <button type="button" :disabled="busy" @click="toggle">{{ opened ? 'Đóng tiếp nhận QR' : 'Tiếp nhận bằng QR CCCD' }}</button>
  <div v-if="opened">
   <p>Đặt con trỏ vào ô quét rồi dùng đầu đọc USB và nhấn Enter. Nhập tay SĐT, đối chiếu với bệnh nhân trước khi tạo hồ sơ.</p>
   <form @submit.prevent="scan()">
    <label>Quét mã CCCD<input ref="scanInput" v-model="raw" type="password" maxlength="1024" autocomplete="off" :disabled="busy || cameraOn" /></label>
    <button :disabled="busy || cameraOn || !raw">Đọc và đối soát</button>
    <button type="button" :disabled="busy || cameraOn" @click="startCamera">Quét bằng camera</button>
   </form>
   <div v-if="cameraOn">
    <video ref="video" muted playsinline aria-label="Camera quét QR CCCD" />
    <button type="button" @click="stopCamera">Dừng camera</button>
   </div>
   <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
   <p v-if="busy" role="status">Đang xử lý…</p>
   <div v-if="existing">
    <p>{{ existing.fullName }} · {{ existing.patientCode }} đã có hồ sơ ({{ existing.masked }}).</p>
    <p v-if="!existing.isActive">Hồ sơ đang ngừng hoạt động; mở hồ sơ để đối chiếu, không tạo trùng.</p>
    <RouterLink :to="{name:'patient-detail',params:{id:existing.patientId}}" @click="close">Mở hồ sơ &amp; tiếp đón</RouterLink>
    <button type="button" @click="close">Hủy</button>
   </div>
   <form v-if="identity && !existing" @submit.prevent="save">
    <p>CCCD: {{ identity.citizenIdMasked }}</p>
    <label>Họ và tên<input v-model="form.fullName" required maxlength="100" :disabled="busy || needsReview" @input="invalidate" /></label>
    <label>Ngày sinh<input v-model="form.dateOfBirth" type="date" required :disabled="busy || needsReview" @input="invalidate" /></label>
    <label>Giới tính<select v-model="form.gender" :disabled="busy || needsReview" @change="invalidate"><option value="Male">Nam</option><option value="Female">Nữ</option></select></label>
    <label>SĐT (nhập tay)<input ref="phoneInput" v-model="form.phone" type="tel" required pattern="0[0-9]{9}" maxlength="10" autocomplete="off" :disabled="busy || needsReview" @input="invalidate" /></label>
    <label>Địa chỉ<textarea v-model="form.address" maxlength="500" :disabled="busy || needsReview" @input="invalidate" /></label>
    <div v-if="duplicates.length">
     <p>SĐT đã được dùng bởi các hồ sơ sau. Đối chiếu trước khi xác nhận người khác dùng chung SĐT.</p>
     <ul><li v-for="item in duplicates" :key="item.patientCode">{{ item.patientCode }} · {{ item.fullName }} · {{ item.dateOfBirth }}</li></ul>
     <label><input v-model="sharedPhoneConfirmed" type="checkbox" :disabled="busy || needsReview" /> Đây là người khác dùng chung SĐT; tạo hồ sơ độc lập</label>
    </div>
    <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu thông tin với bệnh nhân</label>
    <button :disabled="busy || needsReview || !confirmed || (duplicates.length>0 && !sharedPhoneConfirmed)">Tạo hồ sơ</button>
    <p v-if="needsReview">Chưa xác minh kết quả. Quét lại để đối soát; không gửi tạo mới lần nữa.</p>
   </form>
  </div>
 </section>
</template>
<script setup lang="ts">
import { computed,nextTick,onUnmounted,reactive,ref,watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { decodeCitizenQr,validQrBirthDate,qrPatientIntakeService,type ScannedIdentity,type QrExistingPatient } from '@/services/qrPatientIntake';
import type { PatientDuplicateInfo } from '@/services/patients';
const emit=defineEmits<{created:[]}>(),auth=useAuthStore();
const allowed=computed(()=>['ADMIN','RECEPTIONIST'].includes(auth.role));
const opened=ref(false),raw=ref(''),busy=ref(false),error=ref(''),message=ref(''),confirmed=ref(false),sharedPhoneConfirmed=ref(false),needsReview=ref(false);
const identity=ref<ScannedIdentity|null>(null),existing=ref<QrExistingPatient|null>(null),duplicates=ref<PatientDuplicateInfo[]>([]);
const scanInput=ref<HTMLInputElement|null>(null),phoneInput=ref<HTMLInputElement|null>(null),video=ref<HTMLVideoElement|null>(null),cameraOn=ref(false);
const form=reactive({fullName:'',dateOfBirth:'',gender:'Male',phone:'',address:''});
let generation=0,disposed=false,stream:MediaStream|null=null,cameraGeneration=0,timer:ReturnType<typeof setTimeout>|undefined;
const actor=()=>auth.user?.userId+'/'+auth.role;
function alive(ticket:number,owner:string) {return !disposed&&opened.value&&allowed.value&&generation===ticket&&actor()===owner;}
function clear() {raw.value='';identity.value=null;existing.value=null;duplicates.value=[];confirmed.value=false;sharedPhoneConfirmed.value=false;needsReview.value=false;error.value='';message.value='';Object.assign(form,{fullName:'',dateOfBirth:'',gender:'Male',phone:'',address:''});}
function invalidate() {confirmed.value=false;sharedPhoneConfirmed.value=false;duplicates.value=[];}
function stopCamera() {cameraGeneration++;if(timer!==undefined)clearTimeout(timer);timer=undefined;stream?.getTracks().forEach(t=>t.stop());stream=null;if(video.value)video.value.srcObject=null;cameraOn.value=false;}
function close() {generation++;stopCamera();opened.value=false;busy.value=false;clear();}
async function toggle() {if(opened.value)close();else {opened.value=true;clear();await nextTick();scanInput.value?.focus();}}
async function scan(value?:string) {
 if(disposed||busy.value||!allowed.value||!opened.value)return;
 const input=value??raw.value,ticket=++generation,owner=actor();stopCamera();clear();busy.value=true;
 try {
  const decoded=await decodeCitizenQr(input);if(!alive(ticket,owner))return;
  const result=await qrPatientIntakeService.lookup(decoded.citizenIdHash);if(!alive(ticket,owner))return;
  if(!result || !('existing' in result) || result.existing===undefined)throw Error();
  if(result.existing) {const item=result.existing;if(!Number.isSafeInteger(item.patientId)||item.patientId<=0||!/^BN\d+$/.test(item.patientCode)||typeof item.fullName!=='string'||!/^\d{3}\*{6}\d{3}$/.test(item.masked)||typeof item.isActive!=='boolean')throw Error();existing.value=item;}
  else {identity.value=decoded;Object.assign(form,{fullName:decoded.fullName,dateOfBirth:decoded.dateOfBirth,gender:decoded.gender,address:decoded.address,phone:''});await nextTick();if(alive(ticket,owner))phoneInput.value?.focus();}
 } catch {if(alive(ticket,owner))error.value='Không đọc hoặc đối soát được QR CCCD. Kiểm tra mã và kết nối, quét lại hoặc chuyển sang nhập tay.';}
 finally {if(alive(ticket,owner))busy.value=false;}
}
async function save() {
 if(disposed||busy.value||needsReview.value||!opened.value||!allowed.value||!identity.value||existing.value||!confirmed.value||duplicates.value.length&&!sharedPhoneConfirmed.value)return;
 if(!form.fullName.trim()||form.fullName.length>100||!/^0[0-9]{9}$/.test(form.phone)||form.phone.length!==10||!validQrBirthDate(form.dateOfBirth)||!['Male','Female'].includes(form.gender)||form.address.length>500){error.value='Kiểm tra họ tên, ngày sinh, SĐT 10 số và địa chỉ.';return;}
 const ticket=++generation,owner=actor(),id=identity.value;busy.value=true;error.value='';
 try {
  const result=await qrPatientIntakeService.create(id,{...form,gender:form.gender,email:null,confirmNotDuplicate:sharedPhoneConfirmed.value});
  if(!alive(ticket,owner))return;
  if(!Number.isSafeInteger(result.patientId)||result.patientId<=0||!/^BN\d+$/.test(result.patientCode))throw Error();
  clear();existing.value={patientId:result.patientId,patientCode:result.patientCode,fullName:result.fullName,masked:id.citizenIdMasked,isActive:result.isActive};message.value='Đã tạo hồ sơ. Mở hồ sơ để tiếp đón theo luồng hiện có.';emit('created');
 } catch(failure:unknown) {
  if(!alive(ticket,owner))return;
  const data=(failure as {response?:{data?:{code?:string;duplicates?:PatientDuplicateInfo[]}}})?.response?.data;
  confirmed.value=false;
  if(data?.code==='PATIENT_POSSIBLE_DUPLICATE'&&Array.isArray(data.duplicates)&&data.duplicates.length>0&&data.duplicates.length<=20){duplicates.value=data.duplicates;sharedPhoneConfirmed.value=false;error.value='Đối chiếu hồ sơ dùng chung SĐT trước khi tiếp tục.';}
  else {needsReview.value=true;error.value='Trạng thái có thể đã đổi hoặc chưa xác minh kết quả. Quét lại để đối soát trước khi tạo hồ sơ.';}
 } finally {if(alive(ticket,owner))busy.value=false;}
}
interface NativeDetector {detect(source:HTMLVideoElement):Promise<Array<{rawValue:string}>>}
interface NativeDetectorConstructor {new(options:{formats:string[]}):NativeDetector;getSupportedFormats():Promise<string[]>}
async function startCamera() {
 if(disposed||busy.value||cameraOn.value||!allowed.value||!opened.value)return;
 const Detector=(globalThis as typeof globalThis&{BarcodeDetector?:NativeDetectorConstructor}).BarcodeDetector;
 if(!Detector||!navigator.mediaDevices?.getUserMedia){error.value='Camera đọc QR chưa được hỗ trợ. Dùng đầu đọc USB hoặc nhập tay.';return;}
 stopCamera();clear();cameraOn.value=true;const ticket=cameraGeneration,owner=actor();
 const active=()=>!disposed&&opened.value&&allowed.value&&cameraOn.value&&cameraGeneration===ticket&&actor()===owner;
 try {
  if(!(await Detector.getSupportedFormats()).includes('qr_code'))throw Error();if(!active())return;
  const acquired=await navigator.mediaDevices.getUserMedia({video:{facingMode:{ideal:'environment'}},audio:false});
  if(!active()){acquired.getTracks().forEach(t=>t.stop());return;}
  stream=acquired;await nextTick();if(!active()||!video.value){stopCamera();return;}video.value.srcObject=acquired;await video.value.play();
  const detector=new Detector({formats:['qr_code']});
  async function frame() {
   if(!active()||!video.value)return;
   try {if(video.value.readyState>=2){const codes=await detector.detect(video.value);if(!active())return;if(codes[0]?.rawValue){await scan(codes[0].rawValue);return;}}}
   catch {if(active()){stopCamera();error.value='Không đọc được camera. Dùng đầu đọc USB hoặc nhập tay.';}return;}
   if(active())timer=setTimeout(()=>void frame(),300);
  }
  if(active())void frame();
 } catch {if(active()){stopCamera();error.value='Không mở được camera đọc QR. Kiểm tra quyền camera hoặc dùng đầu đọc USB/nhập tay.';}}
}
watch(actor,close);
onUnmounted(()=>{disposed=true;close();});
</script>
<style scoped>
.qr-intake{margin:1rem 0;padding:1rem;border:1px solid #cbd5e1;border-radius:.5rem;}label{display:block;margin:.75rem 0;}input,select,textarea{display:block;max-width:100%;padding:.5rem;}input[type=checkbox]{display:inline;}button{margin:.4rem;padding:.5rem;}video{display:block;width:min(100%,30rem);}[role=alert]{color:#b91c1c;}p{line-height:1.6;}
</style>
