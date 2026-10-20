<template>
  <section class="image-pane" :aria-label="label">
    <h3>{{ label }} — {{ image.originalFileName }}</h3>
    <div class="toolbar" role="group" :aria-label="`Điều khiển ${label}`">
      <button type="button" :disabled="zoom <= 50" @click="changeZoom(-25)">Thu nhỏ</button>
      <label :for="`zoom-${image.id}`">Thu phóng {{ zoom }}%</label>
      <input :id="`zoom-${image.id}`" v-model.number="zoom" type="range" min="50" max="500" step="25" />
      <button type="button" :disabled="zoom >= 500" @click="changeZoom(25)">Phóng to</button>
      <button type="button" @click="angle = (angle + 90) % 360">Xoay 90°</button>
      <button type="button" @click="reset">Đặt lại</button>
    </div>
    <div class="pan-controls" role="group" :aria-label="`Di chuyển ${label}`">
      <button type="button" @click="pan(-30, 0)">Trái</button><button type="button" @click="pan(30, 0)">Phải</button>
      <button type="button" @click="pan(0, -30)">Lên</button><button type="button" @click="pan(0, 30)">Xuống</button>
    </div>
    <div class="viewport" role="group" tabindex="0" :aria-label="`Vùng ${label}; dùng phím mũi tên để di chuyển, cộng trừ để thu phóng`" :style="{ touchAction: zoom > 100 ? 'none' : 'pan-y' }"
      @wheel="wheel" @keydown="key" @pointerdown="startDrag" @pointermove="drag" @pointerup="stopDrag" @pointercancel="stopDrag" @lostpointercapture="stopDrag">
      <img v-if="src" :src="src" :alt="image.originalFileName" draggable="false" :style="{ transform: `translate(${x}px, ${y}px) rotate(${angle}deg) scale(${zoom / 100})` }" @error="imageFailed" />
      <p v-else role="status">{{ loading ? 'Đang tải ảnh...' : error || 'Chưa tải ảnh.' }}</p>
    </div>
    <button v-if="error" type="button" @click="load">Thử tải lại</button>
    <dl>
      <dt>Phân loại</dt><dd>{{ image.category }}</dd><dt>Răng FDI</dt><dd>{{ image.toothNumber ?? 'Không gắn răng' }}</dd>
      <dt>Ngày tải</dt><dd>{{ new Date(image.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) }}</dd>
      <dt>Người tải</dt><dd>{{ image.uploadedByFullName || 'Nhân viên' }}</dd>
      <dt>Ghi chú</dt><dd class="notes">{{ image.notes || 'Chưa có ghi chú ảnh.' }}</dd>
    </dl>
  </section>
</template>
<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import type { DentalImage } from '@/services/dentalImages';
import { useDentalImageBlob } from '@/composables/useDentalImageBlob';
const props = defineProps<{ image: DentalImage; label: string }>();
const { src, loading, error, load, release } = useDentalImageBlob(() => props.image.id);
const zoom = ref(100), angle = ref(0), x = ref(0), y = ref(0);
let dragging: { id: number; x: number; y: number } | null = null;
function changeZoom(delta: number) { zoom.value = Math.max(50, Math.min(500, zoom.value + delta)); }
function pan(dx: number, dy: number) { x.value = Math.max(-10000, Math.min(10000, x.value + dx)); y.value = Math.max(-10000, Math.min(10000, y.value + dy)); }
function reset() { zoom.value = 100; angle.value = 0; x.value = y.value = 0; dragging = null; }
function wheel(event: WheelEvent) { if (event.ctrlKey || event.metaKey || !event.deltaY) return; event.preventDefault(); changeZoom(event.deltaY < 0 ? 25 : -25); }
function key(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const moves: Record<string, [number, number]> = { ArrowLeft: [-30, 0], ArrowRight: [30, 0], ArrowUp: [0, -30], ArrowDown: [0, 30] };
  if (moves[event.key]) { event.preventDefault(); pan(...moves[event.key]!); }
  else if (['+', '=', '-'].includes(event.key)) { event.preventDefault(); changeZoom(event.key === '-' ? -25 : 25); }
}
function startDrag(event: PointerEvent) {
  if (event.button !== 0 || event.isPrimary === false || zoom.value <= 100) return;
  event.preventDefault(); const host = event.currentTarget as HTMLElement; host.focus(); host.setPointerCapture(event.pointerId);
  dragging = { id: event.pointerId, x: event.clientX, y: event.clientY };
}
function drag(event: PointerEvent) { if (dragging?.id !== event.pointerId) return; pan(event.clientX - dragging.x, event.clientY - dragging.y); dragging.x = event.clientX; dragging.y = event.clientY; }
function stopDrag() { dragging = null; }
function imageFailed(event?: Event) {
  const image = event?.target as HTMLImageElement | null;
  if (!src.value || image?.currentSrc && image.currentSrc !== src.value) return;
  release(); error.value = 'Không đọc được nội dung ảnh. Có thể tệp ảnh đã hỏng.';
}
onMounted(load); watch(() => props.image.id, () => { reset(); void load(); });
</script>
<style scoped>
.image-pane { min-width: 0; overflow-wrap: anywhere; }
.toolbar, .pan-controls { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; margin-bottom: .5rem; }
button { font: inherit; padding: .5rem; }
.viewport { height: clamp(16rem, 48vh, 36rem); overflow: hidden; display: flex; align-items: center; justify-content: center; background: #0f172a; color: #f8fafc; }
.viewport img { max-width: 100%; max-height: 100%; object-fit: contain; user-select: none; }
dl { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: .4rem 1rem; }
dd { margin: 0; }.notes { white-space: pre-wrap; }
</style>
