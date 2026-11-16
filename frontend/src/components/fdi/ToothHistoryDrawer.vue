<template>
  <Transition name="history-slide-up">
    <section
      v-if="visible"
      ref="drawerRef"
      class="history-drawer"
      aria-labelledby="history-title"
    >
      <header class="drawer-header">
        <div class="drawer-title-wrap">
          <span v-if="!allJaw && tooth" class="tooth-pill">#{{ tooth }}</span>
          <div>
            <h2 id="history-title" class="drawer-title">
              {{ allJaw ? 'Lịch sử toàn bộ cung hàm' : `Lịch sử điều trị răng #${tooth}` }}
            </h2>
            <p class="drawer-subtitle">
              Chọn trực tiếp răng khác trên sơ đồ phía trên để xem lịch sử mà không cần đóng.
            </p>
          </div>
        </div>

        <div class="drawer-controls">
          <label class="scope-toggle">
            <input v-model="allJaw" type="checkbox" @change="load(1)" />
            <span>{{ allJaw ? 'Đang xem toàn hàm' : 'Xem toàn bộ cung hàm' }}</span>
          </label>

          <slot name="actions" :tooth-number="tooth" :all-jaw="allJaw" />

          <button type="button" class="btn-drawer" :disabled="loading" @click="load(page)">
            ↻ Tải lại
          </button>
          <button type="button" class="btn-drawer btn-drawer-close" aria-label="Đóng lịch sử răng" @click="close">
            ✕ Đóng
          </button>
        </div>
      </header>

      <div class="drawer-body">
        <div v-if="loading" role="status" class="drawer-state drawer-loading">
          <span class="spinner" aria-hidden="true"></span>
          <span>Đang tải lịch sử răng…</span>
        </div>

        <div v-else-if="error" role="alert" class="drawer-state drawer-error">
          <span>⚠️ {{ error }}</span>
        </div>

        <div v-else-if="!items.length" class="drawer-state drawer-empty">
          <span>🦷 Chưa có lịch sử ghi nhận trong phạm vi đã chọn.</span>
        </div>

        <ol v-else class="timeline">
          <li
            v-for="item in items"
            :key="`${item.eventType}-${item.id}`"
            class="timeline-card"
            :class="conditionCardClass(item.conditionCode)"
          >
            <div class="card-top">
              <div class="card-target">
                <span class="target-badge">
                  {{ item.toothNumber === null ? 'Toàn hàm' : `Răng ${item.toothNumber}` }}
                </span>
                <span class="surface-text">{{ formatSurfaceCodes(item.surface) }}</span>
                <span v-if="item.conditionCode" class="condition-badge" :class="conditionBadgeClass(item.conditionCode)">
                  {{ conditionLabel(item.conditionCode) }}
                </span>
              </div>
              <span class="visit-chip">Lần khám #{{ item.visitId }}</span>
            </div>

            <div class="card-meta">
              <span>📅 Ngày khám: <strong>{{ new Date(item.visitDate).toLocaleDateString('vi-VN') }}</strong></span>
              <span>👨‍⚕️ Nha sĩ: <strong>{{ item.dentistName || 'Chưa phân công' }}</strong></span>
              <span>🕒 Ghi nhận: {{ new Date(item.createdAt).toLocaleString('vi-VN') }}</span>
            </div>

            <p v-if="item.note" class="note">📝 {{ item.note }}</p>

            <ul v-if="item.services && item.services.length" class="service-list">
              <li v-for="service in item.services" :key="service.assignmentId" class="service-item">
                <span class="srv-code">{{ service.code }}</span>
                <span class="srv-name">{{ service.name }}</span>
                <span class="srv-price">{{ service.quantity }} × {{ money(service.unitPrice) }}</span>
              </li>
            </ul>
          </li>
        </ol>
      </div>

      <footer v-if="!error && totalPages > 1" class="drawer-footer">
        <button type="button" class="btn-drawer" :disabled="loading || page <= 1" @click="load(page - 1)">
          ← Trang trước
        </button>
        <span class="page-indicator">Trang {{ page }} / {{ Math.max(totalPages, 1) }}</span>
        <button type="button" class="btn-drawer" :disabled="loading || page >= totalPages" @click="load(page + 1)">
          Trang sau →
        </button>
      </footer>
    </section>
  </Transition>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';
