<template>
  <header class="patient-topbar" :data-menu-open="menuOpen">
    <div class="patient-topbar-inner">
      <div class="patient-brand-block">
        <RouterLink :to="isPatient ? '/patient/dashboard' : '/'" class="patient-brand-link" :aria-label="isPatient ? 'Nha khoa Hoàng Gia - Bảng điều khiển' : 'Nha khoa Hoàng Gia - Trang chủ'">
          <img src="/logo-icon.svg" alt="" class="patient-brand-logo" width="34" height="34" />
          <div class="patient-brand-text">
            <span class="patient-brand-title">Nha Khoa Hoàng Gia</span>
            <span v-if="isPatient" class="patient-badge-portal">Cổng Bệnh Nhân</span>
            <span v-else class="patient-badge-portal guest-tag">Chăm sóc nụ cười</span>
          </div>
        </RouterLink>
      </div>

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

      <nav id="patient-nav" class="patient-nav-center" :aria-label="isPatient ? 'Điều hướng Cổng bệnh nhân' : 'Điều hướng chính'">
        <template v-if="isPatient">
          <RouterLink to="/patient/dashboard" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
            <span>Tổng quan</span>
          </RouterLink>
          <RouterLink to="/patient/appointments/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
            <span>Lịch hẹn & Đặt lịch</span>
          </RouterLink>
          <RouterLink to="/patient/my-record" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1" ry="1"/></svg>
            <span>Hồ sơ của tôi</span>
          </RouterLink>
          <RouterLink to="/patient/invoices/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            <span>Hóa đơn của tôi</span>
          </RouterLink>
          <RouterLink to="/patient/profile" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
            <span>Hồ sơ cá nhân</span>
          </RouterLink>
        </template>
        <template v-else>
          <a class="patient-nav-item" href="#chuyen-khoa" @click="handleAnchorClick('chuyen-khoa', $event)">
            <span>Dịch vụ</span>
          </a>
          <a class="patient-nav-item" href="#so-do-fdi" @click="handleAnchorClick('so-do-fdi', $event)">
            <span>Sơ đồ FDI</span>
          </a>
          <a class="patient-nav-item" href="#minh-bach-chi-phi" @click="handleAnchorClick('minh-bach-chi-phi', $event)">
            <span>Bảng giá</span>
          </a>
          <a class="patient-nav-item" href="#cach-dat-lich" @click="handleAnchorClick('cach-dat-lich', $event)">
            <span>Quy trình</span>
          </a>
          <a class="patient-nav-item" href="#cau-hoi-thuong-gap" @click="handleAnchorClick('cau-hoi-thuong-gap', $event)">
            <span>Hỏi đáp</span>
          </a>
        </template>
      </nav>

      <div class="patient-nav-user">
        <template v-if="isPatient">
          <RouterLink to="/patient/profile" class="patient-user-profile" title="Xem thông tin tài khoản">
            <div class="patient-avatar-circle" aria-hidden="true">
              {{ (authStore.user?.fullName || 'BN').charAt(0).toUpperCase() }}
            </div>
            <div class="patient-user-text">
              <span class="patient-user-name">{{ authStore.user?.fullName || 'Bệnh nhân' }}</span>
              <span class="patient-user-role">Tài khoản cá nhân</span>
            </div>
          </RouterLink>
          <button class="patient-btn-logout" type="button" title="Đăng xuất" @click="handleLogout">
            <span>Đăng xuất</span>
          </button>
        </template>
        <template v-else>
          <RouterLink class="guest-login-link" to="/login">Đăng nhập</RouterLink>
          <RouterLink class="guest-book-btn" to="/book-appointment">Đặt lịch khám</RouterLink>
        </template>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { RouterLink, useRoute, useRouter } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const isPatient = computed(() => authStore.isAuthenticated && authStore.role === 'PATIENT');
const menuOpen = ref(false);
const menuButton = ref<HTMLButtonElement | null>(null);

function handleAnchorClick(sectionId: string, event: Event) {
  event.preventDefault();
  if (route.path !== '/') {
    router.push({ path: '/', hash: `#${sectionId}` });
  } else {
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth' });
    }
  }
  if (menuOpen.value) {
    menuOpen.value = false;
  }
}

async function handleLogout() {
  await authStore.logout();
  router.push('/login');
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && menuOpen.value) {
    menuOpen.value = false;
    void nextTick(() => menuButton.value?.focus());
  }
}

function onResize() {
  if (window.innerWidth > 992) {
    menuOpen.value = false;
  }
}

