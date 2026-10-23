import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';
import { parse, compileScript } from '@vue/compiler-sfc';

function execute(source, imports) {
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const module = { exports: {} };
  new Function('require', 'module', 'exports', js)(name => imports[name] ?? {}, module, module.exports);
  return module.exports;
}

const guest = await readFile(new URL('../src/views/GuestBookingView.vue', import.meta.url), 'utf8');
const review = await readFile(new URL('../src/views/BookingRequestReviewView.vue', import.meta.url), 'utf8');
const catalog = () => ({ today: '2099-01-01', slots: ['08:00', '08:30'], durationMinutes: 30, revision: 'A'.repeat(64), dentists: [{ dentistId: 5, fullName: 'Synthetic dentist' }] });
const owner = { patientId: 8, patientCode: 'BN000008', fullName: 'Synthetic owner', dateOfBirth: '1990-01-01', gender: 'Other', phone: '0967000000' };

function fixture(role = '', staff = false) {
  const hooks = {}, auth = { role, user: role ? { userId: 2, phone: '0967000000' } : null }, calls = [];
  let cat = catalog, mine = () => owner, write = body => ({ requestId: body.clientRequestId, status: 'Pending' }), reject = () => {}, list = () => ({ items: [{ id: 3, version: 1, fullName: 'Synthetic', phone: '0967000000', dateOfBirth: '1990-01-01', appointmentDate: '2099-01-02', slotTime: '08:00:00', isReturning: true }], page: 1, pageSize: 20, hasNextPage: false });
  const component = execute(compileScript(parse(staff ? review : guest).descriptor, { id: 'booking-test' }).content, {
    vue: { defineComponent: x => x, ref: value => ({ value }), reactive: x => x, computed: f => ({ get value() { return f(); } }), watch: (_g, fn) => { hooks.watch = fn; }, onMounted: fn => { hooks.mount = fn; }, onUnmounted: fn => { hooks.unmount = fn; } },
    axios: { isAxiosError: e => !!e?.response },
    '@/stores/auth': { useAuthStore: () => auth },
    '@/services/patients': { patientsService: { getMyPatient: async () => await mine() } },
    '@/services/bookingRequests': { bookingRequestsService: {
      catalog: async () => await cat(),
      submit: async body => { calls.push(['submit', structuredClone(body)]); return await write(body); },
      list: async p => { calls.push(['list', p]); return await list(); },
      reject: async (...args) => { calls.push(['reject', ...args]); return await reject(); }
    } }
  });
  const c = component.default.setup({}, { expose() {} });
  return { c, hooks, auth, calls, setCatalog: f => { cat = f; }, setMine: f => { mine = f; }, setWrite: f => { write = f; }, setReject: f => { reject = f; }, setList: f => { list = f; } };
}

async function ready(role = '') {
  const f = fixture(role);
  await f.c.load();
  Object.assign(f.c.form, { fullName: 'Synthetic guest', phone: '0967000000', dateOfBirth: '1990-01-01', gender: 'Other', appointmentDate: '2099-01-02', slot: '08:00' });
  f.c.confirmed.value = true;
  return f;
}

function deferred() {
  let resolve;
  const promise = new Promise(done => { resolve = done; });
  return { promise, resolve };
}

test('guest submits a confirmed booking without login or OTP and keeps the submitted slot frozen', async () => {
  const f = await ready(), pending = deferred();
  f.setWrite(() => pending.promise);
  const submit = f.c.submit();
  const body = f.calls[0][1];
  f.c.form.slot = '08:30';
  assert.equal(body.intent.slotTime, '08:00:00');
  assert.equal(body.intent.patientId, null);
  assert.equal(body.confirmed, true);
  pending.resolve({ requestId: body.clientRequestId, status: 'Pending' });
  await submit;
  assert.equal(f.calls.length, 1);
  assert.equal(f.c.receipt.value.status, 'Pending');
  assert.equal(f.c.form.phone, '');
  assert.equal(f.c.retryBody.value, null);
  assert.match(guest, /Không cần đăng nhập hoặc mã OTP/);
});

test('confirmation is required before submitting', async () => {
  const f = await ready();
  f.c.confirmed.value = false;
  await f.c.submit();
  assert.equal(f.calls.length, 0);
});

for (const [field, value] of [['phone', '123'], ['dateOfBirth', '1800-01-01'], ['dateOfBirth', '2000-02-30'], ['appointmentDate', '2098-01-01'], ['slot', '00:01'], ['fullName', ' '], ['gender', 'unknown'], ['notes', 'X'.repeat(501)]]) {
  test('invalid form ' + field + value.slice(0, 12), async () => {
    const f = await ready();
    f.c.form[field] = value;
    await f.c.submit();
    assert.equal(f.calls.length, 0);
    assert.ok(f.c.error.value);
  });
}

