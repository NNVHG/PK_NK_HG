<template>
  <section v-if="allowed" class="image-metadata" :aria-label="'Thông tin ảnh '+label">
    <h3>Thông tin ảnh · {{ label }}</h3>
    <button type="button" :disabled="busy" @click="load">Tải lại thông tin ảnh</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
    <p v-if="needsReview">Tải lại để đối chiếu trạng thái và kết quả; không tự gửi lại.</p>
    <template v-if="context">
      <p v-if="!context.canEdit">Thông tin ảnh chỉ đọc theo quyền và trạng thái hiện tại. Ca hoàn tất hoặc đã thu tiền bị khóa sửa.</p>
      <button v-else-if="!editing" type="button" :disabled="busy || needsReview" @click="startEdit">Sửa mô tả, phân loại và răng</button>
      <form v-if="editing" @submit.prevent="save">
        <fieldset :disabled="busy || needsReview">
          <legend>Ảnh #{{ context.id }} · {{ context.originalFileName }}</legend>
          <p>Chỉ thay thông tin mô tả ảnh; ghi chú lâm sàng và lần khám được giữ nguyên.</p>
          <label>Phân loại ảnh<select v-model="category" @change="confirmed=false"><option v-for="(name,key) in categories" :key="key" :value="key">{{ name }}</option></select></label>
          <label>Răng FDI (không bắt buộc)<input v-model="toothText" maxlength="2" inputmode="numeric" placeholder="Ví dụ: 46" @input="confirmed=false" /></label>
          <label>Mô tả tải ảnh<textarea v-model="notes" maxlength="500" rows="3" @input="confirmed=false" /></label>
          <label><input v-model="confirmed" type="checkbox" /> Tôi đã đối chiếu đúng ảnh, phân loại và răng</label>
          <button type="submit" :disabled="!confirmed">Lưu thông tin ảnh</button>
          <button type="button" @click="cancelEdit">Hủy chỉnh sửa</button>
        </fieldset>
      </form>
    </template>
  </section>
</template>
<script setup lang="ts">
import {computed,onMounted,onUnmounted,ref,watch} from 'vue';
import {useAuthStore} from '@/stores/auth';
import {validImageTooth} from '@/services/dentalImages';
import {imageMetadataService,type ImageMetadataContext} from '@/services/imageMetadata';
const props=defineProps<{imageId:number;patientId:number;label:string}>(),emit=defineEmits<{updated:[value:ImageMetadataContext]}>(),auth=useAuthStore();
const context=ref<ImageMetadataContext|null>(null),busy=ref(false),needsReview=ref(true),editing=ref(false),confirmed=ref(false);
const category=ref(''),notes=ref(''),toothText=ref(''),error=ref(''),message=ref('');
const categories:Record<string,string>={Panorama:'Toàn cảnh',Periapical:'Cận chóp',Bitewing:'Cắn cánh',Cephalometric:'Sọ nghiêng',Intraoral:'Trong miệng'};
let generation=0,disposed=false;
const identity=()=>`${props.imageId}/${props.patientId}/${auth.user?.userId}/${auth.role}`;
const allowed=computed(()=>['ADMIN','DENTIST'].includes(auth.role)&&Number.isSafeInteger(props.imageId)&&props.imageId>0&&Number.isSafeInteger(props.patientId)&&props.patientId>0);
const alive=(ticket:number,owner:string)=>!disposed&&ticket===generation&&identity()===owner;
function cancelEdit(){editing.value=false;confirmed.value=false;category.value='';toothText.value='';notes.value='';}
function clear(){context.value=null;cancelEdit();error.value='';message.value='';}
function valid(value:ImageMetadataContext){return value&&value.id===props.imageId&&value.patientId===props.patientId&&Number.isSafeInteger(value.version)&&value.version>0&&typeof value.canEdit==='boolean'&&typeof value.originalFileName==='string'&&Object.hasOwn(categories,value.category)&&validImageTooth(value.toothNumber)&&(value.notes===null||typeof value.notes==='string'&&value.notes.length<=500);}
async function load(){
  if(disposed||busy.value||!allowed.value)return;
  const ticket=++generation,owner=identity();clear();busy.value=true;needsReview.value=true;
  try{const result=await imageMetadataService.read(props.imageId);if(!alive(ticket,owner))return;if(!valid(result))throw Error();context.value=result;needsReview.value=false;}
  catch{if(alive(ticket,owner)){clear();error.value='Không tải được thông tin ảnh hoặc bạn không có quyền. Tải lại để đối chiếu.';}}
  finally{if(alive(ticket,owner))busy.value=false;}
}
function startEdit(){
  if(disposed||busy.value||needsReview.value||!allowed.value||!context.value?.canEdit||context.value.version>=2147483647)return;
  category.value=context.value.category;notes.value=context.value.notes??'';toothText.value=context.value.toothNumber?.toString()??'';confirmed.value=false;editing.value=true;message.value='';
}
async function save(){
  const current=context.value;
  if(disposed||busy.value||needsReview.value||!allowed.value||!editing.value||!confirmed.value||!current?.canEdit||current.version>=2147483647)return;
  const raw=toothText.value.trim(),tooth=raw?Number(raw):null;
  if(!Object.hasOwn(categories,category.value)||!validImageTooth(tooth)||raw&&!/^\d{2}$/.test(raw)||notes.value.length>500||notes.value.includes('\0')){error.value='Kiểm tra phân loại, số răng FDI và mô tả tối đa 500 ký tự.';return;}
  const ticket=++generation,owner=identity(),target={category:category.value,toothNumber:tooth,notes:notes.value.trim()||null};busy.value=true;error.value='';message.value='';confirmed.value=false;
  try{const result=await imageMetadataService.update(props.imageId,current.version,target.category,target.toothNumber,notes.value);if(!alive(ticket,owner))return;
    if(!valid(result)||result.version!==current.version+1||result.visitId!==current.visitId||result.category!==target.category||result.toothNumber!==target.toothNumber||result.notes!==target.notes)throw Error();
    context.value=result;cancelEdit();message.value='Đã lưu thông tin ảnh.';emit('updated',result);
  }catch{if(alive(ticket,owner)){cancelEdit();needsReview.value=true;error.value='Chưa xác minh được kết quả lưu, hạn mức răng hoặc trạng thái đã đổi. Tải lại để đối chiếu trước khi thao tác tiếp.';}}
  finally{if(alive(ticket,owner))busy.value=false;}
}
watch(identity,()=>{generation++;busy.value=false;needsReview.value=true;clear();if(!disposed)void load();});
onMounted(()=>void load());onUnmounted(()=>{disposed=true;generation++;clear();});
</script>
<style scoped>
.image-metadata {
  padding: 1.25rem;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
  min-width: 0;
  overflow-wrap: anywhere;
}

h3 {
  color: #0f2d59;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

fieldset {
  padding: 1rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  margin: 1rem 0;
  background: #f8fafc;
}

legend {
  font-weight: 600;
  color: #0f2d59;
  padding: 0 0.5rem;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

input:not([type="checkbox"]),
select,
textarea {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

textarea {
  min-height: 80px;
  resize: vertical;
}

input:focus-visible,
select:focus-visible,
textarea:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  vertical-align: middle;
  margin-right: 0.4rem;
}

button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #0f2d59;
  color: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  margin: 0.4rem 0.4rem 0.4rem 0;
}

button:hover:not(:disabled) {
  background: #1e3a8a;
}

button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

[role="alert"] {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 0.75rem 0;
}

@media (max-width: 640px) {
  button {
    width: 100%;
    margin-right: 0;
  }
}
</style>
