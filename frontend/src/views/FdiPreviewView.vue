<template>
  <div class="fdi-preview-page">
    <header class="preview-header">
      <div class="header-info">
        <h1>Xem trước Sơ đồ răng FDI (Dev Mode)</h1>
        <p class="subtitle">
          Chức năng F_FDI_01 &amp; F_FDI_02 · Tuân thủ ISO 3950 FDI, AGENTS.md §2.4, DL-007, DL-018
        </p>
      </div>

      <div class="preview-actions">
        <label class="toggle-control">
          <input type="checkbox" v-model="readonlyMode" />
          <span>Chế độ chỉ đọc (Readonly)</span>
        </label>
        <label class="toggle-control">
          <input type="checkbox" v-model="multipleMode" />
          <span>Chọn nhiều (Multiple)</span>
        </label>
      </div>
    </header>

    <!-- Nút kịch bản kiểm thử nhanh -->
    <section class="test-scenarios-panel">
      <strong>Kịch bản kiểm thử tương tác:</strong>
      <div class="scenario-buttons">
        <button type="button" class="btn-sample" @click="applyScenario1">
          Kịch bản 1: Chọn mặt nhai R16
        </button>
        <button type="button" class="btn-sample" @click="applyScenario2">
          Kịch bản 2: Chọn toàn bộ R21
        </button>
        <button type="button" class="btn-sample" @click="applyScenario3">
          Kịch bản 3: Chọn Răng sữa (R54, R65)
        </button>
        <button type="button" class="btn-sample btn-danger" @click="clearSelections">
          Xóa toàn bộ
        </button>
      </div>
    </section>

    <!-- Component Sơ đồ răng chính -->
    <main class="chart-wrapper">
      <FdiDentalChart
        v-model="selections"
        v-model:dentition-type="dentitionType"
        v-model:selected-tooth="lastTooth"
        v-model:selected-surface="lastSurface"
        :readonly="readonlyMode"
        :multiple="multipleMode"
        @surface-click="onSurfaceClick"
        @tooth-click="onToothClick"
      />
    </main>

    <!-- Bảng thông tin phản hồi sự kiện -->
    <footer class="inspector-panel">
      <div class="inspector-card">
        <h3>Thông tin lựa chọn hiện tại</h3>
        <p><strong>Loại bộ răng:</strong> {{ dentitionType }}</p>
        <p><strong>Răng vừa click:</strong> {{ lastTooth ? `Răng ${lastTooth}` : 'Chưa có' }}</p>
        <p><strong>Mặt vừa click:</strong> {{ lastSurface ? lastSurface : 'Chưa có' }}</p>
        <p><strong>Tổng số mục chọn:</strong> {{ selections.length }}</p>
      </div>

      <div class="inspector-card raw-data">
        <h3>Dữ liệu v-model (JSON)</h3>
        <pre>{{ JSON.stringify(selections, null, 2) }}</pre>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import {
  FdiDentalChart,
  type SelectedToothSurface,
  type DentitionType,
  type ToothSurface
} from '@/components/fdi';

const dentitionType = ref<DentitionType>('adult');
const readonlyMode = ref<boolean>(false);
const multipleMode = ref<boolean>(true);

const selections = ref<SelectedToothSurface[]>([
  { toothNumber: 16, surface: 'occlusal' },
  { toothNumber: 16, surface: 'buccal' }
]);

const lastTooth = ref<number | null>(16);
const lastSurface = ref<ToothSurface | null>('occlusal');

function onSurfaceClick(payload: { toothNumber: number; surface: ToothSurface }) {
  lastTooth.value = payload.toothNumber;
  lastSurface.value = payload.surface;
}

function onToothClick(toothNumber: number) {
  lastTooth.value = toothNumber;
}

function applyScenario1() {
  dentitionType.value = 'adult';
  selections.value = [{ toothNumber: 16, surface: 'occlusal' }];
  lastTooth.value = 16;
  lastSurface.value = 'occlusal';
}

function applyScenario2() {
  dentitionType.value = 'adult';
  selections.value = [{ toothNumber: 21 }];
  lastTooth.value = 21;
  lastSurface.value = null;
}

function applyScenario3() {
  dentitionType.value = 'child';
  selections.value = [
    { toothNumber: 54, surface: 'occlusal' },
    { toothNumber: 65, surface: 'mesial' }
  ];
  lastTooth.value = 65;
  lastSurface.value = 'mesial';
}

function clearSelections() {
  selections.value = [];
  lastTooth.value = null;
  lastSurface.value = null;
}
</script>

<style scoped>
.fdi-preview-page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 24px 16px 48px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  font-family: inherit;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid #e2e8f0;
}

.header-info h1 {
  font-size: 22px;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 4px;
}

.subtitle {
  font-size: 13px;
  color: #64748b;
  margin: 0;
}

.preview-actions {
  display: flex;
  align-items: center;
  gap: 16px;
}

.toggle-control {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: #334155;
  cursor: pointer;
}

.test-scenarios-panel {
  display: flex;
  align-items: center;
  gap: 12px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 12px 16px;
  flex-wrap: wrap;
  font-size: 13px;
}

.scenario-buttons {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.btn-sample {
  font-family: inherit;
  font-size: 12px;
  font-weight: 600;
  padding: 6px 12px;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  cursor: pointer;
  color: #1e293b;
  transition: all 0.15s ease;
}

.btn-sample:hover {
  background: #f1f5f9;
  border-color: #94a3b8;
}

.btn-sample.btn-danger {
  color: #dc2626;
  border-color: #fecaca;
  background: #fff5f5;
}

.btn-sample.btn-danger:hover {
  background: #fee2e2;
}

.chart-wrapper {
  width: 100%;
}

.inspector-panel {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

@media (max-width: 768px) {
  .inspector-panel {
    grid-template-columns: 1fr;
  }
}

.inspector-card {
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 16px;
  font-size: 13px;
}

.inspector-card h3 {
  margin: 0 0 12px;
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 6px;
}

.inspector-card p {
  margin: 6px 0;
  color: #334155;
}

.inspector-card.raw-data pre {
  margin: 0;
  font-family: monospace;
  font-size: 11px;
  background: #f8fafc;
  padding: 10px;
  border-radius: 6px;
  border: 1px solid #e2e8f0;
  max-height: 160px;
  overflow: auto;
}
</style>
