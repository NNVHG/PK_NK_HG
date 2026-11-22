<template>
  <main class="page appointments-page">
    <!-- Header banner điều hành lịch hẹn -->
    <header class="header-banner">
      <div class="header-icon-box" aria-hidden="true">📅</div>
      <div class="header-titles">
        <h1>Quản lý Lịch hẹn & Tiếp nhận Khám</h1>
        <p class="subtitle">Tiếp nhận, duyệt yêu cầu đặt hẹn trực tuyến và đặt lịch trực tiếp cho khách tại quầy.</p>
      </div>
      <div class="header-actions">
        <button
          type="button"
          class="app-btn app-btn--primary btn-book-appointment"
          @click="openBookingModal"
        >
          <span class="btn-icon">🛎️</span> + Đặt lịch khám
        </button>
        <button
          type="button"
          class="app-btn app-btn--outline"
          :disabled="requestsBusy || deskBusy"
          @click="refreshAll"
        >
          <span class="btn-icon">🔄</span> Tải lại
        </button>
      </div>
    </header>

    <!-- Thông báo toàn trang -->
    <Transition name="alert-slide">
      <div v-if="globalSuccess" class="alert-box alert-success" role="status" aria-live="polite">
        ✅ {{ globalSuccess }}
      </div>
    </Transition>
    <Transition name="alert-slide">
      <div v-if="globalError" class="alert-box alert-error" role="alert">
        ⚠️ {{ globalError }}
      </div>
    </Transition>

    <!-- Thanh chuyển Tab khu vực quản lý lịch hẹn -->
    <nav class="appointments-tabs" role="tablist" aria-label="Chuyển mục quản lý lịch hẹn">
      <button
        type="button"
        role="tab"
        class="appointments-tab-btn"
        :class="{ 'is-active': activeTab === 'all' }"
        :aria-selected="activeTab === 'all'"
        @click="activeTab = 'all'"
      >
        <span>📊 Tất cả (Tổng hợp)</span>
      </button>
      <button
        type="button"
        role="tab"
        class="appointments-tab-btn"
        :class="{ 'is-active': activeTab === 'requests' }"
        :aria-selected="activeTab === 'requests'"
        @click="activeTab = 'requests'"
      >
        <span>📑 Yêu cầu chờ duyệt</span>
        <span class="tab-badge" :class="{ 'tab-badge--warn': requestsRows.length > 0 }">
          {{ requestsRows.length }}
        </span>
      </button>
      <button
        type="button"
        role="tab"
        class="appointments-tab-btn"
        :class="{ 'is-active': activeTab === 'desk' }"
        :aria-selected="activeTab === 'desk'"
        @click="activeTab = 'desk'"
      >
        <span>🗓️ Lịch khám đã chốt</span>
        <span class="tab-badge">{{ deskTotal }}</span>
      </button>
    </nav>

    <Transition name="appt-tab-switch" mode="out-in">
      <div :key="activeTab" class="appointments-tab-stage">
        <!-- ====================================================================== -->
        <!-- PHẦN 1 TRỌNG TÂM: DANH SÁCH KHÁCH HÀNG YÊU CẦU DUYỆT TRỰC TUYẾN -->
        <!-- ====================================================================== -->
        <section
          v-if="activeTab === 'all' || activeTab === 'requests'"
          class="section-card requests-section"
          aria-labelledby="requests-heading"
        >
          <div class="section-header">
            <div class="section-title-wrap">
              <span class="section-icon">📑</span>
              <div>
                <h2 id="requests-heading">Yêu cầu đặt hẹn trực tuyến chờ duyệt</h2>
                <p class="section-desc">Khách gửi yêu cầu từ trang chủ. Khi lễ tân duyệt trong popup, hệ thống sẽ xác nhận lịch hẹn chính thức.</p>
              </div>
            </div>
            <div class="section-badge-wrap">
              <span v-if="requestsRows.length" class="badge-count badge-warning">
                {{ requestsRows.length }} yêu cầu chờ duyệt
              </span>
              <span v-else class="badge-count badge-neutral">
                0 yêu cầu chờ duyệt
              </span>
            </div>
          </div>

          <!-- Trạng thái loading / error -->
          <div v-if="requestsBusy && !requestsRows.length" class="loading-state" role="status">
            <div class="spinner"></div>
            <p>Đang tải danh sách yêu cầu đặt lịch…</p>
          </div>

          <!-- Trạng thái không có yêu cầu nào chờ duyệt -->
          <div v-else-if="!requestsBusy && !requestsRows.length" class="empty-requests-state">
            <div class="empty-icon">🎉</div>
            <h3>Hiện không có yêu cầu trực tuyến nào đang chờ duyệt</h3>
            <p>Tất cả yêu cầu đã được xử lý hoặc chưa có khách gửi mới. Bạn có thể đặt lịch cho khách tại quầy.</p>
            <button type="button" class="app-btn app-btn--primary app-btn--sm" @click="openBookingModal">
              <span class="btn-icon">🛎️</span> Đặt lịch tại quầy ngay
            </button>
          </div>

          <!-- Lưới danh sách yêu cầu trực tuyến -->
          <div v-else class="requests-container">
            <Transition name="appt-page-swap" mode="out-in">
              <div :key="'req-page-' + requestsPage" class="requests-page-frame">
                <TransitionGroup
                  name="request-card-list"
                  tag="ol"
                  class="requests-grid"
                  aria-label="Danh sách yêu cầu đặt hẹn trực tuyến"
                >
                  <li
                    v-for="req in requestsRows"
                    :key="req.id"
                    class="request-card"
                    :class="{ 'request-card--active': approvingRequestId === req.id || rejectingRequest?.id === req.id }"
                  >
                    <div class="request-card-header">
                      <div class="patient-main">
                        <h3 class="patient-name">{{ req.fullName }}</h3>
                        <span v-if="isChildBirthDate(req.dateOfBirth)" class="badge-child">👶 Trẻ em (&lt; 14 tuổi)</span>
                        <span v-if="req.isReturning" class="badge-returning">✨ Đã từng khám</span>
                        <span v-else class="badge-new">Khách mới</span>
                      </div>
                      <span class="request-code">#YC-{{ req.id }}</span>
                    </div>

                    <div class="request-details">
                      <div class="detail-item">
                        <span class="detail-label">Lịch mong muốn:</span>
                        <span class="detail-val highlight-time">
                          📅 {{ formatDate(req.appointmentDate) }} · ⏰ {{ req.slotTime.slice(0, 5) }} ({{ req.slotDurationMinutes }} phút)
                        </span>
                      </div>
                      <div class="detail-item">
                        <span class="detail-label">Số điện thoại:</span>
                        <span class="detail-val">📞 {{ req.phone }}</span>
                      </div>
                      <div class="detail-item">
                        <span class="detail-label">Ngày sinh:</span>
                        <span class="detail-val">🎂 {{ formatDate(req.dateOfBirth) }}</span>
                      </div>
                    </div>

                    <div v-if="parseRequestNotes(req.notes).guardian" class="request-guardian">
                      👨‍👩‍👧 <strong>Người giám hộ:</strong> {{ parseRequestNotes(req.notes).guardian }}
                    </div>

                    <div v-if="parseRequestNotes(req.notes).note" class="request-notes">
                      <span class="notes-tag">Ghi chú:</span> {{ parseRequestNotes(req.notes).note }}
                    </div>

                    <!-- Thanh nút thao tác -->
                    <div class="request-actions">
                      <button
                        type="button"
                        class="app-btn app-btn--success"
                        :disabled="requestsApprovalBusy || requestsBusy"
                        @click="startApprove(req)"
                      >
                        ✓ Duyệt yêu cầu
                      </button>
                      <button
                        type="button"
                        class="app-btn app-btn--danger-outline"
                        :disabled="requestsApprovalBusy || requestsBusy"
                        @click="startReject(req)"
                      >
                        ✕ Từ chối
                      </button>
                    </div>
                  </li>
                </TransitionGroup>
              </div>
            </Transition>

            <!-- Phân trang danh sách yêu cầu -->
            <nav v-if="requestsRows.length || requestsPage > 1" class="pagination-bar" aria-label="Phân trang yêu cầu trực tuyến">
              <span class="page-info">Trang <strong>{{ requestsPage }}</strong></span>
              <div class="pagination-controls">
                <button
                  type="button"
                  class="app-btn app-btn--outline app-btn--sm"
                  :disabled="requestsApprovalBusy || requestsBusy || requestsPage === 1"
                  @click="loadRequests(requestsPage - 1)"
                >
                  ← Trang trước
                </button>
                <button
                  type="button"
                  class="app-btn app-btn--outline app-btn--sm"
                  :disabled="requestsApprovalBusy || requestsBusy || !requestsHasNext"
                  @click="loadRequests(requestsPage + 1)"
                >
                  Trang sau →
                </button>
              </div>
            </nav>
          </div>
        </section>

        <!-- ====================================================================== -->
        <!-- PHẦN 2 BỔ TRỢ: DANH SÁCH LỊCH HẸN ĐÃ XÁC NHẬN THEO NGÀY -->
        <!-- ====================================================================== -->
        <section
          v-if="activeTab === 'all' || activeTab === 'desk'"
          class="section-card confirmed-section"
          aria-labelledby="confirmed-heading"
        >
          <div class="section-header">
            <div class="section-title-wrap">
              <span class="section-icon">🗓️</span>
              <div>
                <h2 id="confirmed-heading">Lịch khám đã chốt theo ngày</h2>
                <p class="section-desc">Theo dõi các lịch hẹn đã xác nhận, nhắc hẹn hoặc hủy/dời lịch khi người bệnh yêu cầu.</p>
              </div>
            </div>
          </div>

          <!-- Bộ lọc ngày & trạng thái -->
          <form class="filters-card" @submit.prevent="loadDesk(1)">
            <div class="filter-group">
              <label for="desk-day">Ngày hẹn</label>
              <input id="desk-day" v-model="deskDate" type="date" :disabled="deskBusy" @change="loadDesk(1)" />
            </div>
            <div class="filter-group">
              <label for="desk-status">Trạng thái</label>
              <select id="desk-status" v-model="deskStatus" :disabled="deskBusy" @change="loadDesk(1)">
                <option value="Scheduled">Đang chờ khám</option>
                <option value="">Tất cả trạng thái</option>
                <option value="Cancelled">Đã hủy</option>
                <option value="CheckedIn">Đã tiếp đón</option>
                <option value="Completed">Hoàn tất</option>
              </select>
            </div>
            <button class="app-btn app-btn--primary btn-filter-submit" :disabled="deskLoading || deskBusy">
              <span v-if="deskLoading">Đang tải…</span>
              <span v-else>Tra cứu / Tải lại</span>
            </button>
          </form>

          <div v-if="deskLoading" class="loading-state" role="status">
            <div class="spinner"></div>
            <p>Đang tải danh sách lịch hẹn…</p>
          </div>

          <div v-else-if="!deskRows.length" class="empty-state">
            <p>Không có lịch hẹn nào phù hợp với điều kiện tìm kiếm ngày {{ formatDate(deskDate) }}.</p>
          </div>

          <Transition v-else name="appt-page-swap" mode="out-in">
            <div :key="'desk-page-' + deskPage + '-' + deskDate + '-' + deskStatus" class="confirmed-table-wrap">
              <table class="confirmed-table">
                <thead>
                  <tr>
                    <th>Giờ hẹn</th>
                    <th>Mã lịch</th>
                    <th>Bệnh nhân</th>
                    <th>Trạng thái</th>
                    <th>Ghi chú</th>
                    <th class="col-actions">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="item in deskRows" :key="item.appointmentId">
                    <td class="cell-time">
                      <strong>{{ item.slotTime.slice(0, 5) }}</strong>
                      <span v-if="item.queueNumber" class="badge-queue">STT #{{ item.queueNumber }}</span>
                    </td>
                    <td>#{{ item.appointmentId }}</td>
                    <td>
                      <div class="patient-cell-name">
                        <span>{{ item.patientName }}</span>
                        <span v-if="item.isChildPatient" class="cell-child-badge">👶 Trẻ em</span>
                        <span v-if="item.missingGuardianAccount" class="cell-missing-badge">⚠️ Thiếu tài khoản</span>
                      </div>
                      <div class="patient-cell-code">#{{ item.patientCode }}</div>
                    </td>
                    <td>
                      <span class="status-pill" :class="'status--' + item.status.toLowerCase()">
                        {{ formatStatus(item.status) }}
                      </span>
                    </td>
                    <td class="cell-notes">{{ item.notes || '—' }}</td>
                    <td class="col-actions">
                      <div class="action-buttons">
                        <AppointmentRescheduling
                          v-if="item.status === 'Scheduled'"
                          :appointment-id="item.appointmentId"
                          :patient-name="item.patientName"
                          @rescheduled="loadDesk(deskPage)"
                        />
                        <button
                          v-if="item.status === 'Scheduled'"
                          type="button"
                          class="action-btn action-btn--cancel"
                          :disabled="deskBusy"
                          @click="chooseDeskCancel(item)"
                        >
                          Hủy hẹn
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </Transition>

          <!-- Phân trang danh sách lịch hẹn -->
          <nav v-if="deskTotal > 20 || deskPage > 1" class="pagination-bar" aria-label="Phân trang lịch hẹn">
            <span class="page-info">Trang <strong>{{ deskPage }}</strong> / Tổng <strong>{{ deskTotal }}</strong> cuộc hẹn</span>
            <div class="pagination-controls">
              <button
                type="button"
                class="app-btn app-btn--outline app-btn--sm"
                :disabled="deskBusy || deskPage === 1"
                @click="loadDesk(deskPage - 1)"
              >
                ← Trang trước
              </button>
              <button
                type="button"
                class="app-btn app-btn--outline app-btn--sm"
                :disabled="deskBusy || !deskHasNext"
                @click="loadDesk(deskPage + 1)"
              >
                Trang sau →
              </button>
            </div>
          </nav>
        </section>
      </div>
    </Transition>

    <!-- ====================================================================== -->
    <!-- MODAL ĐẶT LỊCH KHÁM: ĐIỀN THỦ CÔNG / QUÉT CCCD TỰ ĐỘNG CHECK HỒ SƠ -->
    <!-- ====================================================================== -->
    <Teleport to="body">
      <Transition name="modal-fade">
        <div v-if="isBookingModalOpen" class="modal-backdrop" @click.self="closeBookingModal">
        <div class="booking-dialog" role="dialog" aria-modal="true" aria-labelledby="modal-booking-title">
          <div class="dialog-header">
            <div class="dialog-title-wrap">
              <span class="dialog-icon">🛎️</span>
              <div>
                <h2 id="modal-booking-title">Đặt lịch khám tại quầy</h2>
                <p class="dialog-desc">Điền thông tin đặt lịch như trên web (kèm CCCD & địa chỉ). Hệ thống sẽ tự động kiểm tra hồ sơ và xác nhận lịch.</p>
              </div>
            </div>
            <button
              type="button"
              class="btn-close-dialog"
              :disabled="modalSubmitting"
              aria-label="Đóng hộp thoại"
              @click="closeBookingModal"
            >
              ✕
            </button>
          </div>

          <div class="dialog-body">
            <!-- Thông báo lỗi trong modal -->
            <div v-if="modalError" class="alert-box alert-error" role="alert">
              ⚠️ {{ modalError }}
            </div>

            <!-- Banner quét CCCD nhập nhanh -->
            <div v-if="!isChild && form.documentType === 'citizenId'" class="quick-scan-banner">
              <div class="scan-info">
                <strong>⚡ Nhập nhanh thông tin từ Căn cước công dân</strong>
                <p>Tự động điền Họ tên, CCCD, Ngày sinh, Giới tính, Địa chỉ vào form bên dưới.</p>
              </div>
              <button
                type="button"
                class="app-btn app-btn--primary btn-quick-scan"
                :disabled="modalSubmitting"
                @click="isCameraOpen = true"
              >
                <span class="btn-icon">📷</span> Quét CCCD
              </button>
            </div>

            <!-- THÔNG BÁO TỰ ĐỘNG NHẬN DIỆN HỒ SƠ -->
            <div v-if="checkingPatient" class="recognition-banner recognition-loading">
              <span class="spin-icon">⏳</span> Đang kiểm tra hồ sơ bệnh nhân trong hệ thống…
            </div>
            <div v-else-if="matchedPatient" class="recognition-banner recognition-matched">
              <div class="recog-title">
                ✅ <strong>Đã nhận diện hồ sơ bệnh nhân cũ:</strong> {{ matchedPatient.fullName }}
                <span class="badge-code">#{{ matchedPatient.patientCode }}</span>
              </div>
              <p class="recog-sub">Lịch hẹn mới sẽ được tự động gắn vào hồ sơ đã có của bệnh nhân này.</p>
            </div>
            <div v-else-if="patientCandidates.length" class="recognition-banner recognition-new">
              <div class="recog-title"><strong>Đã tìm thấy hồ sơ trùng thông tin</strong></div>
              <p class="recog-sub">Đối chiếu họ tên, ngày sinh và số liên hệ rồi chọn đúng hồ sơ. Không tự gộp hồ sơ theo số điện thoại.</p>
              <button
                v-for="candidate in patientCandidates"
                :key="candidate.patientId"
                type="button"
                class="candidate-patient-button"
                @click="selectExistingPatient(candidate)"
              >
                {{ candidate.fullName }} · {{ candidate.patientCode }} · {{ formatDate(candidate.dateOfBirth) }}
              </button>
            </div>
            <div v-else-if="form.fullName && form.phone.length === 10" class="recognition-banner recognition-new">
              <div class="recog-title">
                ✨ <strong>Bệnh nhân mới (chưa có hồ sơ):</strong>
              </div>
              <p v-if="isChild" class="recog-sub">Trẻ dưới 14 tuổi không tạo tài khoản riêng và không yêu cầu CCCD; hồ sơ sẽ mang trạng thái <strong>Thiếu tài khoản</strong> nếu chưa xác nhận liên kết người giám hộ tại quầy.</p>
              <p v-else class="recog-sub">Hệ thống sẽ tự động tạo hồ sơ bệnh nhân mới khi bạn bấm "Xác nhận đặt lịch".</p>
            </div>

            <form @submit.prevent="submitBookingModal">
              <!-- ================= MỤC 1: THÔNG TIN BỆNH NHÂN ================= -->
              <fieldset class="form-section">
                <legend class="section-legend">1. Thông tin bệnh nhân</legend>

                <div class="form-grid">
                  <div class="form-group">
                    <label for="form-name">Họ và tên <span class="req">*</span></label>
                    <input
                      id="form-name"
                      v-model="form.fullName"
                      type="text"
                      placeholder="VD: Nguyễn Văn A"
                      maxlength="100"
                      :disabled="modalSubmitting"
                      required
                      @input="onCounterNameInput"
                      @blur="onCounterNameBlur"
                    />
                  </div>
                  <div class="form-group">
                    <label for="form-phone">{{ isChild ? 'Số điện thoại liên hệ của người giám hộ' : 'Số điện thoại liên hệ' }} <span class="req">*</span></label>
                    <input
                      id="form-phone"
                      v-model.trim="form.phone"
                      type="tel"
                      placeholder="VD: 0912345678"
                      maxlength="10"
                      :disabled="modalSubmitting"
                      required
                      @blur="checkExistingPatient"
                    />
                  </div>
                </div>

                <div class="form-grid">
                  <div class="form-group">
                    <span class="dob-field-label">Ngày sinh <span class="req">*</span></span>
                    <VietnameseDateOfBirthInput
                      v-model="form.dateOfBirth"
                      :max="todayDateStr"
                      :disabled="modalSubmitting"
                      id-prefix="counter-dob"
                      required
                      @change="checkExistingPatient"
                    />
                  </div>
                  <div class="form-group">
                    <label for="form-gender">Giới tính <span class="req">*</span></label>
                    <select id="form-gender" v-model="form.gender" :disabled="modalSubmitting" required>
                      <option value="Male">Nam</option>
                      <option value="Female">Nữ</option>
                      <option value="Other">Khác</option>
                    </select>
                  </div>
                </div>

                <!-- Trường CCCD và Địa chỉ (bổ sung so với web) -->
                <div class="form-grid">
                  <div class="form-group" v-if="!isChild">
                    <label for="form-document-type">Giấy tờ định danh <span class="req">*</span></label>
                    <select id="form-document-type" v-model="form.documentType" :disabled="modalSubmitting" @change="onCounterDocumentTypeChanged">
                      <option value="citizenId">CCCD</option>
                      <option value="passport">Hộ chiếu</option>
                    </select>
                    <label for="form-cccd">{{ form.documentType === 'passport' ? 'Số hộ chiếu' : 'Số CCCD' }} <span class="req">*</span></label>
                    <input
                      id="form-cccd"
                      v-model.trim="form.cccd"
                      type="text"
                      :placeholder="form.documentType === 'passport' ? '6–12 chữ cái hoặc chữ số' : '12 chữ số CCCD'"
                      maxlength="12"
                      :disabled="modalSubmitting"
                      required
                      @blur="checkExistingPatient"
                    />
                    <label v-if="form.documentType === 'passport'" for="form-nationality">Quốc gia cấp hộ chiếu <span class="req">*</span></label>
                    <input
                      v-if="form.documentType === 'passport'"
                      id="form-nationality"
                      v-model.trim="form.nationalityCode"
                      type="text"
                      placeholder="Ví dụ: VN"
                      maxlength="3"
                      :disabled="modalSubmitting"
                      required
                    />
                    <small class="form-hint">Đối chiếu giấy tờ định danh trước khi xác nhận tại quầy.</small>
                  </div>
                  <div class="form-group" :class="{ 'grid-full': isChild }">
                    <label for="form-address">Địa chỉ cư trú</label>
                    <input
                      id="form-address"
                      v-model.trim="form.address"
                      type="text"
                      placeholder="Địa chỉ số nhà, phường/xã..."
                      maxlength="200"
                      :disabled="modalSubmitting"
                    />
                  </div>
                </div>

                <div class="form-group">
                  <label for="form-email">Email <span class="optional">(không bắt buộc)</span></label>
                  <input
                    id="form-email"
                    v-model.trim="form.email"
                    type="email"
                    placeholder="example@gmail.com"
                    maxlength="100"
                    :disabled="modalSubmitting"
                  />
                </div>

                <!-- Người giám hộ nếu là Trẻ em (< 14 tuổi) theo chuẩn web & §1.6 -->
                <div v-if="isChild" class="guardian-box">
                  <div class="child-badge-notice">
                    <div class="child-badge-row">
                      <span>👶 <strong>Bệnh nhân là Trẻ em (&lt; 14 tuổi)</strong></span>
                      <span v-if="!confirmedGuardianPatient && !matchedPatient?.hasActiveGuardian" class="cell-missing-badge">⚠️ Thiếu tài khoản</span>
                      <span v-else class="cell-linked-badge">👨‍👩‍👧 Đã xác nhận giám hộ</span>
                    </div>
                    <p class="field-hint">Trẻ em không yêu cầu CCCD riêng và không có tài khoản riêng. Lễ tân kiểm tra hoặc xác nhận người giám hộ có tài khoản tại quầy; nếu chưa có tài khoản giám hộ phù hợp, hồ sơ trẻ vẫn được lưu với trạng thái Thiếu tài khoản.</p>
                  </div>
                  <div class="form-grid">
                    <div class="form-group">
                      <label for="guardian-name">Họ tên người giám hộ <span class="req">*</span></label>
                      <input
                        id="guardian-name"
                        v-model="form.guardianName"
                        type="text"
                        placeholder="Họ tên cha / mẹ / người đại diện"
                        maxlength="100"
                        :disabled="modalSubmitting"
                        :required="isChild"
                        @input="onCounterGuardianInput"
                        @blur="onCounterGuardianBlur"
                      />
                    </div>
                    <div class="form-group">
                      <label for="guardian-relation">Quan hệ với trẻ <span class="req">*</span></label>
                      <select id="guardian-relation" v-model="form.guardianRelation" :disabled="modalSubmitting" :required="isChild">
                        <option value="Bố/Mẹ">Bố/Mẹ</option>
                        <option value="Ông/Bà">Ông/Bà</option>
                        <option value="Người giám hộ hợp pháp">Người giám hộ hợp pháp</option>
                      </select>
                    </div>
                  </div>
                  <div class="guardian-lookup-panel">
                    <label for="counter-guardian-search" class="guardian-lookup-label">Liên kết tài khoản người giám hộ (không tự động gắn theo SĐT)</label>
                    <div class="guardian-lookup-row">
                      <input
                        id="counter-guardian-search"
                        v-model.trim="guardianLookupKeyword"
                        type="text"
                        placeholder="Nhập SĐT hoặc họ tên người giám hộ để tra cứu"
                        :disabled="modalSubmitting || guardianLookupBusy"
                      />
                      <button
                        type="button"
                        class="app-btn app-btn--secondary"
                        :disabled="modalSubmitting || guardianLookupBusy"
                        @click="searchGuardianCandidates"
                      >
                        {{ guardianLookupBusy ? 'Đang tìm…' : '🔍 Tra cứu' }}
                      </button>
                    </div>
                    <div v-if="confirmedGuardianPatient" class="guardian-confirmed-pill">
                      <span>✓ Sẽ liên kết giám hộ: <strong>{{ confirmedGuardianPatient.fullName }}</strong> ({{ confirmedGuardianPatient.patientCode }} · {{ confirmedGuardianPatient.phone }})</span>
                      <button type="button" class="link-btn" :disabled="modalSubmitting" @click="clearConfirmedGuardian">Bỏ chọn</button>
                    </div>
                    <div v-else-if="guardianCandidates.length" class="guardian-candidate-picker">
                      <select v-model.number="selectedGuardianCandidateId" :disabled="modalSubmitting">
                        <option :value="0">Chọn hồ sơ người giám hộ đã đối chiếu…</option>
                        <option
                          v-for="candidate in guardianCandidates"
                          :key="candidate.patientId"
                          :value="candidate.patientId"
                          :disabled="candidate.hasUserAccount === false"
                        >
                          {{ candidate.fullName }} · {{ candidate.phone }} · {{ candidate.patientCode }}{{ candidate.hasUserAccount === false ? ' — Chưa có tài khoản' : ' — Có tài khoản' }}
                        </option>
                      </select>
                      <button
                        type="button"
                        class="app-btn app-btn--primary"
                        :disabled="modalSubmitting || !selectedGuardianCandidateId"
                        @click="confirmSelectedGuardian"
                      >
                        Xác nhận người giám hộ
                      </button>
                    </div>
                    <p v-else-if="guardianLookupSearched && !guardianLookupBusy" class="guardian-lookup-empty">
                      Chưa tìm thấy hồ sơ người lớn có tài khoản phù hợp. Nếu tiếp tục đặt lịch, hồ sơ trẻ vẫn được lưu với trạng thái <strong>Thiếu tài khoản</strong> để cập nhật sau.
                    </p>
                  </div>
                  <label class="confirm-checkbox guardian-counter-confirmation">
                    <input v-model="form.guardianVerifiedAtCounter" type="checkbox" />
                    <span>Tôi đã xác minh trực tiếp người giám hộ tại quầy.</span>
                  </label>
                </div>
              </fieldset>

              <!-- ================= MỤC 2: CHỌN LỊCH KHÁM ================= -->
              <fieldset class="form-section">
                <legend class="section-legend">2. Thời gian khám</legend>

                <div class="form-grid">
                  <div class="form-group">
                    <label for="modal-day">Ngày khám <span class="req">*</span></label>
                    <input
                      id="modal-day"
                      v-model="form.appointmentDate"
                      type="date"
                      :min="todayDateStr"
                      :disabled="modalSubmitting"
                      required
                    />
                  </div>
                  <div class="form-group">
                    <label for="modal-slot">Khung giờ hẹn <span class="req">*</span></label>
                    <select
                      id="modal-slot"
                      v-model="form.slot"
                      :disabled="modalSubmitting || scheduleLoading || !slots.length"
                      required
                    >
                      <option value="">-- Chọn khung giờ --</option>
                      <option v-for="s in slots" :key="s" :value="s">{{ s }}</option>
                    </select>
                    <small v-if="scheduleLoading" class="form-hint">Đang tải khung giờ…</small>
                  </div>
                </div>

                <div class="form-group">
                  <label for="modal-notes">Nội dung đặt lịch / Triệu chứng ban đầu</label>
                  <textarea
                    id="modal-notes"
                    v-model.trim="form.notes"
                    rows="2"
                    maxlength="500"
                    placeholder="Ghi chú triệu chứng đau nhức, nhu cầu khám, nhổ răng, chỉnh nha..."
                    :disabled="modalSubmitting"
                  ></textarea>
                </div>
              </fieldset>

              <!-- Dialog footer -->
              <div class="dialog-footer">
                <button
                  type="button"
                  class="app-btn app-btn--secondary"
                  :disabled="modalSubmitting"
                  @click="closeBookingModal"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  class="app-btn app-btn--primary btn-submit-booking"
                  :disabled="modalSubmitting || !isFormValid"
                >
                  <span v-if="modalSubmitting">Đang kiểm tra & đặt lịch…</span>
                  <span v-else>✓ Xác nhận đặt lịch</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
      </Transition>
    </Teleport>

    <!-- Popup Đối soát & Duyệt yêu cầu đặt hẹn -->
    <dialog
      v-if="approvingRequest"
      :ref="bindApprovalDialog"
      class="custom-dialog approval-modal-dialog"
      aria-labelledby="approval-modal-title"
      @cancel="onApprovalDialogCancel"
    >
      <div class="modal-shell">
        <div class="dialog-header">
          <div class="dialog-title-wrap">
            <span class="dialog-icon">📋</span>
            <div>
              <div class="modal-title-badges">
                <h2 id="approval-modal-title">Đối soát &amp; Duyệt yêu cầu đặt hẹn</h2>
                <span class="request-code">#YC-{{ approvingRequest.id }}</span>
                <span v-if="isChildBirthDate(approvingRequest.dateOfBirth)" class="badge-child">👶 Trẻ em (&lt; 14 tuổi)</span>
                <span v-if="approvingRequest.isReturning" class="badge-returning">✨ Đã từng khám</span>
                <span v-else class="badge-new">Khách mới</span>
              </div>
              <p class="dialog-desc">Kiểm tra thông tin bệnh nhân, chọn hồ sơ nhận lịch và xác nhận.</p>
            </div>
          </div>
          <button
            type="button"
            class="btn-close-dialog"
            aria-label="Đóng hộp thoại duyệt"
            :disabled="requestsApprovalBusy"
            @click="closeApproveModal"
          >
            ✕
          </button>
        </div>

        <div class="dialog-body">
          <div class="approval-request-summary">
            <div class="summary-main-row">
              <strong>{{ approvingRequest.fullName }}</strong>
              <span class="highlight-time">
                📅 {{ formatDate(approvingRequest.appointmentDate) }} · ⏰ {{ approvingRequest.slotTime.slice(0, 5) }} ({{ approvingRequest.slotDurationMinutes }} phút)
              </span>
            </div>
            <div class="summary-meta-row">
              <span>📞 {{ approvingRequest.phone }}</span>
              <span>🎂 {{ formatDate(approvingRequest.dateOfBirth) }}</span>
            </div>
            <p v-if="parseRequestNotes(approvingRequest.notes).guardian" class="summary-guardian">
              <strong>👨‍👩‍👧 Người giám hộ:</strong> {{ parseRequestNotes(approvingRequest.notes).guardian }}
            </p>
            <p v-if="parseRequestNotes(approvingRequest.notes).note" class="summary-notes">
              <strong>Ghi chú:</strong> {{ parseRequestNotes(approvingRequest.notes).note }}
            </p>
          </div>

          <BookingRequestApproval
            :key="approvingRequest.id"
            class="approval-panel"
            :request-id="approvingRequest.id"
            @approved="onApprovedRequest"
            @working="requestsApprovalBusy = $event"
          />
        </div>

        <div class="dialog-footer">
          <button
            type="button"
            class="app-btn app-btn--secondary"
            :disabled="requestsApprovalBusy"
            @click="closeApproveModal"
          >
            Đóng hộp thoại
          </button>
        </div>
      </div>
    </dialog>

    <!-- Popup Từ chối yêu cầu đặt hẹn -->
    <dialog
      v-if="rejectingRequest"
      :ref="bindRejectDialog"
      class="custom-dialog reject-modal-dialog"
      aria-labelledby="reject-modal-title"
      @cancel="onRejectDialogCancel"
    >
      <form class="modal-shell" @submit.prevent="submitReject">
        <div class="dialog-header dialog-header--danger">
          <div class="dialog-title-wrap">
            <span class="dialog-icon">✕</span>
            <div>
              <h2 id="reject-modal-title">Từ chối yêu cầu #YC-{{ rejectingRequest.id }}</h2>
              <p class="dialog-desc">Bệnh nhân: <strong>{{ rejectingRequest.fullName }}</strong></p>
            </div>
          </div>
          <button
            type="button"
            class="btn-close-dialog"
            aria-label="Đóng hộp thoại từ chối"
            :disabled="requestsBusy"
            @click="closeRejectModal"
          >
            ✕
          </button>
        </div>

        <div class="dialog-body">
          <div class="approval-request-summary">
            <div class="summary-main-row">
              <strong>{{ rejectingRequest.fullName }}</strong>
              <span class="highlight-time">
                📅 {{ formatDate(rejectingRequest.appointmentDate) }} · ⏰ {{ rejectingRequest.slotTime.slice(0, 5) }}
              </span>
            </div>
            <div class="summary-meta-row">
              <span>📞 {{ rejectingRequest.phone }}</span>
              <span>🎂 {{ formatDate(rejectingRequest.dateOfBirth) }}</span>
            </div>
          </div>

          <p class="rejection-hint">Yêu cầu khi bị từ chối sẽ ngay lập tức giải phóng khung giờ đã giữ.</p>

          <div v-if="rejectError" class="alert-box alert-error" role="alert">
            ⚠️ {{ rejectError }}
          </div>

          <div class="form-group">
            <label for="reject-reason">Lý do từ chối <span class="req">*</span></label>
            <textarea
              id="reject-reason"
              v-model.trim="rejectReason"
              rows="3"
              maxlength="500"
              placeholder="Nhập lý do gửi đến bệnh nhân..."
              :disabled="requestsBusy"
              autofocus
              required
            ></textarea>
          </div>

          <label class="confirm-checkbox">
            <input v-model="rejectConfirmed" type="checkbox" :disabled="requestsBusy" />
            <span>Xác nhận từ chối yêu cầu đặt hẹn này.</span>
          </label>
        </div>

        <div class="dialog-footer">
          <button
            type="button"
            class="app-btn app-btn--secondary"
            :disabled="requestsBusy"
            @click="closeRejectModal"
          >
            Hủy
          </button>
          <button
            class="app-btn app-btn--danger"
            type="submit"
            :disabled="requestsBusy || !rejectConfirmed || !rejectReason.trim()"
          >
            {{ requestsBusy ? 'Đang từ chối…' : 'Xác nhận từ chối' }}
          </button>
        </div>
      </form>
    </dialog>

    <!-- Hộp thoại Camera quét CCCD -->
    <CameraCccdScanner
      :is-open="isCameraOpen"
      @close="isCameraOpen = false"
      @success="handleCccdScanned"
    />

    <!-- Dialog xác nhận hủy lịch hẹn đã chốt -->
    <dialog
      ref="cancelDialog"
      class="custom-dialog"
      aria-labelledby="cancel-title"
      @cancel.prevent="closeDeskCancel"
    >
      <div v-if="selectedCancelItem" class="dialog-content">
        <div class="dialog-header">
          <div class="dialog-title-wrap">
            <span class="dialog-icon">⚠️</span>
            <div>
              <h2 id="cancel-title" class="dialog-title">Hủy lịch hẹn #{{ selectedCancelItem.appointmentId }}</h2>
              <p class="dialog-subtitle">Bệnh nhân: <strong>{{ selectedCancelItem.patientName }}</strong></p>
            </div>
          </div>
          <button type="button" class="dialog-close-btn" :disabled="deskBusy" @click="closeDeskCancel">✕</button>
        </div>
        <form class="dialog-form" @submit.prevent="submitDeskCancel">
          <div class="form-group">
            <label for="desk-cancel-reason">Lý do hủy <span class="req">*</span></label>
            <textarea
              id="desk-cancel-reason"
              v-model.trim="deskCancelReason"
              rows="3"
              maxlength="500"
              placeholder="Nhập lý do hủy lịch khám..."
              required
            ></textarea>
          </div>
          <label class="confirm-checkbox">
            <input v-model="deskCancelConfirmed" type="checkbox" />
            <span>Xác nhận hủy cuộc hẹn này và giải phóng giờ khám.</span>
          </label>
          <div class="dialog-actions">
            <button type="button" class="app-btn app-btn--secondary" :disabled="deskBusy" @click="closeDeskCancel">Hủy</button>
            <button type="submit" class="app-btn app-btn--danger" :disabled="deskBusy || !deskCancelConfirmed || !deskCancelReason.trim()">
              Xác nhận hủy hẹn
            </button>
          </div>
        </form>
      </div>
    </dialog>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import {
  bookingRequestsService,
  type BookingRequestRow,
  type BookingApprovalResult
} from '@/services/bookingRequests';
import {
  appointmentsService,
  type AppointmentResponse
} from '@/services/appointments';
import {
  patientsService,
  type PatientSummary,
  type PatientDetails,
  type PatientPayload
} from '@/services/patients';
import { patientGuardiansService } from '@/services/patientGuardians';
import {
  qrPatientIntakeService,
  type ScannedIdentity
} from '@/services/qrPatientIntake';
import { useBookingSchedule } from '@/composables/useBookingSchedule';
import { isUnderFourteen } from '@/services/patientAge';
import { capitalizeVietnameseName } from '@/utils/vietnameseName';
import VietnameseDateOfBirthInput from '@/components/VietnameseDateOfBirthInput.vue';
import BookingRequestApproval from '@/components/BookingRequestApproval.vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import CameraCccdScanner from '@/components/CameraCccdScanner.vue';