test('unknown write keeps the same request ID and payload for reconciliation', async () => {
  const f = await ready();
  f.setWrite(() => { throw Error(); });
  await f.c.submit();
  const original = structuredClone(f.c.retryBody.value);
  assert.ok(original.clientRequestId);
  f.c.form.fullName = 'Changed';
  await f.c.submit();
  assert.deepEqual(f.calls[1][1], original);
  f.setWrite(body => ({ requestId: body.clientRequestId, status: 'Pending' }));
  await f.c.submit();
  assert.equal(f.c.retryBody.value, null);
  assert.ok(f.c.receipt.value);
});

test('known conflict clears frozen request and requires a fresh catalog', async () => {
  const f = await ready();
  f.setWrite(() => { throw { response: { status: 409 } }; });
  await f.c.submit();
  assert.equal(f.c.retryBody.value, null);
  assert.equal(f.c.catalog.value, null);
  assert.ok(f.c.error.value);
});

test('incorrect receipt is not acknowledged as success', async () => {
  const f = await ready();
  f.setWrite(() => ({ requestId: 'wrong', status: 'Pending' }));
  await f.c.submit();
  assert.equal(f.c.receipt.value, null);
  assert.ok(f.c.retryBody.value);
});

test('identity change and unmount clear personal data and ignore late replies', async () => {
  const f = fixture();
  let resolve;
  f.setCatalog(() => new Promise(r => { resolve = r; }));
  const pending = f.c.load();
  f.hooks.unmount();
  resolve(catalog());
  await pending;
  assert.equal(f.c.catalog.value, null);
  assert.equal(f.c.form.phone, '');
  const g = await ready();
  g.auth.role = 'PATIENT';
  g.hooks.watch();
  assert.equal(g.c.form.fullName, '');
  assert.equal(g.c.retryBody.value, null);
});

test('Patient booking binds only the single profile returned for the current account', async () => {
  const f = fixture('PATIENT');
  f.setMine(() => owner);
  await f.c.load();
  Object.assign(f.c.form, { appointmentDate: '2099-01-02', slot: '08:00' });
  assert.equal(f.c.intent().patientId, owner.patientId);
  f.c.patientId.value = 9;
  assert.equal(f.c.intent(), null);
  assert.doesNotMatch(guest, /chooseProfile|Chọn hồ sơ|liên kết nhiều hồ sơ/i);
});

for (const role of ['PATIENT', 'DENTIST', 'ASSISTANT', '']) {
  test('private review blocks ' + role, async () => {
    const f = fixture(role, true);
    await f.c.load();
    assert.equal(f.calls.length, 0);
  });
}

test('staff rejection requires reason and confirmation, then clears row and reports release', async () => {
  const f = fixture('RECEPTIONIST', true);
  await f.c.load();
  f.c.choose(f.c.rows.value[0]);
  await f.c.reject();
  assert.equal(f.calls.length, 1);
  f.c.reason.value = 'Synthetic reason';
  f.c.confirmed.value = true;
  await f.c.reject();
  assert.deepEqual(f.calls[1], ['reject', 3, 1, 'Synthetic reason']);
  assert.equal(f.c.rows.value.length, 0);
  assert.ok(f.c.message.value);
});

test('uncertain rejection locks until reload and clears sensitive drafts', async () => {
  const f = fixture('ADMIN', true);
  await f.c.load();
  f.c.choose(f.c.rows.value[0]);
  f.c.reason.value = 'Synthetic reason';
  f.c.confirmed.value = true;
  f.setReject(() => { throw Error(); });
  await f.c.reject();
  assert.equal(f.c.needsReview.value, true);
  assert.equal(f.c.reason.value, '');
  f.c.confirmed.value = true;
  await f.c.reject();
  assert.equal(f.calls.length, 2);
  await f.c.load();
  assert.equal(f.c.needsReview.value, false);
});

test('stale private list never survives a role change', async () => {
  const f = fixture('ADMIN', true);
  let resolve;
  f.setList(() => new Promise(r => { resolve = r; }));
  const pending = f.c.load();
  f.auth.role = 'PATIENT';
  f.hooks.watch();
  resolve({ items: [{ id: 3, version: 1, isReturning: true }], page: 1, pageSize: 20 });
  await pending;
  assert.equal(f.c.rows.value.length, 0);
});

test('routes, protected client and native form semantics remain explicit', async () => {
  const service = await readFile(new URL('../src/services/bookingRequests.ts', import.meta.url), 'utf8');
  const router = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');
  assert.match(service, /confirmed:true/);
  assert.match(router, /name:\s*'guest-booking'.*requiresAuth\s*:\s*false/);
  assert.match(router, /name:\s*'booking-request-review'.*roles:\s*\['ADMIN','RECEPTIONIST'\]/);
  assert.doesNotMatch(service, /otp\(|requestOtp|challengeId/);
  assert.match(review, /role="status" aria-live="polite"/);
  assert.ok(!/localStorage|console\.log/.test(service + guest + review));
});

test('a replayed receipt may already be approved after an uncertain initial submit', async () => {
  const f = await ready();
  f.setWrite(body => ({ requestId: body.clientRequestId, status: 'Approved' }));
  await f.c.submit();
  assert.equal(f.c.receipt.value.status, 'Approved');
  assert.equal(f.c.retryBody.value, null);
  assert.equal(f.c.form.phone, '');
});
