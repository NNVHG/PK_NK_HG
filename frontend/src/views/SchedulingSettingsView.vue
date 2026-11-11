<script setup lang="ts">
import { onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useSchedulingSettingsStore } from '@/stores/schedulingSettings';
import { validSchedule } from '@/services/schedulingSettings';
const store = useSchedulingSettingsStore();
const form = reactive({ workStartTime: '', lunchStartTime: '', lunchEndTime: '', workEndTime: '', slotDurationMinutes: 0 }), confirmed = ref(false), error = ref('');
const heading = ref<HTMLElement | null>(null);
watch(() => store.current, value => { if (value) Object.assign(form, { workStartTime: value.workStartTime, lunchStartTime: value.lunchStartTime, lunchEndTime: value.lunchEndTime, workEndTime: value.workEndTime, slotDurationMinutes: value.slotDurationMinutes }); confirmed.value = false; }, { immediate: true });
async function save() {
  error.value = '';
  if (!store.current || !confirmed.value || !validSchedule({ ...form, expectedRevision: store.current.revision })) { error.value = 'Kiểm tra thứ tự giờ, thời lượng nguyên phút và xác nhận thay đổi.'; return; }
  await store.save({ ...form, expectedRevision: store.current.revision }); confirmed.value = false;
}
onMounted(() => { heading.value?.focus(); void store.load(); });
onUnmounted(() => { confirmed.value = false; Object.assign(form, { workStartTime: '', lunchStartTime: '', lunchEndTime: '', workEndTime: '', slotDurationMinutes: 0 }); });
</script>
<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">CẤU HÌNH HỆ THỐNG</p>
        <h1 ref="heading" tabindex="-1">Giờ làm việc & Khung giờ hẹn</h1>
        <p class="subtitle">Thiết lập thời gian mở cửa, nghỉ trưa và phân bổ thời lượng slot phục vụ đặt lịch khám bệnh.</p>
      </div>
      <div class="header-actions">
        <RouterLink to="/management/admin/clinic-print-settings" class="button button-secondary">
          <span class="btn-icon">🖨️</span> Thiết lập in ấn
        </RouterLink>
        <button type="button" class="button button-secondary" :disabled="store.busy" @click="store.load">
          <span class="btn-icon">🔄</span> Tải lại cấu hình
        </button>
      </div>
    </div>

    <!-- Thông báo hướng dẫn nghiệp vụ -->
    <div class="info-banner">
      <div class="banner-icon">⏰</div>
      <div class="banner-content">
        <strong>Lưu ý áp dụng khung hẹn:</strong> Cấu hình mới sẽ áp dụng ngay cho các lượt đặt hoặc dời lịch tiếp theo. Toàn bộ lịch khám đã hẹn trước đó vẫn giữ nguyên ngày, giờ và vị trí đã đăng ký.
      </div>
    </div>

    <div v-if="store.error" class="alert alert-error" role="alert">{{ store.error }}</div>
    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="store.message" class="alert alert-success" role="status">{{ store.message }}</div>
    <div v-if="store.current?.usingFallback" class="alert alert-warning" role="alert">
      Chưa có đủ cấu hình hợp lệ; hệ thống đang dùng giá trị dự phòng an toàn. Vui lòng kiểm tra các khung giờ dưới đây trước khi lưu.
    </div>

    <!-- Form cấu hình khung giờ làm việc -->
    <div v-if="store.current" class="panel config-panel">
      <div class="panel-header">
        <h2 class="panel-title">Khung giờ hoạt động phòng khám (Múi giờ Asia/Ho_Chi_Minh)</h2>
        <span class="count-badge">Slot: {{ form.slotDurationMinutes }} phút</span>
      </div>

      <div class="panel-body">
        <form @submit.prevent="save">
          <fieldset :disabled="store.busy || store.needsReload" class="config-fieldset">
            <div class="form-grid">
              <div class="form-field">
                <label>Giờ mở cửa phòng khám <b>*</b></label>
                <input v-model="form.workStartTime" type="time" step="60" required @input="confirmed = false" />
                <span class="form-hint">Thời điểm bắt đầu nhận tiếp đón ca sáng.</span>
              </div>

              <div class="form-field">
                <label>Giờ bắt đầu nghỉ trưa <b>*</b></label>
                <input v-model="form.lunchStartTime" type="time" step="60" required @input="confirmed = false" />
                <span class="form-hint">Khung giờ ngưng phục vụ tiếp đón ca sáng.</span>
              </div>

              <div class="form-field">
                <label>Giờ kết thúc nghỉ trưa <b>*</b></label>
                <input v-model="form.lunchEndTime" type="time" step="60" required @input="confirmed = false" />
                <span class="form-hint">Bắt đầu tiếp nhận khám lại ca chiều.</span>
              </div>

              <div class="form-field">
                <label>Giờ đóng cửa phòng khám <b>*</b></label>
                <input v-model="form.workEndTime" type="time" step="60" required @input="confirmed = false" />
                <span class="form-hint">Thời điểm kết thúc ca làm việc trong ngày.</span>
              </div>

              <div class="form-field form-field--wide">
                <label>Thời lượng mỗi lượt khám / Slot hẹn (phút) <b>*</b></label>
                <input v-model.number="form.slotDurationMinutes" type="number" min="1" max="1440" step="1" required placeholder="VD: 30" @input="confirmed = false" />
                <span class="form-hint">Thời lượng slot phải nằm trọn trong một buổi (sáng hoặc chiều). Giới hạn tối đa 100 lịch hẹn đồng thời.</span>
              </div>

              <div class="form-field form-field--wide">
                <div class="confirm-box">
                  <label class="checkbox-label text-warning">
                    <input v-model="confirmed" type="checkbox" required />
                    <span>Tôi đã kiểm tra kỹ thứ tự khung giờ, thời lượng slot và xác nhận áp dụng cho hệ thống.</span>
                  </label>
                </div>
              </div>
            </div>

            <div class="form-actions">
              <button class="button button-primary" type="submit" :disabled="!confirmed || store.busy">
                {{ store.busy ? 'Đang lưu cấu hình...' : 'Lưu cấu hình lịch hẹn' }}
              </button>
            </div>
          </fieldset>
        </form>
      </div>
    </div>
  </main>
