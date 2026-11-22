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
          @click="previewVoice"
        >
          Nghe thử giọng
        </button>
        <div class="clock">{{ currentTime }}</div>
        <small v-if="soundEnabled && (playbackBlocked || viettelTtsExpiredOrUnavailable)" class="sound-status" role="status">
          {{
            playbackBlocked
              ? 'Trình duyệt đang chặn tự phát âm thanh. Bấm Mở khóa loa TV để bật tiếng đọc mời bệnh nhân.'
              : 'Dịch vụ đọc giọng Viettel AI tạm hết hạn hoặc gián đoạn — đang phát chuông Bing-Bong báo hiệu khách hàng chú ý màn hình.'
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
          Vui lòng thư giãn tại sảnh tiếp đón. Loa sẽ đọc tên người bệnh và màn hình hiển thị số thứ tự cùng phòng khi Nha sĩ gọi.
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
            <p>Hệ thống phát âm thanh chuông báo, đọc tên và hiển thị số thứ tự cùng phòng trong 15 giây sau khi được gọi.</p>
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
const VIETTEL_CACHE_LIMIT = 20;

const inConsultation = ref<PublicQueueItemResponse[]>([]);
const waiting = ref<PublicQueueItemResponse[]>([]);
const error = ref('');
const currentTime = ref('');
const soundEnabled = ref(true);
const playbackBlocked = ref(false);
const viettelTtsExpiredOrUnavailable = ref(false);

interface ActiveCallData {
  queueNumberDisplay: string;
  maskedPatientName: string;
  roomName: string | null;
  status: number;
}

interface AudioQueueItem {
  text: string;
  expiresAt: number;
}

const activeCall = ref<ActiveCallData | null>(null);
const remainingCallSeconds = ref(CALL_DURATION_SECONDS);
let callTimer: ReturnType<typeof setInterval> | null = null;
let broadcastChannel: BroadcastChannel | null = null;

function formatCallPatientName(name: string | null | undefined): string {
  const normalized = name?.trim().replace(/\s+/g, ' ') ?? '';
  if (!normalized) return 'Bệnh nhân';
  const withoutDemo = normalized.endsWith(' (DEMO)')
    ? normalized.slice(0, -' (DEMO)'.length).trim()
    : normalized;
  return withoutDemo || 'Bệnh nhân';
}

function triggerCallBanner(data: ActiveCallData) {
  activeCall.value = {
    ...data,
    maskedPatientName: formatCallPatientName(data.maskedPatientName)
  };
  remainingCallSeconds.value = CALL_DURATION_SECONDS;
  if (callTimer) clearInterval(callTimer);

  callTimer = setInterval(() => {
    remainingCallSeconds.value--;
    if (remainingCallSeconds.value <= 0) {
      if (callTimer) clearInterval(callTimer);
      callTimer = null;
      activeCall.value = null;
    }
  }, 1000);
}

function locationLabel(item: { roomName?: string | null }): string {
  return item.roomName || 'Vui lòng hỏi quầy tiếp đón để được hướng dẫn';
}

function formatSpokenQueueNumber(queueNumberDisplay: string | null | undefined): string {
  const normalized = queueNumberDisplay?.trim() ?? '';
  if (!normalized) return '';
  return normalized.replace(/[-_]+/g, ' ').replace(/\s+/g, ' ').trim();
}

function formatSpokenRoomClause(roomName: string | null | undefined): string {
  const normalized = roomName?.trim().replace(/\s+/g, ' ') ?? '';
  if (!normalized) return '';
  if (/^phòng\s+số\s+/i.test(normalized)) {
    const roomNumber = normalized.replace(/^phòng\s+số\s+/i, '').replace(/[-_]+/g, ' ').trim();
    return roomNumber ? `vào phòng số ${roomNumber}` : '';
  }
  const directNumberMatch = normalized.match(/^phòng\s+(\d+[A-Za-z0-9]*)$/i);
  if (directNumberMatch) {
    return `vào phòng số ${directNumberMatch[1]}`;
  }
  const namedRoomNumberMatch = normalized.match(/^phòng\s+(.+?)\s+(\d+[A-Za-z0-9]*)$/i);
  if (namedRoomNumberMatch) {
    const prefix = namedRoomNumberMatch[1].replace(/\s+số$/i, '').trim();
    return `vào phòng ${prefix} số ${namedRoomNumberMatch[2]}`;
  }
  if (/^phòng\b/i.test(normalized)) {
    return `vào ${normalized}`;
  }
  return `vào phòng số ${normalized.replace(/[-_]+/g, ' ').trim()}`;
}

function buildCallAnnouncementText(
  item: Pick<PublicQueueItemResponse, 'maskedPatientName'> & {
    queueNumberDisplay?: string | null;
    roomName?: string | null;
  }
): string {
  // Chuẩn hóa họ tên NFC, giữ dấu tiếng Việt và bỏ các từ bị che ký tự '*' nếu có.
  const rawName = formatCallPatientName(item.maskedPatientName)
    .normalize('NFC')
    .split(' ')
    .filter(part => part && !part.includes('*'))
    .join(' ');
  const lowercaseName = rawName.toLocaleLowerCase('vi-VN');
  const hasSpecificName = Boolean(rawName) && lowercaseName !== 'bệnh nhân';
  const spokenName = hasSpecificName
    ? (rawName === rawName.toLocaleUpperCase('vi-VN')
        ? lowercaseName.replace(/(^|[\s-])\p{L}/gu, part => part.toLocaleUpperCase('vi-VN'))
        : rawName)
    : '';

  const spokenNumber = formatSpokenQueueNumber(item.queueNumberDisplay);
  const roomClause = formatSpokenRoomClause(item.roomName);

  if (spokenNumber && hasSpecificName) {
    const lead = `Mời bệnh nhân số ${spokenNumber}, ${spokenName}`;
    return roomClause ? `${lead}, ${roomClause}.` : `${lead}.`;
  }
  if (spokenNumber) {
    const lead = `Mời bệnh nhân số ${spokenNumber}`;
    return roomClause ? `${lead}, ${roomClause}.` : `${lead}.`;
  }
  if (hasSpecificName) {
    const lead = `Mời bệnh nhân ${spokenName}`;
    return roomClause ? `${lead}, ${roomClause}.` : `${lead}.`;
  }
  return roomClause ? `Mời bệnh nhân ${roomClause}.` : 'Mời bệnh nhân.';
}

const audioQueue: AudioQueueItem[] = [];
const isPlayingAudio = ref(false);
const seenCallNumbers = new Set<string>();
const viettelAudioCache = new Map<string, ArrayBuffer>();
let pollTimer: ReturnType<typeof setInterval> | null = null;
let clockTimer: ReturnType<typeof setInterval> | null = null;
let audioCtx: AudioContext | null = null;
let activeAudioSource: AudioBufferSourceNode | null = null;
let activeFetchController: AbortController | null = null;
let speechTimer: ReturnType<typeof setTimeout> | null = null;
let speechSessionId = 0;
let initialSnapshotLoaded = false;

function updateClock() {
  const now = new Date();
  currentTime.value = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(now);
}

function getSessionStorage(): Storage | null {
  try {
    return typeof window !== 'undefined' && window.sessionStorage ? window.sessionStorage : null;
  } catch {
    return null;
  }
}

function syncSoundPreference() {
  const saved = getSessionStorage()?.getItem(SOUND_STORAGE_KEY);
  soundEnabled.value = saved !== 'false';
}

function saveSoundPreference(enabled: boolean) {
  const storage = getSessionStorage();
  if (!storage) return;
  try {
    if (enabled) storage.removeItem(SOUND_STORAGE_KEY);
    else storage.setItem(SOUND_STORAGE_KEY, 'false');
  } catch {}
}

function ensureAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  if (!audioCtx || audioCtx.state === 'closed') {
    const AudioContextClass =
      window.AudioContext ||
      (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (AudioContextClass) audioCtx = new AudioContextClass();
  }
  return audioCtx;
}

async function tryResumeAudioContext(): Promise<boolean> {
  const ctx = ensureAudioContext();
  if (!ctx) return true;
  if (ctx.state === 'running') {
    playbackBlocked.value = false;
    return true;
  }
  try {
    await ctx.resume();
    const resumed = (ctx.state as AudioContextState) === 'running';
    playbackBlocked.value = !resumed;
    return resumed;
  } catch {
    playbackBlocked.value = true;
    return false;
  }
}

function stopActiveSpeech() {
  speechSessionId++;
  if (speechTimer !== null) clearTimeout(speechTimer);
  speechTimer = null;
  if (activeFetchController) {
    try {
      activeFetchController.abort();
    } catch {}
    activeFetchController = null;
  }
  if (activeAudioSource) {
    try {
      activeAudioSource.stop();
      activeAudioSource.disconnect();
    } catch {}
    activeAudioSource = null;
  }
  isPlayingAudio.value = false;
}

function playTone(freq: number, duration: number, delay: number) {
  const ctx = ensureAudioContext();
  if (!ctx || ctx.state === 'suspended') return;

  const now = ctx.currentTime + delay;
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  osc.type = 'sine';
  osc.frequency.setValueAtTime(freq, now);

  gain.gain.setValueAtTime(0.3, now);
  gain.gain.exponentialRampToValueAtTime(0.001, now + duration);

  osc.connect(gain);
  gain.connect(ctx.destination);

  osc.start(now);
  osc.stop(now + duration);
}

function playDingDong() {
  playTone(800, 0.25, 0);
  playTone(600, 0.45, 0.25);
}

function playBingBongAttention() {
  // Chuông Bing-Bong 2 hồi vang rõ giúp khách hàng chú ý màn hình TV khi Viettel AI hết hạn/gián đoạn.
  playTone(880, 0.28, 0);
  playTone(659.25, 0.48, 0.28);
  playTone(880, 0.28, 0.9);
  playTone(659.25, 0.55, 1.18);
}

function setCachedViettelAudio(text: string, data: ArrayBuffer) {
  if (viettelAudioCache.size >= VIETTEL_CACHE_LIMIT) {
    const oldestKey = viettelAudioCache.keys().next().value;
    if (oldestKey !== undefined) viettelAudioCache.delete(oldestKey);
  }
  viettelAudioCache.set(text, data.slice(0));
}

function isAudioPayloadBuffer(audioBytes: ArrayBuffer, contentType: string): boolean {
  if (!(audioBytes instanceof ArrayBuffer) || audioBytes.byteLength <= 256) return false;
  if (contentType.includes('json')) return false;
  const view = new Uint8Array(audioBytes, 0, Math.min(16, audioBytes.byteLength));
  for (let i = 0; i < view.length; i++) {
    const ch = view[i];
    if (ch === 0x20 || ch === 0x09 || ch === 0x0a || ch === 0x0d) continue;
    return ch !== 0x7b; // '{' biểu thị JSON lỗi thay vì luồng MP3/WAV
  }
  return false;
}

async function requestViettelTtsAudio(text: string): Promise<ArrayBuffer | null> {
  const cached = viettelAudioCache.get(text);
  if (cached) return cached.slice(0);
  if (typeof fetch !== 'function') return null;

  const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
  activeFetchController = controller;
  const timeoutId = controller ? setTimeout(() => controller.abort(), 8000) : null;

  try {
    const response = await fetch('/tts-viettel', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        accept: '*/*'
      },
      body: JSON.stringify({ text }),
      ...(controller ? { signal: controller.signal } : {})
    });
    if (timeoutId) clearTimeout(timeoutId);
    if (activeFetchController === controller) activeFetchController = null;

    if (!response || !response.ok) return null;
    const contentType = (response.headers?.get?.('content-type') || '').toLowerCase();
    const audioBytes = await response.arrayBuffer();
    if (!isAudioPayloadBuffer(audioBytes, contentType)) return null;

    setCachedViettelAudio(text, audioBytes);
    return audioBytes.slice(0);
  } catch {
    if (timeoutId) clearTimeout(timeoutId);
    if (activeFetchController === controller) activeFetchController = null;
    return null;
  }
}