// ============================================================================
// STATE CHUNG
// ============================================================================
type AppointmentTab = 'all' | 'requests' | 'desk';
const route = useRoute();
const activeTab = ref<AppointmentTab>('all');

function syncTabFromQuery(tabQuery: unknown) {
  if (tabQuery === 'requests' || tabQuery === 'desk' || tabQuery === 'all') {
    activeTab.value = tabQuery;
  } else if (tabQuery === 'counter') {
    activeTab.value = 'all';
    openBookingModal();
  }
}

const globalSuccess = ref('');
const globalError = ref('');

function showSuccess(msg: string) {
  globalSuccess.value = msg;
  globalError.value = '';
  setTimeout(() => {
    if (globalSuccess.value === msg) globalSuccess.value = '';
  }, 6000);
}

function showError(msg: string) {
  globalError.value = msg;
}

const todayDateStr = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit'
}).format(new Date());
function vietnamDateTime(): { date: string; time: string } {
  const date = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit'
  }).format(new Date());
  const time = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  }).format(new Date());
  return { date, time };
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  const parts = iso.slice(0, 10).split('-');
  if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`;
  return iso;
}

function isChildBirthDate(dateOfBirth?: string | null): boolean {
  return !!dateOfBirth && isUnderFourteen(dateOfBirth);
}

function parseRequestNotes(notes?: string | null): { guardian: string | null; note: string | null } {
  if (!notes) return { guardian: null, note: null };
  const match = notes.match(/\[Giám hộ:\s*([^\]]+)\]/);
  const guardian = match ? match[1].trim() : null;
  const cleaned = notes.replace(/\[Giám hộ:\s*[^\]]+\]/g, '').trim();
  return { guardian, note: cleaned || null };
}

function onCounterNameInput() {
  if (!matchedPatient.value) form.value.fullName = capitalizeVietnameseName(form.value.fullName, false);
}

function onCounterNameBlur() {
  if (!matchedPatient.value) form.value.fullName = capitalizeVietnameseName(form.value.fullName, true);
  void checkExistingPatient();
}

function onCounterGuardianInput() {
  form.value.guardianName = capitalizeVietnameseName(form.value.guardianName, false);
}

function onCounterGuardianBlur() {
  form.value.guardianName = capitalizeVietnameseName(form.value.guardianName, true);
}

function formatStatus(st: string): string {
  switch (st) {
    case 'Scheduled': return 'Đang chờ khám';
    case 'CheckedIn': return 'Đã tiếp đón';
    case 'Completed': return 'Hoàn tất';
    case 'Cancelled': return 'Đã hủy';
    default: return st;
  }
}

// ============================================================================
// 1. LOGIC YÊU CẦU TRỰC TUYẾN CHỜ DUYỆT (ONLINE REQUESTS)
// ============================================================================
const requestsRows = ref<BookingRequestRow[]>([]);
const requestsPage = ref(1);
const requestsHasNext = ref(false);
const requestsBusy = ref(false);
const requestsApprovalBusy = ref(false);
const approvingRequestId = ref<number | null>(null);
const approvingRequest = ref<BookingRequestRow | null>(null);

const rejectingRequest = ref<BookingRequestRow | null>(null);
const rejectReason = ref('');
const rejectConfirmed = ref(false);
const rejectError = ref('');

let approvalDialogEl: HTMLDialogElement | null = null;
let rejectDialogEl: HTMLDialogElement | null = null;

function bindApprovalDialog(element: unknown) {
  if (typeof HTMLDialogElement !== 'undefined' && element instanceof HTMLDialogElement) {
    approvalDialogEl = element;
    if (!element.open) element.showModal();
  } else if (element === null) {
    approvalDialogEl = null;
  }
}

function bindRejectDialog(element: unknown) {
  if (typeof HTMLDialogElement !== 'undefined' && element instanceof HTMLDialogElement) {
    rejectDialogEl = element;
    if (!element.open) element.showModal();
  } else if (element === null) {
    rejectDialogEl = null;
  }
}

async function loadRequests(page = 1) {
  requestsBusy.value = true;
  approvalDialogEl?.close();
  rejectDialogEl?.close();
  approvingRequest.value = null;
  approvingRequestId.value = null;
  rejectingRequest.value = null;
  rejectError.value = '';
  try {
    const data = await bookingRequestsService.list(page);
    requestsRows.value = data.items;
    requestsPage.value = page;
    requestsHasNext.value = data.hasNextPage;
  } catch (err: any) {
    showError(err.message || 'Không thể tải danh sách yêu cầu đặt lịch trực tuyến.');
  } finally {
    requestsBusy.value = false;
  }
}

function closeApproveModal() {
  if (requestsApprovalBusy.value) return;
  approvalDialogEl?.close();
  approvingRequest.value = null;
  approvingRequestId.value = null;
}

function onApprovalDialogCancel(event: Event) {
  if (event.target !== approvalDialogEl) return;
  event.preventDefault();
  closeApproveModal();
}

function startApprove(req: BookingRequestRow) {
  if (requestsApprovalBusy.value || requestsBusy.value) return;
  closeRejectModal();
  approvingRequest.value = req;
  approvingRequestId.value = req.id;
}

function closeRejectModal() {
  if (requestsBusy.value) return;
  rejectDialogEl?.close();
  rejectingRequest.value = null;
  rejectReason.value = '';
  rejectConfirmed.value = false;
  rejectError.value = '';
}

function onRejectDialogCancel(event: Event) {
  if (event.target !== rejectDialogEl) return;
  event.preventDefault();
  closeRejectModal();
}

function startReject(req: BookingRequestRow) {
  if (requestsApprovalBusy.value || requestsBusy.value) return;
  closeApproveModal();
  rejectingRequest.value = req;
  rejectReason.value = '';
  rejectConfirmed.value = false;
  rejectError.value = '';
}

function onApprovedRequest(result: BookingApprovalResult) {
  approvalDialogEl?.close();
  approvingRequest.value = null;
  approvingRequestId.value = null;
  showSuccess(`Đã duyệt yêu cầu thành công! Lịch hẹn #${result.appointmentId} đã được tạo.`);
  loadRequests(requestsPage.value);
  loadDesk(1);
}

