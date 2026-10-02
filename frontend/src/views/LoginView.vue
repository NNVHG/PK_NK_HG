<template>
  <div class="login-wrapper">
    <div class="login-card">
      <h2>Đăng Nhập Hệ Thống</h2>
      <p class="subtitle">Phòng Khám Nha Khoa</p>
      <div v-if="registrationNotice" class="success-alert">{{ registrationNotice }}</div>

      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <label for="phone">Số điện thoại</label>
          <input
            id="phone"
            v-model="phone"
            type="text"
            placeholder="0912345678"
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
      <p class="register-link">Chưa có tài khoản? <RouterLink to="/register">Đăng ký</RouterLink></p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { RouterLink, useRouter, useRoute } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const phone = ref('');
const password = ref('');
const errorMessage = ref('');
const loading = ref(false);
const registrationNotice = computed(() => route.query.registered === '1'
  ? 'Đăng ký thành công. Vui lòng đăng nhập bằng tài khoản vừa tạo.'
  : '');

async function handleSubmit() {
  errorMessage.value = '';
  loading.value = true;
  try {
    await authStore.login(phone.value, password.value);
    const redirect = (route.query.redirect as string) || '/';
    router.push(redirect);
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Đăng nhập thất bại. Vui lòng kiểm tra lại.';
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.login-wrapper {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.login-card {
  background: #fff;
  padding: 2rem 2.5rem;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 400px;
}

.login-card h2 {
  font-size: 1.5rem;
  margin-bottom: 0.25rem;
  text-align: center;
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
  border-radius: 4px;
  font-size: 0.95rem;
}

.form-group input:focus {
  outline: none;
  border-color: #3498db;
}

.error-alert {
  background-color: #fde8e8;
  color: #c81e1e;
  padding: 0.6rem 0.8rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-bottom: 1.25rem;
}

.success-alert {
  background-color: #e8f7ee;
  color: #187044;
  padding: 0.6rem 0.8rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-bottom: 1.25rem;
}

.register-link {
  margin: 1.25rem 0 0;
  text-align: center;
  color: #59636e;
  font-size: 0.9rem;
}

.register-link a { color: #287bb5; font-weight: 600; }

.btn-submit {
  width: 100%;
  background-color: #3498db;
  color: white;
  border: none;
  padding: 0.75rem;
  font-size: 1rem;
  font-weight: 600;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.btn-submit:hover:not(:disabled) {
  background-color: #2980b9;
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
