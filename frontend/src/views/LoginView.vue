<template>
  <div class="login-page">
    <div class="login-container">
      <!-- Left side: Clinical brand showcase -->
      <div class="hero-copy">
        <RouterLink class="brand-link" to="/" aria-label="Nha khoa Hoàng Gia - Trang chủ" title="Về trang chủ">
          <img src="/logo-ngang.svg" alt="Nha Khoa Hoàng Gia" class="brand-image" width="180" height="48" />
        </RouterLink>
        <p class="eyebrow"><span aria-hidden="true">✦</span> Hệ thống Vận hành Phòng khám</p>
        <h1 id="login-heading">Nụ cười Hoàng Gia <span>tỏa sáng mỗi ngày</span></h1>
        <p class="intro">
          Cổng thông tin chuyên môn nha khoa: hồ sơ bệnh án điện tử, sơ đồ răng giải phẫu FDI 32/20, đơn thuốc điện tử và theo dõi tiến trình điều trị.
        </p>
        <div class="feature-badges">
          <span class="badge"><span class="badge-icon">🦷</span> Sơ đồ răng FDI</span>
          <span class="badge"><span class="badge-icon">🛡️</span> Cảnh báo an toàn & dị ứng</span>
          <span class="badge"><span class="badge-icon">📋</span> Kê đơn & hồ sơ trực tuyến</span>
        </div>
        <p class="hero-note">
          Tài khoản kích hoạt lần đầu sẽ được hệ thống tự động nhận diện và chuyển tiếp sang bước xác thực OTP an toàn.
        </p>
      </div>

      <!-- Right side: Authentication card -->
      <div class="login-wrapper">
        <div class="login-card">
          <div class="login-brand">
            <RouterLink to="/" title="Về trang chủ">
              <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="48" height="48" />
            </RouterLink>
          </div>
          <h2>Đăng Nhập Hệ Thống</h2>
          <p class="subtitle">Nha Khoa Hoàng Gia</p>
          <div v-if="registrationNotice" class="success-alert">{{ registrationNotice }}</div>

          <p v-if="$route.query.phoneChanged === '1'" class="info-alert" role="status">
            Đã đổi SĐT. Nhân viên đăng nhập bằng số mới. Bệnh nhân đăng nhập bằng CCCD/Hộ chiếu và mật khẩu hiện tại, sau đó xác minh OTP gửi tới số mới.
          </p>

          <div class="document-type-selector">
            <label class="doc-radio">
              <input type="radio" value="cccd" v-model="loginMode" />
              <span>SĐT / CCCD</span>
            </label>
            <label class="doc-radio">
              <input type="radio" value="passport" v-model="loginMode" />
              <span>Hộ chiếu</span>
            </label>
          </div>

          <form @submit.prevent="handleSubmit">
            <div class="form-group" v-if="loginMode === 'passport'">
              <label for="country">Quốc gia cấp hộ chiếu</label>
              <input
                id="country"
                v-model="nationalityCode"
                type="text"
                placeholder="Ví dụ: VN, USA, JPN"
                required
                maxlength="3"
                autocomplete="country-name"
              />
            </div>

            <div class="form-group">
              <label for="phone">{{ loginMode === 'passport' ? 'Số Hộ chiếu' : 'SĐT (nhân viên) hoặc CCCD (bệnh nhân)' }}</label>
              <input
                id="phone"
                v-model="phone"
                type="text"
                :placeholder="loginMode === 'passport' ? 'Nhập số hộ chiếu' : 'Số điện thoại hoặc CCCD'"
                required
                autocomplete="username"
              />
            </div>

            <div class="form-group">
              <label for="password">Mật khẩu</label>
              <input
                id="password"
                v-model="password"
                type="password"
                placeholder="••••••••"
                required
                autocomplete="current-password"
              />
            </div>

            <div v-if="errorMessage" class="error-alert">
              {{ errorMessage }}
            </div>

            <button type="submit" class="btn-submit" :disabled="loading">
              {{ loading ? 'Đang xác thực...' : 'Đăng nhập' }}
            </button>
          </form>

          <p class="register-link"><RouterLink to="/forgot-password">Quên mật khẩu?</RouterLink></p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { RouterLink, useRouter, useRoute } from 'vue-router';
import { stageActivationPrefill } from '@/services/patientActivation';

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const loginMode = ref<'cccd' | 'passport'>('cccd');
const nationalityCode = ref('');
const phone = ref('');
const password = ref('');
const errorMessage = ref('');
const loading = ref(false);
const registrationNotice = computed(() => route.query.registered === '1'
  ? 'Đăng ký thành công. Vui lòng đăng nhập bằng tài khoản vừa tạo.'
  : '');

