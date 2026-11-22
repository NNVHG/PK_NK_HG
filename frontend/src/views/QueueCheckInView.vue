<template>
  <main v-if="canCheckIn" class="checkin-page">
    <!-- Header tiếp đón thực chiến -->
    <header class="checkin-header">
      <div class="header-content">
        <div class="header-badge-row">
          <span class="app-badge app-badge--info">Quầy Lễ Tân · Tiếp Đón</span>
          <span class="header-date">📅 {{ todayLocal() }}</span>
        </div>
        <h1 class="checkin-title">Tiếp Đón &amp; Check-In Bệnh Nhân Tại Quầy</h1>
        <p class="checkin-desc">
          Đối chiếu CCCD/hộ chiếu (hoặc người giám hộ với trẻ dưới 14 tuổi), kiểm tra tiền sử công nợ và lịch hẹn đã đăng ký để cấp số thứ tự vào hàng đợi khám.
        </p>
      </div>

      <div class="header-actions">
        <button class="app-btn app-btn--secondary" :disabled="deskLoading || waitingLoading" @click="refreshToday">
          <span :class="{ 'spin-icon': deskLoading || waitingLoading }">🔄</span>
          <span>Làm mới danh sách</span>
        </button>
        <RouterLink class="app-btn app-btn--primary" to="/management/queue/today">
          <span>📋 Mở hàng đợi gọi số →</span>
        </RouterLink>
      </div>
    </header>

    <!-- Bố cục 2 cột vận hành chuẩn Modern Web Guidance -->
    <div class="checkin-grid">
      <!-- CỘT 1: TÌM KIẾM HỒ SƠ & ĐỐI CHIẾU XÁC NHẬN (BÊN TRÁI) -->
      <div class="checkin-col-left">
        <!-- Card 1: Tra cứu bệnh nhân nhanh -->
        <article class="app-card">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">🔍</span>
              <div>
                <h2 class="app-card-title">Tra Cứu Hồ Sơ Bệnh Nhân</h2>
                <p class="app-card-subtitle">Nhập Họ tên, Số điện thoại, Số CCCD hoặc quét mã QR trên thẻ</p>
              </div>
            </div>
          </div>

          <div class="app-card-body">
            <form class="search-form" @submit.prevent="searchPatients(1)">
              <div class="search-input-wrap">
                <input
                  id="patient-search"
                  v-model.trim="keyword"
                  class="app-input search-input"
                  :disabled="checkingIn"
                  autocomplete="off"
                  autocorrect="off"
                  enterkeyhint="search"
                  maxlength="1024"
                  placeholder="Nhập tên, số điện thoại, CCCD hoặc quét mã..."
                />
                <button
                  type="submit"
                  class="app-btn app-btn--medical search-submit-btn"
                  :disabled="searching || checkingIn || !keyword"
                >
                  {{ searching ? 'Đang tìm…' : 'Tìm kiếm' }}
                </button>
                <button
                  type="button"
                  class="app-btn app-btn--primary qr-scan-btn"
                  :disabled="checkingIn || isChild"
                  @click="openScanner"
                  title="Quét CCCD bằng Camera trực tiếp hoặc Máy quét USB"
                >
                  <span>📷 Quét CCCD</span>
                </button>
              </div>
            </form>

            <p v-if="searching" class="app-hint" role="status">Đang tìm hồ sơ trong hệ thống…</p>
            <p v-if="searchError" class="error-banner" role="alert">{{ searchError }}</p>

            <!-- Danh sách kết quả tra cứu -->
            <div v-if="patients.length" class="patient-results-list" aria-label="Kết quả tìm bệnh nhân">
              <button
                v-for="patient in patients"
                :key="patient.patientId"
                type="button"
                class="patient-result-card"
                :class="{ 'patient-result-card--selected': selectedPatient?.patientId === patient.patientId }"
                :aria-pressed="selectedPatient?.patientId === patient.patientId"
                :disabled="checkingIn || selecting"
                @click="selectPatient(patient)"
              >
                <div class="patient-result-avatar">
                  {{ patient.fullName.charAt(0) }}
                </div>
                <div class="patient-result-info">
                  <div class="desk-patient-name-row">
                    <strong>{{ patient.fullName }}</strong>
                    <span v-if="patient.isUnderFourteen ?? isUnderFourteen(patient.dateOfBirth)" class="app-badge app-badge--info">👶 Trẻ &lt;14</span>
                    <span v-if="patient.missingGuardianAccount" class="app-badge app-badge--warning">⚠️ Thiếu tài khoản</span>
                  </div>
                  <span>Mã: {{ patient.patientCode }} · SĐT: {{ patient.phone }}</span>
                </div>
                <div class="patient-result-status">
                  <span v-if="selectedPatient?.patientId === patient.patientId" class="app-badge app-badge--success">✓ Đã chọn</span>
                  <span v-else class="select-hint">Chọn tiếp đón →</span>
                </div>
              </button>
            </div>

            <div v-else-if="searched && !searching && !searchError" class="empty-search-box">
              <p>Không tìm thấy hồ sơ phù hợp với từ khóa "{{ keyword }}".</p>
              <RouterLink class="app-btn app-btn--secondary app-btn--sm" to="/management/patients">
                + Tạo hồ sơ bệnh nhân mới
              </RouterLink>
            </div>

            <nav v-if="searched && !searchError && searchTotal > 0" class="pagination-bar" aria-label="Phân trang kết quả tìm">
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="searching || searchPage <= 1" @click="searchPatients(searchPage - 1)">Trước</button>
              <span class="pagination-text">Trang {{ searchPage }} · {{ searchTotal }} hồ sơ</span>
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="searching || !searchNext" @click="searchPatients(searchPage + 1)">Sau</button>
            </nav>

            <p v-if="selecting" class="app-hint" role="status">Đang tải chi tiết hồ sơ đã chọn…</p>
          </div>
        </article>

        <!-- Card 2: Biểu mẫu đối chiếu & xác nhận tiếp đón (Hiển thị khi đã chọn bệnh nhân) -->
        <article v-if="selectedPatient" class="app-card checkin-form-card">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">📋</span>
              <div>
                <h2 class="app-card-title">{{ isChild ? 'Tiếp Đón Trẻ Em' : 'Đối Chiếu Giấy Tờ' }}: {{ selectedPatient.fullName }}</h2>
                <p class="app-card-subtitle">
                  Mã hồ sơ: <strong>{{ selectedPatient.patientCode }}</strong>
                  · Ngày sinh: <strong>{{ selectedPatient.dateOfBirth }}</strong>
                  <span v-if="isChild" class="app-badge app-badge--info" style="margin-left: 0.4rem;">👶 Trẻ dưới 14 tuổi</span>
                </p>
              </div>
            </div>
          </div>

          <div class="app-card-body">
            <!-- Trẻ dưới 14 tuổi không dùng giấy tờ định danh riêng. -->
            <template v-if="!isChild">
            <div class="app-form-group">
              <label class="app-label" for="checkin-document-type">
                <span>Loại giấy tờ định danh</span>
                <span class="app-label-required">*</span>
              </label>
              <select id="checkin-document-type" v-model="documentType" class="app-select" @change="onDocumentTypeChanged">
                <option value="citizenId">Căn cước công dân (CCCD)</option>
                <option value="passport">Hộ chiếu</option>
              </select>
            </div>

            <!-- Số giấy tờ -->
            <div class="app-form-group">
              <div class="label-with-action">
                <label class="app-label" for="checkin-citizen-id">
                  <span>{{ documentType === 'passport' ? 'Số hộ chiếu người bệnh' : 'Số CCCD người bệnh (12 chữ số)' }}</span>
                  <span class="app-label-required">*</span>
                </label>
                <button v-if="documentType === 'citizenId'" type="button" class="text-link-btn" @click="openScanner">
                  📷 Quét QR để điền
                </button>
              </div>
              <input
                id="checkin-citizen-id"
                v-model="citizenId"
                class="app-input"
                :aria-invalid="!!fieldErrors.citizenId"
                aria-describedby="citizen-error"
                :inputmode="documentType === 'passport' ? 'text' : 'numeric'"
                autocomplete="off"
                maxlength="12"
                :placeholder="documentType === 'passport' ? 'Nhập số hộ chiếu 6–12 ký tự' : 'Nhập đúng 12 chữ số trên thẻ CCCD'"
              />
              <p v-if="fieldErrors.citizenId" id="citizen-error" class="error-msg">{{ fieldErrors.citizenId }}</p>
            </div>

            <!-- Quốc gia cấp nếu là hộ chiếu -->
            <div v-if="documentType === 'passport'" class="app-form-group">
              <label class="app-label" for="checkin-country">
                <span>Mã quốc gia cấp hộ chiếu</span>
                <span class="app-label-required">*</span>
              </label>
              <input
                id="checkin-country"
                v-model="nationalityCode"
                class="app-input"
                :aria-invalid="!!fieldErrors.nationalityCode"
                aria-describedby="country-error"
                autocomplete="off"
                maxlength="3"
                placeholder="Ví dụ: VNM, USA, JPN"
              />
              <p v-if="fieldErrors.nationalityCode" id="country-error" class="error-msg">{{ fieldErrors.nationalityCode }}</p>
            </div>
            </template>

            <section v-else class="guardian-check-section" aria-labelledby="guardian-check-title">
              <div class="child-badge-notice">
                <h3 id="guardian-check-title">Trẻ dưới 14 tuổi · Không yêu cầu CCCD riêng</h3>
                <p>Đối chiếu người giám hộ trực tiếp tại quầy. Số điện thoại trùng không tự tạo tài khoản hoặc quyền xem hồ sơ.</p>
              </div>
              <p v-if="guardianLoading" class="app-hint" role="status">Đang kiểm tra liên kết giám hộ…</p>
              <p v-else-if="guardianLookupFailed" class="error-banner" role="alert">
                Không kiểm tra được liên kết giám hộ. Tải lại hồ sơ trước khi tiếp đón.
                <button type="button" class="app-btn app-btn--secondary app-btn--sm" @click="loadActiveGuardian(selectedPatient)">Thử lại</button>
              </p>
              <div v-else-if="linkedGuardian" class="info-pill-note" role="status">
                Đã liên kết: <strong>{{ linkedGuardian.guardianFullName }}</strong> · {{ linkedGuardian.relationship }}.
                Vẫn cần xác minh người giám hộ có mặt tại quầy.
                <p v-if="guardianLinkSuccess">{{ guardianLinkSuccess }}</p>
              </div>
              <div v-else class="warning-callout" role="status">
                <strong>Chưa có liên kết giám hộ hiệu lực</strong>
                <p>Hồ sơ trẻ vẫn được tiếp đón sau khi xác minh người đi cùng. Không cấp quyền chỉ vì số điện thoại trùng.</p>
                <RouterLink :to="`/management/patients/${selectedPatient.patientId}`" target="_blank">Mở hồ sơ trẻ để quản lý liên kết giám hộ</RouterLink>
              </div>

              <div class="app-form-group">
                <label class="app-label" for="guardian-full-name">Họ tên người giám hộ <span class="app-label-required">*</span></label>
                <input id="guardian-full-name" v-model.trim="guardianFullName" class="app-input" maxlength="100" autocomplete="off" />
                <p v-if="fieldErrors.guardian" class="error-msg">{{ fieldErrors.guardian }}</p>
              </div>
              <div class="app-form-group">
                <label class="app-label" for="guardian-relationship">Quan hệ với trẻ <span class="app-label-required">*</span></label>
                <select id="guardian-relationship" v-model="guardianRelationship" class="app-select">
                  <option value="">Chọn quan hệ</option>
                  <option v-for="rel in GUARDIAN_RELATIONSHIP_OPTIONS" :key="rel" :value="rel">{{ rel }}</option>
                  <option v-if="guardianRelationship && !GUARDIAN_RELATIONSHIP_OPTIONS.includes(guardianRelationship)" :value="guardianRelationship">{{ guardianRelationship }}</option>
                </select>
                <p v-if="fieldErrors.guardian" class="error-msg">{{ fieldErrors.guardian }}</p>
              </div>
              <label class="guardian-verification-label">
                <input v-model="guardianVerifiedAtCounter" type="checkbox" />
                Tôi đã đối chiếu người giám hộ trực tiếp tại quầy.
              </label>
              <p v-if="fieldErrors.guardianVerifiedAtCounter" class="error-msg">{{ fieldErrors.guardianVerifiedAtCounter }}</p>
            </section>

            <!-- Số điện thoại -->
            <div class="app-form-group">
              <label class="app-label" for="checkin-phone">
                <span>{{ isChild ? 'Số điện thoại liên hệ của người giám hộ' : 'Số điện thoại đăng ký trong hồ sơ' }}</span>
                <span class="app-label-required">*</span>
              </label>
              <input
                id="checkin-phone"
                v-model="checkInPhone"
                class="app-input"
                :aria-invalid="!!fieldErrors.phone"
                aria-describedby="phone-error"
                inputmode="tel"
                autocomplete="tel"
                maxlength="10"
                placeholder="Ví dụ: 0912345678"
              />
              <p v-if="fieldErrors.phone" id="phone-error" class="error-msg">{{ fieldErrors.phone }}</p>
              <span class="app-hint">{{ isChild ? 'Chỉ đối chiếu số liên hệ đã lưu; số điện thoại không tự cấp quyền giám hộ.' : 'Hệ thống đối chiếu bảo mật tại quầy, không lưu bản rõ số giấy tờ trong nhật ký.' }}</span>
            </div>

            <div v-if="isChild && !linkedGuardian && !guardianLookupFailed" class="guardian-account-lookup">
              <button type="button" class="app-btn app-btn--secondary app-btn--sm" :disabled="guardianAccountSearchLoading || !/^0[0-9]{9}$/.test(checkInPhone.trim())" @click="searchGuardianAccounts">
                {{ guardianAccountSearchLoading ? 'Đang tra hồ sơ…' : 'Tra hồ sơ người giám hộ theo số điện thoại' }}
              </button>
              <p class="app-hint">Số điện thoại có thể dùng chung. Chỉ nhân viên chọn đúng người và xác nhận tại quầy mới tạo liên kết; hệ thống không tự liên kết theo số điện thoại.</p>
              <p v-if="guardianAccountSearchError" class="error-msg" role="alert">{{ guardianAccountSearchError }}</p>
              <p v-if="guardianAccountSearchComplete && guardianCandidates.length === 0" class="app-hint" role="status">
                Không tìm thấy hồ sơ theo số này. Có thể tiếp đón trẻ sau khi xác minh người đi cùng; không tự tạo tài khoản hoặc quyền xem hồ sơ.
              </p>
              <template v-if="guardianCandidates.length > 0">
                <label class="app-label" for="guardian-account-select">Chọn đúng hồ sơ người giám hộ đang có mặt</label>
                <select id="guardian-account-select" v-model.number="selectedGuardianPatientId" class="app-select" @change="selectGuardianAccount">
                  <option :value="null">Chọn hồ sơ đã đối chiếu</option>
                  <option
                    v-for="candidate in guardianCandidates"
                    :key="candidate.patientId"
                    :value="candidate.patientId"
                    :disabled="candidate.hasUserAccount === false"
                  >
                    {{ candidate.fullName }} · {{ candidate.patientCode }} · {{ candidate.dateOfBirth }}{{ candidate.hasUserAccount === false ? ' — Chưa có tài khoản' : candidate.hasUserAccount === true ? ' — Có tài khoản' : '' }}
                  </option>
                </select>
                <p v-if="guardianAccountSearchHasMore" class="app-hint">Có thêm hồ sơ cùng số điện thoại. Nếu chưa thấy đúng người, hãy tra cứu trong hồ sơ bệnh nhân trước khi xác nhận.</p>
                <button type="button" class="app-btn app-btn--primary app-btn--sm" :disabled="guardianLinkSaving || selectedGuardianPatientId === null || !guardianVerifiedAtCounter || !guardianFullName.trim() || !guardianRelationship.trim() || guardianSearchPhone !== checkInPhone.trim()" @click="confirmGuardianLink">
                  {{ guardianLinkSaving ? 'Đang tạo liên kết…' : 'Xác nhận liên kết người giám hộ' }}
                </button>
              </template>
              <p v-if="guardianLinkError" class="error-msg" role="alert">{{ guardianLinkError }}</p>
              <p v-if="guardianLinkSuccess" class="info-pill-note" role="status">{{ guardianLinkSuccess }}</p>
            </div>

            <!-- Khối cảnh báo công nợ nếu có -->
            <div v-if="debtLoading" class="app-hint" role="status">Đang đối soát công nợ cũ…</div>
            <div v-else-if="debtLookupFailed" class="warning-callout" role="alert">
              <p>Không thể tải công nợ cũ. Bạn vẫn có thể tiếp tục tiếp đón nhưng cần kiểm tra hóa đơn sau.</p>
              <button type="button" class="app-btn app-btn--secondary app-btn--sm" @click="loadOutstandingDebt(selectedPatient)">Thử lại</button>
            </div>
            <div v-else-if="outstandingDebt && outstandingDebt.totalOutstandingDebt > 0" class="debt-callout" role="alert">
              <div class="debt-callout-header">
                <span class="debt-icon">⚠️</span>
                <strong>Bệnh nhân còn dư nợ cũ: {{ formatCurrency(outstandingDebt.totalOutstandingDebt) }}</strong>
              </div>
              <p class="debt-sub">Vui lòng nhắc bệnh nhân tất toán các hóa đơn trước khi vào phòng khám:</p>
              <ul class="debt-list">
                <li v-for="invoiceCode in outstandingDebt.invoiceCodes" :key="invoiceCode">
                  Mã hóa đơn: <strong>{{ invoiceCode }}</strong>
                </li>
              </ul>
            </div>

            <!-- Lịch hẹn hôm nay -->
            <div v-if="loadingAppointments" class="app-hint">Đang kiểm tra lịch hẹn hôm nay…</div>
            <div v-else-if="appointmentLookupFailed" class="error-banner" role="alert">
              <p>Không thể kiểm tra lịch hẹn hôm nay. Vui lòng thử lại trước khi check-in.</p>
              <button type="button" class="app-btn app-btn--secondary app-btn--sm" @click="loadAppointments(selectedPatient)">Thử lại</button>
            </div>
            <div v-else-if="appointments.length" class="app-form-group">
              <label class="app-label" for="appointment">
                <span>Lịch hẹn đã đặt hôm nay</span>
              </label>
              <select id="appointment" v-model.number="appointmentId" class="app-select">
                <option v-for="appointment in appointments" :key="appointment.appointmentId" :value="appointment.appointmentId">
                  Khung giờ: {{ appointment.slotTime.slice(0, 5) }} · Trạng thái: {{ appointment.status }}
                </option>
              </select>
              <span class="app-hint">Check-in sẽ gắn lượt chờ với lịch đã chọn và tự động gắn mức độ ưu tiên đúng giờ hẹn.</span>
            </div>
            <div v-else class="info-pill-note">
              <span>ℹ️ Không có lịch hẹn đặt trước hôm nay. Tiếp đón sẽ ghi nhận lượt Khách Vãng Lai.</span>
            </div>

            <!-- Ghi chú tiếp đón -->
            <div class="app-form-group">
              <label class="app-label" for="notes">Ghi chú tiếp đón (Tình trạng ban đầu / yêu cầu đặc biệt)</label>
              <textarea
                id="notes"
                v-model="notes"
                class="app-textarea"
                :aria-invalid="!!fieldErrors.notes"
                aria-describedby="notes-error"
                rows="2"
                maxlength="500"
                placeholder="Ghi chú đau nhức răng, yêu cầu bác sĩ cụ thể hoặc lưu ý sức khỏe..."
              ></textarea>
              <p v-if="fieldErrors.notes" id="notes-error" class="error-msg">{{ fieldErrors.notes }}</p>
            </div>

            <p v-if="checkInError" class="error-banner" role="alert">{{ checkInError }}</p>

            <button
              type="button"
              class="app-btn app-btn--primary app-btn--lg checkin-submit-btn"
              :disabled="checkingIn || loadingAppointments || appointmentLookupFailed || debtLoading || guardianLoading || guardianLookupFailed || guardianLinkSaving"
              @click="submitCheckIn"
            >
              <span v-if="checkingIn" class="spin-icon">⏳</span>
              <span>{{ checkingIn ? 'Đang cấp số thứ tự…' : 'Xác Nhận Khách Đã Đến &amp; Cấp Số Thứ Tự' }}</span>
            </button>
          </div>
        </article>
      </div>

      <!-- CỘT 2: PHIẾU SỐ THỨ TỰ & DANH SÁCH CHỜ HÔM NAY (BÊN PHẢI) -->
      <div class="checkin-col-right">
        <!-- Phiếu Số Thứ Tự (Boarding Pass Ticket) khi vừa check-in xong -->
        <article v-if="ticket" class="ticket-card" role="status" aria-live="polite">
          <div class="ticket-badge-top">
            <span class="app-badge app-badge--success">✓ Tiếp Đón Thành Công</span>
            <span class="ticket-time">{{ todayLocal() }}</span>
          </div>

          <div class="ticket-number-box">
            <span class="ticket-label">SỐ THỨ TỰ KHÁM</span>
            <strong class="ticket-number">{{ ticket.queueNumber }}</strong>
            <span class="ticket-subtext">Hàng Đợi Khám Hôm Nay</span>
          </div>

          <div class="ticket-patient-details">
            <h3 class="ticket-name">{{ ticket.patientName }}</h3>
            <p v-if="ticket.patientAccountCreated" class="app-hint" role="status">
              Đã tạo tài khoản bệnh nhân; người bệnh cần kích hoạt lần đầu bằng OTP và đổi mật khẩu.
            </p>
            <div class="ticket-meta-row">
              <span>Trạng thái: <strong>{{ ticket.statusText }}</strong></span>
              <span class="ticket-priority-pill" :class="{ 'ticket-priority-pill--normal': !ticket.isPriority }">
                {{ ticket.isPriority ? '★ Có hẹn trước' : 'Khách vãng lai' }}
              </span>
            </div>
          </div>

          <div class="ticket-instructions">
            <p>Mời người bệnh ngồi tại sảnh chờ, hệ thống âm thanh &amp; màn hình TV sẽ đọc tên và hướng dẫn vào phòng khám tương ứng.</p>
          </div>

          <button type="button" class="app-btn app-btn--secondary app-btn--sm" @click="resetForm">
            + Tiếp đón bệnh nhân tiếp theo
          </button>
        </article>

        <!-- Card: Lịch hẹn chờ tiếp đón hôm nay -->
        <article class="app-card">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">📅</span>
              <div>
                <h2 class="app-card-title">Lịch Hẹn Chờ Đến ({{ deskTotal }})</h2>
                <p class="app-card-subtitle">Bệnh nhân đã đặt lịch trước trong ngày</p>
              </div>
            </div>
          </div>

          <div class="app-card-body">
            <p v-if="deskLoading" class="app-hint" role="status">Đang tải lịch hẹn…</p>
            <p v-else-if="deskError" class="error-banner" role="alert">{{ deskError }} <button class="app-btn app-btn--secondary app-btn--sm" @click="loadDesk(deskPage)">Thử lại</button></p>
            <template v-else>
              <p v-if="!deskRows.length" class="app-hint">Không còn lịch hẹn nào chờ tiếp đón trong ngày.</p>
              <div v-else class="desk-appointment-list">
                <div v-for="row in deskRows" :key="row.appointmentId" class="desk-appointment-item">
                  <div class="desk-time-box">
                    <strong>{{ row.slotTime.slice(0, 5) }}</strong>
                    <span>Khung giờ</span>
                  </div>
                  <div class="desk-patient-info">
                    <div class="desk-patient-name-row">
                      <strong>{{ row.patientName }}</strong>
                      <span v-if="row.isChildPatient || (row.patientDateOfBirth && isUnderFourteen(row.patientDateOfBirth))" class="app-badge app-badge--info">👶 Trẻ &lt;14</span>
                      <span v-if="row.missingGuardianAccount" class="app-badge app-badge--warning">⚠️ Thiếu tài khoản</span>
                    </div>
                    <span>Mã: {{ row.patientCode }}</span>
                  </div>
                  <button
                    type="button"
                    class="app-btn app-btn--medical app-btn--sm"
                    :disabled="checkingIn || selecting"
                    @click="selectFromDesk(row)"
                  >
                    Check-in
                  </button>
                </div>
              </div>
            </template>

            <nav v-if="deskTotal > 0" class="pagination-bar" aria-label="Phân trang lịch hẹn">
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="deskLoading || deskPage <= 1" @click="loadDesk(deskPage - 1)">Trước</button>
              <span class="pagination-text">Trang {{ deskPage }} / {{ Math.ceil(deskTotal / 10) || 1 }}</span>
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="deskLoading || !deskNext" @click="loadDesk(deskPage + 1)">Sau</button>
            </nav>
          </div>
        </article>

        <!-- Card: Bệnh nhân đã tiếp đón & đang chờ khám -->
        <article class="app-card">
          <div class="app-card-header">
            <div class="app-card-title-group">
              <span class="app-card-icon">👥</span>
              <div>
                <h2 class="app-card-title">Đang Chờ Khám Tại Ghế ({{ waitingTotal }})</h2>
                <p class="app-card-subtitle">Bệnh nhân đã qua quầy và đang ngồi chờ</p>
              </div>
            </div>
            <RouterLink class="app-btn app-btn--secondary app-btn--sm" to="/management/queue/today">
              Gọi số →
            </RouterLink>
          </div>

          <div class="app-card-body">
            <p v-if="waitingLoading" class="app-hint" role="status">Đang tải danh sách chờ…</p>
            <p v-else-if="waitingError" class="error-banner" role="alert">{{ waitingError }} <button class="app-btn app-btn--secondary app-btn--sm" @click="loadWaiting(waitingPage)">Thử lại</button></p>
            <template v-else>
              <p v-if="!waitingRows.length" class="app-hint">Hiện không có bệnh nhân nào đang chờ tại ghế.</p>
              <div v-else class="waiting-queue-list">
                <div v-for="row in waitingRows" :key="row.queueEntryId" class="waiting-queue-item">
                  <div class="waiting-number-pill">
                    {{ String(row.queueNumber).padStart(3, '0') }}
                  </div>
                  <div class="waiting-item-info">
                    <div class="desk-patient-name-row">
                      <strong>{{ row.patientName }}</strong>
                      <span v-if="row.isChildPatient || (row.patientDateOfBirth && isUnderFourteen(row.patientDateOfBirth))" class="app-badge app-badge--info">👶 Trẻ &lt;14</span>
                      <span v-if="row.missingGuardianAccount" class="app-badge app-badge--warning">⚠️ Thiếu tài khoản</span>
                    </div>
                    <span>{{ row.patientCode }} · {{ row.statusText }}</span>
                  </div>
                  <span class="app-badge app-badge--info">Đang chờ</span>
                </div>
              </div>
            </template>

            <nav v-if="waitingTotal > 0" class="pagination-bar" aria-label="Phân trang hàng chờ">
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="waitingLoading || waitingPage <= 1" @click="loadWaiting(waitingPage - 1)">Trước</button>
              <span class="pagination-text">Trang {{ waitingPage }} / {{ Math.ceil(waitingTotal / 10) || 1 }}</span>
              <button class="app-btn app-btn--secondary app-btn--sm" :disabled="waitingLoading || !waitingNext" @click="loadWaiting(waitingPage + 1)">Sau</button>
            </nav>
          </div>
        </article>
      </div>
    </div>

    <!-- Modal Quét CCCD thống nhất (Camera & Máy quét USB) -->
    <CameraCccdScanner
      :is-open="isCameraScannerOpen"
      @close="isCameraScannerOpen = false"
      @success="handleCccdScanSuccess"
    />
  </main>

  <div v-else class="access-denied-box" role="alert">
    <h2>Không có quyền truy cập</h2>
    <p>Chức năng tiếp đón và check-in chỉ dành cho nhân sự Lễ tân và Quản trị viên.</p>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import axios from 'axios';
