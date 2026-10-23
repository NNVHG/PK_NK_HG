import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const source = await readFile(new URL('../src/views/QueueCheckInView.vue', import.meta.url), 'utf8');
const { descriptor } = parse(source);
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'checkin-debt-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
});

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

function fixture({ debt = async () => ({ totalOutstandingDebt: 0, invoiceCodes: [] }), appointment = async () => ({ items: [] }), role = 'RECEPTIONIST' } = {}) {
  let unmount;
  const debtRequests = [], checkIns = [], auth = { user: { userId: 2 }, role };
  const imports = {
    vue: {
      defineComponent: component => component,
      ref: value => ({ value }),
      computed: getter => ({ get value() { return getter(); } }),
      watch: () => {},
      onMounted: () => {},
      onBeforeUnmount: callback => { unmount = callback; }
    },
    axios: { isAxiosError: error => Boolean(error?.isAxiosError) },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/appointments': {
      appointmentsService: {
        getScheduledForPatientOnDate: (...args) => appointment(...args),
        getForDesk: async () => ({ items: [], totalCount: 0, hasNextPage: false })
      }
    },
    '@/services/patients': {
      patientsService: {
        search: async () => ({ items: [] }),
        getById: async patient => patient
      }
    },
    '@/services/qrPatientIntake': {
      hashCitizenId: async () => 'A'.repeat(64),
      qrPatientIntakeService: { lookup: async () => ({ existing: null }) }
    },
    '@/services/checkInForm': {
      validateCheckIn: (citizenId, phone, notes) => ({
        ...(/^\d{12}$/.test(citizenId) ? {} : { citizenId: 'CCCD không hợp lệ' }),
        ...(/^0\d{9}$/.test(phone) ? {} : { phone: 'Số điện thoại không hợp lệ' }),
        ...(notes.length <= 500 ? {} : { notes: 'Ghi chú quá dài' })
      }),
      checkInProblem: () => ({ fields: {}, message: 'Không thể xác nhận tiếp đón.' })
    },
    '@/services/queue': {
      queueService: {
        getOutstandingDebt: async patientId => { debtRequests.push(patientId); return debt(patientId); },
        getToday: async () => ({ items: [], totalCount: 0, hasNextPage: false }),
        checkIn: async payload => { checkIns.push(payload); return { queueNumber: 7, patientName: 'Synthetic patient' }; }
      }
    }
  };
  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled.outputText)(name => imports[name] ?? {}, module, module.exports);
  const component = module.exports.default.setup({}, { expose() {} });
  return { component, debtRequests, checkIns, auth, unmount: () => unmount?.() };
}

const patient = patientId => ({ patientId, patientCode: 'BN' + String(patientId).padStart(6, '0'), fullName: 'Synthetic ' + patientId, phone: '090000000' + patientId });

function confirmIdentity(f, patientId) {
  f.component.citizenId.value = '123456789012';
  f.component.checkInPhone.value = patient(patientId).phone;
}

test('loads and exposes outstanding balance and invoice codes for selected patient', async () => {
  const f = fixture({ debt: async () => ({ totalOutstandingDebt: 1200, invoiceCodes: ['INV-OLD-001', 'INV-OLD-002'] }) });
  await f.component.selectPatient(patient(1));
  assert.deepEqual(f.debtRequests, [1]);
  assert.deepEqual(f.component.outstandingDebt.value, { totalOutstandingDebt: 1200, invoiceCodes: ['INV-OLD-001', 'INV-OLD-002'] });
  assert.equal(f.component.debtLookupFailed.value, false);
  assert.match(source, /Bệnh nhân còn nợ cũ/);
  assert.match(source, /invoiceCode in outstandingDebt\.invoiceCodes/);
});

test('zero debt does not create a warning and still permits check-in', async () => {
  const f = fixture();
  await f.component.selectPatient(patient(2));
  assert.equal(f.component.outstandingDebt.value.totalOutstandingDebt, 0);
  confirmIdentity(f, 2);
  await f.component.submitCheckIn();
  assert.equal(f.checkIns.length, 1);
  assert.equal(f.checkIns[0].patientId, 2);
});

test('debt lookup failure is explicit, retryable, and does not block check-in', async () => {
  let attempt = 0;
  const f = fixture({ debt: async () => {
    attempt++;
    if (attempt === 1) throw new Error('Synthetic lookup failure');
    return { totalOutstandingDebt: 500, invoiceCodes: ['INV-RETRY'] };
  } });
  await f.component.selectPatient(patient(3));
  assert.equal(f.component.debtLookupFailed.value, true);
  await f.component.loadOutstandingDebt(patient(3));
  assert.equal(f.component.debtLookupFailed.value, false);
  assert.equal(f.component.outstandingDebt.value.invoiceCodes[0], 'INV-RETRY');
  confirmIdentity(f, 3);
  await f.component.submitCheckIn();
  assert.equal(f.checkIns.length, 1);
});

test('late debt response for an earlier patient cannot replace the selected patient summary', async () => {
  const old = deferred();
  const f = fixture({ debt: patientId => patientId === 4 ? old.promise : Promise.resolve({ totalOutstandingDebt: 900, invoiceCodes: ['INV-CURRENT'] }) });
  const oldSelection = f.component.selectPatient(patient(4));
  await f.component.selectPatient(patient(5));
  old.resolve({ totalOutstandingDebt: 100, invoiceCodes: ['INV-STALE'] });
  await oldSelection;
  assert.equal(f.component.selectedPatient.value.patientId, 5);
  assert.deepEqual(f.component.outstandingDebt.value, { totalOutstandingDebt: 900, invoiceCodes: ['INV-CURRENT'] });
});

test('reset and unmount clear debt data and invalidate pending lookups', async () => {
  const pending = deferred();
  const f = fixture({ debt: () => pending.promise });
  const loading = f.component.selectPatient(patient(6));
  f.component.resetForm();
  pending.resolve({ totalOutstandingDebt: 700, invoiceCodes: ['INV-CLEARED'] });
  await loading;
  assert.equal(f.component.outstandingDebt.value, null);
  assert.equal(f.component.selectedPatient.value, null);
  const later = f.component.selectPatient(patient(7));
  f.unmount();
  pending.resolve({ totalOutstandingDebt: 700, invoiceCodes: ['INV-CLEARED'] });
  await later;
  assert.equal(f.component.outstandingDebt.value, null);
  assert.equal(f.component.selectedPatient.value, null);
});

test('roles without check-in permission cannot load patient debt or create a queue entry', async () => {
  const f = fixture({ role: 'PATIENT' });
  await f.component.selectPatient(patient(8));
  confirmIdentity(f, 8);
  await f.component.submitCheckIn();
  assert.equal(f.debtRequests.length, 0);
  assert.equal(f.checkIns.length, 0);
});
