<template>
  <main class="personal-record">
    <h1>Hồ sơ điều trị của tôi</h1>
    <p v-if="loading" role="status">Đang tải hồ sơ cá nhân…</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <p v-else-if="!patient">Chưa có hồ sơ đang hoạt động. Vui lòng liên hệ Lễ tân để đối soát hồ sơ cá nhân.</p>
    <section v-else-if="patient">
      <h2>{{ patient.fullName }}</h2>
      <p>{{ patient.patientCode }}</p>
      <RouterLink :to="{ name: 'patient-detail', params: { id: patient.patientId } }">Xem lịch sử khám, đơn thuốc và hình ảnh</RouterLink>
      <!-- Danh sách người được giám hộ (DL-353, DL-358) -->
      <PatientWardsPanel />
    </section>
    <button type="button" :disabled="loading" @click="load">Tải lại hồ sơ</button>
  </main>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { patientsService, type PatientSummary } from '@/services/patients';
import { useAuthStore } from '@/stores/auth';
import PatientWardsPanel from '@/components/PatientWardsPanel.vue';

const auth = useAuthStore();
const patient = ref<PatientSummary | null>(null), loading = ref(false), error = ref('');
let revision = 0;
async function load() {
  const current = ++revision;
  patient.value = null; error.value = '';
  if (auth.role !== 'PATIENT' || !auth.user?.userId) { loading.value = false; return; }
  loading.value = true;
  try {
    const result = await patientsService.getMyPatient();
    if (current !== revision) return;
    if (result !== null && (!Number.isSafeInteger(result.patientId) || result.patientId <= 0 || typeof result.fullName !== 'string'))
      throw Error('Hồ sơ cá nhân không hợp lệ.');
    patient.value = result;
  }
  catch { if (current === revision) error.value = 'Chưa tìm thấy hồ sơ cá nhân đang hoạt động. Vui lòng liên hệ Lễ tân.'; }
  finally { if (current === revision) loading.value = false; }
}
watch(() => `${auth.user?.userId}:${auth.role}`, load, { immediate: true });
onUnmounted(() => { revision++; patient.value = null; error.value = ''; });
</script>
<style scoped>
.personal-record { max-width: 48rem; margin: auto; padding: 1rem; overflow-wrap: anywhere; }
button { margin-top: 1rem; padding: .6rem 1rem; font: inherit; }
</style>

