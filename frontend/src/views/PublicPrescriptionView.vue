<template>
 <main class="public-rx">
  <h1>Tra cứu đơn thuốc · Nha khoa HG</h1>
  <p v-if="summary">Mã đơn: {{ summary.prescriptionCode }} · Ngày kê: {{ new Date(summary.createdAt).toLocaleDateString('vi-VN') }}</p>
  <p v-if="busy" role="status">Đang đối chiếu…</p><p v-if="error" role="alert">{{ error }}</p>
  <button :disabled="busy" @click="load">Tải lại</button>
  <form v-if="summary && !details" @submit.prevent="verify">
   <label>CCCD của chủ đơn thuốc<input v-model="citizenId" type="password" inputmode="numeric" maxlength="12" autocomplete="off" :disabled="busy" required /></label>
   <button :disabled="busy">Đối chiếu và xem đơn</button>
  </form>
  <section v-if="details" aria-label="Chi tiết đơn thuốc">
   <p>Bệnh nhân: {{ details.patientName }}</p><p>Nha sĩ: {{ details.doctorName }}</p><p>Chẩn đoán: {{ details.diagnosis }}</p>
   <table><thead><tr><th>Thuốc / Hàm lượng</th><th>Số lượng</th><th>Cách dùng</th></tr></thead><tbody><tr v-for="(item, i) in details.items" :key="i"><td>{{ item.name }} {{ item.strength || '' }}</td><td>{{ item.quantity }} {{ item.unit }}</td><td>{{ item.usageInstructions }}<span v-if="item.treatmentDays"> · {{ item.treatmentDays }} ngày</span></td></tr></tbody></table>
   <p v-if="details.doctorAdvice">Lời dặn: {{ details.doctorAdvice }}</p><p v-if="details.revisitDate">Hẹn tái khám: {{ details.revisitDate }}</p>
   <p>Phiên xem hết hạn lúc {{ new Date(expiresAt).toLocaleTimeString('vi-VN') }}.</p><button @click="close">Đóng chi tiết</button>
  </section>
 </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { publicPrescriptionsService, type PublicRxSummary, type PublicRxDetails, type PublicRxSession } from '@/services/publicPrescriptions';
const route = useRoute();
const code = computed(() => String(route.params.code ?? ''));
const summary = ref<PublicRxSummary | null>(null), details = ref<PublicRxDetails | null>(null), citizenId = ref(''), error = ref(''), busy = ref(false), expiresAt = ref('');
let generation = 0, disposed = false, timer: ReturnType<typeof setTimeout> | undefined;
const key = (value = code.value) => 'rx-view:' + value;
function removeSession(value = code.value) { try { sessionStorage.removeItem(key(value)); } catch { /* Browser may disable storage. */ } }
function clearPrivate() { details.value = null; citizenId.value = ''; expiresAt.value = ''; if (timer) clearTimeout(timer); timer = undefined; }
function close() { removeSession(); clearPrivate(); error.value = ''; }
function alive(ticket: number, current: string) { return !disposed && generation === ticket && code.value === current; }
function validSession(s: PublicRxSession) { const expiry = Date.parse(s.expiresAt); return typeof s.token === 'string' && s.token.length > 0 && s.token.length <= 4096 && Number.isFinite(expiry) && expiry > Date.now() && expiry <= Date.now() + 16 * 60000; }
async function readDetails(session: PublicRxSession, ticket: number, current: string) {
 if (!validSession(session)) throw Error('Expired session');
 const data = await publicPrescriptionsService.details(current, session.token);
 if (!alive(ticket, current)) return;
 if (!validSession(session) || data.prescriptionCode !== current || !Array.isArray(data.items)) throw Error('Invalid details');
 details.value = data; expiresAt.value = session.expiresAt;
 try { sessionStorage.setItem(key(current), JSON.stringify(session)); } catch { /* Current page can still use its in-memory session. */ }
 timer = setTimeout(() => { if (alive(ticket, current)) { removeSession(current); clearPrivate(); error.value = 'Phiên xem đã hết hạn. Nhập lại CCCD để đối chiếu.'; } }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
}
async function load() {
 if (disposed || busy.value) return;
 const ticket = ++generation, current = code.value; summary.value = null; clearPrivate(); error.value = '';
 if (current.length !== 16 || !/^RX-[0-9]{8}-[0-9]{4}$/.test(current)) { error.value = 'Mã đơn thuốc không hợp lệ.'; return; }
 busy.value = true;
 try {
  const result = await publicPrescriptionsService.summary(current); if (!alive(ticket, current)) return;
  if (result.prescriptionCode !== current || !Number.isFinite(Date.parse(result.createdAt))) throw Error('Invalid summary');
  summary.value = result;
  let stored: PublicRxSession | null = null;
  try { const raw = sessionStorage.getItem(key(current)); if (raw) stored = JSON.parse(raw); } catch { removeSession(current); }
  if (stored) { if (validSession(stored)) await readDetails(stored, ticket, current); else removeSession(current); }
 } catch {
  if (alive(ticket, current)) { removeSession(current); clearPrivate(); error.value = 'Không tải được đơn hoặc phiên xem không còn hợp lệ. Có thể tải lại và đối chiếu CCCD.'; }
 } finally { if (alive(ticket, current)) busy.value = false; }
}
async function verify() {
 if (disposed || busy.value || !summary.value) return;
 if (citizenId.value.length !== 12 || !/^[0-9]{12}$/.test(citizenId.value)) { error.value = 'CCCD cần đúng 12 chữ số.'; return; }
 const ticket = generation, current = code.value, input = citizenId.value; busy.value = true; error.value = ''; clearPrivate(); removeSession(current);
 try {
  const session = await publicPrescriptionsService.verify(current, input);
  if (alive(ticket, current)) await readDetails(session, ticket, current);
 } catch (failure: unknown) {
  if (alive(ticket, current)) { removeSession(current); clearPrivate(); const status = (failure as { response?: { status?: number } })?.response?.status;
   error.value = status === 429 ? 'Đã vượt giới hạn đối chiếu. Vui lòng thử lại sau.' : 'Chưa đối chiếu được CCCD hoặc đơn không còn được xem. Kiểm tra thông tin và thử lại.'; }
 } finally { if (alive(ticket, current)) busy.value = false; }
}
watch(code, () => { generation++; busy.value = false; clearPrivate(); summary.value = null; error.value = ''; if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clearPrivate(); summary.value = null; error.value = ''; });
</script>
<style scoped>
.public-rx { max-width:48rem;margin:2rem auto;padding:1rem; } label { display:block;margin:1rem 0; } input,button { padding:.6rem;margin:.3rem; } [role=alert] { color:#b91c1c; } table { width:100%;border-collapse:collapse; } td,th { padding:.6rem;text-align:left;border-bottom:1px solid #cbd5e1; }
</style>
