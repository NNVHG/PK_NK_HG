<script setup lang="ts">
import { onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useSchedulingSettingsStore } from '@/stores/schedulingSettings';
import { validSchedule } from '@/services/schedulingSettings';
const store = useSchedulingSettingsStore();
const form = reactive({ workStartTime: '', lunchStartTime: '', lunchEndTime: '', workEndTime: '', slotDurationMinutes: 0 }), confirmed = ref(false), error = ref('');
const heading = ref<HTMLElement | null>(null);
watch(() => store.current, value => { if (value) Object.assign(form, { workStartTime: value.workStartTime, lunchStartTime: value.lunchStartTime, lunchEndTime: value.lunchEndTime, workEndTime: value.workEndTime, slotDurationMinutes: value.slotDurationMinutes }); confirmed.value = false; }, { immediate: true });
async function save() {
  error.value = '';
  if (!store.current || !confirmed.value || !validSchedule({ ...form, expectedRevision: store.current.revision })) { error.value = 'Kiểm tra thứ tự giờ, thời lượng nguyên phút và xác nhận thay đổi.'; return; }
  await store.save({ ...form, expectedRevision: store.current.revision }); confirmed.value = false;
}
onMounted(() => { heading.value?.focus(); void store.load(); });
onUnmounted(() => { confirmed.value = false; Object.assign(form, { workStartTime: '', lunchStartTime: '', lunchEndTime: '', workEndTime: '', slotDurationMinutes: 0 }); });
</script>
<template>
  <section class="settings-page">
    <h1 ref="heading" tabindex="-1">Giờ làm việc & khung hẹn</h1>
    <p>Cấu hình mới dùng cho lần đặt hoặc đổi lịch tiếp theo. Ngày, giờ và phần chỗ của lịch đã hẹn được giữ nguyên.</p>
    <p><RouterLink to="/admin/clinic-print-settings">Thông tin phòng khám trên bản in</RouterLink></p>
    <button type="button" :disabled="store.busy" @click="store.load">Tải lại cấu hình (thay nội dung đang nhập)</button>
    <p v-if="store.busy" role="status">Đang xử lý…</p><p v-if="store.error" role="alert">{{ store.error }}</p><p v-if="error" role="alert">{{ error }}</p><p v-if="store.message" role="status">{{ store.message }}</p>
    <p v-if="store.current?.usingFallback">Chưa có đủ cấu hình hợp lệ; hệ thống đang dùng giá trị dự phòng an toàn. Kiểm tra các giờ dưới đây trước khi lưu.</p>
    <form v-if="store.current" @submit.prevent="save"><fieldset :disabled="store.busy || store.needsReload"><legend>Giờ Việt Nam (Asia/Ho_Chi_Minh)</legend>
      <label>Giờ mở cửa<input v-model="form.workStartTime" type="time" step="60" required @input="confirmed = false" /></label>
      <label>Bắt đầu nghỉ trưa<input v-model="form.lunchStartTime" type="time" step="60" required @input="confirmed = false" /></label>
      <label>Kết thúc nghỉ trưa<input v-model="form.lunchEndTime" type="time" step="60" required @input="confirmed = false" /></label>
      <label>Giờ đóng cửa<input v-model="form.workEndTime" type="time" step="60" required @input="confirmed = false" /></label>
      <label>Thời lượng mỗi slot (phút)<input v-model.number="form.slotDurationMinutes" type="number" min="1" max="1440" step="1" required @input="confirmed = false" /></label>
      <p>Slot phải nằm trọn trong một buổi. Trần 100 chỗ đồng thời và quyền xử lý lịch cấp cứu giữ theo quy định hiện hành.</p>
      <label class="confirm"><input v-model="confirmed" type="checkbox" required /> Tôi đã kiểm tra giờ, thời lượng và xác nhận áp dụng cho lần đặt/đổi lịch mới.</label>
      <button type="submit" :disabled="!confirmed">Lưu cấu hình</button>
    </fieldset></form>
  </section>
</template>
<style scoped>
.settings-page { max-width: 720px; margin: auto; padding: 1.5rem; color: #172b3a; }fieldset { display: grid; gap: 1rem; margin-block: 1rem; padding: 1.25rem; border: 1px solid #aab8c4; }label { display: grid; gap: .4rem; }input, button { font: inherit; padding: .6rem; }button { min-height: 44px; cursor: pointer; }button:disabled { cursor: default; }.confirm { display: flex; gap: .75rem; align-items: start; }[role="alert"] { color: #9a241d; }:focus-visible { outline: 3px solid #2168aa; outline-offset: 3px; }
</style>
