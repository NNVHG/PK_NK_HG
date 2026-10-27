<template>
  <main class="page">
    <header>
      <h1>Đặt lịch khám</h1>
      <p>Đặt lịch cho tài khoản cá nhân của bạn. Hệ thống kiểm tra giờ hợp lệ và chỗ trống khi xác nhận.</p>
    </header>

    <form class="booking-card" @submit.prevent="submit">
      <div v-if="wards.length > 0" class="profile-selection">
        <label for="booking-target">Đặt lịch cho</label>
        <select id="booking-target" v-model.number="selectedPatientId" :disabled="submitting || loadingProfiles">
          <option v-if="patient" :value="patient.patientId">Bản thân: {{ patient.fullName }} ({{ patient.patientCode }})</option>
          <option v-for="w in wards" :key="w.wardPatientId" :value="w.wardPatientId">
            Trẻ: {{ w.fullName }} ({{ w.wardPatientCode }}) · {{ w.relationship }}
          </option>
        </select>
      </div>

      <p v-if="currentProfile">Hồ sơ khám: <strong>{{ currentProfile.fullName }}</strong> · {{ currentProfile.code }}.</p>
      <p v-else-if="!loadingProfiles" class="hint">Chưa tìm thấy hồ sơ cá nhân. Liên hệ Lễ tân để được hỗ trợ.</p>

      <label for="date">Ngày khám</label>
      <input id="date" v-model="appointmentDate" type="date" :min="today" :disabled="submitting" required />

      <label for="slot">Khung giờ</label>
      <p v-if="slotDurationMinutes">Độ dài khung hẹn mới: {{ slotDurationMinutes }} phút.</p>
      <p v-if="scheduleError" role="alert">{{ scheduleError }} <button type="button" :disabled="submitting || scheduleLoading" @click="loadSchedule">Tải lại giờ</button></p>
      <p v-if="scheduleLoading" role="status">Đang tải khung giờ…</p>
      <select id="slot" v-model="slotTime" :disabled="submitting || scheduleLoading" required>
        <option value="" disabled>Chọn khung giờ</option>
        <option v-for="slot in slots" :key="slot" :value="slot">{{ slot }}</option>
      </select>

      <label for="notes">Lý do khám (không bắt buộc)</label>
      <textarea id="notes" v-model.trim="notes" :disabled="submitting" rows="3" maxlength="500" placeholder="Ví dụ: khám tổng quát" />

      <p v-if="errorMessage" class="message error" role="alert">{{ errorMessage }}</p>
      <p v-if="success" class="message success" role="status">
        Đã đặt lịch cho {{ success.patientName }} vào {{ formatDate(success.appointmentDate) }} lúc {{ success.slotTime.slice(0, 5) }}.
      </p>

      <button type="submit" :disabled="submitting || loadingProfiles || !selectedPatientId">
        {{ submitting ? 'Đang đặt lịch…' : 'Xác nhận đặt lịch' }}
      </button>
    </form>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { patientGuardiansService, type WardSummary } from '@/services/patientGuardians';
import { useBookingSchedule } from '@/composables/useBookingSchedule';

const patient = ref<PatientSummary | null>(null);
const wards = ref<WardSummary[]>([]);
const selectedPatientId = ref<number>(0);
const appointmentDate = ref('');
const slotTime = ref('');
const notes = ref('');
const loadingProfiles = ref(true);
const submitting = ref(false);
const errorMessage = ref('');
const success = ref<AppointmentResponse | null>(null);
let version = 0;

const today = computed(() => {
  const date = new Date();
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 10);
});

const currentProfile = computed(() => {
  if (patient.value && selectedPatientId.value === patient.value.patientId) {
    return { fullName: patient.value.fullName, code: patient.value.patientCode };
  }
  const ward = wards.value.find(w => w.wardPatientId === selectedPatientId.value);
  if (ward) {
    return { fullName: ward.fullName, code: ward.wardPatientCode };
  }
  return null;
});

