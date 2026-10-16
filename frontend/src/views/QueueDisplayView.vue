<template>
  <div class="tv-monitor">
    <header class="header">
      <div class="brand">
        <h1>NHA KHOA HỒNG GIA</h1>
        <span class="subtitle">BẢNG GỌI SỐ VÀ TIẾP ĐÓN PHÒNG CHỜ</span>
      </div>
      <div class="header-right">
        <button
          type="button"
          class="sound-toggle"
          :class="{ active: soundEnabled }"
          @click="toggleSound"
        >
          {{ soundEnabled ? '🔊 Âm thanh: BẬT' : '🔇 Âm thanh: TẮT (Click để bật)' }}
        </button>
        <div class="clock">{{ currentTime }}</div>
      </div>
    </header>

    <div v-if="error" class="error-banner" role="alert">
      {{ error }}
    </div>

    <main class="content-grid">
      <section class="current-section">
        <h2 class="section-title">ĐANG KHÁM / MỜI VÀO PHÒNG</h2>
        <div v-if="inConsultation.length === 0" class="empty-current">
          <p>Hiện không có ca đang khám</p>
        </div>
        <div v-else class="current-cards">
          <article
            v-for="item in inConsultation"
            :key="item.queueEntryId"
            class="current-card"
          >
            <div class="number-tag">{{ item.queueNumberDisplay }}</div>
            <div class="patient-name">{{ item.maskedPatientName }}</div>
            <div class="dentist-info">
              <span class="label">Bác sĩ phụ trách:</span>
              <strong class="value">{{ item.dentistName || 'Phòng khám tổng quát' }}</strong>
            </div>
            <div class="status-badge" :class="{ pulse: item.status === 6 }">{{ item.statusText }}</div>
          </article>
        </div>
      </section>

      <section class="waiting-section">
        <h2 class="section-title">
          DANH SÁCH CHỜ TIẾP THEO
          <span class="count-badge">({{ waiting.length }})</span>
        </h2>
        <div v-if="waiting.length === 0" class="empty-waiting">
          <p>Không có bệnh nhân đang chờ</p>
        </div>
        <ol v-else class="waiting-list">
          <li
            v-for="item in waiting"
            :key="item.queueEntryId"
            class="waiting-item"
            :class="{ priority: item.isPriority }"
          >
            <span class="wait-number">{{ item.queueNumberDisplay }}</span>
            <span class="wait-name">{{ item.maskedPatientName }}</span>
            <span v-if="item.isPriority" class="wait-priority">Ưu tiên</span>
            <span class="wait-status">{{ item.statusText }}</span>
          </li>
        </ol>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { queueService, type PublicQueueItemResponse } from '@/services/queue';

const inConsultation = ref<PublicQueueItemResponse[]>([]);
const waiting = ref<PublicQueueItemResponse[]>([]);
const error = ref('');
const currentTime = ref('');
const soundEnabled = ref(false);

interface AudioQueueItem {
  queueNumber: string;
  patientName: string;
  roomName: string;
}

const audioQueue: AudioQueueItem[] = [];
let isPlayingAudio = false;
const seenCallIds = new Set<number>();
let pollTimer: ReturnType<typeof setInterval> | null = null;
let clockTimer: ReturnType<typeof setInterval> | null = null;
let audioCtx: AudioContext | null = null;
function updateClock() {
  const now = new Date();
  currentTime.value = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(now);
}

function playTone(freq: number, duration: number, delay: number) {
  if (typeof window === 'undefined') return;
  if (!audioCtx) {
    const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (AudioContextClass) audioCtx = new AudioContextClass();
  }
  if (!audioCtx) return;

  const now = audioCtx.currentTime + delay;
  const osc = audioCtx.createOscillator();
  const gain = audioCtx.createGain();

  osc.type = 'sine';
  osc.frequency.setValueAtTime(freq, now);

  gain.gain.setValueAtTime(0.3, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + duration);

  osc.connect(gain);
  gain.connect(audioCtx.destination);

  osc.start(now);
  osc.stop(now + duration);
}

function playDingDong() {
  playTone(800, 0.25, 0);
  playTone(600, 0.45, 0.25);
}

function processAudioQueue() {
  if (!active || isPlayingAudio || audioQueue.length === 0 || !soundEnabled.value) return;

  const item = audioQueue.shift();
  if (!item) return;

  isPlayingAudio = true;
  playDingDong();

  if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
    const text = `Mời bệnh nhân số ${item.queueNumber}, ${item.patientName}, vào phòng khám ${item.roomName}`;
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'vi-VN';
    utterance.rate = 0.9;

    utterance.onend = () => {
      setTimeout(() => {
        isPlayingAudio = false;
        processAudioQueue();
      }, 1000);
    };

    utterance.onerror = () => {
      setTimeout(() => {
        isPlayingAudio = false;
        processAudioQueue();
      }, 500);
    };

    setTimeout(() => {
      if (active && soundEnabled.value) window.speechSynthesis.speak(utterance);
    }, 700);
  } else {
    setTimeout(() => {
      isPlayingAudio = false;
      processAudioQueue();
    }, 1500);
  }
}

function enqueueCallAudio(item: PublicQueueItemResponse) {
  if (audioQueue.length >= 3) {
    audioQueue.shift();
  }
  audioQueue.push({
    queueNumber: item.queueNumberDisplay,
    patientName: item.maskedPatientName,
    roomName: item.dentistName || 'bác sĩ'
  });
  processAudioQueue();
}

