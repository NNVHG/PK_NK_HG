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

      <div class="profile-card">
        <h3>Hồ sơ cá nhân</h3>
        <p>Cập nhật thông tin liên hệ và đổi mật khẩu của bạn.</p>
        <RouterLink class="btn-profile" to="/profile">Hồ sơ cá nhân</RouterLink>
      </div>
    </div>

    <div v-if="isStaff" class="modules-grid">
      <div class="card" @click="goTo('/patients')">
        <h3>Bệnh Nhân (MOD_PAT)</h3>
        <p>Hồ sơ hành chính bệnh nhân, tiền sử bệnh lý toàn thân và liên hệ.</p>
      </div>
      <div v-if="canCheckIn" class="card" @click="goTo('/queue/check-in')">
        <h3>Tiếp đón & Check-in (MOD_CHK)</h3>
        <p>Xác nhận khách đã đến, liên kết lịch hẹn trong ngày và cấp số thứ tự.</p>
      </div>
      <RouterLink v-if="canDiagnose" class="card" to="/clinical/diagnosis">
        <h3>Khám & chẩn đoán</h3>
        <p>Chọn lượt khám hôm nay, bắt đầu khám và lưu kết quả lâm sàng.</p>
      </RouterLink>
    </div>
    <div v-else class="patient-banner">
      <h3>Cổng Thông Tin Dành Cho Bệnh Nhân</h3>
      <p>Hồ sơ răng hàm mặt, đơn thuốc và lịch sử khám của bạn sẽ xuất hiện tại đây khi bác sĩ tiến hành điều trị.</p>
      <RouterLink class="btn-appointment" to="/appointments">Đặt lịch khám</RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();

const isStaff = computed(() => {
  const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
  return staffRoles.includes(authStore.role);
});
const canCheckIn = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));
const canDiagnose = computed(() => ['ADMIN', 'DENTIST'].includes(authStore.role));

function goTo(path: string) {
  router.push(path);
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

.user-card, .profile-card {
  background: #fff;
  padding: 1.25rem 1.5rem;
  border-radius: 6px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.05);
}

.user-card h3, .profile-card h3 {
  margin-bottom: 0.75rem;
  font-size: 1.1rem;
  color: #2c3e50;
  border-bottom: 1px solid #f0f0f0;
  padding-bottom: 0.5rem;
}

.user-card p {
  margin: 0.4rem 0;
}

.profile-card p {
  margin-bottom: 1rem;
  color: #666;
  font-size: 0.9rem;
}

.btn-profile {
  display: block;
  background-color: #27ae60;
  color: white;
  border: none;
  padding: 0.6rem 1.2rem;
  font-size: 0.9rem;
  font-weight: 600;
  border-radius: 4px;
  cursor: pointer;
  width: 100%;
  box-sizing: border-box;
  text-align: center;
  text-decoration: none;
  transition: background-color 0.2s;
}

.btn-profile:hover {
  background-color: #219150;
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

.btn-appointment {
  display: inline-block;
  margin-top: .5rem;
  padding: .65rem 1rem;
  border-radius: 4px;
  color: white;
  background: #13795b;
  font-weight: 600;
  text-decoration: none;
}
</style>
