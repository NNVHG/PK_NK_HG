<template>
  <div class="dashboard">
    <h1>Bảng Điều Khiển</h1>
    <p class="welcome">Xin chào, <strong>{{ authStore.user?.fullName }}</strong>!</p>

    <div class="top-section">
      <div class="user-card">
        <h3>Thông tin tài khoản</h3>
        <p><strong>Mã ID:</strong> #{{ authStore.user?.userId }}</p>
        <p><strong>Họ và tên:</strong> {{ authStore.user?.fullName }}</p>
        <p><strong>Vai trò:</strong> {{ authStore.user?.roleName }} (<code>{{ authStore.user?.roleCode }}</code>)</p>
      </div>

      <div class="password-card">
        <h3>Đổi Mật Khẩu (F_AUTH_03)</h3>
        <form @submit.prevent="handleChangePassword">
          <div class="form-group">
            <label>Mật khẩu cũ</label>
            <input v-model="oldPassword" type="password" placeholder="••••••••" required />
          </div>
          <div class="form-group">
            <label>Mật khẩu mới</label>
            <input v-model="newPassword" type="password" placeholder="Tối thiểu 6 ký tự: 1 hoa, 1 thường, 1 số" required />
          </div>
          <div class="form-group">
            <label>Xác nhận mật khẩu mới</label>
            <input v-model="confirmPassword" type="password" placeholder="Nhập lại mật khẩu mới" required />
          </div>

          <div v-if="pwdMessage" class="alert-success">{{ pwdMessage }}</div>
          <div v-if="pwdError" class="alert-error">{{ pwdError }}</div>

          <button type="submit" class="btn-pwd" :disabled="pwdLoading">
            {{ pwdLoading ? 'Đang xử lý...' : 'Cập nhật mật khẩu' }}
          </button>
        </form>
      </div>
    </div>

    <div v-if="isStaff" class="modules-grid">
      <div class="card" @click="goTo('/appointments')">
        <h3>Lịch Hẹn (MOD_APP)</h3>
        <p>Xem và sắp xếp lịch hẹn khám răng, đặt hẹn theo ghế và bác sĩ.</p>
      </div>
      <div class="card" @click="goTo('/patients')">
        <h3>Bệnh Nhân (MOD_PAT)</h3>
        <p>Hồ sơ hành chính bệnh nhân, tiền sử bệnh lý toàn thân và liên hệ.</p>
      </div>
    </div>
    <div v-else class="patient-banner">
      <h3>Cổng Thông Tin Dành Cho Bệnh Nhân</h3>
      <p>Hồ sơ răng hàm mặt, đơn thuốc và lịch sử khám của bạn sẽ xuất hiện tại đây khi bác sĩ tiến hành điều trị.</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter } from 'vue-router';
import { apiClient } from '@/services/api';

const authStore = useAuthStore();
const router = useRouter();

const isStaff = computed(() => {
  const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
  return staffRoles.includes(authStore.role);
});

const oldPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const pwdMessage = ref('');
const pwdError = ref('');
const pwdLoading = ref(false);

function goTo(path: string) {
  router.push(path);
}

async function handleChangePassword() {
  pwdError.value = '';
  pwdMessage.value = '';

  if (newPassword.value !== confirmPassword.value) {
    pwdError.value = 'Mật khẩu mới và xác nhận mật khẩu không trùng khớp.';
    return;
  }

  pwdLoading.value = true;
  try {
    const res = await apiClient.post('/auth/change-password', {
      oldPassword: oldPassword.value,
      newPassword: newPassword.value
    });
    pwdMessage.value = res.data?.message || 'Đổi mật khẩu thành công!';
    oldPassword.value = '';
    newPassword.value = '';
    confirmPassword.value = '';
  } catch (err: any) {
    const data = err.response?.data;
    if (data?.errors && Array.isArray(data.errors)) {
      pwdError.value = data.errors.map((e: any) => e.message).join(' ');
    } else {
      pwdError.value = data?.message || 'Đổi mật khẩu thất bại. Vui lòng thử lại.';
    }
  } finally {
    pwdLoading.value = false;
  }
}
</script>

<style scoped>
.dashboard h1 {
  font-size: 1.75rem;
  margin-bottom: 0.5rem;
}

.welcome {
  color: #555;
  margin-bottom: 1.5rem;
}

.top-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.5rem;
  margin-bottom: 1.5rem;
}

.user-card, .password-card {
  background: #fff;
  padding: 1.25rem 1.5rem;
  border-radius: 6px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.05);
}

.user-card h3, .password-card h3 {
  margin-bottom: 0.75rem;
  font-size: 1.1rem;
  color: #2c3e50;
  border-bottom: 1px solid #f0f0f0;
  padding-bottom: 0.5rem;
}

.user-card p {
  margin: 0.4rem 0;
}

.form-group {
  margin-bottom: 0.9rem;
}

.form-group label {
  display: block;
  font-size: 0.85rem;
  font-weight: 500;
  margin-bottom: 0.3rem;
  color: #444;
}

.form-group input {
  width: 100%;
  padding: 0.5rem 0.7rem;
  border: 1px solid #ccd1d9;
  border-radius: 4px;
  font-size: 0.9rem;
}

.form-group input:focus {
  outline: none;
  border-color: #3498db;
}

.alert-success {
  background-color: #def7ec;
  color: #03543f;
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-bottom: 0.75rem;
}

.alert-error {
  background-color: #fde8e8;
  color: #9b1c1c;
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-bottom: 0.75rem;
}

.btn-pwd {
  background-color: #27ae60;
  color: white;
  border: none;
  padding: 0.6rem 1.2rem;
  font-size: 0.9rem;
  font-weight: 600;
  border-radius: 4px;
  cursor: pointer;
  width: 100%;
  transition: background-color 0.2s;
}

.btn-pwd:hover:not(:disabled) {
  background-color: #219150;
}

.btn-pwd:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.modules-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.25rem;
}

.card {
  background: #fff;
  padding: 1.5rem;
  border-radius: 6px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.05);
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
}

.card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0,0,0,0.1);
}

.card h3 {
  margin-bottom: 0.5rem;
  color: #2c3e50;
}

.card p {
  color: #666;
  font-size: 0.9rem;
  line-height: 1.4;
}

.patient-banner {
  background: #e8f4fd;
  border-left: 4px solid #3498db;
  padding: 1.25rem 1.5rem;
  border-radius: 4px;
}

.patient-banner h3 {
  color: #2980b9;
  font-size: 1.1rem;
  margin-bottom: 0.4rem;
}

.patient-banner p {
  color: #555;
  font-size: 0.95rem;
}
</style>
