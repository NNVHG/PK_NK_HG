<template>
  <main class="page">
    <header>
      <h1>Đặt lịch khám</h1>
      <p>Chọn hồ sơ, ngày và khung giờ. Hệ thống kiểm tra giờ hợp lệ và chỗ trống khi xác nhận.</p>
    </header>

    <form class="booking-card" @submit.prevent="submit">
      <label for="patient">Hồ sơ bệnh nhân</label>
      <select id="patient" v-model.number="patientId" required :disabled="loadingProfiles || profiles.length === 0">
        <option :value="0" disabled>Chọn hồ sơ</option>
        <option v-for="profile in profiles" :key="profile.patientId" :value="profile.patientId">
          {{ profile.fullName }} · {{ profile.patientCode }}
        </option>
      </select>
      <p v-if="!loadingProfiles && profiles.length === 0" class="hint">Tài khoản chưa có hồ sơ bệnh nhân hoạt động để đặt lịch.</p>

      <label for="date">Ngày khám</label>
      <input id="date" v-model="appointmentDate" type="date" :min="today" required />

      <label for="slot">Khung giờ</label>
      <select id="slot" v-model="slotTime" required>
        <option value="" disabled>Chọn khung giờ</option>
        <option v-for="slot in slots" :key="slot" :value="slot">{{ slot }}</option>
      </select>

      <label for="notes">Lý do khám (không bắt buộc)</label>
      <textarea id="notes" v-model.trim="notes" rows="3" maxlength="500" placeholder="Ví dụ: khám tổng quát" />

      <p v-if="errorMessage" class="message error" role="alert">{{ errorMessage }}</p>
      <p v-if="success" class="message success" role="status">
        Đã đặt lịch cho {{ success.patientName }} vào {{ formatDate(success.appointmentDate) }} lúc {{ success.slotTime.slice(0, 5) }}.
      </p>

      <button type="submit" :disabled="submitting || loadingProfiles || profiles.length === 0">
        {{ submitting ? 'Đang đặt lịch…' : 'Xác nhận đặt lịch' }}
      </button>
    </form>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';

const profiles = ref<PatientSummary[]>([]);
const patientId = ref(0);
const appointmentDate = ref('');
const slotTime = ref('');
const notes = ref('');
const loadingProfiles = ref(true);
const submitting = ref(false);
const errorMessage = ref('');
const success = ref<AppointmentResponse | null>(null);

const today = computed(() => {
  const date = new Date();
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 10);
});

const slots = [
  ...makeSlots(8, 0, 11, 30),
  ...makeSlots(13, 30, 16, 30)
];

onMounted(async () => {
  try {
    profiles.value = await patientsService.getMyProfiles();
  } catch {
    errorMessage.value = 'Không thể tải hồ sơ của bạn. Hãy đăng nhập lại hoặc thử sau.';
  } finally {
    loadingProfiles.value = false;
  }
});

async function submit() {
  errorMessage.value = '';
  success.value = null;

  if (!patientId.value || !appointmentDate.value || !slotTime.value) {
    errorMessage.value = 'Vui lòng chọn hồ sơ, ngày và khung giờ khám.';
    return;
  }

  submitting.value = true;
  try {
    success.value = await appointmentsService.create({
      patientId: patientId.value,
      appointmentDate: appointmentDate.value,
      slotTime: `${slotTime.value}:00`,
      notes: notes.value || null
    });
    notes.value = '';
  } catch (error) {
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    errorMessage.value = response?.message
      ?? response?.errors?.[0]?.message
      ?? 'Không thể đặt lịch. Khung giờ có thể đã đầy hoặc thông tin không hợp lệ.';
  } finally {
    submitting.value = false;
  }
}

function makeSlots(startHour: number, startMinute: number, endHour: number, endMinute: number): string[] {
  const result: string[] = [];
  const end = endHour * 60 + endMinute;
  for (let minute = startHour * 60 + startMinute; minute <= end; minute += 30) {
    result.push(`${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`);
  }
  return result;
}

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
