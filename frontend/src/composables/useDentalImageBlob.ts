import { onUnmounted, ref } from 'vue';
import { dentalImagesService } from '@/services/dentalImages';

export function useDentalImageBlob(getId: () => number) {
  const src = ref(''), loading = ref(false), error = ref('');
  let generation = 0, controller: AbortController | null = null;
  function release() {
    generation++; controller?.abort(); controller = null;
    if (src.value) URL.revokeObjectURL(src.value);
    src.value = ''; loading.value = false; error.value = '';
  }
  async function load() {
    release(); const current = generation, id = getId(); controller = new AbortController(); loading.value = true;
    try {
      const blob = await dentalImagesService.file(id, controller.signal);
      if (current !== generation || id !== getId()) return;
      if (!(blob instanceof Blob) || !['image/png', 'image/jpeg', 'image/webp'].includes(blob.type) || blob.size < 12 || blob.size > 10 * 1024 * 1024) throw new Error('Invalid image response');
      src.value = URL.createObjectURL(blob);
    } catch {
      if (current === generation) error.value = 'Không tải được ảnh. Kiểm tra quyền truy cập hoặc thử lại.';
    } finally { if (current === generation) loading.value = false; }
  }
  onUnmounted(release);
  return { src, loading, error, load, release };
}
