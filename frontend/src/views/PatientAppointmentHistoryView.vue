<template>
  <main class="page">
    <h1>Lịch hẹn của gia đình</h1>
    <label for="profile">Hồ sơ</label><select id="profile" v-model.number="patientId" @change="load(1)">
      <option :value="0">Mọi hồ sơ trong tài khoản</option>
      <option v-for="profile in profiles" :key="profile.patientId" :value="profile.patientId">{{ profile.fullName }} · {{ profile.dateOfBirth }} · {{ profile.patientCode }}</option>
    </select>
    <p v-if="profileError" role="alert">{{ profileError }}</p>
    <label for="status">Trạng thái</label><select id="status" v-model="status" @change="load(1)">
      <option value="">Tất cả</option><option value="Scheduled">Đã đặt lịch</option><option value="CheckedIn">Đã tiếp đón</option>
      <option value="Cancelled">Đã hủy</option><option value="Completed">Hoàn tất</option>
    </select>
    <button :disabled="loading" @click="refresh">Tải lại</button>
    <p v-if="loading" role="status">Đang tải lịch hẹn…</p><p v-if="error" role="alert">{{ error }}</p>
    <p v-if="!loading && !error && !rows.length">Không có lịch hẹn phù hợp.</p>
    <ul><li v-for="row in rows" :key="row.appointmentId">
      <strong>#{{ row.appointmentId }} · {{ row.patientName }} · {{ row.patientCode }}</strong>
      <p>{{ row.appointmentDate }} · {{ row.slotTime.slice(0, 5) }} · {{ statusLabel(row.status) }}</p>
    </li></ul>
    <nav aria-label="Phân trang lịch hẹn"><span>{{ total }} lịch · Trang {{ page }}</span>
      <button :disabled="loading || page <= 1" @click="load(page - 1)">Trang trước</button>
      <button :disabled="loading || !hasNext" @click="load(page + 1)">Trang sau</button>
    </nav>
    <p>Liên hệ Lễ tân phòng khám nếu cần hỗ trợ thay đổi lịch.</p>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
const profiles = ref<PatientSummary[]>([]), patientId = ref(0), status = ref(''), profileError = ref('');
const rows = ref<AppointmentResponse[]>([]), page = ref(1), total = ref(0), hasNext = ref(false), loading = ref(false), error = ref('');
let version = 0, profileVersion = 0;
async function loadProfiles() {
  const ticket = ++profileVersion; profileError.value = '';
  try { const result = await patientsService.getMyProfiles(); if (ticket === profileVersion) profiles.value = result; }
  catch { if (ticket === profileVersion) profileError.value = 'Không thể tải bộ chọn hồ sơ. Bạn vẫn có thể xem mọi lịch thuộc tài khoản.'; }
}
async function load(targetPage = 1) {
  if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 || !Number.isInteger(patientId.value) ||
      (patientId.value !== 0 && !profiles.value.some(p => p.patientId === patientId.value)) ||
      !['', 'Scheduled', 'CheckedIn', 'Cancelled', 'Completed'].includes(status.value)) return;
  const ticket = ++version; rows.value = []; error.value = ''; loading.value = true; total.value = 0; hasNext.value = false;
  try {
    const result = await appointmentsService.getMine(targetPage, patientId.value || undefined, status.value || undefined);
    if (ticket === version) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function statusLabel(value: string) { return ({ Scheduled: 'Đã đặt lịch', CheckedIn: 'Đã tiếp đón', Cancelled: 'Đã hủy', Completed: 'Hoàn tất' } as Record<string, string>)[value] ?? value; }
async function refresh() { await Promise.all([loadProfiles(), load(1)]); }
onMounted(refresh);
onUnmounted(() => { version++; profileVersion++; rows.value = []; profiles.value = []; patientId.value = 0; total.value = 0; });
</script>
<style scoped>
.page { max-width: 850px; margin: auto; padding: 1.5rem; } button, select { font: inherit; padding: .5rem; margin: .5rem; }
ul { list-style: none; padding: 0; } li { border: 1px solid #ddd; border-radius: 8px; padding: 1rem; margin: .7rem 0; }
[role=alert] { color: #b91c1c; }
</style>
