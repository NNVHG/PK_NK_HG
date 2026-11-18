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
            <img src="/logo-ngang.svg" alt="Nha Khoa Hoàng Gia" class="patient-brand-logo" width="158" height="42" />
            <span class="patient-badge-portal">Cổng Bệnh Nhân</span>
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
          <router-link to="/patient/dashboard" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
            <span>Tổng quan</span>
          </router-link>

          <router-link to="/patient/my-record" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1" ry="1"/></svg>
            <span>Hồ sơ của tôi</span>
          </router-link>

          <router-link to="/patient/appointments/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
            <span>Lịch hẹn</span>
          </router-link>

          <router-link to="/patient/invoices/mine" class="patient-nav-item">
            <svg class="p-nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            <span>Hóa đơn</span>
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

    <!-- ==================== 0B. THANH TRÊN CÙNG CHO NHÂN VIÊN & ADMIN ==================== -->
    <header
      v-if="authStore.isAuthenticated && isPortalRoute && !isPatient"
      class="patient-topbar staff-topbar"
      :data-menu-open="menuOpen"
    >
      <div class="patient-topbar-inner staff-topbar-inner">
        <div class="patient-brand-block">
          <router-link to="/management/dashboard" class="patient-brand-link" aria-label="Nha Khoa Hoàng Gia - Khu quản lý">
            <img src="/logo-ngang.svg" alt="Nha Khoa Hoàng Gia" class="patient-brand-logo" width="158" height="42" />
            <span v-if="isAdmin" class="patient-badge-portal staff-role-pill staff-role-pill--admin">Khu Admin</span>
            <span v-else class="patient-badge-portal staff-role-pill">{{ authStore.user?.roleName || 'Khu Quản Lý' }}</span>
          </router-link>
        </div>

        <button
          ref="menuButton"
          class="patient-menu-toggle"
          type="button"
          aria-controls="portal-navigation"
          :aria-expanded="menuOpen"
          :aria-label="menuOpen ? 'Đóng menu điều hướng' : 'Mở menu điều hướng'"
          @click="menuOpen = !menuOpen"
        >
          <span aria-hidden="true">{{ menuOpen ? '✕' : '☰' }}</span>
        </button>

        <div v-if="authStore.role === 'DENTIST'" class="staff-topbar-room">
          <button
            type="button"
            class="staff-room-pill"
            :class="{ 'staff-room-pill--missing': !dentistHasValidRoom }"
            :disabled="dentistRoomSaving || (dentistHasValidRoom && dentistWorkspace?.canChange === false)"
            :title="dentistHasValidRoom ? (dentistWorkspace?.canChange === false ? 'Đang có ca khám hoặc lời mời, không thể đổi phòng' : 'Nhấn để đổi phòng điều trị') : 'Bắt buộc chọn phòng điều trị để bắt đầu khám'"
            @click="openDentistRoomModal(true)"
          >
            <span aria-hidden="true">{{ dentistHasValidRoom ? '🏥' : '⚠️' }}</span>
            <span>{{ dentistHasValidRoom ? `Phòng: ${dentistWorkspace?.roomName}` : 'Chưa chọn phòng điều trị' }}</span>
            <small v-if="dentistHasValidRoom && dentistWorkspace?.canChange !== false" class="staff-room-change">Đổi</small>
          </button>
        </div>

        <div class="patient-nav-user staff-topbar-user">
          <router-link to="/management/profile" class="patient-user-profile" title="Xem hồ sơ cá nhân & đổi mật khẩu">
            <div class="patient-avatar-circle staff-avatar-circle" aria-hidden="true">
              {{ (authStore.user?.fullName || 'NV').charAt(0).toUpperCase() }}
            </div>
            <div class="patient-user-text">
              <span class="patient-user-name">{{ authStore.user?.fullName || 'Nhân viên' }}</span>
              <span class="patient-user-role">{{ authStore.user?.roleName || authStore.role }}</span>
            </div>
          </router-link>

          <button class="patient-btn-logout" type="button" title="Đăng xuất" @click="handleLogout">
            <svg class="p-btn-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
            <span>Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>

    <div :class="authStore.isAuthenticated && isPortalRoute && !isPatient ? 'portal-workspace' : 'portal-workspace-plain'">
    <!-- ==================== 1. THANH BÊN TRÁI: DÀNH CHO NHÂN VIÊN & ADMIN ==================== -->
    <header
      v-if="authStore.isAuthenticated && isPortalRoute && !isPatient"
      class="navbar navbar-left navbar-staff"
      :class="{ 'navbar-staff--collapsed': leftNavCollapsed }"
      :data-menu-open="menuOpen"
    >
      <div class="staff-sidebar-head">
        <span v-show="!leftNavCollapsed" class="staff-sidebar-heading">Điều hướng</span>
        <button
          type="button"
          class="staff-sidebar-toggle"
          :title="leftNavCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'"
          :aria-label="leftNavCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'"
          @click="leftNavCollapsed = !leftNavCollapsed"
        >
          {{ leftNavCollapsed ? '»' : '«' }}
        </button>
      </div>

      <div id="portal-navigation" class="portal-navigation">
        <nav class="nav-links" aria-label="Điều hướng chính">
          <!-- THANH BÊN TRÁI: DÀNH RIÊNG CHO CHỨC NĂNG ADMIN -->
          <template v-if="isAdmin">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard" class="link-dashboard-nav" title="Bảng điều khiển">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/></svg>
              <span class="nav-label">Bảng điều khiển</span>
            </router-link>

            <span class="nav-caption caption-admin">Quản Trị Tổ Chức</span>
            <router-link to="/management/admin/staff" title="Tài khoản nhân viên">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
              <span class="nav-label">Tài khoản nhân viên</span>
            </router-link>
            <router-link to="/management/admin/role-permissions" title="Ma trận quyền RBAC">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
              <span class="nav-label">Ma trận quyền RBAC</span>
            </router-link>
            <router-link to="/management/admin/reports/revenue" title="Báo cáo doanh thu">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
              <span class="nav-label">Báo cáo doanh thu</span>
            </router-link>
            <router-link to="/management/admin/audit-logs" title="Nhật ký thao tác (Audit)">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
              <span class="nav-label">Nhật ký thao tác (Audit)</span>
            </router-link>

            <span class="nav-caption caption-admin">Danh Mục Chuyên Môn</span>
            <router-link to="/management/admin/services" title="Dịch vụ & bảng giá">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/></svg>
              <span class="nav-label">Dịch vụ & bảng giá</span>
            </router-link>
            <router-link to="/management/admin/diagnoses" title="Danh mục chẩn đoán">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
              <span class="nav-label">Danh mục chẩn đoán</span>
            </router-link>
            <router-link to="/management/admin/medicines" title="Danh mục thuốc">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z"/><path d="m8.5 8.5 7 7"/></svg>
              <span class="nav-label">Danh mục thuốc</span>
            </router-link>
            <router-link to="/management/admin/medicine-stock" title="Nhập & tồn thuốc">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/></svg>
              <span class="nav-label">Nhập & tồn thuốc</span>
            </router-link>
            <router-link to="/management/admin/medical-supplies" title="Danh mục vật tư">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="7" width="18" height="14" rx="2"/><path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="12" y1="11" x2="12" y2="17"/><line x1="9" y1="14" x2="15" y2="14"/></svg>
              <span class="nav-label">Danh mục vật tư</span>
            </router-link>

            <span class="nav-caption caption-admin">Thiết Lập Phòng Khám</span>
            <router-link to="/management/admin/chairs" title="Phòng điều trị & Trạng thái">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 21h18"/><path d="M5 21V7l7-4 7 4v14"/><path d="M9 21v-6h6v6"/></svg>
              <span class="nav-label">Phòng điều trị &amp; Trạng thái</span>
            </router-link>
            <router-link to="/management/admin/scheduling-settings" title="Giờ làm việc & hẹn">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
              <span class="nav-label">Giờ làm việc & hẹn</span>
            </router-link>
            <router-link to="/management/admin/clinic-print-settings" title="Thông tin trên bản in">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
              <span class="nav-label">Thông tin trên bản in</span>
            </router-link>
          </template>

          <!-- Lễ tân / Thu ngân -->
          <template v-else-if="authStore.role === 'RECEPTIONIST'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard" class="link-dashboard-nav" title="Bảng điều khiển">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/></svg>
              <span class="nav-label">Bảng điều khiển</span>
            </router-link>

            <span class="nav-caption">Lịch hẹn</span>
            <router-link to="/management/appointments" title="Lịch hẹn">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
              <span class="nav-label">Lịch hẹn</span>
            </router-link>

            <span class="nav-caption">Tiếp đón & Thanh toán</span>
            <router-link to="/management/queue/check-in" title="Tiếp đón & Check-in">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><polyline points="17 11 19 13 23 9"/></svg>
              <span class="nav-label">Tiếp đón & Check-in</span>
            </router-link>
            <router-link to="/management/invoices" title="Tra cứu & Thu tiền">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
              <span class="nav-label">Tra cứu & Thu tiền</span>
            </router-link>

            <span class="nav-caption">Dùng chung</span>
            <router-link to="/management/queue/today" title="Hàng đợi hôm nay">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
              <span class="nav-label">Hàng đợi hôm nay</span>
            </router-link>
            <router-link to="/management/patients" title="Hồ sơ bệnh nhân">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
              <span class="nav-label">Hồ sơ bệnh nhân</span>
            </router-link>
          </template>

          <!-- Nha sĩ -->
          <template v-else-if="authStore.role === 'DENTIST'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard" class="link-dashboard-nav" title="Bảng điều khiển">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/></svg>
              <span class="nav-label">Bảng điều khiển</span>
            </router-link>

            <span class="nav-caption">Nghiệp vụ Nha sĩ</span>
            <router-link to="/management/clinical/diagnosis" title="Khám & chẩn đoán FDI">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
              <span class="nav-label">Khám & chẩn đoán FDI</span>
            </router-link>
            <router-link to="/management/inventory/receipts" title="Xem tồn vật tư">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
              <span class="nav-label">Xem tồn vật tư</span>
            </router-link>

            <span class="nav-caption">Dùng chung</span>
            <router-link to="/management/queue/today" title="Hàng đợi hôm nay">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
              <span class="nav-label">Hàng đợi hôm nay</span>
            </router-link>
            <router-link to="/management/patients" title="Hồ sơ bệnh nhân">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
              <span class="nav-label">Hồ sơ bệnh nhân</span>
            </router-link>
            <router-link to="/management/staff/workspace" title="Phòng làm việc">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 21h18"/><path d="M5 21V7l7-4 7 4v14"/></svg>
              <span class="nav-label">Phòng làm việc</span>
            </router-link>
          </template>

          <!-- Phụ tá -->
          <template v-else-if="authStore.role === 'ASSISTANT'">
            <span class="nav-caption">Tổng quan</span>
            <router-link to="/management/dashboard" class="link-dashboard-nav" title="Bảng điều khiển">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/></svg>
              <span class="nav-label">Bảng điều khiển</span>
            </router-link>

            <span class="nav-caption">Nghiệp vụ Phụ tá</span>
            <router-link to="/management/inventory/receipts" title="Tồn kho & nhập kho">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              <span class="nav-label">Tồn kho & nhập kho</span>
            </router-link>
            <router-link to="/management/inventory/issues" title="Tiêu hao theo ca">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1"/></svg>
              <span class="nav-label">Tiêu hao theo ca</span>
            </router-link>

            <span class="nav-caption">Dùng chung</span>
            <router-link to="/management/queue/today" title="Hàng đợi hôm nay">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
              <span class="nav-label">Hàng đợi hôm nay</span>
            </router-link>
            <router-link to="/management/patients" title="Hồ sơ bệnh nhân">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
              <span class="nav-label">Hồ sơ bệnh nhân</span>
            </router-link>
            <router-link to="/management/staff/workspace" title="Nhóm & phòng làm việc">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
              <span class="nav-label">Nhóm & phòng làm việc</span>
            </router-link>
          </template>
        </nav>
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
      <div class="staff-sidebar-head right-nav-header">
        <button
          type="button"
          class="staff-sidebar-toggle"
          :title="rightNavCollapsed ? 'Mở rộng thanh chức năng actor khác' : 'Thu gọn thanh bên'"
          :aria-label="rightNavCollapsed ? 'Mở rộng thanh chức năng actor khác' : 'Thu gọn thanh bên'"
          @click="rightNavCollapsed = !rightNavCollapsed"
        >
          {{ rightNavCollapsed ? '«' : '»' }}
        </button>
        <span v-show="!rightNavCollapsed" class="staff-sidebar-heading">Actor khác</span>
      </div>

      <div class="right-nav-body portal-navigation">
        <nav class="nav-links nav-links--actors" aria-label="Nghiệp vụ các vai trò khác">
          <span class="nav-caption caption-reception">Tiếp Đón & Thu Ngân</span>
          <router-link to="/management/queue/check-in" title="Tiếp đón & Check-in">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><polyline points="17 11 19 13 23 9"/></svg>
            <span class="nav-label">Tiếp đón & Check-in</span>
          </router-link>
          <router-link to="/management/appointments" title="Lịch hẹn">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
            <span class="nav-label">Lịch hẹn</span>
          </router-link>
          <router-link to="/management/invoices" title="Tra cứu & Thu hóa đơn">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            <span class="nav-label">Tra cứu & Thu hóa đơn</span>
          </router-link>

          <span class="nav-caption caption-dentist">Khám & Điều Trị</span>
          <router-link to="/management/clinical/diagnosis" title="Khám lâm sàng & FDI">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
            <span class="nav-label">Khám lâm sàng & FDI</span>
          </router-link>

          <span class="nav-caption caption-assistant">Phụ Tá Điều Trị</span>
          <router-link to="/management/inventory/receipts" title="Tồn kho & nhập kho">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
            <span class="nav-label">Tồn kho & nhập kho</span>
          </router-link>
          <router-link to="/management/inventory/issues" title="Tiêu hao theo ca">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1"/></svg>
            <span class="nav-label">Tiêu hao theo ca</span>
          </router-link>

          <span class="nav-caption caption-shared">Phối Hợp Lâm Sàng</span>
          <router-link to="/management/queue/today" title="Hàng đợi hôm nay">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
            <span class="nav-label">Hàng đợi hôm nay</span>
          </router-link>
          <router-link to="/management/patients" title="Bệnh nhân & CCCD">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
            <span class="nav-label">Bệnh nhân & CCCD</span>
          </router-link>
          <router-link to="/management/staff/workspace" title="Phòng làm việc nhân sự">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
            <span class="nav-label">Phòng làm việc nhân sự</span>
          </router-link>
        </nav>
      </div>
    </aside>
    </div>

    <!-- Popup bắt buộc chọn phòng điều trị cho Nha sĩ -->
    <div
      v-if="showDentistRoomModal && authStore.isAuthenticated && authStore.role === 'DENTIST' && isPortalRoute"
      class="dentist-room-backdrop"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dentist-room-modal-title"
      @click.self="dentistHasValidRoom ? closeDentistRoomModal() : undefined"
    >
      <div class="dentist-room-modal">
        <div class="dentist-room-head">
          <div>
            <span class="dentist-room-kicker">Phân Bổ Phòng Làm Việc</span>
            <h2 id="dentist-room-modal-title">Chọn phòng điều trị hôm nay</h2>
          </div>
          <button
            v-if="dentistHasValidRoom || (!dentistRoomLoading && !dentistRoomChairs.some(isChairSelectable))"
            type="button"
            class="dentist-room-close"
            :disabled="dentistRoomSaving"
            aria-label="Đóng"
            @click="closeDentistRoomModal"
          >✕</button>
        </div>

        <p class="dentist-room-desc">
          <template v-if="!dentistHasValidRoom">
            Bạn cần <strong>chọn một phòng điều trị đang sẵn sàng</strong> trước khi gọi số và bắt đầu khám bệnh nhân. Khi bạn đăng xuất, hệ thống sẽ tự động cập nhật lại trạng thái phòng.
          </template>
          <template v-else>
            Phòng hiện tại: <strong>{{ dentistWorkspace?.roomName }}</strong>. Chọn phòng sẵn sàng khác nếu bạn muốn chuyển phòng.
          </template>
        </p>

        <div v-if="dentistRoomError" class="dentist-room-alert" role="alert">⚠️ {{ dentistRoomError }}</div>

        <div v-if="dentistRoomLoading" class="dentist-room-loading" role="status">
          Đang tải danh sách phòng điều trị…
        </div>

        <div v-else class="dentist-room-grid">
          <label
            v-for="chair in dentistRoomChairs"
            :key="chair.id"
            class="dentist-room-card"
            :class="{
              'dentist-room-card--selected': selectedChairId === chair.id,
              'dentist-room-card--disabled': !isChairSelectable(chair)
            }"
          >
            <input
              v-model.number="selectedChairId"
              type="radio"
              name="dentist-room-choice"
              :value="chair.id"
              :disabled="!isChairSelectable(chair) || dentistRoomSaving"
            />
            <div class="dentist-room-card-body">
              <div class="dentist-room-card-top">
                <strong>{{ chair.roomName }}</strong>
                <span class="dentist-room-code">{{ chair.chairNumber }}</span>
              </div>
              <span class="dentist-room-status" :class="chairStatusBadgeClass(chair)">
                {{ chairStatusText(chair) }}
              </span>
            </div>
          </label>
        </div>

        <div class="dentist-room-actions">
          <button
            type="button"
            class="dentist-room-refresh"
            :disabled="dentistRoomLoading || dentistRoomSaving"
            @click="loadDentistRoomOptions"
          >
            🔄 Tải lại phòng
          </button>
          <div class="dentist-room-actions-right">
            <button
              v-if="dentistHasValidRoom"
              type="button"
              class="dentist-room-cancel"
              :disabled="dentistRoomSaving"
              @click="closeDentistRoomModal"
            >
              Đóng
            </button>
            <button
              type="button"
              class="dentist-room-confirm"
              :disabled="dentistRoomLoading || dentistRoomSaving || !selectedChairId"
              @click="confirmDentistRoom"
            >
              {{ dentistRoomSaving ? 'Đang xác nhận…' : '✓ Xác nhận vào phòng' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter, useRoute } from 'vue-router';
import { workspacesService, type WorkspaceChair, type WorkspaceSnapshot } from '@/services/workspaces';

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
const leftNavCollapsed = ref(false);
const rightNavCollapsed = ref(false);
const menuButton = ref<HTMLButtonElement | null>(null);
const mainContent = ref<HTMLElement | null>(null);

const dentistWorkspace = ref<WorkspaceSnapshot | null>(null);
const dentistRoomChairs = ref<WorkspaceChair[]>([]);
const showDentistRoomModal = ref(false);
const dentistRoomLoading = ref(false);
const dentistRoomSaving = ref(false);
const dentistRoomError = ref('');
const selectedChairId = ref<number | null>(null);
const dentistHasValidRoom = computed(() => Boolean(dentistWorkspace.value?.chairId && dentistWorkspace.value?.isLocationUsable));

function isChairSelectable(chair: WorkspaceChair) {
  const myId = authStore.user?.userId;
  return chair.isActive && chair.status === 1 && (!chair.claimedByUserId || chair.claimedByUserId === myId);
}

function chairStatusText(chair: WorkspaceChair) {
  const myId = authStore.user?.userId;
  if (!chair.isActive || chair.status === 4) return 'Ngừng sử dụng';
  if (chair.status === 3) return 'Đang bảo trì';
  if (chair.status === 2) return 'Đang có ca khám';
  if (chair.claimedByUserId && chair.claimedByUserId !== myId) return 'Đã có nha sĩ khác chọn';
  if (chair.claimedByUserId === myId) return 'Phòng bạn đang chọn';
  return 'Sẵn sàng';
}

function chairStatusBadgeClass(chair: WorkspaceChair) {
  const myId = authStore.user?.userId;
  if (!chair.isActive || chair.status === 4) return 'status--off';
  if (chair.status === 3) return 'status--maint';
  if (chair.status === 2) return 'status--busy';
  if (chair.claimedByUserId && chair.claimedByUserId !== myId) return 'status--claimed';
  return 'status--ready';
}

async function loadDentistRoomOptions() {
  const userId = authStore.user?.userId;
  dentistRoomLoading.value = true;
  dentistRoomError.value = '';
  try {
    const [opts, latestSnap] = await Promise.all([
      workspacesService.options(),
      userId ? workspacesService.get(userId).catch(() => dentistWorkspace.value) : Promise.resolve(null)
    ]);
    if (latestSnap) dentistWorkspace.value = latestSnap;
    dentistRoomChairs.value = opts.chairs || [];
    const currentId = dentistWorkspace.value?.chairId ?? null;
    if (currentId && dentistRoomChairs.value.some(c => c.id === currentId && isChairSelectable(c))) {
      selectedChairId.value = currentId;
    } else {
      const firstAvail = dentistRoomChairs.value.find(isChairSelectable);
      selectedChairId.value = firstAvail ? firstAvail.id : null;
    }
  } catch {
    dentistRoomError.value = 'Không thể tải danh sách phòng điều trị. Vui lòng thử lại.';
  } finally {
    dentistRoomLoading.value = false;
  }
}

async function openDentistRoomModal(manual = false) {
  if (authStore.role !== 'DENTIST' || !authStore.isAuthenticated) return;
  if (manual && dentistHasValidRoom.value && dentistWorkspace.value?.canChange === false) return;
  showDentistRoomModal.value = true;
  await loadDentistRoomOptions();
}

function closeDentistRoomModal() {
  const noSelectableRoom = !dentistRoomLoading.value && !dentistRoomChairs.value.some(isChairSelectable);
  if ((!dentistHasValidRoom.value && !noSelectableRoom) || dentistRoomSaving.value) return;
  showDentistRoomModal.value = false;
  dentistRoomError.value = '';
}

async function syncDentistWorkspace(openIfMissing = true) {
  const userId = authStore.user?.userId;
  if (!authStore.isAuthenticated || authStore.role !== 'DENTIST' || !isPortalRoute.value || !userId) {
    dentistWorkspace.value = null;
    showDentistRoomModal.value = false;
    return;
  }
  try {
    const snap = await workspacesService.get(userId);
    dentistWorkspace.value = snap;
    if (!snap.chairId || !snap.isLocationUsable) {
      if (openIfMissing) await openDentistRoomModal(false);
    } else {
      showDentistRoomModal.value = false;
    }
  } catch {
    // Keep silent on background check; user can click pill if needed
  }
}

async function confirmDentistRoom() {
  const userId = authStore.user?.userId;
  if (!userId || !selectedChairId.value || dentistRoomSaving.value) return;
  dentistRoomSaving.value = true;
  dentistRoomError.value = '';
  try {
    const latest = await workspacesService.get(userId);
    dentistWorkspace.value = latest;
    const updated = await workspacesService.change(userId, {
      chairId: selectedChairId.value,
      dentistId: null,
      expectedVersion: latest.version ?? 0,
      confirmed: true
    });
    dentistWorkspace.value = updated;
    showDentistRoomModal.value = false;
    window.dispatchEvent(new CustomEvent('dentist-workspace-updated', { detail: updated }));
  } catch (err: unknown) {
    const res = err as { response?: { data?: { message?: string } } };
    dentistRoomError.value = res?.response?.data?.message || 'Không thể chọn phòng này. Vui lòng tải lại và chọn phòng khác.';
    await loadDentistRoomOptions();
  } finally {
    dentistRoomSaving.value = false;
  }
}

function onWorkspaceUpdatedEvent(ev: Event) {
  const detail = (ev as CustomEvent<WorkspaceSnapshot>).detail;
  if (detail && authStore.role === 'DENTIST' && detail.userId === authStore.user?.userId) {
    dentistWorkspace.value = detail;
    if (detail.chairId && detail.isLocationUsable) {
      showDentistRoomModal.value = false;
    }
  }
}

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

watch(() => [authStore.isAuthenticated, authStore.role, authStore.user?.userId, isPortalRoute.value], ([authenticated]) => {
  if (!authenticated) {
    menuOpen.value = false;
    dentistWorkspace.value = null;
    showDentistRoomModal.value = false;
  } else {
    void syncDentistWorkspace(true);
  }
}, { immediate: true });

onMounted(() => {
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', onResize);
  window.addEventListener('dentist-workspace-updated', onWorkspaceUpdatedEvent);
  if (window.innerWidth < 1200) rightNavCollapsed.value = true;
});

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown);
  window.removeEventListener('resize', onResize);
  window.removeEventListener('dentist-workspace-updated', onWorkspaceUpdatedEvent);
});

