<template>
  <div
    class="layout"
    :class="{
      'layout--portal': authStore.isAuthenticated && isPortalRoute && !isPatient,
      'layout--patient': authStore.isAuthenticated && isPatient
    }"
  >
    <PageTransitionLoader :visible="routeTransitionVisible" />
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

          <button class="patient-btn-logout" type="button" title="Đăng xuất" :disabled="logoutInProgress" @click="handleLogout">
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

        <div v-if="isWorkspaceRole" class="staff-topbar-room">
          <button
            type="button"
            class="staff-room-pill"
            :class="{ 'staff-room-pill--missing': !workspaceConfirmed }"
            :disabled="workspaceSaving || logoutInProgress || (isDentist && staffWorkspace?.canChange === false)"
            :title="workspaceLabel"
            @click="openWorkspaceModal"
          >
            <span class="staff-room-label">{{ workspaceLabel }}</span>
            <small v-if="workspaceConfirmed && staffWorkspace?.canChange !== false" class="staff-room-change">Đổi</small>
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

          <button class="patient-btn-logout" type="button" title="Đăng xuất" :disabled="workspaceSaving || logoutInProgress" @click="handleLogout">
            <svg class="p-btn-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
            <span>Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>

    <p v-if="logoutError && !showWorkspaceModal" class="staff-logout-error" role="alert">{{ logoutError }}</p>

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
            <router-link to="/management/admin/medicines" title="Thuốc & Tồn kho">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z"/><path d="m8.5 8.5 7 7"/></svg>
              <span class="nav-label">Thuốc &amp; Tồn kho</span>
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
            <router-link to="/management/clinical/pre-exam" title="Đo sinh hiệu & Tiền khám">
              <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
              <span class="nav-label">Đo sinh hiệu &amp; Tiền khám</span>
            </router-link>
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
      <router-view v-slot="{ Component }">
        <Transition name="page-route" mode="out-in">
          <div v-if="Component" :key="route.path" class="page-transition-stage">
            <component :is="Component" />
          </div>
        </Transition>
      </router-view>
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
          <router-link to="/management/clinical/pre-exam" title="Đo sinh hiệu & Tiền khám">
            <svg class="nav-svg" viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
            <span class="nav-label">Đo sinh hiệu &amp; Tiền khám</span>
          </router-link>
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

    <dialog
      v-if="authStore.isAuthenticated && isWorkspaceRole && isPortalRoute"
      ref="workspaceDialog"
      class="dentist-room-modal"
      aria-labelledby="workspace-modal-title"
      @cancel.prevent="closeWorkspaceModal"
    >
      <form @submit.prevent="confirmWorkspace">
        <div class="dentist-room-head">
          <h2 id="workspace-modal-title">
            {{ isDentist ? 'Chọn phòng điều trị hôm nay' : 'Chọn nha sĩ hỗ trợ hôm nay' }}
          </h2>
          <button
            v-if="canCloseWorkspaceModal"
            type="button"
            class="dentist-room-close"
            :disabled="workspaceSaving || logoutInProgress"
            aria-label="Đóng"
            @click="closeWorkspaceModal"
          >✕</button>
        </div>

        <p class="dentist-room-desc">
          <template v-if="isDentist">
            Chọn phòng trước khi gọi số và bắt đầu khám bệnh nhân.
          </template>
          <template v-else>
            Chọn nha sĩ đã nhận phòng hoặc tiếp tục mà không hỗ trợ nha sĩ nào.
          </template>
        </p>

        <div v-if="workspaceError" class="dentist-room-alert" role="alert">{{ workspaceError }}</div>
        <div v-if="logoutError" class="dentist-room-alert" role="alert">{{ logoutError }}</div>

        <div v-if="workspaceLoading" class="dentist-room-loading" role="status">
          {{ isDentist ? 'Đang tải danh sách phòng…' : 'Đang tải danh sách nha sĩ…' }}
        </div>

        <fieldset v-else-if="workspaceOptions" class="dentist-room-grid" :disabled="workspaceSaving || logoutInProgress" aria-labelledby="workspace-modal-title">
          <template v-if="isDentist">
            <p v-if="!workspaceOptions.chairs.some(isChairSelectable)" class="workspace-empty">
              Chưa có phòng sẵn sàng. Hãy tải lại danh sách hoặc liên hệ quản trị viên.
            </p>
            <label
              v-for="chair in workspaceOptions.chairs"
              :key="chair.id"
              class="dentist-room-card"
              :class="{
                'dentist-room-card--selected': selectedWorkspaceId === chair.id,
                'dentist-room-card--disabled': !isChairSelectable(chair)
              }"
            >
              <input v-model.number="selectedWorkspaceId" type="radio" name="workspace-choice" :value="chair.id" :disabled="!isChairSelectable(chair)" />
              <span class="dentist-room-card-body">
                <span class="dentist-room-card-top">{{ chair.roomName }}</span>
                <span class="dentist-room-status" :class="chairStatusBadgeClass(chair)">{{ chairStatusText(chair) }}</span>
              </span>
            </label>
          </template>
          <template v-else>
            <p v-if="!workingDentists.length" class="workspace-empty">Chưa có nha sĩ nào nhận phòng.</p>
            <label
              v-for="dentist in workingDentists"
              :key="dentist.dentistId"
              class="dentist-room-card"
              :class="{ 'dentist-room-card--selected': selectedWorkspaceId === dentist.dentistId }"
            >
              <input v-model.number="selectedWorkspaceId" type="radio" name="workspace-choice" :value="dentist.dentistId" />
              <span class="dentist-room-card-body">
                <span class="dentist-room-card-top">{{ dentist.dentistName }}</span>
                <span class="dentist-room-status status--ready">{{ dentist.roomName }}</span>
              </span>
            </label>
            <label class="dentist-room-card" :class="{ 'dentist-room-card--selected': selectedWorkspaceId === 0 }">
              <input v-model.number="selectedWorkspaceId" type="radio" name="workspace-choice" :value="0" />
              <span class="dentist-room-card-top">Không hỗ trợ nha sĩ nào</span>
            </label>
          </template>
        </fieldset>

        <div class="dentist-room-actions">
          <button
            type="button"
            class="dentist-room-refresh"
            :disabled="workspaceLoading || workspaceSaving || logoutInProgress"
            @click="loadWorkspace(true)"
          >
            Tải lại danh sách
          </button>
          <div class="dentist-room-actions-right">
            <button
              type="button"
              class="dentist-room-cancel"
              :disabled="workspaceSaving || logoutInProgress"
              @click="canCloseWorkspaceModal ? closeWorkspaceModal() : handleLogout()"
            >
              {{ canCloseWorkspaceModal ? 'Đóng' : 'Đăng xuất' }}
            </button>
            <button
              type="submit"
              class="dentist-room-confirm"
              :disabled="workspaceLoading || workspaceSaving || logoutInProgress || !validWorkspaceChoice"
            >
              {{ workspaceSaving ? 'Đang xác nhận…' : 'Xác nhận' }}
            </button>
          </div>
        </div>
      </form>
    </dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRouter, useRoute } from 'vue-router';
