import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('FDI condition screen exposes one-click suggestions while preserving tooth and surface', async () => {
  const view = await readFile(new URL('../src/views/FdiConditionView.vue', import.meta.url), 'utf8');
  const client = await readFile(new URL('../src/services/fdi.ts', import.meta.url), 'utf8');
  const assignments = await readFile(new URL('../src/services/visitServices.ts', import.meta.url), 'utf8');
  assert.match(view, /Tải gợi ý dịch vụ/);
  assert.match(view, /Chỉ định \{\{ suggestedService\.label \}\}/);
  assert.match(view, /toothNumbers: \[selected\.toothNumber\]/);
  assert.match(view, /surface: selected\.surface \? SURFACE_METADATA\[selected\.surface\]\.code : null/);
  assert.match(view, /Đây là chỉ định, không xác nhận thủ thuật đã thực hiện/);
  assert.match(client, /condition-service-suggestions/);
  assert.match(assignments, /item\.code === code && item\.currentPrice !== null/);
});
