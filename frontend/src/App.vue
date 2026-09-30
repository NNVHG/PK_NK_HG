<template>
  <div class="layout">
    <header v-if="authStore.isAuthenticated" class="navbar">
      <div class="brand">
        <h2>Phòng Khám Nha Khoa</h2>
      </div>
      <nav class="nav-links">
        <router-link to="/">Bảng điều khiển</router-link>
        <template v-if="isStaff">
          <router-link to="/appointments">Lịch hẹn</router-link>
          <router-link to="/patients">Bệnh nhân</router-link>
        </template>
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
  align-items: center;
  justify-content: space-between;
  background-color: #2c3e50;
  color: #fff;
  padding: 0.75rem 1.5rem;
}

.brand h2 {
  font-size: 1.15rem;
  font-weight: 600;
}

.nav-links {
  display: flex;
  gap: 1.25rem;
}

.nav-links a {
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
  align-items: center;
  gap: 1rem;
}

.btn-logout {
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
  padding: 1.5rem;
  max-width: 1200px;
  margin: 0 auto;
  width: 100%;
}
</style>
