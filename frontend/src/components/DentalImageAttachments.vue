<template>
  <section class="image-attachments" aria-labelledby="image-heading">
    <h2 id="image-heading">Ảnh hồ sơ</h2>
    <p>Ảnh nhận muộn có thể lưu theo bệnh nhân. Chỉ gắn vào ca đang mở hoặc đính chính hợp lệ, chưa thu tiền.</p>
    <form v-if="canUpload" @submit.prevent="upload">
      <fieldset :disabled="busy">
        <legend>Tải ảnh và gắn thông tin</legend>
        <label for="image-file">Tệp ảnh</label>
        <input id="image-file" :key="fileKey" ref="fileInput" type="file" accept="image/jpeg,image/png,image/webp" :aria-invalid="validationTarget === 'file'" :aria-describedby="validationTarget === 'file' ? 'image-rules image-error' : 'image-rules'" @change="selectFile" />
        <small id="image-rules">JPG, PNG, WebP; tối đa 10MB/tệp, 4096px mỗi chiều. Mỗi ca tối đa 20 ảnh, 3 ảnh/răng và 100MB.</small>
        <label for="image-category">Phân loại</label>
        <select id="image-category" v-model="category"><option v-for="(label, key) in categories" :key="key" :value="key">{{ label }}</option></select>
        <label for="image-notes">Ghi chú ảnh (không bắt buộc)</label>
        <textarea id="image-notes" ref="notesInput" v-model="notes" maxlength="500" rows="2" :aria-invalid="validationTarget === 'notes'" :aria-describedby="validationTarget === 'notes' ? 'image-error' : undefined" />
        <label for="image-tooth">Răng FDI (không bắt buộc)</label>
        <input id="image-tooth" ref="toothInput" v-model="toothText" inputmode="numeric" maxlength="2" :aria-invalid="validationTarget === 'tooth'" :aria-describedby="validationTarget === 'tooth' ? 'image-error' : undefined" placeholder="Ví dụ: 46" />
        <label for="image-visit">Lần khám</label>
        <select id="image-visit" v-model="visitId"><option :value="null">Lưu riêng theo bệnh nhân</option><option v-for="visit in targets" :key="visit.visitId" :value="visit.visitId">#{{ visit.visitId }} · {{ visit.status === 'Correcting' ? 'Đính chính' : visit.status === 'Created' ? 'Chưa bắt đầu' : 'Đang khám' }}</option></select>
        <p v-if="targetError" role="alert">{{ targetError }}</p>
        <button type="button" :disabled="loadingTargets" @click="loadTargets">{{ loadingTargets ? 'Đang tải ca...' : 'Tải lại ca có thể gắn' }}</button>
        <button type="submit">{{ busy ? 'Đang lưu...' : 'Tải ảnh lên' }}</button>
      </fieldset>
    </form>
    <p v-if="error" id="image-error" role="alert" class="error">{{ error }}</p>
    <p role="status" aria-live="polite">{{ success }}</p>
    <template v-if="canRead">
      <button type="button" :disabled="store.loading || busy" @click="refresh">{{ store.loading ? 'Đang tải...' : 'Tải lại danh sách' }}</button>
      <p v-if="listError" role="alert" class="error">{{ listError }}</p>
      <p v-if="!store.loading && !listError && !store.items.length">Chưa có ảnh được lưu.</p>
      <p id="comparison-help">Chọn tối đa 2 ảnh để so sánh; thứ tự chọn tương ứng Trước và Sau.</p>
      <button type="button" :disabled="comparedImages.length !== 2" @click="showComparison">So sánh 2 ảnh đã chọn</button>
      <ul class="image-list">
        <li v-for="image in store.items" :key="image.id">
          <DentalImageThumbnail :image="image" @open="showImage" />
          <label :for="`compare-image-${image.id}`"><input :id="`compare-image-${image.id}`" type="checkbox" :checked="comparisonIds.includes(image.id)" :disabled="!comparisonIds.includes(image.id) && comparisonIds.length >= 2" aria-describedby="comparison-help" @change="toggleComparison(image)" /> So sánh {{ image.originalFileName }}</label>
          <strong>{{ image.originalFileName }}</strong>
          <p>{{ categories[image.category] ?? image.category }} · {{ image.width }} × {{ image.height }}px · {{ Math.ceil(image.sizeBytes / 1024) }}KB</p>
          <p>{{ image.visitId ? `Lần khám #${image.visitId}` : 'Ảnh lưu riêng theo bệnh nhân' }} · {{ image.toothNumber ? `Răng ${image.toothNumber}` : 'Không gắn răng' }}</p>
          <p v-if="image.notes" class="image-notes">{{ image.notes }}</p>
          <template v-if="canManage && image.visitId === null">
            <label :for="`attach-visit-${image.id}`">Gắn ảnh vào lần khám</label>
            <select :id="`attach-visit-${image.id}`" v-model="attachTargets[image.id]" :disabled="busy"><option :value="undefined">Chọn ca đang mở</option><option v-for="target in targets" :key="target.visitId" :value="target.visitId">#{{ target.visitId }}</option></select>
            <button type="button" :disabled="busy || !attachTargets[image.id]" @click="attach(image)">Gắn vào ca</button>
          </template>
        </li>
      </ul>
      <button v-if="store.hasMore" type="button" :disabled="store.loading || busy" @click="loadMore">Xem thêm ảnh</button>
      <DentalImageViewer v-if="viewerImages.length" :images="viewerImages" @close="viewerImages = []" @changed="refresh" @metadata-updated="metadataUpdated" />
    </template>
    <p v-else-if="canUpload">Phụ tá được tải lên theo quyền hiện hành. Danh sách ảnh đã lưu dành cho vai trò có quyền xem.</p>
    <DentalImageDeletion v-if="canUpload" :patient-id="patientId" :refresh-key="deletionRefreshKey" @deleted="imageDeleted" />
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useDentalImagesStore } from '@/stores/dentalImages';
import { dentalImagesService, imageFileError, validImageTooth, type DentalImage, type ImageVisitTarget } from '@/services/dentalImages';
import DentalImageThumbnail from './DentalImageThumbnail.vue';
import DentalImageViewer from './DentalImageViewer.vue';
import DentalImageDeletion from './DentalImageDeletion.vue';
import type { ImageMetadataContext } from '@/services/imageMetadata';
const props = defineProps<{ patientId: number }>();
const auth = useAuthStore(), store = useDentalImagesStore();
const canUpload = computed(() => ['ADMIN', 'DENTIST', 'ASSISTANT'].includes(auth.role));
const canRead = computed(() => ['ADMIN', 'DENTIST', 'RECEPTIONIST', 'PATIENT'].includes(auth.role));
const canManage = computed(() => ['ADMIN', 'DENTIST'].includes(auth.role));
const categories: Record<string, string> = { Panorama: 'Toàn cảnh', Periapical: 'Cận chóp', Bitewing: 'Cắn cánh', Cephalometric: 'Sọ nghiêng', Intraoral: 'Trong miệng' };
const file = ref<File | null>(null), fileInput = ref<HTMLInputElement | null>(null), fileKey = ref(0);
const toothInput = ref<HTMLInputElement | null>(null), notesInput = ref<HTMLTextAreaElement | null>(null), validationTarget = ref<'file' | 'tooth' | 'notes' | ''>('');
const category = ref('Intraoral'), notes = ref(''), toothText = ref(''), visitId = ref<number | null>(null);
const busy = ref(false), error = ref(''), success = ref(''), listError = ref(''), targetError = ref(''), loadingTargets = ref(false);
const targets = ref<ImageVisitTarget[]>([]), attachTargets = ref<Record<number, number | undefined>>({});
const comparisonIds = ref<number[]>([]), viewerImages = ref<DentalImage[]>([]);
const deletionRefreshKey = ref(0);
const comparedImages = computed(() => comparisonIds.value.map(id => store.items.find(image => image.id === id)).filter((image): image is DentalImage => !!image));
function showImage(image: DentalImage) { if (canRead.value && image.patientId === props.patientId) viewerImages.value = [image]; }
function toggleComparison(image: DentalImage) {
  if (!canRead.value || image.patientId !== props.patientId) return;
  comparisonIds.value = comparisonIds.value.includes(image.id) ? comparisonIds.value.filter(id => id !== image.id) : comparisonIds.value.length < 2 ? [...comparisonIds.value, image.id] : comparisonIds.value;
}
function showComparison() { if (canRead.value && comparedImages.value.length === 2 && comparedImages.value.every(image => image.patientId === props.patientId)) viewerImages.value = [...comparedImages.value]; }
let generation = 0;
function problem(e: unknown) { const p = e as { response?: { data?: { message?: string } } }; return p.response?.data?.message || 'Không xác định được kết quả. Tải lại danh sách để đối chiếu trước khi thử tiếp.'; }
function selectFile(event: Event) { file.value = (event.target as HTMLInputElement).files?.[0] ?? null; error.value = imageFileError(file.value); validationTarget.value = error.value ? 'file' : ''; success.value = ''; }
async function loadTargets() {
  if (!canUpload.value || loadingTargets.value) return;
  const current = generation; loadingTargets.value = true; targetError.value = '';
  try { const result = await dentalImagesService.targets(props.patientId); if (current === generation) { targets.value = result; if (!result.some(x => x.visitId === visitId.value)) visitId.value = null; } }
  catch (e) { if (current === generation) { targets.value = []; visitId.value = null; targetError.value = problem(e); } }
  finally { if (current === generation) loadingTargets.value = false; }
}
async function refresh() { deletionRefreshKey.value++; if (!canRead.value) return; const current = generation; listError.value = ''; try { await store.load(props.patientId); } catch(e) { if(current === generation) listError.value = problem(e); } }
function imageDeleted(id:number) { viewerImages.value=[]; comparisonIds.value=[]; store.clear(); error.value=''; success.value=`Đã xóa mềm ảnh #${id}. Tệp vật lý và lịch sử được giữ lại.`; void refresh(); }
function metadataUpdated(value:ImageMetadataContext) { if(value.patientId!==props.patientId)return;viewerImages.value=viewerImages.value.map(i=>i.id===value.id?{...i,version:value.version,category:value.category,toothNumber:value.toothNumber,notes:value.notes}:i); }
async function loadMore() { const current = generation; try { await store.load(props.patientId, true); } catch(e) { if(current === generation) listError.value = problem(e); } }
async function upload() {
  if (busy.value || !canUpload.value) return;
  error.value = imageFileError(file.value);
  validationTarget.value = error.value ? 'file' : '';
  if (!error.value && notes.value.length > 500) { error.value = 'Ghi chú ảnh tối đa 500 ký tự.'; validationTarget.value = 'notes'; }
  const tooth = toothText.value.trim() ? Number(toothText.value.trim()) : null;
  if (!error.value && !validImageTooth(tooth)) { error.value = 'Nhập số răng FDI hợp lệ: 11–18, 21–28, 31–38, 41–48 hoặc bộ răng trẻ em.'; validationTarget.value = 'tooth'; }
  if (error.value) { (validationTarget.value === 'notes' ? notesInput.value : validationTarget.value === 'tooth' ? toothInput.value : fileInput.value)?.focus(); return; }
  const current = generation; busy.value = true; success.value = '';
  try {
    const saved = await dentalImagesService.upload({ patientId: props.patientId, visitId: visitId.value, toothNumber: tooth, category: category.value, notes: notes.value }, file.value!);
    if (current !== generation) return;
    success.value = `Đã lưu ảnh #${saved.id}: ${saved.originalFileName}.`; file.value = null; fileKey.value++; await refresh(); await loadTargets();
  } catch (e) { if (current === generation) error.value = problem(e); }
  finally { if (current === generation) busy.value = false; }
}
async function attach(image: DentalImage) {
  const target = attachTargets.value[image.id]; if (busy.value || !canManage.value || !target) return;
  const current = generation; busy.value = true; error.value = ''; success.value = '';
  try { await dentalImagesService.attach(image, target); if (current === generation) { success.value = `Đã gắn ảnh #${image.id} vào lần khám #${target}.`; await refresh(); } }
  catch (e) { if (current === generation) error.value = problem(e); }
  finally { if (current === generation) busy.value = false; }
}
watch(() => [props.patientId, auth.role, auth.user?.userId], () => { generation++; store.clear(); comparisonIds.value = []; viewerImages.value = []; file.value = null; notes.value = ''; toothText.value = ''; category.value = 'Intraoral'; validationTarget.value = ''; fileKey.value++; visitId.value = null; targets.value = []; attachTargets.value = {}; busy.value = false; loadingTargets.value = false; error.value = ''; success.value = ''; listError.value = ''; targetError.value = ''; void refresh(); void loadTargets(); }, { immediate: true });
onUnmounted(() => { generation++; viewerImages.value = []; comparisonIds.value = []; file.value = null; notes.value = ''; toothText.value = ''; store.clear(); });
</script>

<style scoped>
.image-attachments { overflow-wrap: anywhere; }
fieldset { display: grid; gap: .65rem; border: 1px solid #cbd5e1; border-radius: .5rem; padding: 1rem; max-width: 40rem; }
input, select, button, textarea { font: inherit; padding: .6rem; min-width: 0; }
.image-notes { white-space: pre-wrap; }
.image-list { list-style: none; padding: 0; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr)); gap: 1rem; }
.image-list li { border: 1px solid #cbd5e1; padding: 1rem; border-radius: .5rem; }
.image-list select { max-width: 100%; }
.error { color: #9f1239; }
</style>
