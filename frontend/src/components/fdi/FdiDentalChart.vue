<template>
  <div class="fdi-chart-container" :class="{ 'is-readonly': readonly }">
    <!-- Thanh điều khiển phía trên -->
    <header class="chart-header">
      <div class="header-left">
        <h3 class="chart-title">Sơ đồ răng FDI</h3>
        <!-- Nha sĩ chọn loại bộ răng (DL-018) -->
        <div class="dentition-selector" role="radiogroup" aria-label="Chọn loại bộ răng">
          <button
            v-for="opt in dentitionOptions"
            :key="opt.value"
            type="button"
            class="dentition-btn"
            :class="{ active: currentDentition === opt.value }"
            :disabled="readonly"
            role="radio"
            :aria-checked="currentDentition === opt.value"
            @click="setDentition(opt.value)"
          >
            {{ opt.label }}
          </button>
        </div>
      </div>

      <div class="header-right">
        <!-- Bộ công cụ thu phóng -->
        <div class="zoom-controls" role="group" aria-label="Thu phóng sơ đồ">
          <button
            type="button"
            class="zoom-btn"
            title="Thu nhỏ"
            aria-label="Thu nhỏ sơ đồ"
            :disabled="zoomLevel <= 0.75"
            @click="zoomOut"
          >
            −
          </button>
          <span class="zoom-text">{{ Math.round(zoomLevel * 100) }}%</span>
          <button
            type="button"
            class="zoom-btn"
            title="Phóng to"
            aria-label="Phóng to sơ đồ"
            :disabled="zoomLevel >= 1.35"
            @click="zoomIn"
          >
            +
          </button>
          <button
            type="button"
            class="zoom-btn reset-btn"
            title="Khôi phục kích thước"
            aria-label="Khôi phục kích thước 100%"
            @click="resetZoom"
          >
            Mặc định
          </button>
        </div>

        <button
          v-if="!readonly && selectedCount > 0"
          type="button"
          class="clear-btn"
          aria-label="Bỏ chọn tất cả"
          @click="clearAllSelections"
        >
          Bỏ chọn ({{ selectedCount }})
        </button>
      </div>
    </header>

    <!-- Khu vực cuộn hiển thị sơ đồ răng -->
    <div class="chart-viewport" ref="viewportRef">
      <div
        class="chart-canvas"
        :style="{ transform: `scale(${zoomLevel})`, transformOrigin: 'top center' }"
      >
        <!-- Hàng chỉ hướng giải phẫu -->
        <div class="orientation-bar" aria-hidden="true">
          <span class="side-label side-right">◀ BÊN PHẢI BỆNH NHÂN (PHẢI)</span>
          <span class="midline-label">ĐƯỜNG GIỮA</span>
          <span class="side-label side-left">BÊN TRÁI BỆNH NHÂN (TRÁI) ▶</span>
        </div>

        <!-- HÀM TRÊN (Maxilla) -->
        <section class="arch-section upper-arch" aria-label="Hàm trên">
          <div class="arch-tag">HÀM TRÊN (MAXILLA)</div>

          <!-- Răng sữa hàm trên -->
          <div
            v-if="currentDentition === 'child' || currentDentition === 'mixed'"
            class="arch-row primary-row"
            aria-label="Cung răng sữa hàm trên"
          >
            <div class="quadrant quad-5">
              <FdiTooth
                v-for="num in childUpperRight"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
            <div class="midline-divider" aria-hidden="true"></div>
            <div class="quadrant quad-6">
              <FdiTooth
                v-for="num in childUpperLeft"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
          </div>

          <!-- Răng vĩnh viễn hàm trên -->
          <div
            v-if="currentDentition === 'adult' || currentDentition === 'mixed'"
            class="arch-row permanent-row"
            aria-label="Cung răng vĩnh viễn hàm trên"
          >
            <div class="quadrant quad-1">
              <FdiTooth
                v-for="num in adultUpperRight"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
            <div class="midline-divider" aria-hidden="true"></div>
            <div class="quadrant quad-2">
              <FdiTooth
                v-for="num in adultUpperLeft"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
          </div>
        </section>

        <!-- Mặt phẳng cắn (Occlusal Plane) -->
        <div class="occlusal-plane" aria-hidden="true">
          <span class="plane-line"></span>
          <span class="plane-text">Mặt phẳng cắn</span>
          <span class="plane-line"></span>
        </div>

        <!-- HÀM DƯỚI (Mandible) -->
        <section class="arch-section lower-arch" aria-label="Hàm dưới">
          <!-- Răng vĩnh viễn hàm dưới -->
          <div
            v-if="currentDentition === 'adult' || currentDentition === 'mixed'"
            class="arch-row permanent-row"
            aria-label="Cung răng vĩnh viễn hàm dưới"
          >
            <div class="quadrant quad-4">
              <FdiTooth
                v-for="num in adultLowerRight"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
            <div class="midline-divider" aria-hidden="true"></div>
            <div class="quadrant quad-3">
              <FdiTooth
                v-for="num in adultLowerLeft"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
          </div>

          <!-- Răng sữa hàm dưới -->
          <div
            v-if="currentDentition === 'child' || currentDentition === 'mixed'"
            class="arch-row primary-row"
            aria-label="Cung răng sữa hàm dưới"
          >
            <div class="quadrant quad-8">
              <FdiTooth
                v-for="num in childLowerRight"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
            <div class="midline-divider" aria-hidden="true"></div>
            <div class="quadrant quad-7">
              <FdiTooth
                v-for="num in childLowerLeft"
                :key="num"
                :tooth-number="num"
                :selected-surfaces="getSurfacesForTooth(num)"
                :is-whole-tooth-selected="isWholeToothSelected(num)"
                :readonly="readonly"
                @surface-click="(s) => onSurfaceClick(num, s)"
                @tooth-click="() => onToothClick(num)"
              />
            </div>
          </div>

          <div class="arch-tag">HÀM DƯỚI (MANDIBLE)</div>
        </section>

      </div>
    </div>

    <!-- Bảng chú thích mặt răng & Trạng thái đã chọn -->
    <footer class="chart-footer">
      <div class="surface-legend" aria-label="Chú thích mặt răng">
        <span class="legend-title">Mặt răng (AGENTS.md §2.4):</span>
        <span class="legend-badge"><strong>B</strong> Mặt ngoài (Buccal)</span>
        <span class="legend-badge"><strong>L</strong> Mặt trong (Lingual)</span>
        <span class="legend-badge"><strong>M</strong> Mặt gần (Mesial)</span>
        <span class="legend-badge"><strong>D</strong> Mặt xa (Distal)</span>
        <span class="legend-badge"><strong>O</strong> Mặt nhai (Occlusal)</span>
        <span class="legend-divider">|</span>
        <span class="legend-color normal"><span class="swatch white"></span> Chưa chọn</span>
        <span class="legend-color selected"><span class="swatch blue"></span> Đang chọn</span>
      </div>

      <!-- Tóm tắt đang chọn -->
      <div v-if="selectedCount > 0" class="selection-summary" role="status">
        <span class="summary-label">Đang chọn:</span>
        <span class="summary-content">{{ selectionSummaryText }}</span>
      </div>
    </footer>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import {
  type DentitionType,
  type ToothSurface,
  type SelectedToothSurface,
  ALL_SURFACES,
  SURFACE_METADATA,
  ADULT_UPPER_RIGHT,
  ADULT_UPPER_LEFT,
  ADULT_LOWER_RIGHT,
  ADULT_LOWER_LEFT,
  CHILD_UPPER_RIGHT,
  CHILD_UPPER_LEFT,
  CHILD_LOWER_RIGHT,
  CHILD_LOWER_LEFT
} from '@/types/fdi';
import FdiTooth from './FdiTooth.vue';

