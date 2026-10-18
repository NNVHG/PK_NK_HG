<template>
 <main class="rx-page">
  <h1>Đơn thuốc · Lần khám #{{ visitId }}</h1>
  <button :disabled="busy" @click="load">{{ draft.length ? 'Bỏ thay đổi và tải lại' : 'Tải lại hồ sơ' }}</button>
  <p v-if="busy" role="status">Đang xử lý…</p><p v-if="error" role="alert" class="error">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
  <template v-if="context">
   <RouterLink v-if="context.prescription && !context.prescription.isCancelled" :to="'/portal/prescriptions/' + context.prescription.prescriptionCode">Mở trang tra cứu đơn bằng CCCD</RouterLink>
   <PrescriptionPrint v-if="context.prescription && !context.prescription.isCancelled" :visit-id="visitId" :prescription-id="context.prescription.id" :prescription-version="context.prescription.version" :blocked="busy || needsReview" />
   <PrescriptionQr v-if="context.prescription && !context.prescription.isCancelled" :visit-id="visitId" :prescription-version="context.prescription.version" />
   <PrescriptionLifecycle :visit-id="visitId" :prescription="context.prescription" :can-cancel="context.canCancel === true" :blocked="busy || needsReview" @changed="load" />
   <p>Ca khám: {{ context.visitStatus }} · {{ context.invoiceCode || 'Chưa có hóa đơn' }} · {{ context.invoiceStatus || '' }}</p>
   <RouterLink v-if="!busy && ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)" :to="`/clinical/visits/${visitId}/invoice-draft`">Đối chiếu hóa đơn dịch vụ và thuốc</RouterLink>
   <section v-if="!editable && context.prescription" aria-label="Đơn thuốc đã lưu">
    <h2>{{ context.prescription.prescriptionCode }}</h2><p v-if="context.prescription.isCancelled" class="error">Đơn đã hủy; không dùng để cấp phát hoặc tra cứu QR.</p>
    <p>Chẩn đoán: {{ context.prescription.diagnosis }}</p>
    <table><thead><tr><th>Thuốc / Hàm lượng</th><th>Số lượng</th><th>Giá đã chụp</th><th>Cách dùng</th></tr></thead><tbody><tr v-for="line in context.prescription.items" :key="line.id"><td>{{ line.medicineName }} {{ line.strength || '' }}</td><td>{{ line.quantity }} {{ line.unit }}</td><td>{{ money(line.unitPrice) }}</td><td>{{ line.usageInstructions }}<br v-if="line.treatmentDays" /><span v-if="line.treatmentDays">Số ngày: {{ line.treatmentDays }}</span></td></tr></tbody></table>
    <p v-if="context.prescription.doctorAdvice">Lời dặn: {{ context.prescription.doctorAdvice }}</p><p v-if="context.prescription.revisitDate">Hẹn tái khám: {{ context.prescription.revisitDate }}</p>
   </section>
   <p v-if="!editable">{{ context.prescription ? 'Đơn chỉ đọc với quyền hoặc trạng thái hiện tại.' : 'Chưa có đơn thuốc. Chỉ nha sĩ phụ trách kê đơn khi ca khám cho phép.' }}</p>
   <template v-if="editable">
    <p>{{ context.prescription?.prescriptionCode || 'Đơn mới' }} · Chỉ lưu sau khi kiểm tra xem trước và xác nhận.</p>
    <p>Chưa có đơn mẫu đã được duyệt. Có thể kê thủ công hoặc nạp từ đơn bác sĩ đã lưu.</p>
    <aside v-if="context.allergies.length" class="warning" aria-label="Tiền sử dị ứng"><strong>Tiền sử dị ứng đã ghi nhận</strong><ul><li v-for="a in context.allergies" :key="a.id">{{ a.name }} {{ a.detail || '' }}</li></ul></aside>
    <p v-else>Chưa có mục dị ứng trong bản tiền sử mới nhất.</p>
    <form @submit.prevent="search(1)" class="search"><label>Tìm thuốc hoạt động<input v-model="keyword" maxlength="200" :disabled="busy || needsReview" /></label><button :disabled="busy || needsReview">Tìm</button></form>
    <ul class="choices"><li v-for="m in medicines" :key="m.id"><span>{{ m.code }} · {{ m.name }} {{ m.strength || '' }} · {{ money(m.unitPrice) }}/{{ m.unit }}</span><button :disabled="busy || needsReview || draft.length >= 10 || draft.some(d => d.medicineId === m.id)" @click="add(m)">Chọn thuốc</button></li></ul>
    <div><button :disabled="busy || needsReview || medicinePage <= 1" @click="search(medicinePage - 1)">Trước</button><span> Trang {{ medicinePage }} </span><button :disabled="busy || needsReview || !hasNext" @click="search(medicinePage + 1)">Sau</button></div>
    <button :disabled="busy || needsReview || draft.length > 0" @click="repeat">Nạp đơn gần nhất</button><small> Xóa các dòng đang nhập trước khi nạp; giá và cảnh báo được kiểm lại.</small>
    <form @submit.prevent="previewOrder" class="editor">
     <label>Chẩn đoán trên đơn<textarea v-model="form.diagnosis" required maxlength="500" :disabled="busy || needsReview" @input="invalidate" /></label>
     <p>{{ draft.length }}/10 loại thuốc</p>
     <article v-for="(line, index) in draft" :key="line.medicineId" class="line">
      <h2>{{ line.name }} {{ line.strength || '' }}</h2><p>{{ line.code }} · {{ line.activeIngredient || 'Chưa ghi hoạt chất' }} · {{ money(line.unitPrice) }}/{{ line.unit }}</p>
      <p v-if="!line.active" class="error">Thuốc này đã ngừng dùng/chưa có giá; bỏ dòng và chọn thuốc hoạt động.</p>
      <label>Số lượng ({{ line.unit }})<input v-model="line.quantity" type="number" required min="1" max="100" step="1" :disabled="busy || needsReview" @input="invalidate" /></label>
      <p v-if="Number(line.quantity) > 30" class="warning">Số lượng trên 30; vui lòng kiểm tra lại.</p>
      <label>Cách dùng<textarea v-model="line.usageInstructions" required maxlength="500" :disabled="busy || needsReview" @input="invalidate" /></label>
      <label>Số ngày dùng (tùy chọn, 1–30)<input v-model="line.treatmentDays" type="number" min="1" max="30" step="1" :disabled="busy || needsReview" @input="invalidate" /></label>
      <p>Thành tiền: {{ lineTotal(line) }}</p><button type="button" :disabled="busy || needsReview" @click="remove(index)">Bỏ dòng</button>
     </article>
     <label>Lời dặn chung<textarea v-model="form.doctorAdvice" maxlength="1000" :disabled="busy || needsReview" @input="invalidate" /></label>
     <label>Ngày hẹn tái khám (tùy chọn)<input v-model="form.revisitDate" type="date" :disabled="busy || needsReview" @input="invalidate" /></label>
     <p>Ngày hẹn này ghi trên đơn; không tự tạo lịch hẹn.</p>
     <button :disabled="busy || needsReview">Kiểm tra và xem trước</button>
    </form>
    <section v-if="preview && previewKey === contentKey()" class="preview" aria-label="Xác nhận kê đơn">
     <h2>Kiểm tra viện phí</h2><dl><dt>Tiền dịch vụ</dt><dd>{{ money(preview.servicesTotal) }}</dd><dt>Tiền thuốc</dt><dd>{{ money(preview.medicineTotal) }}</dd><dt>Chiết khấu hiện tại</dt><dd>{{ money(preview.discountAmount) }}</dd><dt>Tổng phải trả</dt><dd>{{ money(preview.finalAmount) }}</dd></dl>
     <div v-for="warning in preview.allergyWarnings" :key="warning.medicineId" class="warning">
      <p>Thuốc {{ drugName(warning.medicineId) }} trùng mục dị ứng: {{ warning.allergyNames.join(', ') }}</p>
      <label><input v-model="approvals[warning.medicineId]!.confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi xác nhận quyết định vẫn kê thuốc này</label>
      <label>Lý do chuyên môn (5–500 ký tự)<textarea v-model="approvals[warning.medicineId]!.reason" minlength="5" maxlength="500" :disabled="busy || needsReview" /></label>
     </div>
     <label v-if="preview.duplicateIngredients.length" class="warning"><input v-model="duplicateConfirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã kiểm tra các thuốc trùng hoạt chất: {{ preview.duplicateIngredients.join(', ') }}</label>
     <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã kiểm tra đơn, cảnh báo và viện phí trước khi lưu</label>
     <button :disabled="busy || needsReview || !confirmed" @click="save">Lưu đơn và đồng bộ hóa đơn</button>
    </section>
    <p v-if="needsReview" role="alert">Tải lại hồ sơ và đối chiếu kết quả trước khi gửi tiếp.</p>
   </template>
  </template>
 </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import PrescriptionQr from '@/components/PrescriptionQr.vue';
