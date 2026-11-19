<template>
  <div class="dashboard-container">
    <!-- Patient Portal banner (fallback if patient visits /dashboard) -->
    <div v-if="!isStaff" class="patient-banner">
      <div class="patient-banner-content">
        <div class="patient-banner-text">
          <span class="patient-badge">Cổng Bệnh Nhân</span>
          <h2>Xin chào, {{ authStore.user?.fullName }}!</h2>
          <p>Hệ thống theo dõi sức khỏe răng miệng, lịch khám và hóa đơn điều trị của bạn tại Nha khoa Hoàng Gia.</p>
        </div>
        <div class="patient-actions">
          <RouterLink class="btn btn-primary" to="/patient/my-record">Hồ sơ điều trị của tôi</RouterLink>
          <RouterLink class="btn btn-accent" to="/patient/appointments">Đặt lịch khám mới</RouterLink>
          <RouterLink class="btn btn-outline" to="/patient/invoices/mine">Tra cứu hóa đơn</RouterLink>
          <RouterLink class="btn btn-outline" to="/patient/profile">Thông tin tài khoản</RouterLink>
        </div>
      </div>
    </div>

    <!-- ==================== STAFF & ADMIN MODERN ROLE DASHBOARD ==================== -->
    <div v-else class="executive-overview">
      <!-- 1. Actor-Themed Hero Header -->
      <header class="actor-hero" :class="'actor-hero--' + roleSlug">
        <div class="hero-left">
          <div class="hero-badges">
            <span class="role-pill">{{ actorMeta.badge }}</span>
            <span class="sync-status">
              <span class="pulse-dot"></span>
              Dữ liệu trực tiếp
            </span>
          </div>
          <h1 class="hero-title">{{ actorMeta.title }}</h1>
          <p class="hero-subtitle">
            Xin chào <strong>{{ authStore.user?.fullName }}</strong> · {{ currentDateFormatted }} · Phiên #{{ authStore.user?.userId }}
          </p>
        </div>

        <div class="hero-actions">
          <button class="btn-hero" :disabled="loadingData" @click="refreshAllData" title="Cập nhật số liệu mới">
            <span :class="{ 'spin-icon': loadingData }">↻</span>
            <span>{{ loadingData ? 'Đang đồng bộ...' : 'Làm mới số liệu' }}</span>
          </button>
          <RouterLink class="btn-hero btn-hero--ghost" to="/management/profile">
            Hồ sơ cá nhân
          </RouterLink>
        </div>
      </header>

      <!-- 2. Role-Specific 4-Card KPI Strip -->
      <section class="kpi-grid" aria-label="Chỉ số tổng quan theo vai trò">
        <div
          v-for="kpi in roleKpis"
          :key="kpi.label"
          class="kpi-card"
          :class="'kpi-card--' + kpi.tone"
        >
          <div class="kpi-card-top">
            <span class="kpi-label">{{ kpi.label }}</span>
            <span class="kpi-tag">{{ kpi.tag }}</span>
          </div>
          <div class="kpi-value">{{ kpi.value }}</div>
          <div class="kpi-foot">{{ kpi.foot }}</div>
        </div>
      </section>

      <!-- 3. Role-Specific Quick Action Bar -->
      <section class="quick-bar" aria-label="Thao tác nhanh theo vai trò">
        <div class="quick-bar-head">
          <strong>Lối tắt nghiệp vụ · {{ authStore.user?.roleName || authStore.role }}</strong>
          <small>{{ actorMeta.quickHint }}</small>
        </div>
        <div class="quick-bar-actions">
          <RouterLink
            v-for="act in roleQuickActions"
            :key="act.to"
            class="quick-pill"
            :class="{ 'quick-pill--primary': act.primary }"
            :to="act.to"
          >
            {{ act.label }}
          </RouterLink>
        </div>
      </section>

      <!-- ==================== 4A. GIAO DIỆN RIÊNG CHO ADMIN ==================== -->
      <template v-if="authStore.role === 'ADMIN'">
        <!-- Biểu đồ doanh thu 7 ngày -->
        <section class="panel chart-panel">
          <div class="panel-header">
            <div>
              <h3 class="panel-title">Biểu Đồ Doanh Thu 7 Ngày Gần Nhất</h3>
              <p class="panel-subtitle">Dòng tiền thực thu (Tiền mặt &amp; Chuyển khoản xác nhận)</p>
            </div>
            <div class="chart-summary-badges">
              <span class="summary-pill"><small>Tổng tuần:</small> <strong>{{ formatCurrency(revenueSummary.weeklyRevenue) }}</strong></span>
              <span class="summary-pill summary-pill--today"><small>Hôm nay:</small> <strong>{{ formatCurrency(revenueSummary.todayRevenue) }}</strong></span>
            </div>
          </div>

          <div class="chart-wrapper">
            <svg class="revenue-svg" viewBox="0 0 780 230" preserveAspectRatio="none" aria-label="Biểu đồ doanh thu tuần">
              <defs>
                <linearGradient id="barGradNormal" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="#38bdf8" />
                  <stop offset="100%" stop-color="#0284c7" />
                </linearGradient>
                <linearGradient id="barGradToday" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="#3b82f6" />
                  <stop offset="100%" stop-color="#1d4ed8" />
                </linearGradient>
                <linearGradient id="lineGlow" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="rgba(37, 99, 235, 0.22)" />
                  <stop offset="100%" stop-color="rgba(37, 99, 235, 0.0)" />
                </linearGradient>
              </defs>

              <line x1="55" y1="35" x2="755" y2="35" stroke="#e2e8f0" stroke-dasharray="4,4" />
              <line x1="55" y1="85" x2="755" y2="85" stroke="#e2e8f0" stroke-dasharray="4,4" />
              <line x1="55" y1="135" x2="755" y2="135" stroke="#e2e8f0" stroke-dasharray="4,4" />
              <line x1="55" y1="185" x2="755" y2="185" stroke="#cbd5e1" stroke-width="1.5" />

              <text x="50" y="38" text-anchor="end" class="svg-axis-text">{{ formatShortCurrency(maxWeeklyRevenue) }}</text>
              <text x="50" y="88" text-anchor="end" class="svg-axis-text">{{ formatShortCurrency(maxWeeklyRevenue * 0.66) }}</text>
              <text x="50" y="138" text-anchor="end" class="svg-axis-text">{{ formatShortCurrency(maxWeeklyRevenue * 0.33) }}</text>
              <text x="50" y="188" text-anchor="end" class="svg-axis-text">0₫</text>

              <polygon :points="areaPoints" fill="url(#lineGlow)" />
              <polyline :points="trendLinePoints" fill="none" stroke="#2563eb" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" />

              <g v-for="(day, idx) in weeklyChartData" :key="day.dateKey">
                <rect
                  :x="day.x - 26"
                  :y="day.barY"
                  width="52"
                  :height="day.barHeight"
                  rx="6"
                  :fill="day.isToday ? 'url(#barGradToday)' : 'url(#barGradNormal)'"
                  class="svg-bar"
                  @mouseenter="hoveredDayIndex = idx"
                  @mouseleave="hoveredDayIndex = null"
                />
                <circle :cx="day.x" :cy="day.nodeY" r="5" fill="#ffffff" stroke="#2563eb" stroke-width="2.5" class="svg-node" />
                <text :x="day.x" :y="day.barY - 8" text-anchor="middle" class="svg-bar-val" :class="{ 'svg-bar-val--today': day.isToday }">
                  {{ formatShortCurrency(day.revenue) }}
                </text>
                <text :x="day.x" :y="204" text-anchor="middle" class="svg-day-name" :class="{ 'svg-day-name--today': day.isToday }">{{ day.dayLabel }}</text>
                <text :x="day.x" :y="220" text-anchor="middle" class="svg-day-date" :class="{ 'svg-day-date--today': day.isToday }">{{ day.shortDate }}</text>
              </g>
            </svg>

            <div v-if="hoveredDayIndex !== null" class="chart-hover-card">
              <span class="hover-date">{{ weeklyChartData[hoveredDayIndex].fullDateLabel }}</span>
              <div class="hover-row"><span>Doanh thu:</span><strong class="hover-amt">{{ formatCurrency(weeklyChartData[hoveredDayIndex].revenue) }}</strong></div>
              <div class="hover-row"><span>Giao dịch:</span><span>{{ weeklyChartData[hoveredDayIndex].transactionCount }} ca</span></div>
            </div>
          </div>

          <div class="chart-legend-strip">
            <div class="legend-item"><span class="legend-swatch swatch-blue"></span> Ngày trong tuần</div>
            <div class="legend-item"><span class="legend-swatch swatch-darkblue"></span> Hôm nay ({{ currentDateFormatted }})</div>
            <RouterLink class="chart-link-more" to="/management/admin/reports/revenue">Xem báo cáo doanh thu chi tiết →</RouterLink>
          </div>
        </section>

        <!-- Lưới 2 cột: Cảnh báo vận hành thực tế + Audit Log -->
        <div class="workspace-grid">
          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Cảnh Báo Vận Hành Phòng Khám</h3>
                <p class="panel-subtitle">Tổng hợp trực tiếp từ kho vật tư, đặt lịch và hàng đợi</p>
              </div>
            </div>
            <div class="alerts-stack">
              <RouterLink class="alert-tile alert-tile--warn" to="/management/admin/medical-supplies">
                <div class="alert-tile-head"><strong>Vật tư dưới ngưỡng tồn tối thiểu</strong><span>{{ lowStockItems.length }} mã</span></div>
                <p v-if="lowStockItems.length">{{ lowStockItems.slice(0, 3).map(x => `${x.name} (${x.onHand}/${x.minimumStockThreshold} ${x.unit})`).join(' · ') }}</p>
                <p v-else>Tất cả vật tư đang ở mức tồn an toàn.</p>
              </RouterLink>

              <RouterLink class="alert-tile alert-tile--info" to="/management/appointments">
                <div class="alert-tile-head"><strong>Yêu cầu đặt lịch trực tuyến chờ duyệt</strong><span>{{ pendingRequests.length }} yêu cầu</span></div>
                <p>Lễ tân/Admin kiểm tra thông tin và duyệt lịch hẹn để chuẩn bị tiếp đón.</p>
              </RouterLink>

              <RouterLink class="alert-tile alert-tile--neutral" to="/management/queue/today">
                <div class="alert-tile-head"><strong>Trạng thái hàng đợi hôm nay</strong><span>{{ queueItems.length }} lượt</span></div>
                <p>{{ waitingQueueCount }} đang chờ · {{ inConsultationCount }} đang khám · {{ pausedQueueCount }} tạm dừng bàn giao.</p>
              </RouterLink>
            </div>
          </section>

          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Nhật Ký Thao Tác Gần Nhất</h3>
                <p class="panel-subtitle">Ghi nhận bảo mật &amp; nghiệp vụ hệ thống</p>
              </div>
              <RouterLink class="panel-link" to="/management/admin/audit-logs">Xem tất cả →</RouterLink>
            </div>
            <div class="audit-table-wrap">
              <table class="data-table">
                <thead>
                  <tr><th>Thời gian</th><th>Người thao tác</th><th>Hành động</th><th>Đối tượng</th></tr>
                </thead>
                <tbody>
                  <tr v-if="!recentAuditLogs.length"><td colspan="4" class="empty-cell">Chưa có bản ghi nhật ký gần đây.</td></tr>
                  <tr v-for="log in recentAuditLogs" :key="log.logId">
                    <td class="mono-cell">{{ formatLogTime(log.createdAt) }}</td>
                    <td><strong>{{ log.userName || `User #${log.userId || '?'}` }}</strong></td>
                    <td><span class="action-badge" :class="getActionBadgeClass(log.action)">{{ getActionLabel(log.action) }}</span></td>
                    <td>{{ log.entityType }} <span v-if="log.entityId">#{{ log.entityId }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </template>

      <!-- ==================== 4B. GIAO DIỆN RIÊNG CHO LỄ TÂN / THU NGÂN ==================== -->
      <template v-else-if="authStore.role === 'RECEPTIONIST'">
        <div class="workspace-grid">
          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Hàng Đợi Tiếp Đón Hôm Nay</h3>
                <p class="panel-subtitle">Theo dõi số thứ tự và trạng thái xử lý tại phòng khám</p>
              </div>
              <RouterLink class="panel-link" to="/management/queue/today">Mở hàng đợi →</RouterLink>
            </div>
            <p v-if="!queueItems.length" class="empty-box">Chưa có lượt chờ nào trong ngày hôm nay.</p>
            <ul v-else class="work-list">
              <li v-for="q in queueItems.slice(0, 6)" :key="q.queueEntryId" class="work-item">
                <div class="work-item-left">
                  <span class="queue-num">#{{ q.queueNumber }}</span>
                  <div>
                    <strong>{{ q.patientName }}</strong>
                    <small>{{ q.patientCode }}<template v-if="q.roomName"> · {{ q.roomName }}</template></small>
                  </div>
                </div>
                <span class="status-chip" :class="queueStatusClass(q.status)">{{ queueStatusLabel(q.status, q.statusText) }}</span>
              </li>
            </ul>
          </section>

          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Yêu Cầu Đặt Lịch &amp; Lịch Hẹn Hôm Nay</h3>
                <p class="panel-subtitle">Duyệt yêu cầu trực tuyến và tiếp đón khách có lịch hẹn</p>
              </div>
              <RouterLink class="panel-link" to="/management/appointments">Quản lý lịch hẹn →</RouterLink>
            </div>
            <div class="sub-block">
              <div class="sub-block-title">Yêu cầu trực tuyến chờ duyệt ({{ pendingRequests.length }})</div>
              <p v-if="!pendingRequests.length" class="empty-box">Không có yêu cầu đặt lịch mới chờ duyệt.</p>
              <ul v-else class="work-list">
                <li v-for="req in pendingRequests.slice(0, 3)" :key="req.id" class="work-item">
                  <div class="work-item-left">
                    <span class="time-pill">{{ req.slotTime.slice(0, 5) }}</span>
                    <div>
                      <strong>{{ req.fullName }}</strong>
                      <small>{{ formatDateOnly(req.appointmentDate) }} · {{ req.phone }}</small>
                    </div>
                  </div>
                  <span class="status-chip chip-warn">Chờ duyệt</span>
                </li>
              </ul>
            </div>

            <div class="sub-block">
              <div class="sub-block-title">Lịch hẹn tại phòng khám hôm nay ({{ deskAppointments.length }})</div>
              <p v-if="!deskAppointments.length" class="empty-box">Không có lịch hẹn nào trong ngày hôm nay.</p>
              <ul v-else class="work-list">
                <li v-for="a in deskAppointments.slice(0, 4)" :key="a.appointmentId" class="work-item">
                  <div class="work-item-left">
                    <span class="time-pill">{{ a.slotTime.slice(0, 5) }}</span>
                    <div>
                      <strong>{{ a.patientName }}</strong>
                      <small>{{ a.patientCode }}<template v-if="a.notes"> · {{ a.notes }}</template></small>
                    </div>
                  </div>
                  <span class="status-chip" :class="apptStatusClass(a.status)">{{ apptStatusLabel(a.status) }}</span>
                </li>
              </ul>
            </div>
          </section>
        </div>
      </template>

      <!-- ==================== 4C. GIAO DIỆN RIÊNG CHO NHA SĨ ==================== -->
      <template v-else-if="authStore.role === 'DENTIST'">
        <div class="workspace-grid">
          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Danh Sách Bệnh Nhân Cần Khám Hôm Nay</h3>
                <p class="panel-subtitle">Bao gồm bệnh nhân đang chờ, đang khám và ca tạm dừng bàn giao</p>
              </div>
              <RouterLink class="panel-link" to="/management/clinical/diagnosis">Vào khám &amp; FDI →</RouterLink>
            </div>
            <p v-if="!queueItems.length" class="empty-box">Hiện chưa có bệnh nhân nào trong hàng đợi hôm nay.</p>
            <ul v-else class="work-list">
              <li v-for="q in queueItems.slice(0, 7)" :key="q.queueEntryId" class="work-item">
                <div class="work-item-left">
                  <span class="queue-num">#{{ q.queueNumber }}</span>
                  <div>
                    <strong>{{ q.patientName }}</strong>
                    <small>{{ q.patientCode }}<template v-if="q.dentistName"> · {{ q.dentistName }}</template><template v-if="q.notes"> · {{ q.notes }}</template></small>
                  </div>
                </div>
                <span class="status-chip" :class="queueStatusClass(q.status)">{{ queueStatusLabel(q.status, q.statusText) }}</span>
              </li>
            </ul>
          </section>

          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Phòng Làm Việc &amp; Quy Tắc Lâm Sàng</h3>
                <p class="panel-subtitle">Trạng thái phòng hiện tại và lưu ý an toàn điều trị</p>
              </div>
              <RouterLink class="panel-link" to="/management/staff/workspace">Đổi phòng →</RouterLink>
            </div>

            <div class="workspace-card" :class="{ 'workspace-card--empty': !myWorkspace?.roomName }">
              <div class="ws-row">
                <span class="ws-label">Phòng điều trị hiện tại</span>
                <strong class="ws-val">{{ myWorkspace?.roomName || 'Chưa chọn phòng làm việc hôm nay' }}</strong>
              </div>
              <p v-if="!myWorkspace?.roomName" class="ws-hint">
                Vui lòng chọn phòng làm việc trước khi gọi bệnh nhân và bắt đầu ca khám.
              </p>
              <p v-else class="ws-hint">
                Phòng đang đồng bộ tới màn hình hàng đợi, phụ tá hỗ trợ và TV gọi số.
              </p>
            </div>

            <ul class="safety-checklist">
              <li><strong>Kiểm tra tiền sử &amp; sinh hiệu:</strong> Luôn xem cảnh báo dị ứng thuốc và huyết áp trước khi tiêm tê/thủ thuật.</li>
              <li><strong>Sơ đồ răng FDI:</strong> Chọn đúng bộ răng 32/20 răng và 5 mặt răng; cập nhật trạng thái sau điều trị dưới dạng bản ghi mới.</li>
              <li><strong>Bàn giao ca (Tạm dừng):</strong> Tạm dừng Visit để nhả phòng nhưng giữ nguyên thứ tự hàng đợi cho nha sĩ kế tiếp.</li>
              <li><strong>Kê đơn &amp; Chốt hồ sơ:</strong> Kết thúc khám để khóa hồ sơ và chuyển hóa đơn nháp sang thu ngân.</li>
            </ul>
          </section>
        </div>
      </template>

      <!-- ==================== 4D. GIAO DIỆN RIÊNG CHO PHỤ TÁ ==================== -->
      <template v-else-if="authStore.role === 'ASSISTANT'">
        <div class="workspace-grid">
          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Hàng Đợi Hỗ Trợ Hôm Nay</h3>
                <p class="panel-subtitle">Theo dõi bệnh nhân đang chờ gọi số và phối hợp chụp X-quang / sinh hiệu</p>
              </div>
              <RouterLink class="panel-link" to="/management/queue/today">Mở hàng đợi →</RouterLink>
            </div>
            <p v-if="!queueItems.length" class="empty-box">Chưa có lượt chờ trong hôm nay.</p>
            <ul v-else class="work-list">
              <li v-for="q in queueItems.slice(0, 6)" :key="q.queueEntryId" class="work-item">
                <div class="work-item-left">
                  <span class="queue-num">#{{ q.queueNumber }}</span>
                  <div>
                    <strong>{{ q.patientName }}</strong>
                    <small>{{ q.patientCode }} · {{ q.roomName || 'Chưa phân phòng' }}{{ q.dentistName ? ` · ${q.dentistName}` : '' }}</small>
                  </div>
                </div>
                <span class="status-chip" :class="queueStatusClass(q.status)">{{ queueStatusLabel(q.status, q.statusText) }}</span>
              </li>
            </ul>
          </section>

          <section class="panel">
            <div class="panel-header">
              <div>
                <h3 class="panel-title">Cảnh Báo Tồn Kho &amp; Hàng Đợi Hỗ Trợ</h3>
                <p class="panel-subtitle">Vật tư chạm ngưỡng tối thiểu và danh sách bệnh nhân chờ gọi số</p>
              </div>
              <RouterLink class="panel-link" to="/management/inventory/issues">Tiêu hao theo ca →</RouterLink>
            </div>

            <div class="sub-block">
              <div class="sub-block-title">Vật tư cần nhập thêm ({{ lowStockItems.length }})</div>
              <p v-if="!lowStockItems.length" class="empty-box">Tất cả vật tư đang đủ định mức tồn kho an toàn.</p>
              <ul v-else class="work-list">
                <li v-for="item in lowStockItems.slice(0, 5)" :key="item.medicalSupplyId" class="work-item">
                  <div class="work-item-left">
                    <span class="code-pill">{{ item.code }}</span>
                    <div>
                      <strong>{{ item.name }}</strong>
                      <small>Tồn hiện tại: {{ item.onHand }} {{ item.unit }} · Tối thiểu: {{ item.minimumStockThreshold }} {{ item.unit }}</small>
                    </div>
                  </div>
                  <span class="status-chip chip-danger">Tồn thấp</span>
                </li>
              </ul>
            </div>

            <div class="sub-block">
              <div class="sub-block-title">Nha sĩ đồng hành &amp; Hàng đợi ({{ queueItems.length }} lượt)</div>
              <div class="workspace-card" :class="{ 'workspace-card--empty': !myWorkspace?.dentistName }">
                <div class="ws-row">
                  <span class="ws-label">Nha sĩ đang hỗ trợ</span>
                  <strong class="ws-val">{{ assistantWorkspaceLabel }}</strong>
                </div>
                <p class="ws-hint" v-if="myWorkspace?.roomName">Phòng đồng bộ: <strong>{{ myWorkspace.roomName }}</strong></p>
                <p class="ws-hint" v-else-if="!myWorkspace?.hasConfirmedToday">Chọn nha sĩ tại mục Nhóm &amp; phòng làm việc để đồng bộ phòng điều trị.</p>
              </div>
            </div>
          </section>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { apiClient } from '@/services/api';