async function submitReject() {
  if (!rejectingRequest.value || !rejectConfirmed.value || !rejectReason.value.trim()) return;
  requestsBusy.value = true;
  rejectError.value = '';
  try {
    const targetName = rejectingRequest.value.fullName;
    await bookingRequestsService.reject(
      rejectingRequest.value.id,
      rejectingRequest.value.version,
      rejectReason.value.trim()
    );
    rejectDialogEl?.close();
    rejectingRequest.value = null;
    rejectReason.value = '';
    rejectConfirmed.value = false;
    showSuccess(`Đã từ chối yêu cầu của ${targetName} và giải phóng khung giờ.`);
    loadRequests(requestsPage.value);
  } catch (err: any) {
    const msg = err.response?.data?.message || 'Không thể từ chối yêu cầu. Vui lòng thử lại.';
    rejectError.value = msg;
    showError(msg);
  } finally {
    requestsBusy.value = false;
  }
}

// ============================================================================
// 2. LOGIC LỊCH KHÁM ĐÃ CHỐT THEO NGÀY (DESK APPOINTMENTS)
// ============================================================================
const deskDate = ref(todayDateStr);
const deskStatus = ref('Scheduled');
const deskRows = ref<AppointmentResponse[]>([]);
const deskPage = ref(1);
const deskTotal = ref(0);
const deskHasNext = ref(false);
const deskBusy = ref(false);
const deskLoading = ref(false);