watch(() => route.fullPath, () => {
  if (menuOpen.value) {
    menuOpen.value = false;
  }
});

onMounted(() => {
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', onResize);
});

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown);
  window.removeEventListener('resize', onResize);
});
</script>

<style scoped>
.patient-topbar {
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid #e4eef6;
  position: sticky;
  top: 0;
  z-index: 50;
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
  font-weight: 850;
  font-size: 1.05rem;
  letter-spacing: -0.01em;
  color: #0f2d59;
  line-height: 1.2;
}

.patient-badge-portal {
  display: inline-block;
  align-self: flex-start;
  font-size: 0.68rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #0284c7;
  background: #e0f2fe;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  border: 1px solid #bae6fd;
}

.patient-badge-portal.guest-tag {
  color: #b45309;
  background: #fef3c7;
  border-color: #fde68a;
}

.patient-menu-toggle {
  display: none;
  background: #f1f5f9;
  border: 1px solid #cbd5e1;
  color: #0f2d59;
  font-size: 1.25rem;
  border-radius: 8px;
  width: 40px;
  height: 40px;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.patient-nav-center {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.patient-nav-item {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.5rem 0.85rem;
  color: #334155;
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 650;
  border-radius: 8px;
  transition: all 0.15s ease;
  position: relative;
}

.patient-nav-item:hover {
  color: #0284c7;
  background: #f0f9ff;
}

.patient-nav-item.router-link-active,
.patient-nav-item.router-link-exact-active {
  color: #0284c7;
  background: #e0f2fe;
  font-weight: 750;
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
  opacity: 0.85;
}

.patient-nav-item.router-link-active .p-nav-ic,
.patient-nav-item.router-link-exact-active .p-nav-ic {
  opacity: 1;
  stroke: #0284c7;
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
  gap: 0.6rem;
  text-decoration: none;
  padding: 0.35rem 0.65rem;
  border-radius: 999px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  transition: all 0.15s ease;
}

.patient-user-profile:hover {
  background: #f1f5f9;
  border-color: #cbd5e1;
}

.patient-avatar-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: linear-gradient(135deg, #0284c7, #0f2d59);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 0.82rem;
  flex-shrink: 0;
}

.patient-user-text {
  display: flex;
  flex-direction: column;
  line-height: 1.2;
}

.patient-user-name {
  font-size: 0.825rem;
  font-weight: 750;
  color: #0f2d59;
  max-width: 140px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.patient-user-role {
  font-size: 0.7rem;
  color: #64748b;
  font-weight: 600;
}

.patient-btn-logout {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.45rem 0.8rem;
  background: #ffffff;
  border: 1px solid #fee2e2;
  color: #dc2626;
  border-radius: 8px;
  font-size: 0.825rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
}

.patient-btn-logout:hover {
  background: #fef2f2;
  border-color: #fca5a5;
  color: #b91c1c;
}

.guest-login-link {
  color: #0f2d59;
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 700;
  padding: 0.45rem 0.85rem;
  border-radius: 8px;
  transition: all 0.15s ease;
}

.guest-login-link:hover {
  background: #f1f5f9;
  color: #0284c7;
}

.guest-book-btn {
  background: linear-gradient(135deg, #d97706, #b45309);
  color: #ffffff;
  text-decoration: none;
  font-size: 0.875rem;
  font-weight: 750;
  padding: 0.55rem 1.15rem;
  border-radius: 8px;
  box-shadow: 0 2px 6px rgba(180, 83, 9, 0.2);
  transition: all 0.15s ease;
}

.guest-book-btn:hover {
  background: linear-gradient(135deg, #b45309, #92400e);
  transform: translateY(-1px);
}

@media (max-width: 992px) {
  .patient-menu-toggle {
    display: inline-flex;
  }

  .patient-topbar[data-menu-open="true"] .patient-nav-center {
    display: flex;
    position: absolute;
    top: 68px;
    left: 0;
    right: 0;
    background: #ffffff;
    flex-direction: column;
    padding: 1rem 1.5rem 1.5rem;
    box-shadow: 0 10px 20px rgba(15, 45, 89, 0.08);
    border-bottom: 1px solid #e2e8f0;
    gap: 0.5rem;
    align-items: stretch;
  }

  .patient-topbar[data-menu-open="false"] .patient-nav-center {
    display: none;
  }

  .patient-nav-item {
    padding: 0.75rem 1rem;
    font-size: 0.95rem;
  }

  .patient-user-text {
    display: none;
  }
}
</style>