import axios from 'axios';
import { toothHistoryApi, type ToothHistoryItem } from '@/services/toothHistory';
import { FDI_CONDITIONS } from '@/services/fdi';
import { formatSurfaceCodes, toothConditionColor } from '@/types/fdi';

const props = defineProps<{ patientId: number }>();
const drawerRef = ref<HTMLElement | null>(null);
const tooth = ref<number | null>(null);
const allJaw = ref(false);
const visible = ref(false);
const items = ref<ToothHistoryItem[]>([]);
const loading = ref(false);
const error = ref('');
const page = ref(1);
const totalPages = ref(0);
let version = 0;

const money = (value: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);

const conditionLabel = (code: string) =>
  FDI_CONDITIONS[code as keyof typeof FDI_CONDITIONS] || code;

function conditionCardClass(code?: string | null) {
  const color = toothConditionColor(code ?? undefined);
  return color ? `accent-${color}` : '';
}

function conditionBadgeClass(code?: string | null) {
  const color = toothConditionColor(code ?? undefined);
  return color ? `badge-${color}` : '';
}

function validTooth(n: number) {
  return (
    Number.isInteger(n) &&
    (((n / 10 >= 1 && n / 10 < 5) && n % 10 >= 1 && n % 10 <= 8) ||
      ((n / 10 >= 5 && n / 10 < 9) && n % 10 >= 1 && n % 10 <= 5))
  );
}

async function open(number: number) {
  if (!Number.isInteger(props.patientId) || props.patientId <= 0 || !validTooth(number)) return;
  tooth.value = number;
  allJaw.value = false;
  visible.value = true;
  await load(1);
}

async function load(nextPage: number) {
  if (!visible.value || tooth.value === null || !Number.isInteger(nextPage) || nextPage < 1 || nextPage > 1000000) {
    return;
  }
  const ticket = ++version;
  loading.value = true;
  error.value = '';
  items.value = [];
  page.value = nextPage;
  totalPages.value = 0;
  try {
    const result = await toothHistoryApi.get(props.patientId, allJaw.value ? null : tooth.value, nextPage);
    if (ticket !== version || !visible.value) return;
    items.value = result.items;
    page.value = result.page;
    totalPages.value = result.totalPages;
  } catch (cause) {
    if (ticket === version && visible.value) {
      error.value = axios.isAxiosError(cause)
        ? cause.response?.data?.message || 'Không tải được lịch sử. Hãy tải lại.'
        : 'Không tải được lịch sử. Hãy tải lại.';
    }
  } finally {
    if (ticket === version) loading.value = false;
  }
}

function close() {
  version++;
  visible.value = false;
  items.value = [];
  error.value = '';
  loading.value = false;
  tooth.value = null;
  page.value = 1;
  totalPages.value = 0;
}

watch(() => props.patientId, close);
onUnmounted(close);
defineExpose({ open, close });
</script>

<style scoped>
.history-drawer {
  background: #ffffff;
  border: 1.5px solid #cde2f5;
  border-radius: 16px;
  box-shadow: 0 12px 30px rgba(16, 46, 86, 0.08);
  overflow: hidden;
  color: #18354c;
}

.history-slide-up-enter-active,
.history-slide-up-leave-active {
  transition: transform 0.24s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.2s ease;
}

.history-slide-up-enter-from,
.history-slide-up-leave-to {
  opacity: 0;
  transform: translateY(18px);
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  padding: 1rem 1.25rem;
  background: #f4faff;
  border-bottom: 1px solid #d8ecfa;
}

.drawer-title-wrap {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.tooth-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 44px;
  height: 38px;
  padding: 0 0.65rem;
  border-radius: 10px;
  background: #1857b6;
  color: #ffffff;
  font-weight: 800;
  font-size: 0.95rem;
}

