<template>
  <div
    class="layout"
    :class="{
      'layout--portal': authStore.isAuthenticated && isPortalRoute && !isPatient,
      'layout--patient': authStore.isAuthenticated && isPatient
    }"
  >
    <!-- ==================== 0. THANH ĐIỀU HƯỚNG TRÊN CÙNG: DÀNH RIÊNG CHO BỆNH NHÂN ==================== -->
    <header v-if="authStore.isAuthenticated && isPatient" class="patient-topbar" :data-menu-open="menuOpen">
      <div class="patient-topbar-inner">
        <!-- Brand / Logo -->
        <div class="patient-brand-block">
          <router-link to="/patient/dashboard" class="patient-brand-link" aria-label="Nha Khoa Hoàng Gia - Cổng bệnh nhân">
            <img src="/logo-icon-nen-toi.svg" alt="" class="patient-brand-logo" width="34" height="34" />
            <div class="patient-brand-text">
              <span class="patient-brand-title">Nha Khoa Hoàng Gia</span>
              <span class="patient-badge-portal">Cổng Bệnh Nhân</span>
            </div>
          </router-link>
        </div>

        <!-- Mobile hamburger toggle -->
        <button
          ref="menuButton"
          class="patient-menu-toggle"
          type="button"
          aria-controls="patient-nav"
          :aria-expanded="menuOpen"
          :aria-label="menuOpen ? 'Đóng menu' : 'Mở menu'"
          @click="menuOpen = !menuOpen"
        >
          <span aria-hidden="true">{{ menuOpen ? '✕' : '☰' }}</span>
        </button>

        <!-- Navigation Links (ở giữa) -->
        <nav id="patient-nav" class="patient-nav-center" aria-label="Điều hướng Cổng bệnh nhân">
          <router-link to="/patient/dashboard" class="patient-nav-item patient-nav-item--home">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
            <span>Tổng quan</span>
          </router-link>

          <router-link to="/patient/my-record" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1" ry="1"/></svg>
            <span>Hồ sơ của tôi</span>
          </router-link>

          <router-link to="/patient/appointments" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
            <span>Đặt lịch khám</span>
          </router-link>

          <router-link to="/patient/appointments/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
            <span>Lịch hẹn của tôi</span>
          </router-link>

          <router-link to="/patient/invoices/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            <span>Hóa đơn & Chi phí</span>
          </router-link>

        </nav>

        <!-- User actions & Logout (bên phải) -->
        <div class="patient-nav-user">
          <router-link to="/patient/profile" class="patient-user-profile" title="Xem thông tin tài khoản & Đổi mật khẩu">
            <div class="patient-avatar-circle" aria-hidden="true">
              {{ (authStore.user?.fullName || 'BN').charAt(0).toUpperCase() }}
            </div>
            <div class="patient-user-text">
              <span class="patient-user-name">{{ authStore.user?.fullName || 'Bệnh nhân' }}</span>
              <span class="patient-user-role">Tài khoản cá nhân</span>
            </div>
          </router-link>

          <button class="patient-btn-logout" type="button" title="Đăng xuất" @click="handleLogout">
            <svg class="p-btn-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
            <span>Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>

    <!-- ==================== 1. THANH BÊN TRÁI: DÀNH CHO NHÂN VIÊN & ADMIN ==================== -->
    <header
      v-if="authStore.isAuthenticated && isPortalRoute && !isPatient"
      class="navbar navbar-left navbar-staff"
      :data-menu-open="menuOpen"
    >
      <div class="brand-row">
        <div class="brand">
          <router-link to="/management/dashboard" class="brand-link">
            <img src="/logo-icon-nen-toi.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="28" height="28" />
            <h2>Nha Khoa Hoàng Gia</h2>
          </router-link>
          <span v-if="isAdmin" class="area-tag admin-tag">👑 Khu Admin</span>
          <span v-else class="area-tag staff-tag">Khu Quản Lý</span>
        </div>
        <button
          ref="menuButton"
          class="menu-toggle"
          type="button"
          aria-controls="portal-navigation"
          :aria-expanded="menuOpen"
          :aria-label="menuOpen ? 'Đóng menu điều hướng' : 'Mở menu điều hướng'"
          @click="menuOpen = !menuOpen"
        >
          <span aria-hidden="true">☰</span> Menu
        </button>
      </div>

      <div id="portal-navigation" class="portal-navigation">
        <nav class="nav-links" aria-label="Điều hướng chính">
          <!-- THANH BÊN TRÁI: DÀNH RIÊNG CHO CHỨC NĂNG ADMIN -->
          <template v-if="isAdmin">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard" class="link-dashboard-nav">
              <span class="nav-icon">📊</span> Bảng điều khiển
            </router-link>

            <span class="nav-caption caption-admin">📋 Quản Trị Tổ Chức</span>
            <router-link to="/management/admin/staff">
              <span class="nav-icon">👥</span> Tài khoản nhân viên
            </router-link>
            <router-link to="/management/admin/role-permissions">
              <span class="nav-icon">🛡️</span> Ma trận quyền RBAC
            </router-link>
            <router-link to="/management/admin/reports/revenue">
              <span class="nav-icon">📈</span> Báo cáo doanh thu
            </router-link>
            <router-link to="/management/admin/audit-logs">
              <span class="nav-icon">📜</span> Nhật ký thao tác (Audit)
            </router-link>

            <span class="nav-caption caption-admin">🦷 Danh Mục Chuyên Môn</span>
            <router-link to="/management/admin/services">
              <span class="nav-icon">🏷️</span> Dịch vụ & bảng giá
            </router-link>
            <router-link to="/management/admin/diagnoses">
              <span class="nav-icon">🩺</span> Danh mục chẩn đoán
            </router-link>
            <router-link to="/management/admin/medicines">
              <span class="nav-icon">💊</span> Danh mục thuốc
            </router-link>
            <router-link to="/management/admin/medicine-stock">
              <span class="nav-icon">📦</span> Nhập & tồn thuốc
            </router-link>
            <router-link to="/management/admin/medical-supplies">
              <span class="nav-icon">🧤</span> Danh mục vật tư
            </router-link>

            <span class="nav-caption caption-admin">⚙️ Thiết Lập Phòng Khám</span>
            <router-link to="/management/admin/chairs">
              <span class="nav-icon">🪑</span> Phòng & ghế nha khoa
            </router-link>
            <router-link to="/management/admin/scheduling-settings">
              <span class="nav-icon">⏰</span> Giờ làm việc & hẹn
            </router-link>
            <router-link to="/management/admin/clinic-print-settings">
              <span class="nav-icon">🖨️</span> Thông tin trên bản in
            </router-link>
          </template>

          <!-- Lễ tân / Thu ngân -->
          <template v-else-if="authStore.role === 'RECEPTIONIST'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard">Bảng điều khiển</router-link>

            <span class="nav-caption">📋 Lễ tân / Thu ngân</span>
            <router-link to="/management/appointments/requests">Yêu cầu đặt lịch</router-link>
            <router-link to="/management/appointments/counter">Đặt lịch tại quầy</router-link>
            <router-link to="/management/appointments/desk">Lịch hẹn tại quầy</router-link>
            <router-link to="/management/invoices">Tra cứu hóa đơn</router-link>
            <router-link to="/management/queue/check-in">Tiếp đón & Check-in</router-link>

            <span class="nav-caption">👥 Dùng chung (2+ vai trò)</span>
            <router-link to="/management/queue/today">Hàng đợi hôm nay</router-link>
            <router-link to="/management/patients">Bệnh nhân</router-link>
          </template>

          <!-- Nha sĩ -->
          <template v-else-if="authStore.role === 'DENTIST'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard">Bảng điều khiển</router-link>

            <span class="nav-caption">🦷 Nghiệp vụ Nha sĩ</span>
            <router-link to="/management/clinical/diagnosis">Khám & chẩn đoán</router-link>
            <router-link to="/management/inventory/receipts">Xem tồn vật tư</router-link>

            <span class="nav-caption">👥 Dùng chung (2+ vai trò)</span>
            <router-link to="/management/queue/today">Hàng đợi hôm nay</router-link>
            <router-link to="/management/patients">Bệnh nhân</router-link>
            <router-link to="/management/staff/workspace">Vị trí nha sĩ/phụ tá</router-link>
          </template>

          <!-- Phụ tá -->
          <template v-else-if="authStore.role === 'ASSISTANT'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard">Bảng điều khiển</router-link>

            <span class="nav-caption">🧤 Nghiệp vụ Phụ tá</span>
            <router-link to="/management/staff/chair-status">Trạng thái ghế</router-link>
            <router-link to="/management/inventory/receipts">Tồn kho & nhập kho</router-link>
            <router-link to="/management/inventory/issues">Tiêu hao theo ca</router-link>

            <span class="nav-caption">👥 Dùng chung (2+ vai trò)</span>
            <router-link to="/management/queue/today">Hàng đợi hôm nay</router-link>
            <router-link to="/management/patients">Bệnh nhân</router-link>
            <router-link to="/management/staff/workspace">Vị trí nha sĩ/phụ tá</router-link>
          </template>
        </nav>

        <div class="user-menu">
          <div class="user-info">
            <span class="user-name">{{ authStore.user?.fullName }}</span>
            <span class="user-role-badge">{{ authStore.user?.roleName || authStore.role }}</span>
          </div>
          <button class="btn-logout" @click="handleLogout">Đăng xuất</button>
        </div>
      </div>
    </header>

    <!-- ==================== 2. KHU VỰC NỘI DUNG CHÍNH (Ở GIỮA) ==================== -->
    <main
      ref="mainContent"
      class="main-content"
      :class="{
        'main-content--public': ['/', '/book-appointment', '/login'].includes(route.path),
        'main-content--patient': isPatient,
        'main-content--with-dual-sidebar': isAdmin && isPortalRoute
      }"
      tabindex="-1"
    >
      <div :key="route.path" class="page-transition-stage">
        <router-view />
      </div>
    </main>

    <!-- ==================== 3. THANH BÊN PHẢI: DÀNH RIÊNG CHO CÁC ACTOR KHÁC ==================== -->
    <aside
      v-if="isAdmin && isPortalRoute"
      class="navbar-right-actors"
      :class="{ 'navbar-right-actors--collapsed': rightNavCollapsed }"
      aria-label="Điều hướng các vai trò khác"
    >
      <div class="right-nav-header">
        <div class="right-nav-title">
          <span class="right-nav-icon">👥</span>
          <div v-show="!rightNavCollapsed" class="right-nav-title-text">
            <h3>Actor Khác</h3>
            <p>Lễ tân · Nha sĩ · Phụ tá · Chung</p>
          </div>
        </div>
        <button
          class="btn-toggle-right"
          :title="rightNavCollapsed ? 'Mở rộng thanh chức năng actor khác' : 'Thu gọn thanh bên'"
          @click="rightNavCollapsed = !rightNavCollapsed"
        >
          {{ rightNavCollapsed ? '◀' : '▶' }}
        </button>
      </div>

      <div v-show="!rightNavCollapsed" class="right-nav-body">
        <nav class="nav-links nav-links--actors" aria-label="Nghiệp vụ các vai trò khác">
          <span class="nav-caption caption-reception">📋 Tiếp Đón & Thu Ngân</span>
          <router-link to="/management/queue/check-in">Tiếp đón & Check-in</router-link>
          <router-link to="/management/appointments/requests">Yêu cầu đặt lịch</router-link>
          <router-link to="/management/appointments/counter">Đặt lịch tại quầy</router-link>
          <router-link to="/management/appointments/desk">Lịch hẹn tại quầy</router-link>
          <router-link to="/management/invoices">Tra cứu & Thu hóa đơn</router-link>

          <span class="nav-caption caption-dentist">🦷 Khám & Điều Trị</span>
          <router-link to="/management/clinical/diagnosis">Khám lâm sàng & FDI</router-link>

          <span class="nav-caption caption-assistant">🧤 Phụ Tá Điều Trị</span>
          <router-link to="/management/staff/chair-status">Trạng thái 6 ghế</router-link>
          <router-link to="/management/inventory/receipts">Tồn kho & nhập kho</router-link>
          <router-link to="/management/inventory/issues">Tiêu hao theo ca</router-link>

          <span class="nav-caption caption-shared">🔄 Phối Hợp Lâm Sàng</span>
          <router-link to="/management/queue/today">Hàng đợi hôm nay</router-link>
          <router-link to="/management/patients">Bệnh nhân & CCCD</router-link>
          <router-link to="/management/staff/workspace">Vị trí nha sĩ/phụ tá</router-link>
        </nav>
      </div>
    </aside>
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
const rightNavCollapsed = ref(false);
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
  if (window.innerWidth < 1200) rightNavCollapsed.value = true;
  else rightNavCollapsed.value = false;
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
  if (window.innerWidth < 1200) rightNavCollapsed.value = true;
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
.layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.layout--portal {
  flex-direction: row;
  background: #f4f7fb;
  min-height: 100vh;
}

