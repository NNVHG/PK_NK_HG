import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const legacyDestinations: Record<string, string> = {
  'my-patient-record': 'patient-portal-my-record',
  'booking-request-review': 'management-booking-request-review',
  'medicine-stock': 'management-medicine-stock',
  'chair-status': 'management-chair-status',
  'scheduling-settings': 'management-scheduling-settings',
  'stock-issues': 'management-stock-issues',
  'stock-receipts': 'management-stock-receipts',
  'clinic-print-settings': 'management-clinic-print-settings',
  'doctor-signature': 'management-doctor-signature',
  'medicine-catalog': 'management-medicine-catalog',
  'revenue-report': 'management-revenue-report',
  'medical-supply-catalog': 'management-medical-supplies',
  'diagnosis-catalog': 'management-diagnoses',
  'role-permissions': 'management-role-permissions',
  'staff-workspace': 'management-staff-workspace',
  'dental-chairs': 'management-dental-chairs',
  'service-catalog': 'management-service-catalog',
  'patient-invoices': 'patient-portal-invoices',
  'invoice-desk': 'management-invoices',
  'counter-booking': 'management-counter-booking',
  'daily-queue': 'management-daily-queue',
  'appointment-desk': 'management-appointment-desk',
  'patient-appointment-history': 'patient-portal-appointments-history',
  'admin-staff': 'management-admin-staff',
  'admin-audit-logs': 'management-audit-logs',
  'appointments': 'patient-portal-appointments',
  'queue-check-in': 'management-queue-check-in',
  'patients': 'management-patients',
  'clinical-diagnosis': 'management-clinical-diagnosis'
};

