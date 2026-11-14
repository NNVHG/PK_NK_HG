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
    <div v-if="globalSuccess" class="alert-box alert-success" role="status" aria-live="polite">
      ✅ {{ globalSuccess }}
    </div>
    <div v-if="globalError" class="alert-box alert-error" role="alert">
      ⚠️ {{ globalError }}
    </div>

    <!-- ====================================================================== -->
    <!-- PHẦN 1 TRỌNG TÂM: DANH SÁCH KHÁCH HÀNG YÊU CẦU DUYỆT TRỰC TUYẾN -->
    <!-- ====================================================================== -->
    <section class="section-card requests-section" aria-labelledby="requests-heading">
      <div class="section-header">
        <div class="section-title-wrap">
          <span class="section-icon">📑</span>
          <div>
            <h2 id="requests-heading">Yêu cầu đặt hẹn trực tuyến chờ duyệt</h2>
            <p class="section-desc">Khách gửi yêu cầu từ trang chủ. Khi lễ tân duyệt, hệ thống sẽ xác nhận lịch hẹn chính thức.</p>
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
        <ol class="requests-grid" aria-label="Danh sách yêu cầu đặt hẹn trực tuyến">
          <li v-for="req in requestsRows" :key="req.id" class="request-card">
            <div class="request-card-header">
              <div class="patient-main">
                <h3 class="patient-name">{{ req.fullName }}</h3>
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
              <div class="detail-item">
                <span class="detail-label">Nha sĩ yêu cầu:</span>
                <span class="detail-val">👨‍⚕️ {{ req.dentistName || 'Chờ phòng khám phân công' }}</span>
              </div>
            </div>

            <div v-if="req.notes" class="request-notes">
              <span class="notes-tag">Ghi chú:</span> {{ req.notes }}
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

            <!-- Khung duyệt trực tiếp (BookingRequestApproval) -->
            <div v-if="approvingRequestId === req.id" class="inline-approval-box">
              <BookingRequestApproval
                :key="req.id"
                class="approval-panel"
                :request-id="req.id"
                @approved="onApprovedRequest"
                @working="requestsApprovalBusy = $event"
              />
              <button
                type="button"
                class="app-btn app-btn--secondary app-btn--xs btn-cancel-approval"
                :disabled="requestsApprovalBusy"
                @click="approvingRequestId = null"
              >
                Đóng khung duyệt
              </button>
            </div>

            <!-- Khung từ chối trực tiếp -->
            <form v-if="rejectingRequest?.id === req.id" class="inline-reject-box" @submit.prevent="submitReject">
              <h4>Từ chối yêu cầu của {{ req.fullName }}</h4>
              <p class="rejection-hint">Yêu cầu khi bị từ chối sẽ ngay lập tức giải phóng khung giờ đã giữ.</p>
              <div class="form-group">
                <label for="reject-reason">Lý do từ chối <span class="req">*</span></label>
                <textarea
                  id="reject-reason"
                  v-model.trim="rejectReason"
                  rows="2"
                  maxlength="500"
                  placeholder="Nhập lý do gửi đến bệnh nhân..."
                  required
                ></textarea>
              </div>
              <label class="confirm-checkbox">
                <input v-model="rejectConfirmed" type="checkbox" />
                <span>Xác nhận từ chối yêu cầu đặt hẹn này.</span>
              </label>
              <div class="reject-actions">
                <button
                  type="button"
                  class="app-btn app-btn--secondary app-btn--sm"
                  :disabled="requestsBusy"
                  @click="rejectingRequest = null"
                >
                  Hủy
                </button>
                <button
                  class="app-btn app-btn--danger app-btn--sm"
                  type="submit"
                  :disabled="requestsBusy || !rejectConfirmed || !rejectReason.trim()"
                >
                  Xác nhận từ chối
                </button>
              </div>
            </form>
          </li>
        </ol>

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
    <section class="section-card confirmed-section" aria-labelledby="confirmed-heading">
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

      <div v-else class="confirmed-table-wrap">
        <table class="confirmed-table">
          <thead>
            <tr>
              <th>Giờ hẹn</th>
              <th>Mã lịch</th>
              <th>Bệnh nhân</th>
              <th>Nha sĩ phụ trách</th>
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
                <div class="patient-cell-name">{{ item.patientName }}</div>
                <div class="patient-cell-code">#{{ item.patientCode }}</div>
              </td>
              <td>{{ item.dentistName || 'Chưa phân công' }}</td>
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

    <!-- ====================================================================== -->
    <!-- MODAL ĐẶT LỊCH KHÁM: ĐIỀN THỦ CÔNG / QUÉT CCCD TỰ ĐỘNG CHECK HỒ SƠ -->
    <!-- ====================================================================== -->
    <Teleport to="body">
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
            <div class="quick-scan-banner">
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
            <div v-else-if="form.fullName && form.phone.length === 10" class="recognition-banner recognition-new">
              <div class="recog-title">
                ✨ <strong>Bệnh nhân mới (chưa có hồ sơ):</strong>
              </div>
              <p class="recog-sub">Hệ thống sẽ tự động tạo hồ sơ bệnh nhân mới khi bạn bấm "Xác nhận đặt lịch".</p>
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
                      v-model.trim="form.fullName"
                      type="text"
                      placeholder="VD: Nguyễn Văn A"
                      maxlength="100"
                      :disabled="modalSubmitting"
                      required
                      @blur="checkExistingPatient"
                    />
                  </div>
                  <div class="form-group">
                    <label for="form-phone">Số điện thoại liên hệ <span class="req">*</span></label>
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
                    <label for="form-dob">Ngày sinh <span class="req">*</span></label>
                    <input
                      id="form-dob"
                      v-model="form.dateOfBirth"
                      type="date"
                      :max="todayDateStr"
                      :disabled="modalSubmitting"
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
                    <label for="form-cccd">Số CCCD / Định danh</label>
                    <input
                      id="form-cccd"
                      v-model.trim="form.cccd"
                      type="text"
                      placeholder="12 chữ số CCCD (nếu có)"
                      maxlength="12"
                      :disabled="modalSubmitting"
                      @blur="checkExistingPatient"
                    />
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
                    <span>👶 <strong>Bệnh nhân là Trẻ em (&lt; 14 tuổi)</strong></span>
                    <p class="field-hint">Trẻ em không yêu cầu CCCD riêng. Vui lòng cung cấp thông tin người giám hộ đi cùng.</p>
                  </div>
                  <div class="form-grid">
                    <div class="form-group">
                      <label for="guardian-name">Họ tên người giám hộ <span class="req">*</span></label>
                      <input
                        id="guardian-name"
                        v-model.trim="form.guardianName"
                        type="text"
                        placeholder="Họ tên cha / mẹ / người đại diện"
                        maxlength="100"
                        :disabled="modalSubmitting"
                        :required="isChild"
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
    </Teleport>

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
import { computed, onMounted, ref } from 'vue';
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
import {
  qrPatientIntakeService,
  type ScannedIdentity
} from '@/services/qrPatientIntake';
import { useBookingSchedule } from '@/composables/useBookingSchedule';
import BookingRequestApproval from '@/components/BookingRequestApproval.vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import CameraCccdScanner from '@/components/CameraCccdScanner.vue';

