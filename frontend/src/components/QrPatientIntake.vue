<template>
  <section v-if="allowed" class="qr-intake" aria-label="Tiếp nhận bằng QR CCCD">
    <div class="intake-heading">
      <div>
        <h2>Tiếp nhận bằng QR CCCD</h2>
        <p>Quét và đối chiếu giấy tờ để tìm hoặc tạo hồ sơ bệnh nhân.</p>
      </div>
      <button type="button" class="secondary-button" :aria-expanded="opened" :disabled="busy" @click="toggle">
        {{ opened ? 'Đóng tiếp nhận QR' : 'Tiếp nhận bằng QR CCCD' }}
      </button>
    </div>

    <div v-if="opened" class="intake-body">
      <form class="scan-panel" @submit.prevent="scan()">
        <label class="field">
          <span>Quét mã CCCD</span>
          <input ref="scanInput" v-model="raw" type="password" maxlength="1024" autocomplete="off" :disabled="busy || cameraOn" />
          <span class="field-hint">Đặt con trỏ vào ô, quét bằng đầu đọc USB và nhấn Enter.</span>
        </label>
        <div class="actions">
          <button class="primary-button" :disabled="busy || cameraOn || !raw">Đọc và đối soát</button>
          <button type="button" class="secondary-button" :disabled="busy || cameraOn" @click="startCamera">Quét bằng camera</button>
        </div>
        <p class="manual-help">Để nhập thông tin thủ công, đóng tiếp nhận QR và chọn “Thêm bệnh nhân” trên trang hồ sơ.</p>
      </form>

      <div v-if="cameraOn" class="camera-panel">
        <video ref="video" muted playsinline aria-label="Camera quét QR CCCD" />
        <button type="button" class="secondary-button" @click="stopCamera">Dừng camera</button>
      </div>
      <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
      <p v-if="message" class="alert alert-success" role="status">{{ message }}</p>
      <p v-if="busy" class="processing" role="status">Đang xử lý…</p>

      <div v-if="existing" class="existing-patient">
        <h3>Hồ sơ đã có</h3>
        <p><strong>{{ existing.fullName }}</strong> · {{ existing.patientCode }}</p>
        <p class="masked-id">CCCD {{ existing.masked }}</p>
        <p v-if="!existing.isActive" class="alert alert-warning">Hồ sơ đang ngừng hoạt động; mở hồ sơ để đối chiếu, không tạo trùng.</p>
        <div class="actions">
          <RouterLink class="primary-button" :to="{name:'management-patient-detail',params:{id:existing.patientId}}" @click="close">Mở hồ sơ &amp; tiếp đón</RouterLink>
          <button type="button" class="secondary-button" @click="close">Hủy</button>
        </div>
      </div>

      <form v-if="identity && !existing" class="identity-form" @submit.prevent="save">
        <div class="identity-heading">
          <h3>Đối chiếu thông tin</h3>
          <p class="masked-id">CCCD {{ identity.citizenIdMasked }}</p>
        </div>
        <div class="field-grid">
          <label class="field field-wide"><span>Họ và tên</span><input v-model="form.fullName" required maxlength="100" :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field"><span>Ngày sinh</span><input v-model="form.dateOfBirth" type="date" required :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field"><span>Giới tính</span><select v-model="form.gender" :disabled="busy || needsReview" @change="invalidate"><option value="Male">Nam</option><option value="Female">Nữ</option></select></label>
          <label class="field field-wide"><span>SĐT (nhập tay)</span><input ref="phoneInput" v-model="form.phone" type="tel" inputmode="numeric" required pattern="0[0-9]{9}" maxlength="10" autocomplete="off" :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field field-wide"><span>Địa chỉ <span class="optional">(không bắt buộc)</span></span><textarea v-model="form.address" maxlength="500" rows="2" :disabled="busy || needsReview" @input="invalidate" /></label>
        </div>
        <div v-if="duplicates.length" class="alert alert-warning">
          <p>SĐT đã được dùng bởi các hồ sơ sau. Đối chiếu CCCD để tránh tạo trùng hồ sơ.</p>
          <ul><li v-for="item in duplicates" :key="item.patientCode">{{ item.patientCode }} · {{ item.fullName }} · {{ item.dateOfBirth }}</li></ul>
          <label class="confirmation"><input v-model="duplicateIdentityConfirmed" type="checkbox" :disabled="busy || needsReview" /><span>Đã đối chiếu CCCD: đây là người khác dùng chung SĐT, cần tạo hồ sơ độc lập</span></label>
        </div>
        <div class="form-footer">
          <label class="confirmation"><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /><span>Tôi đã đối chiếu thông tin với bệnh nhân</span></label>
          <button class="primary-button" :disabled="busy || needsReview || !confirmed || (duplicates.length>0 && !duplicateIdentityConfirmed)">Tạo hồ sơ</button>
        </div>
        <p v-if="needsReview" class="alert alert-warning">Chưa xác minh kết quả. Quét lại để đối soát; không gửi tạo mới lần nữa.</p>
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
const opened=ref(false),raw=ref(''),busy=ref(false),error=ref(''),message=ref(''),confirmed=ref(false),duplicateIdentityConfirmed=ref(false),needsReview=ref(false);
const identity=ref<ScannedIdentity|null>(null),existing=ref<QrExistingPatient|null>(null),duplicates=ref<PatientDuplicateInfo[]>([]);
const scanInput=ref<HTMLInputElement|null>(null),phoneInput=ref<HTMLInputElement|null>(null),video=ref<HTMLVideoElement|null>(null),cameraOn=ref(false);
const form=reactive({fullName:'',dateOfBirth:'',gender:'Male',phone:'',address:''});
let generation=0,disposed=false,stream:MediaStream|null=null,cameraGeneration=0,timer:ReturnType<typeof setTimeout>|undefined;
const actor=()=>auth.user?.userId+'/'+auth.role;
function alive(ticket:number,owner:string) {return !disposed&&opened.value&&allowed.value&&generation===ticket&&actor()===owner;}
function clear() {raw.value='';identity.value=null;existing.value=null;duplicates.value=[];confirmed.value=false;duplicateIdentityConfirmed.value=false;needsReview.value=false;error.value='';message.value='';Object.assign(form,{fullName:'',dateOfBirth:'',gender:'Male',phone:'',address:''});}
function invalidate() {confirmed.value=false;duplicateIdentityConfirmed.value=false;duplicates.value=[];}
function stopCamera() {cameraGeneration++;if(timer!==undefined)clearTimeout(timer);timer=undefined;stream?.getTracks().forEach(t=>t.stop());stream=null;if(video.value)video.value.srcObject=null;cameraOn.value=false;}
function close() {generation++;stopCamera();opened.value=false;busy.value=false;clear();}
async function toggle() {if(opened.value)close();else {opened.value=true;clear();await nextTick();scanInput.value?.focus();}}
async function scan(value?:string) {
 if(disposed||busy.value||!allowed.value||!opened.value)return;
 const input=value??raw.value,ticket=++generation,owner=actor();stopCamera();clear();busy.value=true;
 try {
  const decoded=await decodeCitizenQr(input);if(!alive(ticket,owner))return;
  const result=await qrPatientIntakeService.lookup(decoded.citizenId);if(!alive(ticket,owner))return;
  if(!result || !('existing' in result) || result.existing===undefined)throw Error();
  if(result.existing) {const item=result.existing;if(!Number.isSafeInteger(item.patientId)||item.patientId<=0||!/^BN\d+$/.test(item.patientCode)||typeof item.fullName!=='string'||!/^\d{3}\*{6}\d{3}$/.test(item.masked)||typeof item.isActive!=='boolean')throw Error();existing.value=item;}
  else {identity.value=decoded;Object.assign(form,{fullName:decoded.fullName,dateOfBirth:decoded.dateOfBirth,gender:decoded.gender,address:decoded.address,phone:''});await nextTick();if(alive(ticket,owner))phoneInput.value?.focus();}
 } catch {if(alive(ticket,owner))error.value='Không đọc hoặc đối soát được QR CCCD. Kiểm tra mã và kết nối, quét lại hoặc chuyển sang nhập tay.';}
 finally {if(alive(ticket,owner))busy.value=false;}
}
async function save() {
 if(disposed||busy.value||needsReview.value||!opened.value||!allowed.value||!identity.value||existing.value||!confirmed.value||duplicates.value.length&&!duplicateIdentityConfirmed.value)return;
 if(!form.fullName.trim()||form.fullName.length>100||!/^0[0-9]{9}$/.test(form.phone)||form.phone.length!==10||!validQrBirthDate(form.dateOfBirth)||!['Male','Female'].includes(form.gender)||form.address.length>500){error.value='Kiểm tra họ tên, ngày sinh, SĐT 10 số và địa chỉ.';return;}
 const ticket=++generation,owner=actor(),id=identity.value;busy.value=true;error.value='';
 try {
  const result=await qrPatientIntakeService.create(id,{...form,gender:form.gender,email:null,confirmNotDuplicate:duplicateIdentityConfirmed.value});
  if(!alive(ticket,owner))return;
  if(!Number.isSafeInteger(result.patientId)||result.patientId<=0||!/^BN\d+$/.test(result.patientCode))throw Error();
  clear();existing.value={patientId:result.patientId,patientCode:result.patientCode,fullName:result.fullName,masked:id.citizenIdMasked,isActive:result.isActive};message.value='Đã tạo hồ sơ. Mở hồ sơ để tiếp đón theo luồng hiện có.';emit('created');
 } catch(failure:unknown) {
  if(!alive(ticket,owner))return;
  const data=(failure as {response?:{data?:{code?:string;duplicates?:PatientDuplicateInfo[]}}})?.response?.data;
  confirmed.value=false;
  if(data?.code==='PATIENT_POSSIBLE_DUPLICATE'&&Array.isArray(data.duplicates)&&data.duplicates.length>0&&data.duplicates.length<=20){duplicates.value=data.duplicates;duplicateIdentityConfirmed.value=false;error.value='Đối chiếu danh sách hồ sơ trùng trước khi tiếp tục.';}
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
.qr-intake {
  margin: 1rem 0;
  border: 1px solid #c8deeb;
  border-radius: 16px;
  background: #fff;
  color: #18354c;
  overflow-wrap: anywhere;
}

.intake-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1.25rem;
}

h2, h3 { margin: 0; color: #102e56; }
h2 { font-size: 1.1rem; }
h3 { font-size: 1rem; }
p { line-height: 1.6; }
.intake-heading p { margin: .4rem 0 0; color: #536a7e; font-size: .875rem; }
.intake-body { border-top: 1px solid #e4edf5; padding: 1.25rem; }
.scan-panel { padding: 1rem; border: 1px solid #d8ecfa; border-radius: 10px; background: #f4faff; }
.field { display: flex; flex-direction: column; gap: .5rem; min-width: 0; font-size: .9rem; font-weight: 650; }
.field input, .field select, .field textarea {
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  min-height: 44px;
  padding: .65rem .75rem;
  border: 1px solid #b8cedf;
  border-radius: 8px;
  background: #fff;
  color: #18354c;
  font: inherit;
  font-size: 1rem;
}
.field textarea { resize: vertical; }
.field-hint, .optional { color: #536a7e; font-size: .8rem; font-weight: 400; line-height: 1.5; }
.field-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.field-wide { grid-column: 1 / -1; }
.actions { display: flex; flex-wrap: wrap; gap: .65rem; margin-top: 1rem; }
.primary-button, .secondary-button {
  box-sizing: border-box;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: .65rem 1rem;
  border: 1px solid transparent;
  border-radius: 8px;
  font: inherit;
  font-size: .875rem;
  font-weight: 700;
  text-align: center;
  text-decoration: none;
  cursor: pointer;
}
.primary-button { background: #1857b6; color: #fff; }
.secondary-button { background: #fff; border-color: #b8cedf; color: #1857b6; }
button:disabled, input:disabled, select:disabled, textarea:disabled { opacity: .6; cursor: not-allowed; }
a:focus-visible, button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible { outline: 3px solid #1857b6; outline-offset: 3px; }
.manual-help { margin: 1rem 0 0; color: #536a7e; font-size: .875rem; }
.camera-panel { display: grid; justify-items: start; gap: .75rem; margin-top: 1rem; }
video { display: block; width: min(100%, 30rem); border-radius: 10px; background: #102e56; }
.alert { padding: .8rem 1rem; border: 1px solid transparent; border-radius: 9px; font-size: .875rem; line-height: 1.6; }
.alert-error { color: #991b1b; background: #fef2f2; border-color: #fecaca; }
.alert-success { color: #166534; background: #f0fdf4; border-color: #bbf7d0; }
.alert-warning { color: #854d0e; background: #fffbeb; border-color: #fde68a; }
.alert p { margin-top: 0; }
.alert ul { padding-left: 1.2rem; }
.processing { color: #1857b6; }
.existing-patient, .identity-form { border-top: 1px solid #e4edf5; margin-top: 1.5rem; padding-top: 1.5rem; }
.identity-heading { margin-bottom: 1rem; }
.masked-id { color: #536a7e; font-size: .875rem; }
.confirmation { display: flex; align-items: flex-start; gap: .6rem; line-height: 1.6; font-size: .875rem; }
.confirmation input { flex: none; width: 18px; height: 18px; margin: .15rem 0 0; accent-color: #1857b6; }
.form-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 1rem; padding-top: 1rem; margin-top: 1.25rem; border-top: 1px solid #e4edf5; }
@media (hover: hover) {
  .primary-button:hover:not(:disabled) { background: #144c9e; }
  .secondary-button:hover:not(:disabled) { background: #eaf5fc; }
}
@media (max-width: 540px) {
  .intake-heading { align-items: stretch; flex-direction: column; }
  .intake-heading, .intake-body { padding: 1rem; }
  .scan-panel { padding: .875rem; }
  .field-grid { grid-template-columns: minmax(0, 1fr); }
  .actions > *, .form-footer > button { width: 100%; }
}
</style>
