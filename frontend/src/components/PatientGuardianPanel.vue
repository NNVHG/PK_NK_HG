<template>
  <div class="guardian-management">
    <div class="section-header">
      <div>
        <p class="eyebrow">QUYỀN GIÁM HỘ (DL-353, DL-358, DL-359)</p>
        <h3>Đại diện hợp pháp & Giám hộ</h3>
      </div>
      <button
        v-if="canManage && !activeGuardian"
        type="button"
        class="button button-primary"
        @click="showAssignModal = true"
      >
        + Gán người giám hộ
      </button>
    </div>

    <p v-if="errorMessage" class="notice error" role="alert">{{ errorMessage }}</p>
    <p v-if="successMessage" class="notice success" role="status">{{ successMessage }}</p>

    <p v-if="loading" class="empty-state">Đang tải thông tin giám hộ...</p>

    <div v-else-if="activeGuardian" class="active-guardian-card">
      <div class="card-header">
        <span class="status-badge active">Đang hiệu lực</span>
        <h4>{{ activeGuardian.guardianFullName }} ({{ activeGuardian.relationship }})</h4>
        <p class="code">Mã BN: {{ activeGuardian.guardianPatientCode }}</p>
      </div>
      <div class="card-body">
        <p><strong>Ngày gán:</strong> {{ formatDate(activeGuardian.assignedAt) }}</p>
        <p><strong>Người thực hiện:</strong> {{ activeGuardian.assignedByUserName }}</p>
        <p v-if="activeGuardian.notes"><strong>Ghi chú:</strong> {{ activeGuardian.notes }}</p>
      </div>

      <div v-if="canManage" class="card-actions">
        <button
          type="button"
          class="button button-danger"
          :disabled="actionLoading"
          @click="showRevokeModal = true"
        >
          Thu hồi quyền giám hộ
        </button>
        <button
          type="button"
          class="button button-success"
          :disabled="actionLoading"
          @click="showPromoteModal = true"
        >
          Tách tài khoản độc lập (Có CCCD)
        </button>
      </div>
    </div>

    <p v-else class="empty-state">Bệnh nhân chưa có người giám hộ hiệu lực nào.</p>

    <div v-if="canManage && history.length > 0" class="history-section">
      <h4>Lịch sử thay đổi giám hộ</h4>
      <table class="history-table">
        <thead>
          <tr>
            <th>Người giám hộ</th>
            <th>Quan hệ</th>
            <th>Thời gian gán</th>
            <th>Người gán</th>
            <th>Trạng thái</th>
            <th>Thời gian thu hồi</th>
            <th>Ghi chú</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in history" :key="item.patientGuardianId">
            <td>{{ item.guardianFullName }} ({{ item.guardianPatientCode }})</td>
            <td>{{ item.relationship }}</td>
            <td>{{ formatDate(item.assignedAt) }}</td>
            <td>{{ item.assignedByUserName }}</td>
            <td>
              <span :class="['badge', item.isActive ? 'badge-success' : 'badge-muted']">
                {{ item.isActive ? 'Hiệu lực' : 'Đã thu hồi' }}
              </span>
            </td>
            <td>{{ item.revokedAt ? formatDate(item.revokedAt) : '—' }}</td>
            <td>{{ item.notes || '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Modal Gán Giám Hộ -->
    <div v-if="showAssignModal" class="modal-backdrop">
      <div class="modal-content">
        <h3>Gán người giám hộ cho bệnh nhân</h3>
        <form @submit.prevent="submitAssign">
          <label>
            ID hồ sơ người giám hộ:
            <input v-model.number="assignForm.guardianPatientId" type="number" required min="1" placeholder="Nhập ID hồ sơ bệnh nhân giám hộ" />
          </label>
          <label>
            Mối quan hệ:
            <select v-model="assignForm.relationship" required>
              <option value="Cha">Cha</option>
              <option value="Mẹ">Mẹ</option>
              <option value="Người giám hộ hợp pháp">Người giám hộ hợp pháp</option>
              <option value="Khác">Khác</option>
            </select>
          </label>
          <label>
            Ghi chú / Chứng từ đối soát:
            <textarea v-model="assignForm.notes" rows="2" maxlength="500" placeholder="Số giấy khai sinh, giấy tờ pháp lý..."></textarea>
          </label>
          <div class="modal-actions">
            <button type="button" class="button button-quiet" @click="showAssignModal = false">Hủy</button>
            <button type="submit" class="button button-primary" :disabled="actionLoading">
              {{ actionLoading ? 'Đang lưu...' : 'Xác nhận gán' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Thu Hồi Giám Hộ -->
    <div v-if="showRevokeModal" class="modal-backdrop">
      <div class="modal-content">
        <h3>Thu hồi quyền giám hộ</h3>
        <p>Bạn có chắc chắn muốn thu hồi quyền giám hộ của <strong>{{ activeGuardian?.guardianFullName }}</strong>?</p>
        <form @submit.prevent="submitRevoke">
          <label>
            Lý do thu hồi:
            <textarea v-model="revokeForm.notes" rows="2" maxlength="500" placeholder="Lý do thu hồi..." required></textarea>
          </label>
          <div class="modal-actions">
            <button type="button" class="button button-quiet" @click="showRevokeModal = false">Hủy</button>
            <button type="submit" class="button button-danger" :disabled="actionLoading">
              {{ actionLoading ? 'Đang xử lý...' : 'Xác nhận thu hồi' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Tách Tài Khoản Độc Lập -->
    <div v-if="showPromoteModal" class="modal-backdrop">
      <div class="modal-content">
        <h3>Chuyển đổi thành tài khoản độc lập (DL-359)</h3>
        <p>Bệnh nhân đã đủ điều kiện có CCCD riêng. Khi tạo tài khoản độc lập, quyền giám hộ hiện hành sẽ được chấm dứt tự động cùng giao dịch.</p>
        <form @submit.prevent="submitPromote">
          <label>
            Số CCCD 12 chữ số:
            <input v-model="promoteForm.rawCitizenId" type="text" pattern="[0-9]{12}" required placeholder="079090123456" />
          </label>
          <label>
            Số điện thoại riêng (nếu có, mặc định dùng SĐT hồ sơ):
            <input v-model="promoteForm.phone" type="tel" placeholder="0912345678" />
          </label>
          <label>
            Ghi chú:
            <textarea v-model="promoteForm.notes" rows="2" maxlength="500" placeholder="Ghi chú đối soát tại quầy..."></textarea>
          </label>
          <div class="modal-actions">
            <button type="button" class="button button-quiet" @click="showPromoteModal = false">Hủy</button>
            <button type="submit" class="button button-success" :disabled="actionLoading">
              {{ actionLoading ? 'Đang chuyển đổi...' : 'Xác nhận cấp tài khoản riêng' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { patientGuardiansService, type PatientGuardianSummary } from '@/services/patientGuardians';

const props = defineProps<{
  patientId: number;
}>();

const emit = defineEmits<{
  (e: 'promoted'): void;
}>();

const authStore = useAuthStore();
const canManage = computed(() => ['ADMIN', 'RECEPTIONIST'].includes(authStore.role));

const loading = ref(false);
const actionLoading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const activeGuardian = ref<PatientGuardianSummary | null>(null);
const history = ref<PatientGuardianSummary[]>([]);

const showAssignModal = ref(false);
const showRevokeModal = ref(false);
const showPromoteModal = ref(false);

const assignForm = ref({ guardianPatientId: 0, relationship: 'Cha', notes: '' });
const revokeForm = ref({ notes: '' });
const promoteForm = ref({ rawCitizenId: '', phone: '', notes: '' });

function formatDate(iso: string) {
  return new Date(iso).toLocaleString('vi-VN');
}

async function loadData() {
  loading.value = true;
  errorMessage.value = '';
  try {
    try {
      activeGuardian.value = await patientGuardiansService.getActiveGuardian(props.patientId);
    } catch {
      activeGuardian.value = null;
    }

    if (canManage.value) {
      try {
        history.value = await patientGuardiansService.getHistory(props.patientId);
      } catch {
        history.value = [];
      }
    }
  } finally {
    loading.value = false;
  }
}

async function submitAssign() {
  actionLoading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await patientGuardiansService.assign({
      guardianPatientId: assignForm.value.guardianPatientId,
      wardPatientId: props.patientId,
      relationship: assignForm.value.relationship,
      notes: assignForm.value.notes
    });
    successMessage.value = 'Gán người giám hộ thành công.';
    showAssignModal.value = false;
    assignForm.value = { guardianPatientId: 0, relationship: 'Cha', notes: '' };
    await loadData();
  } catch (e: any) {
    errorMessage.value = e.response?.data?.message || 'Không thể gán người giám hộ.';
  } finally {
    actionLoading.value = false;
  }
}

async function submitRevoke() {
  actionLoading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await patientGuardiansService.revoke(props.patientId, revokeForm.value.notes);
    successMessage.value = 'Thu hồi quyền giám hộ thành công.';
    showRevokeModal.value = false;
    revokeForm.value = { notes: '' };
    await loadData();
  } catch (e: any) {
    errorMessage.value = e.response?.data?.message || 'Không thể thu hồi quyền giám hộ.';
  } finally {
    actionLoading.value = false;
  }
}

async function submitPromote() {
  actionLoading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await patientGuardiansService.promoteIndependent(props.patientId, {
      rawCitizenId: promoteForm.value.rawCitizenId,
      phone: promoteForm.value.phone || null,
      notes: promoteForm.value.notes || null
    });
    successMessage.value = 'Tách tài khoản độc lập và chấm dứt quyền giám hộ thành công.';
    showPromoteModal.value = false;
    promoteForm.value = { rawCitizenId: '', phone: '', notes: '' };
    emit('promoted');
    await loadData();
  } catch (e: any) {
    errorMessage.value = e.response?.data?.message || 'Không thể tách tài khoản độc lập.';
  } finally {
    actionLoading.value = false;
  }
}

onMounted(loadData);
</script>

<style scoped>
.guardian-management { margin-top: 1.5rem; }
.section-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1rem; }
.active-guardian-card { border: 1px solid #cbd5e1; border-radius: 8px; padding: 1.25rem; background: #f8fafc; margin-bottom: 1.5rem; }
.card-header { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.75rem; }
.status-badge { font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.5rem; border-radius: 4px; }
.status-badge.active { background: #dcfce7; color: #15803d; }
.card-actions { display: flex; gap: 0.75rem; margin-top: 1rem; }
.history-section { margin-top: 2rem; }
.history-table { width: 100%; border-collapse: collapse; margin-top: 0.75rem; font-size: 0.875rem; }
.history-table th, .history-table td { border: 1px solid #e2e8f0; padding: 0.5rem 0.75rem; text-align: left; }
.history-table th { background: #f1f5f9; }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; align-items: center; justify-content: center; z-index: 50; }
.modal-content { background: #fff; padding: 1.5rem; border-radius: 8px; max-width: 28rem; width: 100%; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }
.modal-content label { display: block; margin-top: 0.75rem; font-size: 0.875rem; font-weight: 500; }
.modal-content input, .modal-content select, .modal-content textarea { width: 100%; padding: 0.5rem; border: 1px solid #cbd5e1; border-radius: 4px; margin-top: 0.25rem; font: inherit; }
.modal-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.25rem; }
.button-danger { background: #ef4444; color: #fff; border: none; padding: 0.5rem 0.85rem; border-radius: 4px; cursor: pointer; }
.button-success { background: #10b981; color: #fff; border: none; padding: 0.5rem 0.85rem; border-radius: 4px; cursor: pointer; }
.empty-state { color: #64748b; font-style: italic; }
</style>