interface Props {
  modelValue?: SelectedToothSurface[];
  dentitionType?: DentitionType;
  readonly?: boolean;
  multiple?: boolean;
  selectedTooth?: number | null;
  selectedSurface?: ToothSurface | null;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: () => [],
  dentitionType: 'adult',
  readonly: false,
  multiple: true,
  selectedTooth: null,
  selectedSurface: null
});

const emit = defineEmits<{
  (e: 'update:modelValue', val: SelectedToothSurface[]): void;
  (e: 'update:dentitionType', val: DentitionType): void;
  (e: 'update:selectedTooth', val: number | null): void;
  (e: 'update:selectedSurface', val: ToothSurface | null): void;
  (e: 'select', payload: { toothNumber: number; surface?: ToothSurface }): void;
  (e: 'toothClick', toothNumber: number): void;
  (e: 'surfaceClick', payload: { toothNumber: number; surface: ToothSurface }): void;
}>();

// Bộ răng do Nha sĩ lựa chọn (DL-018)
const currentDentition = ref<DentitionType>(props.dentitionType);
watch(
  () => props.dentitionType,
  (newVal) => {
    currentDentition.value = newVal;
  }
);

const dentitionOptions: Array<{ value: DentitionType; label: string }> = [
  { value: 'adult', label: 'Người lớn (32 răng)' },
  { value: 'child', label: 'Trẻ em (20 răng)' },
  { value: 'mixed', label: 'Hỗn hợp' }
];

function setDentition(val: DentitionType) {
  if (props.readonly) return;
  currentDentition.value = val;
  emit('update:dentitionType', val);
}

