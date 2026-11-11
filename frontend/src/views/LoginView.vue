<template>
  <div class="login-page">
    <header class="topbar">
      <div class="topbar-inner">
        <RouterLink class="brand" to="/" aria-label="Nha khoa Hoàng Gia - Trang chủ">
          <img src="/logo-ngang.svg" alt="" width="164" height="44" />
        </RouterLink>
        <nav class="navigation" aria-label="Điều hướng chính">
          <RouterLink class="nav-about" to="/">Trang chủ</RouterLink>
          <RouterLink class="login-link" to="/login">Đăng nhập</RouterLink>
          <RouterLink class="button button-gold nav-book" to="/book-appointment">Đặt lịch khám</RouterLink>
        </nav>
      </div>
    </header>

    <section class="hero" aria-labelledby="login-heading">
      <div class="hero-inner">
        <div class="hero-copy">
          <p class="eyebrow"><span aria-hidden="true">✦</span> Phòng khám Nha khoa Hoàng Gia</p>
          <h1 id="login-heading">Nụ cười Hoàng Gia <span>tỏa sáng mỗi ngày</span></h1>
          <p class="intro">Đăng nhập để tra cứu hồ sơ khám răng, sơ đồ răng giải phẫu FDI, đơn thuốc điện tử và theo dõi các đợt thanh toán.</p>
          <div class="hero-actions">
            <RouterLink class="button button-gold" to="/book-appointment">Đặt lịch khám <span aria-hidden="true">→</span></RouterLink>
            <RouterLink class="button button-outline" to="/activate-account">Kích hoạt lần đầu (OTP)</RouterLink>
          </div>
          <p class="hero-note">Khách hàng mới sẽ được Lễ tân đối chiếu giấy tờ định danh tại quầy để cấp tài khoản.</p>
        </div>

        <div class="login-wrapper">
          <div class="login-card">
            <div class="login-brand">
              <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="48" height="48" />
            </div>
            <h2>Đăng Nhập Hệ Thống</h2>
            <p class="subtitle">Nha Khoa Hoàng Gia</p>
            <div v-if="registrationNotice" class="success-alert">{{ registrationNotice }}</div>

            <p v-if="$route.query.phoneChanged === '1'" role="status">Đã đổi SĐT. Nhân viên đăng nhập bằng số mới. Bệnh nhân đăng nhập bằng CCCD/Hộ chiếu và mật khẩu hiện tại, sau đó xác minh OTP gửi tới số mới.</p>

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
                <label for="phone">{{ loginMode === 'passport' ? 'Số Hộ chiếu' : 'SĐT (nhân viên/tài khoản cũ) hoặc CCCD (bệnh nhân)' }}</label>
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
            <p class="register-link"><RouterLink to="/book-appointment">Gửi yêu cầu đặt lịch trước khi có tài khoản</RouterLink></p>
            <p class="register-link"><RouterLink to="/activate-account">Kích hoạt tài khoản mới của bệnh nhân hoặc nhân viên</RouterLink></p>
          </div>
        </div>
      </div>
    </section>

    <footer class="footer">
      <span>Nha khoa Hoàng Gia</span>
      <RouterLink to="/book-appointment">Đặt lịch khám</RouterLink>
    </footer>
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
    const defaultRoute = authStore.role === 'PATIENT' ? '/patient/dashboard' : '/management/dashboard';
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
  color: #18354c;
  background: #fff;
  display: flex;
  flex-direction: column;
}

.topbar {
  background: #fff;
  border-bottom: 1px solid #e6eff6;
  padding-top: max(0px, env(safe-area-inset-top, 0px));
}

.topbar-inner {
  max-width: 1200px;
  min-height: 76px;
  margin: auto;
  padding: 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  display: inline-flex;
  flex-shrink: 0;
  align-items: center;
}

.brand img {
  display: block;
  width: 164px;
  height: auto;
}

.navigation {
  display: flex;
  align-items: center;
  gap: 24px;
}

.navigation > a {
  font-size: 0.9rem;
  font-weight: 700;
  text-decoration: none;
}

.nav-about {
  color: #425c73;
}

.login-link {
  color: #1857b6;
  border: 1px solid #c5d8eb;
  border-radius: 9px;
  padding: 11px 17px;
  background: #edf6fd;
}

.button {
  display: inline-flex;
  min-height: 48px;
  box-sizing: border-box;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 0 22px;
  border-radius: 10px;
  text-decoration: none;
  font-weight: 750;
  transition: background-color 0.15s, color 0.15s;
}

.button-gold {
  background: #e5b95e;
  color: #102e56;
}

.button-gold:hover {
  background: #d9a948;
}

.button-outline {
  border: 1px solid #b9d0e6;
  background: #fff;
  color: #1857b6;
}

.button-outline:hover {
  background: #edf6fd;
}

.hero {
  flex: 1;
  overflow: hidden;
  background: #f1f9ff;
}