function pruneExpiredAudioQueue(now = Date.now()) {
  while (audioQueue.length > 0 && audioQueue[0].expiresAt <= now) {
    audioQueue.shift();
  }
}

function processAudioQueue() {
  pruneExpiredAudioQueue();
  if (!active || isPlayingAudio.value || !soundEnabled.value || audioQueue.length === 0) return;

  const ctx = ensureAudioContext();
  if (ctx && ctx.state === 'suspended') {
    void tryResumeAudioContext().then((resumed) => {
      if (resumed && active && soundEnabled.value && !isPlayingAudio.value) {
        processAudioQueue();
      }
    });
    return;
  }

  const item = audioQueue.shift();
  if (!item) return;

  isPlayingAudio.value = true;
  const sessionId = ++speechSessionId;
  playDingDong();
  let finished = false;

  function finish() {
    if (finished || sessionId !== speechSessionId) return;
    finished = true;
    if (speechTimer !== null) clearTimeout(speechTimer);
    speechTimer = setTimeout(() => {
      if (sessionId !== speechSessionId) return;
      speechTimer = null;
      isPlayingAudio.value = false;
      processAudioQueue();
    }, 400);
  }

  speechTimer = setTimeout(async () => {
    if (!active || !soundEnabled.value || sessionId !== speechSessionId) return;
    speechTimer = setTimeout(finish, 12_000);

    const audioBytes = await requestViettelTtsAudio(item.text);
    if (!active || !soundEnabled.value || sessionId !== speechSessionId) return;

    const currentCtx = ensureAudioContext();
    if (audioBytes && currentCtx) {
      try {
        const decoded = await currentCtx.decodeAudioData(audioBytes);
        if (!active || !soundEnabled.value || sessionId !== speechSessionId) return;

        viettelTtsExpiredOrUnavailable.value = false;
        const source = currentCtx.createBufferSource();
        source.buffer = decoded;
        source.connect(currentCtx.destination);
        activeAudioSource = source;
        source.addEventListener(
          'ended',
          () => {
            if (activeAudioSource === source) activeAudioSource = null;
            finish();
          },
          { once: true }
        );
        source.start(0);
        return;
      } catch {
        // Khi giải mã thất bại, chuyển sang phát chuông Bing-Bong báo hiệu.
      }
    }

    // Viettel AI TTS hết hạn, hết lưu lượng hoặc gián đoạn -> phát chuông Bing-Bong để khách chú ý.
    viettelTtsExpiredOrUnavailable.value = true;
    playBingBongAttention();
    if (speechTimer !== null) clearTimeout(speechTimer);
    speechTimer = setTimeout(finish, 1800);
  }, 850);
}

