<template>
  <main class="page">
    <div class="header-banner">
      <div class="header-icon">🗓️</div>
      <div class="header-titles">
        <h1>Quản lý lịch hẹn tại quầy</h1>
        <p class="subtitle">Tra cứu lịch hẹn trong ngày, hỗ trợ dời lịch, hủy hẹn và nhắc hẹn tự động qua SMS/Zalo.</p>
      </div>
    </div>

    <AppointmentReminderPanel />

    <form class="filters-card" @submit.prevent="load(1)">
      <div class="filter-group">
        <label for="day">Ngày hẹn</label>
        <input id="day" v-model="date" type="date" :disabled="busy" @change="load(1)" />
      </div>
      <div class="filter-group">
        <label for="status">Trạng thái lịch</label>
        <select id="status" v-model="status" :disabled="busy" @change="load(1)">
          <option value="Scheduled">Đang chờ khám</option>
          <option value="">Tất cả trạng thái</option>
          <option value="Cancelled">Đã hủy</option>
          <option value="CheckedIn">Đã tiếp đón</option>
          <option value="Completed">Hoàn tất</option>
        </select>
      </div>
      <button class="btn btn-primary" :disabled="loading || busy">
        <span v-if="loading">Đang tải…</span>
        <span v-else>Tra cứu / Tải lại</span>
      </button>
    </form>

    <div v-if="error" class="alert-box alert-error" role="alert">⚠️ {{ error }}</div>
    <div v-if="success" class="alert-box alert-success" role="status">✅ {{ success }}</div>
    
    <div v-if="loading" class="loading-state" role="status">
      <div class="spinner"></div>
      <p>Đang tải danh sách lịch hẹn…</p>
    </div>

    <div v-else-if="!error && !rows.length" class="empty-state">
      <p>Không có lịch hẹn nào phù hợp với điều kiện tìm kiếm.</p>
    </div>

    <ul v-else class="appointment-list">
      <li v-for="group in displayGroups" :key="group.key">
        <div class="appointment-group">
          <article
            v-for="row in group.items"
            :key="row.appointmentId"
            class="appointment-card"
            :class="{
              'card-scheduled': row.status === 'Scheduled',
              'card-checkedin': row.status === 'CheckedIn',
              'card-completed': row.status === 'Completed',
              'card-cancelled': row.status === 'Cancelled'
            }"
          >
            <div class="card-header-row">
              <span class="appointment-id">#{{ row.appointmentId }}</span>
              <span
                class="badge-status"
                :class="{
                  'badge-scheduled': row.status === 'Scheduled',
                  'badge-checkedin': row.status === 'CheckedIn',
                  'badge-completed': row.status === 'Completed',
                  'badge-cancelled': row.status === 'Cancelled'
                }"
              >
                {{ row.status === 'Scheduled' ? 'Đang chờ khám' : row.status === 'CheckedIn' ? 'Đã tiếp đón' : row.status === 'Completed' ? 'Đã hoàn tất' : 'Đã hủy' }}
              </span>
            </div>

            <div class="patient-info">
              <span class="patient-name">{{ row.patientName }}</span>
              <span class="patient-code">({{ row.patientCode }})</span>
            </div>

            <div class="time-meta">
              <span>📅 {{ row.appointmentDate }}</span>
              <span>•</span>
              <span>⏰ <strong>{{ row.slotTime.slice(0, 5) }}</strong></span>
            </div>

            <div v-if="row.notes" class="care-note">
              <span class="note-label">Ghi chú:</span> {{ row.notes }}
            </div>
            <div v-if="row.emergencyReason" class="care-note archive-note">
              <span class="note-label">Lưu trữ:</span> {{ row.emergencyReason }}
            </div>

            <div v-if="row.status === 'Scheduled'" class="card-actions">
              <button class="btn btn-danger-outline" :disabled="busy || loading" @click="select(row)">Hủy lịch này</button>
              <AppointmentRescheduling
                v-if="!selected"
                :appointment-id="row.appointmentId"
                :patient-name="row.patientName"
                @rescheduled="onRescheduled"
              />
            </div>
          </article>
        </div>
      </li>
    </ul>
    <!-- Cancel Modal / Panel -->
    <section v-if="selected" class="editor-overlay" @click.self="close">
      <div class="editor-card" role="dialog" aria-modal="true">
        <div class="editor-header">
          <h2>Xác nhận hủy lịch hẹn #{{ selected.appointmentId }}</h2>
          <button type="button" class="btn-close" :disabled="busy" @click="close">✕</button>
        </div>

        <form class="cancel-form" @submit.prevent="cancel">
          <div class="meta-box">
            <div>Bệnh nhân: <strong>{{ selected.patientName }}</strong> ({{ selected.patientCode }})</div>
            <div>Thời gian: <strong>{{ selected.appointmentDate }} lúc {{ selected.slotTime.slice(0, 5) }}</strong></div>
          </div>

          <div class="form-group">
            <label for="reason">Lý do hủy (tối đa {{ reasonLimit }} ký tự còn lại)</label>
            <textarea
              id="reason"
              v-model.trim="reason"
              :maxlength="reasonLimit"
              :disabled="busy || reasonLimit === 0"
              rows="3"
              placeholder="Nhập lý do bệnh nhân yêu cầu hủy hẹn..."
            ></textarea>
          </div>

          <label class="confirm-checkbox">
            <input v-model="confirmed" type="checkbox" :disabled="busy" />
            <span>Đã đối chiếu lịch hẹn và xác nhận sự đồng thuận hủy từ phía bệnh nhân.</span>
          </label>

          <div class="editor-actions">
            <button class="btn btn-danger" :disabled="busy || !confirmed">
              {{ busy ? 'Đang hủy…' : 'Xác nhận hủy lịch' }}
            </button>
            <button type="button" class="btn btn-outline" :disabled="busy" @click="close">Quay lại</button>
          </div>
        </form>
      </div>
    </section>

    <nav v-if="total > 0" class="pagination-bar" aria-label="Phân trang lịch hẹn">
      <span class="page-info">Tổng <strong>{{ total }}</strong> lịch · Trang <strong>{{ page }}</strong></span>
      <div class="pagination-controls">
        <button class="btn btn-outline" :disabled="loading || busy || page <= 1" @click="load(page - 1)">← Trang trước</button>
        <button class="btn btn-outline" :disabled="loading || busy || !hasNext" @click="load(page + 1)">Trang sau →</button>
      </div>
    </nav>
  </main>
