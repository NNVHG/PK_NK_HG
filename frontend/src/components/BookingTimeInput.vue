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
    <label v-if="slots.length" class="suggestions">
      <span>Giờ gợi ý</span>
      <select :value="slots.includes(modelValue) ? modelValue : ''" :disabled="disabled" @change="selectSuggestion">
        <option value="" disabled>Chọn giờ gợi ý</option>
        <option v-for="slot in slots" :key="slot" :value="slot">{{ slot }}</option>
      </select>
    </label>
  </div>
</template>

<script setup lang="ts">
defineProps<{ inputId: string; modelValue: string; slots: string[]; disabled?: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

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
