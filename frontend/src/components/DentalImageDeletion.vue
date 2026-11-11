<template>
  <section v-if="allowed" class="image-deletion" aria-label="Quản lý xóa ảnh">
    <h3>Quản lý xóa ảnh</h3>
    <p>{{ auth.role === 'ADMIN' ? 'Admin quản lý ảnh đủ điều kiện xóa.' : 'Chỉ hiển thị metadata ảnh do bạn tải lên để đối chiếu xóa.' }} Ảnh trong ca hoàn tất hoặc đã thu tiền bị khóa xóa.</p>
    <button type="button" :disabled="busy" @click="load()">Tải lại thông tin xóa</button>
    <p v-if="busy" role="status">Đang xử lý…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
    <p v-if="needsReview">Tải lại để đối chiếu kết quả trước khi thao tác tiếp; không tự gửi lại.</p>
    <p v-if="loaded && !items.length">Không có ảnh trong phạm vi quản lý xóa.</p>
    <ul>
      <li v-for="image in items" :key="image.id">
        <strong>#{{ image.id }} · {{ image.originalFileName }}</strong>
        <p>{{ image.visitId === null ? 'Ảnh độc lập' : `Lần khám #${image.visitId}` }} · {{ image.toothNumber ? `Răng ${image.toothNumber}` : 'Không gắn răng' }}</p>
        <button v-if="image.canDelete" type="button" :disabled="busy || needsReview" @click="choose(image)">Đối chiếu xóa ảnh #{{ image.id }}</button>
        <p v-else>{{ image.unavailableReason || 'Ảnh không đủ điều kiện xóa.' }}</p>
      </li>
    </ul>
    <button v-if="hasMore" type="button" :disabled="busy || needsReview" @click="load(true)">Xem thêm metadata ảnh</button>
    <form v-if="selected" @submit.prevent="remove">
      <fieldset :disabled="busy || needsReview">
        <legend>Xác nhận xóa ảnh #{{ selected.id }} · {{ selected.originalFileName }}</legend>
        <p>Ảnh sẽ được ẩn khỏi hồ sơ đang xem. Tệp vật lý và lịch sử được giữ lại; thao tác này không sửa ca khám hoặc tài chính.</p>
        <label>Lý do xóa<textarea v-model="reason" required maxlength="500" rows="3" @input="confirmed=false" /></label>
        <label><input v-model="confirmed" type="checkbox" /> Tôi đã đối chiếu đúng ảnh và xác nhận xóa mềm</label>
        <button type="submit" :disabled="!confirmed || !reason.trim()">Xác nhận xóa ảnh #{{ selected.id }}</button>
        <button type="button" @click="clearSelection">Hủy thao tác xóa</button>
      </fieldset>
    </form>
  </section>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { imageDeletionService, type ImageDeletionItem, type ImageDeletionPage } from '@/services/imageDeletion';
