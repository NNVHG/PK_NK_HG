import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/clinical/visits/:visitId/invoice-draft',
      name: 'invoice-draft',
      component: () => import('@/views/InvoiceDraftView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'] }
    },
    {
      path: '/clinical/visits/:visitId/services',
      name: 'fdi-service-assignment',
      component: () => import('@/views/FdiServiceAssignmentView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT', 'PATIENT'] }
    },
    {
      path: '/clinical/visits/:visitId/fdi',
      name: 'fdi-condition',
      component: () => import('@/views/FdiConditionView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT', 'PATIENT'] }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { guestOnly: true }
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guestOnly: true }
    },
    {
      path: '/',
      name: 'dashboard',
      component: () => import('@/views/DashboardView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('@/views/ProfileView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/admin/staff',
      name: 'admin-staff',
      component: () => import('@/views/StaffView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN'] }
    },
    {
      path: '/admin/audit-logs',
      name: 'admin-audit-logs',
      component: () => import('@/views/AuditLogView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN'] }
    },
    {
      path: '/appointments',
      name: 'appointments',
      component: () => import('@/views/AppointmentsView.vue'),
      meta: { requiresAuth: true, roles: ['PATIENT'] }
    },
    {
      path: '/queue/check-in',
      name: 'queue-check-in',
      component: () => import('@/views/QueueCheckInView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] }
    },
    {
      path: '/patients',
      name: 'patients',
      component: () => import('@/views/PatientsView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] }
    },
    {
      path: '/clinical/diagnosis',
      name: 'clinical-diagnosis',
      component: () => import('@/views/ClinicalDiagnosisView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST'] }
    },
    {
      path: '/patients/:id(\\d+)',
      name: 'patient-detail',
      component: () => import('@/views/PatientDetailView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] }
    },
    ...(import.meta.env.DEV
      ? [
          {
            path: '/dev/fdi-chart',
            name: 'dev-fdi-chart',
            component: () => import('@/views/FdiPreviewView.vue'),
            meta: { requiresAuth: false }
          }
        ]
      : []),
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue')
    }
  ]
});

router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore();

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return next({ name: 'login', query: { redirect: to.fullPath } });
  }

  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return next({ name: 'dashboard' });
  }

  if (to.meta.roles && Array.isArray(to.meta.roles)) {
    const userRole = authStore.role;
    if (!to.meta.roles.includes(userRole)) {
      return next({ name: 'dashboard' });
    }
  }

  next();
});

export default router;
