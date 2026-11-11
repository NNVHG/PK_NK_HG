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
.signature-page {
  max-width: 768px;
  margin: 1.5rem auto;
  padding: 1.5rem;
  background: #f8fafc;
  color: #1e293b;
  font-family: inherit;
}

h1 {
  color: #0f2d59;
  font-size: 1.6rem;
  font-weight: 700;
  margin: 0 0 0.5rem;
}

h2 {
  color: #0f2d59;
  font-size: 1.25rem;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 0.5rem;
}

p {
  color: #334155;
  line-height: 1.6;
  margin: 0.5rem 0;
}

section {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.5rem;
  margin: 1.25rem 0;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

label {
  display: block;
  font-weight: 500;
  color: #334155;
  margin: 0.75rem 0 0.4rem;
}

input[type="file"] {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.5rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 8px;
  margin-bottom: 0.75rem;
}

input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  accent-color: #0284c7;
  margin-right: 0.5rem;
  vertical-align: middle;
}

button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.625rem 1.25rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  background: #0f2d59;
  color: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  margin: 0.5rem 0.5rem 0.5rem 0;
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

.signature {
  display: block;
  max-width: 100%;
  max-height: 180px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #ffffff;
  background-image: linear-gradient(45deg, #f1f5f9 25%, transparent 25%),
                    linear-gradient(-45deg, #f1f5f9 25%, transparent 25%),
                    linear-gradient(45deg, transparent 75%, #f1f5f9 75%),
                    linear-gradient(-45deg, transparent 75%, #f1f5f9 75%);
  background-size: 16px 16px;
  background-position: 0 0, 0 8px, 8px -8px, -8px 0px;
  margin: 0.75rem 0;
  padding: 0.5rem;
  object-fit: contain;
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

[role="status"] {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  margin: 0.75rem 0;
}

@media (max-width: 640px) {
  .signature-page {
    padding: 1rem 0.75rem;
  }
  section {
    padding: 1rem;
  }
  button {
    width: 100%;
    margin-right: 0;
  }
}
</style>