.layout--patient {
  flex-direction: column;
  background: #f8fbff;
  min-height: 100vh;
}

/* ================= 0. THANH ĐIỀU HƯỚNG TRÊN CÙNG (PATIENT TOPBAR) ================= */
.patient-topbar {
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid #e4eef6;
  position: sticky;
  top: 0;
  z-index: 40;
  box-shadow: 0 2px 12px rgba(15, 45, 89, 0.04);
}

.patient-topbar-inner {
  max-width: 1400px;
  min-height: 68px;
  margin: 0 auto;
  padding: 0 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1.25rem;
  box-sizing: border-box;
}

.patient-brand-block {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.patient-brand-link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  text-decoration: none;
  color: #0f2d59;
}

.patient-brand-logo {
  display: block;
  flex-shrink: 0;
  width: 34px;
  height: 34px;
}

.patient-brand-text {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.patient-brand-title {
  font-size: 1.05rem;
  font-weight: 850;
  color: #0f2d59;
  letter-spacing: -0.01em;
  line-height: 1.2;
}

.patient-badge-portal {
  display: inline-block;
  align-self: flex-start;
  font-size: 0.65rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  background: #e0f2fe;
  color: #0369a1;
  padding: 0.1rem 0.45rem;
  border-radius: 4px;
  border: 1px solid #bae6fd;
}

.patient-menu-toggle {
  display: none;
  background: #f1f5f9;
  border: 1px solid #cbd5e1;
  color: #0f2d59;
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  font-size: 1.1rem;
  cursor: pointer;
}

.patient-nav-center {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  flex: 1;
  min-width: 0;
}

.patient-nav-item {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.5rem 0.85rem;
  border-radius: 999px;
  color: #334155;
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 750;
  white-space: nowrap;
  transition: all 0.15s ease;
}

.patient-nav-item:hover {
  background: #eff8ff;
  color: #0284c7;
}

.patient-nav-item.router-link-exact-active,
.patient-nav-item.router-link-active:not(.patient-nav-item--home) {
  background: #0f2d59;
  color: #ffffff;
}

.patient-nav-item.router-link-exact-active .p-nav-ic,
.patient-nav-item.router-link-active:not(.patient-nav-item--home) .p-nav-ic {
  stroke: #ffffff;
}

.patient-nav-item--home {
  color: #64748b;
}

.patient-nav-item--home:hover {
  color: #0f2d59;
}

.p-nav-ic {
  width: 17px;
  height: 17px;
  fill: none;
  stroke: currentColor;
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
  flex-shrink: 0;
}

.patient-nav-user {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  flex-shrink: 0;
}

.patient-user-profile {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  text-decoration: none;
  padding: 0.35rem 0.65rem;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  background: #ffffff;
  transition: all 0.15s ease;
}

.patient-user-profile:hover {
  border-color: #cbd5e1;
  background: #f8fafc;
}

.patient-avatar-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #0284c7;
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 0.85rem;
  flex-shrink: 0;
}

