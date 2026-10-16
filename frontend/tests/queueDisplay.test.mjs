import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const { descriptor } = parse(await readFile(new URL('../src/views/QueueDisplayView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'queue-display-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

const mockData = {
  inConsultation: [
    {
      queueEntryId: 10,
      queueNumber: 1,
      queueNumberDisplay: 'A-001',
      maskedPatientName: 'Nguyễn V*** An',
      dentistName: 'BS. Lê Văn B',
      status: 2,
      statusText: 'Đang khám',
      isPriority: false,
    },
  ],
  waiting: [
    {
      queueEntryId: 11,
      queueNumber: 2,
      queueNumberDisplay: 'A-002',
      maskedPatientName: 'Trần T*** Bé',
      dentistName: null,
      status: 1,
      statusText: 'Đang chờ',
      isPriority: true,
    },
  ],
};

function fixture(responses = [mockData]) {
  let mount, cleanup;
  const calls = [];
  const intervals = [];
  const clearedIntervals = [];

  const imports = {
    vue: {
      defineComponent: c => c,
      ref: value => ({ value }),
      onMounted: cb => { mount = cb; },
      onUnmounted: cb => { cleanup = cb; },
    },
    '@/services/queue': {
      queueService: {
        getPublicDisplay: async () => {
          calls.push(true);
          const response = responses.shift() ?? mockData;
          if (response instanceof Error) throw response;
          return response;
        },
      },
    },
  };

  const originalSetInterval = globalThis.setInterval;
  const originalClearInterval = globalThis.clearInterval;

  globalThis.setInterval = (fn, ms) => {
    const id = { fn, ms };
    intervals.push(id);
    return id;
  };
  globalThis.clearInterval = id => {
    clearedIntervals.push(id);
  };

  try {
    const module = { exports: {} };
    new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
    const c = module.exports.default.setup({}, { expose() {} });
    return {
      c,
      calls,
      intervals,
      clearedIntervals,
      mount: async () => {
        if (mount) await mount();
      },
      cleanup: () => {
        if (cleanup) cleanup();
        globalThis.setInterval = originalSetInterval;
        globalThis.clearInterval = originalClearInterval;
      },
    };
  } catch (err) {
    globalThis.setInterval = originalSetInterval;
    globalThis.clearInterval = originalClearInterval;
    throw err;
  }
}

test('mount loads public monitor data and populates inConsultation and waiting', async () => {
  const f = fixture();
  try {
    await f.mount();
    assert.equal(f.calls.length, 1);
    assert.equal(f.c.inConsultation.value.length, 1);
    assert.equal(f.c.inConsultation.value[0].queueNumberDisplay, 'A-001');
    assert.equal(f.c.waiting.value.length, 1);
    assert.equal(f.c.waiting.value[0].maskedPatientName, 'Trần T*** Bé');
    assert.equal(f.c.error.value, '');
  } finally {
    f.cleanup();
  }
});

test('sound toggle starts disabled and toggles soundEnabled state', () => {
  const f = fixture();
  try {
    assert.equal(f.c.soundEnabled.value, false);
    f.c.toggleSound();
    assert.equal(f.c.soundEnabled.value, true);
    f.c.toggleSound();
    assert.equal(f.c.soundEnabled.value, false);
  } finally {
    f.cleanup();
  }
});

test('network error sets user friendly message', async () => {
  const f = fixture([new Error('Network error')]);
  try {
    await f.mount();
    assert.ok(f.c.error.value.includes('Mất kết nối'));
  } finally {
    f.cleanup();
  }
});

test('unmount clears polling and clock timers', async () => {
  const f = fixture();
  try {
    await f.mount();
    assert.equal(f.intervals.length, 2);
    f.cleanup();
    assert.equal(f.clearedIntervals.length, 2);
  } finally {
    f.cleanup();
  }
});

test('only Calling is recorded for sound, and ending a call permits later recall', async () => {
  const f = fixture([mockData, { ...mockData, inConsultation: [{ ...mockData.inConsultation[0], status: 6 }] }, mockData,
    { ...mockData, inConsultation: [{ ...mockData.inConsultation[0], status: 6 }] }]);
  try {
    await f.c.loadQueueData(); assert.equal(f.c.seenCallIds.has(10), false);
    await f.c.loadQueueData(); assert.equal(f.c.seenCallIds.has(10), true);
    await f.c.loadQueueData(); assert.equal(f.c.seenCallIds.has(10), false);
    await f.c.loadQueueData(); assert.equal(f.c.seenCallIds.has(10), true);
  } finally { f.cleanup(); }
});

test('late Calling response after unmount cannot restore the monitor or announcement', async () => {
  let done; const pending = new Promise(resolve => { done = resolve; }); const f = fixture([pending]);
  const loading = f.c.loadQueueData(); f.cleanup();
  done({ ...mockData, inConsultation: [{ ...mockData.inConsultation[0], status: 6 }] }); await loading;
  assert.equal(f.c.inConsultation.value.length, 0); assert.equal(f.c.seenCallIds.size, 0); assert.equal(f.c.soundEnabled.value, false);
});


test('location label renders real room and chair with an explicit missing fallback', () => {
  const f = fixture();
  try {
    assert.equal(f.c.locationLabel({ roomName: 'Phòng 3', chairNumber: 'G03' }), 'Phòng 3 · Ghế G03');
    assert.match(f.c.locationLabel({ roomName: null, chairNumber: null }), /Chưa có phòng/);
    assert.equal(f.c.locationLabel({ chairNumber: 'G04' }), 'Ghế G04');
  } finally { f.cleanup(); }
});

test('announcement destination is actual room and chair, never the dentist name', () => {
  const f = fixture();
  try {
    f.c.enqueueCallAudio({ ...mockData.inConsultation[0], roomName: 'Phòng 3', chairNumber: 'G03' });
    assert.equal(f.c.audioQueue[0].destination, 'Phòng 3, ghế G03');
    assert.equal(f.c.audioQueue[0].patientName, 'Nguyễn V*** An');
    assert.ok(!f.c.audioQueue[0].destination.includes('BS.'));
  } finally { f.cleanup(); }
});

test('missing location announcement directs patient to reception without inventing a room', () => {
  const f = fixture();
  try {
    f.c.enqueueCallAudio(mockData.inConsultation[0]);
    assert.equal(f.c.audioQueue[0].destination, 'khu vực tiếp đón để được hướng dẫn');
  } finally { f.cleanup(); }
});

test('location refresh keeps Calling identity and does not repeat the announcement', async () => {
  const call = { ...mockData.inConsultation[0], status: 6, roomName: 'Phòng 3', chairNumber: 'G03' };
  const f = fixture([{ inConsultation: [call], waiting: [] }, { inConsultation: [{ ...call, roomName: 'Phòng 4' }], waiting: [] }]);
  try {
    await f.c.loadQueueData(); f.c.soundEnabled.value = true;
    await f.c.loadQueueData();
    assert.equal(f.c.inConsultation.value[0].roomName, 'Phòng 4');
    assert.equal(f.c.audioQueue.length, 0);
  } finally { f.cleanup(); }
});

test('TV template renders dedicated location and TTS uses destination text', async () => {
  const source = await readFile(new URL('../src/views/QueueDisplayView.vue', import.meta.url), 'utf8');
  assert.match(source, /class="location-info">{{ locationLabel\(item\) }}/);
  assert.ok(source.includes('${item.destination}'));
  assert.ok(!source.includes('phòng khám ${item.roomName}'));
});
