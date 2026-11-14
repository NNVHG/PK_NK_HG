<template>
  <Teleport to="body">
    <div v-if="isOpen" class="scanner-backdrop" @click.self="handleClose" role="dialog" aria-modal="true" aria-labelledby="scanner-modal-title">
      <div class="scanner-modal">
        <header class="scanner-header">
          <div class="scanner-title-group">
            <span class="scanner-icon" aria-hidden="true">📷</span>
            <div>
              <h3 id="scanner-modal-title" class="scanner-title">Quét Căn Cước Công Dân (CCCD)</h3>
              <p class="scanner-subtitle">Hỗ trợ nhận diện tự động qua Camera hoặc Máy quét mã vạch USB</p>
            </div>
          </div>
          <button type="button" class="scanner-close-btn" @click="handleClose" aria-label="Đóng cửa sổ quét">✕</button>
        </header>

      <!-- Mode Switch Tabs: Camera vs Máy quét USB -->
      <div class="scan-mode-tabs" role="tablist">
        <button
          type="button"
          class="scan-mode-tab"
          :class="{ active: activeMode === 'camera' }"
          role="tab"
          :aria-selected="activeMode === 'camera'"
          @click="switchMode('camera')"
        >
          📷 Quét bằng Camera
        </button>
        <button
          type="button"
          class="scan-mode-tab"
          :class="{ active: activeMode === 'usb' }"
          role="tab"
          :aria-selected="activeMode === 'usb'"
          @click="switchMode('usb')"
        >
          ⚡ Máy quét mã vạch USB / Dán mã
        </button>
      </div>

      <div class="scanner-body">
        <!-- TAB 1: CAMERA SCANNER -->
        <template v-if="activeMode === 'camera'">
          <div class="scanner-viewport">
            <video
              ref="videoRef"
              class="scanner-video"
              playsinline
              muted
              autoplay
            ></video>

            <!-- Hidden offscreen canvas for frame analysis -->
            <canvas ref="canvasRef" class="scanner-canvas-hidden" aria-hidden="true"></canvas>

            <!-- Target Viewfinder & Laser Beam -->
            <div class="scanner-reticle" :class="{ 'scanner-reticle--success': scanSuccess }">
              <div class="reticle-corner reticle-tl"></div>
              <div class="reticle-corner reticle-tr"></div>
              <div class="reticle-corner reticle-bl"></div>
              <div class="reticle-corner reticle-br"></div>
              
              <div v-if="!scanSuccess && isStreaming" class="scanner-laser"></div>

              <div v-if="scanSuccess" class="success-indicator">
                <span class="success-icon">✓</span>
                <span class="success-text">Đã nhận diện CCCD!</span>
              </div>
            </div>

            <!-- Loading overlay when camera is initializing -->
            <div v-if="isLoading" class="scanner-loading-overlay">
              <span class="spin-icon">⏳</span>
              <span>Đang khởi động camera…</span>
            </div>

            <!-- Fallback or Error message inside viewport -->
            <div v-if="cameraError" class="scanner-error-overlay">
              <span class="error-icon">⚠️</span>
              <p class="error-text">{{ cameraError }}</p>
              <button type="button" class="app-btn app-btn--secondary app-btn--sm" @click="startScanner">
                Thử lại camera
              </button>
            </div>
          </div>

          <!-- Controls Toolbar for Camera -->
          <div class="scanner-toolbar">
            <!-- Device Selector if multiple cameras found -->
            <div v-if="videoDevices.length > 1" class="toolbar-item">
              <label for="camera-select" class="toolbar-label">Chọn Camera:</label>
              <select
                id="camera-select"
                v-model="selectedDeviceId"
                class="camera-select"
                :disabled="isLoading"
                @change="switchCamera"
              >
                <option v-for="(dev, idx) in videoDevices" :key="dev.deviceId" :value="dev.deviceId">
                  {{ dev.label || `Camera ${idx + 1}` }}
                </option>
              </select>
            </div>

            <!-- Torch toggle if supported -->
            <button
              v-if="hasTorch"
              type="button"
              class="app-btn app-btn--secondary app-btn--sm"
              @click="toggleTorch"
            >
              {{ torchOn ? '🔦 Tắt đèn' : '💡 Bật đèn' }}
            </button>

            <!-- File upload fallback for scanned images -->
            <label class="app-btn app-btn--secondary app-btn--sm file-upload-btn">
              <span>📁 Chọn ảnh CCCD từ máy</span>
              <input
                type="file"
                accept="image/*"
                class="file-input-hidden"
                @change="handleFileUpload"
              />
            </label>
          </div>

          <!-- Quick USB trigger bar (usable right away while in camera mode) -->
          <form class="quick-usb-bar" @submit.prevent="submitUsbCode">
            <span class="quick-usb-icon" aria-hidden="true">⚡</span>
            <input
              ref="quickUsbInput"
              v-model="usbCode"
              type="password"
              maxlength="1024"
              class="quick-usb-input"
              placeholder="Hoặc đặt con trỏ vào đây để bắn tia máy quét USB..."
              autocomplete="off"
            />
            <button type="submit" class="app-btn app-btn--secondary app-btn--sm" :disabled="!usbCode.trim()">
              Giải mã
            </button>
          </form>
        </template>

        <!-- TAB 2: USB SCANNER & MANUAL INTAKE -->
        <template v-else>
          <div class="usb-mode-panel">
            <div class="usb-icon-box" aria-hidden="true">📟</div>
            <h4 class="usb-heading">Đầu Đọc Mã Vạch USB Gắn Quầy</h4>
            <p class="usb-instructions">
              Đặt con trỏ vào ô bên dưới, sau đó hướng đầu đọc USB vào mã QR ở góc trên bên phải thẻ CCCD gắn chip và bấm cò máy quét.
            </p>

            <form class="usb-form" @submit.prevent="submitUsbCode">
              <div class="form-group">
                <label for="usb-barcode-input" class="usb-input-label">Mã QR CCCD quét từ thiết bị USB:</label>
                <input
                  id="usb-barcode-input"
                  ref="usbModeInput"
                  v-model="usbCode"
                  type="password"
                  maxlength="1024"
                  class="usb-large-input"
                  placeholder="Bắn tia quét hoặc dán mã CCCD vào đây..."
                  autocomplete="off"
                />
              </div>

              <div class="usb-actions-row">
                <button type="submit" class="app-btn app-btn--primary app-btn--lg" :disabled="!usbCode.trim()">
                  Giải Mã Dữ Liệu CCCD
                </button>
                <label class="app-btn app-btn--secondary file-upload-btn">
                  <span>📁 Chọn ảnh CCCD từ máy</span>
                  <input
                    type="file"
                    accept="image/*"
                    class="file-input-hidden"
                    @change="handleFileUpload"
                  />
                </label>
              </div>
            </form>
          </div>
        </template>

        <!-- Real-time scan result or error alert -->
        <div v-if="parseError" class="scanner-alert scanner-alert--warning" role="alert">
          {{ parseError }}
        </div>
      </div>

      <footer class="scanner-footer">
        <span class="scanner-hint">
          💡 Mẹo: Hướng camera vào mã QR góc trên bên phải thẻ CCCD, giữ thẻ cách khoảng 15-20cm để quét nét nhất.
        </span>
        <button type="button" class="app-btn app-btn--secondary" @click="handleClose">
          Đóng
        </button>
      </footer>
    </div>
  </div>
  </Teleport>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue';
