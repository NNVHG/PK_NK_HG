<template>
  <!-- §1.6/§6.2: TV chỉ hiển thị thông báo gọi đang hoạt động; không hiển thị danh sách chờ hoặc dữ liệu khám. -->
  <div class="tv-monitor">
    <header class="header">
      <div class="brand">
        <img src="/logo-ngang.svg" alt="Nha Khoa Hoàng Gia" class="tv-logo" width="158" height="42" />
        <div class="brand-text">
          <span class="subtitle">BẢNG GỌI SỐ VÀ TIẾP ĐÓN PHÒNG CHỜ</span>
        </div>
      </div>
      <div class="header-right">
        <div v-if="activeCall" class="call-badge">
          ⚡ Đang gọi: <strong>{{ remainingCallSeconds }}s</strong>
        </div>
        <button
          type="button"
          class="sound-toggle"
          :class="{ active: soundEnabled && !playbackBlocked }"
          :aria-pressed="soundEnabled && !playbackBlocked"
          @click="toggleSound"
        >
          {{ playbackBlocked ? '🔓 Mở khóa loa TV' : (soundEnabled ? '🔊 Âm thanh: BẬT' : '🔇 Bật âm thanh') }}
        </button>
        <button
          v-if="soundEnabled && !playbackBlocked"
          type="button"
          class="sound-toggle"
          :disabled="isPlayingAudio || !!activeCall"
          @click="previewBell"
        >
          Nghe thử chuông
        </button>
        <div class="clock">{{ currentTime }}</div>
        <small v-if="soundEnabled && (playbackBlocked || voiceUnavailable)" class="sound-status" role="status">
          {{
            playbackBlocked
              ? 'Trình duyệt đang chặn tự phát âm thanh. Bấm Mở khóa loa TV để bật âm thanh.'
              : 'Giọng đọc Viettel AI hiện không khả dụng. Xem số thứ tự và phòng khi nghe chuông.'
          }}
        </small>
      </div>
    </header>

    <div v-if="error" class="error-banner" role="alert">
      {{ error }}
    </div>

    <!-- MÀN HÌNH NỔI BẬT KHI ĐANG GỌI SỐ (Hiển thị 15 giây) -->
    <section v-if="activeCall" class="call-active-banner" aria-live="assertive">
      <div class="call-banner-top">
        <span class="call-pulse-dot"></span> XIN MỜI KHÁCH HÀNG VÀO PHÒNG KHÁM
      </div>
      <div class="call-banner-number">{{ activeCall.queueNumberDisplay }}</div>
      <div class="call-banner-name">{{ formatCallPatientName(activeCall.maskedPatientName) }}</div>
      <div class="call-banner-meta">
        <span class="banner-pill">🏢 {{ locationLabel(activeCall) }}</span>
      </div>
      <div class="call-progress-track">
        <div class="call-progress-fill" :style="{ width: ((remainingCallSeconds / CALL_DURATION_SECONDS) * 100) + '%' }"></div>
      </div>
      <p class="call-guide-tip">
        Quý khách vui lòng mang theo sổ/hồ sơ khám di chuyển đến phòng khám trên.
      </p>
    </section>

    <!-- MÀN HÌNH CHỜ TIẾP ĐÓN (STANDBY KHI KHÔNG CÓ LƯỢT GỌI) -->
    <main v-else class="standby-screen">
      <div class="standby-welcome">
        <div class="welcome-tag">HỆ THỐNG Y TẾ NHA KHOA KỸ THUẬT CAO</div>
        <h2 class="welcome-heading">Chào Mừng Quý Khách Đến Khám & Chăm Sóc Răng Miệng</h2>
        <p class="welcome-desc">
          Vui lòng chờ tại sảnh tiếp đón. Khi Nha sĩ gọi, màn hình sẽ hiển thị tên, số thứ tự và phòng.
        </p>
      </div>

      <div class="tips-grid">
        <div class="tip-card">
          <div class="tip-icon">🪪</div>
          <div class="tip-content">
            <h3>Chuẩn bị Căn cước công dân</h3>
            <p>Xuất trình thẻ CCCD hoặc mã định danh VNeID tại quầy lễ tân để đối soát nhanh thông tin hồ sơ.</p>
          </div>
        </div>

        <div class="tip-card">
          <div class="tip-icon">📢</div>
          <div class="tip-content">
            <h3>Lắng nghe gọi tên & số thứ tự</h3>
            <p>Khi nghe đọc tên hoặc nghe chuông, vui lòng xem số thứ tự và phòng trên màn hình.</p>
          </div>
        </div>

        <div class="tip-card">
          <div class="tip-icon">🦷</div>
          <div class="tip-content">
            <h3>Báo tiền sử bệnh lý</h3>
            <p>Vui lòng thông báo với Bác sĩ tiền sử dị ứng thuốc tê, tim mạch, huyết áp hoặc tiểu đường trước khi làm thủ thuật.</p>
          </div>
        </div>

        <div class="tip-card">
          <div class="tip-icon">☕</div>
          <div class="tip-content">
            <h3>Tiện ích sảnh phòng chờ</h3>
            <p>Khu vực phòng chờ phục vụ nước uống tinh khiết, wifi tốc độ cao và tài liệu chăm sóc răng miệng miễn phí.</p>
          </div>
        </div>
      </div>

      <div class="standby-footer-strip">
        <div class="strip-item">
          <span class="strip-dot"></span>
          <span>Phòng khám mở cửa: <strong>08:00 - 20:00</strong> hằng ngày</span>
        </div>
        <div class="strip-item">
          <span>Hỗ trợ tiếp đón: <strong>Quầy Lễ Tân tầng 1</strong></span>
        </div>
        <div class="strip-item">
          <span>Hotline hỗ trợ: <strong>1900 8686</strong></span>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue';
