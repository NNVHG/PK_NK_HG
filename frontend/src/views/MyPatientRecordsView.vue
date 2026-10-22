<template>
  <section class="my-records" aria-labelledby="my-records-title">
    <h1 id="my-records-title">Hồ sơ điều trị gia đình</h1>
    <p>Chọn hồ sơ đã được phòng khám liên kết với tài khoản để xem lịch sử khám, sơ đồ răng, ảnh, đơn thuốc và hóa đơn theo quyền của bạn.</p>
    <button type="button" :disabled="loading" @click="load">Tải lại hồ sơ</button>
    <p v-if="loading" role="status">Đang tải hồ sơ…</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <p v-else-if="!profiles.length">Chưa có hồ sơ đang hoạt động liên kết với tài khoản. Liên hệ Lễ tân nếu cần đối soát hồ sơ đã khám.</p>
    <ul v-else>
      <li v-for="profile in profiles" :key="profile.patientId">
        <h2>{{ profile.fullName }}</h2>
        <p>{{ profile.patientCode }} · Ngày sinh: {{ profile.dateOfBirth }}</p>
        <RouterLink :to="{ name: 'patient-detail', params: { id: profile.patientId } }">Xem hồ sơ {{ profile.fullName }}</RouterLink>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { patientsService, type PatientSummary } from '@/services/patients';
import { useAuthStore } from '@/stores/auth';
const auth = useAuthStore();
const profiles = ref<PatientSummary[]>([]), loading = ref(false), error = ref('');
let version = 0;
async function load() {
  const ticket = ++version; profiles.value = []; error.value = '';
  if (auth.role !== 'PATIENT' || !auth.user?.userId) { loading.value = false; return; }
  loading.value = true;
  try {
    const data = await patientsService.getMyProfiles();
    if (ticket !== version) return;
    if (!Array.isArray(data) || data.some(p => !Number.isSafeInteger(p.patientId) || p.patientId <= 0 || typeof p.fullName !== 'string'))
      throw Error('Invalid profile list');
    profiles.value = data;
  } catch { if (ticket === version) error.value = 'Không tải được hồ sơ. Tải lại hoặc liên hệ phòng khám để đối soát liên kết.'; }
  finally { if (ticket === version) loading.value = false; }
}
watch(() => `${auth.user?.userId}:${auth.role}`, load, { immediate: true });
onUnmounted(() => { version++; profiles.value = []; error.value = ''; });
</script>

<style scoped>
.my-records { max-width: 850px; margin: auto; overflow-wrap: anywhere; }
ul { list-style: none; padding: 0; }
li { border: 1px solid #cbd5e1; padding: 1rem; margin: 1rem 0; border-radius: .5rem; }
button, a { display: inline-block; padding: .6rem .25rem; }
[role=alert] { color: #b91c1c; }
</style>
