<template>
  <div
    ref="rootEl"
    class="bdp-root"
    :class="{ 'bdp-disabled': disabled, 'bdp-open': calendarOpen }"
    @keydown.esc.stop="calendarOpen = false"
  >
    <input
      :id="inputId"
      type="date"
      :value="modelValue"
      :min="effectiveMin"
      :max="effectiveMax || undefined"
      :disabled="disabled"
      :required="required"
      tabindex="-1"
      aria-hidden="true"
      class="bdp-native-input"
      @input="onNativeInput"
      @focus="onNativeFocus"
    />

    <!-- Thẻ hiển thị ngày đã chọn & nút điều hướng nhanh -->
    <div class="bdp-header-card">
      <button
        type="button"
        class="bdp-display-trigger"
        :disabled="disabled"
        :aria-expanded="calendarOpen"
        aria-label="Mở hoặc đóng lịch chọn ngày khám"
        @click="toggleCalendar"
      >
        <span class="bdp-cal-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect x="3" y="4" width="18" height="18" rx="3" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
        </span>
        <span class="bdp-display-text">
          <strong class="bdp-display-date">{{ displayText }}</strong>
          <span v-if="relativeLabel" class="bdp-relative-pill" :class="relativeToneClass">
            {{ relativeLabel }}
          </span>
        </span>
      </button>

      <div class="bdp-header-controls">
        <button
          type="button"
          class="bdp-step-btn"
          :disabled="disabled || !canPrevDay"
          title="Lùi 1 ngày"
          aria-label="Lùi 1 ngày"
          @click="prevDay"
        >
          ‹
        </button>
        <button
          type="button"
          class="bdp-step-btn"
          :disabled="disabled || !canNextDay"
          title="Tiến 1 ngày"
          aria-label="Tiến 1 ngày"
          @click="nextDay"
        >
          ›
        </button>
        <button
          type="button"
          class="bdp-toggle-btn"
          :class="{ active: calendarOpen }"
          :disabled="disabled"
          :aria-expanded="calendarOpen"
          @click="toggleCalendar"
        >
          <span>{{ calendarOpen ? 'Thu gọn' : 'Lịch tháng' }}</span>
          <span class="bdp-caret" :class="{ open: calendarOpen }" aria-hidden="true">▾</span>
        </button>
      </div>
    </div>

    <!-- Dải chọn nhanh 7 ngày tới -->
    <div class="bdp-quick-strip" role="group" aria-label="Chọn nhanh ngày khám trong 7 ngày tới">
      <button
        v-for="item in quickDays"
        :key="item.iso"
        type="button"
        class="bdp-quick-chip"
        :class="{
          selected: modelValue === item.iso,
          today: item.offset === 0,
          weekend: item.isWeekend
        }"
        :aria-pressed="modelValue === item.iso"
        :disabled="disabled || item.disabled"
        @click="selectDate(item.iso)"
      >
        <span class="bdp-chip-top">{{ item.topLabel }}</span>
        <span class="bdp-chip-date">{{ item.shortDate }}</span>
      </button>
    </div>

    <!-- Lưới lịch tháng Tiếng Việt -->
    <div v-if="calendarOpen" class="bdp-calendar-panel" role="dialog" aria-label="Bảng lịch chọn ngày khám">
      <div class="bdp-month-bar">
        <div class="bdp-month-nav">
          <button
            type="button"
            class="bdp-nav-btn"
            :disabled="disabled || !canPrevMonth"
            aria-label="Tháng trước"
            @click="shiftMonth(-1)"
          >
            ‹
          </button>
          <strong class="bdp-month-title">Tháng {{ viewMonth }} / {{ viewYear }}</strong>
          <button
            type="button"
            class="bdp-nav-btn"
            :disabled="disabled || !canNextMonth"
            aria-label="Tháng sau"
            @click="shiftMonth(1)"
          >
            ›
          </button>
        </div>
        <button
          type="button"
          class="bdp-today-btn"
          :disabled="disabled || !isWithinBounds(effectiveMin)"
          @click="jumpToMinDate"
        >
          Về hôm nay
        </button>
      </div>

      <div class="bdp-weekdays">
        <span v-for="wd in CALENDAR_WEEKDAYS" :key="wd" class="bdp-weekday" :class="{ sun: wd === 'CN' }">{{ wd }}</span>
      </div>

      <div class="bdp-days-grid">
        <button
          v-for="cell in calendarCells"
          :key="cell.iso"
          type="button"
          class="bdp-day-cell"
          :class="{
            'is-out': !cell.inMonth,
            'is-today': cell.isToday,
            'is-selected': cell.isSelected,
            'is-weekend': cell.isWeekend
          }"
          :disabled="disabled || cell.disabled"
          :aria-label="formatFullVietnamese(cell.iso)"
          :aria-pressed="cell.isSelected"
          @click="selectFromCalendar(cell.iso)"
        >
          <span class="bdp-day-num">{{ cell.day }}</span>
          <span v-if="cell.isToday && !cell.isSelected" class="bdp-today-dot" aria-hidden="true"></span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

