<template>
 <aside class="clinical-panel" :aria-label="'Ghi chú lâm sàng '+label">
  <h3>Ghi chú lâm sàng · {{ label }}</h3>
  <button type="button" :disabled="busy" @click="load()">Tải lại ghi chú</button>
  <p v-if="busy" role="status">Đang xử lý…</p><p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
  <template v-if="context">
   <dl><dt>Chẩn đoán trên ảnh</dt><dd>{{ context.doctorDiagnosis || 'Chưa ghi.' }}</dd><dt>Ghi chú gốc</dt><dd class="note">{{ context.clinicalNotes || 'Chưa ghi.' }}</dd></dl>
   <p v-if="context.updatedAt">Người ghi: {{ context.updatedByName || 'Nhân viên' }} · {{ date(context.updatedAt) }}</p>
   <p v-if="context.visitId === null && auth.role === 'DENTIST'">Ảnh chưa gắn ca: chỉ Admin được ghi chú. Gắn ảnh vào ca bạn phụ trách trước khi ghi.</p>
   <form v-if="mayReplace" @submit.prevent="replace">
    <label>Chẩn đoán trên ảnh<textarea v-model="diagnosis" maxlength="255" :disabled="busy || needsReview" @input="confirmed = false" /></label>
    <label>Ghi chú lâm sàng<textarea v-model="notes" maxlength="1000" :disabled="busy || needsReview" @input="confirmed = false" /></label>
    <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu đúng ảnh và nội dung cần lưu</label>
    <button :disabled="busy || needsReview || !confirmed">Lưu ghi chú gốc</button>
   </form>
   <form v-else-if="mayAppend" @submit.prevent="append">
    <p>Ca đã hoàn tất. Ghi chú và chẩn đoán gốc được giữ nguyên; phần bổ sung lưu nối tiếp cùng người ghi và thời điểm.</p>
    <label>Ghi chú bổ sung<textarea v-model="addition" required maxlength="1000" :disabled="busy || needsReview" @input="confirmed = false" /></label>
    <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi xác nhận thêm ghi chú vào đúng ảnh</label>
    <button :disabled="busy || needsReview || !confirmed || !addition.trim()">Thêm ghi chú bổ sung</button>
   </form>
   <p v-else>Ghi chú chỉ đọc theo quyền và trạng thái ca hiện tại.</p>
  </template>
  <p v-if="needsReview">Tải lại ảnh và ghi chú để đối chiếu trước khi thao tác tiếp; không tự gửi lại.</p>
  <section v-if="history" aria-label="Lịch sử ghi chú bổ sung">
   <h4>Ghi chú bổ sung</h4><p v-if="!history.items.length">Chưa có ghi chú bổ sung.</p>
   <ol><li v-for="entry in history.items" :key="entry.id"><strong>[{{ date(entry.createdAt) }} - {{ entry.authorRole === 'ADMIN' ? 'Admin' : 'Bs.' }} {{ entry.authorName }}]</strong><p class="note">{{ entry.note }}</p></li></ol>
   <button type="button" :disabled="busy || history.page<=1" @click="loadHistory(history.page-1)">Trước</button>
   <span> Trang {{ history.page }} / {{ Math.max(1,history.totalPages) }} </span>
   <button type="button" :disabled="busy || !history.hasNextPage" @click="loadHistory(history.page+1)">Sau</button>
  </section>
 </aside>
