<template>
  <div class="layout" :class="{ 'layout--portal': authStore.isAuthenticated && isPortalRoute }">
    <header v-if="authStore.isAuthenticated && isPortalRoute" class="navbar" :class="isPatient ? 'navbar-patient' : 'navbar-staff'" :data-menu-open="menuOpen">
      <div class="brand-row">
      <div class="brand">
        <router-link :to="isPatient ? '/patient/my-record' : '/management/dashboard'" class="brand-link">
          <img src="/logo-icon-nen-toi.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="28" height="28" />
          <h2>Nha Khoa Hoàng Gia</h2>
        </router-link>
        <span v-if="isPatient" class="area-tag patient-tag">Cổng Bệnh Nhân</span>
        <span v-else class="area-tag staff-tag">Khu Quản Lý</span>
      </div>
      <button ref="menuButton" class="menu-toggle" type="button" aria-controls="portal-navigation"
        :aria-expanded="menuOpen" :aria-label="menuOpen ? 'Đóng menu điều hướng' : 'Mở menu điều hướng'"
        @click="menuOpen = !menuOpen">
        <span aria-hidden="true">☰</span> Menu
      </button>
      </div>
      <div id="portal-navigation" class="portal-navigation">
      <nav class="nav-links" aria-label="Điều hướng chính">
        <span class="nav-caption">Điều hướng</span>
        <router-link :to="isPatient ? '/patient/dashboard' : '/management/dashboard'">Bảng điều khiển</router-link>
        <router-link v-if="['ADMIN','RECEPTIONIST'].includes(authStore.role)" to="/management/appointments/requests">Yêu cầu đặt lịch</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/book-appointment">Gửi yêu cầu đặt lịch</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/patient/appointments">Đặt lịch khám</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/management/appointments/counter">Đặt lịch tại quầy</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/patient/my-record">Hồ sơ của tôi</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/patient/appointments/mine">Lịch hẹn của tôi</router-link>
        <router-link v-if="authStore.role === 'PATIENT'" to="/patient/invoices/mine">Hóa đơn của tôi</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/management/appointments/desk">Lịch hẹn tại quầy</router-link>
        <router-link v-if="['ADMIN', 'RECEPTIONIST'].includes(authStore.role ?? '')" to="/management/invoices">Tra cứu hóa đơn</router-link>
        <template v-if="isStaff">
          <router-link to="/management/queue/today">Hàng đợi hôm nay</router-link>
          <router-link to="/management/patients">Bệnh nhân</router-link>
        </template>
        <router-link v-if="isAdmin" to="/management/admin/clinic-print-settings">Thông tin phòng khám trên bản in</router-link>
        <router-link v-if="isAdmin" to="/management/admin/scheduling-settings">Giờ làm việc & khung hẹn</router-link>
        <router-link v-if="isAdmin" to="/management/admin/staff">Tài khoản nhân viên</router-link>
        <router-link v-if="isAdmin" to="/management/admin/reports/revenue">Báo cáo doanh thu</router-link>
        <router-link v-if="isAdmin" to="/management/admin/role-permissions">Ma trận quyền</router-link>
        <router-link v-if="['ADMIN', 'DENTIST', 'ASSISTANT'].includes(authStore.role)" to="/management/staff/workspace">Vị trí nha sĩ/phụ tá</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT'].includes(authStore.role)" to="/management/staff/chair-status">Trạng thái ghế</router-link>
        <router-link v-if="isAdmin" to="/management/admin/medicines">Danh mục thuốc</router-link>
        <router-link v-if="isAdmin" to="/management/admin/medicine-stock">Nhập & tồn thuốc</router-link>
        <router-link v-if="isAdmin" to="/management/admin/medical-supplies">Danh mục vật tư</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT', 'DENTIST'].includes(authStore.role)" to="/management/inventory/receipts">Tồn kho & nhập kho</router-link>
        <router-link v-if="['ADMIN', 'ASSISTANT'].includes(authStore.role)" to="/management/inventory/issues">Tiêu hao theo ca</router-link>
        <router-link v-if="isAdmin" to="/management/admin/diagnoses">Danh mục chẩn đoán</router-link>
        <router-link v-if="isAdmin" to="/management/admin/chairs">Phòng & ghế nha khoa</router-link>
        <router-link v-if="isAdmin" to="/management/admin/services">Dịch vụ & bảng giá</router-link>
        <router-link v-if="isAdmin" to="/management/admin/audit-logs">Nhật ký thao tác</router-link>
      </nav>
      <div class="user-menu">
        <span>{{ authStore.user?.fullName }} (<strong>{{ authStore.user?.roleName }}</strong>)</span>
        <button class="btn-logout" @click="handleLogout">Đăng xuất</button>
      </div>
      </div>
    </header>

    <main ref="mainContent" class="main-content" :class="{ 'main-content--public': ['/', '/book-appointment'].includes(route.path) }" tabindex="-1">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter, useRoute } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const isStaff = computed(() => {
  const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
  return staffRoles.includes(authStore.role);
});
const isAdmin = computed(() => authStore.role === 'ADMIN');
const isPatient = computed(() => authStore.role === 'PATIENT');
const isPortalRoute = computed(() => route.path.startsWith('/patient/') || route.path.startsWith('/management/'));