import { useAuthStore } from '@/stores/auth';
import CameraCccdScanner from '@/components/CameraCccdScanner.vue';
import { decodeCitizenQr, qrPatientIntakeService, type ScannedIdentity } from '@/services/qrPatientIntake';
import { validateCheckIn, checkInProblem, type CheckInErrors } from '@/services/checkInForm';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { patientsService, type PatientSummary } from '@/services/patients';
import { patientGuardiansService, type PatientGuardianSummary } from '@/services/patientGuardians';
import { queueService, type OutstandingDebtResponse, type QueueEntryResponse } from '@/services/queue';
import { isUnderFourteen } from '@/services/patientAge';

const GUARDIAN_RELATIONSHIP_OPTIONS = ['Bố/Mẹ', 'Cha', 'Mẹ', 'Ông/Bà', 'Người giám hộ hợp pháp', 'Khác'];
const auth = useAuthStore();
const canCheckIn = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(auth.role ?? ''));
const keyword = ref('');
const patients = ref<PatientSummary[]>([]);
const selectedPatient = ref<PatientSummary | null>(null);
const isChild = computed(() => selectedPatient.value ? isUnderFourteen(selectedPatient.value.dateOfBirth) : false);
const appointments = ref<AppointmentResponse[]>([]);
const appointmentId = ref<number | null>(null);
const notes = ref('');
const citizenId = ref('');
const documentType = ref<'citizenId' | 'passport'>('citizenId');
const nationalityCode = ref('');
const checkInPhone = ref('');
const guardianFullName = ref('');
const guardianRelationship = ref('');
const guardianVerifiedAtCounter = ref(false);
const linkedGuardian = ref<PatientGuardianSummary | null>(null);
const guardianLoading = ref(false);
const guardianLookupFailed = ref(false);
const guardianCandidates = ref<PatientSummary[]>([]);
const selectedGuardianPatientId = ref<number | null>(null);
const guardianAccountSearchLoading = ref(false);
const guardianAccountSearchComplete = ref(false);
const guardianAccountSearchHasMore = ref(false);
const guardianAccountSearchError = ref('');
const guardianSearchPhone = ref('');
const guardianLinkSaving = ref(false);
const guardianLinkError = ref('');
const guardianLinkSuccess = ref('');
let guardianRequestSequence = 0;
let guardianAccountSearchSequence = 0;
const searching = ref(false);
const searched = ref(false);
const loadingAppointments = ref(false);
const appointmentLookupFailed = ref(false);
const checkingIn = ref(false);
const searchError = ref('');
const checkInError = ref('');
const fieldErrors = ref<CheckInErrors>({});
const ticket = ref<QueueEntryResponse | null>(null);
const outstandingDebt = ref<OutstandingDebtResponse | null>(null);
const debtLoading = ref(false);
const debtLookupFailed = ref(false);
let debtRequestSequence = 0;