async function handleSubmit() {
  errorMessage.value = '';
  if (loginMode.value === 'passport' && !/^[A-Za-z]{2,3}$/.test(nationalityCode.value.trim())) {
    errorMessage.value = 'Nhập mã quốc gia cấp hộ chiếu gồm 2–3 chữ cái.';
    return;
  }
  loading.value = true;
  try {
    const docType = loginMode.value === 'passport' ? 2 : (phone.value.length === 12 ? 1 : null);
    const country = loginMode.value === 'passport' ? nationalityCode.value.trim().toUpperCase() : null;

    await authStore.login(phone.value, password.value, docType, country);
    const defaultRoute = authStore.role === 'PATIENT' ? '/' : '/management/dashboard';
    const redirect = (route.query.redirect as string) || defaultRoute;
    router.push(redirect);
  } catch (err: any) {
    if (err.response?.status === 401 && err.response?.data?.code === 'AUTH_ACTIVATION_REQUIRED') {
      stageActivationPrefill({
        identifier: phone.value,
        documentType: loginMode.value,
        nationalityCode: loginMode.value === 'passport' ? nationalityCode.value.trim().toUpperCase() : ''
      });
      router.push('/activate-account');
      return;
    }

    if (err.response?.status === 423) {
      errorMessage.value = err.response?.data?.message || 'Tài khoản tạm thời bị khóa 15 phút do nhập sai nhiều lần.';
    } else {
      errorMessage.value = err.response?.data?.message || 'Đăng nhập thất bại. Vui lòng kiểm tra lại.';
    }
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  width: 100%;
  color: var(--color-text-main, #0f172a);
  background: radial-gradient(circle at 10% 20%, #e8f3fc 0%, #f7fafd 50%, #ffffff 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem 1.5rem;
  box-sizing: border-box;
}

.login-container {
  width: 100%;
  max-width: 1120px;
  margin: auto;
  display: grid;
  grid-template-columns: 1.15fr 0.85fr;
  align-items: center;
  gap: 3.5rem;
  box-sizing: border-box;
}

.hero-copy {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.brand-link {
  display: inline-block;
  margin-bottom: 0.5rem;
  text-decoration: none;
}

.brand-image {
  display: block;
  width: 180px;
  height: auto;
}

.eyebrow {
  color: var(--color-primary-hover, #1857b6);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: var(--font-size-xs, 0.75rem);
  font-weight: 800;
  margin: 0;
}

.eyebrow span {
  color: var(--color-accent, #e5b95e);
  margin-right: 4px;
}

h1 {
  margin: 0.25rem 0 0.5rem;
  color: var(--color-primary, #102e56);
  font-size: clamp(2rem, 3.5vw, 3rem);
  line-height: var(--line-height-tight, 1.2);
  letter-spacing: -0.03em;
  font-weight: 800;
}

h1 span {
  color: var(--color-medical, #0284c7);
}

.intro {
  margin: 0;
  max-width: 520px;
  color: var(--color-text-muted, #475569);
  font-size: var(--font-size-base, 1rem);
  line-height: var(--line-height-relaxed, 1.625);
}

.feature-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-top: 0.5rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  background: rgba(255, 255, 255, 0.85);
  border: 1px solid var(--color-primary-border, #bfdbfe);
  border-radius: var(--radius-pill, 9999px);
  padding: 0.35rem 0.85rem;
  font-size: var(--font-size-xs, 0.75rem);
  font-weight: 600;
  color: var(--color-primary, #102e56);
  box-shadow: 0 2px 4px rgba(16, 46, 86, 0.04);
}

.badge-icon {
  font-size: 0.85rem;
}

.hero-note {
  margin: 0.5rem 0 0;
  color: var(--color-text-subtle, #64748b);
  font-size: var(--font-size-xs, 0.825rem);
  line-height: 1.5;
  font-style: italic;
}

.login-wrapper {
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100%;
}

.login-card {
  background: var(--color-surface-card, #ffffff);
  padding: 2.25rem;
  border-radius: var(--radius-xl, 20px);
  border: 1px solid var(--color-primary-border, #bfdbfe);
  box-shadow: 0 20px 45px -12px rgba(16, 46, 86, 0.15), 0 2px 8px rgba(16, 46, 86, 0.05);
  width: 100%;
  max-width: 440px;
  box-sizing: border-box;
}

.login-brand {
  display: flex;
  justify-content: center;
  margin-bottom: 0.75rem;
}

.brand-logo {
  display: block;
  transition: transform 0.2s ease;
}

.brand-logo:hover {
  transform: scale(1.08);
}

.login-card h2 {
  font-size: 1.45rem;
  margin: 0 0 0.25rem;
  text-align: center;
  color: var(--color-primary, #102e56);
  font-weight: 750;
}

.subtitle {
  color: var(--color-text-subtle, #64748b);
  font-size: var(--font-size-sm, 0.875rem);
  text-align: center;
  margin: 0 0 1.25rem;
}

.document-type-selector {
  display: flex;
  gap: 1.25rem;
  justify-content: center;
  margin-bottom: 1.25rem;
  padding: 0.45rem;
  background: var(--color-surface-subtle, #f8fafc);
  border: 1px solid var(--color-border-subtle, #e2e8f0);
  border-radius: var(--radius-md, 10px);
}

.doc-radio {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: var(--font-size-sm, 0.875rem);
  font-weight: 600;
  cursor: pointer;
  color: var(--color-text-muted, #475569);
}

.doc-radio input[type="radio"] {
  accent-color: var(--color-medical, #0284c7);
  width: 16px;
  height: 16px;
}

.form-group {
  margin-bottom: 1.15rem;
}

.form-group label {
  display: block;
  font-weight: 600;
  margin-bottom: 0.4rem;
  font-size: var(--font-size-sm, 0.875rem);
  color: var(--color-text-main, #0f172a);
}

.form-group input {
  width: 100%;
  min-height: var(--input-height, 44px);
  padding: 0.65rem 0.85rem;
  border: 1px solid var(--color-border-medium, #cbd5e1);
  border-radius: var(--radius-md, 10px);
  font-size: var(--font-size-base, 0.95rem);
  background-color: #ffffff;
  color: var(--color-text-main, #0f172a);
  box-sizing: border-box;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.form-group input:focus {
  outline: none;
  border-color: var(--color-medical, #0284c7);
  box-shadow: var(--shadow-focus, 0 0 0 3px rgba(2, 132, 199, 0.2));
}

.error-alert {
  background-color: var(--color-danger-light, #fee2e2);
  color: var(--color-danger, #dc2626);
  border: 1px solid var(--color-danger-border, #fca5a5);
  padding: 0.65rem 0.85rem;
  border-radius: var(--radius-md, 8px);
  font-size: var(--font-size-sm, 0.85rem);
  margin-bottom: 1.25rem;
  font-weight: 500;
}

.success-alert {
  background-color: var(--color-success-light, #dcfce7);
  color: var(--color-success, #16a34a);
  border: 1px solid var(--color-success-border, #86efac);
  padding: 0.65rem 0.85rem;
  border-radius: var(--radius-md, 8px);
  font-size: var(--font-size-sm, 0.85rem);
  margin-bottom: 1.25rem;
  font-weight: 500;
}

.info-alert {
  background-color: var(--color-medical-light, #e0f2fe);
  color: var(--color-medical-hover, #0369a1);
  padding: 0.65rem 0.85rem;
  border-radius: var(--radius-md, 8px);
  font-size: var(--font-size-sm, 0.85rem);
  margin-bottom: 1.25rem;
  line-height: 1.45;
}

.btn-submit {
  width: 100%;
  min-height: 48px;
  background-color: var(--color-primary-hover, #1857b6);
  color: #ffffff;
  border: none;
  padding: 0.75rem 1.25rem;
  font-size: var(--font-size-base, 1rem);
  font-weight: 700;
  border-radius: var(--radius-md, 10px);
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(24, 87, 182, 0.25);
  transition: background-color 0.2s ease, transform 0.1s ease, box-shadow 0.2s ease;
}

.btn-submit:hover:not(:disabled) {
  background-color: #124794;
  box-shadow: 0 6px 16px rgba(24, 87, 182, 0.35);
}

.btn-submit:active:not(:disabled) {
  transform: translateY(1px);
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  box-shadow: none;
}

.register-link {
  margin: 1.15rem 0 0;
  text-align: center;
  color: var(--color-text-subtle, #64748b);
  font-size: var(--font-size-sm, 0.875rem);
}

.register-link a {
  color: var(--color-primary-hover, #1857b6);
  font-weight: 600;
  text-decoration: none;
}

.register-link a:hover {
  text-decoration: underline;
}

@media (max-width: 900px) {
  .login-container {
    grid-template-columns: 1fr;
    gap: 2rem;
    padding: 1rem 0;
  }

  .hero-copy {
    text-align: center;
    align-items: center;
  }

  .brand-link {
    margin: 0 auto;
  }

  .feature-badges {
    justify-content: center;
  }

  .intro {
    max-width: 100%;
  }

  .login-card {
    padding: 1.75rem 1.25rem;
  }
}
</style>