// ============================================================================
// STATE CHUNG
// ============================================================================
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

function formatDate(iso: string): string {
  if (!iso) return '—';
  const parts = iso.slice(0, 10).split('-');
  if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`;
  return iso;
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

const rejectingRequest = ref<BookingRequestRow | null>(null);
const rejectReason = ref('');
const rejectConfirmed = ref(false);

async function loadRequests(page = 1) {
  requestsBusy.value = true;
  approvingRequestId.value = null;
  rejectingRequest.value = null;
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

function startApprove(req: BookingRequestRow) {
  rejectingRequest.value = null;
  approvingRequestId.value = req.id;
}

function startReject(req: BookingRequestRow) {
  approvingRequestId.value = null;
  rejectingRequest.value = req;
  rejectReason.value = '';
  rejectConfirmed.value = false;
}

function onApprovedRequest(result: BookingApprovalResult) {
  approvingRequestId.value = null;
  showSuccess(`Đã duyệt yêu cầu thành công! Lịch hẹn #${result.appointmentId} đã được tạo.`);
  loadRequests(requestsPage.value);
  loadDesk(1);
}

async function submitReject() {
  if (!rejectingRequest.value || !rejectConfirmed.value || !rejectReason.value.trim()) return;
  requestsBusy.value = true;
  try {
    await bookingRequestsService.reject(
      rejectingRequest.value.id,
      rejectingRequest.value.version,
      rejectReason.value.trim()
    );
    showSuccess(`Đã từ chối yêu cầu của ${rejectingRequest.value.fullName} và giải phóng khung giờ.`);
    rejectingRequest.value = null;
    loadRequests(requestsPage.value);
  } catch (err: any) {
    showError(err.response?.data?.message || 'Không thể từ chối yêu cầu. Vui lòng thử lại.');
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
const checkingPatient = ref(false);
const lastScannedIdentity = ref<ScannedIdentity | null>(null);

// Form nhập thông tin đặt lịch (tương tự trang web + thêm CCCD & Địa chỉ)
const form = ref({
  fullName: '',
  phone: '',
  dateOfBirth: '',
  gender: 'Male' as 'Male' | 'Female' | 'Other',
  cccd: '',
  address: '',
  email: '',
  guardianName: '',
  guardianRelation: 'Bố/Mẹ',
  appointmentDate: todayDateStr,
  slot: '',
  notes: ''
});

// Kiểm tra đối tượng Trẻ em (< 14 tuổi) theo chuẩn web & §1.6
const isChild = computed(() => {
  if (!form.value.dateOfBirth) return false;
  const birth = new Date(form.value.dateOfBirth);
  const now = new Date();
  let age = now.getFullYear() - birth.getFullYear();
  const m = now.getMonth() - birth.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < birth.getDate())) age--;
  return age < 14;
});