const searchPage = ref(1), searchTotal = ref(0), searchNext = ref(false);
const deskRows = ref<AppointmentResponse[]>([]), deskPage = ref(1), deskTotal = ref(0), deskNext = ref(false);
const deskLoading = ref(false), deskError = ref('');
const waitingRows = ref<QueueEntryResponse[]>([]), waitingPage = ref(1), waitingTotal = ref(0), waitingNext = ref(false);
const waitingLoading = ref(false), waitingError = ref(''), selecting = ref(false);
let searchSequence = 0, deskSequence = 0, waitingSequence = 0, selectionSequence = 0, appointmentSequence = 0;
let selectionOwnerSequence = 0;
let searchTimer: ReturnType<typeof setTimeout> | undefined;
let disposed = false;

const isCameraScannerOpen = ref(false);
let qrScanGeneration = 0;

async function loadDesk(page = 1) {
  if (!canCheckIn.value || disposed || page < 1) return;
  const sequence = ++deskSequence;
  deskLoading.value = true; deskError.value = ''; deskRows.value = []; deskTotal.value = 0; deskNext.value = false; deskPage.value = page;
  try {
    const result = await appointmentsService.getForDesk(todayLocal(), page, 'Scheduled');
    if (sequence !== deskSequence || disposed) return;
    deskRows.value = result.items; deskTotal.value = result.totalCount; deskNext.value = result.hasNextPage;
  } catch { if (sequence === deskSequence) deskError.value = 'Không tải được lịch hẹn hôm nay.'; }
  finally { if (sequence === deskSequence) deskLoading.value = false; }
}
async function loadWaiting(page = 1) {
  if (!canCheckIn.value || disposed || page < 1) return;
  const sequence = ++waitingSequence;
  waitingLoading.value = true; waitingError.value = ''; waitingRows.value = []; waitingTotal.value = 0; waitingNext.value = false; waitingPage.value = page;
  try {
    const result = await queueService.getToday(page, 1);
    if (sequence !== waitingSequence || disposed) return;
    waitingRows.value = result.items; waitingTotal.value = result.totalCount; waitingNext.value = result.hasNextPage;
  } catch { if (sequence === waitingSequence) waitingError.value = 'Không tải được hàng chờ hôm nay.'; }
  finally { if (sequence === waitingSequence) waitingLoading.value = false; }
}
async function refreshToday() { await Promise.all([loadDesk(), loadWaiting()]); }
async function selectFromDesk(row: AppointmentResponse) {
  if (!canCheckIn.value || checkingIn.value || selecting.value) return;
  const owner = ++selectionOwnerSequence;
  const sequence = ++selectionSequence;
  searchSequence++;
  searching.value = false;
  selecting.value = true; searchError.value = '';
  try {
    const patient = await patientsService.getById(row.patientId);
    if (owner !== selectionOwnerSequence || sequence !== selectionSequence || disposed) return;
    await selectPatient(patient, '', owner);
    if (owner === selectionOwnerSequence && sequence === selectionSequence && !disposed) {
      if (appointments.value.some(a => a.appointmentId === row.appointmentId)) appointmentId.value = row.appointmentId;
      applyGuardianHintFromNotes(row.notes);
    }
  } catch { if (owner === selectionOwnerSequence && sequence === selectionSequence) searchError.value = 'Không tải được hồ sơ. Vui lòng thử lại.'; }
  finally { if (owner === selectionOwnerSequence) selecting.value = false; }
}
function clearSelection() {
  closeQr();
  guardianRequestSequence++;
  appointmentSequence++; debtRequestSequence++; selectionSequence++; selectionOwnerSequence++;
  selecting.value = false; selectedPatient.value = null; citizenId.value = ''; documentType.value = 'citizenId'; nationalityCode.value = ''; checkInPhone.value = ''; notes.value = '';
  guardianFullName.value = ''; guardianRelationship.value = ''; guardianVerifiedAtCounter.value = false; linkedGuardian.value = null; guardianLoading.value = false; guardianLookupFailed.value = false;
  resetGuardianAccountSearch();
  appointments.value = []; appointmentId.value = null; loadingAppointments.value = false; appointmentLookupFailed.value = false;
  outstandingDebt.value = null; debtLoading.value = false; debtLookupFailed.value = false; fieldErrors.value = {}; checkInError.value = ''; ticket.value = null;
}
async function searchPatients(page = 1) {
  clearTimeout(searchTimer);
  if (!canCheckIn.value || checkingIn.value || disposed || page < 1) return;
  clearSelection();
  const sequence = ++searchSequence, owner = ++selectionOwnerSequence, value = keyword.value.trim();
  patients.value = []; searched.value = false; searchError.value = ''; searchTotal.value = 0; searchNext.value = false; searchPage.value = page;
  if (!value) { searching.value = false; return; }
  searching.value = true;
  try {
    if (value.includes('|')) {
      let scannedCitizenId: string;
      try {
        scannedCitizenId = (await decodeCitizenQr(value)).citizenId;
      } catch {
        if (owner === selectionOwnerSequence && sequence === searchSequence && !disposed) {
          searchError.value = 'Mã QR CCCD không hợp lệ. Vui lòng quét lại hoặc nhập CCCD 12 chữ số.';
        }
        return;
      }
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      const lookup = await qrPatientIntakeService.lookup(scannedCitizenId);
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      const patient = lookup.existing?.isActive ? await patientsService.getById(lookup.existing.patientId) : null;
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      if (patient) {
        patients.value = [patient];
        searchTotal.value = 1;
        searchPage.value = 1;
        searched.value = true;
        const id = value.split('|')[0].trim();
        await selectPatient(patient, id, owner);
        return;
      } else {
        searchError.value = 'Chưa có hồ sơ cho CCCD này. Mở hồ sơ bệnh nhân để tạo mới.';
        searched.value = true;
        return;
      }
    }
    if (/^[0-9]{12}$/.test(value)) {
      const lookup = await qrPatientIntakeService.lookup(value);
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      const patient = lookup.existing?.isActive ? await patientsService.getById(lookup.existing.patientId) : null;
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      patients.value = patient ? [patient] : []; searchTotal.value = patients.value.length; searchPage.value = 1;
    } else {
      const result = await patientsService.search({ keyword: value, page, pageSize: 20 });
      if (owner !== selectionOwnerSequence || sequence !== searchSequence || disposed) return;
      patients.value = result.items; searchTotal.value = result.totalCount; searchNext.value = result.hasNextPage;
    }
    searched.value = true;
  } catch { if (owner === selectionOwnerSequence && sequence === searchSequence) searchError.value = 'Không thể tìm hồ sơ bệnh nhân. Vui lòng thử lại; tìm CCCD cần HTTPS hoặc localhost.'; }
  finally { if (owner === selectionOwnerSequence && sequence === searchSequence) searching.value = false; }
}
watch(keyword, () => {
  clearTimeout(searchTimer); searchSequence++; clearSelection(); patients.value = []; searched.value = false; searchError.value = ''; searching.value = false;
  if (keyword.value.trim() && canCheckIn.value) searchTimer = setTimeout(() => { void searchPatients(1); }, 350);
});
watch(() => [auth.user?.userId, auth.role], () => {
  qrScanGeneration++;
  closeQr();
  clearTimeout(searchTimer);
  searchSequence++;
  deskSequence++;
  waitingSequence++;
  clearSelection();
  keyword.value = '';
  deskRows.value = [];
  waitingRows.value = [];
  patients.value = [];
}, { flush: 'sync' });
watch(checkInPhone, resetGuardianAccountSearch);
onMounted(refreshToday);