const props = withDefaults(defineProps<{
  modelValue: string;
  min?: string;
  max?: string;
  inputId?: string;
  disabled?: boolean;
  required?: boolean;
}>(), {
  min: '',
  max: '',
  inputId: 'booking-date-input',
  disabled: false,
  required: false
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  change: [value: string];
  input: [value: string];
}>();

const WEEKDAY_FULL = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
const WEEKDAY_SHORT = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
const CALENDAR_WEEKDAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

function vietnamToday(): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).format(new Date());
}

function isValidIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [y, m, d] = value.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d));
  return dt.getUTCFullYear() === y && dt.getUTCMonth() === m - 1 && dt.getUTCDate() === d;
}

function parseIsoUtc(iso: string): Date | null {
  if (!isValidIsoDate(iso)) return null;
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d));
}

function toIsoUtc(dt: Date): string {
  const y = dt.getUTCFullYear();
  const m = String(dt.getUTCMonth() + 1).padStart(2, '0');
  const d = String(dt.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function addDays(iso: string, offset: number): string {
  const dt = parseIsoUtc(iso);
  if (!dt) return iso;
  return toIsoUtc(new Date(Date.UTC(dt.getUTCFullYear(), dt.getUTCMonth(), dt.getUTCDate() + offset)));
}

function diffDays(fromIso: string, toIso: string): number | null {
  const a = parseIsoUtc(fromIso);
  const b = parseIsoUtc(toIso);
  if (!a || !b) return null;
  return Math.round((b.getTime() - a.getTime()) / 86_400_000);
}

function formatFullVietnamese(iso: string): string {
  const dt = parseIsoUtc(iso);
  if (!dt) return 'Chọn ngày khám';
  const dow = dt.getUTCDay();
  const [, m, d] = iso.split('-');
  const y = iso.slice(0, 4);
  return `${WEEKDAY_FULL[dow]}, ${d}/${m}/${y}`;
}

const todayIso = vietnamToday();
const effectiveMin = computed(() => (isValidIsoDate(props.min) ? props.min : todayIso));
const effectiveMax = computed(() => (isValidIsoDate(props.max) ? props.max : ''));

function isWithinBounds(iso: string): boolean {
  if (!isValidIsoDate(iso)) return false;
  if (effectiveMin.value && iso < effectiveMin.value) return false;
  if (effectiveMax.value && iso > effectiveMax.value) return false;
  return true;
}

const rootEl = ref<HTMLElement | null>(null);
const calendarOpen = ref(false);
const initialRefDate = computed(() => (isValidIsoDate(props.modelValue) ? props.modelValue : effectiveMin.value));
const viewYear = ref(Number(initialRefDate.value.slice(0, 4)));
const viewMonth = ref(Number(initialRefDate.value.slice(5, 7)));

function syncCalendarMonth(iso: string) {
  if (!isValidIsoDate(iso)) return;
  viewYear.value = Number(iso.slice(0, 4));
  viewMonth.value = Number(iso.slice(5, 7));
}

watch(
  () => props.modelValue,
  (val) => {
    if (isValidIsoDate(val)) syncCalendarMonth(val);
  }
);

const displayText = computed(() =>
  isValidIsoDate(props.modelValue) ? formatFullVietnamese(props.modelValue) : 'Chọn ngày khám'
);

const relativeDelta = computed(() =>
  isValidIsoDate(props.modelValue) ? diffDays(effectiveMin.value, props.modelValue) : null
);

const relativeLabel = computed(() => {
  const delta = relativeDelta.value;
  if (delta === null) return '';
  if (delta === 0) return 'Hôm nay';
  if (delta === 1) return 'Ngày mai';
  if (delta === 2) return 'Ngày kia';
  if (delta > 2 && delta <= 30) return `Sau ${delta} ngày`;
  if (delta < 0) return 'Ngày đã qua';
  return '';
});

const relativeToneClass = computed(() => {
  const delta = relativeDelta.value;
  if (delta === 0) return 'tone-today';
  if (delta === 1) return 'tone-tomorrow';
  if (delta !== null && delta < 0) return 'tone-past';
  return 'tone-future';
});

const anchorIso = computed(() => (isValidIsoDate(props.modelValue) ? props.modelValue : effectiveMin.value));

const canPrevDay = computed(() => {
  if (!isValidIsoDate(props.modelValue)) return false;
  return isWithinBounds(addDays(props.modelValue, -1));
});

const canNextDay = computed(() => isWithinBounds(addDays(anchorIso.value, 1)));

const quickDays = computed(() => {
  const base = effectiveMin.value;
  return Array.from({ length: 7 }, (_, offset) => {
    const iso = addDays(base, offset);
    const dt = parseIsoUtc(iso);
    const dow = dt ? dt.getUTCDay() : 0;
    const topLabel = offset === 0 ? 'Hôm nay' : offset === 1 ? 'Ngày mai' : WEEKDAY_SHORT[dow];
    const shortDate = `${iso.slice(8, 10)}/${iso.slice(5, 7)}`;
    return {
      offset,
      iso,
      topLabel,
      shortDate,
      isWeekend: dow === 0 || dow === 6,
      disabled: !isWithinBounds(iso)
    };
  });
});

const canPrevMonth = computed(() => {
  const minDt = parseIsoUtc(effectiveMin.value);
  if (!minDt) return true;
  const minYm = minDt.getUTCFullYear() * 12 + minDt.getUTCMonth();
  const curYm = viewYear.value * 12 + (viewMonth.value - 1);
  return curYm > minYm;
});

const canNextMonth = computed(() => {
  if (!effectiveMax.value) return true;
  const maxDt = parseIsoUtc(effectiveMax.value);
  if (!maxDt) return true;
  const maxYm = maxDt.getUTCFullYear() * 12 + maxDt.getUTCMonth();
  const curYm = viewYear.value * 12 + (viewMonth.value - 1);
  return curYm < maxYm;
});

const calendarCells = computed(() => {
  const y = viewYear.value;
  const m = viewMonth.value;
  const firstDayUtc = new Date(Date.UTC(y, m - 1, 1));
  const leadingCount = (firstDayUtc.getUTCDay() + 6) % 7;
  const daysInMonth = new Date(Date.UTC(y, m, 0)).getUTCDate();
  const totalCells = leadingCount + daysInMonth > 35 ? 42 : 35;
  const startUtc = new Date(Date.UTC(y, m - 1, 1 - leadingCount));
  const baseMin = effectiveMin.value;

  return Array.from({ length: totalCells }, (_, idx) => {
    const dt = new Date(Date.UTC(startUtc.getUTCFullYear(), startUtc.getUTCMonth(), startUtc.getUTCDate() + idx));
    const iso = toIsoUtc(dt);
    const dow = dt.getUTCDay();
    return {
      iso,
      day: dt.getUTCDate(),
      inMonth: dt.getUTCFullYear() === y && dt.getUTCMonth() === m - 1,
      isToday: iso === baseMin,
      isSelected: iso === props.modelValue,
      isWeekend: dow === 0 || dow === 6,
      disabled: !isWithinBounds(iso)
    };
  });
});

function emitDate(iso: string) {
  emit('update:modelValue', iso);
  emit('input', iso);
  emit('change', iso);
}

function selectDate(iso: string) {
  if (props.disabled || !isWithinBounds(iso)) return false;
  emitDate(iso);
  syncCalendarMonth(iso);
  return true;
}

function selectFromCalendar(iso: string) {
  if (selectDate(iso)) {
    calendarOpen.value = false;
  }
}

function prevDay() {
  if (!canPrevDay.value || props.disabled) return;
  selectDate(addDays(props.modelValue, -1));
}

function nextDay() {
  if (!canNextDay.value || props.disabled) return;
  const target = isValidIsoDate(props.modelValue) ? addDays(props.modelValue, 1) : effectiveMin.value;
  selectDate(target);
}

function shiftMonth(delta: number) {
  if (props.disabled) return;
  if (delta < 0 && !canPrevMonth.value) return;
  if (delta > 0 && !canNextMonth.value) return;
  const dt = new Date(Date.UTC(viewYear.value, viewMonth.value - 1 + delta, 1));
  viewYear.value = dt.getUTCFullYear();
  viewMonth.value = dt.getUTCMonth() + 1;
}

function jumpToMinDate() {
  if (selectDate(effectiveMin.value)) {
    calendarOpen.value = false;
  }
}

function toggleCalendar() {
  if (props.disabled) return;
  if (!calendarOpen.value) {
    syncCalendarMonth(isValidIsoDate(props.modelValue) ? props.modelValue : effectiveMin.value);
  }
  calendarOpen.value = !calendarOpen.value;
}

function onNativeInput(event: Event) {
  const val = (event.target as HTMLInputElement | null)?.value ?? '';
  if (val && isWithinBounds(val)) {
    selectDate(val);
  }
}

function onNativeFocus() {
  if (!props.disabled) {
    calendarOpen.value = true;
  }
}

function handleDocumentClick(event: MouseEvent) {
  if (!calendarOpen.value || !rootEl.value) return;
  const target = event.target as Node | null;
  if (target && !rootEl.value.contains(target)) {
    calendarOpen.value = false;
  }
}

onMounted(() => {
  if (typeof document !== 'undefined') {
    document.addEventListener('click', handleDocumentClick);
  }
});

onUnmounted(() => {
  if (typeof document !== 'undefined') {
    document.removeEventListener('click', handleDocumentClick);
  }
});
</script>

<style scoped>
.bdp-root {
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 0.65rem;
  width: 100%;
  min-width: 0;
}

.bdp-disabled {
  opacity: 0.65;
}

.bdp-native-input {
  position: absolute;
  inset: 0 auto auto 0;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
  margin: 0;
  padding: 0;
  border: 0;
}

.bdp-header-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.45rem 0.55rem 0.45rem 0.75rem;
  border: 1.5px solid #c8d9e7;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 6px rgba(16, 46, 86, 0.04);
  transition: border-color 0.16s ease, box-shadow 0.16s ease;
}

.bdp-root.bdp-open .bdp-header-card,
.bdp-header-card:focus-within {
  border-color: #1857b6;
  box-shadow: 0 0 0 3px rgba(24, 87, 182, 0.12);
}

.bdp-display-trigger {
  flex: 1 1 auto;
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 0.65rem;
  border: 0;
  background: transparent;
  padding: 0.25rem 0;
  text-align: left;
  font: inherit;
  color: #102e56;
  cursor: pointer;
}

.bdp-display-trigger:disabled {
  cursor: not-allowed;
}

.bdp-cal-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 9px;
  background: #edf5fc;
  color: #1857b6;
  flex-shrink: 0;
}