const { slots, revision: scheduleRevision, duration: slotDurationMinutes, loading: scheduleLoading, error: scheduleError, load: loadSchedule } = useBookingSchedule();
watch(scheduleRevision, () => { slotTime.value = ''; });

onMounted(async () => {
  const ticket = version;
  try {
    const [pResult, wResult] = await Promise.allSettled([
      patientsService.getMyPatient(),
      patientGuardiansService.getMyWards()
    ]);
    if (ticket !== version) return;
    if (pResult.status === 'fulfilled') {
      patient.value = pResult.value;
      selectedPatientId.value = pResult.value.patientId;
    }
    if (wResult.status === 'fulfilled') {
      wards.value = wResult.value;
      if (!selectedPatientId.value && wResult.value.length > 0) {
        selectedPatientId.value = wResult.value[0].wardPatientId;
      }
    }
    if (!patient.value && wards.value.length === 0) {
      errorMessage.value = 'Không thể tải hồ sơ của bạn. Hãy đăng nhập lại hoặc thử sau.';
    }
  } catch {
    if (ticket === version) errorMessage.value = 'Không thể tải hồ sơ của bạn. Hãy đăng nhập lại hoặc thử sau.';
  } finally {
    if (ticket === version) loadingProfiles.value = false;
  }
});

async function submit() {
  if (submitting.value || loadingProfiles.value || scheduleLoading.value || !scheduleRevision.value) return;
  errorMessage.value = '';
  success.value = null;

  if (!selectedPatientId.value ||
      !appointmentDate.value || !slots.value.includes(slotTime.value) || notes.value.length > 500) {
    errorMessage.value = 'Vui lòng chọn hồ sơ, ngày và khung giờ khám.';
    return;
  }

  submitting.value = true;
  const ticket = version;
  try {
    const result = await appointmentsService.create({
      patientId: selectedPatientId.value,
      appointmentDate: appointmentDate.value,
      slotTime: `${slotTime.value}:00`,
      notes: notes.value || null,
      expectedScheduleRevision: scheduleRevision.value
    });
    if (ticket !== version) return;
    success.value = result;
    notes.value = '';
  } catch (error) {
    if (ticket !== version) return;
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    if (response?.code === 'APP_SCHEDULE') { slotTime.value = ''; await loadSchedule(); }
    if (ticket !== version) return;
    errorMessage.value = response?.message
      ?? response?.errors?.[0]?.message
      ?? 'Không thể đặt lịch. Khung giờ có thể đã đầy hoặc thông tin không hợp lệ.';
  } finally {
    if (ticket === version) submitting.value = false;
  }
}
onUnmounted(() => { version++; patient.value = null; wards.value = []; notes.value = ''; success.value = null; });

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`));
}
</script>

<style scoped>
.page { max-width: 640px; margin: 0 auto; padding: 2rem 1rem; color: #243447; }
header { margin-bottom: 1.25rem; }
h1 { margin: 0 0 .4rem; font-size: 1.6rem; }
header p, .hint { color: #607080; }
.booking-card { display: grid; gap: .55rem; background: #fff; border: 1px solid #e2e8ef; border-radius: 10px; padding: 1.25rem; box-shadow: 0 3px 12px #18324b0d; }
label { margin-top: .45rem; font-weight: 600; }
input, select, textarea { width: 100%; box-sizing: border-box; padding: .7rem .75rem; border: 1px solid #cbd5df; border-radius: 6px; font: inherit; background: #fff; }
textarea { resize: vertical; }
button { margin-top: .6rem; padding: .75rem 1rem; border: 0; border-radius: 6px; color: #fff; background: #13795b; font: inherit; font-weight: 700; cursor: pointer; }
button:disabled { cursor: wait; opacity: .6; }
.hint, .message { margin: .2rem 0; font-size: .92rem; }
.error { color: #a32323; }
.success { color: #176b45; background: #edf8f1; border-radius: 6px; padding: .7rem; }
</style>
