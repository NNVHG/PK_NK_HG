<template>
  <div class="register-wrapper">
    <section class="register-card">
      <h1>Tạo tài khoản bệnh nhân</h1>
      <p class="subtitle">Đăng ký để sử dụng dịch vụ phòng khám nha khoa.</p>

      <form @submit.prevent="handleSubmit" novalidate>
        <div class="form-grid">
          <label class="form-group full-width">
            <span>Họ và tên</span>
            <input v-model.trim="form.fullName" type="text" maxlength="100" autocomplete="name" />
          </label>
          <label class="form-group">
            <span>Số điện thoại</span>
            <input v-model.trim="form.phone" type="tel" inputmode="numeric" autocomplete="tel" placeholder="0912345678" />
          </label>
          <label class="form-group">
            <span>Ngày sinh</span>
            <input v-model="form.dateOfBirth" type="date" />
          </label>
          <label class="form-group">
            <span>Giới tính</span>
            <select v-model="form.gender">
              <option disabled value="">Chọn giới tính</option>
              <option value="Female">Nữ</option>
              <option value="Male">Nam</option>
              <option value="Other">Khác</option>
            </select>
          </label>
          <label class="form-group">
            <span>Email <small>(không bắt buộc)</small></span>
            <input v-model.trim="form.email" type="email" maxlength="100" autocomplete="email" />
          </label>
          <label class="form-group">
            <span>Mật khẩu</span>
            <input v-model="form.password" type="password" autocomplete="new-password" />
          </label>
          <label class="form-group">
            <span>Nhập lại mật khẩu</span>
            <input v-model="confirmPassword" type="password" autocomplete="new-password" />
          </label>
        </div>

        <div v-if="errorMessage" class="error-alert" role="alert">{{ errorMessage }}</div>
        <button class="btn-submit" type="submit" :disabled="loading">
          {{ loading ? 'Đang tạo tài khoản...' : 'Đăng ký' }}
        </button>
      </form>
      <p class="login-link">Đã có tài khoản? <RouterLink to="/login">Đăng nhập</RouterLink></p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import { isAxiosError } from 'axios';
import { RouterLink, useRouter } from 'vue-router';
import { registerPatient, type RegisterPayload } from '@/services/auth';

const router = useRouter();
const loading = ref(false);
const errorMessage = ref('');
const confirmPassword = ref('');
const form = reactive({
  fullName: '',
  phone: '',
  password: '',
  dateOfBirth: '',
  gender: '' as RegisterPayload['gender'] | '',
  email: ''
});

function validateForm(): string | null {
  if (!form.fullName.trim()) return 'Vui lòng nhập họ và tên.';
  if (!/^0\d{9}$/.test(form.phone)) return 'Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).';
  if (!form.dateOfBirth) return 'Vui lòng chọn ngày sinh.';
  if (!form.gender) return 'Vui lòng chọn giới tính.';
  if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) return 'Email không đúng định dạng.';
  if (form.password.length < 6 || !/[A-Z]/.test(form.password) || !/[a-z]/.test(form.password) || !/[0-9]/.test(form.password)) {
    return 'Mật khẩu cần có ít nhất 6 ký tự, gồm chữ hoa, chữ thường và chữ số.';
  }
  if (confirmPassword.value !== form.password) return 'Mật khẩu nhập lại không khớp.';
  return null;
}

function getBackendMessage(error: unknown): string {
  if (!isAxiosError<{ message?: string; errors?: Array<{ message?: string }> }>(error)) {
    return 'Không thể đăng ký lúc này. Vui lòng thử lại.';
  }

  const responseData = error.response?.data;
  return responseData?.errors?.map(item => item.message).filter((message): message is string => Boolean(message)).join(' ')
    || responseData?.message
    || 'Không thể kết nối máy chủ. Vui lòng thử lại.';
}

async function handleSubmit() {
  errorMessage.value = validateForm() ?? '';
  if (errorMessage.value) return;

  loading.value = true;
  try {
    await registerPatient({
      fullName: form.fullName,
      phone: form.phone,
      password: form.password,
      dateOfBirth: form.dateOfBirth,
      gender: form.gender as RegisterPayload['gender'],
      email: form.email || null
    });
    await router.push({ name: 'login', query: { registered: '1' } });
  } catch (error: unknown) {
    errorMessage.value = getBackendMessage(error);
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.register-wrapper { display: flex; justify-content: center; padding: 1.5rem 0; }
.register-card { width: 100%; max-width: 760px; padding: 2rem; border-radius: 12px; background: #fff; box-shadow: 0 8px 28px rgba(30, 52, 70, .1); }
h1 { margin: 0; color: #20364b; font-size: 1.6rem; }
.subtitle { margin: .45rem 0 1.5rem; color: #697887; }
.form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.form-group { display: flex; flex-direction: column; gap: .4rem; color: #354657; font-size: .9rem; font-weight: 600; }
.form-group small { color: #7a8792; font-weight: 400; }
.form-group input, .form-group select { box-sizing: border-box; width: 100%; min-height: 42px; padding: .6rem .7rem; border: 1px solid #d5dde4; border-radius: 6px; background: #fff; font: inherit; font-weight: 400; }
.form-group input:focus, .form-group select:focus { outline: 2px solid #d2e8fa; border-color: #4c9bd1; }
.full-width { grid-column: 1 / -1; }
.error-alert { margin-top: 1rem; padding: .75rem; border-radius: 6px; background: #fde8e8; color: #a92323; font-size: .9rem; }
.btn-submit { width: 100%; margin-top: 1.25rem; padding: .8rem; border: 0; border-radius: 7px; background: #287fb8; color: #fff; font-size: 1rem; font-weight: 700; cursor: pointer; }
.btn-submit:disabled { opacity: .6; cursor: wait; }
.login-link { margin: 1rem 0 0; text-align: center; color: #657382; font-size: .9rem; }
.login-link a { color: #287bb5; font-weight: 700; }
@media (max-width: 600px) { .register-card { padding: 1.25rem; } .form-grid { grid-template-columns: 1fr; } .full-width { grid-column: auto; } }
</style>
