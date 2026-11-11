<template>
 <section aria-label="Mã QR tra cứu đơn đã lưu">
  <button :disabled="busy || !canRead" @click="generate">{{ busy ? 'Đang tạo QR…' : 'Hiện mã QR tra cứu đơn đã lưu' }}</button>
  <p v-if="error" role="alert">{{ error }}</p>
  <figure v-if="qr">
   <img :src="qr.image" width="228" height="228" :alt="'QR tra cứu đơn ' + code" />
   <figcaption>{{ code }} · Quét rồi nhập CCCD của chủ đơn để xem.</figcaption>
   <a :href="qr.url">Mở trang tra cứu</a>
  </figure>
 </section>
</template>
<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { MOD_RX_lookupQr, prescriptionQrService } from '@/services/prescriptionQr';
const props = defineProps<{ visitId: number; prescriptionVersion: number }>(), auth = useAuthStore();
const qr = ref<{ image: string; url: string } | null>(null), code = ref(''), busy = ref(false), error = ref('');
let generation = 0, disposed = false;
const identity = () => props.visitId + '/' + props.prescriptionVersion + '/' + auth.user?.userId + '/' + auth.role;
const canRead = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT'].includes(auth.role));
function clear() { qr.value = null; code.value = ''; error.value = ''; }
async function generate() {
 if (disposed || busy.value || !canRead.value || !Number.isInteger(props.visitId) || props.visitId <= 0) return;
 const ticket = ++generation, actor = identity(); clear(); busy.value = true;
 try {
  const link = await prescriptionQrService.link(props.visitId);
  if (disposed || ticket !== generation || actor !== identity()) return;
  if (link.version !== props.prescriptionVersion || link.relativePath !== '/portal/prescriptions/' + link.prescriptionCode) throw Error('Changed order');
  const result = await MOD_RX_lookupQr(link.relativePath, window.location.origin);
  if (disposed || ticket !== generation || actor !== identity()) return;
  qr.value = result; code.value = link.prescriptionCode;
 } catch { if (!disposed && ticket === generation && actor === identity()) error.value = 'Không tạo được mã QR hoặc đơn vừa thay đổi. Tải lại đơn để đối chiếu.'; }
 finally { if (!disposed && ticket === generation && actor === identity()) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; clear(); });
onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>
section {
  margin: 1.25rem 0;
  padding: 1.25rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

figure {
  margin: 1rem 0;
  display: flex;
  flex-direction: column;
  align-items: center;
}

img {
  display: block;
  max-width: 200px;
  height: auto;
  image-rendering: pixelated;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.5rem;
  background: #ffffff;
}

figcaption {
  margin-top: 0.5rem;
  font-size: 0.9rem;
  color: #475569;
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
</style>