const menuOpen = ref(false);
const menuButton = ref<HTMLButtonElement | null>(null);
const mainContent = ref<HTMLElement | null>(null);

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && menuOpen.value) {
    menuOpen.value = false;
    void nextTick(() => menuButton.value?.focus());
  }
}

function onResize() {
  if (window.innerWidth > 640) menuOpen.value = false;
}

watch(() => route.fullPath, () => {
  if (menuOpen.value) {
    menuOpen.value = false;
    void nextTick(() => mainContent.value?.focus());
  }
});
watch(() => authStore.isAuthenticated, (authenticated) => {
  if (!authenticated) menuOpen.value = false;
});
onMounted(() => {
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', onResize);
});
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown);
  window.removeEventListener('resize', onResize);
});

async function handleLogout() {
  await authStore.logout();
  router.push('/login');
}
</script>

<style scoped>
.layout { min-height: 100vh; display: flex; flex-direction: column; }
.layout--portal { flex-direction: row; background: #f4f7fb; }
.navbar {
  box-sizing: border-box; display: flex; flex-direction: column; flex-shrink: 0;
  width: 260px; height: 100vh; position: sticky; top: 0; color: #fff;
  background: #102e56; padding: 1.25rem .9rem; min-width: 0;
}
.navbar-patient { border-bottom: 3px solid #55b8aa; }
.navbar-staff { border-bottom: 3px solid #e5b95e; }
.brand-row { display: flex; justify-content: space-between; align-items: center; gap: .75rem; }
.brand { display: flex; align-items: flex-start; flex-direction: column; gap: .65rem; min-width: 0; }
.brand-link { display: flex; align-items: center; gap: .65rem; color: #fff; text-decoration: none; }
.brand-logo { display: block; flex-shrink: 0; width: 34px; height: 34px; }
.brand h2 { font-size: 1rem; line-height: 1.3; margin: 0; }
.area-tag { display: inline-block; font-size: .65rem; font-weight: 700; padding: .2rem .5rem; border-radius: 4px; text-transform: uppercase; letter-spacing: .04em; }
.patient-tag { background-color: #0f766e; color: #fff; }
.staff-tag { background-color: #755413; color: #fff; }
.menu-toggle { display: none; border: 1px solid #aec3db; background: transparent; color: #fff; border-radius: .5rem; padding: .55rem .7rem; cursor: pointer; font: inherit; }
.portal-navigation { min-height: 0; display: flex; flex: 1; flex-direction: column; margin-top: 1.5rem; }
.nav-links { min-height: 0; overflow-y: auto; display: flex; flex: 1; flex-direction: column; gap: .2rem; }
.nav-caption { padding: .4rem .65rem; color: #b8cbe1; font-size: .7rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.nav-links a { color: #e0eafa; padding: .6rem .65rem; border-radius: .5rem; text-decoration: none; font-size: .875rem; line-height: 1.35; overflow-wrap: anywhere; }
.nav-links a.router-link-exact-active, .nav-links a:hover { color: #fff; background: #1857b6; }
.nav-links a:focus-visible, .menu-toggle:focus-visible, .btn-logout:focus-visible, .brand-link:focus-visible { outline: 3px solid #e5b95e; outline-offset: 2px; }
.user-menu { display: flex; flex-wrap: wrap; align-items: center; gap: .75rem; border-top: 1px solid #48617e; padding: 1rem .4rem 0; margin-top: .75rem; font-size: .8rem; }
.user-menu span { overflow-wrap: anywhere; }
.btn-logout { flex-shrink: 0; border: 1px solid #aac1dc; border-radius: .45rem; color: #fff; background: transparent; padding: .45rem .7rem; cursor: pointer; font: inherit; }
.btn-logout:hover { background: #1857b6; }
.main-content { flex: 1; min-width: 0; box-sizing: border-box; padding: 1.5rem; max-width: 1400px; margin: 0 auto; width: 100%; }
.main-content--public { padding: 0; max-width: none; }
@media (max-width: 640px) {
  .layout--portal { flex-direction: column; }
  .navbar { position: relative; width: 100%; height: auto; padding: .75rem 1rem; }
  .brand { gap: .3rem; }
  .brand-logo { width: 28px; height: 28px; }
  .menu-toggle { display: inline-flex; align-items: center; gap: .4rem; }
  .portal-navigation { display: none; max-height: calc(100vh - 100px); overflow-y: auto; margin-top: 1rem; }
  .navbar[data-menu-open="true"] .portal-navigation { display: flex; }
  .nav-links { overflow-y: visible; flex: none; }
  .main-content { padding: 1rem; }
  .main-content--public { padding: 0; }
}
@media print {
  .layout--portal { display: block; background: #fff; }
  .navbar { display: none; }
  .main-content { max-width: none; padding: 0; }
}
</style>