.drawer-title {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: #102e56;
}

.drawer-subtitle {
  margin: 0.15rem 0 0;
  font-size: 0.8rem;
  color: #526c80;
}

.drawer-controls {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.scope-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.42rem 0.75rem;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  font-size: 0.82rem;
  font-weight: 600;
  color: #18354c;
  cursor: pointer;
}

.btn-drawer {
  font-family: inherit;
  font-size: 0.82rem;
  font-weight: 700;
  padding: 0.45rem 0.85rem;
  min-height: 36px;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #18354c;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s ease;
}

.btn-drawer:hover:not(:disabled) {
  background: #e8f4fd;
  border-color: #1857b6;
  color: #102e56;
}

.btn-drawer:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.btn-drawer-close {
  border-color: #fecaca;
  background: #fff5f5;
  color: #b91c1c;
}

.btn-drawer-close:hover:not(:disabled) {
  background: #fee2e2;
  border-color: #f87171;
  color: #991b1b;
}

.drawer-body {
  padding: 1rem 1.25rem;
  max-height: 380px;
  overflow-y: auto;
}

.drawer-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 1.5rem 1rem;
  border-radius: 10px;
  background: #f8fafc;
  border: 1px dashed #cbd5e1;
  color: #64748b;
  font-size: 0.9rem;
}

.drawer-error {
  background: #fef2f2;
  border-color: #fecaca;
  color: #b91c1c;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid #cbd5e1;
  border-top-color: #1857b6;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.timeline {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 0.85rem;
}

.timeline-card {
  padding: 0.9rem 1rem;
  border: 1px solid #e2e8f0;
  border-left: 4px solid #3b82f6;
  border-radius: 10px;
  background: #f8fbff;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.timeline-card.accent-caries { border-left-color: #ef4444; }
.timeline-card.accent-restored { border-left-color: #1857b6; }
.timeline-card.accent-treatment { border-left-color: #d97706; }
.timeline-card.accent-prosthetic { border-left-color: #8b5cf6; }
.timeline-card.accent-missing { border-left-color: #64748b; }

.card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.card-target {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.target-badge {
  background: #e0f2fe;
  color: #0369a1;
  font-size: 0.78rem;
  font-weight: 800;
  padding: 0.15rem 0.5rem;
  border-radius: 6px;
}

.surface-text {
  font-size: 0.82rem;
  font-weight: 700;
  color: #334155;
}

.condition-badge {
  font-size: 0.75rem;
  font-weight: 700;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: #e2e8f0;
  color: #1e293b;
}

.badge-caries { background: #fee2e2; color: #b91c1c; }
.badge-restored { background: #dbeafe; color: #1d4ed8; }
.badge-treatment { background: #fef3c7; color: #b45309; }
.badge-prosthetic { background: #ede9fe; color: #6d28d9; }
.badge-missing { background: #f1f5f9; color: #475569; }

.visit-chip {
  font-size: 0.75rem;
  font-weight: 700;
  color: #64748b;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  padding: 0.12rem 0.45rem;
  border-radius: 999px;
}

.card-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 0.9rem;
  font-size: 0.78rem;
  color: #475569;
}

.note {
  margin: 0.15rem 0 0;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  background: #fffbeb;
  border: 1px solid #fde68a;
  padding: 0.45rem 0.65rem;
  border-radius: 6px;
  color: #92400e;
  font-size: 0.82rem;
}

.service-list {
  list-style: none;
  padding: 0;
  margin: 0.15rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.service-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  font-size: 0.8rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 0.3rem 0.55rem;
}

.srv-code { font-weight: 700; color: #0369a1; }
.srv-name { flex: 1; color: #1e293b; }
.srv-price { font-weight: 700; color: #0f172a; }

.drawer-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 0.75rem 1.25rem;
  background: #f8fafc;
  border-top: 1px solid #e2e8f0;
}

.page-indicator {
  font-size: 0.82rem;
  font-weight: 700;
  color: #475569;
}
</style>
