<template>
  <div class="booking-time">
    <input
      :id="inputId"
      :value="modelValue"
      type="time"
      step="60"
      :disabled="disabled"
      required
      @input="updateTime"
    />
    <label v-if="filteredSlots.length" class="suggestions">
      <span>Giờ gợi ý</span>
      <select :value="filteredSlots.includes(modelValue) ? modelValue : ''" :disabled="disabled" @change="selectSuggestion">
        <option value="" disabled>Chọn giờ gợi ý</option>
        <option v-for="slot in filteredSlots" :key="slot" :value="slot">{{ slot }}</option>
      </select>
    </label>
    <p v-else-if="slots.length && isTodaySelected && hidePastSlots" class="past-slots-hint">
      Hôm nay đã qua các khung giờ gợi ý. Vui lòng chọn ngày khám khác.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  inputId: string;
  modelValue: string;
  slots: string[];
  disabled?: boolean;
  selectedDate?: string;
  hidePastSlots?: boolean;
}>();
const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

function vietnamNow(): { date: string; time: string } {
  const now = new Date();
  const date = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit'
  }).format(now);
  const time = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  }).format(now);
  return { date, time };
}

const isTodaySelected = computed(() => Boolean(props.selectedDate) && props.selectedDate === vietnamNow().date);

const filteredSlots = computed(() => {
  if (!props.hidePastSlots || !isTodaySelected.value) return props.slots;
  const nowTime = vietnamNow().time;
  return props.slots.filter(slot => slot > nowTime);
});

function updateTime(event: Event) {
  emit('update:modelValue', (event.target as HTMLInputElement).value);
}

function selectSuggestion(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (value) emit('update:modelValue', value);
}
</script>

<style scoped>
.booking-time, .suggestions { display: grid; min-width: 0; gap: 0.35rem; }
.booking-time { gap: 0.6rem; }
.suggestions { color: #475569; font-size: 0.875rem; }
.past-slots-hint {
  margin: 0;
  font-size: 0.82rem;
  color: #9a3412;
  background: #fff7ed;
  border: 1px solid #fed7aa;
  border-radius: 8px;
  padding: 0.45rem 0.65rem;
}
.booking-time input, .booking-time select {
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  min-height: 44px;
  padding: 0.5rem 0.75rem;
  border: 1px solid #b9c9db;
  border-radius: 8px;
  background: #fff;
  color: #18354c;
  font: inherit;
  font-size: 1rem;
}
</style>