import PageTransitionLoader from '@/components/PageTransitionLoader.vue';
import {
  isSelectableWorkspaceChair,
  isWorkingDentist,
  WORKSPACE_UPDATED_EVENT,
  workspacesService,
  type WorkspaceChair,
  type WorkspaceOptions,
  type WorkspaceSnapshot
} from '@/services/workspaces';

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

const routeTransitionVisible = ref(false);
let transitionStartedAt = 0;
let transitionHideTimer: ReturnType<typeof setTimeout> | undefined;
let transitionFailsafeTimer: ReturnType<typeof setTimeout> | undefined;

function clearTransitionTimers() {
  if (transitionHideTimer) {
    clearTimeout(transitionHideTimer);
    transitionHideTimer = undefined;
  }
  if (transitionFailsafeTimer) {
    clearTimeout(transitionFailsafeTimer);
    transitionFailsafeTimer = undefined;
  }
}

const removeBeforeEach = router.beforeEach((to, from) => {
  if (from.matched.length > 0 && to.path !== from.path) {
    clearTransitionTimers();
    transitionStartedAt = Date.now();
    routeTransitionVisible.value = true;
    transitionFailsafeTimer = setTimeout(() => {
      routeTransitionVisible.value = false;
    }, 4000);
  }
});

const removeAfterEach = router.afterEach(() => {
  if (!routeTransitionVisible.value) return;
  clearTransitionTimers();
  const elapsed = Date.now() - transitionStartedAt;
  const remaining = Math.max(0, 520 - elapsed);
  if (remaining === 0) {
    routeTransitionVisible.value = false;
  } else {
    transitionHideTimer = setTimeout(() => {
      routeTransitionVisible.value = false;
    }, remaining);
  }
});

