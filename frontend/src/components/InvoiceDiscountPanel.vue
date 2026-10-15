<template>
  <section aria-label="Giảm giá hóa đơn" class="discount-panel">
    <h2>Giảm giá hóa đơn</h2>
    <p>Phần trăm chỉ tính trên tiền dịch vụ; không giảm tiền thuốc. Tiền giảm được làm tròn đến 1 đồng.</p>
    <p v-if="loading" role="status">Đang tải mẫu giảm giá…</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="success" role="status">{{ success }}</p>
    <button v-if="needsReload" type="button" @click="emit('refresh')">Tải lại hóa đơn để đối chiếu</button>
    <form v-if="canEdit" @submit.prevent="apply">
      <fieldset :disabled="busy || loading || needsReload">
        <legend>Áp dụng hoặc thay thế mức giảm hiện tại</legend>
        <label>Hình thức
          <select v-model="mode">
            <option value="Preset">Mẫu giảm giá</option>
            <option v-if="role === 'ADMIN'" value="Custom">Admin nhập giảm tùy chỉnh</option>
            <option v-if="invoice.discountAmount > 0 || invoice.discountType" value="Remove">Bỏ giảm giá hiện tại</option>
          </select>
        </label>
        <label v-if="mode === 'Preset'">Mẫu đang sử dụng
          <select v-model="presetId">
            <option value="">Chọn mẫu</option>
            <option v-for="preset in activePresets" :key="preset.id" :value="String(preset.id)">
              {{ preset.code }} · {{ preset.name }} · {{ preset.discountType === 'Percentage' ? `${preset.value}%` : money(preset.value) }}
            </option>
          </select>
        </label>
        <p v-if="mode === 'Preset' && !loading && !activePresets.length">Chưa có mẫu giảm giá đang sử dụng. Admin cần cấu hình mẫu.</p>
        <template v-if="mode === 'Custom' && role === 'ADMIN'">
          <label>Loại giảm <select v-model="customType"><option value="Percentage">Phần trăm dịch vụ</option><option value="FixedAmount">Số tiền VND</option></select></label>
          <label>Giá trị <input v-model="customValue" type="number" min="0.01" :max="customType === 'Percentage' ? 100 : invoice.totalAmount" :step="customType === 'Percentage' ? '.01' : '1'" required /></label>
        </template>
        <label>Lý do ưu đãi (bắt buộc, tối đa 255 ký tự)<textarea v-model="reason" maxlength="255" required /></label>
        <template v-if="preview !== null">
          <p>Tiền giảm: <strong>{{ money(preview) }}</strong> · Phải thu sau giảm: <strong>{{ money(invoice.totalAmount - preview) }}</strong></p>
          <p v-if="overLimit" class="error">Vượt hạn mức thu ngân 25% hoặc 500.000 VND. Admin cần trực tiếp thực hiện.</p>
        </template>
        <label><input v-model="confirmed" type="checkbox" /> Tôi đã kiểm tra số tiền sau giảm và xác nhận thay đổi</label>
        <button type="submit" :disabled="!canSubmit">Xác nhận {{ mode === 'Remove' ? 'bỏ giảm giá' : 'áp dụng giảm giá' }}</button>
      </fieldset>
    </form>
    <p v-else>Chỉ sửa giảm giá trên hóa đơn chờ thanh toán chưa có khoản thu.</p>
    <details v-if="role === 'ADMIN'">
      <summary>Admin cấu hình mẫu giảm giá</summary>
      <ul><li v-for="preset in presets" :key="preset.id">
        {{ preset.code }} · {{ preset.name }} · {{ preset.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
        <button type="button" :disabled="busy || needsReload" @click="editPreset(preset)">Sửa mẫu</button>
      </li></ul>
      <form @submit.prevent="savePreset">
        <fieldset :disabled="busy || loading || needsReload">
          <legend>{{ editingId === null ? 'Tạo mẫu' : 'Sửa mẫu' }}</legend>
          <label>Mã mẫu <input v-model="editor.code" maxlength="30" pattern="[A-Za-z0-9_-]+" required /></label>
          <label>Tên mẫu <input v-model="editor.name" maxlength="100" required /></label>
          <label>Loại giảm <select v-model="editor.discountType"><option value="Percentage">Phần trăm dịch vụ</option><option value="FixedAmount">Số tiền VND</option></select></label>
          <label>Giá trị <input v-model="editor.value" type="number" min="0.01" :max="editor.discountType === 'Percentage' ? 100 : undefined" :step="editor.discountType === 'Percentage' ? '.01' : '1'" required /></label>
          <label><input v-model="editor.isActive" type="checkbox" /> Đang sử dụng</label>
          <button :disabled="!validEditor" type="submit">Lưu mẫu</button>
          <button type="button" @click="resetEditor">Mẫu mới</button>
        </fieldset>
      </form>
      <p>Sửa hoặc ngừng dùng mẫu không đổi mức giảm đã lưu trên hóa đơn cũ.</p>
    </details>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import axios from 'axios';
import type { InvoiceDraft } from '@/services/invoiceDraft';
import { invoiceDiscountApi } from '@/services/invoiceDiscounts';
import type { DiscountPreset, DiscountType } from '@/services/invoiceDiscounts';
import { calculateInvoiceDiscount, validDiscountValue } from '@/utils/invoiceDiscount';

const props = defineProps<{ invoice: InvoiceDraft; role: string | null }>();
const emit = defineEmits<{ applied: []; refresh: []; busy: [value: boolean] }>();
const presets = ref<DiscountPreset[]>([]), loading = ref(false), busy = ref(false), needsReload = ref(false);
const error = ref(''), success = ref(''), mode = ref('Preset'), presetId = ref(''), reason = ref(''), confirmed = ref(false);
const customType = ref<DiscountType>('Percentage'), customValue = ref('');
const editingId = ref<number | null>(null);
const emptyEditor = () => ({ code: '', name: '', discountType: 'Percentage' as DiscountType, value: '', isActive: true });
const editor = ref(emptyEditor());
let version = 0;
const money = (amount: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
const canEdit = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(props.role ?? '') && props.invoice.status === 1 && props.invoice.paidAmount === 0);
const activePresets = computed(() => presets.value.filter(x => x.isActive));
const selected = computed(() => activePresets.value.find(x => String(x.id) === presetId.value));
const selectedType = computed(() => mode.value === 'Preset' ? selected.value?.discountType : customType.value);
const selectedValue = computed(() => mode.value === 'Preset' ? selected.value?.value : customValue.value);
const preview = computed(() => mode.value === 'Remove' ? 0 : selectedType.value && selectedValue.value !== undefined
  ? calculateInvoiceDiscount(props.invoice, selectedType.value, selectedValue.value) : null);
const overLimit = computed(() => props.role === 'RECEPTIONIST' && preview.value !== null &&
  (preview.value > 500000 || preview.value > props.invoice.totalAmount * .25 ||
   mode.value !== 'Remove' && selectedType.value === 'Percentage' && Number(selectedValue.value) > 25));
const canSubmit = computed(() => canEdit.value && !busy.value && !loading.value && !needsReload.value && confirmed.value &&
  Number.isSafeInteger(props.invoice.totalAmount) && Number.isSafeInteger(props.invoice.discountAmount) &&
  reason.value.trim().length > 0 && reason.value.trim().length <= 255 && preview.value !== null && !overLimit.value &&
  (mode.value === 'Preset' && !!selected.value || mode.value === 'Custom' && props.role === 'ADMIN' ||
   mode.value === 'Remove' && (props.invoice.discountAmount > 0 || !!props.invoice.discountType)));
const validEditor = computed(() => /^[A-Za-z0-9_-]{1,30}$/.test(editor.value.code) && editor.value.name.trim().length > 0 &&
  editor.value.name.trim().length <= 100 && validDiscountValue(editor.value.discountType, editor.value.value));
function failure(cause: unknown) { return axios.isAxiosError(cause) ? cause.response?.data?.message || 'Không xác định kết quả. Tải lại hóa đơn để đối chiếu.' : 'Không thể xử lý giảm giá.'; }
function resetEditor() { editingId.value = null; editor.value = emptyEditor(); }
function editPreset(preset: DiscountPreset) {
  if (busy.value || needsReload.value || props.role !== 'ADMIN') return;
  editingId.value = preset.id; editor.value = { ...preset, value: String(preset.value) };
}
async function loadPresets() {
  const ticket = ++version; loading.value = true; presets.value = []; presetId.value = ''; confirmed.value = false;
  error.value = ''; success.value = ''; needsReload.value = false; busy.value = false; emit('busy', false);
  reason.value = ''; mode.value = 'Preset'; customValue.value = ''; resetEditor();
  if (!['ADMIN', 'RECEPTIONIST'].includes(props.role ?? '')) { loading.value = false; return; }
  try { const data = await invoiceDiscountApi.presets(); if (ticket === version) presets.value = data; }
  catch (cause) { if (ticket === version) { error.value = failure(cause); needsReload.value = true; } }
  finally { if (ticket === version) loading.value = false; }
}
async function apply() {
  if (!canSubmit.value) return;
  const ticket = version; busy.value = true; emit('busy', true); error.value = ''; success.value = '';
  const request = { mode: mode.value as 'Preset' | 'Custom' | 'Remove', presetId: mode.value === 'Preset' ? selected.value!.id : null,
    discountType: mode.value === 'Custom' ? customType.value : null, value: mode.value === 'Custom' ? Number(customValue.value) : null,
    reason: reason.value.trim(), expectedTotalAmount: props.invoice.totalAmount,
    expectedCurrentDiscountAmount: props.invoice.discountAmount, expectedDiscountAmount: preview.value! };
  try { await invoiceDiscountApi.apply(props.invoice.id, request); if (ticket === version) { needsReload.value = true; emit('applied'); } }
  catch (cause) { if (ticket === version) { error.value = failure(cause); needsReload.value = true; } }
  finally { if (ticket === version) { busy.value = false; confirmed.value = false; emit('busy', false); } }
}
async function savePreset() {
  if (props.role !== 'ADMIN' || !validEditor.value || busy.value || loading.value || needsReload.value) return;
  const ticket = version; busy.value = true; emit('busy', true); error.value = ''; success.value = '';
  try {
    const data = await invoiceDiscountApi.savePreset(editingId.value, { ...editor.value, name: editor.value.name.trim(), value: Number(editor.value.value) });
    if (ticket !== version) return;
    presets.value = [...presets.value.filter(x => x.id !== data.id), data].sort((a, b) => a.code.localeCompare(b.code));
    confirmed.value = false; presetId.value = ''; resetEditor(); success.value = 'Đã lưu mẫu giảm giá.';
  } catch (cause) { if (ticket === version) { error.value = failure(cause); needsReload.value = true; } }
  finally { if (ticket === version) { busy.value = false; emit('busy', false); } }
}
watch([mode, presetId, customType, customValue, reason], () => { confirmed.value = false; });
watch(() => props.invoice.id, loadPresets, { immediate: true });
onUnmounted(() => { version++; presets.value = []; reason.value = ''; resetEditor(); emit('busy', false); });
</script>

<style scoped>
.discount-panel { margin: 1.5rem 0; padding: 1rem; border: 1px solid #cbd5e1; border-radius: .5rem; }
label { display: block; margin: .75rem 0; }
input, select, textarea { max-width: 100%; }
textarea { display: block; width: 100%; min-height: 4rem; }
button { margin: .4rem; padding: .5rem; }
.error { color: #b91c1c; }
</style>