import { queueService, type PublicQueueItemResponse } from '@/services/queue';

const SOUND_STORAGE_KEY = 'dental.tv.sound';
const CALL_DURATION_SECONDS = 15;
const CALL_TTL_MS = CALL_DURATION_SECONDS * 1000;
const BELL_DURATION_MS = 1730;

interface TimedCall {
  key: string;
  item: PublicQueueItemResponse;
  deadline: number;
}

const error = ref('');
const currentTime = ref('');
const soundEnabled = ref(true);
const playbackBlocked = ref(false);
const voiceUnavailable = ref(false);
const activeCall = ref<PublicQueueItemResponse | null>(null);
const remainingCallSeconds = ref(0);
const isPlayingAudio = ref(false);

const seenCalls = new Map<string, number>();
const audioQueue: TimedCall[] = [];
let currentCalls: TimedCall[] = [];
let playingAudio: TimedCall | null = null;
let audioContext: AudioContext | null = null;
let cancelAudioStage: (() => void) | null = null;
let displayTimer: ReturnType<typeof setInterval> | null = null;
let clockTimer: ReturnType<typeof setInterval> | null = null;
let pollTimer: ReturnType<typeof setInterval> | null = null;
let publicRequest: AbortController | null = null;
let initialSnapshotLoaded = false;
let mounted = false;

function formatCallPatientName(name: string | null | undefined): string {
  const normalized = name?.normalize('NFC').trim().replace(/\s+/g, ' ').replace(/\s+\(DEMO\)$/i, '').trim();
  return normalized || 'Bệnh nhân';
}

function locationLabel(item: PublicQueueItemResponse): string {
  return item.roomName || 'Vui lòng hỏi quầy tiếp đón để được hướng dẫn';
}

function updateClock() {
  currentTime.value = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(new Date());
}

function saveSoundPreference() {
  try {
    if (soundEnabled.value) window.sessionStorage.removeItem(SOUND_STORAGE_KEY);
    else window.sessionStorage.setItem(SOUND_STORAGE_KEY, 'false');
  } catch {}
}