import jsQR from 'jsqr';
import { decodeCitizenQr, type ScannedIdentity } from '@/services/qrPatientIntake';

const props = defineProps<{
  isOpen: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'success', data: { raw: string; identity: ScannedIdentity }): void;
}>();

const videoRef = ref<HTMLVideoElement | null>(null);
const canvasRef = ref<HTMLCanvasElement | null>(null);
const quickUsbInput = ref<HTMLInputElement | null>(null);
const usbModeInput = ref<HTMLInputElement | null>(null);

const activeMode = ref<'camera' | 'usb'>('camera');
const usbCode = ref('');

const isLoading = ref(false);
const isStreaming = ref(false);
const cameraError = ref('');
const parseError = ref('');
const scanSuccess = ref(false);

const videoDevices = ref<MediaDeviceInfo[]>([]);
const selectedDeviceId = ref<string>('');
const hasTorch = ref(false);
const torchOn = ref(false);

let mediaStream: MediaStream | null = null;
let animFrameId: number | null = null;
let scanTimer: ReturnType<typeof setTimeout> | null = null;
let currentTicket = 0;

/**
 * Switch mode between camera and USB intake
 */
function switchMode(mode: 'camera' | 'usb') {
  activeMode.value = mode;
  parseError.value = '';
  if (mode === 'camera') {
    startScanner();
  } else {
    stopScanner();
    nextTick(() => {
      usbModeInput.value?.focus();
    });
  }
}