function parseGuardianNote(rawNotes?: string | null): { name: string; relationship: string } | null {
  if (!rawNotes) return null;
  const match = rawNotes.match(/\[Giám hộ:\s*([^\]]+)\]/);
  if (!match) return null;
  const content = match[1].trim();
  if (!content) return null;
  const sepIdx = content.lastIndexOf(' - ');
  if (sepIdx > 0) {
    return { name: content.slice(0, sepIdx).trim(), relationship: content.slice(sepIdx + 3).trim() };
  }
  return { name: content, relationship: '' };
}

function applyGuardianHintFromNotes(rawNotes?: string | null) {
  if (!isChild.value || linkedGuardian.value || guardianFullName.value) return;
  const parsed = parseGuardianNote(rawNotes);
  if (!parsed) return;
  guardianFullName.value = parsed.name;
  if (parsed.relationship && !guardianRelationship.value) {
    guardianRelationship.value = parsed.relationship;
  }
  guardianVerifiedAtCounter.value = false;
}

async function selectPatient(patient: PatientSummary, scannedCitizenId = '', parentOwner?: number) {
  if (!canCheckIn.value || checkingIn.value || disposed) return;
  if (parentOwner === undefined) {
    selectionOwnerSequence++;
    searchSequence++;
  } else if (parentOwner !== selectionOwnerSequence) {
    return;
  }
  const owner = parentOwner ?? selectionOwnerSequence;
  closeQr();
  notes.value = '';
  if (isUnderFourteen(patient.dateOfBirth) && scannedCitizenId) {
    searchError.value = 'Trẻ dưới 14 tuổi không dùng CCCD riêng. Vui lòng xác minh người giám hộ tại quầy.';
    scannedCitizenId = '';
  }
  selectedPatient.value = patient;
  citizenId.value = scannedCitizenId;
  documentType.value = 'citizenId';
  nationalityCode.value = '';
  fieldErrors.value = {};
  guardianFullName.value = ''; guardianRelationship.value = ''; guardianVerifiedAtCounter.value = false;
  linkedGuardian.value = null; guardianLookupFailed.value = false;
  resetGuardianAccountSearch();
  checkInPhone.value = scannedCitizenId || isUnderFourteen(patient.dateOfBirth) ? patient.phone : '';
  await Promise.all([loadAppointments(patient), loadOutstandingDebt(patient), loadActiveGuardian(patient)]);
  if (owner !== selectionOwnerSequence || selectedPatient.value?.patientId !== patient.patientId || disposed) return;
  if (isUnderFourteen(patient.dateOfBirth) && !linkedGuardian.value && !guardianFullName.value) {
    const targetAppointment = appointments.value.find(a => a.appointmentId === appointmentId.value) ?? appointments.value[0];
    applyGuardianHintFromNotes(targetAppointment?.notes);
  }
}

