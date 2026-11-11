<template>
  <main class="public-rx">
    <header class="rx-header">
      <div class="header-brand">
        <img src="/logo-icon.svg" alt="Nha Khoa Hoàng Gia" class="brand-logo" width="44" height="44" />
        <div>
          <span class="brand-badge">Nha Khoa Hoàng Gia</span>
          <h1>Tra cứu Đơn thuốc Điện tử</h1>
        </div>
      </div>
      <p class="rx-tagline">Hệ thống tra cứu thông tin đơn thuốc lâm sàng chính thức theo định danh CCCD của bệnh nhân.</p>
    </header>

    <div v-if="summary" class="rx-meta-card">
      <div class="meta-item">
        <span class="meta-label">Mã đơn thuốc</span>
        <span class="meta-value meta-code">{{ summary.prescriptionCode }}</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">Ngày kê đơn</span>
        <span class="meta-value">{{ new Date(summary.createdAt).toLocaleDateString('vi-VN') }}</span>
      </div>
      <button :disabled="busy" @click="load" class="btn-refresh">Tải lại</button>
    </div>

    <p v-if="busy" role="status" class="alert-status">Đang đối chiếu dữ liệu y tế…</p>
    <p v-if="error" role="alert" class="alert-error">{{ error }}</p>

    <!-- Form xác minh CCCD chủ đơn -->
    <form v-if="summary && !details" @submit.prevent="verify" class="verify-form">
      <h2>Xác thực quyền xem đơn thuốc</h2>
      <p class="verify-desc">Nhập số CCCD gồm 12 chữ số của chủ đơn thuốc để bảo mật thông tin khám chữa bệnh theo quy định.</p>
      <label>CCCD của chủ đơn thuốc
        <input v-model="citizenId" type="password" inputmode="numeric" maxlength="12" autocomplete="off" :disabled="busy" placeholder="••••••••••••" required />
      </label>
      <button :disabled="busy" class="btn-primary">Đối chiếu và xem đơn thuốc</button>
    </form>

    <!-- Chi tiết đơn thuốc sau khi verify thành công -->
    <section v-if="details" aria-label="Chi tiết đơn thuốc" class="prescription-details">
      <div class="patient-banner">
        <div class="patient-info">
          <div class="info-row"><span class="label">Bệnh nhân:</span> <strong>{{ details.patientName }}</strong></div>
          <div class="info-row"><span class="label">Nha sĩ phụ trách:</span> <strong>{{ details.doctorName }}</strong></div>
        </div>
        <div class="diagnosis-block">
          <span class="label">Chẩn đoán:</span>
          <span class="diagnosis-text">{{ details.diagnosis }}</span>
        </div>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Thuốc / Hàm lượng</th>
              <th style="width: 130px;">Số lượng</th>
              <th>Liều lượng &amp; Hướng dẫn dùng</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(item, i) in details.items" :key="i">
              <td>
                <span class="medicine-name">{{ item.name }}</span>
                <span v-if="item.strength" class="medicine-strength"> {{ item.strength }}</span>
              </td>
              <td><strong>{{ item.quantity }}</strong> <small>{{ item.unit }}</small></td>
              <td>
                <div class="usage-text">{{ item.usageInstructions }}</div>
                <div v-if="item.treatmentDays" class="treatment-days">Dùng trong: {{ item.treatmentDays }} ngày</div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="details.doctorAdvice || details.revisitDate" class="doctor-notes">
        <p v-if="details.doctorAdvice" class="note-item">
          <strong>Lời dặn của nha sĩ:</strong> {{ details.doctorAdvice }}
        </p>
        <p v-if="details.revisitDate" class="note-item">
          <strong>Hẹn ngày tái khám:</strong> {{ details.revisitDate }}
        </p>
      </div>

      <footer class="details-footer">
        <span class="session-expiry">Phiên xem an toàn hết hạn lúc: <strong>{{ new Date(expiresAt).toLocaleTimeString('vi-VN') }}</strong></span>
        <button @click="close" class="btn-close">Đóng chi tiết</button>
      </footer>
    </section>
  </main>