const removeOnError = router.onError(() => {
  clearTransitionTimers();
  routeTransitionVisible.value = false;
});

const isDentist = computed(() => authStore.role === 'DENTIST');
const isWorkspaceRole = computed(() => isDentist.value || authStore.role === 'ASSISTANT');
const staffWorkspace = ref<WorkspaceSnapshot | null>(null);
const workspaceOptions = ref<WorkspaceOptions | null>(null);
const workspaceDialog = ref<HTMLDialogElement | null>(null);
const showWorkspaceModal = ref(false);
const workspaceLoading = ref(false);
const workspaceSaving = ref(false);
const workspaceError = ref('');
const selectedWorkspaceId = ref<number | null>(null);
const logoutInProgress = ref(false);
const logoutError = ref('');
const WORKSPACE_PROMPTED_TOKEN_KEY = 'dental_workspace_prompted_token';
function readPromptedWorkspaceToken(): string | null {
  try {
    return typeof window !== 'undefined' && window.sessionStorage
      ? window.sessionStorage.getItem(WORKSPACE_PROMPTED_TOKEN_KEY)
      : null;
  } catch {
    return null;
  }
}
function persistPromptedWorkspaceToken(value: string | null) {
  try {
    if (typeof window === 'undefined' || !window.sessionStorage) return;
    if (value) window.sessionStorage.setItem(WORKSPACE_PROMPTED_TOKEN_KEY, value);
    else window.sessionStorage.removeItem(WORKSPACE_PROMPTED_TOKEN_KEY);
  } catch {}
}
let workspaceRequest = 0;
let promptedWorkspaceToken: string | null = readPromptedWorkspaceToken();
let assistantWorkspaceTimer: ReturnType<typeof setInterval> | undefined;
let refreshingAssistantWorkspace = false;

const workingDentists = computed(() => workspaceOptions.value?.dentists.filter(isWorkingDentist) ?? []);
const workspaceConfirmed = computed(() => {
  const snapshot = staffWorkspace.value;
  if (!snapshot) return false;
  if (isDentist.value) return Boolean(snapshot.chairId && snapshot.isLocationUsable);
  if (snapshot.workingDentistId) return snapshot.isLocationUsable;
  return snapshot.hasConfirmedToday === true;
});
const workspaceLabel = computed(() => {
  if (isDentist.value) return workspaceConfirmed.value
    ? staffWorkspace.value?.roomName ?? 'Phòng điều trị'
    : 'Chọn phòng điều trị';
  if (!workspaceConfirmed.value) return 'Chọn nha sĩ hỗ trợ';
  return staffWorkspace.value?.dentistName
    ? `Hỗ trợ ${staffWorkspace.value.dentistName}`
    : 'Không hỗ trợ nha sĩ';
});
const canCloseWorkspaceModal = computed(() => workspaceConfirmed.value
  || (isDentist.value && workspaceOptions.value !== null && !workspaceOptions.value.chairs.some(isChairSelectable)));
const validWorkspaceChoice = computed(() => {
  const selectedId = selectedWorkspaceId.value;
  if (!workspaceOptions.value || !staffWorkspace.value?.canChange || selectedId === null) return false;
  if (isDentist.value) {
    return workspaceOptions.value.chairs.some(chair => chair.id === selectedId && isChairSelectable(chair));
  }
  return selectedId === 0 || workingDentists.value.some(dentist => dentist.dentistId === selectedId);
});

