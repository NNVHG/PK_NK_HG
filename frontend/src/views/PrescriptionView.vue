<template>
 <main class="rx-page">
  <div class="page-header">
   <div class="header-nav">
    <RouterLink to="/" class="nav-link">← Trang chủ</RouterLink>
    <RouterLink
     v-if="context?.prescription && !context.prescription.isCancelled"
     :to="'/portal/prescriptions/' + context.prescription.prescriptionCode"
     class="nav-link alt"
    >
     🔍 Mở trang tra cứu đơn bằng CCCD
    </RouterLink>
    <RouterLink
     v-if="!busy && ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role)"
     :to="`${portalPrefix}/clinical/visits/${visitId}/invoice-draft`"
     class="nav-link"
    >
     💳 Đối chiếu hóa đơn viện phí
    </RouterLink>
   </div>
   <div class="header-main">
    <div class="header-titles">
     <span class="eyebrow">DƯỢC LÂM SÀNG & ĐƠN THUỐC</span>
     <h1>Đơn thuốc ca khám · Lần khám #{{ visitId }}</h1>
     <p v-if="context" class="visit-meta-tag">
      <span>Trạng thái ca: <strong>{{ context.visitStatus }}</strong></span>
      <span>· Hóa đơn: <strong>{{ context.invoiceCode || 'Chưa có' }}</strong> ({{ context.invoiceStatus || 'Chưa lập' }})</span>
     </p>
    </div>
    <div class="header-actions">
     <button class="btn btn-secondary" :disabled="busy" @click="load">
      {{ draft.length ? '🔄 Bỏ thay đổi & Tải lại' : '🔄 Tải lại hồ sơ' }}
     </button>
    </div>
   </div>
  </div>

  <div v-if="busy" class="alert alert-info" role="status">Đang xử lý…</div>
  <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
  <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>

  <template v-if="context">
   <div class="prescription-management-strip" v-if="context.prescription && !context.prescription.isCancelled">
    <PrescriptionPrint :visit-id="visitId" :prescription-id="context.prescription.id" :prescription-version="context.prescription.version" :blocked="busy || needsReview" />
    <PrescriptionHandover :visit-id="visitId" :prescription-id="context.prescription.id" :prescription-version="context.prescription.version" :blocked="busy || needsReview" @changed="load" />
    <PrescriptionQr :visit-id="visitId" :prescription-version="context.prescription.version" />
    <PrescriptionLifecycle :visit-id="visitId" :prescription="context.prescription" :can-cancel="context.canCancel === true" :blocked="busy || needsReview" @changed="load" />
   </div>

   <section v-if="!editable && context.prescription" class="card prescription-view-card" aria-label="Đơn thuốc đã lưu">
    <div class="card-header">
     <div class="code-wrap">
      <span class="badge badge-info">ĐÃ LƯU</span>
      <h2>{{ context.prescription.prescriptionCode }}</h2>
     </div>
     <span v-if="context.prescription.isCancelled" class="badge badge-danger">ĐÃ HỦY</span>
    </div>
    <p v-if="context.prescription.isCancelled" class="alert alert-error">Đơn đã hủy; không dùng để cấp phát hoặc tra cứu QR.</p>
    <div class="diagnosis-box">
     <strong>Chẩn đoán kê đơn:</strong> {{ context.prescription.diagnosis }}
    </div>
    <div class="table-wrap">
     <table class="prescription-table">
      <thead>
       <tr>
        <th>Thuốc / Hàm lượng</th>
        <th class="text-center">Số lượng</th>
        <th class="text-right">Giá chụp</th>
        <th>Cách dùng & Liều lượng</th>
       </tr>
      </thead>
      <tbody>
       <tr v-for="line in context.prescription.items" :key="line.id">
        <td>
         <div class="drug-name">{{ line.medicineName }}</div>
         <div v-if="line.strength" class="drug-strength">{{ line.strength }}</div>
        </td>
        <td class="text-center font-bold">{{ line.quantity }} {{ line.unit }}</td>
        <td class="text-right">{{ money(line.unitPrice) }}</td>
        <td>
         <div>{{ line.usageInstructions }}</div>
         <small v-if="line.treatmentDays" class="days-tag">Thời gian: {{ line.treatmentDays }} ngày</small>
        </td>
       </tr>
      </tbody>
     </table>
    </div>
    <div class="advice-block" v-if="context.prescription.doctorAdvice || context.prescription.revisitDate">
     <p v-if="context.prescription.doctorAdvice"><strong>Lời dặn của nha sĩ:</strong> {{ context.prescription.doctorAdvice }}</p>
     <p v-if="context.prescription.revisitDate"><strong>Hẹn tái khám:</strong> {{ context.prescription.revisitDate }}</p>
    </div>
   </section>

   <div v-if="!editable" class="info-banner">
    {{ context.prescription ? 'Đơn chỉ đọc với quyền hoặc trạng thái hiện tại.' : 'Chưa có đơn thuốc. Chỉ nha sĩ phụ trách kê đơn khi ca khám cho phép.' }}
   </div>
   <template v-if="editable">
    <PrescriptionStockNotice :visit-id="visitId" :items="draft.map(d => ({ medicineId: d.medicineId, quantity: Number(d.quantity), unit: d.unit }))" />
    <div class="prescription-meta-notice">
     <strong>{{ context.prescription?.prescriptionCode || 'Đơn thuốc mới' }}</strong> · Chỉ lưu sau khi kiểm tra xem trước và xác nhận hợp lệ.
    </div>
    
    <aside v-if="context.allergies.length" class="alert alert-warning allergy-box" aria-label="Tiền sử dị ứng">
     <strong>⚠️ Tiền sử dị ứng đã ghi nhận của bệnh nhân</strong>
     <ul>
      <li v-for="a in context.allergies" :key="a.id">{{ a.name }} {{ a.detail || '' }}</li>
     </ul>
    </aside>
    <div v-else class="alert alert-neutral">
     ℹ️ Chưa ghi nhận tiền sử dị ứng trong hồ sơ bệnh nhân.
    </div>

    <section class="card drug-search-card" aria-label="Tra cứu và thêm thuốc">
     <div class="card-header">
      <h3>Thêm thuốc vào đơn</h3>
      <button class="btn btn-secondary btn-sm" :disabled="busy || needsReview || draft.length > 0" @click="repeat">
       📋 Nạp từ đơn gần nhất
      </button>
     </div>
     <form @submit.prevent="search(1)" class="search-bar">
      <div class="search-input-wrap">
       <label for="drug-search-input">Tìm thuốc hoạt động (tên thuốc hoặc hoạt chất)</label>
       <input id="drug-search-input" v-model="keyword" maxlength="200" placeholder="Nhập tên thuốc..." :disabled="busy || needsReview" />
      </div>
      <button class="btn btn-primary" :disabled="busy || needsReview">Tìm kiếm</button>
     </form>

     <ul v-if="medicines.length" class="choices-list">
      <li v-for="m in medicines" :key="m.id" class="choice-item">
       <div class="choice-info">
        <strong class="choice-name">{{ m.name }} {{ m.strength || '' }}</strong>
        <span class="choice-meta">{{ m.code }} · {{ m.activeIngredient || 'Chưa ghi hoạt chất' }} · {{ money(m.unitPrice) }}/{{ m.unit }}</span>
       </div>
       <button class="btn btn-outline-primary btn-sm" :disabled="busy || needsReview || draft.length >= 10 || draft.some(d => d.medicineId === m.id)" @click="add(m)">
        + Chọn thuốc
       </button>
      </li>
     </ul>
     <div v-if="medicines.length" class="pagination-bar">
      <button class="btn btn-secondary btn-sm" :disabled="busy || needsReview || medicinePage <= 1" @click="search(medicinePage - 1)">Trang trước</button>
      <span class="page-indicator">Trang {{ medicinePage }}</span>
      <button class="btn btn-secondary btn-sm" :disabled="busy || needsReview || !hasNext" @click="search(medicinePage + 1)">Trang sau</button>
     </div>
    </section>

    <form @submit.prevent="previewOrder" class="card editor-card">
     <div class="card-header">
      <h3>Chi tiết đơn thuốc ({{ draft.length }}/10 loại thuốc)</h3>
     </div>
     <div class="form-group">
      <label for="form-diag">Chẩn đoán trên đơn thuốc (bắt buộc)</label>
      <textarea id="form-diag" v-model="form.diagnosis" required maxlength="500" placeholder="Nhập chẩn đoán lâm sàng..." :disabled="busy || needsReview" @input="invalidate" />
     </div>

     <div class="draft-items-list">
      <article v-for="(line, index) in draft" :key="line.medicineId" class="draft-line-card">
       <div class="line-header">
        <div>
         <h4 class="line-title">{{ line.name }} {{ line.strength || '' }}</h4>
         <div class="line-meta">{{ line.code }} · {{ line.activeIngredient || 'Chưa ghi hoạt chất' }} · {{ money(line.unitPrice) }}/{{ line.unit }}</div>
        </div>
        <button type="button" class="btn btn-outline-danger btn-sm" :disabled="busy || needsReview" @click="remove(index)">
         🗑️ Bỏ thuốc
        </button>
       </div>
       <div v-if="!line.active" class="alert alert-error line-alert">
        Thuốc này đã ngừng dùng/chưa có giá; bỏ dòng và chọn thuốc hoạt động.
       </div>
       <div class="line-grid">
        <div class="form-group">
         <label>Số lượng ({{ line.unit }})</label>
         <input v-model="line.quantity" type="number" required min="1" max="100" step="1" :disabled="busy || needsReview" @input="invalidate" />
         <small v-if="Number(line.quantity) > 30" class="warning-text">⚠️ Số lượng trên 30; vui lòng kiểm tra lại.</small>
        </div>
        <div class="form-group">
         <label>Số ngày dùng (1–30)</label>
         <input v-model="line.treatmentDays" type="number" min="1" max="30" step="1" placeholder="VD: 5" :disabled="busy || needsReview" @input="invalidate" />
        </div>
        <div class="form-group col-full">
         <label>Cách dùng & Liều lượng</label>
         <textarea v-model="line.usageInstructions" required maxlength="500" placeholder="VD: Uống ngày 2 lần, mỗi lần 1 viên sau ăn..." :disabled="busy || needsReview" @input="invalidate" />
        </div>
       </div>
       <div class="line-footer">
        <span>Thành tiền: <strong class="line-sum">{{ lineTotal(line) }}</strong></span>
       </div>
      </article>
     </div>

     <div class="form-group">
      <label for="form-advice">Lời dặn chung cho bệnh nhân</label>
      <textarea id="form-advice" v-model="form.doctorAdvice" maxlength="1000" placeholder="Lời dặn về chế độ ăn uống, kiêng cữ..." :disabled="busy || needsReview" @input="invalidate" />
     </div>
     <div class="form-group">
      <label for="form-revisit">Ngày hẹn tái khám (tùy chọn)</label>
      <input id="form-revisit" v-model="form.revisitDate" type="date" :disabled="busy || needsReview" @input="invalidate" />
      <small class="helper-text">Ngày hẹn này ghi trên đơn; không tự tạo lịch hẹn.</small>
     </div>

     <button class="btn btn-primary" :disabled="busy || needsReview">Kiểm tra và xem trước</button>
    </form>

    <section v-if="preview && previewKey === contentKey()" class="card preview-card" aria-label="Xác nhận kê đơn">
     <div class="card-header">
      <h3>Kiểm tra viện phí & Cảnh báo an toàn</h3>
      <span class="badge badge-success">Sẵn sàng lưu</span>
     </div>
     <dl class="fee-summary-grid">
      <dt>Tiền dịch vụ:</dt>
      <dd>{{ money(preview.servicesTotal) }}</dd>
      <dt>Tiền thuốc:</dt>
      <dd>{{ money(preview.medicineTotal) }}</dd>
      <dt>Chiết khấu hiện tại:</dt>
      <dd class="discount-val">-{{ money(preview.discountAmount) }}</dd>
      <dt class="final-lbl">Tổng phải trả:</dt>
      <dd class="final-val">{{ money(preview.finalAmount) }}</dd>
     </dl>

     <div v-for="warning in preview.allergyWarnings" :key="warning.medicineId" class="alert alert-warning allergy-alert-box">
      <p class="warning-title">⚠️ Thuốc {{ drugName(warning.medicineId) }} trùng mục dị ứng: {{ warning.allergyNames.join(', ') }}</p>
      <label class="checkbox-label">
       <input v-model="approvals[warning.medicineId]!.confirmed" type="checkbox" :disabled="busy || needsReview" />
       <span>Tôi xác nhận quyết định chuyên môn vẫn kê thuốc này</span>
      </label>
      <div class="form-group mt-2">
       <label>Lý do chuyên môn (5–500 ký tự)</label>
       <textarea v-model="approvals[warning.medicineId]!.reason" minlength="5" maxlength="500" placeholder="Giải thích lý do y khoa..." :disabled="busy || needsReview" />
      </div>
     </div>

     <div v-if="preview.duplicateIngredients.length" class="alert alert-warning">
      <label class="checkbox-label">
       <input v-model="duplicateConfirmed" type="checkbox" :disabled="busy || needsReview" />
       <span>Tôi đã kiểm tra các thuốc trùng hoạt chất: {{ preview.duplicateIngredients.join(', ') }}</span>
      </label>
     </div>

     <div class="confirm-box">
      <label class="checkbox-label">
       <input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" />
       <span>Tôi đã kiểm tra đơn, cảnh báo lâm sàng và viện phí trước khi lưu</span>
      </label>
     </div>
     <button class="btn btn-success" :disabled="busy || needsReview || !confirmed" @click="save">
      💾 Lưu đơn thuốc và đồng bộ hóa đơn
     </button>
    </section>

    <div v-if="needsReview" class="alert alert-error" role="alert">
     Tải lại hồ sơ và đối chiếu kết quả trước khi gửi tiếp.
    </div>
   </template>
  </template>
 </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import PrescriptionQr from '@/components/PrescriptionQr.vue';
