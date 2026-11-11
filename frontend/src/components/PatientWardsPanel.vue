<template>
  <div v-if="wards.length > 0" class="wards-panel">
    <div class="panel-header">
      <div class="panel-title-wrap">
        <span class="panel-icon">👶</span>
        <div>
          <h3 class="panel-title">Người được giám hộ</h3>
          <p class="panel-subtitle">Hồ sơ bệnh nhi do bạn đại diện hợp pháp theo dõi</p>
        </div>
      </div>
      <span class="count-badge">{{ wards.length }} hồ sơ</span>
    </div>

    <div class="wards-grid">
      <div v-for="ward in wards" :key="ward.wardPatientId" class="ward-card">
        <div class="ward-card-header">
          <div class="ward-avatar">
            <span>{{ ward.fullName.charAt(0).toUpperCase() }}</span>
          </div>
          <div class="ward-main-info">
            <h4 class="ward-name">{{ ward.fullName }}</h4>
            <div class="ward-tags">
              <span class="code-badge">{{ ward.wardPatientCode }}</span>
              <span class="rel-badge">{{ ward.relationship }}</span>
            </div>
          </div>
        </div>

        <div class="ward-card-body">
          <div class="ward-meta-item">
            <span class="meta-label">Ngày sinh:</span>
            <span class="meta-value">{{ formatDate(ward.dateOfBirth) }}</span>
          </div>
        </div>

        <div class="ward-card-actions">
          <RouterLink
            class="button button-outline-primary"
            :to="{ name: 'patient-portal-patient-detail', params: { id: ward.wardPatientId } }"
          >
            <span>Xem hồ sơ bệnh nhi</span>
            <span aria-hidden="true">→</span>
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { patientGuardiansService, type WardSummary } from '@/services/patientGuardians';

const wards = ref<WardSummary[]>([]);

function formatDate(dateStr?: string) {
  if (!dateStr) return 'Chưa cập nhật';
  const parts = dateStr.split('-');
  if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`;
  return dateStr;
}

onMounted(async () => {
  try {
    wards.value = await patientGuardiansService.getMyWards();
  } catch {
    wards.value = [];
  }
});
</script>

<style scoped>
.wards-panel {
  margin-top: 1.75rem;
  padding-top: 1.5rem;
  border-top: 1px solid #e2e8f0;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.panel-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.65rem;
}

.panel-icon {
  font-size: 1.35rem;
}

.panel-title {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
}

.panel-subtitle {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #64748b;
}

.count-badge {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.775rem;
  font-weight: 750;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
}

.wards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.ward-card {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1.1rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  transition: all 0.15s ease;
}

.ward-card:hover {
  border-color: #cbd5e1;
  background: #ffffff;
  box-shadow: 0 4px 12px rgba(15, 45, 89, 0.05);
}

.ward-card-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.ward-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #dbeafe;
  color: #1d4ed8;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 850;
  font-size: 1.1rem;
  flex-shrink: 0;
}

.ward-main-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.ward-name {
  margin: 0;
  font-size: 0.975rem;
  font-weight: 750;
  color: #0f2d59;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ward-tags {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.725rem;
  font-weight: 700;
  background: #e2e8f0;
  color: #334155;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
}

.rel-badge {
  font-size: 0.725rem;
  font-weight: 700;
  background: #fef3c7;
  color: #92400e;
  padding: 0.1rem 0.45rem;
  border-radius: 4px;
}

.ward-card-body {
  font-size: 0.825rem;
  color: #475569;
}

.ward-meta-item {
  display: flex;
  gap: 0.4rem;
}

.meta-label {
  color: #64748b;
}

.meta-value {
  font-weight: 600;
  color: #1e293b;
}

.ward-card-actions {
  margin-top: auto;
  display: flex;
}

.button-outline-primary {
  width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  height: 36px;
  padding: 0 0.85rem;
  border: 1px solid #bae6fd;
  border-radius: 6px;
  background: #ffffff;
  color: #0284c7;
  font-size: 0.825rem;
  font-weight: 700;
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-outline-primary:hover {
  background: #f0f9ff;
  border-color: #0284c7;
}
</style>
