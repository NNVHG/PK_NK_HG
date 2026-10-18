<template><main class="clinic-settings"><h1>Thông tin phòng khám trên bản in</h1><p>Dùng cho đơn thuốc và biên lai. Tên cơ sở: Nha khoa Hoàng Gia.</p>
 <p v-if="error" role="alert">{{ error }}</p><p v-if="message" role="status">{{ message }}</p>
 <button :disabled="busy || !allowed" @click="load">Tải lại thông tin</button>
 <form v-if="loaded" @submit.prevent="save"><label>Địa chỉ<input v-model="address" maxlength="500" required :disabled="busy || needsReview" /></label><label>Hotline<input v-model="phone" maxlength="30" required type="tel" :disabled="busy || needsReview" /></label>
 <label><input v-model="confirmed" type="checkbox" :disabled="busy || needsReview" /> Tôi đã đối chiếu thông tin phòng khám</label><button :disabled="busy || needsReview || !confirmed">Lưu thông tin in</button></form>
 <RouterLink to="/admin/staff">Khai báo học hàm/học vị và ảnh chữ ký bác sĩ</RouterLink>
 </main></template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { clinicPrintSettingsService, type ClinicPrintSettings } from '@/services/clinicPrintSettings';
const auth = useAuthStore(), allowed = computed(() => auth.role === 'ADMIN');
const address = ref(''), phone = ref(''), revision = ref(''), loaded = ref(''), busy = ref(false), confirmed = ref(false), needsReview = ref(false), error = ref(''), message = ref('');
let generation = 0, disposed = false;
const identity = () => auth.user?.userId + '/' + auth.role;
function clear() { address.value = ''; phone.value = ''; revision.value = ''; loaded.value = ''; confirmed.value = false; }
function alive(n: number, id: string) { return !disposed && generation === n && id === identity(); }
function valid(s: ClinicPrintSettings) { return /^[A-F0-9]{64}$/.test(s.revision) && (s.address === null || typeof s.address === 'string') && (s.phone === null || typeof s.phone === 'string'); }
async function load() {
 if (disposed || busy.value || !allowed.value) return;
 const n = ++generation, id = identity(); clear(); busy.value = true; error.value = ''; message.value = '';
 try { const data = await clinicPrintSettingsService.read(); if (!alive(n,id)) return; if (!valid(data)) throw Error('Invalid settings'); address.value = data.address ?? ''; phone.value = data.phone ?? ''; revision.value = data.revision; loaded.value = id; needsReview.value = false; }
 catch { if (alive(n,id)) { clear(); needsReview.value = true; error.value = 'Không đọc được cấu hình; kiểm tra quyền và tải lại.'; } }
 finally { if (alive(n,id)) busy.value = false; }
}
async function save() {
 if (disposed || busy.value || !allowed.value || loaded.value !== identity() || needsReview.value || !confirmed.value) return;
 if (!address.value.trim() || address.value.length > 500 || /[\x00-\x1f\x7f]/.test(address.value) || !/^[0-9+ ().-]{8,30}$/.test(phone.value)) { error.value = 'Kiểm tra địa chỉ tối đa 500 ký tự và hotline 8–30 ký tự.'; return; }
 const n = generation, id = identity(); busy.value = true; confirmed.value = false; error.value = ''; message.value = '';
 try { const data = await clinicPrintSettingsService.save(address.value.trim(), phone.value.trim(), revision.value); if (!alive(n,id)) return; if (!valid(data) || data.address !== address.value.trim() || data.phone !== phone.value.trim()) throw Error('Invalid saved settings'); revision.value = data.revision; message.value = 'Đã lưu thông tin phòng khám và audit.'; }
 catch { if (alive(n,id)) { needsReview.value = true; loaded.value = ''; error.value = 'Chưa xác minh kết quả lưu hoặc thông tin vừa đổi. Tải lại trước khi gửi tiếp.'; } }
 finally { if (alive(n,id)) busy.value = false; }
}
watch(identity, () => { generation++; busy.value = false; clear(); error.value = ''; message.value = ''; if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clear(); });
</script>
<style scoped>.clinic-settings { max-width:48rem;margin:1rem auto;padding:1rem; } label { display:block;margin:1rem 0; } input,button { padding:.5rem; } input:not([type=checkbox]) { width:100%; } [role=alert] { color:#b91c1c; }</style>
