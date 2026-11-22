<template>
  <section v-if="allowed" class="qr-intake" aria-label="Tiếp nhận bằng QR CCCD">
    <div class="intake-heading">
      <div>
        <h2>Tiếp nhận bằng QR CCCD</h2>
        <p>Quét và đối chiếu giấy tờ để tìm hoặc tạo hồ sơ bệnh nhân.</p>
      </div>
      <button type="button" class="secondary-button" :aria-expanded="opened" :disabled="busy" @click="toggle">
        {{ opened ? 'Đóng tiếp nhận QR' : 'Tiếp nhận bằng QR CCCD' }}
      </button>
    </div>

    <div v-if="opened" class="intake-body">
      <form class="scan-panel" @submit.prevent="scan()">
        <label class="field">
          <span>Quét mã CCCD</span>
          <input ref="scanInput" v-model="raw" type="password" maxlength="1024" autocomplete="off" :disabled="busy || cameraOn" />
          <span class="field-hint">Đặt con trỏ vào ô, quét bằng đầu đọc USB và nhấn Enter.</span>
        </label>
        <div class="actions">
          <button class="primary-button" :disabled="busy || cameraOn || !raw">Đọc và đối soát</button>
          <button type="button" class="secondary-button" :disabled="busy || cameraOn" @click="startCamera">📷 Quét bằng camera</button>
        </div>
        <p class="manual-help">Để nhập thông tin thủ công, đóng tiếp nhận QR và chọn “Thêm bệnh nhân” trên trang hồ sơ.</p>
      </form>

      <!-- Reusable Real-Time Dual-Engine Camera CCCD Scanner -->
      <CameraCccdScanner
        :is-open="cameraOn"
        @close="cameraOn = false"
        @success="handleCameraScanSuccess"
      />

      <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
      <p v-if="message" class="alert alert-success" role="status">{{ message }}</p>
      <p v-if="busy" class="processing" role="status">Đang xử lý…</p>

      <div v-if="existing" class="existing-patient">
        <h3>Hồ sơ đã có</h3>
        <p><strong>{{ existing.fullName }}</strong> · {{ existing.patientCode }}</p>
        <p class="masked-id">CCCD {{ existing.masked }}</p>
        <p v-if="!existing.isActive" class="alert alert-warning">Hồ sơ đang ngừng hoạt động; mở hồ sơ để đối chiếu, không tạo trùng.</p>
        <div class="actions">
          <RouterLink class="primary-button" :to="{name:'management-patient-detail',params:{id:existing.patientId}}" @click="close">Mở hồ sơ &amp; tiếp đón</RouterLink>
          <button type="button" class="secondary-button" @click="close">Hủy</button>
        </div>
      </div>

      <form v-if="identity && !existing" class="identity-form" @submit.prevent="save">
        <div class="identity-heading">
          <h3>Đối chiếu thông tin</h3>
          <p class="masked-id">CCCD {{ identity.citizenIdMasked }}</p>
        </div>
        <div class="field-grid">
          <label class="field field-wide"><span>Họ và tên</span><input v-model="form.fullName" required maxlength="100" :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field"><span>Ngày sinh</span><input v-model="form.dateOfBirth" type="date" required :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field"><span>Giới tính</span><select v-model="form.gender" :disabled="busy || needsReview" @change="invalidate"><option value="Male">Nam</option><option value="Female">Nữ</option></select></label>
          <label class="field field-wide"><span>SĐT (nhập tay)</span><input ref="phoneInput" v-model="form.phone" type="tel" inputmode="numeric" required pattern="0[0-9]{9}" maxlength="10" autocomplete="off" :disabled="busy || needsReview" @input="invalidate" /></label>
          <label class="field field-wide"><span>Địa chỉ <span class="optional">(không bắt buộc)</span></span><textarea v-model="form.address" maxlength="500" rows="2" :disabled="busy || needsReview" @input="invalidate" /></label>
        </div>
        <div v-if="duplicates.length" class="alert alert-warning">
          <p>SĐT đã được dùng bởi các hồ sơ sau. Đối chiếu CCCD để tránh tạo trùng hồ sơ.</p>
          <ul><li v-for="item in duplicates" :key="item.patientCode">{{ item.patientCode }} · {{ item.fullName }} · {{ item.dateOfBirth }}</li></ul>
          <label class="confirmation"><input v-model="duplicateIdentityConfirmed" type="checkbox" :disabled="busy || needsReview" /><span>Đã đối chiếu CCCD: đây là người khác dùng chung SĐT, cần tạo hồ sơ độc lập</span></label>
        </div>
        <div class="form-footer">
          <label class="confirmation"><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /><span>Tôi đã đối chiếu thông tin với bệnh nhân</span></label>
          <button class="primary-button" :disabled="busy || needsReview || !confirmed || (duplicates.length>0 && !duplicateIdentityConfirmed)">Tạo hồ sơ</button>
        </div>
        <p v-if="needsReview" class="alert alert-warning">Chưa xác minh kết quả. Quét lại để đối soát; không gửi tạo mới lần nữa.</p>
      </form>
    </div>
  </section>