async function loadActiveGuardian(patient: PatientSummary | null) {
  const sequence = ++guardianRequestSequence;
  linkedGuardian.value = null;
  guardianLookupFailed.value = false;
  guardianFullName.value = '';
  guardianRelationship.value = '';
  if (!patient || !isUnderFourteen(patient.dateOfBirth)) {
    guardianLoading.value = false;
    return;
  }

  guardianLoading.value = true;
  try {
    const guardian = await patientGuardiansService.getActiveGuardian(patient.patientId);
    if (sequence !== guardianRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    linkedGuardian.value = guardian;
    guardianFullName.value = guardian.guardianFullName;
    guardianRelationship.value = guardian.relationship;
  } catch (error) {
    if (sequence !== guardianRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    if (!axios.isAxiosError(error) || error.response?.status !== 404) guardianLookupFailed.value = true;
  } finally {
    if (sequence === guardianRequestSequence) guardianLoading.value = false;
  }
}

function resetGuardianAccountSearch() {
  guardianAccountSearchSequence++;
  guardianCandidates.value = [];
  selectedGuardianPatientId.value = null;
  guardianAccountSearchLoading.value = false;
  guardianAccountSearchComplete.value = false;
  guardianAccountSearchHasMore.value = false;
  guardianAccountSearchError.value = '';
  guardianSearchPhone.value = '';
  guardianLinkError.value = '';
  guardianLinkSuccess.value = '';
}

async function searchGuardianAccounts() {
  const patient = selectedPatient.value;
  const phone = checkInPhone.value.trim();
  if (!canCheckIn.value || !patient || !isUnderFourteen(patient.dateOfBirth) || !/^0[0-9]{9}$/.test(phone)) return;

  const sequence = ++guardianAccountSearchSequence;
  guardianAccountSearchLoading.value = true;
  guardianAccountSearchError.value = '';
  guardianCandidates.value = [];
  selectedGuardianPatientId.value = null;
  guardianAccountSearchComplete.value = false;
  guardianAccountSearchHasMore.value = false;
  guardianSearchPhone.value = phone;
  guardianLinkError.value = '';
  guardianLinkSuccess.value = '';
  try {
    const result = await patientsService.search({ keyword: phone, page: 1, pageSize: 50 });
    if (sequence !== guardianAccountSearchSequence || selectedPatient.value?.patientId !== patient.patientId || checkInPhone.value.trim() !== phone || disposed) return;
    guardianCandidates.value = result.items.filter(candidate => candidate.patientId !== patient.patientId && candidate.phone.trim() === phone);
    guardianAccountSearchHasMore.value = result.hasNextPage;
    guardianAccountSearchComplete.value = true;
  } catch {
    if (sequence === guardianAccountSearchSequence) guardianAccountSearchError.value = 'Không tra được hồ sơ theo số điện thoại. Vui lòng thử lại.';
  } finally {
    if (sequence === guardianAccountSearchSequence) guardianAccountSearchLoading.value = false;
  }
}

function selectGuardianAccount() {
  const candidate = guardianCandidates.value.find(item => item.patientId === selectedGuardianPatientId.value);
  if (candidate) guardianFullName.value = candidate.fullName;
}

async function confirmGuardianLink() {
  const ward = selectedPatient.value;
  const guardianPatientId = selectedGuardianPatientId.value;
  if (!canCheckIn.value || !ward || !isUnderFourteen(ward.dateOfBirth) || guardianPatientId === null ||
      !guardianVerifiedAtCounter.value || !guardianFullName.value.trim() || !guardianRelationship.value.trim() ||
      guardianSearchPhone.value !== checkInPhone.value.trim() || guardianLinkSaving.value) {
    guardianLinkError.value = 'Hãy xác minh người giám hộ tại quầy và chọn đúng hồ sơ trước khi tạo liên kết.';
    return;
  }

  guardianLinkSaving.value = true;
  guardianLinkError.value = '';
  guardianLinkSuccess.value = '';
  try {
    const guardian = await patientGuardiansService.assign({
      guardianPatientId,
      wardPatientId: ward.patientId,
      relationship: guardianRelationship.value.trim(),
      notes: 'Đã xác minh trực tiếp tại quầy khi tiếp đón.',
      verifiedAtCounter: true
    });
    if (selectedPatient.value?.patientId !== ward.patientId || disposed) return;
    linkedGuardian.value = guardian;
    guardianFullName.value = guardian.guardianFullName;
    guardianRelationship.value = guardian.relationship;
    guardianLinkSuccess.value = 'Đã xác minh và liên kết tài khoản người giám hộ. Số điện thoại không tự cấp quyền.';
    guardianCandidates.value = [];
    selectedGuardianPatientId.value = null;
  } catch (error) {
    if (axios.isAxiosError(error)) guardianLinkError.value = error.response?.data?.message || 'Không thể tạo liên kết. Hãy xác minh tài khoản người giám hộ và trạng thái liên kết hiện tại.';
    else guardianLinkError.value = 'Không thể tạo liên kết. Vui lòng tải lại hồ sơ và thử lại.';
  } finally {
    guardianLinkSaving.value = false;
  }
}

async function loadOutstandingDebt(patient: PatientSummary) {
  const requestSequence = ++debtRequestSequence;
  outstandingDebt.value = null;
  debtLookupFailed.value = false;
  debtLoading.value = true;
  try {
    const result = await queueService.getOutstandingDebt(patient.patientId);
    if (requestSequence !== debtRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    outstandingDebt.value = result;
  } catch {
    if (requestSequence !== debtRequestSequence || selectedPatient.value?.patientId !== patient.patientId) return;
    debtLookupFailed.value = true;
  } finally {
    if (requestSequence === debtRequestSequence && selectedPatient.value?.patientId === patient.patientId) {
      debtLoading.value = false;
    }
  }
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(amount);
}

async function loadAppointments(patient: PatientSummary) {
  const sequence = ++appointmentSequence;
  appointments.value = [];
  appointmentId.value = null;
  appointmentLookupFailed.value = false;
  checkInError.value = '';
  ticket.value = null;
  loadingAppointments.value = true;
  try {
    const result = await appointmentsService.getScheduledForPatientOnDate(patient.patientId, todayLocal());
    if (sequence !== appointmentSequence || disposed || selectedPatient.value?.patientId !== patient.patientId) return;
    appointments.value = result.items;
    appointmentId.value = result.items[0]?.appointmentId ?? null;
  } catch {
    if (sequence === appointmentSequence && !disposed) appointmentLookupFailed.value = true;
  } finally {
    if (sequence === appointmentSequence) loadingAppointments.value = false;
  }
}

function onDocumentTypeChanged() {
  closeQr();
  citizenId.value = '';
  nationalityCode.value = '';
  fieldErrors.value = {};
  checkInError.value = '';
}

async function submitCheckIn() {
  if (!canCheckIn.value || !selectedPatient.value || checkingIn.value || guardianLinkSaving.value || loadingAppointments.value || appointmentLookupFailed.value || debtLoading.value) return;
  if (guardianLoading.value || guardianLookupFailed.value) return;
  fieldErrors.value = validateCheckIn(citizenId.value, checkInPhone.value, notes.value, documentType.value,
    nationalityCode.value, isChild.value, guardianFullName.value, guardianRelationship.value, guardianVerifiedAtCounter.value);
  checkInError.value = '';
  if (Object.keys(fieldErrors.value).length) return;
  checkingIn.value = true;
  checkInError.value = '';
  try {
    ticket.value = await queueService.checkIn({
      patientId: selectedPatient.value.patientId,
      appointmentId: appointmentId.value,
      dentistId: null,
      notes: notes.value.trim() || null,
      citizenId: isChild.value ? null : citizenId.value.trim(),
      phone: checkInPhone.value.trim(),
      documentType: isChild.value ? null : documentType.value === 'passport' ? 2 : 1,
      nationalityCode: isChild.value || documentType.value !== 'passport' ? null : nationalityCode.value.trim().toUpperCase(),
      guardianFullName: isChild.value ? guardianFullName.value.trim() : null,
      guardianRelationship: isChild.value ? guardianRelationship.value.trim() : null,
      guardianVerifiedAtCounter: isChild.value && guardianVerifiedAtCounter.value
    });
    notes.value = ''; citizenId.value = ''; checkInPhone.value = ''; documentType.value = 'citizenId'; nationalityCode.value = ''; selectedPatient.value = null;
    guardianFullName.value = ''; guardianRelationship.value = ''; guardianVerifiedAtCounter.value = false; linkedGuardian.value = null;
    await refreshToday();
  } catch (error) {
    const response = axios.isAxiosError(error) ? error.response?.data : null;
    const problem = checkInProblem(response);
    fieldErrors.value = problem.fields;
    checkInError.value = problem.message;
  } finally {
    checkingIn.value = false;
  }
}

function openScanner() {
  if (disposed || !canCheckIn.value || checkingIn.value || isChild.value) return;
  isCameraScannerOpen.value = true;
}

function closeQr() {
  qrScanGeneration++;
  isCameraScannerOpen.value = false;
}

async function handleCccdScanSuccess(data: { raw: string; identity: ScannedIdentity }) {
  closeQr();
  await processQrString(data.raw);
}

async function processQrString(raw: string) {
  if (disposed || !canCheckIn.value || checkingIn.value) return;
  if (isChild.value) {
    searchError.value = 'Trẻ dưới 14 tuổi không dùng CCCD riêng. Vui lòng xác minh người giám hộ tại quầy.';
    return;
  }
  const value = raw.trim();
  if (!value) return;
  const generation = ++qrScanGeneration;
  const userId = auth.user?.userId;
  const role = auth.role;
  const patientId = selectedPatient.value?.patientId;
  const isCurrent = () => generation === qrScanGeneration && !disposed
    && canCheckIn.value && auth.user?.userId === userId && auth.role === role
    && selectedPatient.value?.patientId === patientId;
  searchError.value = '';
  try {
    let id12 = '';
    let decodedName = '';
    if (value.includes('|')) {
      const decoded = await decodeCitizenQr(value);
      if (!isCurrent()) return;
      id12 = decoded.citizenId;
      decodedName = decoded.fullName;
    } else if (/^[0-9]{12}$/.test(value)) {
      id12 = value;
    } else {
      searchError.value = 'Mã không đúng định dạng QR CCCD gắn chip hoặc 12 số CCCD.';
      return;
    }

    if (!selectedPatient.value) {
      const lookup = await qrPatientIntakeService.lookup(id12);
      if (!isCurrent()) return;
      const patient = lookup.existing?.isActive ? await patientsService.getById(lookup.existing.patientId) : null;
      if (!isCurrent()) return;
      if (patient) {
        // Commit the scan before loading dependent data. Those loaders guard selection changes.
        await selectPatient(patient, id12);
      } else {
        searchError.value = `Không tìm thấy hồ sơ cho CCCD ${id12}${decodedName ? ` (${decodedName})` : ''}. Mở trang Bệnh nhân để tạo hồ sơ mới.`;
      }
    } else {
      documentType.value = 'citizenId';
      nationalityCode.value = '';
      citizenId.value = id12;
    }
  } catch {
    if (isCurrent()) {
      searchError.value = 'Không thể đọc mã CCCD. Vui lòng quét lại hoặc nhập tay 12 số.';
    }
  }
}

function resetForm() {
  clearTimeout(searchTimer); searchSequence++; clearSelection(); keyword.value = ''; patients.value = []; searched.value = false; searching.value = false; searchError.value = '';
}
onBeforeUnmount(() => {
  disposed = true; isCameraScannerOpen.value = false; clearTimeout(searchTimer); searchSequence++; deskSequence++; waitingSequence++; clearSelection(); keyword.value = ''; patients.value = []; deskRows.value = []; waitingRows.value = [];
});

function todayLocal(): string {
  return new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
}
</script>

<style scoped>
.checkin-page {
  max-width: 1380px;
  margin: 0 auto;
  padding: var(--spacing-6);
  box-sizing: border-box;
}

/* Header Tiếp Đón */
.checkin-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: var(--spacing-4);
  margin-block-end: var(--spacing-6);
  padding-block-end: var(--spacing-4);
  border-block-end: 1px solid var(--color-border-subtle);
  flex-wrap: wrap;
}

.header-badge-row {
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  margin-block-end: 0.35rem;
}

.header-date {
  font-size: var(--font-size-xs);
  font-weight: 700;
  color: var(--color-text-subtle);
  background: #ffffff;
  padding: 0.2rem 0.55rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border-subtle);
}

.checkin-title {
  font-size: var(--font-size-2xl);
  font-weight: 850;
  color: var(--color-primary);
  margin: 0 0 0.35rem;
  letter-spacing: -0.01em;
}

.checkin-desc {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin: 0;
  max-width: 75ch;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  flex-wrap: wrap;
}

/* Bố cục Grid 2 Cột */
.checkin-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(0, 1fr);
  gap: var(--spacing-6);
  align-items: start;
}