// Danh sách răng theo cung và tiêu chuẩn FDI
const adultUpperRight = ADULT_UPPER_RIGHT;
const adultUpperLeft = ADULT_UPPER_LEFT;
const adultLowerRight = ADULT_LOWER_RIGHT;
const adultLowerLeft = ADULT_LOWER_LEFT;

const childUpperRight = CHILD_UPPER_RIGHT;
const childUpperLeft = CHILD_UPPER_LEFT;
const childLowerRight = CHILD_LOWER_RIGHT;
const childLowerLeft = CHILD_LOWER_LEFT;

// Điều khiển thu phóng (Zoom)
const zoomLevel = ref<number>(1);
function zoomIn() {
  zoomLevel.value = Math.min(1.35, +(zoomLevel.value + 0.1).toFixed(2));
}
function zoomOut() {
  zoomLevel.value = Math.max(0.75, +(zoomLevel.value - 0.1).toFixed(2));
}
function resetZoom() {
  zoomLevel.value = 1;
}

function getSurfacesForTooth(toothNumber: number): ToothSurface[] {
  const surfaces: ToothSurface[] = [];
  for (const item of props.modelValue) {
    if (item.toothNumber === toothNumber) {
      if (!item.surface) {
        return [...ALL_SURFACES];
      }
      if (!surfaces.includes(item.surface)) {
        surfaces.push(item.surface);
      }
    }
  }
  return surfaces;
}

function isWholeToothSelected(toothNumber: number): boolean {
  const hasWholeItem = props.modelValue.some(
    (item) => item.toothNumber === toothNumber && !item.surface
  );
  if (hasWholeItem) return true;
  const surfaces = getSurfacesForTooth(toothNumber);
  return surfaces.length >= ALL_SURFACES.length;
}

const selectedCount = computed(() => {
  return props.modelValue.length;
});

function onSurfaceClick(toothNumber: number, surface: ToothSurface) {
  if (props.readonly) return;

  emit('surfaceClick', { toothNumber, surface });
  emit('update:selectedTooth', toothNumber);
  emit('update:selectedSurface', surface);
  emit('select', { toothNumber, surface });

  if (!props.multiple) {
    emit('update:modelValue', [{ toothNumber, surface }]);
    return;
  }

  const isAlreadySelected = props.modelValue.some(
    (item) => item.toothNumber === toothNumber && (item.surface === surface || !item.surface)
  );

  let newSelections: SelectedToothSurface[];

  if (isAlreadySelected) {
    const wholeItemIndex = props.modelValue.findIndex(
      (item) => item.toothNumber === toothNumber && !item.surface
    );
    if (wholeItemIndex !== -1) {
      const remaining = ALL_SURFACES.filter((s) => s !== surface).map((s) => ({
        toothNumber,
        surface: s
      }));
      newSelections = [
        ...props.modelValue.filter((item) => item.toothNumber !== toothNumber),
        ...remaining
      ];
    } else {
      newSelections = props.modelValue.filter(
        (item) => !(item.toothNumber === toothNumber && item.surface === surface)
      );
    }
  } else {
    newSelections = [...props.modelValue, { toothNumber, surface }];
  }

  emit('update:modelValue', newSelections);
}

function onToothClick(toothNumber: number) {
  if (props.readonly) return;

  emit('toothClick', toothNumber);
  emit('update:selectedTooth', toothNumber);
  emit('select', { toothNumber });

  if (!props.multiple) {
    emit('update:modelValue', [{ toothNumber }]);
    return;
  }

  const alreadyWhole = isWholeToothSelected(toothNumber);
  let newSelections: SelectedToothSurface[];

  if (alreadyWhole) {
    newSelections = props.modelValue.filter((item) => item.toothNumber !== toothNumber);
  } else {
    const otherTeeth = props.modelValue.filter((item) => item.toothNumber !== toothNumber);
    newSelections = [...otherTeeth, { toothNumber }];
  }

  emit('update:modelValue', newSelections);
}

function clearAllSelections() {
  if (props.readonly) return;
  emit('update:modelValue', []);
  emit('update:selectedTooth', null);
  emit('update:selectedSurface', null);
}

const selectionSummaryText = computed(() => {
  if (!props.modelValue.length) return '';

  const toothMap = new Map<number, Set<ToothSurface | 'ALL'>>();
  for (const item of props.modelValue) {
    if (!toothMap.has(item.toothNumber)) {
      toothMap.set(item.toothNumber, new Set());
    }
    if (!item.surface) {
      toothMap.get(item.toothNumber)!.add('ALL');
    } else {
      toothMap.get(item.toothNumber)!.add(item.surface);
    }
  }

  const parts: string[] = [];
  for (const [tNum, sSet] of toothMap.entries()) {
    if (sSet.has('ALL') || sSet.size >= ALL_SURFACES.length) {
      parts.push(`Răng ${tNum} (Cả răng)`);
    } else {
      const surfaceCodes = Array.from(sSet)
        .filter((s): s is ToothSurface => s !== 'ALL')
        .map((s) => SURFACE_METADATA[s].code)
        .join(',');
      parts.push(`Răng ${tNum} (${surfaceCodes})`);
    }
  }

  return parts.join('; ');
});
</script>

