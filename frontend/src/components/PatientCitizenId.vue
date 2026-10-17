<template>
 <section aria-label="CCCD của bệnh nhân">
  <h3>CCCD</h3>
  <p v-if="status">Đã lưu: {{ status.masked || 'Chưa có CCCD' }}</p>
  <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
  <button :disabled="busy" @click="load">Tải lại thông tin CCCD</button>
  <form v-if="canWrite && status" @submit.prevent="save">
   <label>Nhập CCCD của bệnh nhân (12 chữ số)<input v-model="citizenId" @input="confirmed = false" type="password" inputmode="numeric" maxlength="12" autocomplete="off" :disabled="busy || needsReview" /></label>
   <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu CCCD với bệnh nhân</label>
   <button :disabled="busy || needsReview || !confirmed">Lưu CCCD</button>
  </form>
 </section>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { patientCitizenIdService, type CitizenIdStatus } from '@/services/patientCitizenId';
const props = defineProps<{ patientId: number }>(), auth = useAuthStore();
const status = ref<CitizenIdStatus | null>(null), citizenId = ref(''), confirmed = ref(false), busy = ref(false), needsReview = ref(false), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => props.patientId + '/' + auth.user?.userId + '/' + auth.role;
const loaded = ref('');
const canWrite = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role) && loaded.value === identity());
function clear() { status.value = null; citizenId.value = ''; confirmed.value = false; loaded.value = ''; }
function alive(ticket: number, actor: string) { return !disposed && ticket === generation && identity() === actor; }
async function load() {
 if (disposed || busy.value || !Number.isInteger(props.patientId) || props.patientId <= 0 || !['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT'].includes(auth.role)) return;
 const ticket = ++generation, actor = identity(); clear(); busy.value = true; error.value = ''; message.value = '';
 try { const data = await patientCitizenIdService.read(props.patientId); if (!alive(ticket, actor)) return;
  if (data.patientId !== props.patientId || !Number.isInteger(data.version) || data.version < 0 || data.masked !== null && !/^[0-9]{3}\*{6}[0-9]{3}$/.test(data.masked)) throw Error('Invalid response');
  status.value = data; loaded.value = actor; needsReview.value = false;
 } catch { if (alive(ticket, actor)) { error.value = 'Không tải được thông tin CCCD. Tải lại để đối chiếu.'; needsReview.value = true; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function save() {
 if (disposed || !canWrite.value || busy.value || needsReview.value || !confirmed.value || !status.value) return;
 if (citizenId.value.length !== 12 || !/^[0-9]{12}$/.test(citizenId.value)) { error.value = 'CCCD cần đúng 12 chữ số.'; confirmed.value = false; return; }
 const ticket = generation, actor = identity(), nextVersion = status.value.version + 1, body = citizenId.value; busy.value = true; error.value = ''; message.value = '';
 citizenId.value = ''; confirmed.value = false;
 try { const data = await patientCitizenIdService.save(props.patientId, body, status.value.version); if (!alive(ticket, actor)) return;
  if (data.patientId !== props.patientId || data.version !== nextVersion || data.masked !== body.slice(0, 3) + '******' + body.slice(-3)) throw Error('Invalid response');
  status.value = data; message.value = 'Đã lưu CCCD dạng bảo vệ.';
 } catch { if (alive(ticket, actor)) { error.value = 'Chưa xác minh kết quả lưu hoặc CCCD đã thuộc hồ sơ khác. Tải lại trước khi gửi tiếp.'; needsReview.value = true; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; needsReview.value = true; error.value = ''; message.value = ''; clear(); if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clear(); error.value = ''; message.value = ''; });
</script>
<style scoped>
section { margin-top:1rem; padding:1rem; border:1px solid #cbd5e1; border-radius:6px; } label { display:block; margin:.7rem 0; } input,button { padding:.45rem; } [role=alert] { color:#b91c1c; }
</style>
