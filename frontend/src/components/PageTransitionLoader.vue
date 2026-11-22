<template>
  <Transition name="page-loader-fade">
    <div v-if="visible" class="page-loader-overlay" role="status" aria-live="polite" aria-label="Đang chuyển trang">
      <div class="page-loader-card">
        <svg class="page-loader-svg" viewBox="0 0 128 128" width="96" height="96" aria-hidden="true">
          <defs>
            <clipPath id="hg-tooth-fill-clip">
              <rect class="tooth-clip-rect" x="0" y="0" width="128" height="128" />
            </clipPath>
            <linearGradient id="hg-tooth-blue" x1="0" y1="1" x2="0" y2="0">
              <stop offset="0%" stop-color="#102e56" />
              <stop offset="65%" stop-color="#1857b6" />
              <stop offset="100%" stop-color="#397bcc" />
            </linearGradient>
          </defs>

          <g transform="translate(4 0) scale(1.2)">
            <!-- Logo rỗng nền ban đầu -->
            <path
              class="tooth-outline"
              d="M20 34 C27 27 36 27 49 31 C62 27 72 26 80 34 C93 46 82 69 77 81 C71 98 66 104 59 96 C55 91 57 73 50 73 C43 73 45 91 41 96 C34 104 29 96 23 81 C18 67 7 47 20 34 Z"
            />
            <path class="smile-outline" d="M37 49 Q50 60 63 49" />
          </g>

          <!-- Logo tràn màu từ dưới lên -->
          <g clip-path="url(#hg-tooth-fill-clip)">
            <g transform="translate(4 0) scale(1.2)">
              <path
                fill="url(#hg-tooth-blue)"
                d="M20 34 C27 27 36 27 49 31 C62 27 72 26 80 34 C93 46 82 69 77 81 C71 98 66 104 59 96 C55 91 57 73 50 73 C43 73 45 91 41 96 C34 104 29 96 23 81 C18 67 7 47 20 34 Z"
              />
              <path d="M37 49 Q50 60 63 49" fill="none" stroke="#FFFFFF" stroke-width="5.6" stroke-linecap="round" />
            </g>
          </g>

          <!-- Vương miện hạ xuống đội lên đầu chiếc răng -->
          <g class="crown-group">
            <g transform="translate(4 0) scale(1.2)">
              <path fill="#E5B95E" d="M24 24 L21 9 L39 16 L50 5 L61 16 L79 9 L76 24 Z" />
              <path d="M30 28 H70" stroke="#E5B95E" stroke-width="4" stroke-linecap="round" />
            </g>
          </g>
        </svg>
        <span class="page-loader-label">Nha Khoa Hoàng Gia</span>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
defineProps<{ visible: boolean }>();
</script>

<style scoped>
.page-loader-overlay {
  position: fixed;
  inset: 0;
  z-index: 3000;
  display: grid;
  place-items: center;
  background: rgba(248, 251, 255, 0.72);
  backdrop-filter: blur(5px);
  pointer-events: auto;
}

.page-loader-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.75rem;
  border-radius: 20px;
  background: rgba(255, 255, 255, 0.96);
  border: 1px solid #d5e6f7;
  box-shadow: 0 20px 50px rgba(16, 46, 86, 0.14);
}

.page-loader-svg {
  display: block;
  overflow: visible;
}

.tooth-outline {
  fill: rgba(24, 87, 182, 0.07);
  stroke: #94b8e6;
  stroke-width: 2.5;
  stroke-linejoin: round;
}

.smile-outline {
  fill: none;
  stroke: #cbd5e1;
  stroke-width: 4.5;
  stroke-linecap: round;
}

.tooth-clip-rect {
  transform: translateY(108px);
  animation: hgToothRise 0.48s cubic-bezier(0.22, 1, 0.36, 1) forwards;
}

.crown-group {
  transform-origin: center;
  animation: hgCrownDrop 0.52s cubic-bezier(0.22, 1, 0.36, 1) forwards;
}

.page-loader-label {
  font-size: 0.85rem;
  font-weight: 800;
  letter-spacing: 0.02em;
  color: #102e56;
}

@keyframes hgToothRise {
  0% {
    transform: translateY(108px);
  }
  100% {
    transform: translateY(0);
  }
}

@keyframes hgCrownDrop {
  0% {
    opacity: 0.25;
    transform: translateY(-20px) scale(0.9);
  }
  65% {
    opacity: 1;
    transform: translateY(2px) scale(1.04);
  }
  100% {
    opacity: 1;
    transform: translateY(0) scale(1);
    filter: drop-shadow(0 2px 6px rgba(229, 185, 94, 0.45));
  }
}

.page-loader-fade-enter-active,
.page-loader-fade-leave-active {
  transition: opacity 0.16s ease;
}
.page-loader-fade-enter-from,
.page-loader-fade-leave-to {
  opacity: 0;
}

@media (prefers-reduced-motion: reduce) {
  .tooth-clip-rect,
  .crown-group {
    animation: none;
    transform: none;
    opacity: 1;
  }
}
</style>