function ensureAudioContext(): AudioContext | null {
  if (!audioContext || audioContext.state === 'closed') {
    const AudioContextClass =
      window.AudioContext ||
      (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (AudioContextClass) audioContext = new AudioContextClass();
  }
  return audioContext;
}

async function unlockAudio() {
  const context = ensureAudioContext();
  if (context?.state !== 'running' && context) {
    playbackBlocked.value = true;
    try {
      await context.resume();
    } catch {
      return;
    }
    if (!mounted || !soundEnabled.value || (context.state as AudioContextState) !== 'running') return;
  }
  playbackBlocked.value = false;
  processAudioQueue();
}

function playTone(frequency: number, duration: number, delay: number) {
  const context = ensureAudioContext();
  if (!context || context.state !== 'running') return () => {};
  const start = context.currentTime + delay;
  const oscillator = context.createOscillator();
  const gain = context.createGain();
  oscillator.frequency.setValueAtTime(frequency, start);
  gain.gain.setValueAtTime(0.3, start);
  gain.gain.exponentialRampToValueAtTime(0.001, start + duration);
  oscillator.connect(gain);
  gain.connect(context.destination);
  function disconnect() {
    oscillator.onended = null;
    oscillator.disconnect();
    gain.disconnect();
  }
  oscillator.onended = disconnect;
  oscillator.start(start);
  oscillator.stop(start + duration);
  return disconnect;
}

function playDingDong() {
  const stop = [playTone(800, 0.25, 0), playTone(600, 0.45, 0.25)];
  return () => stop.forEach(disconnect => disconnect());
}

function playAttentionBell(onComplete: () => void) {
  const stop = [
    playTone(880, 0.28, 0), playTone(659.25, 0.48, 0.28),
    playTone(880, 0.28, 0.9), playTone(659.25, 0.55, 1.18),
  ];
  const completion = setTimeout(onComplete, BELL_DURATION_MS);
  cancelAudioStage = () => {
    clearTimeout(completion);
    stop.forEach(disconnect => disconnect());
  };
}

function syncCallBanner(now = Date.now()) {
  const playingCall = playingAudio;
  const selected = playingCall && playingCall.deadline > now
    ? playingCall
    : currentCalls.find(call => call.deadline > now);
  activeCall.value = selected?.item ?? null;
  remainingCallSeconds.value = selected ? Math.ceil((selected.deadline - now) / 1000) : 0;
}

function stopActiveAudio() {
  playingAudio = null;
  isPlayingAudio.value = false;
  cancelAudioStage?.();
  cancelAudioStage = null;
}

function canPlay(item: TimedCall, now = Date.now()) {
  return item.deadline > now && currentCalls.some(call => call.key === item.key);
}

function refreshCalls(now = Date.now()) {
  currentCalls = currentCalls.filter(call => call.deadline > now);
  for (const [key, deadline] of seenCalls) {
    if (deadline <= now) seenCalls.delete(key);
  }
  for (let i = audioQueue.length - 1; i >= 0; i--) {
    if (!canPlay(audioQueue[i], now)) audioQueue.splice(i, 1);
  }
  if (playingAudio && !canPlay(playingAudio, now)) stopActiveAudio();
  syncCallBanner(now);
  processAudioQueue();
}

function processAudioQueue() {
  if (!mounted || !soundEnabled.value || playbackBlocked.value || isPlayingAudio.value) return;
  const item = audioQueue.shift();
  if (!item || !canPlay(item)) return;
  const context = ensureAudioContext();
  if (!context) {
    voiceUnavailable.value = true;
    return;
  }
  if (context.state !== 'running') {
    audioQueue.unshift(item);
    playbackBlocked.value = true;
    return;
  }

  voiceUnavailable.value = false;
  isPlayingAudio.value = true;
  playingAudio = item;
  syncCallBanner();
  void playViettelCall(context, item);
}

async function playViettelCall(context: AudioContext, item: TimedCall) {
  const request = new AbortController();
  cancelAudioStage = () => request.abort();
  function stillPlaying() {
    return playingAudio === item && canPlay(item);
  }

  function finish() {
    if (playingAudio !== item) return;
    stopActiveAudio();
    syncCallBanner();
    processAudioQueue();
  }

  try {
    const audio = await queueService.getPublicCallAudio(item.item.queueNumberDisplay, item.item.expiresAtUtc, request.signal);
    if (!stillPlaying()) return;
    const buffer = await context.decodeAudioData(audio);
    if (!stillPlaying()) return;

    const source = context.createBufferSource();
    source.buffer = buffer;
    source.onended = finish;
    source.connect(context.destination);
    cancelAudioStage = () => {
      source.onended = null;
      source.disconnect();
    };
    source.start(context.currentTime + 0.76);
    const stopChime = playDingDong();
    cancelAudioStage = () => {
      stopChime();
      source.onended = null;
      source.stop();
      source.disconnect();
    };
  } catch {
    if (!stillPlaying()) return;
    cancelAudioStage?.();
    voiceUnavailable.value = true;
    playAttentionBell(finish);
  }
}

function previewBell() {
  if (!soundEnabled.value || playbackBlocked.value || activeCall.value || isPlayingAudio.value) return;
  isPlayingAudio.value = true;
  playAttentionBell(() => {
    stopActiveAudio();
    processAudioQueue();
  });
}

function toggleSound() {
  soundEnabled.value = playbackBlocked.value || !soundEnabled.value;
  saveSoundPreference();
  if (soundEnabled.value) {
    void unlockAudio();
  } else {
    audioQueue.length = 0;
    stopActiveAudio();
    syncCallBanner();
  }
}

function applyPublicSnapshot(items: PublicQueueItemResponse[]) {
  const now = Date.now();
  const firstSnapshot = !initialSnapshotLoaded;
  initialSnapshotLoaded = true;
  currentCalls = [];
  for (const item of items) {
    const expiresAt = Date.parse(item.expiresAtUtc);
    if (item.status !== 6 || !Number.isFinite(expiresAt) || expiresAt <= now) continue;

    const key = item.queueNumberDisplay + '/' + item.expiresAtUtc;
    const previousDeadline = seenCalls.get(key);
    const deadline = previousDeadline ?? Math.min(expiresAt, now + CALL_TTL_MS);
    if (deadline <= now) continue;

    const call = { key, item, deadline };
    currentCalls.push(call);
    if (previousDeadline === undefined && soundEnabled.value && !firstSnapshot) {
      audioQueue.push(call);
    }
    seenCalls.set(key, deadline);
  }
  currentCalls.sort((left, right) => Date.parse(right.item.expiresAtUtc) - Date.parse(left.item.expiresAtUtc));
  audioQueue.sort((left, right) => left.deadline - right.deadline);
  refreshCalls(now);
}

async function loadQueueData() {
  if (!mounted || publicRequest) return;
  const request = new AbortController();
  publicRequest = request;
  try {
    const data = await queueService.getPublicDisplay(request.signal);
    if (request.signal.aborted) return;
    error.value = '';
    applyPublicSnapshot(data.inConsultation);
  } catch {
    if (!request.signal.aborted) error.value = 'Mất kết nối máy chủ. Đang tự động kết nối lại...';
  } finally {
    if (publicRequest === request) publicRequest = null;
  }
}

onMounted(() => {
  mounted = true;
  updateClock();
  clockTimer = setInterval(updateClock, 1000);
  displayTimer = setInterval(refreshCalls, 200);
  try {
    soundEnabled.value = window.sessionStorage.getItem(SOUND_STORAGE_KEY) !== 'false';
  } catch {}
  if (soundEnabled.value) void unlockAudio();
  void loadQueueData();
  pollTimer = setInterval(loadQueueData, 2000);
});

onUnmounted(() => {
  mounted = false;
  publicRequest?.abort();
  if (clockTimer !== null) clearInterval(clockTimer);
  if (displayTimer !== null) clearInterval(displayTimer);
  if (pollTimer !== null) clearInterval(pollTimer);
  stopActiveAudio();
  currentCalls = [];
  seenCalls.clear();
  audioQueue.length = 0;
  activeCall.value = null;
  if (audioContext && audioContext.state !== 'closed') void audioContext.close().catch(() => {});
});
</script>

<style scoped>
.tv-monitor {
  min-height: 100vh;
  background: linear-gradient(180deg, #f0f7ff 0%, #f8fafc 100%);
  color: #1e293b;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  padding: 1.5rem 2rem;
  box-sizing: border-box;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  padding: 1rem 1.75rem;
  box-shadow: 0 4px 16px rgba(15, 45, 89, 0.05);
  margin-bottom: 2rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 1.25rem;
}

.tv-logo {
  flex-shrink: 0;
  display: block;
  width: 158px;
  height: 42px;
  object-fit: contain;
}

.brand h1 {
  margin: 0;
  font-size: 1.85rem;
  font-weight: 800;
  letter-spacing: 0.5px;
  color: #0f2d59;
}

.subtitle {
  font-size: 0.95rem;
  font-weight: 600;
  color: #0284c7;
  letter-spacing: 0.5px;
}

.header-right {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 1.25rem;
}

.sound-status {
  flex-basis: 100%;
}

.call-badge {
  background: #ea580c;
  color: #ffffff;
  font-weight: 700;
  padding: 0.5rem 1.1rem;
  border-radius: 999px;
  font-size: 0.95rem;
  box-shadow: 0 2px 8px rgba(234, 88, 12, 0.35);
  animation: pulse 1s infinite alternate;
}

/* Call Banner 15s nổi bật cực đại */
.call-active-banner {
  background: linear-gradient(135deg, #0284c7 0%, #0f2d59 100%);
  border: 3px solid #38bdf8;
  border-radius: 24px;
  padding: 3rem 2rem;
  margin-bottom: 2rem;
  text-align: center;
  box-shadow: 0 16px 48px rgba(15, 45, 89, 0.25);
  color: #ffffff;
}
.call-banner-top {
  display: inline-flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 1.4rem;
  font-weight: 800;
  letter-spacing: 0.15em;
  color: #fde047;
  text-transform: uppercase;
  margin-bottom: 1rem;
  background: rgba(0, 0, 0, 0.25);
  padding: 0.4rem 1.5rem;
  border-radius: 999px;
}
.call-pulse-dot {
  width: 14px;
  height: 14px;
  background: #ef4444;
  border-radius: 50%;
  box-shadow: 0 0 12px #ef4444;
  animation: pulse 1s infinite;
}
.call-banner-number {
  font-size: clamp(5rem, 10vw, 8rem);
  font-weight: 900;
  line-height: 1;
  color: #ffffff;
  letter-spacing: 3px;
  text-shadow: 0 6px 20px rgba(0, 0, 0, 0.4);
  margin-bottom: 0.5rem;
}
.call-banner-name {
  font-size: clamp(2.2rem, 4vw, 3.25rem);
  font-weight: 800;
  color: #e0f2fe;
  margin-bottom: 1.75rem;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  line-height: 1.25;
  overflow-wrap: break-word;
}
.call-banner-meta {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 1.25rem;
}
.banner-pill {
  background: rgba(255, 255, 255, 0.15);
  backdrop-filter: blur(8px);
  border: 1.5px solid rgba(255, 255, 255, 0.35);
  border-radius: 999px;
  padding: 0.75rem 1.75rem;
  font-size: 1.35rem;
  font-weight: 700;
  color: #ffffff;
}
.dimmed-by-call {
  opacity: 0.85;
}

.sound-toggle {
  background: #f1f5f9;
  border: 1px solid #cbd5e1;
  color: #334155;
  padding: 0.6rem 1.2rem;
  border-radius: 10px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.sound-toggle:hover:not(:disabled) {
  background: #e2e8f0;
}

.sound-toggle:disabled {
  opacity: 0.6;
  cursor: default;
}

.sound-toggle.active {
  background: #0284c7;
  border-color: #0284c7;
  color: #ffffff;
}

.clock {
  font-size: 1.85rem;
  font-family: monospace;
  font-weight: 800;
  color: #0f2d59;
  background: #f1f5f9;
  padding: 0.35rem 1rem;
  border-radius: 10px;
  border: 1px solid #e2e8f0;
}

.error-banner {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #b91c1c;
  padding: 0.85rem 1.25rem;
  border-radius: 10px;
  margin-bottom: 1.5rem;
  text-align: center;
  font-weight: 600;
}

.call-progress-track {
  width: 100%;
  max-width: 650px;
  height: 10px;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  margin: 2rem auto 1rem;
  overflow: hidden;
}

.call-progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #38bdf8, #22c55e);
  transition: width 1s linear;
}

.call-guide-tip {
  margin: 0;
  font-size: 1.25rem;
  color: #e0f2fe;
  font-weight: 500;
}

/* MÀN HÌNH CHỜ STANDBY SÁNG SỦA, SANG TRỌNG */
.standby-screen {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  max-width: 1240px;
  margin: 0 auto;
  width: 100%;
}

.standby-welcome {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 24px;
  padding: 3rem 2.5rem;
  text-align: center;
  box-shadow: 0 6px 24px rgba(15, 45, 89, 0.05);
}

.welcome-tag {
  display: inline-block;
  background: #e0f2fe;
  border: 1px solid #bae6fd;
  color: #0369a1;
  font-size: 0.95rem;
  font-weight: 800;
  letter-spacing: 2px;
  padding: 0.45rem 1.5rem;
  border-radius: 999px;
  margin-bottom: 1.25rem;
}

.welcome-heading {
  margin: 0 0 1rem;
  font-size: 2.5rem;
  font-weight: 900;
  color: #0f2d59;
}

.welcome-desc {
  margin: 0 auto;
  max-width: 820px;
  font-size: 1.25rem;
  color: #475569;
  line-height: 1.6;
}

.tips-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.75rem;
}

.tip-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 20px;
  padding: 1.75rem 2rem;
  display: flex;
  gap: 1.5rem;
  align-items: flex-start;
  box-shadow: 0 4px 16px rgba(15, 45, 89, 0.03);
  transition: transform 0.2s, border-color 0.2s, box-shadow 0.2s;
}

.tip-card:hover {
  transform: translateY(-3px);
  border-color: #0284c7;
  box-shadow: 0 8px 24px rgba(2, 132, 199, 0.12);
}

.tip-icon {
  font-size: 2.75rem;
  line-height: 1;
  background: #f0f9ff;
  border: 1px solid #bae6fd;
  padding: 0.85rem;
  border-radius: 16px;
  flex-shrink: 0;
}

.tip-content h3 {
  margin: 0 0 0.5rem;
  font-size: 1.35rem;
  font-weight: 800;
  color: #0f2d59;
}

.tip-content p {
  margin: 0;
  font-size: 1.1rem;
  color: #475569;
  line-height: 1.6;
}

.standby-footer-strip {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  padding: 1.25rem 2.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 1.15rem;
  color: #334155;
  box-shadow: 0 2px 10px rgba(15, 45, 89, 0.04);
}

.strip-item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.strip-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #16a34a;
  box-shadow: 0 0 8px rgba(22, 163, 74, 0.4);
}

