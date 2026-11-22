<template>
  <div class="vn-dob-wrap" :class="{ 'vn-dob-wrap--readonly': readonly }">
    <div class="vn-dob-grid">
      <div class="vn-dob-col">
        <label :for="`${idPrefix}-day`" class="vn-dob-sub">Ngày</label>
        <select
          :id="`${idPrefix}-day`"
          v-model.number="day"
          class="vn-dob-select"
          :disabled="disabled || readonly"
          :required="required"
          aria-label="Ngày sinh"
          @change="onPartChange"
        >
          <option :value="0">Ngày</option>
          <option v-for="d in dayOptions" :key="d" :value="d">
            {{ String(d).padStart(2, '0') }}
          </option>
        </select>
      </div>

      <div class="vn-dob-col">
        <label :for="`${idPrefix}-month`" class="vn-dob-sub">Tháng</label>
        <select
          :id="`${idPrefix}-month`"
          v-model.number="month"
          class="vn-dob-select"
          :disabled="disabled || readonly"
          :required="required"
          aria-label="Tháng sinh"
          @change="onPartChange"
        >
          <option :value="0">Tháng</option>
          <option v-for="m in 12" :key="m" :value="m">
            Tháng {{ m }}
          </option>
        </select>
      </div>

      <div class="vn-dob-col vn-dob-col--year">
        <label :for="`${idPrefix}-year`" class="vn-dob-sub">Năm sinh</label>
        <select
          :id="`${idPrefix}-year`"
          v-model.number="year"
          class="vn-dob-select"
          :disabled="disabled || readonly"
          :required="required"
          aria-label="Năm sinh"
          @change="onPartChange"
        >
          <option :value="0">Năm</option>
          <option v-for="y in yearOptions" :key="y" :value="y">
            {{ y }}
          </option>
        </select>
      </div>
    </div>

    <p v-if="rangeWarning" class="vn-dob-warning" role="alert">{{ rangeWarning }}</p>
    <p v-else-if="formattedPreview" class="vn-dob-preview">
      <span>Ngày sinh đã chọn: <strong>{{ formattedPreview }}</strong></span>
      <span v-if="isFullChildDob" class="vn-dob-child-pill">👶 Trẻ em (&lt; 14 tuổi)</span>
    </p>
    <p v-else-if="isPartialChildYear" class="vn-dob-child-hint">
      👶 Năm sinh {{ year }} thuộc nhóm trẻ em (&lt; 14 tuổi) — chọn đủ Ngày và Tháng sinh để hiện mục người giám hộ.
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { isUnderFourteen } from '@/services/patientAge';

const props = withDefaults(defineProps<{
  modelValue: string;
  max?: string;
  disabled?: boolean;
  readonly?: boolean;
  required?: boolean;
  idPrefix?: string;
}>(), {
  max: '',
  disabled: false,
  readonly: false,
  required: false,
  idPrefix: 'vn-dob'
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  change: [value: string];
}>();

const day = ref(0);
const month = ref(0);
const year = ref(0);
const rangeWarning = ref('');

const fallbackToday = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit'
}).format(new Date());

const effectiveMax = computed(() => (/^\d{4}-\d{2}-\d{2}$/.test(props.max) ? props.max : fallbackToday));

const maxYear = computed(() => Number(effectiveMax.value.slice(0, 4)));

const yearOptions = computed(() => {
  const list: number[] = [];
  for (let y = maxYear.value; y >= maxYear.value - 120; y--) {
    list.push(y);
  }
  return list;
});

const maxDaysInMonth = computed(() => {
  if (month.value < 1 || month.value > 12) return 31;
  const targetYear = year.value >= 1900 ? year.value : 2000;
  return new Date(Date.UTC(targetYear, month.value, 0)).getUTCDate();
});

const dayOptions = computed(() => {
  const list: number[] = [];
  for (let d = 1; d <= maxDaysInMonth.value; d++) {
    list.push(d);
  }
  return list;
});

const formattedPreview = computed(() => {
  if (day.value <= 0 || month.value <= 0 || year.value <= 0 || rangeWarning.value) return '';
  return `${String(day.value).padStart(2, '0')}/${String(month.value).padStart(2, '0')}/${year.value}`;
});

const isFullChildDob = computed(() =>
  Boolean(props.modelValue && !rangeWarning.value && isUnderFourteen(props.modelValue, effectiveMax.value))
);

const isPartialChildYear = computed(
  () =>
    !props.modelValue &&
    !rangeWarning.value &&
    year.value > 0 &&
    maxYear.value - year.value < 14 &&
    (day.value === 0 || month.value === 0)
);

function syncFromModel(value: string) {
  rangeWarning.value = '';
  const clean = (value || '').slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(clean)) {
    year.value = 0;
    month.value = 0;
    day.value = 0;
    return;
  }
  const [y, m, d] = clean.split('-').map(Number);
  year.value = y;
  month.value = m;
  day.value = d;
}

watch(() => props.modelValue, syncFromModel, { immediate: true });

function onPartChange() {
  rangeWarning.value = '';
  if (day.value > maxDaysInMonth.value) {
    day.value = maxDaysInMonth.value;
  }
  if (year.value > 0 && month.value > 0 && day.value > 0) {
    const iso = `${String(year.value).padStart(4, '0')}-${String(month.value).padStart(2, '0')}-${String(day.value).padStart(2, '0')}`;
    if (iso > effectiveMax.value) {
      rangeWarning.value = 'Ngày sinh không được lớn hơn ngày hiện tại.';
      if (props.modelValue !== '') {
        emit('update:modelValue', '');
        emit('change', '');
      }
      return;
    }
    if (props.modelValue !== iso) {
      emit('update:modelValue', iso);
      emit('change', iso);
    }
  } else if (props.modelValue !== '') {
    emit('update:modelValue', '');
    emit('change', '');
  }
}
</script>

<style scoped>
.vn-dob-wrap {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.vn-dob-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.15fr) minmax(0, 1.15fr);
  gap: 8px;
}

.vn-dob-col {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.vn-dob-sub {
  font-size: 0.75rem;
  font-weight: 600;
  color: #4a6378;
}

.vn-dob-select {
  box-sizing: border-box;
  width: 100%;
  min-width: 0;
  min-height: 46px;
  border: 1px solid #c8d9e7;
  border-radius: 9px;
  padding: 10px 8px;
  background: #ffffff;
  color: #18354c;
  font: inherit;
  font-size: 0.95rem;
}

.vn-dob-wrap--readonly .vn-dob-select:disabled {
  background: #f4f7fa;
  color: #445d72;
}

.vn-dob-preview {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin: 0;
  font-size: 0.78rem;
  color: #1857b6;
}

.vn-dob-child-pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border-radius: 999px;
  background: #fef3c7;
  border: 1px solid #f59e0b;
  color: #92400e;
  font-size: 0.74rem;
  font-weight: 700;
}

.vn-dob-child-hint {
  margin: 0;
  padding: 6px 10px;
  border-radius: 8px;
  background: #fffbeb;
  border: 1px solid #fde68a;
  color: #92400e;
  font-size: 0.78rem;
  line-height: 1.4;
}

.vn-dob-warning {
  margin: 0;
  font-size: 0.78rem;
  font-weight: 600;
  color: #b42318;
}

@media (max-width: 420px) {
  .vn-dob-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .vn-dob-col--year {
    grid-column: 1 / -1;
  }
}
</style>