/**
 * Handle submit from USB barcode reader input
 */
async function submitUsbCode() {
  const code = usbCode.value.trim();
  if (!code) return;
  await handleDetectedCode(code);
}

/**
 * Web Audio API audio confirmation beep (880Hz -> 1320Hz chirp, 90ms)
 */
function playSuccessBeep() {
  try {
    const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
    if (!AudioContextClass) return;
    const ctx = new AudioContextClass();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();

    osc.type = 'sine';
    osc.frequency.setValueAtTime(880, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(1320, ctx.currentTime + 0.08);

    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.09);

    osc.connect(gain);
    gain.connect(ctx.destination);

    osc.start();
    osc.stop(ctx.currentTime + 0.09);
  } catch {
    // Audio feedback optional, silently ignore if blocked by browser policy
  }
}

/**
 * Enumerate available video inputs for switching between front/back/USB counter cameras
 */
async function enumerateCameras() {
  try {
    if (!navigator.mediaDevices?.enumerateDevices) return;
    const devices = await navigator.mediaDevices.enumerateDevices();
    const videoInputs = devices.filter(d => d.kind === 'videoinput');
    videoDevices.value = videoInputs;

    if (!selectedDeviceId.value && videoInputs.length > 0) {
      // Prioritize environment / back / USB cameras over front webcam if available
      const backOrUsb = videoInputs.find(d => 
        /back|rear|environment|usb|scanner|counter|external/i.test(d.label)
      );
      selectedDeviceId.value = backOrUsb ? backOrUsb.deviceId : videoInputs[0].deviceId;
    }
  } catch {
    // Ignore enumeration failure
  }
}

/**
 * Start camera feed and setup processing loop
 */
async function startScanner() {
  stopScanner();
  isLoading.value = true;
  cameraError.value = '';
  parseError.value = '';
  scanSuccess.value = false;
  const ticket = ++currentTicket;

  if (!navigator.mediaDevices?.getUserMedia) {
    isLoading.value = false;
    cameraError.value = 'Trình duyệt không hỗ trợ truy cập Camera. Vui lòng chuyển sang tab Máy quét USB hoặc tải ảnh lên.';
    return;
  }

  try {
    await enumerateCameras();
    if (ticket !== currentTicket) return;

    const constraints: MediaStreamConstraints = {
      video: selectedDeviceId.value
        ? { deviceId: { exact: selectedDeviceId.value }, width: { ideal: 1280 }, height: { ideal: 720 } }
        : { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
      audio: false
    };

    const stream = await navigator.mediaDevices.getUserMedia(constraints);
    if (ticket !== currentTicket) {
      stream.getTracks().forEach(t => t.stop());
      return;
    }

    mediaStream = stream;
    await nextTick();

    if (!videoRef.value) {
      stopScanner();
      return;
    }

    videoRef.value.srcObject = stream;
    await videoRef.value.play();

    // Check torch capability
    const track = stream.getVideoTracks()[0];
    if (track) {
      const capabilities: any = track.getCapabilities?.() || {};
      hasTorch.value = !!capabilities.torch;
    }

    isStreaming.value = true;
    isLoading.value = false;

    // Begin scanning loop
    startDecodingLoop(ticket);
  } catch (err: any) {
    if (ticket !== currentTicket) return;
    isLoading.value = false;
    isStreaming.value = false;
    if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
      cameraError.value = 'Quyền Camera bị từ chối. Bạn có thể cho phép quyền hoặc chuyển sang tab "Máy quét mã vạch USB".';
    } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
      cameraError.value = 'Không tìm thấy thiết bị Camera. Vui lòng chuyển sang tab "Máy quét mã vạch USB".';
    } else {
      cameraError.value = 'Không thể mở Camera. Vui lòng kiểm tra thiết bị hoặc chuyển sang tab "Máy quét USB".';
    }
  }
}