const { slots, revision: scheduleRevision, loading: scheduleLoading, load: loadSchedule } = useBookingSchedule(false);

function resetForm() {
  form.value = {
    fullName: '',
    phone: '',
    dateOfBirth: '',
    gender: 'Male',
    cccd: '',
    address: '',
    email: '',
    guardianName: '',
    guardianRelation: 'Bố/Mẹ',
    appointmentDate: todayDateStr,
    slot: '',
    notes: ''
  };
  matchedPatient.value = null;
  lastScannedIdentity.value = null;
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

  if (!cccd && !phone) {
    matchedPatient.value = null;
    return;
  }

  checkingPatient.value = true;
  try {
    // 1. Kiểm tra ưu tiên theo CCCD nếu có 12 số
    if (cccd.length === 12) {
      const check = await qrPatientIntakeService.lookup(cccd);
      if (currentSeq !== checkSeq) return;
      if (check.existing?.isActive) {
        const patient = await patientsService.getById(check.existing.patientId);
        if (currentSeq === checkSeq) {
          matchedPatient.value = patient;
          // Điền thêm các thông tin chưa có từ hồ sơ
          if (!form.value.fullName) form.value.fullName = patient.fullName;
          if (!form.value.phone) form.value.phone = patient.phone;
          if (!form.value.dateOfBirth) form.value.dateOfBirth = patient.dateOfBirth;
          if (!form.value.gender && patient.gender) form.value.gender = patient.gender as any;
          if (!form.value.address && patient.address) form.value.address = patient.address;
          return;
        }
      }
    }

    // 2. Kiểm tra theo SĐT nếu có 10 số
    if (phone.length === 10) {
      const searchRes = await patientsService.search({ keyword: phone, page: 1, pageSize: 5 });
      if (currentSeq !== checkSeq) return;
      // Tìm người trùng cả tên hoặc sđt
      const match = searchRes.items.find(
        p => p.phone === phone || (form.value.fullName && p.fullName.toLowerCase() === form.value.fullName.toLowerCase())
      );
      if (match) {
        matchedPatient.value = match;
        if (!form.value.fullName) form.value.fullName = match.fullName;
        if (!form.value.dateOfBirth) form.value.dateOfBirth = match.dateOfBirth;
        return;
      }
    }

    if (currentSeq === checkSeq) {
      matchedPatient.value = null;
    }
  } catch {
    if (currentSeq === checkSeq) matchedPatient.value = null;
  } finally {
    if (currentSeq === checkSeq) checkingPatient.value = false;
  }
}

// Xử lý khi quét thẻ CCCD thành công: tự động đổ vào form và kiểm tra hồ sơ
async function handleCccdScanned(data: { raw: string; identity: ScannedIdentity }) {
  isCameraOpen.value = false;
  modalError.value = '';
  lastScannedIdentity.value = data.identity;

  // Điền tự động các trường từ CCCD
  form.value.fullName = data.identity.fullName;
  form.value.dateOfBirth = data.identity.dateOfBirth;
  form.value.gender = data.identity.gender;
  form.value.cccd = data.identity.citizenId;
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
  if (!f.appointmentDate || !f.slot) return false;
  return true;
});