.bdp-display-text {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.45rem;
  min-width: 0;
}

.bdp-display-date {
  font-size: 0.98rem;
  font-weight: 700;
  color: #102e56;
}

.bdp-relative-pill {
  display: inline-flex;
  align-items: center;
  padding: 0.16rem 0.55rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.01em;
}

.bdp-relative-pill.tone-today {
  background: #dcfce7;
  color: #15803d;
}

.bdp-relative-pill.tone-tomorrow {
  background: #dbeafe;
  color: #1d4ed8;
}

.bdp-relative-pill.tone-future {
  background: #f1f5f9;
  color: #334155;
}

.bdp-relative-pill.tone-past {
  background: #fee2e2;
  color: #b91c1c;
}

.bdp-header-controls {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  flex-shrink: 0;
}

.bdp-step-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid #d5e2ee;
  border-radius: 8px;
  background: #f8fbff;
  color: #18354c;
  font-size: 1.15rem;
  font-weight: 700;
  line-height: 1;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
}

.bdp-step-btn:hover:not(:disabled) {
  background: #e6f0fc;
  border-color: #93c5fd;
  color: #1857b6;
}

.bdp-step-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.bdp-toggle-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  min-height: 34px;
  padding: 0.35rem 0.7rem;
  border: 1px solid #bfdbfe;
  border-radius: 8px;
  background: #eff6ff;
  color: #1d4ed8;
  font: inherit;
  font-size: 0.82rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s ease;
}