async function handleLogout() {
  await authStore.logout();
  dentistWorkspace.value = null;
  dentistRoomChairs.value = [];
  showDentistRoomModal.value = false;
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
  flex-direction: column;
  background: #f4f7fb;
  min-height: 100vh;
}

.portal-workspace {
  display: flex;
  flex: 1;
  min-height: 0;
}

.portal-workspace-plain {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.staff-topbar-inner {
  max-width: none;
}

.staff-brand-title {
  margin: 0;
  font-size: 0.98rem;
  font-weight: 850;
  color: #0f2d59;
  letter-spacing: -0.015em;
  line-height: 1.2;
}

.staff-role-pill--admin {
  background: #fef3c7;
  color: #b45309;
}

.staff-avatar-circle {
  background: linear-gradient(135deg, #102e56, #1857b6);
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

.patient-topbar-inner.staff-topbar-inner {
  width: 100%;
  max-width: none;
  margin: 0;
  padding: 0 0.75rem;
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
  width: 158px;
  height: 42px;
  object-fit: contain;
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
  align-self: center;
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
.patient-nav-item.router-link-active {
  background: #0f2d59;
  color: #ffffff;
}

.patient-nav-item.router-link-exact-active .p-nav-ic,
.patient-nav-item.router-link-active .p-nav-ic {
  stroke: #ffffff;
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
  width: 256px;
  height: calc(100vh - 68px);
  position: sticky;
  top: 68px;
  color: #fff;
  background: linear-gradient(180deg, #0f2d59 0%, #0d2344 100%);
  padding: 0.9rem 0.75rem 1.1rem;
  min-width: 0;
  z-index: 30;
  border-right: 1px solid rgba(255, 255, 255, 0.08);
  transition: width 0.24s cubic-bezier(0.22, 1, 0.36, 1), padding 0.24s ease;
}

.navbar-staff {
  border-bottom: 3px solid #e5b95e;
}

.navbar-staff--collapsed {
  width: 72px;
  padding: 0.9rem 0.45rem 1.1rem;
}

.staff-sidebar-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0 0.35rem 0.65rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.12);
}

.navbar-staff--collapsed .staff-sidebar-head {
  justify-content: center;
  padding: 0 0 0.65rem;
}

.staff-sidebar-heading {
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #9cb4d2;
}

.staff-sidebar-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.08);
  color: #ffffff;
  font-size: 0.9rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
}

.staff-sidebar-toggle:hover {
  background: #1857b6;
  border-color: #60a5fa;
}

.portal-navigation {
  min-height: 0;
  display: flex;
  flex: 1;
  flex-direction: column;
  margin-top: 0.65rem;
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
  color: #dbeafe;
  padding: 0.52rem 0.7rem;
  border-radius: 10px;
  border-left: 3px solid transparent;
  text-decoration: none;
  font-size: 0.845rem;
  font-weight: 600;
  line-height: 1.35;
  overflow-wrap: anywhere;
  transition: all 0.15s ease;
  display: flex;
  align-items: center;
  gap: 0.55rem;
}

.nav-links a:hover {
  color: #ffffff;
  background: rgba(255, 255, 255, 0.1);
}

.nav-links a.router-link-exact-active {
  color: #ffffff;
  background: #1d63c9;
  border-left-color: #e5b95e;
  font-weight: 750;
}

.navbar-staff--collapsed .nav-caption {
  display: none;
}

.navbar-staff--collapsed .nav-links a {
  justify-content: center;
  padding: 0.65rem 0.4rem;
  border-left-width: 0;
}

.navbar-staff--collapsed .nav-label {
  display: none;
}

.nav-svg {
  width: 17px;
  height: 17px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.85;
  stroke-linecap: round;
  stroke-linejoin: round;
  flex-shrink: 0;
}

.navbar-staff--collapsed .nav-svg {
  width: 19px;
  height: 19px;
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
  overflow-wrap: anywhere;
}

.user-role-badge {
  font-size: 0.7rem;
  color: #e5b95e;
  font-weight: 700;
}

.btn-logout {
  width: 100%;
  box-sizing: border-box;
  flex-shrink: 0;
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
  width: 256px;
  height: calc(100vh - 68px);
  position: sticky;
  top: 68px;
  background: linear-gradient(180deg, #0f2d59 0%, #0d2344 100%);
  color: #ffffff;
  padding: 0.9rem 0.75rem 1.1rem;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  border-bottom: 3px solid #e5b95e;
  z-index: 25;
  transition: width 0.24s cubic-bezier(0.22, 1, 0.36, 1), padding 0.24s ease;
}

.navbar-right-actors--collapsed {
  width: 72px;
  padding: 0.9rem 0.45rem 1.1rem;
}

.navbar-right-actors--collapsed .staff-sidebar-head {
  justify-content: center;
  padding: 0 0 0.65rem;
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
  border-left-width: 0;
  border-right: 3px solid transparent;
}

.navbar-right-actors .nav-links a.router-link-exact-active {
  background: #1d63c9;
  color: #ffffff;
  border-right-color: #e5b95e;
}

.navbar-right-actors--collapsed .nav-caption,
.navbar-right-actors--collapsed .nav-label {
  display: none;
}

.navbar-right-actors--collapsed .nav-links a {
  justify-content: center;
  padding: 0.65rem 0.4rem;
  border-right-width: 0;
}

.navbar-right-actors--collapsed .nav-svg {
  width: 19px;
  height: 19px;
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

  .portal-workspace {
    flex-direction: column;
  }
  .staff-collapse-btn,
  .staff-sidebar-head {
    display: none;
  }
  .navbar-left {
    position: relative;
    top: 0;
    width: 100%;
    height: auto;
    padding: 0;
    border-bottom: none;
  }
  .portal-navigation {
    display: none;
    max-height: calc(100vh - 96px);
    overflow-y: auto;
    margin-top: 0;
    padding: 0.75rem 1rem 1rem;
  }
  .navbar-left[data-menu-open="true"] .portal-navigation {
    display: flex;
  }
  .navbar-staff--collapsed .nav-caption,
  .navbar-staff--collapsed .nav-label {
    display: block;
  }
  .navbar-staff--collapsed .nav-links a {
    justify-content: flex-start;
    padding: 0.52rem 0.7rem;
  }
  .navbar-right-actors {
    position: relative;
    top: 0;
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
  .patient-topbar, .navbar-left, .navbar-right-actors, .dentist-room-backdrop { display: none; }
  .main-content { max-width: none; padding: 0; }
}

.staff-topbar-room {
  display: flex;
  align-items: center;
}

.staff-room-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.42rem 0.85rem;
  border-radius: 999px;
  border: 1px solid #bae6fd;
  background: #f0f9ff;
  color: #0369a1;
  font: inherit;
  font-size: 0.83rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
}

.staff-room-pill:hover:not(:disabled) {
  background: #e0f2fe;
  border-color: #7dd3fc;
}

.staff-room-pill:disabled {
  opacity: 0.75;
  cursor: not-allowed;
}

.staff-room-pill--missing {
  background: #fef3c7;
  border-color: #fcd34d;
  color: #b45309;
}

.staff-room-pill--missing:hover:not(:disabled) {
  background: #fde68a;
}

.staff-room-change {
  font-size: 0.72rem;
  font-weight: 800;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  background: #ffffff;
  color: #0284c7;
  border: 1px solid #bae6fd;
}

.dentist-room-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1000;
  background: rgba(15, 23, 42, 0.58);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.dentist-room-modal {
  width: 100%;
  max-width: 620px;
  background: #ffffff;
  border-radius: 16px;
  border: 1px solid #e2e8f0;
  box-shadow: 0 24px 48px rgba(15, 45, 89, 0.22);
  padding: 1.5rem;
  max-height: 90vh;
  overflow-y: auto;
  box-sizing: border-box;
}

.dentist-room-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.dentist-room-kicker {
  display: inline-block;
  font-size: 0.72rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #0284c7;
  margin-bottom: 0.2rem;
}

.dentist-room-head h2 {
  margin: 0;
  font-size: 1.25rem;
  color: #0f2d59;
  font-weight: 800;
}

.dentist-room-close {
  border: 1px solid #cbd5e1;
  background: #f8fafc;
  color: #475569;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 700;
}

.dentist-room-desc {
  margin: 0 0 1rem;
  font-size: 0.9rem;
  line-height: 1.5;
  color: #475569;
}

.dentist-room-alert {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fecaca;
  border-radius: 10px;
  padding: 0.75rem 1rem;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

.dentist-room-loading {
  padding: 2rem;
  text-align: center;
  color: #64748b;
  font-size: 0.925rem;
}

.dentist-room-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

.dentist-room-card {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
  padding: 0.85rem;
  border-radius: 12px;
  border: 1.5px solid #e2e8f0;
  background: #ffffff;
  cursor: pointer;
  transition: all 0.15s ease;
}

.dentist-room-card:hover:not(.dentist-room-card--disabled) {
  border-color: #38bdf8;
  background: #f8fbff;
}

.dentist-room-card--selected {
  border-color: #0284c7;
  background: #f0f9ff;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.14);
}

.dentist-room-card--disabled {
  opacity: 0.58;
  cursor: not-allowed;
  background: #f8fafc;
}

.dentist-room-card input {
  margin-top: 0.25rem;
  accent-color: #0284c7;
}

.dentist-room-card-body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.dentist-room-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  color: #0f2d59;
  font-size: 0.92rem;
}

.dentist-room-code {
  font-size: 0.72rem;
  font-weight: 700;
  color: #64748b;
  background: #f1f5f9;
  padding: 0.1rem 0.4rem;
  border-radius: 6px;
}

.dentist-room-status {
  display: inline-block;
  align-self: flex-start;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
}

.dentist-room-status.status--ready { background: #dcfce7; color: #166534; }
.dentist-room-status.status--busy { background: #dbeafe; color: #1e40af; }
.dentist-room-status.status--maint { background: #fef3c7; color: #92400e; }
.dentist-room-status.status--claimed { background: #ede9fe; color: #5b21b6; }
.dentist-room-status.status--off { background: #e2e8f0; color: #475569; }

.dentist-room-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
  border-top: 1px solid #e2e8f0;
  padding-top: 1rem;
}

.dentist-room-actions-right {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.dentist-room-refresh,
.dentist-room-cancel,
.dentist-room-confirm {
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  border-radius: 9px;
  padding: 0.55rem 1rem;
  cursor: pointer;
}

.dentist-room-refresh,
.dentist-room-cancel {
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #334155;
}

.dentist-room-confirm {
  border: 0;
  background: #0284c7;
  color: #ffffff;
}

.dentist-room-confirm:hover:not(:disabled) {
  background: #0369a1;
}

.dentist-room-confirm:disabled,
.dentist-room-refresh:disabled,
.dentist-room-cancel:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
