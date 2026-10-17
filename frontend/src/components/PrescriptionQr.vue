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
section { margin:1rem 0; } figure { margin:.8rem 0; } img { display:block; max-width:100%;height:auto; image-rendering:pixelated; } [role=alert] { color:#b91c1c; }
</style>