import PrescriptionPrint from '@/components/PrescriptionPrint.vue';
import PrescriptionLifecycle from '@/components/PrescriptionLifecycle.vue';
import { useAuthStore } from '@/stores/auth';
import { prescriptionsService, type RxContext, type RxMedicine, type RxPreview, type RxRequest } from '@/services/prescriptions';
interface DraftLine { medicineId: number; medicineVersion: number; code: string; name: string; activeIngredient: string | null; strength: string | null; unit: string; unitPrice: string; quantity: string | number; usageInstructions: string; treatmentDays: string | number; active: boolean }
const route = useRoute(), auth = useAuthStore();
const visitId = computed(() => { const text = String(route.params.visitId ?? ''); const id = Number(text); return /^\d+$/.test(text) && Number.isSafeInteger(id) && id > 0 && id <= 2147483647 ? id : 0; });
const context = ref<RxContext | null>(null), draft = ref<DraftLine[]>([]), medicines = ref<RxMedicine[]>([]), keyword = ref(''), medicinePage = ref(1), hasNext = ref(false);
const busy = ref(false), error = ref(''), message = ref(''), needsReview = ref(false), preview = ref<RxPreview | null>(null), previewKey = ref(''), confirmed = ref(false), duplicateConfirmed = ref(false);
const approvals = reactive<Record<number, { confirmed: boolean; reason: string }>>({});
const form = reactive({ diagnosis: '', doctorAdvice: '', revisitDate: '' });
let generation = 0, searchVersion = 0, disposed = false;
const identity = () => `${visitId.value}/${auth.user?.userId}/${auth.role}`;
const loadedIdentity = ref('');
const editable = computed(() => context.value?.canEdit === true && loadedIdentity.value === identity() && auth.role === 'DENTIST' && context.value.dentistId === auth.user?.userId);
function contentKey() { return JSON.stringify({ ...form, items: draft.value.map(d => ({ ...d })) }); }
function invalidate() { preview.value = null; previewKey.value = ''; confirmed.value = false; duplicateConfirmed.value = false; Object.keys(approvals).forEach(k => { delete approvals[Number(k)]; }); }
function clear() { context.value = null; draft.value = []; medicines.value = []; Object.assign(form, { diagnosis: '', doctorAdvice: '', revisitDate: '' }); invalidate(); hasNext.value = false; loadedIdentity.value = ''; }
function alive(ticket: number, actor: string) { return !disposed && ticket === generation && actor === identity(); }
async function load() {
 if (disposed || busy.value || !visitId.value || !['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT', 'PATIENT'].includes(auth.role)) return false;
 const ticket = ++generation, actor = identity(); searchVersion++; busy.value = true; error.value = ''; message.value = ''; clear();
 try {
  const data = await prescriptionsService.context(visitId.value);
  if (!alive(ticket, actor)) return false;
  if (data.visitId !== visitId.value || !Array.isArray(data.allergies)) throw Error('Invalid context');
  context.value = data; loadedIdentity.value = actor; needsReview.value = false;
  form.diagnosis = data.prescription?.diagnosis ?? data.visitDiagnosis; form.doctorAdvice = data.prescription?.doctorAdvice ?? ''; form.revisitDate = data.prescription?.revisitDate ?? '';
  draft.value = (data.prescription?.items ?? []).map(l => ({ medicineId: l.medicineId, medicineVersion: l.medicineVersion, code: l.medicineCode, name: l.medicineName, activeIngredient: l.activeIngredient, strength: l.strength, unit: l.unit, unitPrice: l.unitPrice, quantity: String(l.quantity), usageInstructions: l.usageInstructions, treatmentDays: l.treatmentDays === null ? '' : String(l.treatmentDays), active: l.medicineActive }));
  return true;
 } catch { if (alive(ticket, actor)) { error.value = 'Không tải được đơn hoặc không có quyền xem ca khám. Tải lại để đối chiếu.'; needsReview.value = true; } return false; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function search(page = 1) {
 if (disposed || !editable.value || busy.value || needsReview.value || keyword.value.length > 200 || !Number.isInteger(page) || page < 1 || page > 1000000) return;
 const ticket = generation, searchTicket = ++searchVersion, actor = identity(), term = keyword.value; busy.value = true; error.value = ''; medicines.value = [];
 try {
  const data = await prescriptionsService.medicines(visitId.value, term.trim(), page);
  if (!alive(ticket, actor) || searchTicket !== searchVersion || term !== keyword.value) return;
  medicines.value = data.items; medicinePage.value = data.page; hasNext.value = data.hasNextPage;
 } catch { if (alive(ticket, actor)) error.value = 'Không tìm được thuốc. Tải lại nếu ca khám hoặc danh mục vừa thay đổi.'; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
function add(medicine: RxMedicine) {
 if (disposed || !editable.value || busy.value || needsReview.value || draft.value.length >= 10 || draft.value.some(d => d.medicineId === medicine.id)) return;
 const m = medicines.value.find(x => x.id === medicine.id); if (!m) return;
 draft.value.push({ medicineId: m.id, medicineVersion: m.version, code: m.code, name: m.name, activeIngredient: m.activeIngredient, strength: m.strength, unit: m.unit, unitPrice: m.unitPrice, quantity: '', usageInstructions: m.defaultUsageInstruction ?? '', treatmentDays: '', active: true }); invalidate();
}
function remove(index: number) { if (!disposed && editable.value && !busy.value && !needsReview.value && index >= 0 && index < draft.value.length) { draft.value.splice(index, 1); invalidate(); } }
async function repeat() {
 if (disposed || !editable.value || busy.value || needsReview.value || draft.value.length || !context.value) return;
 const ticket = generation, actor = identity(); busy.value = true; error.value = '';
 try {
  const result = await prescriptionsService.latest(context.value.patientId, visitId.value);
  if (!alive(ticket, actor)) return;
  if (!Array.isArray(result.items) || result.items.length < 1 || result.items.length > 10) throw Error('Invalid repeat');
  draft.value = result.items.map(l => ({ medicineId: l.medicineId, medicineVersion: l.currentMedicine?.version ?? 0, code: l.currentMedicine?.code ?? '', name: l.currentMedicine?.name ?? `Thuốc #${l.medicineId} không còn được chọn`, activeIngredient: l.currentMedicine?.activeIngredient ?? null, strength: l.currentMedicine?.strength ?? null, unit: l.currentMedicine?.unit ?? '', unitPrice: l.currentMedicine?.unitPrice ?? '0', quantity: String(l.quantity), usageInstructions: l.usageInstructions, treatmentDays: l.treatmentDays === null ? '' : String(l.treatmentDays), active: !!l.currentMedicine }));
  invalidate(); message.value = `Đã nạp ${result.sourceCode} để kiểm tra; chưa lưu đơn mới.`;
 } catch { if (alive(ticket, actor)) error.value = 'Chưa có đơn trước phù hợp để nạp hoặc ca khám không còn cho phép. Có thể kê thủ công.'; }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
function valid() {
 const date = form.revisitDate ? Date.parse(form.revisitDate) : null;
 if (!form.diagnosis.trim() || form.diagnosis.length > 500 || form.doctorAdvice.length > 1000 || draft.value.length < 1 || draft.value.length > 10 || new Set(draft.value.map(d => d.medicineId)).size !== draft.value.length ||
  form.revisitDate && (date === null || !Number.isFinite(date) || new Date(date).toISOString().slice(0, 10) !== form.revisitDate) || draft.value.some(d => !d.active || d.medicineVersion <= 0 || !/^[0-9]+$/.test(String(d.quantity)) || Number(d.quantity) < 1 || Number(d.quantity) > 100 || !d.usageInstructions.trim() || d.usageInstructions.length > 500 || d.treatmentDays !== '' && (!/^[0-9]+$/.test(String(d.treatmentDays)) || Number(d.treatmentDays) < 1 || Number(d.treatmentDays) > 30) || !/^[0-9]{1,18}$/.test(d.unitPrice))) {
  error.value = 'Kiểm tra chẩn đoán, 1–10 thuốc hoạt động, số lượng 1–100, cách dùng và số ngày tùy chọn 1–30.'; return false;
 }
 const total = draft.value.reduce((n, d) => n + BigInt(d.unitPrice) * BigInt(d.quantity), 0n);
 if (total > 999999999999999999n) { error.value = 'Tổng tiền thuốc vượt giới hạn lưu trữ.'; return false; }
 return true;
}
function request(): RxRequest { return { expectedVersion: context.value?.prescription?.version ?? 0, diagnosis: form.diagnosis.trim(), doctorAdvice: form.doctorAdvice.trim() || null, revisitDate: form.revisitDate || null, duplicateIngredientConfirmed: duplicateConfirmed.value,
 items: draft.value.map(d => ({ medicineId: d.medicineId, medicineVersion: d.medicineVersion, quantity: Number(d.quantity), usageInstructions: d.usageInstructions.trim(), treatmentDays: d.treatmentDays === '' ? null : Number(d.treatmentDays), allergyConfirmed: approvals[d.medicineId]?.confirmed ?? false, allergyOverrideReason: approvals[d.medicineId]?.reason.trim() || null })) }; }
async function previewOrder() {
 if (disposed || !editable.value || busy.value || needsReview.value || !valid()) return;
 const ticket = generation, actor = identity(), key = contentKey(), body = request(); busy.value = true; error.value = ''; invalidate();
 try {
  const data = await prescriptionsService.preview(visitId.value, body);
  if (!alive(ticket, actor) || key !== contentKey()) return;
  if (!/^[A-Fa-f0-9]{64}$/.test(data.token) || !Array.isArray(data.allergyWarnings) || !Array.isArray(data.duplicateIngredients)) throw Error('Invalid preview');
  preview.value = data; previewKey.value = key; for (const warning of data.allergyWarnings) approvals[warning.medicineId] = { confirmed: false, reason: '' };
 } catch { if (alive(ticket, actor)) { error.value = 'Không xem trước được; có thể ca khám, giá, dị ứng hoặc hóa đơn vừa thay đổi. Tải lại để đối chiếu.'; needsReview.value = true; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
async function save() {
 if (disposed || !editable.value || busy.value || needsReview.value || !confirmed.value || !preview.value || previewKey.value !== contentKey() || !valid()) return;
 if (preview.value.allergyWarnings.some(w => !approvals[w.medicineId]?.confirmed || (approvals[w.medicineId]?.reason.trim().length ?? 0) < 5 || (approvals[w.medicineId]?.reason.trim().length ?? 0) > 500) || preview.value.duplicateIngredients.length && !duplicateConfirmed.value) { error.value = 'Xác nhận từng cảnh báo; lý do ghi đè dị ứng cần từ 5 đến 500 ký tự.'; return; }
 const ticket = generation, actor = identity(), creating = !context.value!.prescription, expectedId = context.value!.prescription?.id, expectedVersion = (context.value!.prescription?.version ?? 0) + 1;
 const body = { ...request(), previewToken: preview.value.token }; busy.value = true; error.value = '';
 try {
  const data = await prescriptionsService.save(visitId.value, creating, body);
  if (!alive(ticket, actor)) return;
  if (data.visitId !== visitId.value || data.doctorId !== auth.user?.userId || data.version !== expectedVersion || !Number.isInteger(data.id) || data.id <= 0 || expectedId && data.id !== expectedId) throw Error('Invalid saved order');
  busy.value = false; const loaded = await load();
  if (!disposed && actor === identity()) message.value = loaded ? 'Đã lưu đơn và đồng bộ tiền thuốc vào hóa đơn chung.' : 'Đã lưu đơn nhưng chưa tải được bản đối chiếu. Vui lòng tải lại.';
 } catch { if (alive(ticket, actor)) { needsReview.value = true; confirmed.value = false; error.value = 'Chưa xác minh kết quả lưu. Tải lại hồ sơ để đối chiếu trước khi gửi tiếp.'; } }
 finally { if (alive(ticket, actor)) busy.value = false; }
}
function money(value: string) { try { return BigInt(value).toLocaleString('vi-VN') + ' ₫'; } catch { return '—'; } }
function lineTotal(line: DraftLine) { return /^[0-9]+$/.test(String(line.quantity)) ? money(String(BigInt(line.unitPrice) * BigInt(line.quantity))) : '—'; }
function drugName(id: number) { return draft.value.find(d => d.medicineId === id)?.name ?? `#${id}`; }
watch(identity, () => { generation++; searchVersion++; busy.value = false; error.value = ''; message.value = ''; needsReview.value = true; clear(); if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; searchVersion++; clear(); error.value = ''; message.value = ''; });
</script>
<style scoped>
.rx-page { max-width: 68rem; margin: 1rem auto; } h2 { font-size: 1.1rem; } p { line-height: 1.6; } label { display: grid; gap: .4rem; margin: .6rem 0; } input,textarea,button { padding: .5rem; } textarea { min-height: 4rem; } .editor,.preview,.line { padding: 1rem; border: 1px solid #cbd5e1; border-radius: 6px; margin: 1rem 0; } .warning { padding: .8rem; background: #fff3cd; border-left: 4px solid #d97706; } .error { color: #b91c1c; } .choices { list-style: none; padding: 0; } .choices li { display: flex; justify-content: space-between; gap: 1rem; padding: .6rem; border-bottom: 1px solid #ddd; } .search { display:flex;gap:.6rem;align-items:end; } .search label { flex:1; } button:disabled { opacity: .55; } table { width:100%;border-collapse:collapse; } th,td { text-align:left;padding:.6rem;border-bottom:1px solid #ddd; } dl { display:grid;grid-template-columns:1fr auto;gap:.5rem; } dd { margin:0; }
</style>