@media (max-width: 900px) {
  .tips-grid {
    grid-template-columns: 1fr;
  }
  .standby-footer-strip {
    flex-direction: column;
    gap: 0.75rem;
    text-align: center;
  }
}

@media (max-width: 600px) {
  .tv-monitor {
    padding: 0.75rem;
  }
  .header {
    flex-wrap: wrap;
    gap: 0.75rem;
  }
  .brand {
    gap: 0.75rem;
  }
  .brand h1 {
    font-size: 1.35rem;
    letter-spacing: 0;
  }
  .subtitle {
    font-size: 0.8rem;
  }
  .header-right {
    width: 100%;
    justify-content: space-between;
    gap: 0.5rem;
  }
  .clock {
    font-size: 1.35rem;
  }
  .sound-toggle {
    padding: 0.4rem 0.8rem;
    font-size: 0.85rem;
  }
  .call-active-banner {
    padding: 1.5rem 1rem;
  }
  .call-banner-top {
    font-size: 1rem;
    letter-spacing: 0.08em;
    padding: 0.35rem 1rem;
  }
  .call-banner-number {
    font-size: clamp(3rem, 14vw, 4.5rem);
  }
  .call-banner-name {
    font-size: clamp(1.5rem, 7vw, 2.2rem);
    margin-bottom: 1.15rem;
  }
  .banner-pill {
    font-size: 1rem;
    padding: 0.55rem 1.1rem;
  }
  .call-guide-tip {
    font-size: 1rem;
  }
  .current-section, .waiting-section {
    padding: 1rem;
  }
  .current-card {
    padding: 1rem;
  }
  .number-tag {
    font-size: 3rem;
  }
  .patient-name {
    font-size: 1.5rem;
    overflow-wrap: break-word;
  }
  .dentist-info {
    font-size: 1rem;
    overflow-wrap: break-word;
  }
  .location-info {
    font-size: 1.15rem;
  }
  .waiting-item {
    flex-wrap: wrap;
    gap: 0.4rem;
    padding: 0.75rem;
  }
  .wait-number {
    width: auto;
    margin-right: 0.5rem;
    font-size: 1.2rem;
  }
  .wait-name {
    font-size: 1.05rem;
    overflow-wrap: break-word;
  }
}
</style>