.bdp-toggle-btn:hover:not(:disabled) {
  background: #dbeafe;
  border-color: #93c5fd;
}

.bdp-toggle-btn.active {
  background: #1857b6;
  border-color: #1857b6;
  color: #ffffff;
}

.bdp-toggle-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.bdp-caret {
  display: inline-block;
  font-size: 0.75rem;
  transition: transform 0.16s ease;
}

.bdp-caret.open {
  transform: rotate(180deg);
}

.bdp-quick-strip {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(74px, 1fr));
  gap: 0.4rem;
}

.bdp-quick-chip {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.15rem;
  min-width: 0;
  min-height: 52px;
  padding: 0.4rem 0.2rem;
  overflow: visible;
  border: 1.5px solid #dbe6f0;
  border-radius: 10px;
  background: #f8fbff;
  color: #18354c;
  font: inherit;
  cursor: pointer;
  transition: all 0.15s ease;
}

.bdp-quick-chip:hover:not(:disabled):not(.selected) {
  border-color: #1857b6;
  background: #eff6ff;
  color: #1857b6;
  transform: translateY(-1px);
}

.bdp-quick-chip.weekend:not(.selected) {
  background: #fffaf5;
}

.bdp-quick-chip.today:not(.selected) {
  border-color: #93c5fd;
}