import { reportsService } from '@/services/reports';
import { queueService, type QueueEntryResponse } from '@/services/queue';
import { bookingRequestsService, type BookingRequestRow } from '@/services/bookingRequests';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
import { chairStatusService, type ChairStatusRow } from '@/services/chairStatus';
import { stockReceiptsService, type StockItem } from '@/services/stockReceipts';
import { WORKSPACE_UPDATED_EVENT, workspacesService, type WorkspaceSnapshot } from '@/services/workspaces';

const authStore = useAuthStore();

const loadingData = ref(false);
const hoveredDayIndex = ref<number | null>(null);

const revenueSummary = ref({
  todayRevenue: 0,
  todayTransactions: 0,
  weeklyRevenue: 0,
});

interface DayRevenueItem {
  dateKey: string;
  dayLabel: string;
  shortDate: string;
  fullDateLabel: string;
  revenue: number;
  transactionCount: number;
  isToday: boolean;
  x: number;
  barY: number;
  barHeight: number;
  nodeY: number;
}

const weeklyChartData = ref<DayRevenueItem[]>([]);
const maxWeeklyRevenue = ref(1000000);

interface SimpleAuditLog {
  logId: number;
  userId: number | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: number | null;
  createdAt: string;
}

const recentAuditLogs = ref<SimpleAuditLog[]>([]);
const queueItems = ref<QueueEntryResponse[]>([]);
const pendingRequests = ref<BookingRequestRow[]>([]);
const deskAppointments = ref<AppointmentResponse[]>([]);
const chairItems = ref<ChairStatusRow[]>([]);
const lowStockItems = ref<StockItem[]>([]);
const myWorkspace = ref<WorkspaceSnapshot | null>(null);
const assistantWorkspaceLabel = computed(() => myWorkspace.value?.dentistName
  || (myWorkspace.value?.hasConfirmedToday ? 'Không hỗ trợ nha sĩ' : 'Chưa chọn nha sĩ hỗ trợ'));