const cancelDialog = ref<HTMLDialogElement | null>(null);
const selectedCancelItem = ref<AppointmentResponse | null>(null);
const deskCancelReason = ref('');
const deskCancelConfirmed = ref(false);

async function loadDesk(page = 1) {
  deskLoading.value = true;
  deskBusy.value = true;
  try {
    const data = await appointmentsService.getForDesk(
      deskDate.value,
      page,
      deskStatus.value || undefined
    );
    deskRows.value = data.items;
    deskPage.value = page;
    deskTotal.value = data.totalCount;
    deskHasNext.value = data.hasNextPage;
  } catch {
    // Không block view chính nếu API desk lỗi
  } finally {
    deskLoading.value = false;
    deskBusy.value = false;
  }
}

function chooseDeskCancel(item: AppointmentResponse) {
  selectedCancelItem.value = item;
  deskCancelReason.value = '';
  deskCancelConfirmed.value = false;
  cancelDialog.value?.showModal();
}

function closeDeskCancel() {
  selectedCancelItem.value = null;
  cancelDialog.value?.close();
}

async function submitDeskCancel() {
  if (!selectedCancelItem.value || !deskCancelConfirmed.value || !deskCancelReason.value.trim()) return;
  deskBusy.value = true;
  try {
    await appointmentsService.cancel(selectedCancelItem.value.appointmentId, deskCancelReason.value.trim());
    showSuccess(`Đã hủy lịch hẹn #${selectedCancelItem.value.appointmentId} thành công.`);
    closeDeskCancel();
    loadDesk(deskPage.value);
  } catch (err: any) {
    showError(err.response?.data?.message || 'Không thể hủy lịch hẹn.');
  } finally {
    deskBusy.value = false;
  }
}

