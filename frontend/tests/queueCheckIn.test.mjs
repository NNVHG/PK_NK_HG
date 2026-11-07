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
function deferred() { let resolve, reject; const promise = new Promise((done, fail) => { resolve = done; reject = fail; }); return { promise, resolve, reject }; }
function fixture({ role = 'RECEPTIONIST', queryPages = [page], deskPages = [page], waitingPages = [{ ...page, items: [] }], rejectQr = false, decode, lookup, getPatient, debt, appointments } = {}) {
  let mount, unmount; const watchers = [], calls = { search: [], desk: [], waiting: [], patient: [], appointments: [], debt: [], checkIn: [], lookup: [] };
  const auth = { role, user: { userId: 5 } };
  const imports = {
    vue: { defineComponent: component => component, computed: get => ({ get value() { return get(); } }), ref: value => ({ value }), nextTick: cb => Promise.resolve().then(cb), onMounted: cb => { mount = cb; }, onBeforeUnmount: cb => { unmount = cb; }, watch: (getter, cb) => watchers.push({ getter, cb }) },
    axios: { isAxiosError: error => Boolean(error?.response) },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/qrPatientIntake': {
      decodeCitizenQr: async raw => {
        if (decode) return decode(raw);
        if (rejectQr) throw new Error('Mã QR không hợp lệ.');
        const parts = raw.split('|');
        return { citizenId: parts[0], citizenIdMasked: parts[0].slice(0, 3) + '***', fullName: parts[2] || '', dateOfBirth: '1990-01-01', gender: 'Male', address: '' };
      },
      qrPatientIntakeService: { lookup: async citizenId => { calls.lookup.push(['lookup', citizenId]); return lookup ? lookup(citizenId) : { existing: { patientId: 7, isActive: true } }; } }
    },
    '@/services/checkInForm': { validateCheckIn: (id, phone, notes, documentType, nationalityCode) => { const e = {}; if (documentType === 'passport') { if (!/^[A-Za-z0-9]{6,12}$/.test(id.trim())) e.citizenId = 'Số hộ chiếu không hợp lệ.'; if (!/^[A-Za-z]{2,3}$/.test(nationalityCode.trim())) e.nationalityCode = 'Mã quốc gia cấp không hợp lệ.'; } else if (!/^\d{12}$/.test(id.trim())) e.citizenId = 'CCCD phải gồm 12 chữ số.'; if (!/^0\d{9}$/.test(phone.trim())) e.phone = 'Số điện thoại phải gồm 10 chữ số.'; if (notes.length > 500) e.notes = 'Ghi chú không được vượt quá 500 ký tự.'; return e; }, checkInProblem: problem => ({ fields: {}, message: problem?.message ?? 'Không thể tiếp đón.' }) },
    '@/services/appointments': { appointmentsService: {
      getForDesk: async (...args) => { calls.desk.push(args); const value = deskPages.shift() ?? page; if (value instanceof Error) throw value; return value; },
      getScheduledForPatientOnDate: async (...args) => { calls.appointments.push(args); return appointments ? appointments(...args) : { ...page, items: [appointment] }; },
    } },
    '@/services/patients': { patientsService: {
      search: async (...args) => { calls.search.push(args); const value = queryPages.shift() ?? page; if (value instanceof Error) throw value; return value; },
      getById: async id => { calls.patient.push(id); return getPatient ? getPatient(id) : patient; },
    } },
    '@/services/queue': { queueService: {
      getToday: async (...args) => { calls.waiting.push(args); const value = waitingPages.shift() ?? { ...page, items: [] }; if (value instanceof Error) throw value; return value; },
      getOutstandingDebt: async (...args) => { calls.debt.push(args); return debt ? debt(...args) : { totalOutstandingDebt: 0, invoiceCodes: [] }; },
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
test('F_CHK_01 sends CCCD only in authenticated lookup body, not search URL', async () => {
  const f = fixture(); f.c.keyword.value = '000000000901'; await f.c.searchPatients();
  assert.deepEqual(f.calls.lookup, [['lookup', '000000000901']]);
  assert.equal(f.calls.patient[0], 7); assert.equal(f.calls.search.length, 0);
});
test('F_CHK_01 parses QR locally, looks up CCCD, selects patient and populates citizenId', async () => {
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

test('QR success selects one patient and clears raw input after lookup', async () => {
  const f = fixture(); f.c.openQr(); f.c.qrRaw.value = '000000000901';
  await f.c.onQrSubmit();
  assert.equal(f.c.selectedPatient.value.patientId, 7);
  assert.equal(f.c.citizenId.value, '000000000901');
  assert.equal(f.c.checkInPhone.value, patient.phone);
  assert.equal(f.c.qrOpened.value, false);
  assert.equal(f.c.qrRaw.value, '');
});

test('closing QR during decode prevents lookup and form writes', async () => {
  const pending = deferred(), f = fixture({ decode: () => pending.promise });
  f.c.openQr(); const operation = f.c.processQrString('synthetic|qr');
  f.c.closeQr(); pending.resolve({ citizenId: '000000000901' }); await operation;
  assert.deepEqual(f.calls.lookup, []);
  assert.equal(f.c.selectedPatient.value, null); assert.equal(f.c.citizenId.value, '');
});

test('closing QR during lookup prevents patient fetch and form writes', async () => {
  const pending = deferred(), f = fixture({ lookup: () => pending.promise });
  f.c.openQr(); const operation = f.c.processQrString('000000000901');
  f.c.closeQr(); pending.resolve({ existing: { patientId: 7, isActive: true } }); await operation;
  assert.deepEqual(f.calls.patient, []);
  assert.equal(f.c.selectedPatient.value, null); assert.equal(f.c.citizenId.value, '');
});

test('late patient fetch after cancel does not repopulate identity or debt', async () => {
  const pending = deferred(), reached = deferred();
  const f = fixture({ getPatient: () => { reached.resolve(); return pending.promise; } });
  f.c.openQr(); const operation = f.c.processQrString('000000000901'); await reached.promise;
  f.c.closeQr(); pending.resolve(patient); await operation;
  assert.equal(f.c.selectedPatient.value, null); assert.equal(f.c.citizenId.value, '');
  assert.equal(f.c.checkInPhone.value, ''); assert.deepEqual(f.calls.debt, []);
});

test('identity changes and unmount clear QR state and ignore pending requests', async () => {
  for (const change of ['user', 'role', 'unmount']) {
    const pending = deferred(), f = fixture({ lookup: () => pending.promise });
    f.c.openQr(); f.c.qrRaw.value = 'synthetic raw';
    const operation = f.c.processQrString('000000000901');
    if (change === 'unmount') f.unmount();
    else {
      if (change === 'user') f.auth.user.userId++;
      else f.auth.role = 'PATIENT';
      f.watchers[1].cb();
    }
    pending.resolve({ existing: { patientId: 7, isActive: true } }); await operation;
    assert.equal(f.c.qrOpened.value, false); assert.equal(f.c.qrRaw.value, '');
    assert.equal(f.c.selectedPatient.value, null); assert.equal(f.c.citizenId.value, '');
    assert.deepEqual(f.calls.patient, []);
  }
});

test('manual patient selection invalidates a pending QR lookup', async () => {
  const pending = deferred(), f = fixture({ lookup: () => pending.promise });
  f.c.openQr(); const operation = f.c.processQrString('000000000901');
  await f.c.selectPatient({ ...patient, patientId: 8, fullName: 'Current synthetic patient' });
  pending.resolve({ existing: { patientId: 7, isActive: true } }); await operation;
  assert.equal(f.c.selectedPatient.value.patientId, 8); assert.equal(f.c.citizenId.value, '');
  assert.deepEqual(f.calls.patient, []);
});

test('the latest scan wins when an earlier lookup finishes last', async () => {
  const pending = deferred();
  const f = fixture({ lookup: id => id === '000000000901' ? pending.promise : { existing: { patientId: 8, isActive: true } }, getPatient: id => ({ ...patient, patientId: id }) });
  f.c.openQr(); const earlier = f.c.processQrString('000000000901');
  await f.c.processQrString('000000000902');
  pending.resolve({ existing: { patientId: 7, isActive: true } }); await earlier;
  assert.equal(f.c.selectedPatient.value.patientId, 8); assert.equal(f.c.citizenId.value, '000000000902');
  assert.deepEqual(f.calls.patient, [8]);
});

test('late rejected QR lookup cannot overwrite a reopened dialog', async () => {
  const pending = deferred(), f = fixture({ lookup: () => pending.promise });
  f.c.openQr(); const operation = f.c.processQrString('000000000901');
  f.c.closeQr(); f.c.openQr();
  pending.reject(new Error('Synthetic failure')); await operation;
  assert.equal(f.c.qrOpened.value, true); assert.equal(f.c.qrError.value, '');
});

test('identity change during QR dependent debt load prevents repopulation', async () => {
  const pending = deferred(), reached = deferred();
  const f = fixture({ debt: () => { reached.resolve(); return pending.promise; } });
  f.c.openQr(); const operation = f.c.processQrString('000000000901'); await reached.promise;
  f.auth.user.userId++; f.watchers[1].cb();
  pending.resolve({ totalOutstandingDebt: 1000, invoiceCodes: ['SYNTHETIC-OLD'] }); await operation;
  assert.equal(f.c.selectedPatient.value, null); assert.equal(f.c.citizenId.value, '');
  assert.equal(f.c.checkInPhone.value, ''); assert.equal(f.c.outstandingDebt.value, null);
});

test('quick search QR scan does not repopulate identity after identity change or unmount during debt load', async () => {
  const pending = deferred(), reached = deferred();
  const f = fixture({ debt: () => { reached.resolve(); return pending.promise; } });
  f.c.keyword.value = '000000000901||Nguyen Van A|01011990|Nam|Ha Noi|01012021';
  const op = f.c.searchPatients();
  await reached.promise;
  f.auth.user.userId++;
  f.watchers[1].cb();
  pending.resolve({ totalOutstandingDebt: 500, invoiceCodes: ['INV-STALE'] });
  await op;
  assert.equal(f.c.selectedPatient.value, null);
  assert.equal(f.c.citizenId.value, '');
  assert.equal(f.c.checkInPhone.value, '');
});

test('late quick search QR response does not overwrite patient selected from appointment desk', async () => {
  const pendingA = deferred();
  const reachedA = deferred();
  const patientA = { patientId: 7, fullName: 'Patient A', patientCode: 'BN000007', phone: '0900000071' };
  const patientB = { patientId: 8, fullName: 'Patient B', patientCode: 'BN000008', phone: '0900000081' };
  const deskAppointmentB = { appointmentId: 22, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '10:00:00', status: 'Scheduled', notes: null };
  const f = fixture({
    deskPages: [{ items: [deskAppointmentB], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    lookup: () => ({ existing: { patientId: 7, isActive: true } }),
    getPatient: id => {
      if (id === 7) {
        reachedA.resolve();
        return pendingA.promise;
      }
      if (id === 8) return patientB;
      return patient;
    },
    appointments: () => ({ items: [deskAppointmentB], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false })
  });

  await f.mount();
  assert.equal(f.c.deskRows.value.length, 1);
  assert.equal(f.c.deskRows.value[0].patientId, 8);

  f.c.keyword.value = '000000000701||Patient A|01011990|Nam|Ha Noi|01012021';
  const searchPromise = f.c.searchPatients();

  // Barrier: wait until getById(7) has strictly been reached and is in flight
  await reachedA.promise;
  assert.equal(f.c.searching.value, true);
  assert.equal(f.c.selecting.value, false);

  const selectDeskPromise = f.c.selectFromDesk(f.c.deskRows.value[0]);
  await selectDeskPromise;

  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.selectedPatient.value?.fullName, 'Patient B');

  pendingA.resolve(patientA);
  await searchPromise;

  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.selectedPatient.value?.fullName, 'Patient B');
  assert.equal(f.c.citizenId.value, '');
  assert.notEqual(f.c.checkInPhone.value, patientA.phone);
  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.searching.value, false);
  assert.equal(f.c.canCheckIn.value, true);
  assert.equal(f.c.appointmentId.value, 22);
});

test('selectFromDesk normal success resets selecting to false and preserves non-blocked selection controls', async () => {
  const patientB = { patientId: 8, fullName: 'Patient B', patientCode: 'BN000008', phone: '0900000081' };
  const deskAppointment = { appointmentId: 22, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '10:00:00', status: 'Scheduled', notes: null };
  const f = fixture({
    deskPages: [{ items: [deskAppointment], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    getPatient: () => patientB,
    appointments: () => ({ items: [deskAppointment], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false })
  });

  await f.mount();
  assert.equal(f.c.selecting.value, false);

  await f.c.selectFromDesk(deskAppointment);

  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.appointmentId.value, 22);
});

test('selectFromDesk with multiple appointments preserves exactly clicked appointmentId', async () => {
  const patientB = { patientId: 8, fullName: 'Patient B', patientCode: 'BN000008', phone: '0900000081' };
  const appt99 = { appointmentId: 99, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '08:00:00', status: 'Scheduled', notes: null };
  const appt11 = { appointmentId: 11, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '14:00:00', status: 'Scheduled', notes: null };
  const f = fixture({
    deskPages: [{ items: [appt99, appt11], page: 1, pageSize: 20, totalCount: 2, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    getPatient: () => patientB,
    appointments: () => ({ items: [appt99, appt11], page: 1, pageSize: 20, totalCount: 2, totalPages: 1, hasNextPage: false, hasPreviousPage: false })
  });

  await f.mount();
  assert.equal(f.c.selecting.value, false);

  // User explicitly clicks appointment 11
  await f.c.selectFromDesk(appt11);

  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.appointmentId.value, 11);
});

test('quick search QR success clears searching and does not leave search indicator active', async () => {
  const patientA = { patientId: 7, fullName: 'Patient A', patientCode: 'BN000007', phone: '0900000071' };
  const f = fixture({
    lookup: () => ({ existing: { patientId: 7, isActive: true } }),
    getPatient: () => patientA
  });

  f.c.keyword.value = '000000000701||Patient A|01011990|Nam|Ha Noi|01012021';
  await f.c.searchPatients();

  assert.equal(f.c.searching.value, false);
  assert.equal(f.c.searchError.value, '');
  assert.equal(f.c.selectedPatient.value?.patientId, 7);
  assert.equal(f.c.citizenId.value, '000000000701');
});

test('selectFromDesk network failure resets selecting to false and sets searchError', async () => {
  const deskAppointment = { appointmentId: 22, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '10:00:00', status: 'Scheduled', notes: null };
  const f = fixture({
    deskPages: [{ items: [deskAppointment], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    getPatient: () => { throw new Error('Network error'); }
  });

  await f.mount();
  assert.equal(f.c.selecting.value, false);

  await f.c.selectFromDesk(deskAppointment);

  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.selectedPatient.value, null);
  assert.equal(f.c.searchError.value, 'Không tải được hồ sơ. Vui lòng thử lại.');
});

test('subsequent patient searches and selections work cleanly after quick search A is superseded by desk appointment B', async () => {
  const pendingA = deferred();
  const patientA = { patientId: 7, fullName: 'Patient A', patientCode: 'BN000007', phone: '0900000071' };
  const patientB = { patientId: 8, fullName: 'Patient B', patientCode: 'BN000008', phone: '0900000081' };
  const patientC = { patientId: 9, fullName: 'Patient C', patientCode: 'BN000009', phone: '0900000091' };
  const deskApptB = { appointmentId: 22, patientId: 8, patientName: 'Patient B', patientCode: 'BN000008', slotTime: '10:00:00', status: 'Scheduled', notes: null };
  const f = fixture({
    deskPages: [{ items: [deskApptB], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    queryPages: [{ items: [patientC], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }],
    lookup: () => ({ existing: { patientId: 7, isActive: true } }),
    getPatient: id => {
      if (id === 7) return pendingA.promise;
      if (id === 8) return patientB;
      if (id === 9) return patientC;
      return patient;
    },
    appointments: id => ({ items: [{ appointmentId: id === 8 ? 22 : 33, patientId: id, patientName: 'Patient', patientCode: 'BN', slotTime: '10:00:00', status: 'Scheduled', notes: null }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false })
  });

  await f.mount();

  // 1. Initiate search A
  f.c.keyword.value = '000000000701||Patient A|01011990|Nam|Ha Noi|01012021';
  const searchPromiseA = f.c.searchPatients();

  // 2. Desk pick B supersedes search A
  await f.c.selectFromDesk(deskApptB);
  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.searching.value, false);

  // 3. Late resolve A
  pendingA.resolve(patientA);
  await searchPromiseA;
  assert.equal(f.c.selectedPatient.value?.patientId, 8);
  assert.equal(f.c.searching.value, false);

  // 4. Subsequent search C works without being blocked
  f.c.keyword.value = 'Patient C';
  await f.c.searchPatients();
  assert.equal(f.c.searching.value, false);
  assert.equal(f.c.patients.value.length, 1);
  assert.equal(f.c.patients.value[0].patientId, 9);

  // 5. Select C
  await f.c.selectPatient(f.c.patients.value[0]);
  assert.equal(f.c.selectedPatient.value?.patientId, 9);
  assert.equal(f.c.selecting.value, false);
  assert.equal(f.c.searching.value, false);
});
