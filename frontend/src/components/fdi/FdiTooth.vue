<template>
  <div
    class="fdi-tooth"
    :class="[
      {
        'is-selected': isWholeToothSelected || selectedSurfaces.length > 0,
        'is-fully-selected': isWholeToothSelected,
        'is-readonly': readonly,
        'is-missing': missingTooth
      },
      toothStateClass
    ]"
    role="group"
    :aria-label="toothAriaLabel"
  >
    <!-- Hàm trên: Số răng -> Hình giải phẫu -> 5 mặt răng -->
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

    <button
      v-if="effectiveNumberPosition === 'top'"
      type="button"
      class="tooth-anat-btn"
      :disabled="readonly"
      :tabindex="-1"
      :title="`Chọn cả răng #${toothNumber} (${toothInfo.nameVi})`"
      @click="onToothNumberClick"
    >
      <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
        <path :d="anatPaths.root" class="svg-root" />
        <path d="M6,29 Q20,32 34,29" class="svg-gum" />
        <path :d="anatPaths.crown" class="svg-crown" />
        <path :d="anatPaths.groove" class="svg-groove" />
        <circle v-if="dominantColor === 'caries'" cx="20" cy="43" r="4.5" class="svg-caries" />
        <ellipse v-if="dominantColor === 'restored'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
        <path v-if="dominantColor === 'treatment'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
        <g v-if="missingTooth" class="missing-anat-mark">
          <line x1="9" y1="30" x2="31" y2="52" />
          <line x1="31" y1="30" x2="9" y2="52" />
        </g>
      </svg>
    </button>

    <div class="tooth-surface-target-wrap">
      <svg
        :width="surfaceSvgSize"
        :height="surfaceSvgSize"
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
              'has-condition': toothConditionColor(conditionCodeFor(surfacesByPos[pos])) !== undefined,
              [`condition-${toothConditionColor(conditionCodeFor(surfacesByPos[pos]))}`]: toothConditionColor(conditionCodeFor(surfacesByPos[pos])) !== undefined,
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

        <g v-if="missingTooth" class="missing-tooth-mark" aria-hidden="true">
          <line x1="7" y1="7" x2="41" y2="41" />
          <line x1="41" y1="7" x2="7" y2="41" />
        </g>

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
    </div>

    <!-- Hàm dưới: 5 mặt răng -> Hình giải phẫu -> Số răng -->
    <button
      v-if="effectiveNumberPosition === 'bottom'"
      type="button"
      class="tooth-anat-btn"
      :disabled="readonly"
      :tabindex="-1"
      :title="`Chọn cả răng #${toothNumber} (${toothInfo.nameVi})`"
      @click="onToothNumberClick"
    >
      <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
        <g transform="translate(0,60) scale(1,-1)">
          <path :d="anatPaths.root" class="svg-root" />
          <path d="M6,29 Q20,32 34,29" class="svg-gum" />
          <path :d="anatPaths.crown" class="svg-crown" />
          <path :d="anatPaths.groove" class="svg-groove" />
          <circle v-if="dominantColor === 'caries'" cx="20" cy="43" r="4.5" class="svg-caries" />
          <ellipse v-if="dominantColor === 'restored'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
          <path v-if="dominantColor === 'treatment'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
          <g v-if="missingTooth" class="missing-anat-mark">
            <line x1="9" y1="30" x2="31" y2="52" />
            <line x1="31" y1="30" x2="9" y2="52" />
          </g>
        </g>
      </svg>
    </button>

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
  isUpperArch,
  toothConditionColor
} from '@/types/fdi';

type PositionKey = 'top' | 'bottom' | 'left' | 'right' | 'center';

const surfacePositions: PositionKey[] = ['top', 'bottom', 'left', 'right', 'center'];

interface Props {
  toothNumber: number;
  selectedSurfaces?: ToothSurface[];
  conditionBySurface?: Partial<Record<ToothSurface, string>>;
  missingTooth?: boolean;
  isWholeToothSelected?: boolean;
  readonly?: boolean;
  size?: number;
  showNumber?: boolean;
  numberPosition?: 'top' | 'bottom' | 'auto';
}