function isChairSelectable(chair: WorkspaceChair) {
  const userId = authStore.user?.userId;
  if (userId === undefined) return false;
  const hasCurrentVisitRoom = workspaceOptions.value?.chairs.some(option => option.isCurrentVisitRoom);
  return (!hasCurrentVisitRoom || chair.isCurrentVisitRoom === true) && isSelectableWorkspaceChair(chair, userId);
}

function chairStatusText(chair: WorkspaceChair) {
  const myId = authStore.user?.userId;
  if (!chair.isActive || chair.status === 4) return 'Ngừng sử dụng';
  if (chair.status === 3) return 'Đang bảo trì';
  if (chair.isCurrentVisitRoom) return 'Phòng ca khám của bạn';
  if (chair.status === 2) return 'Đang có ca khám';
  if (chair.claimedByUserId && chair.claimedByUserId !== myId) return 'Đã có nha sĩ khác chọn';
  if (chair.claimedByUserId === myId) return 'Phòng bạn đang chọn';
  if (!isChairSelectable(chair)) return 'Bạn đang có ca khám';
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

async function loadWorkspace(openForEdit = false, promptAtLogin = false) {
  const userId = authStore.user?.userId;
  if (!authStore.isAuthenticated || !isWorkspaceRole.value || !isPortalRoute.value || !userId
    || workspaceSaving.value || logoutInProgress.value) return;
  const request = ++workspaceRequest;
  workspaceLoading.value = true;
  workspaceError.value = '';
  workspaceOptions.value = null;
  selectedWorkspaceId.value = null;
  if (openForEdit) showWorkspaceModal.value = true;

  try {
    const snapshot = await workspacesService.get(userId);
    if (request !== workspaceRequest) return;
    if (snapshot.userId !== userId || snapshot.roleCode !== authStore.role) throw new Error('Invalid workspace');
    staffWorkspace.value = snapshot;
    if (!openForEdit && workspaceConfirmed.value && (!promptAtLogin || !snapshot.canChange)) return;

    showWorkspaceModal.value = true;
    const options = await workspacesService.options();
    if (request !== workspaceRequest) return;
    workspaceOptions.value = options;
    if (isDentist.value && options.chairs.some(chair => chair.id === snapshot.chairId && isChairSelectable(chair))) {
      selectedWorkspaceId.value = snapshot.chairId;
    } else if (!isDentist.value && workingDentists.value.some(dentist => dentist.dentistId === snapshot.workingDentistId)) {
      selectedWorkspaceId.value = snapshot.workingDentistId;
    } else if (!isDentist.value && workspaceConfirmed.value) {
      selectedWorkspaceId.value = 0;
    }
  } catch {
    if (request !== workspaceRequest) return;
    showWorkspaceModal.value = true;
    workspaceError.value = 'Không thể tải lựa chọn làm việc. Vui lòng thử lại.';
  } finally {
    if (request === workspaceRequest) workspaceLoading.value = false;
  }
}

function openWorkspaceModal() {
  if (isDentist.value && staffWorkspace.value?.canChange === false) return;
  void loadWorkspace(true);
}

function closeWorkspaceModal() {
  if (!canCloseWorkspaceModal.value || workspaceSaving.value || logoutInProgress.value) return;
  showWorkspaceModal.value = false;
}

async function confirmWorkspace() {
  const userId = authStore.user?.userId;
  const snapshot = staffWorkspace.value;
  const selectedId = selectedWorkspaceId.value;
  if (!userId || snapshot?.userId !== userId || !validWorkspaceChoice.value ||
    workspaceLoading.value || workspaceSaving.value || logoutInProgress.value) return;
  const request = workspaceRequest;
  workspaceSaving.value = true;
  workspaceError.value = '';
  try {
    const updated = await workspacesService.change(userId, {
      chairId: isDentist.value ? selectedId : null,
      dentistId: !isDentist.value && selectedId ? selectedId : null,
      expectedVersion: snapshot.version,
      confirmed: true
    });
    if (request !== workspaceRequest) return;
    if (updated.userId !== userId || updated.roleCode !== authStore.role) throw new Error('Invalid workspace');
    staffWorkspace.value = updated;
    showWorkspaceModal.value = false;
    window.dispatchEvent(new CustomEvent(WORKSPACE_UPDATED_EVENT, { detail: updated }));
  } catch (err: unknown) {
    if (request !== workspaceRequest) return;
    const res = err as { response?: { data?: { message?: string } } };
    workspaceError.value = res?.response?.data?.message || 'Không thể xác nhận lựa chọn. Hãy tải lại danh sách và thử lại.';
    workspaceOptions.value = null;
  } finally {
    if (request === workspaceRequest) workspaceSaving.value = false;
  }
}

function onWorkspaceUpdatedEvent(ev: Event) {
  const detail = (ev as CustomEvent<WorkspaceSnapshot>).detail;
  if (detail && isWorkspaceRole.value && detail.userId === authStore.user?.userId) {
    staffWorkspace.value = detail;
  }
}

async function refreshAssistantWorkspace() {
  const userId = authStore.user?.userId;
  if (!userId || authStore.role !== 'ASSISTANT' || !isPortalRoute.value || document.hidden
    || workspaceLoading.value || workspaceSaving.value || logoutInProgress.value || refreshingAssistantWorkspace) return;
  const showingOptions = showWorkspaceModal.value;
  if (showingOptions && !workspaceOptions.value) return;
  const request = workspaceRequest;
  const canApply = () => request === workspaceRequest && showingOptions === showWorkspaceModal.value
    && !workspaceLoading.value && !workspaceSaving.value && !logoutInProgress.value;
  refreshingAssistantWorkspace = true;
  try {
    if (showingOptions) {
      const options = await workspacesService.options();
      if (!canApply()) return;
      workspaceOptions.value = options;
      if (selectedWorkspaceId.value && !validWorkspaceChoice.value) selectedWorkspaceId.value = null;
    } else {
      const previous = staffWorkspace.value;
      const updated = await workspacesService.get(userId);
      if (!canApply() || staffWorkspace.value !== previous || updated.userId !== userId || updated.roleCode !== 'ASSISTANT') return;
      if (previous?.version !== updated.version || previous?.roomName !== updated.roomName
        || previous?.dentistName !== updated.dentistName || previous?.isLocationUsable !== updated.isLocationUsable
        || previous?.workingDentistId !== updated.workingDentistId || previous?.hasConfirmedToday !== updated.hasConfirmedToday) {
        window.dispatchEvent(new CustomEvent(WORKSPACE_UPDATED_EVENT, { detail: updated }));
      }
    }
  } catch {
    // Keep the last successful data available when the background refresh fails.
  } finally {
    refreshingAssistantWorkspace = false;
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

watch([workspaceDialog, showWorkspaceModal], ([dialog, open]) => {
  if (!dialog) return;
  if (open && !dialog.open) dialog.showModal();
  if (!open && dialog.open) dialog.close();
}, { flush: 'post' });

watch(() => [authStore.token, authStore.role, authStore.user?.userId, isPortalRoute.value], () => {
  workspaceRequest++;
  staffWorkspace.value = null;
  workspaceOptions.value = null;
  selectedWorkspaceId.value = null;
  workspaceError.value = '';
  workspaceLoading.value = false;
  workspaceSaving.value = false;
  showWorkspaceModal.value = false;
  if (!authStore.isAuthenticated) {
    menuOpen.value = false;
    promptedWorkspaceToken = null;
    persistPromptedWorkspaceToken(null);
  } else if (isWorkspaceRole.value && isPortalRoute.value) {
    const promptAtLogin = promptedWorkspaceToken !== authStore.token;
    promptedWorkspaceToken = authStore.token;
    persistPromptedWorkspaceToken(authStore.token);
    void loadWorkspace(false, promptAtLogin);
  }
}, { immediate: true });

onMounted(() => {
  if (authStore.isAuthenticated) {
    void authStore.fetchMe();
  }
  document.addEventListener('keydown', onKeydown);
  window.addEventListener('resize', onResize);
  window.addEventListener(WORKSPACE_UPDATED_EVENT, onWorkspaceUpdatedEvent);
  assistantWorkspaceTimer = setInterval(refreshAssistantWorkspace, 15000);
  if (window.innerWidth < 1200) rightNavCollapsed.value = true;
});

onBeforeUnmount(() => {
  workspaceRequest++;
  clearInterval(assistantWorkspaceTimer);
  clearTransitionTimers();
  removeBeforeEach();
  removeAfterEach();
  removeOnError();
  document.removeEventListener('keydown', onKeydown);
  window.removeEventListener('resize', onResize);
  window.removeEventListener(WORKSPACE_UPDATED_EVENT, onWorkspaceUpdatedEvent);
});

async function handleLogout() {
  if (logoutInProgress.value || workspaceSaving.value) return;
  logoutError.value = '';
  logoutInProgress.value = true;
  try {
    await authStore.logout();
    await router.push('/login');
  } catch (err: unknown) {
    const response = err as { response?: { data?: { message?: string } } };
    logoutError.value = response?.response?.data?.message || 'Chưa thể đăng xuất. Vui lòng thử lại.';
  } finally {
    logoutInProgress.value = false;
  }
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
  min-width: 0;
}

.page-route-enter-active,
.page-route-leave-active {
  transition: opacity 0.22s ease, transform 0.24s cubic-bezier(0.22, 1, 0.36, 1);
}

.page-route-enter-from {
  opacity: 0;
  transform: translate3d(0, 10px, 0);
}

.page-route-leave-to {
  opacity: 0;
  transform: translate3d(0, -6px, 0);
}

@media (prefers-reduced-motion: reduce) {
  .page-route-enter-active,
  .page-route-leave-active {
    transition: none;
  }
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
  .patient-topbar, .navbar-left, .navbar-right-actors, .dentist-room-modal { display: none; }
  .main-content { max-width: none; padding: 0; }
}

.staff-topbar-room {
  display: flex;
  align-items: center;
  min-width: 0;
  max-width: 21rem;
}

.staff-room-pill {
  display: inline-flex;
  align-items: center;
  max-width: 100%;
  min-width: 0;
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

.staff-room-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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

.dentist-room-modal::backdrop {
  background: rgba(15, 23, 42, 0.58);
  backdrop-filter: blur(4px);
}

.dentist-room-modal {
  position: fixed;
  inset: 0;
  margin: auto;
  width: min(620px, calc(100% - 2rem));
  background: #ffffff;
  border-radius: 16px;
  border: 1px solid #e2e8f0;
  box-shadow: 0 24px 48px rgba(15, 45, 89, 0.22);
  padding: 1.5rem;
  max-height: calc(100vh - 2rem);
  max-height: calc(100dvh - 2rem);
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
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr));
  gap: 0.75rem;
  min-width: 0;
  margin: 0 0 1.25rem;
  padding: 0;
  border: 0;
}

.workspace-empty {
  grid-column: 1 / -1;
  margin: 0;
  color: #475569;
  font-size: 0.9rem;
}

.staff-logout-error {
  margin: 0;
  padding: 0.75rem 1rem;
  color: #991b1b;
  background: #fef2f2;
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
  color: #0f2d59;
  font-size: 0.92rem;
  font-weight: 700;
  overflow-wrap: anywhere;
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

@media (max-width: 640px) {
  .patient-topbar-inner.staff-topbar-inner {
    flex-wrap: wrap;
    gap: 0.4rem;
    padding-block: 0.4rem;
  }

  .staff-topbar-inner .patient-brand-logo {
    width: clamp(100px, 35vw, 158px);
  }

  .staff-topbar-inner .staff-role-pill {
    display: none;
  }

  .staff-topbar-room {
    flex: 1 0 100%;
    max-width: 100%;
    order: 1;
  }

  .dentist-room-modal {
    padding: 1rem;
  }
}
</style>