const isStaff = computed(() => ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'].includes(authStore.role));
const roleSlug = computed(() => (authStore.role || 'staff').toLowerCase());

const waitingQueueCount = computed(() => queueItems.value.filter(q => q.status === 1 || q.status === 6).length);
const inConsultationCount = computed(() => queueItems.value.filter(q => q.status === 2).length);
const pausedQueueCount = computed(() => queueItems.value.filter(q => q.status === 8).length);
const completedQueueCount = computed(() => queueItems.value.filter(q => q.status === 3).length);
const availableChairsCount = computed(() => chairItems.value.filter(c => c.status === 1 && c.isActive).length);
const occupiedChairsCount = computed(() => chairItems.value.filter(c => c.status === 2).length);

const actorMeta = computed(() => {
  switch (authStore.role) {
    case 'ADMIN':
      return {
        badge: 'Quản trị viên phòng khám',
        title: 'Tổng Quan Điều Hành Phòng Khám',
        quickHint: 'Quản lý nhân sự, danh mục chuyên môn, cấu hình và báo cáo doanh thu'
      };
    case 'RECEPTIONIST':
      return {
        badge: 'Lễ tân / Thu ngân',
        title: 'Bàn Làm Việc Tiếp Đón & Thu Ngân',
        quickHint: 'Duyệt lịch hẹn, quét QR CCCD check-in hàng đợi và thu ngân'
      };
    case 'DENTIST':
      return {
        badge: 'Nha sĩ điều trị',
        title: 'Bảng Điều Khiển Lâm Sàng Nha Sĩ',
        quickHint: 'Chọn phòng làm việc, gọi bệnh nhân từ hàng đợi và cập nhật sơ đồ FDI'
      };
    case 'ASSISTANT':
      return {
        badge: 'Phụ tá điều trị',
        title: 'Điều Phối Phòng Ghế & Kho Vật Tư',
        quickHint: 'Cập nhật phòng/ghế, hỗ trợ hàng đợi và xác nhận tiêu hao vật tư theo ca'
      };
    default:
      return {
        badge: 'Nhân viên',
        title: 'Bảng Điều Khiển Tổng Quan',
        quickHint: 'Truy cập nhanh các nghiệp vụ được phân quyền'
      };
  }
});

const roleKpis = computed(() => {
  switch (authStore.role) {
    case 'ADMIN':
      return [
        { label: 'Doanh thu hôm nay', tag: 'Tài chính', value: formatCurrency(revenueSummary.value.todayRevenue), foot: `${revenueSummary.value.todayTransactions} giao dịch thực thu`, tone: 'blue' },
        { label: 'Doanh thu 7 ngày', tag: 'Tuần này', value: formatCurrency(revenueSummary.value.weeklyRevenue), foot: 'Tiền mặt & chuyển khoản xác nhận', tone: 'indigo' },
        { label: 'Hàng đợi hôm nay', tag: 'Tiếp đón', value: `${queueItems.value.length} lượt`, foot: `${waitingQueueCount.value} chờ · ${inConsultationCount.value} đang khám`, tone: 'teal' },
        { label: 'Phòng / Ghế điều trị', tag: 'Vận hành', value: `${occupiedChairsCount.value}/${chairItems.value.length || 0}`, foot: `${availableChairsCount.value} phòng sẵn sàng · ${lowStockItems.value.length} vật tư tồn thấp`, tone: 'amber' }
      ];
    case 'RECEPTIONIST':
      return [
        { label: 'Yêu cầu đặt lịch mới', tag: 'Trực tuyến', value: `${pendingRequests.value.length}`, foot: 'Yêu cầu đang chờ duyệt', tone: 'amber' },
        { label: 'Lịch hẹn hôm nay', tag: 'Tại quầy', value: `${deskAppointments.value.length}`, foot: `${deskAppointments.value.filter(a => a.status === 'Scheduled').length} lịch chưa check-in`, tone: 'blue' },
        { label: 'Hàng đợi đang chờ', tag: 'Tiếp đón', value: `${waitingQueueCount.value}`, foot: `${inConsultationCount.value} đang khám · ${pausedQueueCount.value} tạm dừng`, tone: 'teal' },
        { label: 'Tổng lượt hôm nay', tag: 'Trong ngày', value: `${queueItems.value.length}`, foot: `${completedQueueCount.value} ca đã hoàn tất khám`, tone: 'indigo' }
      ];
    case 'DENTIST':
      return [
        { label: 'Phòng làm việc', tag: 'Hôm nay', value: myWorkspace.value?.roomName || 'Chưa chọn', foot: myWorkspace.value?.chairNumber ? `Ghế đồng bộ: ${myWorkspace.value.chairNumber}` : 'Cần chọn phòng trước khi khám', tone: myWorkspace.value?.roomName ? 'teal' : 'amber' },
        { label: 'Bệnh nhân chờ khám', tag: 'Hàng đợi', value: `${waitingQueueCount.value}`, foot: 'Sẵn sàng gọi vào phòng', tone: 'blue' },
        { label: 'Đang khám / Tạm dừng', tag: 'Lâm sàng', value: `${inConsultationCount.value} / ${pausedQueueCount.value}`, foot: 'Bao gồm ca chờ bàn giao', tone: 'indigo' },
        { label: 'Hoàn tất hôm nay', tag: 'Kết quả', value: `${completedQueueCount.value} ca`, foot: `Tổng ${queueItems.value.length} lượt trong hàng đợi`, tone: 'emerald' }
      ];
    case 'ASSISTANT':
      return [
        { label: 'Nha sĩ đồng hành', tag: 'Phối hợp', value: assistantWorkspaceLabel.value, foot: myWorkspace.value?.roomName || 'Nhóm & phòng làm việc', tone: 'blue' },
        { label: 'Khách đang chờ', tag: 'Hàng đợi', value: `${waitingQueueCount.value}`, foot: `Tổng ${queueItems.value.length} lượt khám hôm nay`, tone: 'indigo' },
        { label: 'Đang gọi / Khám', tag: 'Phối hợp', value: `${inConsultationCount.value}`, foot: 'Ca đang xử lý tại phòng', tone: 'teal' },
        { label: 'Vật tư tồn thấp', tag: 'Cảnh báo kho', value: `${lowStockItems.value.length} mã`, foot: 'Chạm hoặc dưới ngưỡng tối thiểu', tone: lowStockItems.value.length ? 'amber' : 'emerald' }
      ];
    default:
      return [];
  }
});

const roleQuickActions = computed(() => {
  switch (authStore.role) {
    case 'ADMIN':
      return [
        { label: '+ Quản lý nhân sự', to: '/management/admin/staff', primary: true },
        { label: 'Dịch vụ & bảng giá', to: '/management/admin/services' },
        { label: 'Danh mục vật tư', to: '/management/admin/medical-supplies' },
        { label: 'Danh mục thuốc', to: '/management/admin/medicines' },
        { label: 'Báo cáo doanh thu', to: '/management/admin/reports/revenue' },
        { label: 'Nhật ký thao tác', to: '/management/admin/audit-logs' }
      ];
    case 'RECEPTIONIST':
      return [
        { label: 'Tiếp đón & Check-in (QR)', to: '/management/queue/check-in', primary: true },
        { label: 'Quản lý lịch hẹn', to: '/management/appointments' },
        { label: 'Tra cứu & Thu tiền', to: '/management/invoices' },
        { label: 'Hàng đợi hôm nay', to: '/management/queue/today' },
        { label: 'Hồ sơ bệnh nhân', to: '/management/patients' }
      ];
    case 'DENTIST':
      return [
        { label: 'Khám & Chẩn đoán FDI', to: '/management/clinical/diagnosis', primary: true },
        { label: 'Chọn phòng làm việc', to: '/management/staff/workspace' },
        { label: 'Hàng đợi hôm nay', to: '/management/queue/today' },
        { label: 'Hồ sơ bệnh nhân', to: '/management/patients' },
        { label: 'Xem tồn vật tư', to: '/management/inventory/receipts' }
      ];
    case 'ASSISTANT':
      return [
        { label: 'Xác nhận tiêu hao theo ca', to: '/management/inventory/issues', primary: true },
        { label: 'Nhập kho vật tư', to: '/management/inventory/receipts' },
        { label: 'Nhóm & phòng làm việc', to: '/management/staff/workspace' },
        { label: 'Hàng đợi hôm nay', to: '/management/queue/today' }
      ];
    default:
      return [];
  }
});

const currentDateFormatted = computed(() => {
  return new Date().toLocaleDateString('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  });
});

const trendLinePoints = computed(() =>
  weeklyChartData.value.length ? weeklyChartData.value.map(d => `${d.x},${d.nodeY}`).join(' ') : ''
);

const areaPoints = computed(() => {
  if (!weeklyChartData.value.length) return '';
  const first = weeklyChartData.value[0];
  const last = weeklyChartData.value[weeklyChartData.value.length - 1];
  const points = weeklyChartData.value.map(d => `${d.x},${d.nodeY}`).join(' ');
  return `${first.x},185 ${points} ${last.x},185`;
});

function formatCurrency(val: number): string {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(val || 0);
}

function formatShortCurrency(val: number): string {
  if (!val) return '0₫';
  if (val >= 1000000) return `${(val / 1000000).toFixed(1).replace('.0', '')} tr`;
  if (val >= 1000) return `${Math.round(val / 1000)}k`;
  return `${val}₫`;
}

function formatDateOnly(val: string): string {
  if (!val) return '';
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${val.slice(0, 10)}T00:00:00`));
}

function formatLogTime(isoStr: string): string {
  if (!isoStr) return '—';
  const d = new Date(isoStr);
  return `${d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} ${d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' })}`;
}

function getActionLabel(action: string): string {
  const map: Record<string, string> = {
    LOGIN: 'Đăng nhập',
    LOGIN_FAILED: 'Sai mật khẩu',
    LOGOUT: 'Đăng xuất',
    CREATE: 'Tạo mới',
    UPDATE: 'Cập nhật',
    DELETE: 'Xóa',
    QUEUE_CHECKED_IN: 'Tiếp đón',
    PAYMENT_CONFIRMED: 'Thu tiền',
  };
  return map[action] || action;
}

function getActionBadgeClass(action: string): string {
  if (action === 'LOGIN') return 'badge-login';
  if (action === 'CREATE') return 'badge-create';
  if (action.includes('PAYMENT')) return 'badge-payment';
  if (action === 'DELETE') return 'badge-delete';
  return 'badge-default';
}

function queueStatusLabel(status: number, fallback: string): string {
  const map: Record<number, string> = {
    1: 'Đang chờ',
    2: 'Đang khám',
    3: 'Hoàn tất',
    4: 'Đã hủy',
    5: 'Chụp X-quang',
    6: 'Đang gọi số',
    7: 'Vắng mặt',
    8: 'Tạm dừng'
  };
  return map[status] || fallback || 'Khác';
}

function queueStatusClass(status: number): string {
  if (status === 2) return 'chip-blue';
  if (status === 3) return 'chip-green';
  if (status === 8 || status === 5) return 'chip-warn';
  if (status === 4 || status === 7) return 'chip-muted';
  return 'chip-info';
}

function apptStatusLabel(status: string): string {
  const map: Record<string, string> = {
    Scheduled: 'Đã đặt lịch',
    CheckedIn: 'Đã tiếp đón',
    Completed: 'Hoàn tất',
    Cancelled: 'Đã hủy'
  };
  return map[status] || status;
}

function apptStatusClass(status: string): string {
  if (status === 'CheckedIn') return 'chip-blue';
  if (status === 'Completed') return 'chip-green';
  if (status === 'Cancelled') return 'chip-muted';
  return 'chip-info';
}

function chairStatusLabel(status: number, fallback: string): string {
  const map: Record<number, string> = { 1: 'Sẵn sàng', 2: 'Đang dùng', 3: 'Bảo trì', 4: 'Ngưng hoạt động' };
  return map[status] || fallback;
}

function chairStatusChipClass(status: number): string {
  if (status === 1) return 'chip-green';
  if (status === 2) return 'chip-blue';
  if (status === 3) return 'chip-warn';
  return 'chip-muted';
}

function chairToneClass(status: number): string {
  if (status === 1) return 'chair-tile--free';
  if (status === 2) return 'chair-tile--busy';
  return 'chair-tile--maint';
}

async function loadRevenueReport() {
  const today = new Date();
  const past7Days = new Date();
  past7Days.setDate(today.getDate() - 6);
  const toIsoDate = (d: Date) => d.toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
  const fromDate = toIsoDate(past7Days);
  const toDate = toIsoDate(today);

  try {
    const res = await reportsService.revenue({ fromDate, toDate, groupBy: 'Auto', top: '10', sortBy: 'Revenue' });
    if (res?.summary) {
      revenueSummary.value.weeklyRevenue = res.summary.netRevenue || res.summary.grossRevenue || 0;
    }

    const days: DayRevenueItem[] = [];
    const dayNames = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    let highest = 0;

    for (let i = 0; i < 7; i++) {
      const curDate = new Date(past7Days);
      curDate.setDate(past7Days.getDate() + i);
      const curDateKey = toIsoDate(curDate);
      const isToday = curDateKey === toDate;
      const found = res?.dailyRevenue?.find(d => d.date === curDateKey);
      const dayRev = found ? (found.revenue || found.grossRevenue || 0) : 0;
      const txCount = found ? found.transactionCount : 0;

      if (isToday) {
        revenueSummary.value.todayRevenue = dayRev;
        revenueSummary.value.todayTransactions = txCount;
      }
      if (dayRev > highest) highest = dayRev;

      days.push({
        dateKey: curDateKey,
        dayLabel: dayNames[curDate.getDay()],
        shortDate: `${curDate.getDate()}/${curDate.getMonth() + 1}`,
        fullDateLabel: curDate.toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }),
        revenue: dayRev,
        transactionCount: txCount,
        isToday,
        x: 85 + i * 100,
        barY: 185,
        barHeight: 0,
        nodeY: 185
      });
    }

    const ceiling = highest > 0 ? Math.ceil((highest * 1.25) / 100000) * 100000 : 1000000;
    maxWeeklyRevenue.value = ceiling;
    days.forEach(d => {
      const ratio = d.revenue > 0 ? Math.min(d.revenue / ceiling, 1) : 0.04;
      const h = Math.max(ratio * 130, 6);
      d.barHeight = h;
      d.barY = 185 - h;
      d.nodeY = Math.max(185 - h - 6, 38);
    });
    weeklyChartData.value = days;
  } catch {
    weeklyChartData.value = [];
  }
}

async function refreshAllData() {
  if (!isStaff.value) return;
  loadingData.value = true;
  const role = authStore.role;
  const todayStr = new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });

  const tasks: Promise<unknown>[] = [
    queueService.getToday(1).then(res => { queueItems.value = res.items || []; }).catch(() => { queueItems.value = []; })
  ];

  if (role === 'ADMIN') {
    tasks.push(
      loadRevenueReport(),
      apiClient.get('/audit-logs', { params: { page: 1, pageSize: 6 } })
        .then(res => { recentAuditLogs.value = Array.isArray(res.data?.items) ? res.data.items : []; })
        .catch(() => { recentAuditLogs.value = []; }),
      bookingRequestsService.list(1).then(res => { pendingRequests.value = res.items || []; }).catch(() => { pendingRequests.value = []; }),
      chairStatusService.getPage({ page: 1, pageSize: 20 }).then(res => { chairItems.value = res.items || []; }).catch(() => { chairItems.value = []; }),
      stockReceiptsService.stock('', 1)
        .then(res => { lowStockItems.value = (res.items || []).filter(s => s.isActive && s.onHand <= s.minimumStockThreshold); })
        .catch(() => { lowStockItems.value = []; })
    );
  } else if (role === 'RECEPTIONIST') {
    tasks.push(
      bookingRequestsService.list(1).then(res => { pendingRequests.value = res.items || []; }).catch(() => { pendingRequests.value = []; }),
      appointmentsService.getForDesk(todayStr, 1).then(res => { deskAppointments.value = res.items || []; }).catch(() => { deskAppointments.value = []; })
    );
  } else if (role === 'DENTIST') {
    if (authStore.user?.userId) {
      tasks.push(
        workspacesService.get(authStore.user.userId).then(res => { myWorkspace.value = res; }).catch(() => { myWorkspace.value = null; })
      );
    }
  } else if (role === 'ASSISTANT') {
    tasks.push(
      stockReceiptsService.stock('', 1)
        .then(res => { lowStockItems.value = (res.items || []).filter(s => s.isActive && s.onHand <= s.minimumStockThreshold); })
        .catch(() => { lowStockItems.value = []; })
    );
    if (authStore.user?.userId) {
      tasks.push(
        workspacesService.get(authStore.user.userId).then(res => { myWorkspace.value = res; }).catch(() => { myWorkspace.value = null; })
      );
    }
  }

  await Promise.allSettled(tasks);
  loadingData.value = false;
}