</template>
<script setup lang="ts">
import {computed,onMounted,onUnmounted,ref,watch} from 'vue';
import {useAuthStore} from '@/stores/auth';
import {imageClinicalService,type ImageClinicalContext,type ImageClinicalAddendum} from '@/services/imageClinicalNotes';
import type {PagedResult} from '@/services/patients';
const props=defineProps<{imageId:number;patientId:number;label:string}>(),emit=defineEmits<{changed:[]}>(),auth=useAuthStore();
const context=ref<ImageClinicalContext|null>(null),history=ref<PagedResult<ImageClinicalAddendum>|null>(null);
const busy=ref(false),error=ref(''),message=ref(''),needsReview=ref(false),confirmed=ref(false),notes=ref(''),diagnosis=ref(''),addition=ref('');
let generation=0,disposed=false;
const identity=()=>props.imageId+'/'+props.patientId+'/'+auth.user?.userId+'/'+auth.role;
const mayRead=computed(()=>Number.isSafeInteger(props.imageId)&&props.imageId>0&&Number.isSafeInteger(props.patientId)&&props.patientId>0&&['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role));
const mayReplace=computed(()=>['ADMIN','DENTIST'].includes(auth.role)&&context.value?.canReplace===true);
const mayAppend=computed(()=>['ADMIN','DENTIST'].includes(auth.role)&&context.value?.canAppend===true);
const alive=(ticket:number,owner:string)=>!disposed&&ticket===generation&&identity()===owner;
function drafts(){notes.value='';diagnosis.value='';addition.value='';confirmed.value=false;}
function clear(){context.value=null;history.value=null;drafts();error.value='';message.value='';}
function validContext(value:ImageClinicalContext){return value&&value.imageId===props.imageId&&value.patientId===props.patientId&&Number.isSafeInteger(value.version)&&value.version>0&&typeof value.canReplace==='boolean'&&typeof value.canAppend==='boolean'&&!(value.canReplace&&value.canAppend)&&(value.clinicalNotes===null||typeof value.clinicalNotes==='string')&&(value.doctorDiagnosis===null||typeof value.doctorDiagnosis==='string');}
function validPage(value:PagedResult<ImageClinicalAddendum>,page:number){return value&&value.page===page&&value.pageSize===20&&Number.isSafeInteger(value.totalCount)&&value.totalCount>=0&&Array.isArray(value.items)&&value.items.length<=20&&value.items.every(i=>i.imageId===props.imageId&&Number.isSafeInteger(i.id)&&i.id>0&&typeof i.note==='string'&&typeof i.authorName==='string'&&['ADMIN','DENTIST'].includes(i.authorRole));}
function date(value:string){const d=new Date(value);if(!Number.isFinite(d.getTime()))return '—';const parts=new Intl.DateTimeFormat('vi-VN',{timeZone:'Asia/Ho_Chi_Minh',day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit',hour12:false}).formatToParts(d);const part=(type:string)=>parts.find(p=>p.type===type)?.value??'';return `${part('day')}/${part('month')}/${part('year')} ${part('hour')}:${part('minute')}`;}
async function load(){
 if(disposed||busy.value||!mayRead.value)return;
 const ticket=++generation,owner=identity();clear();busy.value=true;needsReview.value=true;
 try{const [data,page]=await Promise.all([imageClinicalService.read(props.imageId),imageClinicalService.history(props.imageId,1)]);if(!alive(ticket,owner))return;
  if(!validContext(data)||!validPage(page,1)||page.items.some(i=>i.imageVersion>data.version))throw Error();
  context.value=data;history.value=page;notes.value=data.clinicalNotes??'';diagnosis.value=data.doctorDiagnosis??'';needsReview.value=false;
 }catch{if(alive(ticket,owner)){clear();error.value='Không tải được ghi chú hoặc không có quyền xem ảnh. Tải lại để đối chiếu.';}}
 finally{if(alive(ticket,owner))busy.value=false;}
}
async function loadHistory(page:number){
 if(disposed||busy.value||!mayRead.value||!Number.isInteger(page)||page<1||page>1000000)return;
 const ticket=++generation,owner=identity();history.value=null;busy.value=true;error.value='';
 try{const result=await imageClinicalService.history(props.imageId,page);if(!alive(ticket,owner))return;if(!validPage(result,page))throw Error();history.value=result;}
 catch{if(alive(ticket,owner))error.value='Không tải được lịch sử bổ sung. Thử tải lại.';}
 finally{if(alive(ticket,owner))busy.value=false;}
}
async function write(appendOnly:boolean){
 const current=context.value;
 if(disposed||busy.value||needsReview.value||!mayRead.value||!confirmed.value||!current||!(appendOnly?mayAppend.value:mayReplace.value)||current.version>=2147483647)return;
 const note=addition.value.trim(),original=notes.value,diagnostic=diagnosis.value;
 if(appendOnly?(!note||addition.value.length>1000||note.includes('\0')):(original.length>1000||diagnostic.length>255||original.includes('\0')||diagnostic.includes('\0'))){error.value='Kiểm tra nội dung: ghi chú tối đa 1000 ký tự, chẩn đoán tối đa 255 ký tự.';return;}
 const ticket=++generation,owner=identity(),expected=current.version+1;busy.value=true;error.value='';message.value='';confirmed.value=false;
 try{
  if(appendOnly){const result=await imageClinicalService.append(props.imageId,current.version,note,globalThis.crypto.randomUUID());if(!alive(ticket,owner))return;
   if(result.imageId!==props.imageId||result.imageVersion!==expected||result.createdByUserId!==auth.user?.userId||result.note!==note||!Number.isSafeInteger(result.id)||result.id<=0)throw Error();
  }else{const result=await imageClinicalService.replace(props.imageId,current.version,original,diagnostic);if(!alive(ticket,owner))return;if(!validContext(result)||result.version!==expected)throw Error();}
  drafts();needsReview.value=true;busy.value=false;emit('changed');await load();
  if(!disposed&&identity()===owner)message.value=context.value && !needsReview.value ? 'Đã lưu ghi chú. Nội dung đã được tải lại để đối chiếu.' : 'Đã lưu ghi chú; chưa tải lại được nội dung. Tải lại để đối chiếu.';
 }catch{if(alive(ticket,owner)){drafts();needsReview.value=true;error.value='Chưa xác minh kết quả lưu hoặc trạng thái đã đổi. Tải lại trước khi thao tác tiếp.';}}
 finally{if(alive(ticket,owner))busy.value=false;}
}
async function replace(){await write(false);}
async function append(){await write(true);}
watch(identity,()=>{generation++;busy.value=false;needsReview.value=true;clear();if(!disposed)void load();});
onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();});
</script>
<style scoped>
.clinical-panel{padding:1rem;border:1px solid #cbd5e1;border-radius:.5rem;min-width:0;}label{display:block;margin:.7rem 0;}textarea{display:block;box-sizing:border-box;width:100%;min-height:5rem;padding:.5rem;font:inherit;}button{padding:.5rem;margin:.3rem 0;}.note{white-space:pre-wrap;overflow-wrap:anywhere;}dd{margin:.3rem 0 1rem;}[role=alert]{color:#b91c1c;}p,li{line-height:1.6;}
</style>
