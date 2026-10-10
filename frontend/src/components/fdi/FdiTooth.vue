<template>
  <div
    class="fdi-tooth"
    :class="{
      'is-selected': isWholeToothSelected || selectedSurfaces.length > 0,
      'is-fully-selected': isWholeToothSelected,
      'is-readonly': readonly
    }"
    role="group"
    :aria-label="toothAriaLabel"
  >
    <button
      v-if="showNumber && effectiveNumberPosition === 'top'"
      type="button"
      class="tooth-number-btn"
      :class="{
        'is-active': isWholeToothSelected || selectedSurfaces.length > 0,
        'is-fully-selected': isWholeToothSelected
      }"
      :disabled="readonly"
      :tabindex="readonly ? -1 : 0"
      :aria-label="numberBtnAriaLabel"
      @click="onToothNumberClick"
    >
      {{ toothNumber }}
    </button>

    <svg
      :width="size"
      :height="size"
      viewBox="0 0 48 48"
      class="tooth-svg"
      xmlns="http://www.w3.org/2000/svg"
    >
      <polygon
        v-for="pos in surfacePositions"
        :key="pos"
        :points="polygonPoints[pos]"
        :class="[
          'tooth-surface',
          pos,
          {
            'is-selected': isSurfaceSelected(surfacesByPos[pos]),
            'is-readonly': readonly
          }
        ]"
        :role="readonly ? undefined : 'button'"
        :tabindex="readonly ? -1 : 0"
        :aria-label="getSurfaceAriaLabel(surfacesByPos[pos])"
        :aria-pressed="readonly ? undefined : isSurfaceSelected(surfacesByPos[pos])"
        @click="onSurfaceClick(surfacesByPos[pos])"
        @keydown.enter.prevent="onSurfaceClick(surfacesByPos[pos])"
        @keydown.space.prevent="onSurfaceClick(surfacesByPos[pos])"
      >
        <title>{{ getSurfaceTooltip(surfacesByPos[pos]) }}</title>
      </polygon>

      <text
        v-for="pos in surfacePositions"
        :key="'text-' + pos"
        :x="textCoords[pos].x"
        :y="textCoords[pos].y"
        class="surface-code"
        :class="{ 'is-selected': isSurfaceSelected(surfacesByPos[pos]) }"
      >
        {{ getSurfaceCode(surfacesByPos[pos]) }}
      </text>
    </svg>

    <button
      v-if="showNumber && effectiveNumberPosition === 'bottom'"
      type="button"
      class="tooth-number-btn"
      :class="{
        'is-active': isWholeToothSelected || selectedSurfaces.length > 0,
        'is-fully-selected': isWholeToothSelected
      }"
      :disabled="readonly"
      :tabindex="readonly ? -1 : 0"
      :aria-label="numberBtnAriaLabel"
      @click="onToothNumberClick"
    >
      {{ toothNumber }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import {
  type ToothSurface,
  SURFACE_METADATA,
  getToothInfo,
  getSurfaceAtPosition,
  isUpperArch
} from '@/types/fdi';

type PositionKey = 'top' | 'bottom' | 'left' | 'right' | 'center';

const surfacePositions: PositionKey[] = ['top', 'bottom', 'left', 'right', 'center'];

interface Props {
  toothNumber: number;
  selectedSurfaces?: ToothSurface[];
  isWholeToothSelected?: boolean;
  readonly?: boolean;
  size?: number;
  showNumber?: boolean;
  numberPosition?: 'top' | 'bottom' | 'auto';
}

const props = withDefaults(defineProps<Props>(), {
  selectedSurfaces: () => [],
  isWholeToothSelected: false,
  readonly: false,
  size: 44,
  showNumber: true,
  numberPosition: 'auto'
});

const emit = defineEmits<{
  (e: 'surfaceClick', surface: ToothSurface): void;
  (e: 'toothClick', toothNumber: number): void;
}>();

const toothInfo = computed(() => getToothInfo(props.toothNumber));

const effectiveNumberPosition = computed(() => {
  if (props.numberPosition !== 'auto') return props.numberPosition;
  return isUpperArch(props.toothNumber) ? 'top' : 'bottom';
});

const surfacesByPos = computed<Record<PositionKey, ToothSurface>>(() => ({
  top: getSurfaceAtPosition(props.toothNumber, 'top'),
  bottom: getSurfaceAtPosition(props.toothNumber, 'bottom'),
  left: getSurfaceAtPosition(props.toothNumber, 'left'),
  right: getSurfaceAtPosition(props.toothNumber, 'right'),
  center: getSurfaceAtPosition(props.toothNumber, 'center')
}));

const polygonPoints: Record<PositionKey, string> = {
  top: '2,2 46,2 32,16 16,16',
  bottom: '16,32 32,32 46,46 2,46',
  left: '2,2 16,16 16,32 2,46',
  right: '46,2 46,46 32,32 32,16',
  center: '16,16 32,16 32,32 16,32'
};

const textCoords: Record<PositionKey, { x: number; y: number }> = {
  top: { x: 24, y: 9.5 },
  bottom: { x: 24, y: 39 },
  left: { x: 9.5, y: 24 },
  right: { x: 38.5, y: 24 },
  center: { x: 24, y: 24 }
};

function isSurfaceSelected(surface: ToothSurface): boolean {
  return props.isWholeToothSelected || props.selectedSurfaces.includes(surface);
}

function getSurfaceCode(surface: ToothSurface): string {
  return SURFACE_METADATA[surface].code;
}

function getSurfaceTooltip(surface: ToothSurface): string {
  const meta = SURFACE_METADATA[surface];
  return `Răng ${props.toothNumber} (${toothInfo.value.nameVi}) - ${meta.descriptionVi}`;
}

function getSurfaceAriaLabel(surface: ToothSurface): string {
  const meta = SURFACE_METADATA[surface];
  const selectedText = isSurfaceSelected(surface) ? 'Đã chọn' : 'Chưa chọn';
  return `Răng ${props.toothNumber}, ${meta.descriptionVi}. ${selectedText}`;
}

const toothAriaLabel = computed(() => {
  return `Răng FDI ${props.toothNumber}, ${toothInfo.value.nameVi}`;
});

const numberBtnAriaLabel = computed(() => {
  const state = props.isWholeToothSelected ? 'Đang chọn toàn bộ' : 'Chưa chọn toàn bộ';
  return `Răng ${props.toothNumber}: ${toothInfo.value.nameVi}. ${state}. Nhấn để chọn toàn bộ răng.`;
});

function onSurfaceClick(surface: ToothSurface) {
  if (props.readonly) return;
  emit('surfaceClick', surface);
}

function onToothNumberClick() {
  if (props.readonly) return;
  emit('toothClick', props.toothNumber);
}
</script>

<style scoped>
.fdi-tooth {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  user-select: none;
  padding: 2px;
  border-radius: 6px;
  transition: background-color 0.15s ease;
}

.tooth-number-btn {
  font-family: inherit;
  font-size: 11px;
  font-weight: 700;
  color: #334155;
  background: #f1f5f9;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  min-width: 28px;
  height: 20px;
  line-height: 18px;
  padding: 0 4px;
  cursor: pointer;
  transition: all 0.15s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.tooth-number-btn:hover:not(:disabled) {
  background: #e2e8f0;
  border-color: #94a3b8;
  color: #0f172a;
}

.tooth-number-btn.is-active {
  background: #e0f2fe;
  border-color: #38bdf8;
  color: #0369a1;
}

.tooth-number-btn.is-fully-selected {
  background: #0284c7;
  border-color: #0369a1;
  color: #ffffff;
}

.tooth-number-btn:disabled {
  cursor: default;
  opacity: 0.85;
}

.tooth-number-btn:focus-visible {
  outline: 2px solid #2563eb;
  outline-offset: 1px;
}

.tooth-svg {
  display: block;
  overflow: visible;
}

.tooth-surface {
  fill: #ffffff;
  stroke: #94a3b8;
  stroke-width: 1.2;
  cursor: pointer;
  transition: fill 0.15s ease, stroke 0.15s ease;
}

.tooth-surface:hover:not(.is-readonly) {
  fill: #bae6fd;
  stroke: #0284c7;
}

.tooth-surface.is-selected {
  fill: #0284c7;
  stroke: #0369a1;
}

.tooth-surface.is-selected:hover:not(.is-readonly) {
  fill: #0369a1;
}

.tooth-surface.is-readonly {
  cursor: default;
}

.tooth-surface:focus-visible {
  outline: 2px solid #2563eb;
  outline-offset: -1px;
}

.surface-code {
  font-size: 8px;
  font-weight: 700;
  fill: #64748b;
  text-anchor: middle;
  dominant-baseline: central;
  pointer-events: none;
  user-select: none;
  transition: fill 0.15s ease;
}

.surface-code.is-selected {
  fill: #ffffff;
}
</style>
