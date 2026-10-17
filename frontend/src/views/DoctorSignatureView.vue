<template>
 <main class="signature-page">
  <h1>Ảnh chữ ký bác sĩ</h1>
  <p>Ảnh chữ ký scan phục vụ bản in nội bộ. Mỗi lần thay ảnh được lưu thành phiên bản có người thực hiện.</p>
  <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
  <button :disabled="busy || !allowed" @click="load">Tải lại thông tin</button>
  <section v-if="status">
   <h2>{{ status.doctorName }}</h2><p>{{ status.version ? 'Phiên bản hiện tại: ' + status.version : 'Chưa có ảnh chữ ký' }}</p>
   <img v-if="currentImage" :src="currentImage" alt="Ảnh chữ ký đã lưu" class="signature" />
   <form @submit.prevent="save">
    <label>Ảnh PNG, tối đa 256 KB<input type="file" accept="image/png" :disabled="busy || needsReview" @change="selectFile" /></label>
    <img v-if="newImage" :src="'data:image/png;base64,' + newImage" alt="Ảnh chữ ký đang chọn, chưa lưu" class="signature" />
    <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview || !newImage" /> Tôi đã đối chiếu ảnh chữ ký với bác sĩ này</label>
    <button :disabled="busy || needsReview || !confirmed || !newImage">Lưu phiên bản chữ ký</button>
   </form>
  </section>
 </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { doctorSignaturesService, MOD_AUTH_signatureFile, type DoctorSignatureStatus } from '@/services/doctorSignatures';
const route = useRoute(), auth = useAuthStore();
const doctorId = computed(() => { const text = String(route.params.doctorId ?? ''); const n = Number(text); return /^[0-9]+$/.test(text) && Number.isSafeInteger(n) && n > 0 && n <= 2147483647 ? n : 0; });
const allowed = computed(() => doctorId.value > 0 && (auth.role === 'ADMIN' || auth.role === 'DENTIST' && doctorId.value === auth.user?.userId));
const status = ref<DoctorSignatureStatus | null>(null), currentImage = ref(''), newImage = ref(''), busy = ref(false), error = ref(''), message = ref(''), needsReview = ref(false), confirmed = ref(false);
let generation = 0, disposed = false;
const identity = () => doctorId.value + '/' + auth.user?.userId + '/' + auth.role;
const loaded = ref('');
function clear() { if (currentImage.value) URL.revokeObjectURL(currentImage.value); currentImage.value = ''; newImage.value = ''; confirmed.value = false; status.value = null; loaded.value = ''; }
function alive(ticket: number, actor: string) { return !disposed && generation === ticket && actor === identity(); }
function valid(s: DoctorSignatureStatus, version?: number) { return s.doctorId === doctorId.value && Number.isInteger(s.version) && s.version >= 0 && (version === undefined || s.version === version) && (s.version === 0 ? s.signatureImageUrl === null : s.signatureImageUrl === '/api/doctors/' + doctorId.value + '/signature/file?version=' + s.version); }
async function load() {
 if (disposed || busy.value || !allowed.value) return false;
 const ticket = ++generation, actor = identity(); clear(); error.value = ''; message.value = ''; busy.value = true;
 try {
  const result = await doctorSignaturesService.read(doctorId.value); if (!alive(ticket, actor)) return false;
  if (!valid(result)) throw Error('Invalid status');
  if (result.version > 0) {
   const blob = await doctorSignaturesService.file(doctorId.value, result.version); if (!alive(ticket, actor)) return false;
   if (blob.type !== 'image/png' || blob.size <= 0 || blob.size > 256 * 1024) throw Error('Invalid image');
   currentImage.value = URL.createObjectURL(blob);
  }
  status.value = result; loaded.value = actor; needsReview.value = false; return true;
 } catch { if (alive(ticket, actor)) { clear(); needsReview.value = true; error.value = 'Không tải được ảnh hoặc không có quyền quản lý bác sĩ này.'; } return false; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function selectFile(event: Event) {
 const input = event.target as HTMLInputElement, file = input.files?.[0]; input.value = '';
 if (disposed || busy.value || !allowed.value || needsReview.value || loaded.value !== identity() || !file) return;
 const ticket = generation, actor = identity(); busy.value = true; newImage.value = ''; confirmed.value = false; error.value = ''; message.value = '';
 try { const encoded = await MOD_AUTH_signatureFile(file); if (alive(ticket, actor)) newImage.value = encoded; }
 catch { if (alive(ticket, actor)) error.value = 'Chọn ảnh PNG tối đa 256 KB, không xen kẽ hoặc có bảng màu. Backend kiểm tra ảnh trước khi lưu.'; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function save() {
 if (disposed || busy.value || !allowed.value || loaded.value !== identity() || needsReview.value || !status.value || !newImage.value || !confirmed.value) return;
 const ticket = generation, actor = identity(), version = status.value.version + 1, image = newImage.value; newImage.value = ''; confirmed.value = false; busy.value = true; error.value = ''; message.value = '';
 try {
  const result = await doctorSignaturesService.save(doctorId.value, status.value.version, image); if (!alive(ticket, actor)) return;
  if (!valid(result, version)) throw Error('Invalid saved version');
  busy.value = false; const refreshed = await load();
  if (!disposed && actor === identity()) message.value = refreshed ? 'Đã lưu phiên bản chữ ký và audit.' : 'Đã lưu chữ ký nhưng chưa tải được ảnh đối chiếu. Vui lòng tải lại.';
 } catch { if (alive(ticket, actor)) { clear(); needsReview.value = true; error.value = 'Chưa xác minh kết quả lưu hoặc có thay đổi đồng thời. Tải lại trước khi gửi tiếp.'; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; message.value = ''; needsReview.value = true; if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clear(); error.value = ''; message.value = ''; });
</script>
<style scoped>
.signature-page { max-width:48rem;margin:1rem auto;padding:1rem; } label { display:block;margin:1rem 0; } input,button { padding:.5rem; } .signature { max-width:100%;max-height:180px;border:1px solid #cbd5e1;background:#fff; } [role=alert] { color:#b91c1c; }
</style>
