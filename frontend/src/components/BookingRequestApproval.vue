<template>
  <section class="approval" aria-labelledby="booking-approval-heading">
    <header class="approval-toolbar">
      <div class="approval-title-block">
        <h2 id="booking-approval-heading">Đối soát hồ sơ &amp; xác nhận lịch</h2>
        <p class="approval-sub">Chọn hồ sơ bệnh nhân phù hợp hoặc tạo hồ sơ mới sau khi đối chiếu.</p>
      </div>
      <button type="button" class="approval-reload-btn" :disabled="busy" @click="load">
        🔄 Đối chiếu lại yêu cầu
      </button>
    </header>

    <p v-if="error" class="approval-alert approval-alert--error" role="alert">{{ error }}</p>
    <p class="approval-status" :class="{ 'approval-status--empty': !message }" role="status" aria-live="polite">
      {{ message }}
    </p>

    <Transition name="approval-fade" mode="out-in">
      <div v-if="context && !context.result" key="context" class="approval-body">
        <div class="approval-summary-grid">
          <div class="approval-summary-item">
            <span class="summary-label">Người yêu cầu</span>
            <div class="summary-name-row">
              <strong>{{ context.fullName }}</strong>
              <span v-if="isChildContext" class="approval-child-badge">👶 Trẻ em (&lt; 14 tuổi)</span>
              <span v-if="isChildContext" class="approval-missing-badge">⚠️ Chưa liên kết giám hộ</span>
            </div>
            <span class="summary-meta">🎂 {{ context.dateOfBirth }} · 📞 {{ context.phone }}</span>
          </div>
          <div class="approval-summary-item">
            <span class="summary-label">Buổi khám dự kiến</span>
            <strong class="summary-slot">📅 {{ context.appointmentDate }} · 🌤️ {{ formatSessionLabel(context.slotTime) }}</strong>
            <span class="summary-meta">Khám trong {{ context.slotTime.slice(0, 5) < '12:00' ? 'Buổi sáng (08:00 – 12:00)' : 'Buổi chiều (13:30 – 20:00)' }}</span>
          </div>
          <div v-if="isChildContext || parsedNotes.guardian" class="approval-summary-item approval-summary-item--full">
            <span class="summary-label">Thông tin giám hộ &amp; ghi chú</span>
            <strong>👨‍👩‍👧 Người giám hộ: {{ parsedNotes.guardian || 'Chưa ghi nhận — đối chiếu tại quầy khi trẻ đến' }}</strong>
            <span v-if="parsedNotes.note" class="summary-meta">📝 Ghi chú: {{ parsedNotes.note }}</span>
          </div>
        </div>

        <p class="approval-policy-note">
          <template v-if="isChildContext">
            Trẻ dưới 14 tuổi không yêu cầu CCCD và không tạo tài khoản riêng (đánh dấu <strong>Thiếu tài khoản</strong> nếu chưa gắn người giám hộ). Khi trẻ đến khám, Lễ tân xác minh và liên kết tài khoản người giám hộ tại quầy.
          </template>
          <template v-else>
            Tài khoản bệnh nhân được tạo hoặc chuyển tại quầy sau khi lễ tân đối chiếu CCCD và số điện thoại lúc check-in.
          </template>
        </p>

        <p v-if="context.blockReason" class="approval-alert approval-alert--error" role="alert">
          {{ context.blockReason }}
        </p>

        <form v-else class="approval-form" @submit.prevent="approve">
          <label class="approval-field">
            <span class="field-title">Hồ sơ nhận lịch</span>
            <select v-model="selection" :disabled="busy || needsReview || context.verifiedPatientId !== null" required>
              <option value="">Chọn sau khi đối soát</option>
              <option
                v-for="p in context.patients"
                :key="p.patientId"
                :value="String(p.patientId)"
                :disabled="!canSelectCandidate(p)"
              >
                {{ p.fullName }} · {{ p.dateOfBirth }} · #{{ p.patientId }}{{ candidateDisabledLabel(p) }}
              </option>
              <option v-if="context.verifiedPatientId === null" value="new">
                Tạo hồ sơ mới theo thông tin yêu cầu
              </option>
            </select>
          </label>

          <p v-if="context.patients.length" class="approval-candidate-hint">
            Đã tìm thấy hồ sơ tương đồng. Đối chiếu từng người; không tạo trùng hoặc chuyển hồ sơ đã có chủ.
          </p>
          <p v-if="hasAgeMismatchCandidates" class="approval-candidate-hint">
            Hồ sơ cùng SĐT nhưng khác nhóm tuổi (&lt; 14 / ≥ 14) bị khóa chọn để tránh gắn nhầm trẻ em vào hồ sơ người lớn. Chọn "Tạo hồ sơ mới theo thông tin yêu cầu" nếu đây là người mới.
          </p>

          <div class="approval-checks">
            <label v-if="selection === 'new'" class="approval-check-card">
              <input v-model="confirmedNew" type="checkbox" :disabled="busy || needsReview" />
              <span>Tôi đã đối chiếu hồ sơ tương đồng và xác nhận đây là người mới cần tạo hồ sơ</span>
            </label>
            <label v-if="context.requiresCounterConfirmation" class="approval-check-card">
              <input v-model="counter" type="checkbox" :disabled="busy || needsReview" />
              <span>Khách/người đại diện đang tại quầy; tôi đã đối soát và xác nhận đúng hồ sơ bệnh nhân</span>
            </label>
            <label class="approval-check-card">
              <input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" />
              <span>Tôi đã đối chiếu đúng người và giờ hẹn để xác nhận lịch</span>
            </label>
          </div>

          <button type="submit" class="approval-submit-btn" :disabled="busy || needsReview">
            {{ busy ? 'Đang xác nhận lịch…' : '✓ Xác nhận lịch đã hẹn' }}
          </button>
        </form>
      </div>

      <div v-else-if="context?.result" key="result" class="approval-result-card">
        <strong>✅ Đã xác nhận lịch hẹn #{{ context.result.appointmentId }}</strong>
        <p v-if="isChildContext">Trẻ dưới 14 tuổi không tạo tài khoản riêng (trạng thái Thiếu tài khoản nếu chưa liên kết người giám hộ); người giám hộ sẽ được xác minh và liên kết khi trẻ đến khám.</p>
        <p v-else>Tài khoản được xử lý tại quầy khi khách check-in và đối chiếu CCCD/SĐT.</p>
      </div>

      <div v-else-if="busy" key="loading" class="approval-loading-state">
        <span class="approval-spinner" aria-hidden="true"></span>
        <span>Đang tải dữ liệu đối soát hồ sơ…</span>
      </div>
    </Transition>
  </section>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { bookingRequestsService, type BookingApprovalContext, type BookingApprovalResult } from '@/services/bookingRequests';