function toggleSound() {
  soundEnabled.value = !soundEnabled.value;
  if (soundEnabled.value) {
    playDingDong();
    processAudioQueue();
  } else {
    audioQueue.length = 0;
    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel();
    }
  }
}

let active = true, requestVersion = 0;
async function loadQueueData() {
  if (!active) return;
  const ticket = ++requestVersion;
  try {
    const data = await queueService.getPublicDisplay();
    if (!active || ticket !== requestVersion) return;
    error.value = '';

    const currentCalls = data.inConsultation.filter(item => item.status === 6);
    for (const item of currentCalls) {
      if (!seenCallIds.has(item.queueEntryId)) {
        if (soundEnabled.value) {
          enqueueCallAudio(item);
        }
      }
    }
    seenCallIds.clear();
    for (const item of currentCalls) seenCallIds.add(item.queueEntryId);

    inConsultation.value = data.inConsultation;
    waiting.value = data.waiting;
  } catch {
    if (active && ticket === requestVersion) error.value = 'Mất kết nối máy chủ. Đang tự động kết nối lại...';
  }
}

onMounted(() => {
  updateClock();
  clockTimer = setInterval(updateClock, 1000);
  void loadQueueData();
  pollTimer = setInterval(loadQueueData, 3000);
});

onUnmounted(() => {
  active = false; requestVersion++; seenCallIds.clear(); soundEnabled.value = false;
  if (clockTimer) clearInterval(clockTimer);
  if (pollTimer) clearInterval(pollTimer);
  if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
    window.speechSynthesis.cancel();
  }
  audioQueue.length = 0;
  if (audioCtx && audioCtx.state !== 'closed') {
    void audioCtx.close();
  }
});

</script>

<style scoped>
.tv-monitor {
  min-height: 100vh;
  background: #0f172a;
  color: #f8fafc;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  padding: 1.5rem;
  box-sizing: border-box;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 2px solid #334155;
  padding-bottom: 1rem;
  margin-bottom: 1.5rem;
}

.brand h1 {
  margin: 0;
  font-size: 2rem;
  font-weight: 800;
  letter-spacing: 1px;
  color: #38bdf8;
}

.subtitle {
  font-size: 0.95rem;
  color: #94a3b8;
  letter-spacing: 0.5px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.sound-toggle {
  background: #1e293b;
  border: 1px solid #475569;
  color: #e2e8f0;
  padding: 0.6rem 1.2rem;
  border-radius: 8px;
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s;
}

.sound-toggle.active {
  background: #0284c7;
  border-color: #38bdf8;
  color: #fff;
}

.clock {
  font-size: 2rem;
  font-family: monospace;
  font-weight: 700;
  color: #f1f5f9;
}

.error-banner {
  background: #b91c1c;
  color: #fff;
  padding: 0.75rem 1rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  text-align: center;
}

.content-grid {
  display: grid;
  grid-template-columns: 1.1fr 0.9fr;
  gap: 1.5rem;
}

.section-title {
  margin: 0 0 1rem;
  font-size: 1.25rem;
  font-weight: 700;
  color: #94a3b8;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.count-badge {
  color: #38bdf8;
}

.current-section, .waiting-section {
  background: #1e293b;
  border-radius: 12px;
  padding: 1.5rem;
  border: 1px solid #334155;
}

.current-cards {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.current-card {
  background: #0f172a;
  border: 2px solid #0284c7;
  border-radius: 12px;
  padding: 1.5rem;
  text-align: center;
}

.number-tag {
  font-size: 4.5rem;
  font-weight: 900;
  color: #38bdf8;
  letter-spacing: 2px;
  line-height: 1;
  margin-bottom: 0.75rem;
}

.patient-name {
  font-size: 2.25rem;
  font-weight: 700;
  color: #f8fafc;
  margin-bottom: 0.75rem;
}

.dentist-info {
  font-size: 1.2rem;
  color: #cbd5e1;
  margin-bottom: 1rem;
}

.dentist-info .label {
  color: #94a3b8;
  margin-right: 0.5rem;
}

.dentist-info .value {
  color: #f1f5f9;
}

.status-badge {
  display: inline-block;
  padding: 0.4rem 1.25rem;
  background: #0369a1;
  color: #fff;
  border-radius: 20px;
  font-weight: 600;
  font-size: 1rem;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

.pulse {
  animation: pulse 2s infinite ease-in-out;
}

.waiting-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.waiting-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #0f172a;
  padding: 0.9rem 1.25rem;
  border-radius: 8px;
  border-left: 4px solid #475569;
}

.waiting-item.priority {
  border-left-color: #10b981;
}

.wait-number {
  font-size: 1.5rem;
  font-weight: 700;
  color: #38bdf8;
  width: 90px;
}

.wait-name {
  font-size: 1.25rem;
  font-weight: 600;
  color: #f1f5f9;
  flex: 1;
}

.wait-priority {
  background: #065f46;
  color: #6ee7b7;
  padding: 0.2rem 0.6rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-right: 1rem;
}

.wait-status {
  color: #94a3b8;
  font-size: 1rem;
}

.empty-current, .empty-waiting {
  padding: 3rem 1rem;
  text-align: center;
  color: #64748b;
  font-size: 1.2rem;
}

@media (max-width: 900px) {
  .content-grid {
    grid-template-columns: 1fr;
  }
}
</style>