</template>
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { publicPrescriptionsService, type PublicRxSummary, type PublicRxDetails, type PublicRxSession } from '@/services/publicPrescriptions';
const route = useRoute();
const code = computed(() => String(route.params.code ?? ''));
const summary = ref<PublicRxSummary | null>(null), details = ref<PublicRxDetails | null>(null), citizenId = ref(''), error = ref(''), busy = ref(false), expiresAt = ref('');
let generation = 0, disposed = false, timer: ReturnType<typeof setTimeout> | undefined;
const key = (value = code.value) => 'rx-view:' + value;
function removeSession(value = code.value) { try { sessionStorage.removeItem(key(value)); } catch { /* Browser may disable storage. */ } }
function clearPrivate() { details.value = null; citizenId.value = ''; expiresAt.value = ''; if (timer) clearTimeout(timer); timer = undefined; }
function close() { removeSession(); clearPrivate(); error.value = ''; }
function alive(ticket: number, current: string) { return !disposed && generation === ticket && code.value === current; }
function validSession(s: PublicRxSession) { const expiry = Date.parse(s.expiresAt); return typeof s.token === 'string' && s.token.length > 0 && s.token.length <= 4096 && Number.isFinite(expiry) && expiry > Date.now() && expiry <= Date.now() + 16 * 60000; }
async function readDetails(session: PublicRxSession, ticket: number, current: string) {
 if (!validSession(session)) throw Error('Expired session');
 const data = await publicPrescriptionsService.details(current, session.token);
 if (!alive(ticket, current)) return;
 if (!validSession(session) || data.prescriptionCode !== current || !Array.isArray(data.items)) throw Error('Invalid details');
 details.value = data; expiresAt.value = session.expiresAt;
 try { sessionStorage.setItem(key(current), JSON.stringify(session)); } catch { /* Current page can still use its in-memory session. */ }
 timer = setTimeout(() => { if (alive(ticket, current)) { removeSession(current); clearPrivate(); error.value = 'Phiên xem đã hết hạn. Nhập lại CCCD để đối chiếu.'; } }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
}
async function load() {
 if (disposed || busy.value) return;
 const ticket = ++generation, current = code.value; summary.value = null; clearPrivate(); error.value = '';
 if (current.length !== 16 || !/^RX-[0-9]{8}-[0-9]{4}$/.test(current)) { error.value = 'Mã đơn thuốc không hợp lệ.'; return; }
 busy.value = true;
 try {
  const result = await publicPrescriptionsService.summary(current); if (!alive(ticket, current)) return;
  if (result.prescriptionCode !== current || !Number.isFinite(Date.parse(result.createdAt))) throw Error('Invalid summary');
  summary.value = result;
  let stored: PublicRxSession | null = null;
  try { const raw = sessionStorage.getItem(key(current)); if (raw) stored = JSON.parse(raw); } catch { removeSession(current); }
  if (stored) { if (validSession(stored)) await readDetails(stored, ticket, current); else removeSession(current); }
 } catch {
  if (alive(ticket, current)) { removeSession(current); clearPrivate(); error.value = 'Không tải được đơn hoặc phiên xem không còn hợp lệ. Có thể tải lại và đối chiếu CCCD.'; }
 } finally { if (alive(ticket, current)) busy.value = false; }
}
async function verify() {
 if (disposed || busy.value || !summary.value) return;
 if (citizenId.value.length !== 12 || !/^[0-9]{12}$/.test(citizenId.value)) { error.value = 'CCCD cần đúng 12 chữ số.'; return; }
 const ticket = generation, current = code.value, input = citizenId.value; busy.value = true; error.value = ''; clearPrivate(); removeSession(current);
 try {
  const session = await publicPrescriptionsService.verify(current, input);
  if (alive(ticket, current)) await readDetails(session, ticket, current);
 } catch (failure: unknown) {
  if (alive(ticket, current)) { removeSession(current); clearPrivate(); const status = (failure as { response?: { status?: number } })?.response?.status;
   error.value = status === 429 ? 'Đã vượt giới hạn đối chiếu. Vui lòng thử lại sau.' : 'Chưa đối chiếu được CCCD hoặc đơn không còn được xem. Kiểm tra thông tin và thử lại.'; }
 } finally { if (alive(ticket, current)) busy.value = false; }
}
watch(code, () => { generation++; busy.value = false; clearPrivate(); summary.value = null; error.value = ''; if (!disposed) void load(); });
onMounted(() => load()); onUnmounted(() => { disposed = true; generation++; clearPrivate(); summary.value = null; error.value = ''; });
</script>
<style scoped>
.public-rx {
  max-width: 820px;
  margin: 2.5rem auto;
  padding: 2rem;
  background: #f8fafc;
  color: #1e293b;
  font-family: inherit;
}

.rx-header {
  margin-bottom: 1.5rem;
  padding-bottom: 1.25rem;
  border-bottom: 1px solid #e2e8f0;
}

.header-brand {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.brand-badge {
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: #0369a1;
  background: #e0f2fe;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  display: inline-block;
  margin-bottom: 0.25rem;
}

h1 {
  color: #0f2d59;
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0;
}

.rx-tagline {
  color: #64748b;
  font-size: 0.88rem;
  line-height: 1.5;
  margin: 0.5rem 0 0;
}

.rx-meta-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1rem 1.25rem;
  margin-bottom: 1.25rem;
  box-shadow: 0 2px 6px rgba(15, 45, 89, 0.04);
}

.meta-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.meta-label {
  font-size: 0.78rem;
  font-weight: 600;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.meta-value {
  font-size: 0.95rem;
  font-weight: 700;
  color: #0f2d59;
}

.meta-code {
  font-family: monospace;
  color: #0284c7;
  font-size: 1.05rem;
}

.btn-refresh {
  min-height: 38px;
  padding: 0.4rem 0.9rem;
  font-size: 0.85rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #0f2d59;
  cursor: pointer;
}

.btn-refresh:hover:not(:disabled) {
  background: #f1f5f9;
}

.verify-form, .prescription-details {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  padding: 1.75rem;
  margin: 1.25rem 0;
  box-shadow: 0 4px 14px rgba(15, 45, 89, 0.05);
}

.verify-form h2 {
  color: #0f2d59;
  font-size: 1.2rem;
  font-weight: 700;
  margin: 0 0 0.4rem;
}

.verify-desc {
  color: #64748b;
  font-size: 0.88rem;
  line-height: 1.5;
  margin: 0 0 1.25rem;
}

label {
  display: block;
  font-weight: 600;
  color: #334155;
  font-size: 0.88rem;
  margin-bottom: 0.35rem;
}

input {
  display: block;
  width: 100%;
  box-sizing: border-box;
  min-height: 44px;
  padding: 0.65rem 0.95rem;
  font-size: 1.1rem;
  letter-spacing: 0.2em;
  color: #1e293b;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  margin: 0.4rem 0 1.25rem;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
  border-color: #0284c7;
}

.btn-primary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 46px;
  padding: 0.75rem 1.5rem;
  font-size: 0.95rem;
  font-weight: 700;
  border-radius: 10px;
  border: none;
  background: linear-gradient(135deg, #0284c7, #0f2d59);
  color: #ffffff;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(2, 132, 199, 0.2);
}

.btn-primary:hover:not(:disabled) {
  opacity: 0.94;
}

/* Patient banner */
.patient-banner {
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  border-radius: 12px;
  padding: 1.25rem;
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.patient-info {
  display: flex;
  flex-wrap: wrap;
  gap: 1.5rem;
}

.info-row {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.925rem;
}

.info-row .label {
  color: #0369a1;
  font-weight: 600;
}

.diagnosis-block {
  display: flex;
  gap: 0.4rem;
  font-size: 0.925rem;
  border-top: 1px solid #e0f2fe;
  padding-top: 0.5rem;
}

.diagnosis-block .label {
  color: #0369a1;
  font-weight: 600;
}

.diagnosis-text {
  color: #0c4a6e;
  font-weight: 600;
}

/* Table */
.table-wrap {
  overflow-x: auto;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  margin-bottom: 1.5rem;
}

table {
  width: 100%;
  border-collapse: collapse;
}

td, th {
  padding: 0.85rem 1.15rem;
  text-align: left;
  border-bottom: 1px solid #e2e8f0;
}

th {
  background: #f8fafc;
  color: #0f2d59;
  font-weight: 700;
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

tr:last-child td {
  border-bottom: none;
}

.medicine-name {
  font-weight: 700;
  color: #0f2d59;
}

.medicine-strength {
  color: #0284c7;
  font-weight: 600;
}

.usage-text {
  color: #334155;
  font-size: 0.9rem;
}

.treatment-days {
  color: #64748b;
  font-size: 0.8rem;
  margin-top: 0.2rem;
}

.doctor-notes {
  background: #fffbeb;
  border: 1px solid #fef3c7;
  border-radius: 12px;
  padding: 1rem 1.25rem;
  margin-bottom: 1.5rem;
}

.note-item {
  color: #92400e;
  font-size: 0.9rem;
  line-height: 1.5;
  margin: 0.35rem 0;
}

.details-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-top: 1rem;
  border-top: 1px solid #f1f5f9;
}

.session-expiry {
  font-size: 0.85rem;
  color: #64748b;
}

.session-expiry strong {
  color: #0f2d59;
}

.btn-close {
  min-height: 38px;
  padding: 0.45rem 1rem;
  font-size: 0.88rem;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #475569;
  cursor: pointer;
}

.btn-close:hover {
  background: #f1f5f9;
  color: #0f2d59;
}

.alert-error {
  color: #991b1b;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-weight: 500;
  font-size: 0.88rem;
  margin: 0.75rem 0;
}

.alert-status {
  color: #0369a1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-weight: 500;
  font-size: 0.88rem;
  margin: 0.75rem 0;
}

@media (max-width: 640px) {
  .public-rx {
    padding: 1.25rem 0.75rem;
  }
  .rx-meta-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.75rem;
  }
  .btn-refresh {
    width: 100%;
  }
  .verify-form, .prescription-details {
    padding: 1.25rem 1rem;
  }
  .btn-primary {
    width: 100%;
  }
  .details-footer {
    flex-direction: column;
    gap: 0.75rem;
    align-items: flex-start;
  }
  .btn-close {
    width: 100%;
  }
}
</style>
