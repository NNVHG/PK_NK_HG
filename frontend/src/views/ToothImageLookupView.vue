<template>
  <main class="tooth-images">
    <RouterLink v-if="visitId" :to="`/clinical/visits/${visitId}/fdi`">← Trở lại sơ đồ răng</RouterLink>
    <h1>Tra cứu ảnh theo răng</h1>
    <form @submit.prevent="applyFilter">
      <label for="lookup-tooth">Răng FDI (bỏ trống để xem mọi răng)</label>
      <input id="lookup-tooth" v-model="filterInput" maxlength="2" inputmode="numeric" :disabled="loading" />
      <button :disabled="loading">Lọc ảnh</button>
      <button type="button" :disabled="loading" @click="clearFilter">Bỏ lọc răng</button>
    </form>
    <p v-if="filterError" role="alert">{{ filterError }}</p>
    <p v-if="loading" role="status">Đang tải hồ sơ ảnh…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <button v-if="error" type="button" :disabled="loading" @click="load">Tải lại</button>
    <DentalImageAttachments v-if="visit" :key="`${visit.patientId}/${appliedTooth ?? ''}`" :patient-id="visit.patientId" :tooth-number="appliedTooth" />
  </main>
</template>
<script setup lang="ts">
import {computed,onUnmounted,ref,watch} from 'vue';
import {useRoute,useRouter} from 'vue-router';
import {useAuthStore} from '@/stores/auth';
import {dentalImagesService,validImageTooth,type ImageVisitContext} from '@/services/dentalImages';
import DentalImageAttachments from '@/components/DentalImageAttachments.vue';
const route=useRoute(),router=useRouter(),auth=useAuthStore();
const visitId=computed(()=>Number(route.params.visitId)),visit=ref<ImageVisitContext|null>(null),appliedTooth=ref<number|null>(null),filterInput=ref('');
const loading=ref(false),error=ref(''),filterError=ref('');let generation=0,disposed=false;
const identity=()=>`${route.params.visitId}/${JSON.stringify(route.query.toothNumber)}/${auth.role}/${auth.user?.userId}`;
const allowed=computed(()=>['ADMIN','DENTIST','RECEPTIONIST','PATIENT'].includes(auth.role));
function parseTooth(value:unknown):number|null|undefined{if(value===undefined)return null;if(typeof value!=='string'||!/^\d{2}$/.test(value))return undefined;const number=Number(value);return validImageTooth(number)?number:undefined;}
async function load(){
  const ticket=++generation,owner=identity();visit.value=null;error.value='';filterError.value='';loading.value=false;
  if(disposed||!allowed.value)return;
  const tooth=parseTooth(route.query.toothNumber),id=visitId.value;
  if(!Number.isSafeInteger(id)||id<1||tooth===undefined){error.value='Mã lần khám hoặc số răng FDI không hợp lệ.';return;}
  appliedTooth.value=tooth;filterInput.value=tooth?.toString()??'';loading.value=true;
  try{const result=await dentalImagesService.visitContext(id);if(disposed||ticket!==generation||identity()!==owner)return;if(result.visitId!==id||!Number.isSafeInteger(result.patientId)||result.patientId<1)throw Error();visit.value=result;}
  catch{if(!disposed&&ticket===generation&&identity()===owner)error.value='Không tải được lần khám hoặc bạn không có quyền xem hồ sơ này.';}
  finally{if(!disposed&&ticket===generation&&identity()===owner)loading.value=false;}
}
async function applyFilter(){
  if(disposed||loading.value||!allowed.value)return;
  const text=filterInput.value.trim();if(text&&parseTooth(text)===undefined){filterError.value='Nhập số răng FDI hợp lệ của bộ răng người lớn hoặc trẻ em.';return;}
  filterError.value='';await router.replace({query:{...route.query,toothNumber:text||undefined}});
}
async function clearFilter(){filterInput.value='';await applyFilter();}
watch(identity,()=>void load(),{immediate:true});onUnmounted(()=>{disposed=true;generation++;visit.value=null;filterInput.value='';});
</script>
<style scoped>
.tooth-images{padding:1rem;max-width:90rem;margin:auto;}form{display:flex;flex-wrap:wrap;align-items:center;gap:.6rem;margin:1rem 0;}input,button{font:inherit;padding:.6rem;}input{width:5rem;}[role=alert]{color:#9f1239;}
</style>