.hero-inner {
  max-width: 1200px;
  min-height: 540px;
  margin: auto;
  padding: 40px 24px;
  display: grid;
  grid-template-columns: 1.05fr 0.95fr;
  align-items: center;
  gap: 28px;
  box-sizing: border-box;
}

.hero-copy {
  position: relative;
  z-index: 1;
  padding: 24px 0;
}

.eyebrow {
  color: #1857b6;
  text-transform: uppercase;
  letter-spacing: 0.09em;
  font-size: 0.76rem;
  font-weight: 800;
}

.eyebrow span {
  color: #cc9b3a;
  margin-right: 5px;
}

h1 {
  margin: 16px 0 20px;
  color: #102e56;
  max-width: 14ch;
  font-size: clamp(2.2rem, 4vw, 3.6rem);
  line-height: 1.12;
  letter-spacing: -0.035em;
}

h1 span {
  color: #397bcc;
}

.intro {
  max-width: 520px;
  color: #4a6479;
  font-size: 1.02rem;
  line-height: 1.7;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 24px;
}

.hero-note {
  margin: 18px 0 0;
  color: #526c80;
  font-size: 0.88rem;
  line-height: 1.5;
}

.login-wrapper {
  display: flex;
  justify-content: center;
  align-items: center;
}

.login-card {
  background: #fff;
  padding: 2rem 2.25rem;
  border-radius: 16px;
  border: 1px solid #d8ecfa;
  box-shadow: 0 16px 48px rgba(24, 87, 182, 0.12);
  width: 100%;
  max-width: 440px;
  box-sizing: border-box;
}

.document-type-selector {
  display: flex;
  gap: 1.5rem;
  justify-content: center;
  margin-bottom: 1.25rem;
  padding: 0.5rem;
  background: #f8fafc;
  border-radius: 6px;
}

.doc-radio {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: #475569;
}

.doc-radio input[type="radio"] {
  accent-color: #0284c7;
}

.login-brand {
  display: flex;
  justify-content: center;
  margin-bottom: 0.75rem;
}

.brand-logo {
  display: block;
}

.login-card h2 {
  font-size: 1.5rem;
  margin-bottom: 0.25rem;
  text-align: center;
  color: #102e56;
}

.subtitle {
  color: #7f8c8d;
  font-size: 0.9rem;
  text-align: center;
  margin-bottom: 1.5rem;
}

.form-group {
  margin-bottom: 1.25rem;
}

.form-group label {
  display: block;
  font-weight: 500;
  margin-bottom: 0.4rem;
  font-size: 0.9rem;
}

.form-group input {
  width: 100%;
  padding: 0.6rem 0.8rem;
  border: 1px solid #ccd1d9;
  border-radius: 8px;
  font-size: 0.95rem;
  box-sizing: border-box;
}

.form-group input:focus {
  outline: none;
  border-color: #1857b6;
}

.error-alert {
  background-color: #fde8e8;
  color: #c81e1e;
  padding: 0.6rem 0.8rem;
  border-radius: 6px;
  font-size: 0.85rem;
  margin-bottom: 1.25rem;
}

.success-alert {
  background-color: #e8f7ee;
  color: #187044;
  padding: 0.6rem 0.8rem;
  border-radius: 6px;
  font-size: 0.85rem;
  margin-bottom: 1.25rem;
}

.register-link {
  margin: 1rem 0 0;
  text-align: center;
  color: #59636e;
  font-size: 0.9rem;
}

.register-link a {
  color: #1857b6;
  font-weight: 600;
}

.btn-submit {
  width: 100%;
  background-color: #1857b6;
  color: white;
  border: none;
  padding: 0.75rem;
  font-size: 1rem;
  font-weight: 600;
  border-radius: 10px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.btn-submit:hover:not(:disabled) {
  background-color: #124794;
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.footer {
  border-top: 1px solid #e6eff6;
  padding: 24px max(24px, calc((100% - 1152px) / 2));
  display: flex;
  justify-content: space-between;
  gap: 20px;
  color: #526c80;
  font-size: 0.875rem;
  background: #fff;
}

.footer a {
  color: #1857b6;
}

@media (max-width: 820px) {
  .topbar-inner {
    min-height: 68px;
    padding: 0 16px;
    gap: 8px;
  }
  .brand img {
    width: 130px;
  }
  .navigation {
    gap: 10px;
  }
  .nav-about {
    display: none;
  }
  .navigation > a {
    font-size: 0.8rem;
  }
  .login-link {
    padding: 10px;
  }
  .nav-book {
    min-height: 40px;
    padding: 0 10px;
  }
  .hero-inner {
    grid-template-columns: 1fr;
    gap: 20px;
    padding: 24px 16px 36px;
  }
  .hero-copy {
    padding: 12px 0 4px;
  }
  .login-card {
    padding: 1.5rem 1.25rem;
  }
  .footer {
    padding: 20px 16px;
  }
}
</style>