<style scoped>
.fdi-chart-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 18px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  padding-bottom: 12px;
  border-bottom: 1px solid #f1f5f9;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}

.chart-title {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.dentition-selector {
  display: inline-flex;
  background: #f1f5f9;
  padding: 3px;
  border-radius: 8px;
  gap: 2px;
}

.dentition-btn {
  font-family: inherit;
  font-size: 12px;
  font-weight: 600;
  color: #475569;
  background: transparent;
  border: none;
  padding: 5px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.dentition-btn:hover:not(:disabled) {
  color: #0f172a;
}

.dentition-btn.active {
  background: #ffffff;
  color: #0284c7;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
}

.dentition-btn:disabled {
  cursor: default;
  opacity: 0.8;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.zoom-controls {
  display: inline-flex;
  align-items: center;
  background: #f8fafc;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  overflow: hidden;
}

.zoom-btn {
  font-family: inherit;
  font-size: 13px;
  font-weight: 700;
  color: #334155;
  background: transparent;
  border: none;
  padding: 4px 10px;
  cursor: pointer;
  transition: background 0.15s ease;
}

.zoom-btn:hover:not(:disabled) {
  background: #e2e8f0;
}

.zoom-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.zoom-btn.reset-btn {
  font-size: 11px;
  font-weight: 600;
  border-left: 1px solid #cbd5e1;
}

.zoom-text {
  font-size: 11px;
  font-weight: 600;
  color: #64748b;
  min-width: 42px;
  text-align: center;
  border-left: 1px solid #e2e8f0;
  border-right: 1px solid #e2e8f0;
  padding: 4px 2px;
}

.clear-btn {
  font-family: inherit;
  font-size: 12px;
  font-weight: 600;
  color: #dc2626;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 6px;
  padding: 5px 10px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.clear-btn:hover {
  background: #fee2e2;
  border-color: #fca5a5;
}

.chart-viewport {
  overflow-x: auto;
  overflow-y: hidden;
  padding: 12px 4px;
  background: #f8fafc;
  border-radius: 8px;
  border: 1px solid #f1f5f9;
  -webkit-overflow-scrolling: touch;
}

.chart-canvas {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  min-width: 780px;
  width: 100%;
  gap: 10px;
  transition: transform 0.2s ease;
}

.orientation-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  max-width: 760px;
  padding: 2px 8px;
  font-size: 10px;
  font-weight: 700;
  color: #64748b;
  letter-spacing: 0.5px;
}

.midline-label {
  color: #94a3b8;
  font-weight: 600;
}

.arch-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.arch-tag {
  font-size: 10px;
  font-weight: 800;
  color: #475569;
  letter-spacing: 0.8px;
}

.arch-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  width: 100%;
}

.arch-row.primary-row {
  opacity: 0.95;
}

.quadrant {
  display: inline-flex;
  align-items: center;
  gap: 2px;
}

.midline-divider {
  width: 2px;
  height: 48px;
  background-color: #cbd5e1;
  border-radius: 1px;
  margin: 0 6px;
}

.occlusal-plane {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  width: 100%;
  max-width: 760px;
  margin: 4px 0;
}

.plane-line {
  flex: 1;
  height: 1px;
  background: #cbd5e1;
}

.plane-text {
  font-size: 9px;
  font-weight: 700;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.6px;
}

.chart-footer {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding-top: 10px;
  border-top: 1px solid #f1f5f9;
}

.surface-legend {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
  font-size: 11px;
  color: #475569;
}

.legend-title {
  font-weight: 700;
  color: #0f172a;
}

.legend-badge {
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  border-radius: 4px;
  padding: 2px 6px;
}

.legend-badge strong {
  color: #0284c7;
  margin-right: 2px;
}

.legend-divider {
  color: #cbd5e1;
}

.legend-color {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 3px;
  border: 1px solid #94a3b8;
}

.swatch.white {
  background: #ffffff;
}

.swatch.blue {
  background: #0284c7;
  border-color: #0369a1;
}

.selection-summary {
  display: flex;
  align-items: baseline;
  gap: 8px;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 12px;
  color: #0369a1;
}

.summary-label {
  font-weight: 700;
  white-space: nowrap;
}

.summary-content {
  font-weight: 500;
  word-break: break-word;
}
</style>