</template>
<script setup lang="ts">
import { computed, nextTick, onUnmounted, reactive, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import CameraCccdScanner from '@/components/CameraCccdScanner.vue';
import { decodeCitizenQr, validQrBirthDate, qrPatientIntakeService, type ScannedIdentity, type QrExistingPatient } from '@/services/qrPatientIntake';
import { isUnderFourteen } from '@/services/patientAge';
import type { PatientDuplicateInfo } from '@/services/patients';

const emit = defineEmits<{created:[]}>();
const auth = useAuthStore();
const allowed = computed(() => ['ADMIN','RECEPTIONIST'].includes(auth.role));
const opened = ref(false);
const raw = ref('');
const busy = ref(false);
const error = ref('');
const message = ref('');
const confirmed = ref(false);
const duplicateIdentityConfirmed = ref(false);
const needsReview = ref(false);

const identity = ref<ScannedIdentity|null>(null);
const existing = ref<QrExistingPatient|null>(null);
const duplicates = ref<PatientDuplicateInfo[]>([]);
const scanInput = ref<HTMLInputElement|null>(null);
const phoneInput = ref<HTMLInputElement|null>(null);
const cameraOn = ref(false);
const form = reactive({ fullName: '', dateOfBirth: '', gender: 'Male', phone: '', address: '' });

let generation = 0;
let disposed = false;
const actor = () => auth.user?.userId + '/' + auth.role;

function alive(ticket: number, owner: string) {
  return !disposed && opened.value && allowed.value && generation === ticket && actor() === owner;
}

function clear() {
  raw.value = '';
  identity.value = null;
  existing.value = null;
  duplicates.value = [];
  confirmed.value = false;
  duplicateIdentityConfirmed.value = false;
  needsReview.value = false;
  error.value = '';
  message.value = '';
  Object.assign(form, { fullName: '', dateOfBirth: '', gender: 'Male', phone: '', address: '' });
}

function invalidate() {
  confirmed.value = false;
  duplicateIdentityConfirmed.value = false;
  duplicates.value = [];
}

function stopCamera() {
  cameraOn.value = false;
}

function close() {
  generation++;
  stopCamera();
  opened.value = false;
  busy.value = false;
  clear();
}

async function toggle() {
  if (opened.value) {
    close();
  } else {
    opened.value = true;
    clear();
    await nextTick();
    scanInput.value?.focus();
  }
}

function startCamera() {
  if (disposed || busy.value || !allowed.value || !opened.value) return;
  clear();
  cameraOn.value = true;
}

function handleCameraScanSuccess(data: { raw: string; identity: ScannedIdentity }) {
  cameraOn.value = false;
  scan(data.raw);
}

async function scan(value?: string) {
  if (disposed || busy.value || !allowed.value || !opened.value) return;
  const input = value ?? raw.value;
  const ticket = ++generation;
  const owner = actor();
  stopCamera();
  clear();
  busy.value = true;
  try {
    const decoded = await decodeCitizenQr(input);
    if (!alive(ticket, owner)) return;
    if (isUnderFourteen(decoded.dateOfBirth)) {
      error.value = 'Người dưới 14 tuổi không sử dụng CCCD để tạo hồ sơ. Vui lòng tạo hồ sơ trẻ em và xác nhận người giám hộ.';
      return;
    }
    const result = await qrPatientIntakeService.lookup(decoded.citizenId);
    if (!alive(ticket, owner)) return;
    if (!result || !('existing' in result) || result.existing === undefined) throw Error();
    if (result.existing) {
      const item = result.existing;
      if (!Number.isSafeInteger(item.patientId) || item.patientId <= 0 || !/^BN\d+$/.test(item.patientCode) || typeof item.fullName !== 'string' || !/^\d{3}\*{6}\d{3}$/.test(item.masked) || typeof item.isActive !== 'boolean') throw Error();
      existing.value = item;
    } else {
      identity.value = decoded;
      Object.assign(form, { fullName: decoded.fullName, dateOfBirth: decoded.dateOfBirth, gender: decoded.gender, address: decoded.address, phone: '' });
      await nextTick();
      if (alive(ticket, owner)) phoneInput.value?.focus();
    }
  } catch {
    if (alive(ticket, owner)) error.value = 'Không đọc hoặc đối soát được QR CCCD. Kiểm tra mã và kết nối, quét lại hoặc chuyển sang nhập tay.';
  } finally {
    if (alive(ticket, owner)) busy.value = false;
  }
}

async function save() {
  if (disposed || busy.value || needsReview.value || !opened.value || !allowed.value || !identity.value || existing.value || !confirmed.value || (duplicates.value.length && !duplicateIdentityConfirmed.value)) return;
  if (!form.fullName.trim() || form.fullName.length > 100 || !/^0[0-9]{9}$/.test(form.phone) || form.phone.length !== 10 || !validQrBirthDate(form.dateOfBirth) || !['Male','Female'].includes(form.gender) || form.address.length > 500) {
    error.value = 'Kiểm tra họ tên, ngày sinh, SĐT 10 số và địa chỉ.';
    return;
  }
  if (isUnderFourteen(form.dateOfBirth)) {
    error.value = 'Người dưới 14 tuổi không sử dụng CCCD để tạo hồ sơ. Vui lòng tạo hồ sơ trẻ em và xác nhận người giám hộ.';
    return;
  }
  const ticket = ++generation;
  const owner = actor();
  const id = identity.value;
  busy.value = true;
  error.value = '';
  try {
    const result = await qrPatientIntakeService.create(id, { ...form, gender: form.gender, email: null, confirmNotDuplicate: duplicateIdentityConfirmed.value });
    if (!alive(ticket, owner)) return;
    if (!Number.isSafeInteger(result.patientId) || result.patientId <= 0 || !/^BN\d+$/.test(result.patientCode)) throw Error();
    clear();
    existing.value = { patientId: result.patientId, patientCode: result.patientCode, fullName: result.fullName, masked: id.citizenIdMasked, isActive: result.isActive };
    message.value = 'Đã tạo hồ sơ. Mở hồ sơ để tiếp đón theo luồng hiện có.';
    emit('created');
  } catch(failure: unknown) {
    if (!alive(ticket, owner)) return;
    const data = (failure as { response?: { data?: { code?: string; duplicates?: PatientDuplicateInfo[] } } })?.response?.data;
    confirmed.value = false;
    if (data?.code === 'PATIENT_POSSIBLE_DUPLICATE' && Array.isArray(data.duplicates) && data.duplicates.length > 0 && data.duplicates.length <= 20) {
      duplicates.value = data.duplicates;
      duplicateIdentityConfirmed.value = false;
      error.value = 'Đối chiếu danh sách hồ sơ trùng trước khi tiếp tục.';
    } else {
      needsReview.value = true;
      error.value = 'Trạng thái có thể đã đổi hoặc chưa xác minh kết quả. Quét lại để đối soát trước khi tạo hồ sơ.';
    }
  } finally {
    if (alive(ticket, owner)) busy.value = false;
  }
}

watch(actor, close);
onUnmounted(() => { disposed = true; close(); });
</script>

<style scoped>
.qr-intake {
  margin: 1rem 0;
  border: 1px solid var(--color-border-subtle, #c8deeb);
  border-radius: var(--radius-xl, 16px);
  background: var(--color-surface-card, #fff);
  color: var(--color-text-main, #18354c);
  overflow-wrap: anywhere;
}

.intake-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
}

.intake-heading h2 {
  font-size: 1.15rem;
  margin: 0;
  color: var(--color-primary, #102e56);
}

.intake-heading p {
  margin: 0.25rem 0 0;
  font-size: 0.85rem;
  color: var(--color-text-subtle, #64748b);
}

.intake-body {
  padding: 1.25rem;
}

.scan-panel {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.field > span {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-text-main, #0f172a);
}

.field input,
.field select,
.field textarea {
  padding: 0.65rem 0.85rem;
  border: 1px solid var(--color-border-medium, #cbd5e1);
  border-radius: var(--radius-md, 8px);
  font-size: 0.95rem;
}

.field-hint,
.manual-help {
  font-size: 0.8rem;
  color: var(--color-text-subtle, #64748b);
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.primary-button {
  background: var(--color-primary-hover, #1857b6);
  color: #fff;
  border: none;
  padding: 0.65rem 1.25rem;
  border-radius: var(--radius-md, 8px);
  font-weight: 600;
  cursor: pointer;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
}

.primary-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.secondary-button {
  background: var(--color-surface-subtle, #f8fafc);
  border: 1px solid var(--color-border-medium, #cbd5e1);
  color: var(--color-text-main, #0f172a);
  padding: 0.65rem 1.25rem;
  border-radius: var(--radius-md, 8px);
  font-weight: 600;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
}

.secondary-button:hover:not(:disabled) {
  background: var(--color-surface-muted, #e2e8f0);
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md, 8px);
  font-size: 0.875rem;
  margin: 1rem 0;
}

.alert-error {
  background: var(--color-danger-light, #fee2e2);
  color: var(--color-danger, #dc2626);
  border: 1px solid var(--color-danger-border, #fca5a5);
}

.alert-success {
  background: var(--color-success-light, #dcfce7);
  color: var(--color-success, #16a34a);
  border: 1px solid var(--color-success-border, #86efac);
}

.alert-warning {
  background: var(--color-warning-light, #fff7ed);
  color: var(--color-warning, #ea580c);
  border: 1px solid var(--color-warning-border, #fed7aa);
}

.existing-patient {
  margin-top: 1rem;
  padding: 1rem;
  border-radius: var(--radius-md, 8px);
  background: var(--color-surface-subtle, #f8fafc);
  border: 1px solid var(--color-border-subtle, #e2e8f0);
}

.masked-id {
  font-family: var(--font-family-mono, monospace);
  font-weight: 600;
}

.identity-form {
  margin-top: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.field-wide {
  grid-column: 1 / -1;
}

.form-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  margin-top: 1rem;
}

.confirmation {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  cursor: pointer;
}

@media (max-width: 640px) {
  .field-grid {
    grid-template-columns: 1fr;
  }
  .form-footer {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