.checkin-col-left,
.checkin-col-right {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-6);
}

/* Form tìm kiếm */
.search-input-wrap {
  display: flex;
  gap: var(--spacing-2);
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 220px;
}

.patient-results-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-2);
  margin-block-start: var(--spacing-3);
}

.patient-result-card {
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  padding: var(--spacing-3) var(--spacing-4);
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
  text-align: left;
  cursor: pointer;
  transition: all 0.15s ease;
  font-family: inherit;
  color: inherit;
}

.patient-result-card:hover:not(:disabled) {
  background: #eff6ff;
  border-color: #93c5fd;
  transform: translateX(2px);
}

.patient-result-card--selected {
  background: #e0f2fe !important;
  border-color: var(--color-medical) !important;
  box-shadow: 0 0 0 2px rgba(2, 132, 199, 0.2);
}

.patient-result-avatar {
  width: 38px;
  height: 38px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: var(--font-size-base);
  flex-shrink: 0;
}

.patient-result-info {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  flex: 1;
  min-width: 0;
}

.patient-result-info strong {
  font-size: var(--font-size-sm);
  color: var(--color-text-main);
}

.patient-result-info > span {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

.select-hint {
  font-size: var(--font-size-xs);
  font-weight: 700;
  color: var(--color-medical);
}

.empty-search-box {
  padding: var(--spacing-4);
  text-align: center;
  background: var(--color-surface-subtle);
  border: 1px dashed var(--color-border-medium);
  border-radius: var(--radius-md);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-2);
}

