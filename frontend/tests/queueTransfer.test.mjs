import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/DailyQueueView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'queue-transfer' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

const defaultRow = {
  queueEntryId: 10,
  status: 1,
  dentistId: 5,
  dentistName: 'Synthetic dentist A',
  queueNumber: 12,
  patientName: 'Synthetic patient',
  patientCode: 'PAT001',
  queueDate: '2026-10-20',
  visitId: 100,
  canCancelConsultation: false,
};

const inProgressRow = {
  queueEntryId: 11,
  status: 2,
  dentistId: 5,
  dentistName: 'Synthetic dentist A',
  queueNumber: 14,
  patientName: 'Synthetic patient B',
  patientCode: 'PAT002',
  queueDate: '2026-10-20',
  visitId: 101,
  canCancelConsultation: true,
};

function deferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

function fixture({ role = 'ADMIN', userId = 1, selected = defaultRow, fail = false, result, readFailureAfter = Infinity } = {}) {
  let cleanup, mount;
  const reads = [], writes = [];
  const auth = { role, user: { userId } };
  const mockDentists = [
    { dentistId: 5, dentistName: 'Synthetic dentist A' },
    { dentistId: 6, dentistName: 'Synthetic dentist B' },
    { dentistId: 7, dentistName: 'Synthetic dentist C' },
  ];
  const imports = {
    vue: {
      defineComponent: c => c,
      ref: value => ({ value }),
      computed: get => ({ get value() { return get(); } }),
      onMounted: cb => { mount = cb; },
      onUnmounted: cb => { cleanup = cb; }
    },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/appointments': {
      appointmentsService: {
        getBookingDentists: async () => mockDentists
      }
    },
    '@/services/queue': {
      queueService: {
        getCallingDentists: async () => mockDentists,
        getToday: async () => {
          reads.push(true);
          if (reads.length > readFailureAfter) throw new Error('Read failure');
          return { items: [selected], page: 1, totalCount: 1, hasNextPage: false };
        },
        transferDentist: async (id, payload) => {
          writes.push({ id, payload });
          if (fail) throw { response: { data: { message: 'Transfer failed' } } };
          return result ?? {
            ...selected,
            dentistId: payload.targetDentistId,
            dentistName: 'Synthetic dentist B',
            roomName: 'P1',
            chairNumber: 'G1'
          };
        },
        changeAbsence: async () => selected,
        callNext: async () => selected,
      }
    },
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, reads, writes, auth, mount: () => mount(), cleanup: () => cleanup() };
}

test('canTransfer allows Waiting (status 1) and active consultation with canCancelConsultation', async () => {
  const f = fixture({ role: 'ADMIN' });
  await f.mount();
  assert.equal(f.c.canTransfer(defaultRow), true);
  assert.equal(f.c.canTransfer(inProgressRow), true);
  assert.equal(f.c.canTransfer({ ...defaultRow, status: 0 }), false);
  assert.equal(f.c.canTransfer({ ...inProgressRow, canCancelConsultation: false }), false);
});

test('openTransfer and closeTransfer toggle modal and preserve state', async () => {
  const f = fixture({ role: 'ADMIN' });
  await f.mount();
  f.c.openTransfer(defaultRow);
  assert.deepEqual(f.c.transferRow.value, defaultRow);
  assert.equal(f.c.transferConfirmed.value, false);
  assert.equal(f.c.transferDentistId.value, 0);

  f.c.transferReason.value = 'Hội chẩn';
  f.c.closeTransfer();
  assert.equal(f.c.transferRow.value, null);
  assert.equal(f.c.transferReason.value, '');
});

test('handleTransfer requires confirmation and valid target dentist', async () => {
  const f = fixture({ role: 'ADMIN' });
  await f.mount();
  f.c.openTransfer(defaultRow);

  f.c.transferDentistId.value = 6;
  await f.c.handleTransfer();
  assert.equal(f.writes.length, 0);

  f.c.transferConfirmed.value = true;
  f.c.transferDentistId.value = 5;
  await f.c.handleTransfer();
  assert.equal(f.writes.length, 0);

  f.c.transferDentistId.value = 6;
  f.c.transferReason.value = 'Chuyển bác sĩ điều trị';
  await f.c.handleTransfer();
  assert.equal(f.writes.length, 1);
  assert.equal(f.writes[0].id, defaultRow.queueEntryId);
  assert.equal(f.writes[0].payload.targetDentistId, 6);
  assert.equal(f.writes[0].payload.expectedDentistId, 5);
  assert.equal(f.writes[0].payload.expectedStatus, 1);
  assert.equal(f.writes[0].payload.confirmed, true);
  assert.equal(f.writes[0].payload.reason, 'Chuyển bác sĩ điều trị');
  assert.equal(f.c.transferRow.value, null);
});

test('handleTransfer error sets needsReview and resets confirmation', async () => {
  const f = fixture({ role: 'ADMIN', fail: true });
  await f.mount();
  f.c.openTransfer(defaultRow);
  f.c.transferDentistId.value = 6;
  f.c.transferConfirmed.value = true;

  await f.c.handleTransfer();
  assert.equal(f.writes.length, 1);
  assert.equal(f.c.transferConfirmed.value, false);
  assert.equal(f.c.needsReview.value, true);
  assert.ok(f.c.error.value.length > 0);
});

test('double submit while transferring writes only once', async () => {
  const d = deferred();
  const f = fixture({ role: 'ADMIN', result: d.promise });
  await f.mount();
  f.c.openTransfer(defaultRow);
  f.c.transferDentistId.value = 6;
  f.c.transferConfirmed.value = true;

  const promise1 = f.c.handleTransfer();
  const promise2 = f.c.handleTransfer();
  assert.equal(f.writes.length, 1);

  d.resolve({
    ...defaultRow,
    dentistId: 6,
    dentistName: 'Synthetic dentist B',
    roomName: 'P1',
    chairNumber: 'G1'
  });
  await Promise.all([promise1, promise2]);
});

test('unmount clears transfer state and cancels in-flight transfer response', async () => {
  const d = deferred();
  const f = fixture({ role: 'ADMIN', result: d.promise });
  await f.mount();
  f.c.openTransfer(defaultRow);
  f.c.transferDentistId.value = 6;
  f.c.transferConfirmed.value = true;

  const pending = f.c.handleTransfer();
  f.cleanup();
  assert.equal(f.c.transferRow.value, null);

  d.resolve({
    ...defaultRow,
    dentistId: 6,
    dentistName: 'Synthetic dentist B',
    roomName: 'P1',
    chairNumber: 'G1'
  });
  await pending;
  assert.equal(f.c.actionMsg.value, '');
});


test('only ADMIN, RECEPTIONIST, and assigned DENTIST can initiate transfer', async () => {
  for (const [role, userId, expectedCan] of [
    ['ADMIN', 1, true],
    ['RECEPTIONIST', 2, true],
    ['DENTIST', 5, true],
    ['DENTIST', 6, false],
    ['ASSISTANT', 3, false],
    ['PATIENT', 4, false],
  ]) {
    const f = fixture({ role, userId });
    await f.mount();
    assert.equal(f.c.canTransfer(defaultRow), expectedCan, `Role ${role} with userId ${userId}`);
  }
});
