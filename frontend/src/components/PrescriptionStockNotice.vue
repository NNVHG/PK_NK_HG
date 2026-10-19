<template>
  <section v-if="auth.role === 'DENTIST' && items.length" aria-labelledby="rx-stock-title" class="stock-notice">
    <h3 id="rx-stock-title">Tồn thuốc tham khảo</h3>
    <p>Chưa giữ hoặc trừ tồn khi kê đơn. Khi xác nhận giao, hệ thống kiểm lại và chặn nếu thiếu thuốc.</p>
    <button type="button" :disabled="loading" @click="load">Tải lại tồn thuốc</button>
    <p v-if="loading" role="status">Đang đọc tồn thuốc…</p><p v-if="error" role="alert">{{ error }}</p>
    <ul v-if="!loading && !error"><li v-for="item in items" :key="item.medicineId">
      <template v-if="byId(item.medicineId)">{{ byId(item.medicineId)!.code }} · {{ byId(item.medicineId)!.name }}:
        còn {{ byId(item.medicineId)!.onHand }} {{ byId(item.medicineId)!.unit }}.
        <strong v-if="Number.isInteger(item.quantity) && item.quantity > byId(item.medicineId)!.onHand">Thiếu so với {{ item.quantity }} cần kê; vẫn có thể lưu đơn.</strong>
        <strong v-if="item.unit !== byId(item.medicineId)!.unit">Đơn vị đã thay đổi; đối chiếu lại thuốc trước khi kê.</strong>
      </template><span v-else>Chưa xác minh được tồn của thuốc {{ item.medicineId }}.</span>
    </li></ul>
  </section>
</template>
<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { medicineStockService, type MedicineAvailability } from '@/services/medicineStock';
const props = defineProps<{ visitId: number; items: { medicineId: number; quantity: number; unit: string }[] }>();
const auth = useAuthStore(), rows = ref<MedicineAvailability[]>([]), loading = ref(false), error = ref('');
let generation = 0, disposed = false;
const identity = () => `${auth.user?.userId}/${auth.role}/${props.visitId}/${[...new Set(props.items.map(i => i.medicineId))].sort((a,b) => a-b).join(',')}`;
function byId(id: number) { return rows.value.find(r => r.medicineId === id); }
async function load() {
  const n = ++generation, actor = identity(); rows.value = []; error.value = ''; loading.value = false;
  const ids = [...new Set(props.items.map(i => i.medicineId))];
  if (disposed || auth.role !== 'DENTIST' || !Number.isInteger(props.visitId) || props.visitId <= 0 || ids.length === 0 || ids.length > 10 || ids.some(id => !Number.isInteger(id) || id <= 0)) return;
  loading.value = true;
  try {
    const result = await medicineStockService.availability(props.visitId, ids);
    if (disposed || n !== generation || actor !== identity()) return;
    if (!Array.isArray(result) || result.length > ids.length || new Set(result.map(r => r.medicineId)).size !== result.length || result.some(r => !ids.includes(r.medicineId) || !Number.isInteger(r.onHand) || r.onHand < 0 || r.onHand > 2147483647 || typeof r.isActive !== 'boolean' || typeof r.unit !== 'string' || typeof r.code !== 'string' || typeof r.name !== 'string')) throw new Error('Invalid stock response');
    rows.value = result;
  } catch { if (!disposed && n === generation && actor === identity()) error.value = 'Chưa đọc được tồn thuốc. Có thể tiếp tục kê; cần tải lại để đối chiếu trước khi giao.'; }
  finally { if (!disposed && n === generation) loading.value = false; }
}
watch(identity, () => { void load(); }, { immediate: true });
onUnmounted(() => { disposed = true; generation++; rows.value = []; loading.value = false; });
</script>
<style scoped>.stock-notice{border:1px solid #94a3b8;padding:1rem;margin:1rem 0}.stock-notice strong{display:block;color:#92400e}.stock-notice li{margin:.5rem 0}[role=alert]{color:#b91c1c}button{font:inherit;padding:.5rem}</style>
