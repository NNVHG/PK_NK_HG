<template>
  <div>
    <textarea id="diagnosis" :value="modelValue" :disabled="disabled" required maxlength="1000" rows="4" @input="input" aria-describedby="diagnosis-help" />
    <p id="diagnosis-help">Nhập từ khóa để tìm theo mã hoặc tên không dấu. Có thể chọn gợi ý hoặc tiếp tục nhập chẩn đoán tự do.</p>
    <p v-if="loading" role="status">Đang tìm danh mục…</p>
    <p v-if="error" role="status">{{ error }} Bạn vẫn có thể nhập tự do.</p>
    <ul v-if="items.length" aria-label="Gợi ý chẩn đoán"><li v-for="item in items" :key="item.id">
      <button type="button" :disabled="disabled" @click="choose(item)">{{ item.code }} · {{ item.name }}</button>
    </li></ul>
  </div>
</template>
<script setup lang="ts">
import { ref, onUnmounted } from 'vue';
import { diagnosesService, type Diagnosis } from '@/services/diagnoses';
const props = defineProps<{ modelValue: string; disabled?: boolean }>();
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>();
const items = ref<Diagnosis[]>([]), loading = ref(false), error = ref('');
let version = 0, timer: ReturnType<typeof setTimeout> | undefined;
function input(event: Event) {
  if (props.disabled) return;
  const value = (event.target as HTMLTextAreaElement).value;
  emit('update:modelValue', value); search(value);
}
function search(value: string) {
  const ticket = ++version; clearTimeout(timer); items.value = []; error.value = ''; loading.value = false;
  if (!value.trim() || value.trim().length > 200 || props.disabled) return;
  timer = setTimeout(async () => {
    loading.value = true;
    try { const page = await diagnosesService.list(value.trim()); if (ticket === version && !props.disabled && props.modelValue === value) items.value = page.items.filter(i => i.isActive); }
    catch { if (ticket === version) error.value = 'Không tải được danh mục.'; }
    finally { if (ticket === version) loading.value = false; }
  }, 300);
}
function choose(item: Diagnosis) {
  if (props.disabled || !items.value.some(i => i.id === item.id && i.isActive)) return;
  version++; clearTimeout(timer); items.value = []; loading.value = false; emit('update:modelValue', item.name);
}
onUnmounted(() => { version++; clearTimeout(timer); items.value = []; error.value = ''; });
</script>
<style scoped>
textarea {
  width: 100%;
  box-sizing: border-box;
  min-height: 80px;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
  resize: vertical;
}

textarea:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

ul {
  list-style: none;
  padding: 0;
  max-height: 14rem;
  overflow: auto;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  margin: 0.5rem 0;
  background: #ffffff;
  box-shadow: 0 4px 12px rgba(15, 45, 89, 0.08);
}

li {
  border-bottom: 1px solid #f1f5f9;
}

li:last-child {
  border-bottom: none;
}

button {
  width: 100%;
  text-align: left;
  min-height: 40px;
  padding: 0.5rem 0.75rem;
  border: none;
  background: transparent;
  color: #1e293b;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background 0.15s;
}

button:hover {
  background: #f0f9ff;
  color: #0284c7;
}

button:focus-visible {
  outline: 2px solid #0284c7;
  outline-offset: -2px;
}

p {
  font-size: 0.85rem;
  color: #64748b;
  margin: 0.25rem 0;
}
</style>
