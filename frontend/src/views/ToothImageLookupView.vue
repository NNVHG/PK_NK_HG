<template>
  <main class="tooth-images">
    <div class="page-header">
      <RouterLink v-if="visitId" :to="`${portalPrefix}/clinical/visits/${visitId}/fdi`" class="back-link">
        ← Trở lại sơ đồ răng FDI
      </RouterLink>
      <div class="header-titles">
        <span class="eyebrow">HÌNH ẢNH LÂM SÀNG & X-QUANG</span>
        <h1>Tra cứu ảnh theo răng · Lần khám #{{ visitId }}</h1>
        <p class="subtitle">Xem và quản lý hình ảnh chụp trong miệng, phim X-quang gắn với từng răng hoặc toàn hàm.</p>
      </div>
    </div>

    <section class="filter-panel" aria-labelledby="filter-heading">
      <h2 id="filter-heading" class="sr-only">Bộ lọc răng FDI</h2>
      <form class="filter-form" @submit.prevent="applyFilter">
        <div class="input-group">
          <label for="lookup-tooth">Răng FDI (bỏ trống để xem toàn bộ)</label>
          <div class="input-with-badge">
            <input
              id="lookup-tooth"
              v-model="filterInput"
              maxlength="2"
              inputmode="numeric"
              placeholder="VD: 11"
              :disabled="loading"
            />
            <span v-if="appliedTooth" class="active-badge">Đang lọc răng {{ appliedTooth }}</span>
          </div>
        </div>
        <div class="button-group">
          <button type="submit" class="btn btn-primary" :disabled="loading">Lọc ảnh</button>
          <button type="button" class="btn btn-secondary" :disabled="loading" @click="clearFilter">Bỏ lọc răng</button>
        </div>
      </form>
    </section>

    <div v-if="filterError" class="alert alert-error" role="alert">{{ filterError }}</div>
    <div v-if="loading" class="alert alert-info" role="status">Đang tải hồ sơ ảnh…</div>
    <div v-if="error" class="alert alert-error" role="alert">
      <span>{{ error }}</span>
      <button type="button" class="btn btn-outline-danger" :disabled="loading" @click="load">Tải lại</button>
    </div>

    <DentalImageAttachments
      v-if="visit"
      :key="`${visit.patientId}/${appliedTooth ?? ''}`"
      :patient-id="visit.patientId"
      :tooth-number="appliedTooth"
    />
  </main>
</template>
<script setup lang="ts">
import {computed,onUnmounted,ref,watch} from 'vue';
import {useRoute,useRouter} from 'vue-router';
import {useAuthStore} from '@/stores/auth';
import {dentalImagesService,validImageTooth,type ImageVisitContext} from '@/services/dentalImages';
import DentalImageAttachments from '@/components/DentalImageAttachments.vue';
const route=useRoute(),router=useRouter(),auth=useAuthStore();
const portalPrefix=computed(()=>auth.role==='PATIENT'?'/patient':'/management');
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
.tooth-images {
  max-width: 80rem;
  margin: 1.5rem auto;
  padding: 1.5rem;
  background: #f8fafc;
  color: #1e293b;
  font-family: inherit;
}

.page-header {
  margin-bottom: 1.5rem;
}

.back-link {
  display: inline-flex;
  align-items: center;
  font-size: 0.9rem;
  font-weight: 600;
  color: #0284c7;
  text-decoration: none;
  margin-bottom: 0.75rem;
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
  background: #e0f2fe;
  transition: all 0.15s ease-in-out;
}

.back-link:hover {
  background: #bae6fd;
  color: #0369a1;
  text-decoration: none;
}

.header-titles {
  border-left: 4px solid #0284c7;
  padding-left: 1rem;
}

.eyebrow {
  display: block;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: #0284c7;
  text-transform: uppercase;
  margin-bottom: 0.25rem;
}

h1 {
  color: #0f2d59;
  font-size: 1.6rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
  line-height: 1.3;
}

.subtitle {
  color: #64748b;
  font-size: 0.95rem;
  margin: 0;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}

.filter-panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.5rem;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

.filter-form {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.input-group label {
  font-size: 0.9rem;
  font-weight: 600;
  color: #334155;
}

.input-with-badge {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

input {
  box-sizing: border-box;
  min-height: 44px;
  width: 7.5rem;
  padding: 0.5rem 0.75rem;
  font-size: 1.05rem;
  text-align: center;
  font-weight: 600;
  color: #0f2d59;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.2);
}

.active-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.35rem 0.65rem;
  background: #ecfdf5;
  color: #047857;
  border: 1px solid #a7f3d0;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
}

.button-group {
  display: flex;
  gap: 0.6rem;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1.25rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
}

.btn-primary {
  background: #0f2d59;
  color: #ffffff;
  border: 1px solid #0f2d59;
}

.btn-primary:hover:not(:disabled) {
  background: #1e3a8a;
  border-color: #1e3a8a;
}

.btn-secondary {
  background: #ffffff;
  color: #475569;
  border: 1px solid #cbd5e1;
}

.btn-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  color: #1e293b;
}

.btn-outline-danger {
  background: #ffffff;
  color: #b91c1c;
  border: 1px solid #fecaca;
  min-height: 36px;
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

.btn-outline-danger:hover:not(:disabled) {
  background: #fef2f2;
}

.btn:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  font-size: 0.95rem;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.alert-error {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
}

.alert-info {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
}

@media (max-width: 640px) {
  .tooth-images {
    padding: 1rem 0.75rem;
  }
  .filter-form {
    flex-direction: column;
    align-items: stretch;
  }
  .input-with-badge {
    flex-direction: column;
    align-items: stretch;
  }
  input {
    width: 100%;
  }
  .button-group {
    flex-direction: column;
    width: 100%;
  }
  .btn {
    width: 100%;
  }
}
</style>