.bdp-quick-chip.selected,
.bdp-quick-chip.selected:hover:not(:disabled) {
  background: #1857b6;
  border-color: #1857b6;
  color: #ffffff;
  box-shadow: 0 4px 10px rgba(24, 87, 182, 0.22);
}

.bdp-quick-chip:disabled {
  opacity: 0.42;
  cursor: not-allowed;
}

.bdp-chip-top {
  font-size: 0.72rem;
  font-weight: 700;
  opacity: 0.9;
  white-space: nowrap;
}

.bdp-chip-date {
  font-size: 0.86rem;
  font-weight: 800;
}

.bdp-calendar-panel {
  border: 1px solid #cbd5e1;
  border-radius: 14px;
  background: #ffffff;
  padding: 0.9rem;
  box-shadow: 0 12px 28px rgba(16, 46, 86, 0.14);
}

.bdp-month-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.bdp-month-nav {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.bdp-nav-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #f8fafc;
  color: #102e56;
  font-size: 1.1rem;
  font-weight: 700;
  cursor: pointer;
}

.bdp-nav-btn:hover:not(:disabled) {
  background: #eff6ff;
  border-color: #93c5fd;
  color: #1857b6;
}

.bdp-nav-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.bdp-month-title {
  min-width: 120px;
  text-align: center;
  font-size: 0.95rem;
  font-weight: 800;
  color: #102e56;
}

.bdp-today-btn {
  padding: 0.32rem 0.65rem;
  border: 1px solid #bbf7d0;
  border-radius: 999px;
  background: #f0fdf4;
  color: #15803d;
  font: inherit;
  font-size: 0.78rem;
  font-weight: 700;
  cursor: pointer;
}

.bdp-today-btn:hover:not(:disabled) {
  background: #dcfce7;
}

.bdp-today-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.bdp-weekdays,
.bdp-days-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 0.3rem;
}

.bdp-weekdays {
  margin-bottom: 0.35rem;
}

.bdp-weekday {
  text-align: center;
  font-size: 0.76rem;
  font-weight: 700;
  color: #64748b;
  padding: 0.25rem 0;
}

.bdp-weekday.sun {
  color: #dc2626;
}

.bdp-day-cell {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 38px;
  border: 1px solid transparent;
  border-radius: 9px;
  background: transparent;
  color: #18354c;
  font: inherit;
  font-size: 0.88rem;
  font-weight: 650;
  cursor: pointer;
  transition: all 0.13s ease;
}

.bdp-day-cell.is-weekend:not(.is-selected):not(:disabled) {
  color: #b45309;
}

.bdp-day-cell.is-out {
  color: #94a3b8;
  font-weight: 500;
}

.bdp-day-cell:hover:not(:disabled):not(.is-selected) {
  background: #eff6ff;
  border-color: #bfdbfe;
  color: #1857b6;
}

.bdp-day-cell.is-today:not(.is-selected) {
  border-color: #1857b6;
  background: #f0f7ff;
  color: #1857b6;
  font-weight: 800;
}

.bdp-day-cell.is-selected {
  background: #1857b6;
  border-color: #1857b6;
  color: #ffffff;
  font-weight: 800;
  box-shadow: 0 3px 8px rgba(24, 87, 182, 0.28);
}

.bdp-day-cell:disabled {
  color: #cbd5e1;
  background: #f8fafc;
  cursor: not-allowed;
  text-decoration: line-through;
}

.bdp-today-dot {
  position: absolute;
  bottom: 4px;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: #1857b6;
}

@media (max-width: 600px) {
  .bdp-header-card {
    flex-wrap: wrap;
    padding: 0.55rem 0.65rem;
  }

  .bdp-quick-strip {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}
</style>