function refreshAll() {
  loadRequests(requestsPage.value);
  loadDesk(deskPage.value);
}

// ============================================================================
// 3. LOGIC MODAL ĐẶT LỊCH KHÁM (ĐIỀN THỦ CÔNG / QUÉT CCCD TỰ ĐỘNG CHECK HỒ SƠ)
// ============================================================================
const isBookingModalOpen = ref(false);
const isCameraOpen = ref(false);
const modalSubmitting = ref(false);
const modalError = ref('');

// Bệnh nhân đã nhận diện được trong hệ thống
const matchedPatient = ref<PatientSummary | PatientDetails | null>(null);
const patientCandidates = ref<PatientSummary[]>([]);
const checkingPatient = ref(false);
const lastScannedIdentity = ref<ScannedIdentity | null>(null);
const guardianLookupKeyword = ref('');
const guardianLookupBusy = ref(false);
const guardianLookupSearched = ref(false);
const guardianCandidates = ref<PatientSummary[]>([]);
const selectedGuardianCandidateId = ref(0);
const confirmedGuardianPatient = ref<PatientSummary | null>(null);

// Form nhập thông tin đặt lịch (tương tự trang web + thêm CCCD & Địa chỉ)
const form = ref({
  fullName: '',
  phone: '',
  dateOfBirth: '',
  gender: 'Male' as 'Male' | 'Female' | 'Other',
  cccd: '',
  documentType: 'citizenId' as 'citizenId' | 'passport',
  nationalityCode: '',
  address: '',
  email: '',
  guardianName: '',
  guardianRelation: 'Bố/Mẹ',
  guardianVerifiedAtCounter: false,
  appointmentDate: todayDateStr,
  slot: '',
  notes: ''
});

// Dùng mốc ngày Việt Nam thống nhất với check-in và booking công khai.
const isChild = computed(() => !!form.value.dateOfBirth && isUnderFourteen(form.value.dateOfBirth));

watch(isChild, (child) => {
  if (child) {
    form.value.cccd = '';
    form.value.nationalityCode = '';
    lastScannedIdentity.value = null;
    isCameraOpen.value = false;
  }
  guardianLookupKeyword.value = '';
  guardianLookupSearched.value = false;
  guardianCandidates.value = [];
  selectedGuardianCandidateId.value = 0;
  confirmedGuardianPatient.value = null;
});

async function searchGuardianCandidates() {
  const keyword = guardianLookupKeyword.value.trim() || form.value.phone.trim();
  if (!keyword) return;
  guardianLookupKeyword.value = keyword;
  guardianLookupBusy.value = true;
  guardianLookupSearched.value = true;
  confirmedGuardianPatient.value = null;
  selectedGuardianCandidateId.value = 0;
  try {
    const page = await patientsService.search({ keyword, page: 1, pageSize: 20 });
    guardianCandidates.value = page.items.filter(
      p => !isUnderFourteen(p.dateOfBirth) && p.patientId !== matchedPatient.value?.patientId
    );
  } catch {
    guardianCandidates.value = [];
  } finally {
    guardianLookupBusy.value = false;
  }
}

function confirmSelectedGuardian() {
  const chosen = guardianCandidates.value.find(c => c.patientId === selectedGuardianCandidateId.value);
  if (!chosen || chosen.hasUserAccount === false) return;
  confirmedGuardianPatient.value = chosen;
  if (!form.value.guardianName.trim()) {
    form.value.guardianName = chosen.fullName;
  }
  form.value.guardianVerifiedAtCounter = true;
}

function clearConfirmedGuardian() {
  confirmedGuardianPatient.value = null;
  selectedGuardianCandidateId.value = 0;
}

const { slots, revision: scheduleRevision, loading: scheduleLoading, load: loadSchedule } = useBookingSchedule(false);

function resetForm() {
  form.value = {
    fullName: '',
    phone: '',
    dateOfBirth: '',
    gender: 'Male',
    cccd: '',
    documentType: 'citizenId',
    nationalityCode: '',
    address: '',
    email: '',
    guardianName: '',
    guardianRelation: 'Bố/Mẹ',
    guardianVerifiedAtCounter: false,
    appointmentDate: todayDateStr,
    slot: '',
    notes: ''
  };
  matchedPatient.value = null;
  patientCandidates.value = [];
  lastScannedIdentity.value = null;
  guardianLookupKeyword.value = '';
  guardianLookupSearched.value = false;
  guardianCandidates.value = [];
  selectedGuardianCandidateId.value = 0;
  confirmedGuardianPatient.value = null;
  modalError.value = '';
}

function openBookingModal() {
  resetForm();
  isBookingModalOpen.value = true;
  void loadSchedule();
}

function closeBookingModal() {
  if (modalSubmitting.value) return;
  isBookingModalOpen.value = false;
}