</template>
<script setup lang="ts">
import AppointmentReminderPanel from '@/components/AppointmentReminderPanel.vue';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import AppointmentRescheduling from '@/components/AppointmentRescheduling.vue';
import axios from 'axios';
import { appointmentsService, type AppointmentResponse } from '@/services/appointments';
const date = ref(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()));
const status = ref('Scheduled'), rows = ref<AppointmentResponse[]>([]), page = ref(1), total = ref(0), hasNext = ref(false);
const selected = ref<AppointmentResponse | null>(null), reason = ref(''), confirmed = ref(false);
const busy = ref(false), loading = ref(false), error = ref(''), success = ref('');
const reasonLimit = computed(() => Math.max(0, 500 - (selected.value?.notes?.length ?? 0) - (selected.value?.notes ? 1 : 0) - '[Lý do hủy: ]'.length));
const displayGroups = computed(() => {
  const grouped = new Map<string, AppointmentResponse[]>();
  for (const row of rows.value) {
    const key = row.dentistId
      ? `${row.appointmentDate}:${row.slotTime}:${row.dentistId}`
      : `appointment:${row.appointmentId}`;
    const items = grouped.get(key) ?? [];
    items.push(row);
    grouped.set(key, items);
  }
  return Array.from(grouped, ([key, items]) => ({ key, items }));
});
let version = 0;
function close() { selected.value = null; reason.value = ''; confirmed.value = false; }
async function load(targetPage = 1) {
  if (busy.value) return;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date.value) || !Number.isInteger(targetPage) || targetPage < 1 || targetPage > 1000000 ||
      !['', 'Scheduled', 'Cancelled', 'CheckedIn', 'Completed'].includes(status.value)) { error.value = 'Kiểm tra ngày và trạng thái tra cứu.'; return; }
  const ticket = ++version; close(); rows.value = []; error.value = ''; success.value = ''; loading.value = true; hasNext.value = false; total.value = 0;
  try {
    const result = await appointmentsService.getForDesk(date.value, targetPage, status.value || undefined);
    if (ticket === version) { rows.value = result.items; page.value = result.page; total.value = result.totalCount; hasNext.value = result.hasNextPage; }
  } catch { if (ticket === version) error.value = 'Không thể tải lịch hẹn. Vui lòng thử lại.'; }
  finally { if (ticket === version) loading.value = false; }
}
function select(row: AppointmentResponse) {
  if (busy.value || loading.value || row.status !== 'Scheduled' || !rows.value.some(item => item.appointmentId === row.appointmentId)) return;
  close(); selected.value = row; error.value = ''; success.value = '';
}
async function cancel() {
  if (busy.value || loading.value || !confirmed.value || !selected.value || selected.value.status !== 'Scheduled') return;
  if (reason.value.trim().length > reasonLimit.value) { error.value = 'Lý do hủy vượt số ký tự còn lại.'; return; }
  const ticket = version, id = selected.value.appointmentId; busy.value = true; error.value = ''; success.value = '';
  try {
    await appointmentsService.cancel(id, reason.value.trim() || null);
    if (ticket !== version) return;
    busy.value = false;
    await load(page.value);
    if (version === ticket + 1) success.value = `Đã hủy lịch #${id}.`;
  } catch (cause) {
    if (ticket === version) { confirmed.value = false; error.value = axios.isAxiosError(cause) ? cause.response?.data?.message ?? 'Chưa xác định kết quả. Tải lại lịch để đối chiếu trước khi thử tiếp.' : 'Chưa xác định kết quả. Tải lại lịch để đối chiếu trước khi thử tiếp.'; }
  } finally { if (ticket === version) busy.value = false; }
}
async function onRescheduled() {
  const expected = version + 1;
  await load(page.value);
  if (version === expected) success.value = 'Đã đổi lịch. Tra cứu ngày mới để xem lịch cập nhật.';
}
onMounted(() => load()); onUnmounted(() => { version++; close(); rows.value = []; });
</script>
<style scoped>
.page { max-width: 980px; margin: 0 auto; padding: 1.5rem; color: #1e293b; box-sizing: border-box; }
.header-banner { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.25rem; }
.header-icon { font-size: 2.25rem; background: #e0f2fe; width: 3.5rem; height: 3.5rem; display: flex; align-items: center; justify-content: center; border-radius: 12px; }
.header-titles h1 { margin: 0; font-size: 1.6rem; color: #0f172a; font-weight: 700; }
.subtitle { margin: 0.25rem 0 0; color: #64748b; font-size: 0.95rem; }
.filters-card { display: flex; gap: 1rem; align-items: flex-end; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1rem 1.25rem; margin: 1.25rem 0; flex-wrap: wrap; }
.filter-group { display: flex; flex-direction: column; gap: 0.35rem; flex: 1; min-width: 180px; }
.filter-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
input[type="date"], select { height: 44px; padding: 0.5rem 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.95rem; background: #fff; box-sizing: border-box; }
.btn { height: 44px; padding: 0 1.25rem; border-radius: 8px; font-weight: 600; font-size: 0.95rem; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; border: none; }
.btn-primary { background: #0284c7; color: #fff; }
.btn-outline { background: #fff; color: #334155; border: 1px solid #cbd5e1; }
.btn-danger { background: #dc2626; color: #fff; }
.btn-danger-outline { height: 38px; padding: 0 0.85rem; background: #fff; border: 1px solid #fca5a5; color: #dc2626; border-radius: 6px; font-size: 0.85rem; font-weight: 600; cursor: pointer; }
.btn:disabled { opacity: 0.55; cursor: not-allowed; }
.alert-box { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; margin-bottom: 1rem; }
.alert-error { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
.alert-success { background: #f0fdf4; border: 1px solid #bbf7d0; color: #166534; }
.loading-state, .empty-state { text-align: center; padding: 3rem 1rem; color: #64748b; background: #f8fafc; border-radius: 12px; }
.spinner { width: 30px; height: 30px; border: 3px solid #e2e8f0; border-top-color: #0284c7; border-radius: 50%; animation: spin 0.8s linear infinite; margin: 0 auto 0.75rem; }
@keyframes spin { to { transform: rotate(360deg); } }
.appointment-list { list-style: none; padding: 0; margin: 0 0 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.appointment-group { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 1rem; }
.appointment-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.15rem; display: flex; flex-direction: column; gap: 0.6rem; border-left: 5px solid #cbd5e1; }
.card-scheduled { border-left-color: #0284c7; }
.card-checkedin { border-left-color: #10b981; }
.card-completed { border-left-color: #8b5cf6; }
.card-cancelled { border-left-color: #94a3b8; opacity: 0.75; }
.card-header-row { display: flex; justify-content: space-between; align-items: center; }
.appointment-id { font-weight: 700; color: #0284c7; font-size: 0.95rem; }
.badge-status { font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.55rem; border-radius: 9999px; }
.badge-scheduled { background: #e0f2fe; color: #0369a1; }
.badge-checkedin { background: #dcfce7; color: #15803d; }
.badge-completed { background: #f3e8ff; color: #6b21a8; }
.badge-cancelled { background: #f1f5f9; color: #475569; }
.patient-info { display: flex; align-items: baseline; gap: 0.4rem; }
.patient-name { font-weight: 700; color: #0f172a; font-size: 1.05rem; }
.patient-code { font-size: 0.85rem; color: #64748b; }
.time-meta { font-size: 0.85rem; color: #475569; display: flex; gap: 0.4rem; align-items: center; }
.care-note { font-size: 0.825rem; color: #334155; background: #f8fafc; padding: 0.4rem 0.65rem; border-radius: 6px; border: 1px solid #f1f5f9; white-space: pre-wrap; }
.note-label { font-weight: 600; color: #64748b; }
.card-actions { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 0.5rem; }
.pagination-bar { display: flex; justify-content: space-between; align-items: center; padding-top: 1rem; border-top: 1px solid #e2e8f0; flex-wrap: wrap; gap: 0.5rem; }
.editor-overlay { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.5); backdrop-filter: blur(2px); display: flex; align-items: center; justify-content: center; z-index: 50; padding: 1rem; }
.editor-card { background: #fff; border-radius: 14px; max-width: 480px; width: 100%; padding: 1.5rem; }
.editor-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
.editor-header h2 { margin: 0; font-size: 1.2rem; color: #0f172a; }
.btn-close { background: transparent; border: none; font-size: 1.25rem; color: #94a3b8; cursor: pointer; }
.cancel-form { display: flex; flex-direction: column; gap: 1rem; }
.meta-box { background: #f8fafc; border-radius: 8px; padding: 0.75rem; font-size: 0.875rem; display: flex; flex-direction: column; gap: 0.25rem; }
.form-group { display: flex; flex-direction: column; gap: 0.35rem; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #475569; }
textarea { width: 100%; border: 1px solid #cbd5e1; border-radius: 8px; padding: 0.65rem; font-family: inherit; font-size: 0.95rem; box-sizing: border-box; }
.confirm-checkbox { display: flex; align-items: flex-start; gap: 0.5rem; font-size: 0.85rem; color: #334155; }
.editor-actions { display: flex; gap: 0.75rem; justify-content: flex-end; }
@media (max-width: 640px) { .page { padding: 1rem; } .filters-card { flex-direction: column; } .btn { width: 100%; } }
</style>