const sharedLegacyDestinations: Record<string, [string, string]> = {
  'dashboard': ['patient-portal-dashboard', 'management-dashboard'],
  'profile': ['patient-portal-profile', 'management-profile'],
  'patient-detail': ['patient-portal-patient-detail', 'management-patient-detail'],
  'tooth-image-lookup': ['patient-portal-visit-images', 'management-tooth-images'],
  'prescription': ['patient-portal-visit-prescription', 'management-prescription'],
  'invoice-draft': ['patient-portal-invoice-draft', 'management-invoice-draft'],
  'fdi-service-assignment': ['patient-portal-fdi-services', 'management-fdi-services'],
  'fdi-condition': ['patient-portal-fdi-condition', 'management-fdi-condition']
};

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // ==================== KHU VỰC BỆNH NHÂN (/patient/*) ====================
    { path: '/patient/my-record', name: 'patient-portal-my-record', component: () => import('@/views/MyPatientRecordView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/appointments', name: 'patient-portal-appointments', component: () => import('@/views/AppointmentsView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/appointments/mine', name: 'patient-portal-appointments-history', component: () => import('@/views/PatientAppointmentHistoryView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/invoices/mine', name: 'patient-portal-invoices', component: () => import('@/views/PatientInvoicesView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/dashboard', name: 'patient-portal-dashboard', component: () => import('@/views/DashboardView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/profile', name: 'patient-portal-profile', component: () => import('@/views/ProfileView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/clinical/visits/:visitId/images', name: 'patient-portal-visit-images', component: () => import('@/views/ToothImageLookupView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/clinical/visits/:visitId/prescription', name: 'patient-portal-visit-prescription', component: () => import('@/views/PrescriptionView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/clinical/visits/:visitId/invoice-draft', name: 'patient-portal-invoice-draft', component: () => import('@/views/InvoiceDraftView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/clinical/visits/:visitId/services', name: 'patient-portal-fdi-services', component: () => import('@/views/FdiServiceAssignmentView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/clinical/visits/:visitId/fdi', name: 'patient-portal-fdi-condition', component: () => import('@/views/FdiConditionView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    { path: '/patient/patients/:id(\\d+)', name: 'patient-portal-patient-detail', component: () => import('@/views/PatientDetailView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    // ==================== KHU VỰC QUẢN LÝ / NHÂN VIÊN (/management/*) ====================
    { path: '/management/dashboard', name: 'management-dashboard', component: () => import('@/views/DashboardView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/profile', name: 'management-profile', component: () => import('@/views/ProfileView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/queue/today', name: 'management-daily-queue', component: () => import('@/views/DailyQueueView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/queue/check-in', name: 'management-queue-check-in', component: () => import('@/views/QueueCheckInView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] } },
    { path: '/management/patients', name: 'management-patients', component: () => import('@/views/PatientsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/patients/:id(\\d+)', name: 'management-patient-detail', component: () => import('@/views/PatientDetailView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/appointments/requests', name: 'management-booking-request-review', component: () => import('@/views/BookingRequestReviewView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] } },
    { path: '/management/appointments/counter', name: 'management-counter-booking', component: () => import('@/views/CounterBookingView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] } },
    { path: '/management/appointments/desk', name: 'management-appointment-desk', component: () => import('@/views/AppointmentDeskView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] } },
    { path: '/management/invoices', name: 'management-invoices', component: () => import('@/views/InvoicesView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] } },
    { path: '/management/clinical/diagnosis', name: 'management-clinical-diagnosis', component: () => import('@/views/ClinicalDiagnosisView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST'] } },
    { path: '/management/clinical/visits/:visitId/images', name: 'management-tooth-images', component: () => import('@/views/ToothImageLookupView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST'] } },
    { path: '/management/clinical/visits/:visitId/prescription', name: 'management-prescription', component: () => import('@/views/PrescriptionView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT'] } },
    { path: '/management/clinical/visits/:visitId/invoice-draft', name: 'management-invoice-draft', component: () => import('@/views/InvoiceDraftView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST'] } },
    { path: '/management/clinical/visits/:visitId/services', name: 'management-fdi-services', component: () => import('@/views/FdiServiceAssignmentView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT'] } },
    { path: '/management/clinical/visits/:visitId/fdi', name: 'management-fdi-condition', component: () => import('@/views/FdiConditionView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT'] } },
    { path: '/management/staff/workspace', name: 'management-staff-workspace', component: () => import('@/views/StaffWorkspacesView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'ASSISTANT'] } },
    { path: '/management/staff/chair-status', name: 'management-chair-status', component: () => import('@/views/ChairStatusView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT'] } },
    { path: '/management/staff/signatures/:doctorId', name: 'management-doctor-signature', component: () => import('@/views/DoctorSignatureView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST'] } },
    { path: '/management/inventory/receipts', name: 'management-stock-receipts', component: () => import('@/views/StockReceiptsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT', 'DENTIST'] } },
    { path: '/management/inventory/issues', name: 'management-stock-issues', component: () => import('@/views/StockIssuesView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT'] } },
    { path: '/management/admin/staff', name: 'management-admin-staff', component: () => import('@/views/StaffView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/chairs', name: 'management-dental-chairs', component: () => import('@/views/DentalChairsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/services', name: 'management-service-catalog', component: () => import('@/views/ServiceCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/medicines', name: 'management-medicine-catalog', component: () => import('@/views/MedicineCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/medicine-stock', name: 'management-medicine-stock', component: () => import('@/views/MedicineStockView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/medical-supplies', name: 'management-medical-supplies', component: () => import('@/views/MedicalSupplyCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/diagnoses', name: 'management-diagnoses', component: () => import('@/views/DiagnosisCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/scheduling-settings', name: 'management-scheduling-settings', component: () => import('@/views/SchedulingSettingsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/clinic-print-settings', name: 'management-clinic-print-settings', component: () => import('@/views/ClinicPrintSettingsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/role-permissions', name: 'management-role-permissions', component: () => import('@/views/RolePermissionsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/reports/revenue', name: 'management-revenue-report', component: () => import('@/views/RevenueReportView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/management/admin/audit-logs', name: 'management-audit-logs', component: () => import('@/views/AuditLogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },



    { path: '/my-record', name: 'my-patient-record', component: () => import('@/views/MyPatientRecordView.vue'), meta: { requiresAuth: true, roles: ['PATIENT'] } },
    {path:'/book-appointment',name:'guest-booking',component:()=>import('@/views/GuestBookingView.vue'),meta:{requiresAuth:false}},
    {path:'/appointments/requests',name:'booking-request-review',component:()=>import('@/views/BookingRequestReviewView.vue'),meta:{requiresAuth:true,roles:['ADMIN','RECEPTIONIST']}},
    { path: '/clinical/visits/:visitId/images', name: 'tooth-image-lookup', component: () => import('@/views/ToothImageLookupView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'] } },
    { path: '/admin/medicine-stock', name: 'medicine-stock', component: () => import('@/views/MedicineStockView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/staff/chair-status', name: 'chair-status', component: () => import('@/views/ChairStatusView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT'] } },
    { path: '/admin/scheduling-settings', name: 'scheduling-settings', component: () => import('@/views/SchedulingSettingsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/inventory/issues', name: 'stock-issues', component: () => import('@/views/StockIssuesView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT'] } },
    { path: '/inventory/receipts', name: 'stock-receipts', component: () => import('@/views/StockReceiptsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT', 'DENTIST'] } },
    { path: '/forgot-password', name: 'password-recovery', component: () => import('@/views/PasswordRecoveryView.vue'), meta: { guestOnly: true } },
    { path: '/admin/clinic-print-settings', name: 'clinic-print-settings', component: () => import('@/views/ClinicPrintSettingsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/staff/signatures/:doctorId', name: 'doctor-signature', component: () => import('@/views/DoctorSignatureView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST'] } },
    { path: '/portal/prescriptions/:code', name: 'public-prescription', component: () => import('@/views/PublicPrescriptionView.vue'), meta: { requiresAuth: false } },
    { path: '/clinical/visits/:visitId/prescription', name: 'prescription', component: () => import('@/views/PrescriptionView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'ASSISTANT', 'PATIENT'] } },
    { path: '/admin/medicines', name: 'medicine-catalog', component: () => import('@/views/MedicineCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/admin/reports/revenue', name: 'revenue-report', component: () => import('@/views/RevenueReportView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/admin/medical-supplies', name: 'medical-supply-catalog', component: () => import('@/views/MedicalSupplyCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    { path: '/admin/diagnoses', name: 'diagnosis-catalog', component: () => import('@/views/DiagnosisCatalogView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    {
      path: '/admin/role-permissions',
      name: 'role-permissions',
      component: () => import('@/views/RolePermissionsView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN'] }
    },
    { path: '/staff/workspace', name: 'staff-workspace', component: () => import('@/views/StaffWorkspacesView.vue'), meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST', 'ASSISTANT'] } },
    { path: '/admin/chairs', name: 'dental-chairs', component: () => import('@/views/DentalChairsView.vue'), meta: { requiresAuth: true, roles: ['ADMIN'] } },
    {
      path: '/admin/services',
      name: 'service-catalog',
      component: () => import('@/views/ServiceCatalogView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN'] }
    },
    {
      path: '/invoices/mine',
      name: 'patient-invoices',
      component: () => import('@/views/PatientInvoicesView.vue'),
      meta: { requiresAuth: true, roles: ['PATIENT'] }
    },
    {
      path: '/invoices',
      name: 'invoice-desk',
      component: () => import('@/views/InvoicesView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] }
    },
    {
      path: '/appointments/counter',
      name: 'counter-booking',
      component: () => import('@/views/CounterBookingView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] }
    },
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
    { path: '/activate-account', name: 'patient-activation', component: () => import('@/views/PatientActivationView.vue'), meta: { guestOnly: true } },
    {
      path: '/register',
      name: 'register',
      redirect: { name: 'guest-booking' },
      meta: { guestOnly: true }
    },
    {
      path: '/queue/today',
      name: 'daily-queue',
      component: () => import('@/views/DailyQueueView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] }
    },
    {
      path: '/queue/display',
      name: 'queue-display',
      component: () => import('@/views/QueueDisplayView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/queue/public-monitor',
      name: 'queue-public-monitor',
      component: () => import('@/views/QueueDisplayView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/appointments/desk',
      name: 'appointment-desk',
      component: () => import('@/views/AppointmentDeskView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST'] }
    },
    {
      path: '/appointments/mine',
      name: 'patient-appointment-history',
      component: () => import('@/views/PatientAppointmentHistoryView.vue'),
      meta: { requiresAuth: true, roles: ['PATIENT'] }
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guestOnly: true }
    },
    {
      path: '/',
      name: 'public-home',
      component: () => import('@/views/PublicHomeView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/dashboard',
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
      path: '/my-records',
      name: 'my-patient-records',
      redirect: { name: 'patient-portal-my-record' },
      meta: { requiresAuth: true, roles: ['PATIENT'] }
    },
    {
      path: '/patients/:id(\\d+)',
      name: 'patient-detail',
      component: () => import('@/views/PatientDetailView.vue'),
      meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT', 'PATIENT'] }
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
  const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
  const userRole = authStore.role;
  const isStaff = staffRoles.includes(userRole);
  const isPatient = userRole === 'PATIENT';

  // 1. Tuyến yêu cầu đăng nhập nhưng chưa xác thực
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return next({ name: 'login', query: { redirect: to.fullPath } });
  }

  // 2. Tuyến chỉ dành cho khách (guest-only) khi đã đăng nhập
  if (to.meta.guestOnly && authStore.isAuthenticated) {
    if (isPatient) {
      return next({ path: '/patient/my-record' });
    }
    return next({ path: '/management/dashboard' });
  }

  // 3. Phân lập khu vực giao diện theo vai trò thực tế
  if (authStore.isAuthenticated) {
    // Bệnh nhân không được truy cập khu vực quản lý /management/*
    if (isPatient && to.path.startsWith('/management/')) {
      return next({ path: '/patient/my-record' });
    }

    // Nhân viên không truy cập khu vực cá nhân bệnh nhân /patient/*
    if (isStaff && to.path.startsWith('/patient/')) {
      return next({ path: '/management/dashboard' });
    }

    // Kiểm tra danh sách vai trò cho phép theo cấu hình route
    if (to.meta.roles && Array.isArray(to.meta.roles)) {
      if (!to.meta.roles.includes(userRole)) {
        if (isPatient) {
          return next({ path: '/patient/my-record' });
        }
        return next({ path: '/management/dashboard' });
      }
    }

    // Chuyển URL cũ sang khu vực đúng vai trò sau khi đã kiểm tra quyền.
    if (typeof to.name === 'string') {
      const destination = legacyDestinations[to.name]
        ?? sharedLegacyDestinations[to.name]?.[isPatient ? 0 : 1];
      if (destination) {
        return next({ name: destination, params: to.params, query: to.query, hash: to.hash, replace: true });
      }
    }
  }

  next();
});

export default router;