// Tự động kiểm tra hồ sơ bệnh nhân khi Lễ tân điền CCCD hoặc Số điện thoại
let checkSeq = 0;
async function checkExistingPatient() {
  const currentSeq = ++checkSeq;
  const cccd = form.value.cccd.trim();
  const phone = form.value.phone.trim();
  matchedPatient.value = null;
  patientCandidates.value = [];
  modalError.value = '';
  if (!cccd && (!phone || !form.value.fullName.trim() || !form.value.dateOfBirth)) return;

  checkingPatient.value = true;
  try {
    // CCCD đối soát chính xác; không tự nhận diện theo số điện thoại dùng chung.
    if (form.value.documentType === 'citizenId' && /^[0-9]{12}$/.test(cccd)) {
      const check = await qrPatientIntakeService.lookup(cccd);
      if (currentSeq !== checkSeq) return;
      if (check.existing && !check.existing.isActive) {
        modalError.value = 'Hồ sơ theo CCCD đã ngừng hoạt động. Vui lòng nhờ Admin đối soát trước khi đặt lịch.';
        return;
      }
      if (check.existing?.isActive) {
        const patient = await patientsService.getById(check.existing.patientId);
        if (currentSeq === checkSeq) {
          matchedPatient.value = patient;
          // Nếu người nhập có thông tin mâu thuẫn, yêu cầu đối chiếu thay vì ghi đè âm thầm.
          if (form.value.fullName && form.value.fullName.trim().toLocaleLowerCase('vi') !== patient.fullName.trim().toLocaleLowerCase('vi') ||
              form.value.dateOfBirth && form.value.dateOfBirth !== patient.dateOfBirth.slice(0, 10) ||
              form.value.phone && form.value.phone !== patient.phone) {
            matchedPatient.value = null;
            modalError.value = 'Thông tin trên hồ sơ không khớp dữ liệu đã nhập. Vui lòng kiểm tra lại trước khi tiếp tục.';
            return;
          }
          if (!form.value.fullName) form.value.fullName = patient.fullName;
          if (!form.value.phone) form.value.phone = patient.phone;
          if (!form.value.dateOfBirth) form.value.dateOfBirth = patient.dateOfBirth.slice(0, 10);
          if (!form.value.gender && patient.gender) form.value.gender = patient.gender as any;
          if (!form.value.address && patient.address) form.value.address = patient.address;
          return;
        }
      }
    }
    if (!form.value.fullName.trim() || !form.value.dateOfBirth || !/^0[0-9]{9}$/.test(phone)) return;
    const searchRes = await patientsService.search({ keyword: form.value.fullName.trim(), page: 1, pageSize: 50 });
    if (currentSeq !== checkSeq) return;
    const candidates = searchRes.items.filter(patient =>
      patient.phone === phone &&
      patient.dateOfBirth.slice(0, 10) === form.value.dateOfBirth &&
      patient.fullName.trim().toLocaleLowerCase('vi') === form.value.fullName.trim().toLocaleLowerCase('vi')
    );
    if (candidates.length) {
      if (currentSeq !== checkSeq) return;
      patientCandidates.value = candidates;
    }
  } catch {
    if (currentSeq === checkSeq) matchedPatient.value = null;
  } finally {
    if (currentSeq === checkSeq) checkingPatient.value = false;
  }
}

function onCounterDocumentTypeChanged() {
  form.value.cccd = '';
  form.value.nationalityCode = '';
  lastScannedIdentity.value = null;
  matchedPatient.value = null;
  patientCandidates.value = [];
  modalError.value = '';
}

function selectExistingPatient(patient: PatientSummary) {
  matchedPatient.value = patient;
  patientCandidates.value = [];
  form.value.fullName = patient.fullName;
  form.value.phone = patient.phone;
  form.value.dateOfBirth = patient.dateOfBirth.slice(0, 10);
  form.value.gender = (patient.gender as 'Male' | 'Female' | 'Other') || 'Other';
}

// Xử lý khi quét thẻ CCCD thành công: tự động đổ vào form và kiểm tra hồ sơ
async function handleCccdScanned(data: { raw: string; identity: ScannedIdentity }) {
  isCameraOpen.value = false;
  modalError.value = '';
  if (isUnderFourteen(data.identity.dateOfBirth)) {
    modalError.value = 'Trẻ dưới 14 tuổi không dùng CCCD riêng. Vui lòng nhập thông tin người giám hộ.';
    return;
  }
  lastScannedIdentity.value = data.identity;

  // Điền tự động các trường từ CCCD
  form.value.fullName = data.identity.fullName;
  form.value.dateOfBirth = data.identity.dateOfBirth;
  form.value.gender = data.identity.gender;
  form.value.cccd = data.identity.citizenId;
  form.value.documentType = 'citizenId';
  form.value.nationalityCode = '';
  form.value.address = data.identity.address;

  // Kiểm tra hồ sơ ngay
  await checkExistingPatient();
}

// Kiểm tra tính hợp lệ của toàn bộ form đặt lịch
const isFormValid = computed(() => {
  const f = form.value;
  if (!f.fullName.trim() || f.fullName.trim().length < 2) return false;
  if (!/^0[0-9]{9}$/.test(f.phone.trim())) return false;
  if (!f.dateOfBirth || f.dateOfBirth.length !== 10) return false;
  if (isChild.value && !f.guardianName.trim()) return false;
  if (isChild.value && !f.guardianVerifiedAtCounter) return false;
  if (!isChild.value && f.documentType === 'citizenId' && !/^[0-9]{12}$/.test(f.cccd.trim())) return false;
  if (!isChild.value && f.documentType === 'passport' &&
      (!/^[A-Za-z0-9]{6,12}$/.test(f.cccd.trim()) || !/^[A-Za-z]{2,3}$/.test(f.nationalityCode.trim()))) return false;
  if (patientCandidates.value.length && !matchedPatient.value) return false;
  if (!f.appointmentDate || !f.slot) return false;
  return true;
});

// XÁC NHẬN ĐẶT LỊCH: Tạo hồ sơ nếu chưa có -> Tạo lịch hẹn (coi như đã duyệt)
async function submitBookingModal() {
  if (!isFormValid.value ||
      modalSubmitting.value) {
    if (isChild.value && !form.value.guardianVerifiedAtCounter)
      modalError.value = 'Vui lòng xác nhận đã đối chiếu người giám hộ trực tiếp tại quầy.';
    else if (!isChild.value && !isFormValid.value)
      modalError.value = 'Vui lòng nhập và đối chiếu CCCD hoặc hộ chiếu cùng quốc gia cấp trước khi xác nhận.';
    return;
  }
  modalSubmitting.value = true;
  modalError.value = '';

  try {
    let patientId = 0;
    let patientName = form.value.fullName.trim();

    // 1. Nếu đã nhận diện được bệnh nhân cũ -> dùng luôn ID
    if (matchedPatient.value) {
      patientId = matchedPatient.value.patientId;
      patientName = matchedPatient.value.fullName;
    } else {
      // 2. Nếu chưa có hồ sơ -> Tạo mới
      // Nếu có CCCD từ quét mã: dùng qr-intake để lưu cả dữ liệu thẻ
      if (!isChild.value && form.value.documentType === 'citizenId' && /^[0-9]{12}$/.test(form.value.cccd.trim())) {
        const payload: PatientPayload = {
          fullName: form.value.fullName.trim(),
          dateOfBirth: form.value.dateOfBirth,
          gender: form.value.gender,
          phone: form.value.phone.trim(),
          email: form.value.email.trim() || null,
          address: form.value.address.trim() || null,
          confirmNotDuplicate: true
        };
        const created = lastScannedIdentity.value && form.value.cccd === lastScannedIdentity.value.citizenId
          ? await qrPatientIntakeService.create(lastScannedIdentity.value, payload)
          : await qrPatientIntakeService.createWithCitizenId(form.value.cccd.trim(), payload);
        patientId = created.patientId;
        patientName = created.fullName;
      } else {
        // Tạo hồ sơ thủ công bình thường
        const payload: PatientPayload = {
          fullName: form.value.fullName.trim(),
          dateOfBirth: form.value.dateOfBirth,
          gender: form.value.gender,
          phone: form.value.phone.trim(),
          email: form.value.email.trim() || null,
          address: form.value.address.trim() || null,
          confirmNotDuplicate: true
        };
        const created = await patientsService.create(payload);
        patientId = created.patientId;
        patientName = created.fullName;
      }
    }

    // 3. Tạo lịch hẹn chính thức (coi như đã duyệt)
    let notesText = form.value.notes.trim() || '';
    const appointment = await appointmentsService.create({
      patientId,
      appointmentDate: form.value.appointmentDate,
      slotTime: `${form.value.slot}:00`,
      notes: notesText || null,
      expectedScheduleRevision: scheduleRevision.value,
      citizenId: !isChild.value ? form.value.cccd.trim() : null,
      contactPhone: form.value.phone.trim(),
      guardianFullName: isChild.value ? form.value.guardianName.trim() : null,
      guardianRelationship: isChild.value ? form.value.guardianRelation : null,
      guardianVerifiedAtCounter: isChild.value && form.value.guardianVerifiedAtCounter,
      documentType: isChild.value ? null : form.value.documentType === 'passport' ? 2 : 1,
      nationalityCode: !isChild.value && form.value.documentType === 'passport' ? form.value.nationalityCode.trim().toUpperCase() : null
    });

    let guardianLinked = false;
    if (isChild.value && confirmedGuardianPatient.value && patientId > 0) {
      try {
        await patientGuardiansService.assign({
          guardianPatientId: confirmedGuardianPatient.value.patientId,
          wardPatientId: patientId,
          relationship: form.value.guardianRelation,
          verifiedAtCounter: true,
          notes: 'Liên kết người giám hộ khi đặt lịch tại quầy'
        });
        guardianLinked = true;
      } catch {
        // Non-fatal if ward already has an active guardian or needs manual follow-up in PatientDetailView.
      }
    }

    // Thông báo thành công kèm số thứ tự (nếu ca hôm nay)
    let successMessage = `Đặt lịch thành công cho bệnh nhân ${patientName} (Mã lịch #${appointment.appointmentId} lúc ${form.value.slot}). Lịch hẹn đã được duyệt và xác nhận!`;
    if (appointment.queueNumber) {
      successMessage += ` Số thứ tự tiếp đón: #${appointment.queueNumber}.`;
    }
    if (appointment.patientAccountCreated) {
      successMessage += ' Đã tạo tài khoản cá nhân; người bệnh cần kích hoạt lần đầu bằng OTP và đổi mật khẩu.';
    } else if (isChild.value) {
      successMessage += guardianLinked
        ? ' Đã liên kết tài khoản người giám hộ.'
        : (!matchedPatient.value?.hasActiveGuardian ? ' Hồ sơ trẻ đang mang trạng thái Thiếu tài khoản (chưa liên kết tài khoản người giám hộ).' : '');
    }

    closeBookingModal();
    showSuccess(successMessage);

    // Làm mới lại dữ liệu cả 2 danh sách
    loadRequests(1);
    loadDesk(1);
  } catch (err: any) {
    modalError.value =
      err.response?.data?.message || err.message || 'Không thể hoàn tất đặt lịch. Vui lòng kiểm tra lại thông tin.';
  } finally {
    modalSubmitting.value = false;
  }
}

// Khởi chạy khi mount
watch(
  () => route?.query?.tab,
  (tabQuery) => {
    syncTabFromQuery(tabQuery);
  }
);

onMounted(() => {
  syncTabFromQuery(route?.query?.tab);
  loadRequests(1);
  loadDesk(1);
});
</script>

<style scoped>
.appointments-page {
  padding: 1.5rem;
  max-width: 1300px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 1.75rem;
}

