<template>
  <div ref="host" class="image-thumbnail">
    <button type="button" :aria-label="`Xem ảnh ${image.originalFileName}`" @click="emit('open', image)">
      <img v-if="src" :src="src" :alt="image.originalFileName" loading="lazy" draggable="false" @error="imageFailed" />
      <span v-else>{{ loading ? 'Đang tải ảnh...' : error ? 'Mở để thử tải lại ảnh' : 'Xem ảnh' }}</span>
    </button>
    <p v-if="error" role="status">{{ error }}</p>
  </div>
</template>
<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import type { DentalImage } from '@/services/dentalImages';
import { useDentalImageBlob } from '@/composables/useDentalImageBlob';
const props = defineProps<{ image: DentalImage }>();
const emit = defineEmits<{ open: [image: DentalImage] }>();
const host = ref<HTMLElement | null>(null);
const { src, loading, error, load, release } = useDentalImageBlob(() => props.image.id);
let observer: IntersectionObserver | null = null, visible = false, active = true;
function imageFailed(event?: Event) {
  const image = event?.target as HTMLImageElement | null;
  if (!src.value || image?.currentSrc && image.currentSrc !== src.value) return;
  release(); error.value = 'Không đọc được nội dung ảnh.';
}
onMounted(() => {
  if (typeof IntersectionObserver === 'undefined') { visible = true; void load(); return; }
  observer = new IntersectionObserver(entries => {
    if (!active) return;
    visible = entries.some(entry => entry.isIntersecting);
    if (visible && !src.value && !loading.value && !error.value) void load();
    if (!visible) release();
  }, { rootMargin: '50px' });
  if (host.value) observer.observe(host.value);
});
watch(() => props.image.id, () => { release(); if (visible) void load(); });
onUnmounted(() => { active = false; observer?.disconnect(); });
</script>
<style scoped>
.image-thumbnail button { width: 160px; height: 160px; max-width: 100%; padding: 0; border: 1px solid #cbd5e1; border-radius: .5rem; overflow: hidden; font: inherit; }
img { width: 100%; height: 100%; object-fit: cover; }
p { font-size: .875rem; }
</style>
