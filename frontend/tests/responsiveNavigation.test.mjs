import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse } from '@vue/compiler-sfc';
const source = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
const { descriptor, errors } = parse(source);
const css = descriptor.styles.map(s => s.content).join('\n');
test('root navigation remains native, named and free of new permission logic', () => {
  assert.deepEqual(errors, []);
  assert.match(descriptor.template.content, /<nav class="nav-links" aria-label="Điều hướng chính">/);
  assert.match(descriptor.template.content, /v-if="authStore.role === 'PATIENT'" to="\/appointments"/);
  assert.match(descriptor.template.content, /v-if="isAdmin" to="\/admin\/staff"/);
});
test('header and navigation allow wrapping without breaking words in links', () => {
  assert.match(css, /\.navbar\s*\{[^}]*flex-wrap:\s*wrap/s);
  assert.match(css, /\.nav-links\s*\{[^}]*flex-wrap:\s*wrap/s);
  assert.match(css, /\.nav-links a\s*\{[^}]*white-space:\s*nowrap/s);
});
test('mobile navigation has its own line and main width includes padding', () => {
  assert.match(css, /@media\s*\(max-width:\s*640px\)/);
  assert.match(css, /\.nav-links\s*\{\s*flex-basis:\s*100%/);
  assert.match(css, /\.main-content\s*\{[^}]*box-sizing:\s*border-box/s);
});
test('long names can wrap while logout button retains its width', () => {
  assert.match(css, /\.user-menu span\s*\{[^}]*overflow-wrap:\s*anywhere/);
  assert.match(css, /\.btn-logout\s*\{[^}]*flex-shrink:\s*0/s);
  assert.doesNotMatch(css, /overflow-x:\s*hidden/);
});
test('family appointment filters cannot exceed the narrow content width', async () => {
  const history = await readFile(new URL('../src/views/PatientAppointmentHistoryView.vue', import.meta.url), 'utf8');
  assert.match(history, /select, button\s*\{[^}]*max-width:\s*100%/s);
  assert.match(history, /select, button\s*\{[^}]*margin-left:\s*0/s);
  assert.match(history, /overflow-wrap:\s*anywhere/);
});
test('dashboard grids can shrink below fixed desktop card widths', async () => {
  const dashboard = await readFile(new URL('../src/views/DashboardView.vue', import.meta.url), 'utf8');
  assert.ok(dashboard.includes('minmax(min(100%, 320px), 1fr)'));
  assert.ok(dashboard.includes('minmax(min(100%, 280px), 1fr)'));
});
