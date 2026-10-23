<template>
  <section v-if="allowed" aria-label="Liên kết tài khoản bệnh nhân">
    <h3>Liên kết tài khoản bệnh nhân</h3>
    <p>Đối soát trực tiếp tại quầy trước khi xác nhận. Tài khoản được liên kết sẽ xem được lịch sử và ảnh của hồ sơ này.</p>
    <button type="button" :disabled="busy" @click="load">{{ busy ? 'Đang xử lý…' : 'Tải lại để đối soát' }}</button>
    <p role="status" aria-live="polite">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <template v-if="context">
      <p v-if="context.isLinked">Hồ sơ đã liên kết tài khoản. Không thể chuyển sang tài khoản khác tại đây.</p>
      <p v-else-if="!context.account">Không có tài khoản bệnh nhân đang hoạt động cùng SĐT hoặc tài khoản đã có hồ sơ liên kết. Chưa thể liên kết hồ sơ.</p>
      <form v-else @submit.prevent="save">
        <dl>
          <div><dt>Hồ sơ sẽ được truy cập</dt><dd>{{ context.patientName }} · {{ context.dateOfBirth }} · {{ context.phone }}</dd></div>
          <div><dt>Tài khoản bệnh nhân</dt><dd>{{ context.account.fullName }} · {{ context.account.dateOfBirth || 'Chưa có ngày sinh' }} · {{ context.account.phone }}</dd></div>
        </dl>
        <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối soát tại quầy và xác nhận đúng hồ sơ, đúng tài khoản được phép truy cập</label>
        <button type="submit" :disabled="busy || needsReview || !confirmed">Xác nhận liên kết</button>
      </form>
    </template>
  </section>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { patientAccountLinkService, type PatientAccountLinkContext } from '@/services/patientAccountLink';
const props=defineProps<{patientId:number}>(),auth=useAuthStore();
const allowed=computed(()=>['ADMIN','RECEPTIONIST'].includes(auth.role));
const context=ref<PatientAccountLinkContext|null>(null),confirmed=ref(false),busy=ref(false),needsReview=ref(true),error=ref(''),message=ref('');
let generation=0,disposed=false;
const identity=()=>`${props.patientId}/${auth.role}/${auth.user?.userId}`;
function clear(){context.value=null;confirmed.value=false;needsReview.value=true;}
function alive(ticket:number,owner:string){return !disposed&&ticket===generation&&owner===identity()&&allowed.value;}
function valid(data:PatientAccountLinkContext){
  return data.patientId===props.patientId && typeof data.patientName==='string' && /^\d{4}-\d{2}-\d{2}$/.test(data.dateOfBirth) && /^0\d{9}$/.test(data.phone) && typeof data.isLinked==='boolean'
    && (data.account===null ? data.reviewStamp===null : !data.isLinked && Number.isSafeInteger(data.account.userId) && data.account.userId>0 && typeof data.account.fullName==='string'
      && data.account.phone===data.phone && (data.account.dateOfBirth===null||/^\d{4}-\d{2}-\d{2}$/.test(data.account.dateOfBirth)) && typeof data.reviewStamp==='string' && /^[A-F0-9]{64}$/.test(data.reviewStamp));
}
async function load(){
  if(disposed||busy.value||!allowed.value||!Number.isSafeInteger(props.patientId)||props.patientId<1)return;
  const ticket=++generation,owner=identity();clear();busy.value=true;error.value='';message.value='';
  try{const data=await patientAccountLinkService.read(props.patientId);if(!alive(ticket,owner))return;if(!valid(data))throw Error();context.value=data;needsReview.value=false;}
  catch{if(alive(ticket,owner))error.value='Không tải được thông tin liên kết. Tải lại để đối soát.';}
  finally{if(alive(ticket,owner))busy.value=false;}
}
async function save(){
  if(disposed||!allowed.value||busy.value||needsReview.value||!confirmed.value||!context.value?.account||!context.value.reviewStamp||context.value.isLinked)return;
  const ticket=generation,owner=identity(),patientId=props.patientId,accountId=context.value.account.userId,stamp=context.value.reviewStamp;
  busy.value=true;confirmed.value=false;error.value='';message.value='';
  try{const result=await patientAccountLinkService.link(patientId,accountId,stamp);if(!alive(ticket,owner))return;
    if(result.patientId!==patientId||result.accountUserId!==accountId)throw Error();
    context.value=null;needsReview.value=true;message.value='Đã liên kết hồ sơ. Tài khoản có thể truy cập hồ sơ này khi tải lại danh sách hồ sơ.';
  }catch{if(alive(ticket,owner)){needsReview.value=true;error.value='Chưa xác minh kết quả hoặc dữ liệu vừa thay đổi. Tải lại để đối soát trước khi gửi tiếp.';}}
  finally{if(alive(ticket,owner))busy.value=false;}
}
watch(identity,()=>{generation++;busy.value=false;clear();error.value='';message.value='';if(!disposed)void load();});
onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();error.value='';message.value='';});
</script>
<style scoped>
section{margin-top:1rem;padding:1rem;border:1px solid #cbd5e1;border-radius:6px;}dl div{margin:.8rem 0;}dt{font-weight:600;}dd{margin:.2rem 0;}label{display:block;margin:1rem 0;}button{font:inherit;padding:.6rem;}input{margin-right:.5rem;}[role=alert]{color:#9f1239;}
</style>