/* Header */
.header-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: linear-gradient(135deg, #102e56 0%, #1a4275 100%);
  color: #ffffff;
  padding: 1.5rem 2rem;
  border-radius: 14px;
  box-shadow: 0 4px 16px rgba(16, 46, 86, 0.15);
  gap: 1.5rem;
  flex-wrap: wrap;
}

.header-icon-box {
  font-size: 2.5rem;
  background: rgba(255, 255, 255, 0.12);
  width: 60px;
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
}

.header-titles {
  flex: 1;
}

.header-titles h1 {
  font-size: 1.45rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
}

.header-titles .subtitle {
  font-size: 0.92rem;
  opacity: 0.9;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

/* Nút bấm giao diện chung */
.app-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  padding: 0.55rem 1.1rem;
  font-size: 0.9rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 0.15s ease;
  white-space: nowrap;
}

.app-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.app-btn--primary {
  background: #2563eb;
  color: #ffffff;
}

.app-btn--primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.app-btn--outline {
  background: #ffffff;
  color: #102e56;
  border-color: #cbd5e1;
}

.app-btn--outline:hover:not(:disabled) {
  background: #f8fafc;
  border-color: #94a3b8;
}

.app-btn--success {
  background: #16a34a;
  color: #ffffff;
}

.app-btn--success:hover:not(:disabled) {
  background: #15803d;
}

.app-btn--danger-outline {
  background: transparent;
  color: #dc2626;
  border-color: #fca5a5;
}

.app-btn--danger-outline:hover:not(:disabled) {
  background: #fef2f2;
  border-color: #ef4444;
}

.app-btn--danger {
  background: #dc2626;
  color: #ffffff;
}

.app-btn--danger:hover:not(:disabled) {
  background: #b91c1c;
}

.app-btn--secondary {
  background: #f1f5f9;
  color: #334155;
  border-color: #cbd5e1;
}

.app-btn--secondary:hover:not(:disabled) {
  background: #e2e8f0;
}

.app-btn--sm {
  padding: 0.4rem 0.8rem;
  font-size: 0.82rem;
}

.app-btn--xs {
  padding: 0.25rem 0.55rem;
  font-size: 0.78rem;
}

.btn-book-appointment {
  background: #e5b95e;
  color: #102e56;
  font-weight: 700;
  box-shadow: 0 2px 8px rgba(229, 185, 94, 0.4);
}

.btn-book-appointment:hover:not(:disabled) {
  background: #d8aa4e;
}

/* Thông báo alert */
.alert-box {
  padding: 0.85rem 1.25rem;
  border-radius: 8px;
  font-size: 0.92rem;
  font-weight: 500;
}

.alert-success {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.alert-error {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fecaca;
}

/* Thanh chuyển Tab */
.appointments-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
  padding: 0.4rem;
  background: #f1f5f9;
  border: 1px solid #dbe5ed;
  border-radius: 12px;
}

.appointments-tab-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.45rem;
  min-height: 42px;
  padding: 0.5rem 1rem;
  border: 1px solid transparent;
  border-radius: 9px;
  background: transparent;
  color: #475569;
  font-size: 0.9rem;
  font-weight: 700;
  cursor: pointer;
  transition: background-color 0.18s ease, color 0.18s ease, box-shadow 0.18s ease, transform 0.15s ease;
}

.appointments-tab-btn:hover:not(.is-active) {
  background: #e2e8f0;
  color: #102e56;
}

.appointments-tab-btn.is-active {
  background: #102e56;
  color: #ffffff;
  box-shadow: 0 3px 10px rgba(16, 46, 86, 0.22);
}

.appointments-tab-btn:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.tab-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.5rem;
  padding: 0.1rem 0.45rem;
  border-radius: 9999px;
  background: rgba(15, 23, 42, 0.09);
  color: inherit;
  font-size: 0.76rem;
  font-weight: 750;
}

.appointments-tab-btn.is-active .tab-badge {
  background: rgba(255, 255, 255, 0.2);
}

.tab-badge--warn {
  background: #fef3c7;
  color: #b45309;
}

.appointments-tab-btn.is-active .tab-badge--warn {
  background: #e5b95e;
  color: #102e56;
}

.appointments-tab-stage {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  min-width: 0;
}

/* Section Card */
.section-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  padding: 1.5rem;
  min-width: 0;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 1rem;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.section-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.section-icon {
  font-size: 1.6rem;
}

.section-header h2 {
  font-size: 1.15rem;
  font-weight: 700;
  color: #0f172a;
  margin: 0;
}

.section-desc {
  font-size: 0.85rem;
  color: #64748b;
  margin: 0.15rem 0 0;
}

.badge-count {
  display: inline-block;
  padding: 0.35rem 0.85rem;
  border-radius: 9999px;
  font-size: 0.82rem;
  font-weight: 600;
}

.badge-warning {
  background: #fef3c7;
  color: #b45309;
}

.badge-neutral {
  background: #f1f5f9;
  color: #64748b;
}

/* Danh sách yêu cầu */
.empty-requests-state {
  text-align: center;
  padding: 3rem 1.5rem;
  background: #f8fafc;
  border-radius: 10px;
  border: 1px dashed #cbd5e1;
}

.empty-icon {
  font-size: 2.8rem;
  margin-bottom: 0.5rem;
}

.empty-requests-state h3 {
  font-size: 1.1rem;
  font-weight: 600;
  color: #334155;
  margin: 0 0 0.4rem;
}

.empty-requests-state p {
  color: #64748b;
  font-size: 0.9rem;
  margin: 0 0 1.25rem;
}

.requests-page-frame {
  min-width: 0;
}

.requests-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 330px), 1fr));
  gap: 1.25rem;
  list-style: none;
  padding: 0;
  margin: 0;
}

.request-card {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  min-width: 0;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.03);
  transition: border-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease;
}

.request-card:hover {
  border-color: #cbd5e1;
  box-shadow: 0 6px 18px rgba(16, 46, 86, 0.07);
  transform: translateY(-2px);
}

.request-card--active {
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.16);
}

.request-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.patient-name {
  font-size: 1.05rem;
  font-weight: 700;
  color: #0f172a;
  margin: 0 0 0.25rem;
}

.badge-returning {
  display: inline-block;
  background: #dbeafe;
  color: #1e40af;
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
}

.badge-new {
  display: inline-block;
  background: #f1f5f9;
  color: #475569;
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
}

.badge-child {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  background: #e0f2fe;
  color: #0369a1;
  border: 1px solid #bae6fd;
  font-size: 0.75rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
}

.request-guardian {
  background: #eff8ff;
  border: 1px solid #bfdbfe;
  border-left: 3px solid #1857b6;
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  font-size: 0.84rem;
  color: #102e56;
}

.summary-guardian {
  margin: 0.35rem 0 0;
  padding: 0.45rem 0.65rem;
  background: #eff8ff;
  border-radius: 6px;
  font-size: 0.84rem;
  color: #102e56;
}

.dob-field-label {
  display: block;
  font-size: 0.85rem;
  font-weight: 600;
  color: #334155;
  margin-bottom: 0.35rem;
}

.request-code {
  font-size: 0.78rem;
  font-weight: 600;
  color: #64748b;
  background: #f8fafc;
  padding: 0.2rem 0.45rem;
  border-radius: 4px;
}

.request-details {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  font-size: 0.88rem;
}

.detail-item {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem 0.5rem;
  min-width: 0;
}

.detail-label {
  color: #64748b;
  min-width: 105px;
}

.detail-val {
  color: #1e293b;
  font-weight: 500;
  overflow-wrap: anywhere;
}

.highlight-time {
  color: #0369a1;
  font-weight: 600;
}

.request-notes {
  background: #f8fafc;
  padding: 0.6rem 0.75rem;
  border-radius: 6px;
  font-size: 0.84rem;
  color: #334155;
  border-left: 3px solid #94a3b8;
  overflow-wrap: anywhere;
}

.notes-tag {
  font-weight: 600;
  color: #475569;
}

.request-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  margin-top: auto;
  padding-top: 0.6rem;
  border-top: 1px solid #f1f5f9;
}

.request-actions .app-btn {
  flex: 1 1 8.5rem;
}

.rejection-hint {
  font-size: 0.84rem;
  color: #991b1b;
  background: #fff5f5;
  border: 1px solid #fecaca;
  border-radius: 8px;
  padding: 0.65rem 0.85rem;
  margin: 0;
}

/* Lịch khám đã chốt */
.filters-card {
  display: flex;
  gap: 1rem;
  align-items: flex-end;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.filter-group label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #475569;
}

.filter-group input,
.filter-group select {
  padding: 0.5rem 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font-size: 0.88rem;
}

.btn-filter-submit {
  height: 38px;
}

.confirmed-table-wrap {
  overflow-x: auto;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
}

.confirmed-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}

.confirmed-table th {
  background: #f8fafc;
  color: #475569;
  font-weight: 600;
  padding: 0.75rem 1rem;
  text-align: left;
  border-bottom: 1px solid #e2e8f0;
}

.confirmed-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid #f1f5f9;
  color: #1e293b;
}

.confirmed-table tr:hover td {
  background: #f8fafc;
}

.cell-time {
  white-space: nowrap;
}

.badge-queue {
  display: inline-block;
  margin-left: 0.4rem;
  background: #dbeafe;
  color: #1e40af;
  font-size: 0.75rem;
  font-weight: 700;
  padding: 0.15rem 0.45rem;
  border-radius: 4px;
}

.patient-cell-name {
  font-weight: 600;
}

.patient-cell-name {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
  font-weight: 600;
  color: #0f172a;
}

.cell-child-badge,
.cell-missing-badge,
.cell-linked-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.12rem 0.45rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
}

.cell-child-badge {
  background: #dbeafe;
  color: #1e40af;
}

.cell-missing-badge {
  background: #fef3c7;
  border: 1px solid #f59e0b;
  color: #92400e;
}

.cell-linked-badge {
  background: #dcfce7;
  color: #166534;
}

.patient-cell-code {
  font-size: 0.78rem;
  color: #64748b;
}

.status-pill {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  border-radius: 9999px;
  font-size: 0.76rem;
  font-weight: 600;
}

.status--scheduled {
  background: #e0f2fe;
  color: #0369a1;
}

.status--checkedin {
  background: #fef3c7;
  color: #b45309;
}

.status--completed {
  background: #dcfce7;
  color: #15803d;
}

.status--cancelled {
  background: #fee2e2;
  color: #b91c1c;
}

.action-buttons {
  display: flex;
  gap: 0.4rem;
}

.action-btn {
  padding: 0.35rem 0.65rem;
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: pointer;
}

.action-btn--cancel {
  background: transparent;
  color: #dc2626;
  border-color: #fca5a5;
}