/**
 * Dual-engine frame decoding: BarcodeDetector + jsQR fallback
 */
function startDecodingLoop(ticket: number) {
  let isScanningFrame = false;

  const DetectorClass = (globalThis as any).BarcodeDetector;
  let nativeDetector: any = null;
  if (DetectorClass) {
    try {
      nativeDetector = new DetectorClass({ formats: ['qr_code'] });
    } catch {
      nativeDetector = null;
    }
  }

  async function processFrame() {
    if (ticket !== currentTicket || !props.isOpen || scanSuccess.value || activeMode.value !== 'camera') return;
    if (!videoRef.value || !canvasRef.value || videoRef.value.readyState < 2) {
      scanTimer = setTimeout(processFrame, 150);
      return;
    }

    if (!isScanningFrame) {
      isScanningFrame = true;
      try {
        const video = videoRef.value;
        const canvas = canvasRef.value;
        let qrData: string | null = null;

        // Try Engine 1: Native BarcodeDetector if available
        if (nativeDetector) {
          try {
            const detected = await nativeDetector.detect(video);
            if (detected && detected.length > 0 && detected[0]?.rawValue) {
              qrData = detected[0].rawValue;
            }
          } catch {
            // Native detector frame error, proceed to fallback
          }
        }

        // Try Engine 2: Canvas + jsQR
        if (!qrData && video.videoWidth > 0 && video.videoHeight > 0) {
          const width = video.videoWidth;
          const height = video.videoHeight;

          // Limit canvas processing resolution to ~640px wide for peak performance
          const scale = Math.min(1, 640 / width);
          const drawW = Math.round(width * scale);
          const drawH = Math.round(height * scale);

          canvas.width = drawW;
          canvas.height = drawH;
          const ctx = canvas.getContext('2d', { willReadFrequently: true });
          if (ctx) {
            ctx.drawImage(video, 0, 0, drawW, drawH);
            const imgData = ctx.getImageData(0, 0, drawW, drawH);
            const code = jsQR(imgData.data, drawW, drawH, {
              inversionAttempts: 'dontInvert'
            });
            if (code && code.data) {
              qrData = code.data;
            }
          }
        }

        // If a code was detected, process CCCD
        if (qrData) {
          await handleDetectedCode(qrData);
          if (scanSuccess.value) {
            isScanningFrame = false;
            return;
          }
        }
      } catch {
        // Frame processing exception ignored to continue scanning
      } finally {
        isScanningFrame = false;
      }
    }

    if (ticket === currentTicket && props.isOpen && !scanSuccess.value && activeMode.value === 'camera') {
      scanTimer = setTimeout(processFrame, 120);
    }
  }

  processFrame();
}

/**
 * Handle and parse raw QR string
 */
