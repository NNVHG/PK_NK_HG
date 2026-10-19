<template>
  <dialog ref="dialog" aria-labelledby="image-viewer-title" @close="emit('close')">
    <header><h2 id="image-viewer-title" ref="heading" tabindex="-1">{{ images.length === 2 ? 'So sánh Trước / Sau' : 'Xem ảnh hồ sơ' }}</h2><button type="button" @click="dialog?.close()">Đóng ảnh</button></header>
    <p v-if="images.length === 2">Ảnh chọn thứ nhất ở bên Trước; ảnh chọn thứ hai ở bên Sau. Mỗi ảnh có điều khiển riêng.</p>
    <div class="image-panes" :class="{ compare: images.length === 2 }">
      <DentalImageCanvas v-for="(image, index) in images.slice(0, 2)" :key="image.id" :image="image" :label="images.length === 2 ? index === 0 ? 'Trước' : 'Sau' : 'Ảnh'" />
    </div>
  </dialog>
</template>
<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue';
import type { DentalImage } from '@/services/dentalImages';
import DentalImageCanvas from './DentalImageCanvas.vue';
defineProps<{ images: DentalImage[] }>();
const emit = defineEmits<{ close: [] }>();
const dialog = ref<HTMLDialogElement | null>(null), heading = ref<HTMLElement | null>(null);
let trigger: HTMLElement | null = null;
onMounted(async () => { trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null; dialog.value?.showModal(); await nextTick(); heading.value?.focus(); });
onUnmounted(() => { if (trigger?.isConnected) trigger.focus(); });
</script>
<style scoped>
dialog { width: min(96vw, 90rem); max-height: 94vh; overflow: auto; border: 1px solid #cbd5e1; border-radius: .75rem; padding: 1rem; }
dialog::backdrop { background: rgb(15 23 42 / .7); }
header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }h2 { margin-top: 0; }
header button { font: inherit; padding: .6rem; }
.image-panes { display: grid; gap: 1rem; }.compare { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }
@media (max-width: 760px) { .compare { grid-template-columns: minmax(0, 1fr); } }
</style>
