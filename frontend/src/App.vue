<template>
  <div class="layout">
    <header v-if="authStore.isAuthenticated" class="navbar">
      <div class="brand">
        <h2>Phòng Khám Nha Khoa</h2>
      </div>
      <nav class="nav-links" aria-label="Điều hướng chính">
        <router-link to="/dashboard">Bảng điều khiển</router-link>
        <router-link v-if="['ADMIN','RECEPTIONIST'].includes(authStore.role)" to="/appointments/requests">Yêu cầu đặt lịch</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/book-appointment">Gửi yêu cầu đặt lịch</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/appointments">Đặt lịch khám</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/appointments/counter">Đặt lịch tại quầy</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/my-record">Hồ sơ của tôi</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/appointments/mine">Lịch hẹn của tôi</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/invoices/mine">Hóa đơn của tôi</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/appointments/desk">Lịch hẹn tại quầy</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/invoices">Tra cứu hóa đơn</router-link>
        <template v-if="isStaff">
          <router-link to="/queue/today">Hàng đợi hôm nay</router-link>
          <router-link to="/patients">Bệnh nhân</router-link>
        </template>
        <router-link v-if="isAdmin" to="/admin/clinic-print-settings">Thông tin phòng khám trên bản in</router-link>
        <router-link v-if="isAdmin" to="/admin/scheduling-settings">Giờ làm việc & khung hẹn</router-link>
        <router-link v-if="isAdmin" to="/admin/staff">Tài khoản nhân viên</router-link>
        <router-link v-if="isAdmin" to="/admin/reports/revenue">Báo cáo doanh thu</router-link>
        <router-link v-if="isAdmin" to="/admin/role-permissions">Ma trận quyền</router-link>
        <router-link v-if="['ADMIN', 'DENTIST', 'ASSISTANT'].includes(authStore.role)" to="/staff/workspace">Vị trí nha sĩ/phụ tá</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT'].includes(authStore.role)" to="/staff/chair-status">Trạng thái ghế</router-link>
        <router-link v-if="isAdmin" to="/admin/medicines">Danh mục thuốc</router-link>
        <router-link v-if="isAdmin" to="/admin/medicine-stock">Nhập & tồn thuốc</router-link>
        <router-link v-if="isAdmin" to="/admin/medical-supplies">Danh mục vật tư</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT', 'DENTIST'].includes(authStore.role)" to="/inventory/receipts">Tồn kho & nhập kho</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT'].includes(authStore.role)" to="/inventory/issues">Tiêu hao theo ca</router-link>
        <router-link v-if="isAdmin" to="/admin/diagnoses">Danh mục chẩn đoán</router-link>
        <router-link v-if="isAdmin" to="/admin/chairs">Phòng & ghế nha khoa</router-link>
        <router-link v-if="isAdmin" to="/admin/services">Dịch vụ & bảng giá</router-link>
        <router-link v-if="isAdmin" to="/admin/audit-logs">Nhật ký thao tác</router-link>
      </nav>
      <div class="user-menu">
        <span>{{ authStore.user?.fullName }} (<strong>{{ authStore.user?.roleName }}</strong>)</span>
        <button class="btn-logout" @click="handleLogout">Đăng xuất</button>
      </div>
    </header>

    <main class="main-content">
      <router-view />
    </main>
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
const isAdmin = computed(() => authStore.role === 'ADMIN');

async function handleLogout() {
  await authStore.logout();
  router.push('/login');
}
</script>

<style scoped>
.layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.navbar {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: center;
  justify-content: space-between;
  background-color: #2c3e50;
  color: #fff;
  padding: 0.75rem 1.5rem;
}

.brand { flex: 0 0 auto; }

.brand h2 {
  font-size: 1.15rem;
  font-weight: 600;
}

.nav-links {
  display: flex;
  flex: 1 1 36rem;
  min-width: 0;
  flex-wrap: wrap;
  gap: .5rem 1rem;
}

.nav-links a {
  white-space: nowrap;
  padding: .25rem 0;
  color: #bdc3c7;
  text-decoration: none;
  font-weight: 500;
  transition: color 0.2s;
}

.nav-links a.router-link-active,
.nav-links a:hover {
  color: #fff;
}

.user-menu {
  display: flex;
  flex-wrap: wrap;
  min-width: 0;
  align-items: center;
  gap: 1rem;
}

.user-menu span { overflow-wrap: anywhere; }

.btn-logout {
  flex-shrink: 0;
  background-color: #e74c3c;
  color: white;
  border: none;
  padding: 0.4rem 0.8rem;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
}

.btn-logout:hover {
  background-color: #c0392b;
}

.main-content {
  flex: 1;
  min-width: 0;
  box-sizing: border-box;
  padding: 1.5rem;
  max-width: 1200px;
  margin: 0 auto;
  width: 100%;
}

@media (max-width: 640px) {
  .navbar { align-items: flex-start; padding: .75rem 1rem; }
  .nav-links { flex-basis: 100%; }
  .main-content { padding: 1rem; }
}
</style>