async function handleDetectedCode(rawString: string) {
  parseError.value = '';
  try {
    const identity = await decodeCitizenQr(rawString);
    scanSuccess.value = true;
    playSuccessBeep();

    // Small delay so user sees visual green confirmation before closing
    setTimeout(() => {
      stopScanner();
      emit('success', { raw: rawString, identity });
      handleClose();
    }, 450);
  } catch {
    parseError.value = 'Mã quét không đúng định dạng CCCD gắn chip (cần có số CCCD 12 số, họ tên, ngày sinh). Vui lòng quét lại.';
  }
}

/**
 * Handle static file upload
 */
function handleFileUpload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = async (e) => {
    const img = new Image();
    img.onload = async () => {
      if (!canvasRef.value) return;
      const canvas = canvasRef.value;
      canvas.width = img.width;
      canvas.height = img.height;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      ctx.drawImage(img, 0, 0);
      const imgData = ctx.getImageData(0, 0, img.width, img.height);
      const code = jsQR(imgData.data, img.width, img.height);
      if (code && code.data) {
        await handleDetectedCode(code.data);
      } else {
        parseError.value = 'Không tìm thấy mã QR trong ảnh đã chọn. Vui lòng thử ảnh rõ nét hơn.';
      }
    };
    img.src = e.target?.result as string;
  };
  reader.readAsDataURL(file);
  input.value = '';
}

/**
 * Switch camera device
 */
function switchCamera() {
  startScanner();
}

/**
 * Toggle flashlight
 */
async function toggleTorch() {
  if (!mediaStream) return;
  const track = mediaStream.getVideoTracks()[0];
  if (!track) return;
  try {
    torchOn.value = !torchOn.value;
    await (track as any).applyConstraints({
      advanced: [{ torch: torchOn.value }]
    });
  } catch {
    torchOn.value = false;
  }
}

/**
 * Stop media stream and cleanup timers
 */
function stopScanner() {
  currentTicket++;
  if (scanTimer) {
    clearTimeout(scanTimer);
    scanTimer = null;
  }
  if (animFrameId) {
    cancelAnimationFrame(animFrameId);
    animFrameId = null;
  }
  if (mediaStream) {
    mediaStream.getTracks().forEach(t => t.stop());
    mediaStream = null;
  }
  if (videoRef.value) {
    videoRef.value.srcObject = null;
  }
  isStreaming.value = false;
  isLoading.value = false;
  torchOn.value = false;
}

function handleClose() {
  stopScanner();
  usbCode.value = '';
  emit('close');
}

watch(
  () => props.isOpen,
  (open) => {
    if (typeof document !== 'undefined') {
      if (open) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    }
    if (open) {
      usbCode.value = '';
      if (activeMode.value === 'camera') {
        startScanner();
      } else {
        nextTick(() => {
          usbModeInput.value?.focus();
        });
      }
    } else {
      stopScanner();
    }
  },
  { immediate: true }
);

onBeforeUnmount(() => {
  stopScanner();
  if (typeof document !== 'undefined') {
    document.body.style.overflow = '';
  }
});
</script>

<style scoped>
.scanner-backdrop {
  position: fixed;
  inset: 0;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background-color: rgba(15, 23, 42, 0.75);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  z-index: 99999;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.25rem;
  box-sizing: border-box;
}