import PrescriptionHandover from '@/components/PrescriptionHandover.vue';
import PrescriptionPrint from '@/components/PrescriptionPrint.vue';
import PrescriptionLifecycle from '@/components/PrescriptionLifecycle.vue';
import PrescriptionStockNotice from '@/components/PrescriptionStockNotice.vue';
import { useAuthStore } from '@/stores/auth';
import { prescriptionsService, type RxContext, type RxMedicine, type RxPreview, type RxRequest } from '@/services/prescriptions';
interface DraftLine { medicineId: number; medicineVersion: number; code: string; name: string; activeIngredient: string | null; strength: string | null; unit: string; unitPrice: string; quantity: string | number; usageInstructions: string; treatmentDays: string | number; active: boolean }
const route = useRoute(), auth = useAuthStore();
const portalPrefix = computed(() => auth.role === 'PATIENT' ? '/patient' : '/management');
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
.rx-page {
  max-width: 980px;
  margin: 1.5rem auto;
  padding: 1.5rem;
  background: #f8fafc;
  color: #1e293b;
  font-family: inherit;
}

.page-header {
  margin-bottom: 1.5rem;
}

.header-nav {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.nav-link {
  display: inline-flex;
  align-items: center;
  font-size: 0.9rem;
  font-weight: 600;
  color: #0284c7;
  text-decoration: none;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  background: #e0f2fe;
  transition: all 0.15s ease-in-out;
}

.nav-link.alt {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.nav-link:hover {
  text-decoration: none;
  filter: brightness(0.95);
}

.header-main {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
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
  font-size: 1.65rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
  line-height: 1.3;
}

.visit-meta-tag {
  font-size: 0.9rem;
  color: #64748b;
  margin: 0;
}

.card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
  margin: 1.25rem 0;
  box-shadow: 0 2px 8px rgba(15, 45, 89, 0.04);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  gap: 0.5rem;
}

.card-header h2,
.card-header h3 {
  color: #0f2d59;
  font-weight: 700;
  margin: 0;
}

.code-wrap {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.badge {
  padding: 0.2rem 0.55rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
}

.badge-info { background: #e0f2fe; color: #0369a1; }
.badge-danger { background: #fee2e2; color: #991b1b; }
.badge-success { background: #dcfce7; color: #166534; }

.prescription-management-strip {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin: 1rem 0;
}

.diagnosis-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  margin-bottom: 1rem;
  font-size: 0.95rem;
}

.table-wrap {
  overflow-x: auto;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  margin-bottom: 1rem;
}

.prescription-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.prescription-table th,
.prescription-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid #e2e8f0;
  font-size: 0.92rem;
}

.prescription-table th {
  background: #f8fafc;
  color: #0f2d59;
  font-weight: 600;
}

.drug-name {
  font-weight: 600;
  color: #0f2d59;
}

.drug-strength {
  font-size: 0.8rem;
  color: #64748b;
}

.days-tag {
  display: inline-block;
  color: #0369a1;
  font-size: 0.8rem;
  font-weight: 500;
}

.advice-block {
  background: #f8fafc;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  border-left: 3px solid #0284c7;
  font-size: 0.92rem;
}

.advice-block p {
  margin: 0.25rem 0;
}

.info-banner {
  background: #f1f5f9;
  color: #475569;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-size: 0.92rem;
  margin: 1rem 0;
  text-align: center;
}

.prescription-meta-notice {
  font-size: 0.9rem;
  color: #64748b;
  margin: 0.5rem 0;
}

.search-bar {
  display: flex;
  gap: 0.75rem;
  align-items: flex-end;
  margin-bottom: 1rem;
}

.search-input-wrap {
  flex: 1;
}

.choices-list {
  list-style: none;
  padding: 0;
  margin: 0.75rem 0;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  overflow: hidden;
}

.choice-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid #e2e8f0;
  background: #ffffff;
}

.choice-item:last-child {
  border-bottom: none;
}

.choice-info {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.choice-name {
  color: #0f2d59;
  font-size: 0.95rem;
}

.choice-meta {
  font-size: 0.82rem;
  color: #64748b;
}

.pagination-bar {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 1rem;
  margin-top: 0.75rem;
}

.page-indicator {
  font-size: 0.9rem;
  font-weight: 600;
  color: #475569;
}

.draft-items-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.draft-line-card {
  background: #f8fafc;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 1rem;
}

.line-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 0.75rem;
  gap: 0.5rem;
}

.line-title {
  color: #0f2d59;
  font-size: 1.05rem;
  font-weight: 700;
  margin: 0 0 0.2rem;
}

.line-meta {
  font-size: 0.82rem;
  color: #64748b;
}

.line-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
}

.col-full {
  grid-column: 1 / -1;
}

.line-footer {
  text-align: right;
  margin-top: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid #e2e8f0;
  font-size: 0.9rem;
}

.line-sum {
  color: #0f2d59;
  font-size: 1rem;
}

.fee-summary-grid {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.5rem;
  background: #f8fafc;
  padding: 1rem;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  margin-bottom: 1rem;
}

.fee-summary-grid dt {
  color: #475569;
  font-size: 0.95rem;
}

.fee-summary-grid dd {
  margin: 0;
  font-weight: 600;
  color: #0f2d59;
  text-align: right;
}

.discount-val {
  color: #059669 !important;
}

.final-lbl {
  font-weight: 700;
  color: #0f2d59;
  font-size: 1.05rem !important;
  border-top: 1px solid #cbd5e1;
  padding-top: 0.4rem;
}

.final-val {
  font-weight: 700;
  color: #0284c7 !important;
  font-size: 1.15rem !important;
  border-top: 1px solid #cbd5e1;
  padding-top: 0.4rem;
}

.allergy-alert-box {
  margin-bottom: 1rem;
}

.warning-title {
  font-weight: 700;
  margin: 0 0 0.5rem;
}

.confirm-box {
  margin: 1.25rem 0;
  padding: 0.75rem 1rem;
  background: #f1f5f9;
  border-radius: 8px;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  font-size: 0.9rem;
  font-weight: 600;
  color: #334155;
  margin-bottom: 0.4rem;
}

.helper-text {
  display: block;
  font-size: 0.8rem;
  color: #64748b;
  margin-top: 0.25rem;
}

.warning-text {
  color: #d97706;
  font-size: 0.82rem;
  font-weight: 500;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.92rem;
  font-weight: 500;
  color: #334155;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  width: 1.15rem;
  height: 1.15rem;
  accent-color: #0284c7;
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
  border: 1px solid transparent;
}

.btn-sm {
  min-height: 36px;
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: #0f2d59;
  color: #ffffff;
}

.btn-primary:hover:not(:disabled) {
  background: #1e3a8a;
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

.btn-outline-primary {
  background: #ffffff;
  color: #0284c7;
  border: 1px solid #0284c7;
}

.btn-outline-primary:hover:not(:disabled) {
  background: #e0f2fe;
}

.btn-outline-danger {
  background: #ffffff;
  color: #b91c1c;
  border: 1px solid #fecaca;
}

.btn-outline-danger:hover:not(:disabled) {
  background: #fef2f2;
}

.btn-success {
  background: #059669;
  color: #ffffff;
}

.btn-success:hover:not(:disabled) {
  background: #047857;
}

.btn:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

input:not([type="checkbox"]):not([type="radio"]),
select,
textarea {
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

textarea {
  min-height: 80px;
  resize: vertical;
}

input:focus-visible,
select:focus-visible,
textarea:focus-visible {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.2);
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-weight: 500;
  font-size: 0.95rem;
  margin-bottom: 1rem;
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

.alert-success {
  color: #166534;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
}

.alert-warning {
  color: #92400e;
  background: #fffbeb;
  border: 1px solid #fde68a;
}

.alert-neutral {
  color: #475569;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
}

.font-bold { font-weight: 700; }
.text-right { text-align: right; }
.text-center { text-align: center; }
.mt-2 { margin-top: 0.5rem; }

@media (max-width: 640px) {
  .rx-page {
    padding: 1rem 0.75rem;
  }
  .header-main {
    flex-direction: column;
    align-items: flex-start;
  }
  .header-actions {
    width: 100%;
  }
  .header-actions .btn {
    width: 100%;
  }
  .search-bar {
    flex-direction: column;
    align-items: stretch;
  }
  .line-grid {
    grid-template-columns: 1fr;
  }
  .btn {
    width: 100%;
  }
}
</style>
