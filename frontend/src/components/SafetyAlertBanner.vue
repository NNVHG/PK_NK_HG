<template>
  <section class="safety-banner" aria-labelledby="safety-banner-title">
    <div class="banner-heading">
      <div>
        <p class="eyebrow">AN TOÀN NGƯỜI BỆNH</p>
        <h2 id="safety-banner-title">Cảnh báo cần lưu ý</h2>
      </div>
      <button class="refresh-button" type="button" :disabled="loading" @click="loadAlerts">
        {{ loading ? 'Đang tải...' : 'Làm mới' }}
      </button>
    </div>
    <p v-if="loading && !data" class="banner-message">Đang tải thông tin cảnh báo...</p>
    <p v-else-if="error" class="banner-error" role="alert">{{ error }}</p>
    <template v-else-if="data">
      <p v-if="!data.hasHistory" class="no-history">Chưa có tiền sử bệnh/dị ứng được ghi nhận</p>
      <p v-else-if="!data.alerts.length" class="no-alerts">Chưa có mục nào được đánh dấu cần cảnh báo.</p>
      <ul v-else class="alert-list">
        <li v-for="(alert, index) in data.alerts" :key="`${alert.visitId}-${alert.type}-${alert.name}-${index}`">
          <span class="alert-kind">{{ alert.type === 'Allergy' ? 'Dị ứng' : alert.type === 'Condition' ? 'Tình trạng' : alert.type }}</span>
          <div>
            <strong>{{ alert.name }}</strong>
            <p v-if="alert.detail">{{ alert.detail }}</p>
            <small>Ghi nhận {{ formatDate(alert.recordedAt) }} · Lần khám #{{ alert.visitId }}</small>
          </div>
        </li>
      </ul>
    </template>
    <p class="disclaimer">Cảnh báo chỉ nhắc lại thông tin đã ghi nhận, không thay thế quyết định chuyên môn</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { patientsService, type PatientSafetyAlerts } from '@/services/patients';

const props = defineProps<{ patientId: number; refreshKey?: number }>();
const data = ref<PatientSafetyAlerts | null>(null);
const loading = ref(false);
const error = ref('');

onMounted(() => void loadAlerts());
watch(() => props.refreshKey, () => void loadAlerts());

async function loadAlerts() {
  loading.value = true;
  error.value = '';
  try {
    data.value = await patientsService.getSafetyAlerts(props.patientId);
  } catch {
    error.value = 'Không tải được cảnh báo an toàn. Vui lòng thử tải lại.';
  } finally {
    loading.value = false;
  }
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'thời điểm không xác định';
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Ho_Chi_Minh'
  }).format(date);
}
</script>

<style scoped>
.safety-banner { display: grid; gap: 0.8rem; padding: 1rem 1.15rem; border: 1px solid #f2c36b; border-left: 5px solid #dc941c; border-radius: 14px; background: #fff8e9; color: #5a4219; }
.banner-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
.eyebrow { margin: 0 0 0.2rem; color: #9b6814; font-size: 0.68rem; font-weight: 800; letter-spacing: 0.09em; }
h2, p { margin-top: 0; }
h2 { margin-bottom: 0; font-size: 1.05rem; }
.refresh-button { border: 1px solid #e1bb73; border-radius: 8px; padding: 0.45rem 0.7rem; background: #fff; color: #674a18; cursor: pointer; }
.refresh-button:disabled { opacity: 0.65; cursor: wait; }
.banner-message, .no-history, .no-alerts, .banner-error { margin: 0; }
.no-history { color: #6e5a34; }
.banner-error { color: #9e2e28; }
.alert-list { display: grid; gap: 0.6rem; margin: 0; padding: 0; list-style: none; }
.alert-list li { display: grid; grid-template-columns: auto 1fr; align-items: start; gap: 0.8rem; padding: 0.7rem; border-radius: 10px; background: #fff; }
.alert-kind { display: inline-block; border-radius: 99px; padding: 0.25rem 0.55rem; background: #ffe5ac; color: #754a00; font-size: 0.75rem; font-weight: 700; }
.alert-list strong { color: #422f10; }
.alert-list p { margin: 0.25rem 0; white-space: pre-wrap; }
.alert-list small { color: #786744; }
.disclaimer { margin: 0; color: #746346; font-size: 0.76rem; }
</style>