</template>
<style scoped>
.page-container {
  max-width: 900px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
  color: #1e293b;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
}

.eyebrow {
  margin: 0 0 0.25rem;
  color: #0284c7;
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

h1 {
  font-size: 1.65rem;
  font-weight: 850;
  color: #0f2d59;
  margin: 0;
  line-height: 1.25;
}

.subtitle {
  margin: 0.35rem 0 0;
  color: #64748b;
  font-size: 0.885rem;
  line-height: 1.45;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.info-banner {
  display: flex;
  align-items: flex-start;
  gap: 0.85rem;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 10px;
  padding: 0.85rem 1.15rem;
  font-size: 0.85rem;
  color: #0369a1;
  line-height: 1.45;
}

.banner-icon {
  font-size: 1.2rem;
  flex-shrink: 0;
}

.panel {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.03);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid #f1f5f9;
  background: #f8fafc;
  border-radius: 12px 12px 0 0;
}

.panel-title {
  font-size: 1.05rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0;
}

.count-badge {
  background: #e2e8f0;
  color: #475569;
  font-size: 0.78rem;
  font-weight: 750;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
}

.panel-body {
  padding: 1.5rem;
}

.config-fieldset {
  border: none;
  padding: 0;
  margin: 0;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.form-field {
  display: grid;
  gap: 0.35rem;
  font-size: 0.825rem;
  font-weight: 600;
  color: #334155;
}

.form-field b {
  color: #dc2626;
}

.form-field--wide {
  grid-column: 1 / -1;
}

.form-hint {
  font-size: 0.775rem;
  color: #64748b;
}

input {
  box-sizing: border-box;
  width: 100%;
  height: 40px;
  padding: 0 0.85rem;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #f8fafc;
  color: #1e293b;
  font: inherit;
  font-size: 0.875rem;
  outline: none;
  transition: all 0.15s ease;
}

input:focus {
  border-color: #0284c7;
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.15);
}

.confirm-box {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 0.85rem 1rem;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.55rem;
  font-size: 0.825rem;
  font-weight: 600;
  cursor: pointer;
}

.text-warning {
  color: #92400e;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid #f1f5f9;
}

/* Buttons */
.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
  height: 40px;
  padding: 0 1.15rem;
  border: 1px solid transparent;
  border-radius: 8px;
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  cursor: pointer;
  text-decoration: none;
  transition: all 0.15s ease;
}

.button-primary {
  background: #0284c7;
  color: #ffffff;
}

.button-primary:hover:not(:disabled) {
  background: #0369a1;
}

.button-secondary {
  background: #ffffff;
  color: #334155;
  border-color: #cbd5e1;
}

.button-secondary:hover:not(:disabled) {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.btn-icon {
  font-size: 1.1rem;
  line-height: 1;
}

/* Alerts */
.alert {
  padding: 0.85rem 1.15rem;
  border-radius: 8px;
  font-size: 0.875rem;
  font-weight: 600;
}

.alert-error {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fee2e2;
}

.alert-success {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.alert-warning {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fef3c7;
}

@media (max-width: 640px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