import { isUnderFourteen } from '@/services/patientAge';

function isChildBirthDate(dateOfBirth?: string | null): boolean {
  return typeof isUnderFourteen === 'function' && !!dateOfBirth && isUnderFourteen(dateOfBirth);
}

function isAgeGroupMismatch(dateOfBirth?: string | null): boolean {
  return Boolean(context.value && dateOfBirth && isChildBirthDate(dateOfBirth) !== isChildContext.value);
}

function canSelectCandidate(p: { dateOfBirth?: string | null; canSelect: boolean }): boolean {
  return p.canSelect && !isAgeGroupMismatch(p.dateOfBirth);
}

function candidateDisabledLabel(p: { dateOfBirth?: string | null; canSelect: boolean }): string {
  if (canSelectCandidate(p)) return '';
  return isAgeGroupMismatch(p.dateOfBirth) ? ' · Khác nhóm tuổi (< 14 / ≥ 14)' : ' · Không đủ điều kiện';
}

function formatSessionLabel(slot?: string | null): string {
  if (!slot) return 'Chưa chọn buổi';
  return slot.slice(0, 5) < '12:00' ? 'Buổi sáng' : 'Buổi chiều';
}

function parseRequestNotes(notes?: string | null): { guardian: string | null; note: string | null } {
  if (!notes) return { guardian: null, note: null };
  const match = notes.match(/\[Giám hộ:\s*([^\]]+)\]/);
  const guardian = match ? match[1].trim() : null;
  const cleaned = notes.replace(/\[Giám hộ:\s*([^\]]+)\]/g, '').trim();
  return { guardian, note: cleaned || null };
}