.patient-user-text {
  display: flex;
  flex-direction: column;
  line-height: 1.2;
}

.patient-user-name {
  font-size: 0.825rem;
  font-weight: 800;
  color: #0f2d59;
  max-width: 130px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.patient-user-role {
  font-size: 0.7rem;
  color: #64748b;
}

.patient-btn-logout {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  background: #fee2e2;
  color: #991b1b;
  border: 1px solid #fecaca;
  padding: 0.45rem 0.85rem;
  border-radius: 8px;
  font: inherit;
  font-size: 0.825rem;
  font-weight: 750;
  cursor: pointer;
  transition: all 0.15s ease;
}

.patient-btn-logout:hover {
  background: #fecaca;
  color: #7f1d1d;
}

.p-btn-ic {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: currentColor;
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* ================= 1. THANH BÊN TRÁI (ADMIN & ROLE CHÍNH) ================= */
.navbar-left {
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  width: 250px;
  height: 100vh;
  position: sticky;
  top: 0;
  color: #fff;
  background: #102e56;
  padding: 1.15rem 0.85rem;
  min-width: 0;
  z-index: 30;
  border-right: 1px solid rgba(255, 255, 255, 0.08);
}

.navbar-patient { border-bottom: 3px solid #55b8aa; }
.navbar-staff { border-bottom: 3px solid #e5b95e; }

.brand-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.brand {
  display: flex;
  align-items: flex-start;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}

.brand-link {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  color: #fff;
  text-decoration: none;
}

.brand-logo {
  display: block;
  flex-shrink: 0;
  width: 32px;
  height: 32px;
}

.brand h2 {
  font-size: 0.95rem;
  line-height: 1.25;
  margin: 0;
  font-weight: 800;
  letter-spacing: -0.01em;
}

.area-tag {
  display: inline-block;
  font-size: 0.65rem;
  font-weight: 750;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.patient-tag { background-color: #0f766e; color: #fff; }
.staff-tag { background-color: #755413; color: #fff; }
.admin-tag { background: #b45309; color: #fef3c7; border: 1px solid #d97706; }

.menu-toggle {
  display: none;
  border: 1px solid #aec3db;
  background: transparent;
  color: #fff;
  border-radius: 0.5rem;
  padding: 0.55rem 0.7rem;
  cursor: pointer;
  font: inherit;
}

.portal-navigation {
  min-height: 0;
  display: flex;
  flex: 1;
  flex-direction: column;
  margin-top: 0.85rem;
}

.nav-links {
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 0.15rem;
  padding-right: 0.2rem;
  scrollbar-width: thin;
  scrollbar-color: #274570 transparent;
}

.nav-links::-webkit-scrollbar {
  width: 4px;
}

.nav-links::-webkit-scrollbar-thumb {
  background: #274570;
  border-radius: 4px;
}

.nav-caption {
  padding: 0.45rem 0.6rem 0.25rem;
  color: #9cb4d2;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  margin-top: 0.45rem;
}

.caption-admin {
  color: #fde68a;
  background: rgba(245, 158, 11, 0.12);
  border-radius: 4px;
  padding: 0.35rem 0.55rem;
  margin: 0.45rem 0 0.25rem 0;
}

.nav-links a {
  color: #d1e2f7;
  padding: 0.45rem 0.65rem;
  border-radius: 6px;
  text-decoration: none;
  font-size: 0.835rem;
  line-height: 1.35;
  overflow-wrap: anywhere;
  transition: all 0.15s ease;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.nav-links a.router-link-exact-active,
.nav-links a:hover {
  color: #ffffff;
  background: #1857b6;
}

.link-dashboard-nav {
  font-weight: 750;
  background: rgba(255, 255, 255, 0.08);
  margin-bottom: 0.35rem;
}

.user-menu {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  border-top: 1px solid rgba(255, 255, 255, 0.12);
  padding: 0.85rem 0.4rem 0;
  margin-top: 0.5rem;
  font-size: 0.8rem;
}

.user-info {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.user-name {
  font-weight: 750;
  color: #ffffff;
}

.user-role-badge {
  font-size: 0.7rem;
  color: #e5b95e;
  font-weight: 700;
}

.btn-logout {
  width: 100%;
  border: 1px solid #758ca9;
  border-radius: 6px;
  color: #ffffff;
  background: rgba(255, 255, 255, 0.05);
  padding: 0.45rem;
  cursor: pointer;
  font: inherit;
  font-size: 0.8rem;
  font-weight: 700;
  transition: all 0.15s ease;
}

.btn-logout:hover {
  background: #dc2626;
  border-color: #dc2626;
}

/* ================= 2. KHU VỰC NỘI DUNG CHÍNH (Ở GIỮA) ================= */
.main-content {
  flex: 1;
  min-width: 0;
  box-sizing: border-box;
  padding: 1.5rem 1.75rem;
  max-width: 1600px;
  margin: 0 auto;
  width: 100%;
}

.main-content--public {
  padding: 0;
  max-width: none;
}

.main-content--patient {
  max-width: 1300px;
  padding: 1.75rem 1.5rem;
}

.main-content--with-dual-sidebar {
  max-width: none;
}

.page-transition-stage {
  animation: vuePageEnter 0.36s cubic-bezier(0.22, 1, 0.36, 1) both;
}

@keyframes vuePageEnter {
  0% { opacity: 0; transform: translate3d(0, 10px, 0); }
  100% { opacity: 1; transform: translate3d(0, 0, 0); }
}

/* ================= 3. THANH BÊN PHẢI (CHỨC NĂNG CÁC ACTOR KHÁC) ================= */
.navbar-right-actors {
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  width: 255px;
  height: 100vh;
  position: sticky;
  top: 0;
  background: #0d274c;
  color: #ffffff;
  padding: 1.15rem 0.85rem;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  z-index: 25;
  transition: width 0.25s cubic-bezier(0.22, 1, 0.36, 1);
}

.navbar-right-actors--collapsed {
  width: 54px;
  padding: 1.15rem 0.35rem;
}

.right-nav-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  margin-bottom: 0.65rem;
}

.right-nav-title {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  overflow: hidden;
}

.right-nav-icon {
  font-size: 1.25rem;
  flex-shrink: 0;
}

.right-nav-title-text h3 {
  font-size: 0.9rem;
  font-weight: 800;
  color: #ffffff;
  margin: 0;
  line-height: 1.2;
}

.right-nav-title-text p {
  font-size: 0.68rem;
  color: #8fb1d6;
  margin: 0.1rem 0 0 0;
}

.btn-toggle-right {
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: #ffffff;
  border-radius: 4px;
  padding: 0.25rem 0.45rem;
  font-size: 0.75rem;
  cursor: pointer;
  transition: all 0.15s ease;
  flex-shrink: 0;
}

.btn-toggle-right:hover {
  background: #1857b6;
}

.right-nav-body {
  min-height: 0;
  display: flex;
  flex: 1;
  flex-direction: column;
}

.nav-links--actors {
  padding-right: 0.2rem;
}

.caption-reception { color: #bae6fd; }
.caption-dentist { color: #ddd6fe; }
.caption-assistant { color: #bbf7d0; }
.caption-shared { color: #e2e8f0; }

.navbar-right-actors .nav-links a {
  font-size: 0.81rem;
  padding: 0.42rem 0.6rem;
  color: #c9ddf5;
  border-radius: 6px;
  text-decoration: none;
  line-height: 1.3;
  transition: all 0.15s ease;
}

.navbar-right-actors .nav-links a:hover,
.navbar-right-actors .nav-links a.router-link-exact-active {
  background: #0284c7;
  color: #ffffff;
}

/* Responsive adjustments */
@media (max-width: 1024px) {
  .patient-topbar-inner {
    padding: 0 1rem;
  }
  .patient-nav-item span {
    display: none;
  }
  .patient-nav-item {
    padding: 0.5rem;
  }
  .patient-user-text {
    display: none;
  }

  .layout--portal {
    flex-direction: column;
  }
  .navbar-left {
    position: relative;
    width: 100%;
    height: auto;
    padding: 0.75rem 1rem;
  }
  .menu-toggle {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
  }
  .portal-navigation {
    display: none;
    max-height: calc(100vh - 100px);
    overflow-y: auto;
    margin-top: 1rem;
  }
  .navbar-left[data-menu-open="true"] .portal-navigation {
    display: flex;
  }
  .navbar-right-actors {
    position: relative;
    width: 100%;
    height: auto;
    border-left: none;
    border-top: 2px solid rgba(255, 255, 255, 0.1);
  }
  .navbar-right-actors--collapsed {
    display: none;
  }
  .main-content {
    padding: 1rem;
  }
}

@media (max-width: 640px) {
  .patient-menu-toggle {
    display: inline-flex;
    align-items: center;
    justify-content: center;
  }
  .patient-nav-center {
    display: none;
  }
  .patient-topbar[data-menu-open="true"] .patient-nav-center {
    display: flex;
    flex-direction: column;
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    background: #ffffff;
    border-bottom: 1px solid #cbd5e1;
    padding: 1rem;
    gap: 0.5rem;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
  }
  .patient-topbar[data-menu-open="true"] .patient-nav-item span {
    display: inline;
  }
  .patient-topbar[data-menu-open="true"] .patient-nav-item {
    width: 100%;
    justify-content: flex-start;
    padding: 0.75rem 1rem;
    border-radius: 8px;
  }
  .patient-btn-logout span {
    display: none;
  }
}

@media print {
  .layout--portal, .layout--patient { display: block; background: #fff; }
  .patient-topbar, .navbar-left, .navbar-right-actors { display: none; }
  .main-content { max-width: none; padding: 0; }
}
</style>
