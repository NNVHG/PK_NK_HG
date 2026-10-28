import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
const { descriptor } = parse(await readFile(new URL('../src/views/QueueCheckInView.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'check-in-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText;
const appointment = { appointmentId: 11, patientId: 7, patientName: 'Synthetic patient', patientCode: 'BN000007', slotTime: '09:00:00', status: 'Scheduled', notes: null };
const patient = { patientId: 7, fullName: 'Synthetic patient', patientCode: 'BN000007', phone: '0900000091' };
const page = { items: [appointment], page: 1, pageSize: 20, totalCount: 41, totalPages: 3, hasNextPage: true, hasPreviousPage: false };
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
function fixture({ role = 'RECEPTIONIST', queryPages = [page], deskPages = [page], waitingPages = [{ ...page, items: [] }], rejectQr = false } = {}) {
  let mount, unmount; const watchers = [], calls = { search: [], desk: [], waiting: [], patient: [], appointments: [], debt: [], checkIn: [], lookup: [] };
  const auth = { role, user: { userId: 5 } };
  const imports = {
    vue: { defineComponent: component => component, computed: get => ({ get value() { return get(); } }), ref: value => ({ value }), onMounted: cb => { mount = cb; }, onBeforeUnmount: cb => { unmount = cb; }, watch: (getter, cb) => watchers.push({ getter, cb }) },
    axios: { isAxiosError: error => Boolean(error?.response) },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/qrPatientIntake': {
      decodeCitizenQr: async raw => {
        if (rejectQr) throw new Error('Mã QR không hợp lệ.');
        const parts = raw.split('|');
        return { citizenIdHash: 'HASH:' + parts[0], citizenIdMasked: parts[0].slice(0, 3) + '***', fullName: parts[2] || '', dateOfBirth: '1990-01-01', gender: 'Male', address: '' };
      },
      hashCitizenId: async id => { calls.lookup.push(['hash', id]); return 'HASH:' + id; },
      qrPatientIntakeService: { lookup: async hash => { calls.lookup.push(['lookup', hash]); return { existing: { patientId: 7, isActive: true } }; } }
    },
    '@/services/checkInForm': { validateCheckIn: (id, phone, notes, documentType, nationalityCode) => { const e = {}; if (documentType === 'passport') { if (!/^[A-Za-z0-9]{6,12}$/.test(id.trim())) e.citizenId = 'Số hộ chiếu không hợp lệ.'; if (!/^[A-Za-z]{2,3}$/.test(nationalityCode.trim())) e.nationalityCode = 'Mã quốc gia cấp không hợp lệ.'; } else if (!/^\d{12}$/.test(id.trim())) e.citizenId = 'CCCD phải gồm 12 chữ số.'; if (!/^0\d{9}$/.test(phone.trim())) e.phone = 'Số điện thoại phải gồm 10 chữ số.'; if (notes.length > 500) e.notes = 'Ghi chú không được vượt quá 500 ký tự.'; return e; }, checkInProblem: problem => ({ fields: {}, message: problem?.message ?? 'Không thể tiếp đón.' }) },
    '@/services/appointments': { appointmentsService: {
      getForDesk: async (...args) => { calls.desk.push(args); const value = deskPages.shift() ?? page; if (value instanceof Error) throw value; return value; },
      getScheduledForPatientOnDate: async (...args) => { calls.appointments.push(args); return { ...page, items: [appointment] }; },
    } },
    '@/services/patients': { patientsService: {
      search: async (...args) => { calls.search.push(args); const value = queryPages.shift() ?? page; if (value instanceof Error) throw value; return value; },
      getById: async id => { calls.patient.push(id); return patient; },
    } },
    '@/services/queue': { queueService: {
      getToday: async (...args) => { calls.waiting.push(args); const value = waitingPages.shift() ?? { ...page, items: [] }; if (value instanceof Error) throw value; return value; },
      getOutstandingDebt: async (...args) => { calls.debt.push(args); return { totalOutstandingDebt: 0, invoiceCodes: [] }; },
      checkIn: async payload => { calls.checkIn.push(payload); return { queueNumber: 9, patientName: patient.fullName, queueDate: '2026-10-24', statusText: 'Đang chờ' }; },
    } },
  };
  const module = { exports: {} }; new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup({}, { expose() {} });
  return { c, auth, calls, watchers, mount: () => mount(), unmount: () => unmount() };
}
test('F_CHK_01 loads today appointments and Waiting queue on mount', async () => {
  const f = fixture(); await f.mount();
  assert.deepEqual(f.calls.desk[0].slice(1), [1, 'Scheduled']); assert.deepEqual(f.calls.waiting, [[1, 1]]);
  assert.equal(f.c.deskRows.value.length, 1); assert.equal(f.c.waitingRows.value.length, 0);
});
test('F_CHK_01 paginates appointment and queue panels independently', async () => {
  const f = fixture(); await f.c.loadDesk(2); await f.c.loadWaiting(3);
  assert.deepEqual(f.calls.desk[0].slice(1), [2, 'Scheduled']); assert.deepEqual(f.calls.waiting, [[3, 1]]);
});
test('F_CHK_01 searches name, phone and patient code with server pagination', async () => {
  const f = fixture(); f.c.keyword.value = 'BN000007'; await f.c.searchPatients(2);
  assert.deepEqual(f.calls.search, [[{ keyword: 'BN000007', page: 2, pageSize: 20 }]]); assert.equal(f.c.searchNext.value, true);
});
test('F_CHK_01 hashes CCCD before server lookup and never queries with the raw identifier', async () => {
  const f = fixture(); f.c.keyword.value = '000000000901'; await f.c.searchPatients();
  assert.deepEqual(f.calls.lookup, [['hash', '000000000901'], ['lookup', 'HASH:000000000901']]);
  assert.equal(f.calls.patient[0], 7); assert.equal(f.calls.search.length, 0);
});
test('F_CHK_01 parses raw CCCD QR string, looks up hashed CCCD, selects patient and populates citizenId', async () => {
  const f = fixture(); f.c.keyword.value = '000000000901||Nguyen Van A|01011990|Nam|Ha Noi|01012021'; await f.c.searchPatients();
  assert.equal(f.calls.patient[0], 7);
  assert.equal(f.c.citizenId.value, '000000000901');
});
test('F_CHK_01 rejects invalid CCCD QR locally without sending identifiers in search URL', async () => {
  const f = fixture({ rejectQr: true }); f.c.keyword.value = '000000000901|invalid'; await f.c.searchPatients();
  assert.match(f.c.searchError.value, /Mã QR CCCD không hợp lệ/);
  assert.deepEqual(f.calls.search, []); assert.deepEqual(f.calls.lookup, []);
  assert.equal(f.c.searching.value, false);
});

test('F_CHK_01 debounce reruns the quick search after input changes', async () => {
  const f = fixture(); f.c.keyword.value = 'Synthetic'; f.watchers[0].cb();
  await new Promise(resolve => setTimeout(resolve, 390));
  assert.equal(f.calls.search.length, 1); assert.equal(f.c.searched.value, true);
});
test('F_CHK_01 keeps loading, empty and recoverable error states distinct', async () => {
  const loading = deferred(), f = fixture({ queryPages: [loading.promise] }); f.c.keyword.value = 'Synthetic'; const pending = f.c.searchPatients();
  assert.equal(f.c.searching.value, true); loading.resolve({ ...page, items: [], totalCount: 0 }); await pending;
  assert.equal(f.c.searching.value, false); assert.equal(f.c.searched.value, true); assert.deepEqual(f.c.patients.value, []);
  const failed = fixture({ queryPages: [new Error('Synthetic network failure')] }); failed.c.keyword.value = 'Synthetic'; await failed.c.searchPatients();
  assert.ok(failed.c.searchError.value); assert.equal(failed.c.searched.value, false);
});
test('F_CHK_01 older search response cannot replace the latest result', async () => {
  const first = deferred(), f = fixture({ queryPages: [first.promise, { ...page, items: [{ ...patient, patientId: 8 }], totalCount: 1 }] });
  f.c.keyword.value = 'old'; const a = f.c.searchPatients(); f.c.keyword.value = 'new'; const b = f.c.searchPatients(); await b; first.resolve(page); await a;
  assert.equal(f.c.patients.value[0].patientId, 8);
});
test('F_CHK_01 denies Patient, Dentist and Assistant before list or search requests', async () => {
  for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT']) {
    const f = fixture({ role }); await f.mount(); f.c.keyword.value = 'Synthetic'; await f.c.searchPatients();
    assert.deepEqual(f.calls.desk, []); assert.deepEqual(f.calls.waiting, []); assert.deepEqual(f.calls.search, []); assert.equal(f.c.canCheckIn.value, false);
  }
});
test('F_CHK_01 optional blank reception note submits null and refreshes lists', async () => {
  const f = fixture(); await f.c.selectPatient(patient); f.c.citizenId.value = '000000000901'; f.c.checkInPhone.value = '0900000091';
  await f.c.submitCheckIn(); assert.equal(f.calls.checkIn.length, 1); assert.equal(f.calls.checkIn[0].notes, null);
  assert.equal(f.calls.checkIn[0].patientId, 7); assert.equal(f.c.ticket.value.queueNumber, 9);
});
test('F_CHK_01 missing CCCD or phone is shown per field without sending check-in', async () => {
  const f = fixture(); await f.c.selectPatient(patient); await f.c.submitCheckIn();
  assert.deepEqual(Object.keys(f.c.fieldErrors.value).sort(), ['citizenId', 'phone']); assert.equal(f.calls.checkIn.length, 0);
});
test('F_CHK_01 passport check-in requires country and sends normalized document context', async () => {
  const f = fixture(); await f.c.selectPatient(patient);
  f.c.documentType.value = 'passport'; f.c.onDocumentTypeChanged();
  f.c.citizenId.value = 'B1234567'; f.c.checkInPhone.value = '0900000091';
  await f.c.submitCheckIn();
  assert.ok(f.c.fieldErrors.value.nationalityCode); assert.equal(f.calls.checkIn.length, 0);
  f.c.nationalityCode.value = 'us'; await f.c.submitCheckIn();
  assert.equal(f.calls.checkIn.length, 1);
  assert.equal(f.calls.checkIn[0].documentType, 2);
  assert.equal(f.calls.checkIn[0].nationalityCode, 'US');
  assert.equal(f.c.documentType.value, 'citizenId');
});
test('F_CHK_01 component hides check-in controls from disallowed roles and exposes field errors', () => {
  assert.match(descriptor.template.content, /v-if="canCheckIn"/); assert.match(descriptor.template.content, /aria-invalid/);
  assert.match(descriptor.template.content, /role="status"/); assert.match(descriptor.template.content, /role="alert"/);
});
test('F_CHK_01 refuses patient lookups after unmount', async () => {
  const f = fixture(); f.unmount(); f.c.keyword.value = 'Synthetic'; await f.c.searchPatients();
  assert.deepEqual(f.calls.search, []); assert.deepEqual(f.c.patients.value, []);
});