const props = withDefaults(defineProps<Props>(), {
  selectedSurfaces: () => [],
  conditionBySurface: () => ({}),
  missingTooth: false,
  isWholeToothSelected: false,
  readonly: false,
  size: 46,
  showNumber: true,
  numberPosition: 'auto'
});

const emit = defineEmits<{
  (e: 'surfaceClick', surface: ToothSurface): void;
  (e: 'toothClick', toothNumber: number): void;
}>();

const toothInfo = computed(() => getToothInfo(props.toothNumber));

function conditionCodeFor(surface: ToothSurface) { return props.conditionBySurface[surface]; }

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

const surfaceSvgSize = computed(() => Math.min(props.size, 32));

const anatPaths = computed(() => {
  const num = props.toothNumber;
  const pos = num % 10;
  const isChild = num >= 51;
  const isMolar = isChild ? pos >= 4 : pos >= 6;
  const isPremolar = !isChild && (pos === 4 || pos === 5);
  const isCanine = pos === 3;
  const isUpper = isUpperArch(num);

  if (isMolar) {
    return {
      root: isUpper
        ? 'M8,30 C6,16 8,5 12,4 C14,4 15,15 16,21 C18,12 19,3 20,3 C21,3 22,12 24,21 C25,15 26,4 28,4 C32,5 34,16 32,30 Z'
        : 'M8,30 C6,16 9,4 14,4 C17,5 18,15 20,21 C22,15 23,5 26,4 C31,4 34,16 32,30 Z',
      crown: 'M5,29 C3,35 3,47 7,53 C9,56 13,56 16,53 C18,56 22,56 24,53 C27,56 31,56 33,53 C37,47 37,35 35,29 C28,27 12,27 5,29 Z',
      groove: 'M10,43 Q20,46 30,43 M16,36 L16,51 M24,36 L24,51'
    };
  }
  if (isPremolar) {
    return {
      root: 'M12,30 C11,17 13,5 16,4 C18,4 19,12 20,19 C21,12 22,4 24,4 C27,5 29,17 28,30 Z',
      crown: 'M8,29 C5,34 5,47 9,53 C12,56 17,55 20,52 C23,55 28,56 31,53 C35,47 35,34 32,29 C26,27 14,27 8,29 Z',
      groove: 'M12,43 Q20,46 28,43 M20,37 L20,50'
    };
  }
  if (isCanine) {
    return {
      root: 'M14,30 C13,12 16,2 20,2 C24,2 27,12 26,30 Z',
      crown: 'M9,29 C7,34 8,45 11,51 L20,58 L29,51 C32,45 33,34 31,29 C26,27 14,27 9,29 Z',
      groove: 'M20,37 L20,53'
    };
  }
  return {
    root: 'M14,30 C13,14 16,3 20,3 C24,3 27,14 26,30 Z',
    crown: 'M9,29 C7,34 7,46 9,53 C13,55 27,55 31,53 C33,46 33,34 31,29 C26,27 14,27 9,29 Z',
    groove: 'M13,50 H27'
  };
});

const dominantColor = computed(() => {
  if (props.missingTooth) return 'missing';
  const colors = Object.values(props.conditionBySurface)
    .map((code) => toothConditionColor(code))
    .filter((c): c is NonNullable<typeof c> => Boolean(c) && c !== 'healthy');
  if (colors.includes('caries')) return 'caries';
  if (colors.includes('treatment')) return 'treatment';
  if (colors.includes('prosthetic')) return 'prosthetic';
  if (colors.includes('restored')) return 'restored';
  return undefined;
});

