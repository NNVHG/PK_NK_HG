<template>
  <main class="page-container">
    <div class="page-header">
      <div>
        <p class="eyebrow">CẤU HÌNH HỆ THỐNG</p>
        <h1>Thông tin phòng khám trên bản in</h1>
        <p class="subtitle">Thiết lập địa chỉ, số hotline và thông tin pháp lý hiển thị trên tiêu đề đơn thuốc và biên lai thanh toán.</p>
      </div>
      <div class="header-actions">
        <RouterLink to="/management/admin/staff" class="button button-secondary">
          <span class="btn-icon">👨‍⚕️</span> Học vị & Chữ ký bác sĩ
        </RouterLink>
        <button class="button button-secondary" :disabled="busy || !allowed" @click="load">
          <span class="btn-icon">🔄</span> Tải lại thông tin
        </button>
      </div>
    </div>

    <!-- Thông báo hướng dẫn nghiệp vụ -->
    <div class="info-banner">
      <div class="banner-icon">📄</div>
      <div class="banner-content">
        <strong>Tên cơ sở y tế cố định:</strong> <b>Nha khoa Hoàng Gia</b>. Thông tin địa chỉ và hotline sẽ tự động in trên góc đầu trang của mọi đơn thuốc điện tử, biên lai thu tiền và phiếu xác nhận khám bệnh.
      </div>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>
    <div v-if="message" class="alert alert-success" role="status">{{ message }}</div>

    <!-- Preview thương hiệu & Form cấu hình -->
    <div class="panel print-config-panel">
      <div class="panel-header">
        <h2 class="panel-title">Mẫu tiêu đề chứng từ in ấn</h2>
        <span class="count-badge">Bản in A4 / A5</span>
      </div>

      <div class="panel-body">
        <!-- Khung xem trước tiêu đề in -->
        <div class="print-preview-box">
          <div class="preview-brand">
            <img src="/logo-ngang.svg" alt="Nha Khoa Hoàng Gia" class="preview-logo" />
            <div class="preview-clinic-details">
              <span class="preview-clinic-name">NHA KHOA HOÀNG GIA</span>
              <span class="preview-clinic-address">{{ address || 'Chưa thiết lập địa chỉ phòng khám' }}</span>
              <span class="preview-clinic-phone">Hotline: {{ phone || 'Chưa thiết lập số điện thoại' }}</span>
            </div>
          </div>
          <div class="preview-badge">Xem trước tiêu đề</div>
        </div>

        <form v-if="loaded" class="print-form" @submit.prevent="save">
          <div class="form-grid">
            <div class="form-field form-field--wide">
              <label for="clinic-address">Địa chỉ phòng khám hiển thị trên bản in <b>*</b></label>
              <input id="clinic-address" v-model="address" maxlength="500" required placeholder="VD: Số 123 Đường Đại Lộ Bình Dương, TP. Thủ Dầu Một..." :disabled="busy || needsReview" />
              <span class="form-hint">Địa chỉ đầy đủ có dấu (tối đa 500 ký tự).</span>
            </div>

            <div class="form-field form-field--wide">
              <label for="clinic-phone">Số hotline / Tổng đài tư vấn <b>*</b></label>
              <input id="clinic-phone" v-model="phone" maxlength="30" required type="tel" placeholder="VD: 0274 3822 123 hoặc 1900 1234" :disabled="busy || needsReview" />
              <span class="form-hint">Số hotline gồm 8–30 ký tự (chữ số, khoảng cách, dấu gạch nối).</span>
            </div>

            <div class="form-field form-field--wide">
              <div class="confirm-box">
                <label class="checkbox-label text-warning">
                  <input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" />
                  <span>Tôi đã kiểm tra kỹ lưỡng và xác nhận thông tin cơ sở hiển thị trên các chứng từ y tế in ra.</span>
                </label>
              </div>
            </div>
          </div>

          <div class="form-actions">
            <button class="button button-primary" type="submit" :disabled="busy || needsReview || !confirmed">
              {{ busy ? 'Đang lưu thông tin...' : 'Lưu thông tin in ấn' }}
            </button>
          </div>
        </form>

        <div v-else-if="busy" class="loading-state">
          Đang tải thông tin cấu hình bản in...
        </div>
      </div>
    </div>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { clinicPrintSettingsService, type ClinicPrintSettings } from '@/services/clinicPrintSettings';