const props=defineProps<{patientId:number;refreshKey:number}>(),emit=defineEmits<{deleted:[id:number]}>(),auth=useAuthStore();
const items=ref<ImageDeletionItem[]>([]),selected=ref<ImageDeletionItem|null>(null),reason=ref(''),confirmed=ref(false);
const busy=ref(false),error=ref(''),message=ref(''),hasMore=ref(false),loaded=ref(false),needsReview=ref(true);
let generation=0,disposed=false;
const identity=()=>`${props.patientId}/${auth.user?.userId}/${auth.role}/${props.refreshKey}`;
const allowed=computed(()=>['ADMIN','DENTIST','ASSISTANT'].includes(auth.role)&&Number.isSafeInteger(props.patientId)&&props.patientId>0);
const alive=(ticket:number,owner:string)=>!disposed&&generation===ticket&&identity()===owner;
function clearSelection(){selected.value=null;reason.value='';confirmed.value=false;}
function clear(){items.value=[];hasMore.value=false;loaded.value=false;clearSelection();error.value='';message.value='';}
function validPage(value:ImageDeletionPage,before:number){return value&&typeof value.hasMore==='boolean'&&Array.isArray(value.items)&&value.items.length<=20&&(!value.hasMore||value.items.length===20)&&value.items.every((i,index)=>i.patientId===props.patientId&&Number.isSafeInteger(i.id)&&i.id>0&&(!before||i.id<before)&&(index===0||i.id<value.items[index-1]!.id)&&Number.isSafeInteger(i.version)&&i.version>0&&typeof i.originalFileName==='string'&&typeof i.canDelete==='boolean');}
async function load(more=false){
  if(disposed||busy.value||!allowed.value||more&&(!hasMore.value||needsReview.value))return;
  const before=more?items.value.at(-1)?.id??0:0,ticket=++generation,owner=identity(),previous=more?[...items.value]:[];
  clearSelection();if(!more){items.value=[];hasMore.value=false;loaded.value=false;}busy.value=true;error.value='';message.value='';needsReview.value=true;
  try{const result=await imageDeletionService.list(props.patientId,before);if(!alive(ticket,owner))return;if(!validPage(result,before))throw Error();items.value=[...previous,...result.items];hasMore.value=result.hasMore;loaded.value=true;needsReview.value=false;}
  catch{if(alive(ticket,owner)){clear();error.value='Không tải được thông tin xóa ảnh hoặc bạn không có quyền. Tải lại để đối chiếu.';}}
  finally{if(alive(ticket,owner))busy.value=false;}
}
function choose(image:ImageDeletionItem){
  if(disposed||busy.value||needsReview.value||!allowed.value||image.patientId!==props.patientId||!items.value.includes(image)||!image.canDelete||image.version>=2147483647)return;
  clearSelection();selected.value={...image};
}
async function remove(){
  const image=selected.value;
  if(disposed||busy.value||needsReview.value||!allowed.value||!image||!confirmed.value||!image.canDelete||image.patientId!==props.patientId||!items.value.some(i=>i.id===image.id&&i.version===image.version&&i.canDelete))return;
  const text=reason.value.trim();if(!text||reason.value.length>500||text.includes('\0')){error.value='Nhập lý do xóa từ 1 đến 500 ký tự.';return;}
  const ticket=++generation,owner=identity();busy.value=true;error.value='';message.value='';confirmed.value=false;
  try{const status=await imageDeletionService.remove(image.id,image.version,text);if(!alive(ticket,owner))return;if(status!==204)throw Error();clearSelection();items.value=items.value.filter(i=>i.id!==image.id);needsReview.value=true;hasMore.value=false;message.value=`Đã xóa mềm ảnh #${image.id}. Tệp vật lý được giữ lại. Tải lại trước khi thao tác tiếp.`;emit('deleted',image.id);}
  catch{if(alive(ticket,owner)){clearSelection();needsReview.value=true;error.value='Chưa xác minh được kết quả xóa hoặc ảnh vừa thay đổi. Tải lại để đối chiếu; không tự gửi lại.';}}
  finally{if(alive(ticket,owner))busy.value=false;}
}
watch(identity,()=>{generation++;busy.value=false;needsReview.value=true;clear();if(!disposed)void load();},{immediate:true});
onUnmounted(()=>{disposed=true;generation++;clear();});
</script>
<style scoped>
.image-deletion {
  margin-top: 1.5rem;
  padding: 1.25rem;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
  overflow-wrap: anywhere;
}

h3 {
  color: #0f2d59;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.75rem;
}

ul {
  padding-left: 1.25rem;
  margin: 1rem 0;
}

li {
  padding: 0.6rem 0;
  border-bottom: 1px solid #f1f5f9;
}

li:last-child {
  border-bottom: none;
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.35rem;
  font-size: 0.925rem;
}

textarea {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 80px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
  resize: vertical;
}

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