/* Callout đối soát công nợ */
.debt-callout {
  padding: var(--spacing-3) var(--spacing-4);
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-left: 4px solid var(--color-warning);
  border-radius: var(--radius-md);
  color: #9a3412;
  margin-block: var(--spacing-2);
}

.debt-callout-header {
  display: flex;
  align-items: center;
  gap: var(--spacing-2);
  font-size: var(--font-size-sm);
}

.debt-sub {
  font-size: var(--font-size-xs);
  margin: 0.25rem 0 0.15rem;
}

.debt-list {
  margin: 0;
  padding-left: 1.25rem;
  font-size: var(--font-size-xs);
}

.label-with-action {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.text-link-btn {
  background: none;
  border: none;
  color: var(--color-medical);
  font-size: var(--font-size-xs);
  font-weight: 750;
  cursor: pointer;
  padding: 0;
  text-decoration: underline;
}

.text-link-btn:hover {
  color: var(--color-medical-hover);
}

.info-pill-note {
  padding: var(--spacing-2) var(--spacing-3);
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  border-radius: var(--radius-sm);
  font-size: var(--font-size-xs);
  color: #166534;
}

.warning-callout {
  padding: var(--spacing-3);
  background: var(--color-warning-light);
  border: 1px solid var(--color-warning-border);
  border-radius: var(--radius-sm);
  font-size: var(--font-size-xs);
  color: #9a3412;
}

.error-banner {
  padding: var(--spacing-2) var(--spacing-3);
  background: var(--color-danger-light);
  border: 1px solid var(--color-danger-border);
  border-radius: var(--radius-sm);
  font-size: var(--font-size-xs);
  color: #991b1b;
  margin: 0.25rem 0;
}

.error-msg {
  color: var(--color-danger);
  font-size: var(--font-size-xs);
  margin: 0.2rem 0 0;
}

.checkin-submit-btn {
  inline-size: 100%;
  margin-block-start: var(--spacing-3);
  font-size: var(--font-size-base);
  min-block-size: 48px;
}

/* PHIẾU SỐ THỨ TỰ (BOARDING PASS) */
.ticket-card {
  background: linear-gradient(135deg, #0f2d59 0%, #1e4976 100%);
  color: #ffffff;
  border-radius: var(--radius-xl);
  padding: var(--spacing-6);
  box-shadow: 0 12px 28px rgba(15, 45, 89, 0.22);
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--spacing-4);
  position: relative;
  overflow: hidden;
}

.ticket-card::before {
  content: "";
  position: absolute;
  top: -40px;
  right: -40px;
  width: 140px;
  height: 140px;
  background: radial-gradient(circle, rgba(229, 185, 94, 0.3) 0%, transparent 70%);
  border-radius: 50%;
  pointer-events: none;
}

.ticket-badge-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  inline-size: 100%;
}

