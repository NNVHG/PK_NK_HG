<template>
  <section class="citizen-id-card" aria-label="CCCD của bệnh nhân">
    <div class="card-header">
      <div class="header-title">
        <span class="card-icon" aria-hidden="true">🪪</span>
        <div>
          <h3 class="card-heading">Căn cước công dân (CCCD)</h3>
          <p class="card-subheading">Thông tin định danh bảo vệ theo tiêu chuẩn y tế</p>
        </div>
      </div>
      <button type="button" class="btn-refresh" :disabled="busy" @click="load">
        <span class="refresh-icon" :class="{ 'spin': busy }">🔄</span>
        <span>{{ busy ? 'Đang tải…' : 'Tải lại' }}</span>
      </button>
    </div>

    <div v-if="status" class="status-box">
      <span class="status-label">Số CCCD đã lưu:</span>
      <strong class="status-value">{{ status.masked || 'Chưa liên kết số CCCD' }}</strong>
    </div>

    <p v-if="error" class="alert alert-error" role="alert">{{ error }}</p>
    <p v-if="message" class="alert alert-success" role="status">{{ message }}</p>

    <form v-if="canWrite && status" class="citizen-form" @submit.prevent="save">
      <div class="form-group">
        <label for="citizen-input" class="form-label">
          Nhập số CCCD bệnh nhân (12 chữ số)
        </label>
        <input
          id="citizen-input"
          v-model="citizenId"
          class="form-input"
          type="password"
          inputmode="numeric"
          maxlength="12"
          autocomplete="off"
          placeholder="Ví dụ: 074088001234"
          :disabled="busy || needsReview"
          @input="confirmed = false"
        />
      </div>

      <label class="checkbox-label">
        <input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" />
        <span>Tôi xác nhận đã đối chiếu bản gốc CCCD trực tiếp với bệnh nhân</span>
      </label>

      <button type="submit" class="btn-primary" :disabled="busy || needsReview || !confirmed">
        {{ busy ? 'Đang lưu…' : 'Lưu số CCCD bảo mật' }}
      </button>
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
.citizen-id-card {
  margin-top: 1.5rem;
  padding: 1.5rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.05);
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.header-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.card-icon {
  font-size: 1.5rem;
  line-height: 1;
}

.card-heading {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  color: #0f2d59;
}

.card-subheading {
  margin: 0.15rem 0 0;
  font-size: 0.85rem;
  color: #64748b;
}

.btn-refresh {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.45rem 0.85rem;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  background: #f8fafc;
  color: #334155;
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}

.btn-refresh:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.btn-refresh:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.refresh-icon.spin {
  display: inline-block;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  100% { transform: rotate(360deg); }
}

.status-box {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: 1rem 1.25rem;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  border-radius: 8px;
}

.status-label {
  font-size: 0.9rem;
  color: #166534;
  font-weight: 600;
}

.status-value {
  font-size: 1.1rem;
  letter-spacing: 0.05em;
  color: #15803d;
  font-family: monospace, monospace;
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 6px;
  font-size: 0.875rem;
  line-height: 1.4;
  margin: 0;
}

.alert-error {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #b91c1c;
}

.alert-success {
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #15803d;
}

.citizen-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #f1f5f9;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.form-label {
  font-size: 0.875rem;
  font-weight: 600;
  color: #334155;
}

.form-input {
  max-width: 320px;
  width: 100%;
  padding: 0.6rem 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font-size: 0.95rem;
  letter-spacing: 0.05em;
  color: #0f172a;
  background: #ffffff;
}

.form-input:focus {
  outline: none;
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  color: #475569;
  cursor: pointer;
  user-select: none;
}

.btn-primary {
  align-self: flex-start;
  padding: 0.6rem 1.25rem;
  background: #0075ff;
  color: #ffffff;
  border: none;
  border-radius: 6px;
  font-weight: 700;
  font-size: 0.875rem;
  cursor: pointer;
  transition: background 0.15s ease;
}

.btn-primary:hover:not(:disabled) {
  background: #0060d0;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
