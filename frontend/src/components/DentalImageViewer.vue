<template>
  <dialog ref="dialog" aria-labelledby="image-viewer-title" @close="emit('close')">
    <header><h2 id="image-viewer-title" ref="heading" tabindex="-1">{{ images.length === 2 ? 'So sánh Trước / Sau' : 'Xem ảnh hồ sơ' }}</h2><button type="button" @click="dialog?.close()">Đóng ảnh</button></header>
    <p v-if="images.length === 2">Ảnh chọn thứ nhất ở bên Trước; ảnh chọn thứ hai ở bên Sau. Mỗi ảnh có điều khiển riêng.</p>
    <div class="image-panes" :class="{ compare: images.length === 2 }">
      <section v-for="(image, index) in images.slice(0, 2)" :key="image.id" class="image-detail">
        <DentalImageCanvas :image="image" :label="images.length === 2 ? index === 0 ? 'Trước' : 'Sau' : 'Ảnh'" />
        <div class="image-records">
          <DentalImageMetadataEditor :image-id="image.id" :patient-id="image.patientId" :label="images.length === 2 ? index === 0 ? 'Trước' : 'Sau' : 'Ảnh'" @updated="metadataUpdated" />
          <DentalImageClinicalNotes :image-id="image.id" :patient-id="image.patientId" :label="images.length === 2 ? index === 0 ? 'Trước' : 'Sau' : 'Ảnh'" @changed="emit('changed')" />
        </div>
      </section>
    </div>
  </dialog>
</template>
<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue';
import type { DentalImage } from '@/services/dentalImages';
import DentalImageCanvas from './DentalImageCanvas.vue';
import DentalImageClinicalNotes from './DentalImageClinicalNotes.vue';
import DentalImageMetadataEditor from './DentalImageMetadataEditor.vue';
import type { ImageMetadataContext } from '@/services/imageMetadata';
defineProps<{ images: DentalImage[] }>();
const emit = defineEmits<{ close: []; changed: []; metadataUpdated:[value:ImageMetadataContext] }>();
function metadataUpdated(value:ImageMetadataContext) { emit('metadataUpdated',value); emit('changed'); }
const dialog = ref<HTMLDialogElement | null>(null), heading = ref<HTMLElement | null>(null);
let trigger: HTMLElement | null = null;
onMounted(async () => { trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null; dialog.value?.showModal(); await nextTick(); heading.value?.focus(); });
onUnmounted(() => { if (trigger?.isConnected) trigger.focus(); });
</script>
<style scoped>
dialog {
  width: min(96vw, 90rem);
  max-height: 94vh;
  overflow: auto;
  border: 1px solid #cbd5e1;
  border-radius: 12px;
  padding: 1.5rem;
  background: #ffffff;
  box-shadow: 0 10px 25px rgba(15, 45, 89, 0.2);
  color: #1e293b;
  font-family: inherit;
}

dialog::backdrop {
  background: rgba(15, 45, 89, 0.6);
  backdrop-filter: blur(2px);
}

header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #e2e8f0;
  margin-bottom: 1rem;
}

h2 {
  margin: 0;
  color: #0f2d59;
  font-size: 1.4rem;
  font-weight: 700;
}

header button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.5rem 1rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #475569;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
}

header button:hover {
  background: #f1f5f9;
  color: #0f2d59;
}

header button:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}

.image-panes {
  display: grid;
  gap: 1.25rem;
}

.compare {
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
}

.image-detail {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(16rem, 1fr);
  gap: 1.25rem;
  min-width: 0;
}

.compare .image-detail {
  grid-template-columns: minmax(0, 1fr);
}

.image-records {
  display: grid;
  gap: 1rem;
  align-content: start;
  min-width: 0;
}

@media (max-width: 1000px) {
  .image-detail {
    grid-template-columns: minmax(0, 1fr);
  }
}

@media (max-width: 760px) {
  dialog {
    padding: 1rem 0.75rem;
  }
  .compare {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