// XÁC NHẬN ĐẶT LỊCH: Tạo hồ sơ nếu chưa có -> Tạo lịch hẹn (coi như đã duyệt)
async function submitBookingModal() {
  if (!isFormValid.value || modalSubmitting.value) return;
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
      if (lastScannedIdentity.value && form.value.cccd === lastScannedIdentity.value.citizenId) {
        const payload: PatientPayload = {
          fullName: form.value.fullName.trim(),
          dateOfBirth: form.value.dateOfBirth,
          gender: form.value.gender,
          phone: form.value.phone.trim(),
          email: form.value.email.trim() || null,
          address: form.value.address.trim() || null,
          confirmNotDuplicate: true
        };
        const created = await qrPatientIntakeService.create(lastScannedIdentity.value, payload);
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
    if (isChild.value && form.value.guardianName) {
      const guardianInfo = `[Người giám hộ: ${form.value.guardianName} (${form.value.guardianRelation})]`;
      notesText = notesText ? `${guardianInfo} ${notesText}` : guardianInfo;
    }

    const appointment = await appointmentsService.create({
      patientId,
      appointmentDate: form.value.appointmentDate,
      slotTime: `${form.value.slot}:00`,
      dentistId: null,
      notes: notesText || null,
      expectedScheduleRevision: scheduleRevision.value
    });

    // Thông báo thành công kèm số thứ tự (nếu ca hôm nay)
    let successMessage = `Đặt lịch thành công cho bệnh nhân ${patientName} (Mã lịch #${appointment.appointmentId} lúc ${form.value.slot}). Lịch hẹn đã được duyệt và xác nhận!`;
    if (appointment.queueNumber) {
      successMessage += ` Số thứ tự tiếp đón: #${appointment.queueNumber}.`;
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
onMounted(() => {
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

/* Section Card */
.section-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  padding: 1.5rem;
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

.requests-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 1.25rem;
  list-style: none;
  padding: 0;
  margin: 0;
}

.request-card {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1.25rem;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.03);
  transition: border-color 0.15s, box-shadow 0.15s;
}

.request-card:hover {
  border-color: #cbd5e1;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
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
  gap: 0.5rem;
}

.detail-label {
  color: #64748b;
  min-width: 105px;
}

.detail-val {
  color: #1e293b;
  font-weight: 500;
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
}

.notes-tag {
  font-weight: 600;
  color: #475569;
}

.request-actions {
  display: flex;
  gap: 0.6rem;
  margin-top: auto;
  padding-top: 0.6rem;
  border-top: 1px solid #f1f5f9;
}

.request-actions .app-btn {
  flex: 1;
}

.inline-approval-box {
  margin-top: 0.75rem;
  padding: 0.85rem;
  background: #f8fafc;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
}

.btn-cancel-approval {
  margin-top: 0.6rem;
  width: 100%;
}

.inline-reject-box {
  margin-top: 0.75rem;
  padding: 0.85rem;
  background: #fff5f5;
  border: 1px solid #fecaca;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.inline-reject-box h4 {
  font-size: 0.92rem;
  font-weight: 700;
  color: #991b1b;
  margin: 0;
}

.rejection-hint {
  font-size: 0.78rem;
  color: #b91c1c;
  margin: 0;
}

.reject-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
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

.child-badge-notice strong {
  color: #0369a1;
}

.child-badge-notice .field-hint {
  margin: 0.15rem 0 0;
  color: #64748b;
  font-size: 0.78rem;
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

/* Dialog hủy */
.custom-dialog {
  padding: 0;
  border: none;
  border-radius: 12px;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.2);
  width: 90%;
  max-width: 500px;
}

.custom-dialog::backdrop {
  background: rgba(15, 23, 42, 0.5);
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
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: #475569;
  cursor: pointer;
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

@media (max-width: 768px) {
  .appointments-page {
    padding: 1rem;
  }
  .header-banner {
    padding: 1.25rem;
  }
  .form-grid {
    grid-template-columns: 1fr;
  }
  .requests-grid {
    grid-template-columns: 1fr;
  }
}
</style>