function enqueueCallAudio(
  item: Pick<PublicQueueItemResponse, 'maskedPatientName'> & {
    queueNumberDisplay?: string | null;
    roomName?: string | null;
  }
) {
  if (audioQueue.length >= 3) {
    audioQueue.shift();
  }
  audioQueue.push({
    text: buildCallAnnouncementText(item),
    expiresAt: Date.now() + CALL_TTL_MS
  });
  processAudioQueue();
}

function previewVoice() {
  if (!soundEnabled.value || playbackBlocked.value || activeCall.value || isPlayingAudio.value) return;
  audioQueue.push({
    text: 'Mời bệnh nhân số A 001, Nguyễn Văn An, vào phòng số 1.',
    expiresAt: Date.now() + CALL_TTL_MS
  });
  processAudioQueue();
}

function toggleSound() {
  if (playbackBlocked.value) {
    soundEnabled.value = true;
    saveSoundPreference(true);
    void tryResumeAudioContext().then((resumed) => {
      if (resumed && active && soundEnabled.value) {
        playDingDong();
        processAudioQueue();
      }
    });
    return;
  }

  soundEnabled.value = !soundEnabled.value;
  saveSoundPreference(soundEnabled.value);
  if (soundEnabled.value) {
    void tryResumeAudioContext().then((resumed) => {
      if (resumed && active && soundEnabled.value) {
        playDingDong();
        processAudioQueue();
      }
    });
  } else {
    audioQueue.length = 0;
    stopActiveSpeech();
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

    const isInitialSnapshot = !initialSnapshotLoaded;
    initialSnapshotLoaded = true;

    const currentCalls = data.inConsultation.filter(item => item.status === 6);
    for (const item of currentCalls) {
      if (!seenCallNumbers.has(item.queueNumberDisplay)) {
        triggerCallBanner({
          queueNumberDisplay: item.queueNumberDisplay,
          maskedPatientName: item.maskedPatientName,
          roomName: item.roomName,
          status: item.status
        });
        if (soundEnabled.value && !isInitialSnapshot) {
          enqueueCallAudio(item);
        }
      }
    }
    const currentCallNumbers = new Set(currentCalls.map(item => item.queueNumberDisplay));
    for (const number of seenCallNumbers) {
      if (!currentCallNumbers.has(number)) seenCallNumbers.delete(number);
    }
    for (const item of currentCalls) seenCallNumbers.add(item.queueNumberDisplay);

    inConsultation.value = currentCalls;
    waiting.value = [];
  } catch {
    if (active && ticket === requestVersion) error.value = 'Mất kết nối máy chủ. Đang tự động kết nối lại...';
  }
}