.ticket-time {
  font-size: var(--font-size-xs);
  color: #93c5fd;
  font-weight: 700;
}

.ticket-number-box {
  background: rgba(255, 255, 255, 0.12);
  border: 2px dashed rgba(255, 255, 255, 0.3);
  border-radius: var(--radius-lg);
  padding: var(--spacing-4) var(--spacing-6);
  display: flex;
  flex-direction: column;
  align-items: center;
  inline-size: 85%;
  backdrop-filter: blur(4px);
}

.ticket-label {
  font-size: 0.72rem;
  letter-spacing: 0.1em;
  color: #fed7aa;
  font-weight: 800;
  text-transform: uppercase;
}

.ticket-number {
  font-size: 3.5rem;
  line-height: 1;
  font-weight: 900;
  color: #ffffff;
  margin: 0.35rem 0;
  letter-spacing: -0.02em;
}

.ticket-subtext {
  font-size: var(--font-size-xs);
  color: #bfdbfe;
}

.ticket-patient-details {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.ticket-name {
  font-size: var(--font-size-xl);
  font-weight: 850;
  color: #ffffff;
  margin: 0;
}

.ticket-meta-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--spacing-3);
  font-size: var(--font-size-xs);
  color: #e2e8f0;
}

.ticket-priority-pill {
  padding: 0.2rem 0.55rem;
  background: #f59e0b;
  color: #78350f;
  border-radius: var(--radius-pill);
  font-weight: 800;
  font-size: 0.7rem;
}

.ticket-priority-pill--normal {
  background: rgba(255, 255, 255, 0.18);
  color: #ffffff;
}

.ticket-instructions {
  font-size: var(--font-size-xs);
  color: #cbd5e1;
  line-height: 1.4;
  border-top: 1px solid rgba(255, 255, 255, 0.15);
  padding-top: var(--spacing-3);
  margin: 0;
}

/* Danh sách lịch hẹn chờ đến hôm nay */
.desk-appointment-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-2);
}

.desk-appointment-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--spacing-3);
  padding: var(--spacing-2) var(--spacing-3);
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
}

.desk-time-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  background: #ffffff;
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-sm);
  padding: 0.25rem 0.5rem;
  min-width: 52px;
}

.desk-time-box strong {
  color: var(--color-medical);
  font-size: var(--font-size-sm);
  line-height: 1.1;
}

.desk-time-box span {
  font-size: 0.65rem;
  color: var(--color-text-subtle);
}

.desk-patient-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.desk-patient-name-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.desk-patient-info strong {
  font-size: var(--font-size-sm);
  color: var(--color-text-main);
}

.desk-patient-info > span {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

/* Danh sách bệnh nhân đang chờ */
.waiting-queue-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-2);
}

.waiting-queue-item {
  display: flex;
  align-items: center;
  gap: var(--spacing-3);
  padding: var(--spacing-2) var(--spacing-3);
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-subtle);
  border-radius: var(--radius-md);
}

.waiting-number-pill {
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  background: var(--color-primary);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 850;
  font-size: var(--font-size-sm);
  flex-shrink: 0;
}

.waiting-item-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.waiting-item-info strong {
  font-size: var(--font-size-sm);
}

.app-badge--warning {
  background: #fef3c7;
  color: #92400e;
  border: 1px solid #f59e0b;
}

.waiting-item-info > span {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--spacing-2);
  margin-block-start: var(--spacing-3);
  padding-block-start: var(--spacing-2);
  border-top: 1px solid var(--color-border-subtle);
}

.pagination-text {
  font-size: var(--font-size-xs);
  color: var(--color-text-subtle);
}

.access-denied-box {
  padding: var(--spacing-8);
  text-align: center;
  color: var(--color-danger);
}

.spin-icon {
  display: inline-block;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Responsive adjustments */
@media (max-width: 900px) {
  .checkin-grid {
    grid-template-columns: 1fr;
  }
}
</style>