function onWorkspaceUpdated(ev: Event) {
  const detail = (ev as CustomEvent<WorkspaceSnapshot>).detail;
  if (detail && detail.userId === authStore.user?.userId) {
    myWorkspace.value = detail;
  }
}

onMounted(() => {
  window.addEventListener(WORKSPACE_UPDATED_EVENT, onWorkspaceUpdated);
  if (isStaff.value) void refreshAllData();
});

onBeforeUnmount(() => {
  window.removeEventListener(WORKSPACE_UPDATED_EVENT, onWorkspaceUpdated);
});
</script>

<style scoped>
.dashboard-container { width: 100%; }

.patient-banner {
  background: linear-gradient(135deg, #102e56 0%, #1d5ca3 100%);
  border-radius: 18px;
  padding: 2rem 2.25rem;
  color: #ffffff;
  box-shadow: 0 12px 30px rgba(16, 46, 86, 0.14);
}
.patient-banner-content { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1.5rem; }
.patient-badge { display: inline-block; background: rgba(255, 255, 255, 0.16); padding: 0.25rem 0.75rem; border-radius: 999px; font-size: 0.75rem; font-weight: 700; margin-bottom: 0.5rem; }
.patient-banner-text h2 { margin: 0 0 0.4rem; font-size: 1.65rem; font-weight: 800; color: #fde047; }
.patient-banner-text p { margin: 0; color: #dbeafe; max-width: 560px; }
.patient-actions { display: flex; flex-wrap: wrap; gap: 0.65rem; }
.btn { padding: 0.6rem 1rem; border-radius: 9px; text-decoration: none; font-weight: 700; font-size: 0.88rem; }
.btn-primary { background: #ffffff; color: #102e56; }
.btn-accent { background: #e5b95e; color: #102e56; }
.btn-outline { border: 1px solid rgba(255, 255, 255, 0.45); color: #ffffff; }

.executive-overview { display: flex; flex-direction: column; gap: 1.25rem; }

/* 1. Actor Hero */
.actor-hero {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1.25rem;
  padding: 1.5rem 1.75rem;
  border-radius: 18px;
  color: #ffffff;
  box-shadow: 0 12px 28px rgba(15, 45, 89, 0.14);
}
.actor-hero--admin { background: linear-gradient(135deg, #0f2d59 0%, #1d4ed8 100%); }
.actor-hero--receptionist { background: linear-gradient(135deg, #0f2d59 0%, #0284c7 100%); }
.actor-hero--dentist { background: linear-gradient(135deg, #0e3a4c 0%, #0d9488 100%); }
.actor-hero--assistant { background: linear-gradient(135deg, #1e3a5f 0%, #0f766e 100%); }

.hero-badges { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; margin-bottom: 0.5rem; }
.role-pill {
  padding: 0.25rem 0.75rem;
  border-radius: 999px;
  font-size: 0.74rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: rgba(255, 255, 255, 0.18);
  border: 1px solid rgba(255, 255, 255, 0.35);
}
.sync-status { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.76rem; font-weight: 600; color: #e2e8f0; }
.pulse-dot { width: 8px; height: 8px; border-radius: 50%; background: #4ade80; box-shadow: 0 0 0 3px rgba(74, 222, 128, 0.25); }
.hero-title { margin: 0 0 0.25rem; font-size: clamp(1.3rem, 2.5vw, 1.7rem); font-weight: 850; color: #fde047; letter-spacing: -0.015em; }
.hero-subtitle { margin: 0; font-size: 0.88rem; color: #e2e8f0; }
.hero-actions { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
.btn-hero {
  display: inline-flex; align-items: center; gap: 0.45rem;
  padding: 0.58rem 1rem; border-radius: 10px;
  border: 1px solid rgba(255, 255, 255, 0.28);
  background: #ffffff; color: #0f2d59;
  font: inherit; font-size: 0.84rem; font-weight: 750;
  text-decoration: none; cursor: pointer; transition: all 0.15s ease;
}
.btn-hero:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 6px 14px rgba(0, 0, 0, 0.12); }
.btn-hero--ghost { background: rgba(255, 255, 255, 0.14); color: #ffffff; }
.spin-icon { display: inline-block; animation: spin 0.9s linear infinite; }

/* 2. KPI Strip */
.kpi-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
.kpi-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-top: 4px solid #1857b6;
  border-radius: 14px;
  padding: 1.05rem 1.15rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  box-shadow: 0 4px 14px rgba(15, 45, 89, 0.04);
}
.kpi-card--blue { border-top-color: #2563eb; }
.kpi-card--indigo { border-top-color: #4f46e5; }
.kpi-card--teal { border-top-color: #0d9488; }
.kpi-card--emerald { border-top-color: #16a34a; }
.kpi-card--amber { border-top-color: #d97706; }

.kpi-card-top { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
.kpi-label { font-size: 0.8rem; font-weight: 700; color: #475569; }
.kpi-tag { font-size: 0.68rem; font-weight: 800; padding: 0.15rem 0.5rem; border-radius: 999px; background: #f1f5f9; color: #334155; }
.kpi-value { font-size: 1.45rem; font-weight: 850; color: #0f2d59; line-height: 1.2; overflow-wrap: anywhere; }
.kpi-foot { font-size: 0.78rem; color: #64748b; }

/* 3. Quick Bar */
.quick-bar {
  display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem;
  background: #ffffff; border: 1px solid #dce7f0; border-radius: 14px; padding: 0.95rem 1.25rem;
}
.quick-bar-head strong { display: block; font-size: 0.92rem; color: #0f2d59; }
.quick-bar-head small { font-size: 0.78rem; color: #64748b; }
.quick-bar-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.quick-pill {
  padding: 0.5rem 0.9rem; border-radius: 9px;
  border: 1px solid #cbd5e1; background: #f8fbff; color: #0f2d59;
  font-size: 0.82rem; font-weight: 700; text-decoration: none; transition: all 0.15s ease;
}
.quick-pill:hover { background: #e8f4fd; border-color: #1857b6; }
.quick-pill--primary { background: #1857b6; border-color: #1857b6; color: #ffffff; }
.quick-pill--primary:hover { background: #134898; }

/* 4. Panels & Workspace Grid */
.workspace-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; }
.panel {
  background: #ffffff; border: 1px solid #dce7f0; border-radius: 16px;
  padding: 1.25rem 1.35rem; box-shadow: 0 8px 24px rgba(15, 45, 89, 0.04);
  display: flex; flex-direction: column; gap: 1rem;
}
.panel-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
.panel-title { margin: 0; font-size: 1.05rem; font-weight: 800; color: #0f2d59; }
.panel-subtitle { margin: 0.2rem 0 0; font-size: 0.8rem; color: #64748b; }
.panel-link { font-size: 0.82rem; font-weight: 750; color: #1857b6; text-decoration: none; white-space: nowrap; }
.panel-link:hover { text-decoration: underline; }

/* Chart */
.chart-summary-badges { display: flex; gap: 0.6rem; flex-wrap: wrap; }
.summary-pill { padding: 0.35rem 0.75rem; border-radius: 999px; background: #f1f5f9; font-size: 0.82rem; color: #334155; }
.summary-pill--today { background: #e0f2fe; color: #0369a1; }
.chart-wrapper { position: relative; width: 100%; overflow-x: auto; }
.revenue-svg { width: 100%; min-width: 600px; height: 225px; display: block; }
.svg-axis-text { font-size: 10px; fill: #64748b; font-weight: 600; }
.svg-bar { transition: opacity 0.15s ease; cursor: pointer; }
.svg-bar:hover { opacity: 0.85; }
.svg-bar-val { font-size: 10px; fill: #334155; font-weight: 700; }
.svg-bar-val--today { fill: #1d4ed8; font-weight: 800; }
.svg-day-name { font-size: 11px; fill: #475569; font-weight: 700; }
.svg-day-name--today { fill: #1d4ed8; }
.svg-day-date { font-size: 10px; fill: #94a3b8; }
.chart-hover-card {
  position: absolute; top: 8px; right: 12px;
  background: rgba(15, 45, 89, 0.94); color: #ffffff;
  padding: 0.65rem 0.9rem; border-radius: 10px; font-size: 0.8rem;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.2); pointer-events: none;
}
.hover-date { display: block; font-weight: 700; color: #fde047; margin-bottom: 0.25rem; }
.hover-row { display: flex; justify-content: space-between; gap: 1rem; }
.chart-legend-strip { display: flex; align-items: center; flex-wrap: wrap; gap: 1rem; font-size: 0.78rem; color: #475569; }
.legend-item { display: inline-flex; align-items: center; gap: 0.4rem; }
.legend-swatch { width: 12px; height: 12px; border-radius: 3px; }
.swatch-blue { background: #0284c7; }
.swatch-darkblue { background: #1d4ed8; }
.chart-link-more { margin-left: auto; font-weight: 750; color: #1857b6; text-decoration: none; }

/* Alerts & Tables */
.alerts-stack { display: flex; flex-direction: column; gap: 0.75rem; }
.alert-tile {
  display: block; padding: 0.9rem 1rem; border-radius: 12px;
  border: 1px solid #e2e8f0; border-left: 4px solid #0284c7;
  background: #f8fbff; text-decoration: none; color: inherit; transition: all 0.15s ease;
}
.alert-tile:hover { border-color: #93c5fd; transform: translateY(-1px); }
.alert-tile--warn { border-left-color: #d97706; background: #fffbeb; }
.alert-tile--info { border-left-color: #2563eb; background: #eff6ff; }
.alert-tile--neutral { border-left-color: #0d9488; background: #f0fdfa; }
.alert-tile-head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; margin-bottom: 0.3rem; color: #0f2d59; }
.alert-tile-head span { font-size: 0.75rem; font-weight: 800; padding: 0.12rem 0.5rem; border-radius: 999px; background: rgba(255, 255, 255, 0.85); }
.alert-tile p { margin: 0; font-size: 0.82rem; color: #475569; }

.audit-table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.84rem; }
.data-table th { text-align: left; padding: 0.6rem 0.75rem; background: #f8fafc; color: #475569; font-weight: 700; border-bottom: 1px solid #e2e8f0; }
.data-table td { padding: 0.65rem 0.75rem; border-bottom: 1px solid #f1f5f9; color: #1e293b; }
.mono-cell { font-family: ui-monospace, monospace; font-size: 0.78rem; color: #64748b; }
.empty-cell { text-align: center; color: #64748b; padding: 1.5rem; }
.action-badge { padding: 0.15rem 0.5rem; border-radius: 6px; font-size: 0.74rem; font-weight: 750; background: #e2e8f0; color: #1e293b; }
.badge-login { background: #e0f2fe; color: #0369a1; }
.badge-create { background: #dcfce7; color: #166534; }
.badge-payment { background: #fef3c7; color: #b45309; }
.badge-delete { background: #fee2e2; color: #b91c1c; }

/* Worklist & Chips */
.sub-block { display: flex; flex-direction: column; gap: 0.55rem; }
.sub-block-title { font-size: 0.82rem; font-weight: 800; color: #334155; text-transform: uppercase; letter-spacing: 0.04em; }
.empty-box { margin: 0; padding: 1rem; border-radius: 10px; border: 1px dashed #cbd5e1; background: #f8fafc; color: #64748b; font-size: 0.86rem; text-align: center; }
.work-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.55rem; }
.work-item {
  display: flex; justify-content: space-between; align-items: center; gap: 0.75rem;
  padding: 0.7rem 0.9rem; border-radius: 10px; border: 1px solid #e2e8f0; background: #f8fbff;
}
.work-item-left { display: flex; align-items: center; gap: 0.75rem; min-width: 0; }
.work-item-left strong { display: block; font-size: 0.9rem; color: #0f2d59; }
.work-item-left small { display: block; font-size: 0.78rem; color: #64748b; }
.queue-num, .time-pill, .code-pill {
  padding: 0.25rem 0.55rem; border-radius: 8px;
  background: #e0f2fe; color: #0369a1; font-size: 0.78rem; font-weight: 800; flex-shrink: 0;
}
.status-chip { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.74rem; font-weight: 750; white-space: nowrap; }
.chip-info { background: #e0f2fe; color: #0369a1; }
.chip-blue { background: #dbeafe; color: #1d4ed8; }
.chip-green { background: #dcfce7; color: #166534; }
.chip-warn { background: #fef3c7; color: #b45309; }
.chip-danger { background: #fee2e2; color: #b91c1c; }
.chip-muted { background: #f1f5f9; color: #64748b; }

/* Dentist Workspace & Safety */
.workspace-card { padding: 0.9rem 1rem; border-radius: 12px; border: 1px solid #bae6fd; background: #f0f9ff; }
.workspace-card--empty { border-color: #fde68a; background: #fffbeb; }
.ws-row { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
.ws-label { font-size: 0.82rem; font-weight: 700; color: #475569; }
.ws-val { font-size: 0.98rem; color: #0f2d59; }
.ws-hint { margin: 0.35rem 0 0; font-size: 0.8rem; color: #475569; }
.safety-checklist { margin: 0; padding-left: 1.15rem; display: grid; gap: 0.5rem; font-size: 0.84rem; color: #334155; }

/* Assistant Chairs Grid */
.chairs-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(175px, 1fr)); gap: 0.75rem; }
.chair-tile {
  padding: 0.85rem; border-radius: 12px; border: 1px solid #e2e8f0; background: #f8fbff;
  display: flex; flex-direction: column; gap: 0.5rem;
}
.chair-tile--free { border-left: 4px solid #16a34a; }
.chair-tile--busy { border-left: 4px solid #2563eb; }
.chair-tile--maint { border-left: 4px solid #d97706; }
.chair-tile-top { display: flex; justify-content: space-between; align-items: center; gap: 0.4rem; }
.chair-tile-top strong { font-size: 0.88rem; color: #0f2d59; }
.chair-code { font-size: 0.74rem; font-weight: 800; color: #64748b; }

@keyframes spin { to { transform: rotate(360deg); } }

@media (max-width: 1100px) {
  .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .workspace-grid { grid-template-columns: 1fr; }
}

@media (max-width: 640px) {
  .kpi-grid { grid-template-columns: 1fr; }
  .actor-hero { padding: 1.2rem; }
}
</style>