function setupExternalListeners() {
  try {
    broadcastChannel = new BroadcastChannel('dental_tv_call_channel');
    broadcastChannel.onmessage = (event) => {
      if (event.data && typeof event.data === 'object' && event.data.status === 6 &&
          typeof event.data.queueNumberDisplay === 'string' && typeof event.data.maskedPatientName === 'string') {
        const item = event.data as ActiveCallData;
        seenCallNumbers.add(item.queueNumberDisplay);
        triggerCallBanner(item);
        if (soundEnabled.value) {
          enqueueCallAudio(item);
        }
      }
    };
  } catch {}
}

onMounted(() => {
  updateClock();
  clockTimer = setInterval(updateClock, 1000);
  syncSoundPreference();
  if (soundEnabled.value) {
    void tryResumeAudioContext();
  }
  setupExternalListeners();
  void loadQueueData();
  pollTimer = setInterval(loadQueueData, 3000);
});

onUnmounted(() => {
  active = false; requestVersion++; seenCallNumbers.clear(); soundEnabled.value = false;
  if (clockTimer) clearInterval(clockTimer);
  if (pollTimer) clearInterval(pollTimer);
  if (callTimer) clearInterval(callTimer);
  if (broadcastChannel) {
    broadcastChannel.close();
    broadcastChannel = null;
  }
  stopActiveSpeech();
  audioQueue.length = 0;
  viettelAudioCache.clear();
  if (audioCtx && audioCtx.state !== 'closed') {
    void audioCtx.close();
  }
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