.action-btn--cancel:hover {
  background: #fef2f2;
}

/* Phân trang */
.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 1.25rem;
  padding-top: 1rem;
  border-top: 1px solid #f1f5f9;
}

.page-info {
  font-size: 0.85rem;
  color: #64748b;
}

.pagination-controls {
  display: flex;
  gap: 0.5rem;
}

/* ============================================================================ */
/* MODAL ĐẶT LỊCH KHÁM TẠI QUẦY */
/* ============================================================================ */
.modal-backdrop {
  position: fixed;
  inset: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(15, 23, 42, 0.6);
  backdrop-filter: blur(4px);
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  overflow-y: auto;
}

.booking-dialog {
  background: #ffffff;
  border-radius: 14px;
  width: 100%;
  max-width: 700px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.2), 0 10px 10px -5px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid #e2e8f0;
  background: #f8fafc;
}

.dialog-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.dialog-icon {
  font-size: 1.8rem;
}

.dialog-header h2 {
  font-size: 1.2rem;
  font-weight: 700;
  color: #0f172a;
  margin: 0;
}

.dialog-desc {
  font-size: 0.82rem;
  color: #64748b;
  margin: 0.15rem 0 0;
}

.btn-close-dialog {
  background: transparent;
  border: none;
  font-size: 1.25rem;
  color: #64748b;
  cursor: pointer;
  padding: 0.4rem;
  border-radius: 6px;
}

.btn-close-dialog:hover {
  background: #e2e8f0;
  color: #0f172a;
}

.dialog-body {
  padding: 1.25rem 1.5rem;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 1.15rem;
}

/* Banner quét CCCD nhanh */
.quick-scan-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  border: 1px solid #bfdbfe;
  padding: 0.85rem 1.15rem;
  border-radius: 10px;
  gap: 1rem;
  flex-wrap: wrap;
}

.scan-info strong {
  color: #1e3a8a;
  font-size: 0.92rem;
}

.scan-info p {
  color: #3b82f6;
  font-size: 0.8rem;
  margin: 0.15rem 0 0;
}

.btn-quick-scan {
  background: #1d4ed8;
  color: #ffffff;
  padding: 0.45rem 0.95rem;
  font-size: 0.85rem;
}

.btn-quick-scan:hover {
  background: #1e40af;
}

/* Banners nhận diện hồ sơ tự động */
.recognition-banner {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-size: 0.86rem;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.recognition-loading {
  background: #f8fafc;
  color: #475569;
  border: 1px solid #e2e8f0;
}

.recognition-matched {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.recognition-new {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fde68a;
}

.recog-title {
  font-size: 0.9rem;
}

.badge-code {
  background: rgba(22, 101, 52, 0.15);
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  font-size: 0.8rem;
  margin-left: 0.35rem;
}

.recog-sub {
  margin: 0;
  font-size: 0.78rem;
  opacity: 0.9;
}

/* Form Sections trong Modal */
.form-section {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1rem 1.2rem;
  margin: 0 0 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.section-legend {
  font-size: 0.92rem;
  font-weight: 700;
  color: #1e293b;
  padding: 0 0.4rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.grid-full {
  grid-column: 1 / -1;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.form-group label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #334155;
}

.req {
  color: #dc2626;
}

.optional {
  font-size: 0.75rem;
  font-weight: 400;
  color: #64748b;
}

.form-group input,
.form-group select,
.form-group textarea {
  padding: 0.55rem 0.75rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font-size: 0.88rem;
  font-family: inherit;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  outline: none;
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.15);
}

.form-hint {
  font-size: 0.76rem;
  color: #64748b;
}

/* Hộp người giám hộ cho trẻ em */
.guardian-box {
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  border-radius: 8px;
  padding: 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.child-badge-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
}

.child-badge-notice strong {
  color: #0369a1;
}

.child-badge-notice .field-hint {
  margin: 0.15rem 0 0;
  color: #64748b;
  font-size: 0.78rem;
}

.guardian-lookup-panel {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.65rem;
  border-radius: 8px;
  background: #ffffff;
  border: 1px solid #dbe5ed;
}

.guardian-lookup-label {
  font-size: 0.8rem;
  font-weight: 700;
  color: #1e3a5f;
}

.guardian-lookup-row,
.guardian-candidate-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.guardian-lookup-row input,
.guardian-candidate-picker select {
  flex: 1 1 200px;
  min-width: 0;
  padding: 0.45rem 0.65rem;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  font-size: 0.86rem;
}

.guardian-confirmed-pill {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.65rem;
  border-radius: 6px;
  background: #ecfdf5;
  border: 1px solid #86efac;
  color: #166534;
  font-size: 0.84rem;
}

.guardian-lookup-empty {
  margin: 0;
  font-size: 0.8rem;
  color: #92400e;
}

.link-btn {
  border: 0;
  background: transparent;
  color: #0369a1;
  font-weight: 700;
  cursor: pointer;
  text-decoration: underline;
  padding: 0;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  border-top: 1px solid #e2e8f0;
  background: #f8fafc;
}

.btn-submit-booking {
  padding: 0.55rem 1.25rem;
}

/* Dialog popup dùng chung & Popup duyệt / từ chối */
.custom-dialog {
  box-sizing: border-box;
  margin: auto;
  padding: 0;
  border: 1px solid #dbe5ed;
  border-radius: 16px;
  box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
  width: min(520px, calc(100% - 1.25rem));
  max-height: calc(100dvh - 1.5rem);
  overflow: hidden;
  background: #ffffff;
  color: #1e293b;
}

.custom-dialog:modal {
  overscroll-behavior: contain;
}

.custom-dialog[open] {
  animation: dialogPopIn 0.22s cubic-bezier(0.22, 1, 0.36, 1) both;
}

.custom-dialog::backdrop {
  background: rgba(15, 23, 42, 0.56);
  backdrop-filter: blur(4px);
  overflow: hidden;
  overscroll-behavior: contain;
}

.approval-modal-dialog {
  width: min(720px, calc(100% - 1.25rem));
}

.reject-modal-dialog {
  width: min(540px, calc(100% - 1.25rem));
}

.modal-shell {
  display: flex;
  flex-direction: column;
  max-height: calc(100dvh - 1.5rem);
  min-width: 0;
}

.modal-shell .dialog-body {
  flex: 1 1 auto;
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.dialog-header--danger {
  background: #fff5f5;
  border-bottom-color: #fecaca;
}

.dialog-header--danger h2 {
  color: #991b1b;
}

.modal-title-badges {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.45rem;
}

.approval-request-summary {
  display: grid;
  gap: 0.45rem;
  padding: 0.9rem 1rem;
  border: 1px solid #dbe5ed;
  border-radius: 10px;
  background: #f8fafc;
  overflow-wrap: anywhere;
}

.summary-main-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 0.45rem;
}

.summary-main-row strong {
  color: #0f2d59;
  font-size: 1rem;
}

.summary-meta-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem 1rem;
  font-size: 0.85rem;
  color: #475569;
}

.summary-notes {
  margin: 0.15rem 0 0;
  padding-top: 0.45rem;
  border-top: 1px dashed #cbd5e1;
  font-size: 0.85rem;
  color: #334155;
}

.approval-panel {
  border-top: 1px solid #e2e8f0;
  padding-top: 0.9rem;
}

.dialog-content {
  padding: 1.25rem;
}

.dialog-form {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.confirm-checkbox {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: #475569;
  cursor: pointer;
}

.confirm-checkbox input {
  margin-top: 0.15rem;
  flex: none;
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  gap: 0.75rem;
  color: #64748b;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e2e8f0;
  border-top-color: #2563eb;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@keyframes dialogPopIn {
  from {
    opacity: 0;
    transform: translateY(10px) scale(0.97);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

/* Hiệu ứng chuyển Tab, chuyển Trang, danh sách Card, Modal & Alert */
.appt-tab-switch-enter-active,
.appt-tab-switch-leave-active {
  transition: opacity 0.22s ease, transform 0.22s cubic-bezier(0.22, 1, 0.36, 1);
}

.appt-tab-switch-enter-from {
  opacity: 0;
  transform: translateY(8px);
}

.appt-tab-switch-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

.appt-page-swap-enter-active,
.appt-page-swap-leave-active {
  transition: opacity 0.2s ease, transform 0.2s cubic-bezier(0.22, 1, 0.36, 1);
}

.appt-page-swap-enter-from {
  opacity: 0;
  transform: translateX(10px);
}

.appt-page-swap-leave-to {
  opacity: 0;
  transform: translateX(-10px);
}

.request-card-list-move,
.request-card-list-enter-active,
.request-card-list-leave-active {
  transition: opacity 0.24s ease, transform 0.24s cubic-bezier(0.22, 1, 0.36, 1);
}

.request-card-list-enter-from,
.request-card-list-leave-to {
  opacity: 0;
  transform: scale(0.96) translateY(6px);
}

.request-card-list-leave-active {
  position: relative;
}

.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.2s ease;
}

.modal-fade-enter-active .booking-dialog,
.modal-fade-leave-active .booking-dialog {
  transition: transform 0.22s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.2s ease;
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}

.modal-fade-enter-from .booking-dialog,
.modal-fade-leave-to .booking-dialog {
  opacity: 0;
  transform: translateY(10px) scale(0.97);
}

.alert-slide-enter-active,
.alert-slide-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.alert-slide-enter-from,
.alert-slide-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

@media (max-width: 768px) {
  .appointments-page {
    padding: 1rem;
  }
  .header-banner {
    padding: 1.25rem;
  }
  .section-card {
    padding: 1rem;
  }
  .form-grid {
    grid-template-columns: 1fr;
  }
  .requests-grid {
    grid-template-columns: 1fr;
  }
  .appointments-tabs {
    display: grid;
    grid-template-columns: 1fr;
  }
  .appointments-tab-btn {
    justify-content: space-between;
  }
  .pagination-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
  }
  .pagination-controls {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .dialog-footer {
    flex-wrap: wrap;
    padding: 0.85rem 1rem;
  }
}

@media (prefers-reduced-motion: reduce) {
  .custom-dialog[open],
  .spinner {
    animation: none;
  }
  .appt-tab-switch-enter-active,
  .appt-tab-switch-leave-active,
  .appt-page-swap-enter-active,
  .appt-page-swap-leave-active,
  .request-card-list-move,
  .request-card-list-enter-active,
  .request-card-list-leave-active,
  .modal-fade-enter-active,
  .modal-fade-leave-active,
  .modal-fade-enter-active .booking-dialog,
  .modal-fade-leave-active .booking-dialog,
  .alert-slide-enter-active,
  .alert-slide-leave-active,
  .appointments-tab-btn,
  .request-card {
    transition: none;
  }
}
</style>