.scanner-modal {
  background: var(--color-surface-card, #ffffff);
  border-radius: var(--radius-xl, 18px);
  box-shadow: 0 25px 50px -12px rgba(15, 23, 42, 0.45);
  width: 100%;
  max-width: 560px;
  max-height: min(92vh, 680px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--color-border-subtle, #e2e8f0);
  margin: auto;
}

.scanner-header {
  padding: 1.15rem 1.5rem;
  background: var(--color-surface-subtle, #f8fafc);
  border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.scanner-title-group {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.scanner-icon {
  font-size: 1.5rem;
  line-height: 1;
}

.scanner-title {
  margin: 0;
  font-size: var(--font-size-base, 1.1rem);
  font-weight: 750;
  color: var(--color-primary, #102e56);
}

.scanner-subtitle {
  margin: 0.2rem 0 0;
  font-size: var(--font-size-xs, 0.8rem);
  color: var(--color-text-subtle, #64748b);
}

.scanner-close-btn {
  background: transparent;
  border: none;
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text-subtle, #64748b);
  cursor: pointer;
  padding: 0.4rem 0.6rem;
  border-radius: var(--radius-sm, 6px);
  line-height: 1;
  transition: background-color 0.15s, color 0.15s;
}

.scanner-close-btn:hover {
  background: var(--color-surface-muted, #e2e8f0);
  color: var(--color-text-main, #0f172a);
}

/* Tabs */
.scan-mode-tabs {
  display: flex;
  background: var(--color-surface-subtle, #f8fafc);
  border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
  padding: 0 1rem;
  gap: 0.5rem;
}

.scan-mode-tab {
  background: transparent;
  border: none;
  padding: 0.75rem 1rem;
  font-size: var(--font-size-sm, 0.875rem);
  font-weight: 600;
  color: var(--color-text-muted, #475569);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: color 0.15s, border-color 0.15s;
}

.scan-mode-tab.active {
  color: var(--color-primary-hover, #1857b6);
  border-bottom-color: var(--color-primary-hover, #1857b6);
}

.scanner-body {
  padding: 1.25rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow-y: auto;
}

.scanner-viewport {
  position: relative;
  width: 100%;
  height: 250px;
  max-height: 40vh;
  background: #000000;
  border-radius: var(--radius-lg, 14px);
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: inset 0 0 20px rgba(0, 0, 0, 0.6);
}

.scanner-video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.scanner-canvas-hidden {
  display: none;
}

/* Targeting Reticle Box */
.scanner-reticle {
  position: absolute;
  width: 200px;
  height: 200px;
  max-width: 80%;
  max-height: 80%;
  border-radius: 12px;
  pointer-events: none;
  box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.45);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.scanner-reticle--success {
  box-shadow: 0 0 0 9999px rgba(22, 163, 74, 0.4);
}

.reticle-corner {
  position: absolute;
  width: 24px;
  height: 24px;
  border-color: var(--color-accent, #e5b95e);
  border-style: solid;
}

.reticle-tl { top: -2px; left: -2px; border-width: 4px 0 0 4px; border-top-left-radius: 10px; }
.reticle-tr { top: -2px; right: -2px; border-width: 4px 4px 0 0; border-top-right-radius: 10px; }
.reticle-bl { bottom: -2px; left: -2px; border-width: 0 0 4px 4px; border-bottom-left-radius: 10px; }
.reticle-br { bottom: -2px; right: -2px; border-width: 0 4px 4px 0; border-bottom-right-radius: 10px; }

.scanner-reticle--success .reticle-corner {
  border-color: var(--color-success, #16a34a);
}

/* Red / Gold laser scan line */
.scanner-laser {
  position: absolute;
  left: 5%;
  right: 5%;
  height: 3px;
  background: linear-gradient(90deg, transparent, #ef4444 20%, #ef4444 80%, transparent);
  box-shadow: 0 0 8px #ef4444;
  animation: laserScan 2s infinite ease-in-out;
}

@keyframes laserScan {
  0% { top: 8%; opacity: 0.8; }
  50% { top: 90%; opacity: 1; }
  100% { top: 8%; opacity: 0.8; }
}

.success-indicator {
  position: absolute;
  inset: 0;
  background: rgba(22, 163, 74, 0.85);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: #ffffff;
  border-radius: 12px;
}

.success-icon {
  font-size: 2.5rem;
  font-weight: 800;
}

.success-text {
  font-size: 1rem;
  font-weight: 700;
}

/* Loading & Error Overlays */
.scanner-loading-overlay,
.scanner-error-overlay {
  position: absolute;
  inset: 0;
  background: rgba(15, 23, 42, 0.85);
  color: #ffffff;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 1.5rem;
  text-align: center;
}

.error-icon {
  font-size: 2rem;
}

.error-text {
  margin: 0;
  font-size: 0.9rem;
  color: #fca5a5;
  max-width: 320px;
}

/* Controls Toolbar */
.scanner-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.toolbar-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.toolbar-label {
  font-size: var(--font-size-xs, 0.8rem);
  font-weight: 600;
  color: var(--color-text-muted, #475569);
}

.camera-select {
  padding: 0.35rem 0.65rem;
  font-size: var(--font-size-xs, 0.825rem);
  border: 1px solid var(--color-border-medium, #cbd5e1);
  border-radius: var(--radius-sm, 6px);
  background: #ffffff;
  color: var(--color-text-main, #0f172a);
}

.file-upload-btn {
  cursor: pointer;
  position: relative;
  overflow: hidden;
}

.file-input-hidden {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

/* Quick USB trigger bar */
.quick-usb-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  background: var(--color-surface-subtle, #f8fafc);
  border: 1px dashed var(--color-border-medium, #cbd5e1);
  border-radius: var(--radius-md, 10px);
}

.quick-usb-icon {
  font-size: 1.1rem;
}

.quick-usb-input {
  flex: 1;
  background: transparent;
  border: none;
  font-size: var(--font-size-sm, 0.85rem);
  color: var(--color-text-main, #0f172a);
  outline: none;
}

/* USB Mode Dedicated Panel */
.usb-mode-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 1.5rem 1rem;
  background: var(--color-surface-subtle, #f8fafc);
  border: 1px solid var(--color-border-subtle, #e2e8f0);
  border-radius: var(--radius-lg, 14px);
}

.usb-icon-box {
  font-size: 2.75rem;
  line-height: 1;
  margin-bottom: 0.75rem;
}

.usb-heading {
  margin: 0 0 0.4rem;
  font-size: 1.15rem;
  color: var(--color-primary, #102e56);
  font-weight: 750;
}

.usb-instructions {
  margin: 0 0 1.5rem;
  font-size: 0.85rem;
  color: var(--color-text-muted, #475569);
  max-width: 440px;
  line-height: 1.45;
}

.usb-form {
  width: 100%;
  max-width: 460px;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.usb-input-label {
  display: block;
  text-align: left;
  font-size: var(--font-size-xs, 0.825rem);
  font-weight: 600;
  color: var(--color-text-main, #0f172a);
  margin-bottom: 0.4rem;
}

.usb-large-input {
  width: 100%;
  min-height: 48px;
  padding: 0.75rem 1rem;
  border: 2px solid var(--color-primary-border, #bfdbfe);
  border-radius: var(--radius-md, 10px);
  font-size: 1rem;
  box-sizing: border-box;
  background: #ffffff;
}

.usb-large-input:focus {
  outline: none;
  border-color: var(--color-primary-hover, #1857b6);
  box-shadow: var(--shadow-focus, 0 0 0 3px rgba(2, 132, 199, 0.2));
}

.usb-actions-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  justify-content: center;
}

.scanner-alert {
  padding: 0.65rem 0.85rem;
  border-radius: var(--radius-md, 8px);
  font-size: var(--font-size-xs, 0.825rem);
  font-weight: 500;
  line-height: 1.4;
}

.scanner-alert--warning {
  background: var(--color-warning-light, #fff7ed);
  color: var(--color-warning, #ea580c);
  border: 1px solid var(--color-warning-border, #fed7aa);
}

.scanner-footer {
  padding: 1rem 1.5rem;
  background: var(--color-surface-subtle, #f8fafc);
  border-top: 1px solid var(--color-border-subtle, #e2e8f0);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.scanner-hint {
  font-size: var(--font-size-xs, 0.775rem);
  color: var(--color-text-subtle, #64748b);
  line-height: 1.35;
}

@media (max-width: 640px) {
  .scanner-footer {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
