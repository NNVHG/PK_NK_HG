<template>
  <div v-if="wards.length > 0" class="wards-panel">
    <h3>Hồ sơ người được giám hộ (Trẻ em / Đại diện hợp pháp)</h3>
    <div class="wards-list">
      <div v-for="ward in wards" :key="ward.wardPatientId" class="ward-item">
        <div class="ward-info">
          <h4>{{ ward.fullName }}</h4>
          <p class="code">Mã BN: {{ ward.wardPatientCode }}</p>
          <p>Quan hệ: <strong>{{ ward.relationship }}</strong> | Ngày sinh: {{ ward.dateOfBirth }}</p>
        </div>
        <RouterLink class="action-btn" :to="{ name: 'patient-detail', params: { id: ward.wardPatientId } }">
          Xem hồ sơ bệnh nhi
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { patientGuardiansService, type WardSummary } from '@/services/patientGuardians';

const wards = ref<WardSummary[]>([]);

onMounted(async () => {
  try {
    wards.value = await patientGuardiansService.getMyWards();
  } catch {
    wards.value = [];
  }
});
</script>

<style scoped>
.wards-panel { margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #e2e8f0; }
.wards-list { display: flex; flex-direction: column; gap: 0.75rem; margin-top: 0.75rem; }
.ward-item { display: flex; justify-content: space-between; align-items: center; padding: 1rem; border: 1px solid #cbd5e1; border-radius: 6px; background: #f8fafc; }
.ward-info h4 { margin: 0 0 0.25rem 0; font-size: 1.05rem; }
.ward-info p { margin: 0; font-size: 0.875rem; color: #475569; }
.ward-info .code { font-weight: 600; color: #1e293b; }
.action-btn { color: #2563eb; font-weight: 500; text-decoration: underline; }
</style>
