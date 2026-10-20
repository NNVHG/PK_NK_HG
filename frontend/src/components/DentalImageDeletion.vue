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
.image-deletion{margin-top:1.5rem;padding:1rem;border:1px solid #cbd5e1;border-radius:.5rem;overflow-wrap:anywhere;}ul{padding-left:1.2rem;}li{padding:.6rem 0;}button,textarea{font:inherit;padding:.6rem;}label{display:block;margin:.8rem 0;}textarea{display:block;width:100%;box-sizing:border-box;}fieldset{padding:1rem;border:1px solid #cbd5e1;}[role=alert]{color:#9f1239;}
</style>