const props = defineProps<{ requestId: number }>();
const emit = defineEmits<{ approved: [result: BookingApprovalResult]; working: [value: boolean] }>();
const auth = useAuthStore();
const context = ref<BookingApprovalContext | null>(null), selection = ref(''), counter = ref(false), confirmed = ref(false), confirmedNew = ref(false);
const isChildContext = computed(() => Boolean(context.value && (context.value.isChildPatient || isChildBirthDate(context.value.dateOfBirth))));
const hasAgeMismatchCandidates = computed(() => Boolean(context.value?.patients.some(p => isAgeGroupMismatch(p.dateOfBirth))));
const parsedNotes = computed(() => parseRequestNotes(context.value?.notes));
const busy = ref(false), needsReview = ref(true), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}/${props.requestId}`;
const allowed = () => ['ADMIN', 'RECEPTIONIST'].includes(auth.role);
const alive = (t: number, owner: string) => !disposed && t === generation && owner === identity() && allowed();
function clear() { context.value = null; selection.value = ''; counter.value = false; confirmed.value = false; confirmedNew.value = false; }
const validResult = (r: BookingApprovalResult) => r && Number.isSafeInteger(r.appointmentId) && r.appointmentId > 0 && Number.isSafeInteger(r.patientId) && r.patientId > 0 && typeof r.createdAccount === 'boolean' && typeof r.requiresPhoneActivation === 'boolean';
async function load() {
  if (disposed || busy.value || !allowed() || !Number.isSafeInteger(props.requestId) || props.requestId < 1) return;
  const t = ++generation, owner = identity(); clear(); busy.value = true; needsReview.value = true; error.value = ''; message.value = '';
  try {
    const data = await bookingRequestsService.approval(props.requestId);
    if (!alive(t, owner)) return;
    if (data.id !== props.requestId || !Number.isSafeInteger(data.version) || data.version < 1 || !/^[A-F0-9]{64}$/.test(data.reviewStamp) || !Array.isArray(data.patients) || data.patients.length > 20 || data.result && !validResult(data.result)) throw Error();
    context.value = data; selection.value = data.verifiedPatientId ? String(data.verifiedPatientId) : ''; needsReview.value = false;
  } catch { if (alive(t, owner)) error.value = 'Không tải được thông tin đối soát hoặc bạn không có quyền.'; }
  finally { if (alive(t, owner)) busy.value = false; }
}
async function approve() {
  const c = context.value;
  if (disposed || busy.value || needsReview.value || !allowed() || !c || c.id !== props.requestId || c.result || c.blockReason) return;
  const patientId = selection.value === 'new' ? null : Number(selection.value);
  if (!confirmed.value || patientId === null && (!confirmedNew.value || c.verifiedPatientId !== null) || patientId !== null && !c.patients.some(p => p.patientId === patientId && canSelectCandidate(p)) || c.requiresCounterConfirmation && !counter.value) {
    error.value = 'Chọn đúng hồ sơ và hoàn tất các xác nhận đối soát trước khi gửi.'; return;
  }
  const t = generation, owner = identity(); busy.value = true; emit('working', true); error.value = '';
  const body = { expectedVersion: c.version, reviewStamp: c.reviewStamp, patientId, confirmedNewPatient: patientId === null, confirmedAtCounter: counter.value, confirmed: true };
  confirmed.value = false;
  try {
    const result = await bookingRequestsService.approve(c.id, body);
    if (!alive(t, owner)) return;
    if (!validResult(result)) throw Error();
    clear(); needsReview.value = true; emit('approved', result);
  } catch { if (alive(t, owner)) { error.value = 'Chưa xác minh kết quả hoặc dữ liệu đã đổi. Bấm Đối chiếu lại yêu cầu trước khi xử lý tiếp.'; needsReview.value = true; counter.value = false; confirmedNew.value = false; } }
  finally { if (alive(t, owner)) { busy.value = false; emit('working', false); } }
}
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; message.value = ''; if (!disposed) void load(); });
watch(selection, () => { confirmed.value = false; confirmedNew.value = false; });
onMounted(() => void load()); onUnmounted(() => { disposed = true; generation++; clear(); emit('working', false); });
</script>
<style scoped>
.approval {
  display: grid;
  gap: 0.9rem;
  color: #1e293b;
}

.approval-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.approval-title-block h2 {
  margin: 0;
  font-size: 1.08rem;
  font-weight: 750;
  color: #0f2d59;
}

.approval-sub {
  margin: 0.2rem 0 0;
  font-size: 0.84rem;
  color: #51647a;
}

.approval-reload-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 40px;
  padding: 0.45rem 0.85rem;
  font-size: 0.86rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #102e56;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease;
}

.approval-reload-btn:hover:not(:disabled) {
  background: #f8fafc;
  border-color: #94a3b8;
}

.approval-status {
  margin: 0;
  padding: 0.65rem 0.9rem;
  border-radius: 9px;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #166534;
  font-size: 0.88rem;
  font-weight: 500;
}

.approval-status--empty {
  display: none;
}

.approval-alert {
  margin: 0;
  padding: 0.75rem 1rem;
  border-radius: 9px;
  font-size: 0.9rem;
  font-weight: 500;
}

.approval-alert--error {
  color: #b91c1c;
  background: #fef2f2;
  border: 1px solid #fecaca;
}

.approval-body {
  display: grid;
  gap: 0.9rem;
}

.approval-summary-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.approval-summary-item {
  display: grid;
  gap: 0.22rem;
  padding: 0.75rem 0.9rem;
  border: 1px solid #dbe5ed;
  border-radius: 10px;
  background: #f8fafc;
  min-width: 0;
  overflow-wrap: anywhere;
}

.summary-label {
  font-size: 0.74rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #64748b;
}

.summary-name-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
}

.approval-child-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
  font-size: 0.74rem;
  font-weight: 700;
}

.approval-missing-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: #fef3c7;
  color: #92400e;
  border: 1px solid #f59e0b;
  font-size: 0.74rem;
  font-weight: 700;
}

.approval-summary-item--full {
  grid-column: 1 / -1;
}

.approval-summary-item strong {
  font-size: 0.95rem;
  color: #0f2d59;
}

.summary-slot {
  color: #0369a1 !important;
}

.summary-meta {
  font-size: 0.82rem;
  color: #475569;
}

.approval-policy-note {
  margin: 0;
  padding: 0.65rem 0.85rem;
  border-left: 3px solid #0284c7;
  border-radius: 0 8px 8px 0;
  background: #f0f9ff;
  color: #0c4a6e;
  font-size: 0.84rem;
  line-height: 1.45;
}

.approval-form {
  display: grid;
  gap: 0.85rem;
}

.approval-field {
  display: grid;
  gap: 0.35rem;
}

.field-title {
  font-size: 0.88rem;
  font-weight: 700;
  color: #1e293b;
}

select {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.625rem 0.875rem;
  font-size: 0.94rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

select:focus-visible,
.approval-reload-btn:focus-visible,
.approval-submit-btn:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

.approval-candidate-hint {
  margin: 0;
  padding: 0.65rem 0.85rem;
  border-radius: 8px;
  border: 1px solid #fde68a;
  background: #fffbeb;
  color: #92400e;
  font-size: 0.84rem;
  line-height: 1.45;
}

.approval-checks {
  display: grid;
  gap: 0.55rem;
}

.approval-check-card {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
  padding: 0.7rem 0.85rem;
  border: 1px solid #dbe5ed;
  border-radius: 9px;
  background: #f8fafc;
  font-size: 0.88rem;
  line-height: 1.45;
  color: #1e293b;
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}

.approval-check-card:hover {
  border-color: #93c5fd;
  background: #f0f9ff;
}

.approval-check-card:has(input:checked) {
  border-color: #16a34a;
  background: #f0fdf4;
  color: #14532d;
}

.approval-check-card input[type="checkbox"] {
  flex: 0 0 auto;
  width: 1.125rem;
  height: 1.125rem;
  margin-top: 0.12rem;
  accent-color: #16a34a;
}

.approval-submit-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 44px;
  padding: 0.65rem 1.1rem;
  font-size: 0.95rem;
  font-weight: 700;
  border-radius: 9px;
  border: 1px solid transparent;
  background: #16a34a;
  color: #ffffff;
  cursor: pointer;
  transition: background-color 0.15s ease, transform 0.15s ease, box-shadow 0.15s ease;
}

.approval-submit-btn:hover:not(:disabled) {
  background: #15803d;
  box-shadow: 0 4px 12px rgba(22, 163, 74, 0.22);
}

button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.approval-result-card {
  padding: 0.9rem 1rem;
  border-radius: 10px;
  border: 1px solid #bbf7d0;
  background: #f0fdf4;
  color: #166534;
  display: grid;
  gap: 0.3rem;
}

.approval-result-card p {
  margin: 0;
  font-size: 0.88rem;
}

.approval-loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.65rem;
  padding: 1.5rem 1rem;
  border-radius: 10px;
  background: #f8fafc;
  color: #475569;
  font-size: 0.9rem;
}

.approval-spinner {
  width: 20px;
  height: 20px;
  border: 2.5px solid #e2e8f0;
  border-top-color: #0284c7;
  border-radius: 50%;
  animation: approvalSpin 0.8s linear infinite;
}

@keyframes approvalSpin {
  to { transform: rotate(360deg); }
}

.approval-fade-enter-active,
.approval-fade-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.approval-fade-enter-from,
.approval-fade-leave-to {
  opacity: 0;
  transform: translateY(6px);
}

@media (max-width: 640px) {
  .approval-summary-grid {
    grid-template-columns: 1fr;
  }

  .approval-reload-btn {
    width: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .approval-fade-enter-active,
  .approval-fade-leave-active,
  .approval-spinner {
    transition: none;
    animation: none;
  }
}
</style>