const auth = useAuthStore(), allowed = computed(() => auth.role === 'ADMIN');
const address = ref(''), phone = ref(''), revision = ref(''), loaded = ref(''), busy = ref(false), confirmed = ref(false), needsReview = ref(false), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => auth.user?.userId + '/' + auth.role;
function clear() { address.value = ''; phone.value = ''; revision.value = ''; loaded.value = ''; confirmed.value = false; }
function alive(n: number, id: string) { return !disposed && generation === n && id === identity(); }
function valid(s: ClinicPrintSettings) { return /^[A-F0-9]{64}$/.test(s.revision) && (s.address === null || typeof s.address === 'string') && (s.phone === null || typeof s.phone === 'string'); }
async function load() {
 if (disposed || busy.value || !allowed.value) return;
 const n = ++generation, id = identity(); clear(); busy.value = true; error.value = ''; message.value = '';
 try { const data = await clinicPrintSettingsService.read(); if (!alive(n,id)) return; if (!valid(data)) throw Error('Invalid settings'); address.value = data.address ?? ''; phone.value = data.phone ?? ''; revision.value = data.revision; loaded.value = id; needsReview.value = false; }
 catch { if (alive(n,id)) { clear(); needsReview.value = true; error.value = 'Không đọc được cấu hình; kiểm tra quyền và tải lại.'; } }
 finally { if (alive(n,id)) busy.value = false; }
}
async function save() {
 if (disposed || busy.value || !allowed.value || loaded.value !== identity() || needsReview.value || !confirmed.value) return;
 if (!address.value.trim() || address.value.length > 500 || /[\x00-\x1f\x7f]/.test(address.value) || !/^[0-9+ ().-]{8,30}$/.test(phone.value)) { error.value = 'Kiểm tra địa chỉ tối đa 500 ký tự và hotline 8–30 ký tự.'; return; }
 const n = generation, id = identity(); busy.value = true; confirmed.value = false; error.value = ''; message.value = '';
 try { const data = await clinicPrintSettingsService.save(address.value.trim(), phone.value.trim(), revision.value); if (!alive(n,id)) return; if (!valid(data) || data.address !== address.value.trim() || data.phone !== phone.value.trim()) throw Error('Invalid saved settings'); revision.value = data.revision; message.value = 'Đã lưu thông tin phòng khám và audit.'; }
 catch { if (alive(n,id)) { needsReview.value = true; loaded.value = ''; error.value = 'Chưa xác minh kết quả lưu hoặc thông tin vừa đổi. Tải lại trước khi gửi tiếp.'; } }
 finally { if (alive(n,id)) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; message.value = ''; if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
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

/* Print Preview Box */
.print-preview-box {
  position: relative;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 10px;
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.75rem;
}

.preview-brand {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.preview-logo {
  height: 48px;
  width: auto;
  display: block;
}

.preview-clinic-details {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  border-left: 2px solid #e2e8f0;
  padding-left: 1.25rem;
}

.preview-clinic-name {
  font-size: 1.05rem;
  font-weight: 850;
  color: #0f2d59;
  letter-spacing: 0.02em;
}

.preview-clinic-address {
  font-size: 0.85rem;
  color: #475569;
}

.preview-clinic-phone {
  font-size: 0.825rem;
  color: #0284c7;
  font-weight: 700;
}

.preview-badge {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  font-size: 0.7rem;
  font-weight: 750;
  color: #64748b;
  background: #e2e8f0;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

/* Form */
.form-grid {
  display: grid;
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

.loading-state {
  text-align: center;
  padding: 2.5rem 1rem;
  color: #64748b;
  font-size: 0.925rem;
}
</style>