const toothStateClass = computed(() => {
  if (!dominantColor.value) return '';
  return `st-${dominantColor.value}`;
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
  width: 42px;
  padding: 4px 2px;
  border: 1.5px solid #dce7f3;
  border-radius: 10px;
  background: #ffffff;
  transition: transform 0.14s ease, border-color 0.14s ease, background-color 0.14s ease, box-shadow 0.14s ease;
}

.fdi-tooth:hover:not(.is-readonly) {
  border-color: #397bcc;
  background: #f4faff;
  transform: translateY(-1px);
}

.fdi-tooth.is-selected {
  border-color: #1857b6;
  background: #e8f4fd;
  box-shadow: 0 0 0 2px rgba(24, 87, 182, 0.16);
}

.fdi-tooth.is-missing {
  background: #f8fafc;
  opacity: 0.78;
}

.tooth-anat-btn {
  background: transparent;
  border: none;
  padding: 0;
  margin: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.tooth-anat-btn:disabled {
  cursor: default;
}

.t-anat-svg {
  width: 30px;
  height: 44px;
  display: block;
}

.svg-root { fill: #f6e7cb; stroke: #948368; stroke-width: 1.3; stroke-linejoin: round; }
.svg-gum { fill: none; stroke: #f4a5ae; stroke-width: 2.2; stroke-linecap: round; opacity: 0.75; }
.svg-crown { fill: #ffffff; stroke: #64748b; stroke-width: 1.4; stroke-linejoin: round; }
.svg-groove { fill: none; stroke: #cbd5e1; stroke-width: 1.1; stroke-linecap: round; }
.svg-caries { fill: #ef4444; stroke: #991b1b; stroke-width: 1; }
.svg-filling { fill: #3b82f6; stroke: #1d4ed8; stroke-width: 1; }
.svg-rct { fill: none; stroke: #8b5cf6; stroke-width: 2; stroke-linecap: round; }
.missing-anat-mark { stroke: #475569; stroke-width: 2.4; stroke-linecap: round; }

.st-prosthetic .svg-crown { fill: #ede9fe; stroke: #7c3aed; }
.st-treatment .svg-crown { fill: #fef3c7; stroke: #d97706; }

.tooth-surface-target-wrap {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 32px;
  min-height: 32px;
}

.tooth-number-btn {
  font-family: inherit;
  font-size: 11px;
  font-weight: 800;
  color: #102e56;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  min-width: 30px;
  min-height: 20px;
  line-height: 18px;
  padding: 0 3px;
  cursor: pointer;
  transition: all 0.15s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.tooth-number-btn:hover:not(:disabled) {
  background: #d8ecfa;
  border-color: #397bcc;
  color: #102e56;
}

.tooth-number-btn.is-active {
  background: #d8ecfa;
  border-color: #1857b6;
  color: #102e56;
}

.tooth-number-btn.is-fully-selected {
  background: #1857b6;
  border-color: #102e56;
  color: #ffffff;
}

.tooth-number-btn:disabled {
  cursor: default;
  opacity: 0.85;
}

.tooth-number-btn:focus-visible {
  outline: 2px solid #1857b6;
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

.tooth-surface.condition-healthy { --condition-fill: #ffffff; fill: var(--condition-fill); stroke: #cbd5e1; }
.tooth-surface.condition-caries { --condition-fill: #ef4444; fill: var(--condition-fill); }
.tooth-surface.condition-restored { --condition-fill: #1857b6; fill: var(--condition-fill); }
.tooth-surface.condition-missing { --condition-fill: #64748b; fill: var(--condition-fill); }
.tooth-surface.condition-treatment { --condition-fill: #e5b95e; fill: var(--condition-fill); }
.tooth-surface.condition-prosthetic { --condition-fill: #8b5cf6; fill: var(--condition-fill); }

.tooth-surface:hover:not(.is-readonly) {
  fill: #d8ecfa;
  stroke: #1857b6;
}

.tooth-surface.is-selected:not(.has-condition) {
  fill: #1857b6;
  stroke: #102e56;
}

.tooth-surface.is-selected:hover:not(.is-readonly):not(.has-condition) {
  fill: #102e56;
}

.tooth-surface.has-condition.is-selected { stroke: #102e56; stroke-width: 2.5; }
.tooth-surface.has-condition.is-selected:hover:not(.is-readonly) { fill: var(--condition-fill); }
.missing-tooth-mark { stroke: #424242; stroke-width: 3; stroke-linecap: round; pointer-events: none; }

.tooth-surface.is-readonly {
  cursor: default;
}

.tooth-surface:focus-visible {
  outline: 2px solid #1857b6;
  outline-offset: -1px;
}

.surface-code {
  font-family: inherit;
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

.fdi-tooth.is-fully-selected {
  background-color: rgba(24, 87, 182, 0.08);
}
</style>
