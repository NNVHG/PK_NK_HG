<template>
  <main class="personal-record">
    <h1>Hồ sơ điều trị của tôi</h1>
    <p v-if="loading" role="status">Đang tải hồ sơ cá nhân…</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <section v-else-if="patient">
      <h2>{{ patient.fullName }}</h2>
      <p>{{ patient.patientCode }}</p>
      <RouterLink :to="`/patients/${patient.patientId}`">Xem lịch sử khám, đơn thuốc và hình ảnh</RouterLink>
    </section>
    <button v-if="error" type="button" @click="load">Thử lại</button>
  </main>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { patientsService, type PatientSummary } from '@/services/patients';
const patient = ref<PatientSummary | null>(null), loading = ref(false), error = ref('');
let revision = 0;
async function load() {
  const current = ++revision;
  loading.value = true; patient.value = null; error.value = '';
  try { const result = await patientsService.getMyPatient(); if (current === revision) patient.value = result; }
  catch { if (current === revision) error.value = 'Chưa tìm thấy hồ sơ cá nhân đang hoạt động. Vui lòng liên hệ Lễ tân.'; }
  finally { if (current === revision) loading.value = false; }
}
onMounted(load);
onUnmounted(() => { revision++; patient.value = null; });
</script>
<style scoped>
.personal-record { max-width: 48rem; margin: auto; padding: 1rem; overflow-wrap: anywhere; }
button { margin-top: 1rem; padding: .6rem 1rem; font: inherit; }
</style>
