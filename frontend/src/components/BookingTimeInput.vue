<template>
  <div class="booking-session">
    <input
      :id="inputId"
      :value="modelValue"
      type="text"
      readonly
      tabindex="-1"
      aria-hidden="true"
      class="session-anchor-input"
    />

    <div class="session-grid" role="radiogroup" aria-label="Chọn buổi khám">
      <button
        type="button"
        role="radio"
        class="session-card"
        :class="{ selected: selectedSession === 'morning' }"
        :aria-checked="selectedSession === 'morning'"
        :disabled="disabled || !availableMorningSlots.length"
        @click="selectSession('morning')"
      >
        <span class="session-icon" aria-hidden="true">🌤️</span>
        <span class="session-info">
          <strong class="session-title">Buổi sáng</strong>
          <span class="session-window">{{ morningRangeLabel }}</span>
        </span>
        <span v-if="selectedSession === 'morning'" class="session-check" aria-hidden="true">✓</span>
      </button>

      <button
        type="button"
        role="radio"
        class="session-card"
        :class="{ selected: selectedSession === 'afternoon' }"
        :aria-checked="selectedSession === 'afternoon'"
        :disabled="disabled || !availableAfternoonSlots.length"
        @click="selectSession('afternoon')"
      >
        <span class="session-icon" aria-hidden="true">🌇</span>
        <span class="session-info">
          <strong class="session-title">Buổi chiều</strong>
          <span class="session-window">{{ afternoonRangeLabel }}</span>
        </span>
        <span v-if="selectedSession === 'afternoon'" class="session-check" aria-hidden="true">✓</span>
      </button>
    </div>

    <p v-if="slots.length && hidePastSlots && isTodaySelected && !availableMorningSlots.length && !availableAfternoonSlots.length" class="past-slots-hint">
      {{ AFTER_HOURS_BOOKING_NOTICE }}
    </p>
    <p v-else-if="slots.length && hidePastSlots && isTodaySelected && !availableMorningSlots.length" class="past-slots-hint">
      Buổi sáng hôm nay đã hết giờ nhận lịch trực tuyến; bạn có thể chọn Buổi chiều hoặc ngày khác.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue';
import { AFTER_HOURS_BOOKING_NOTICE, getVietnamNow } from '@/utils/bookingWindow';

const props = defineProps<{
  inputId: string;
  modelValue: string;
  slots: string[];
  disabled?: boolean;
  selectedDate?: string;
  hidePastSlots?: boolean;
}>();
const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

const isTodaySelected = computed(() => Boolean(props.selectedDate) && props.selectedDate === getVietnamNow().date);

const allMorningSlots = computed(() => props.slots.filter(slot => slot < '12:00'));
const allAfternoonSlots = computed(() => props.slots.filter(slot => slot >= '12:00'));

const availableMorningSlots = computed(() => {
  if (!props.hidePastSlots || !isTodaySelected.value) return allMorningSlots.value;
  const nowTime = getVietnamNow().time;
  return allMorningSlots.value.filter(slot => slot > nowTime);
});

const availableAfternoonSlots = computed(() => {
  if (!props.hidePastSlots || !isTodaySelected.value) return allAfternoonSlots.value;
  const nowTime = getVietnamNow().time;
  return allAfternoonSlots.value.filter(slot => slot > nowTime);
});

const selectedSession = computed<'morning' | 'afternoon' | ''>(() => {
  if (!props.modelValue) return '';
  return props.modelValue.slice(0, 5) < '12:00' ? 'morning' : 'afternoon';
});

const morningRangeLabel = computed(() => {
  const first = allMorningSlots.value[0];
  return first ? `Từ ${first} – 12:00` : '08:00 – 12:00';
});

const afternoonRangeLabel = computed(() => {
  const first = allAfternoonSlots.value[0];
  return first ? `Từ ${first} – 20:00` : '13:30 – 20:00';
});

function selectSession(session: 'morning' | 'afternoon') {
  if (props.disabled) return;
  const list = session === 'morning' ? availableMorningSlots.value : availableAfternoonSlots.value;
  if (!list.length) return;
  emit('update:modelValue', list[0]);
}

watch(
  [() => props.selectedDate, () => props.slots, () => props.hidePastSlots],
  () => {
    if (!props.modelValue || !props.slots.length) return;
    if (!props.hidePastSlots || !isTodaySelected.value) return;
    const nowTime = getVietnamNow().time;
    const list = selectedSession.value === 'morning' ? availableMorningSlots.value : availableAfternoonSlots.value;
    if (!list.length) {
      emit('update:modelValue', '');
      return;
    }
    if (props.modelValue <= nowTime) {
      emit('update:modelValue', list[0]);
    }
  }
);
</script>

<style scoped>
.booking-session {
  position: relative;
  display: grid;
  gap: 0.6rem;
  min-width: 0;
}

.session-anchor-input {
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

.session-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.session-card {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  min-height: 60px;
  padding: 0.7rem 0.9rem;
  border: 1.5px solid #c8d9e7;
  border-radius: 12px;
  background: #f8fbff;
  color: #18354c;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: all 0.16s ease;
}

.session-card:hover:not(:disabled):not(.selected) {
  border-color: #1857b6;
  background: #eff6ff;
  transform: translateY(-1px);
}

.session-card.selected,
.session-card.selected:hover:not(:disabled) {
  background: #1857b6;
  border-color: #1857b6;
  color: #ffffff;
  box-shadow: 0 4px 12px rgba(24, 87, 182, 0.24);
}

.session-card:disabled {
  opacity: 0.48;
  cursor: not-allowed;
  background: #f1f5f9;
}

.session-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  border-radius: 10px;
  background: #ffffff;
  font-size: 1.25rem;
  flex-shrink: 0;
}

.session-card.selected .session-icon {
  background: rgba(255, 255, 255, 0.18);
}

.session-info {
  display: flex;
  flex-direction: column;
  gap: 0.12rem;
  min-width: 0;
  flex: 1;
}

.session-title {
  font-size: 0.96rem;
  font-weight: 800;
}

.session-window {
  font-size: 0.78rem;
  font-weight: 600;
  opacity: 0.85;
}

.session-check {
  font-size: 1rem;
  font-weight: 900;
  flex-shrink: 0;
}

.past-slots-hint {
  margin: 0;
  font-size: 0.82rem;
  color: #9a3412;
  background: #fff7ed;
  border: 1px solid #fed7aa;
  border-radius: 8px;
  padding: 0.5rem 0.7rem;
}

@media (max-width: 520px) {
  .session-grid {
    grid-template-columns: 1fr;
  }
}
</style>
