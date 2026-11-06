import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';

const { descriptor } = parse(await readFile(new URL('../src/components/SafetyAlertBanner.vue', import.meta.url), 'utf8'));
const compiled = ts.transpileModule(compileScript(descriptor, { id: 'safety-alert-test' }).content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function deferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function createBannerFixture({ initialPatientId = 1, getAlerts } = {}) {
  let mount, cleanup;
  const watchers = [];
  const calls = [];
  const props = {
    patientId: initialPatientId,
    refreshKey: 0,
  };

  const imports = {
    vue: {
      defineComponent: c => c,
      ref: value => ({ value }),
      watch: (getter, cb) => watchers.push({ getter, cb }),
      onMounted: cb => { mount = cb; },
      onBeforeUnmount: cb => { cleanup = cb; },
    },
    '@/services/patients': {
      patientsService: {
        getSafetyAlerts: async id => {
          calls.push(id);
          if (getAlerts) return getAlerts(id);
          return {
            hasHistory: true,
            alerts: [{ visitId: 1, type: 'Allergy', name: `Allergy-${id}`, detail: 'Penicillin', recordedAt: '2026-11-06T10:00:00Z' }],
          };
        },
      },
    },
  };

  const module = { exports: {} };
  new Function('require', 'module', 'exports', compiled)(name => imports[name], module, module.exports);
  const c = module.exports.default.setup(props, { expose() {} });

  return {
    c,
    props,
    calls,
    watchers,
    mount: () => mount?.(),
    unmount: () => cleanup?.(),
    changePatient: (newId) => {
      props.patientId = newId;
      watchers[0]?.cb();
    },
    triggerRefresh: () => {
      props.refreshKey++;
      watchers[1]?.cb();
    },
  };
}

test('SafetyAlertBanner loads alerts for initial patient on mount', async () => {
  const f = createBannerFixture({ initialPatientId: 10 });
  await f.mount();
  assert.deepEqual(f.calls, [10]);
  assert.equal(f.c.loading.value, false);
  assert.equal(f.c.data.value?.alerts[0]?.name, 'Allergy-10');
  assert.equal(f.c.error.value, '');
});

test('SafetyAlertBanner drops stale response when patientId switches before first response returns', async () => {
  const pendingA = deferred();
  const alertB = {
    hasHistory: true,
    alerts: [{ visitId: 2, type: 'Condition', name: 'Hypertension', detail: '140/90', recordedAt: '2026-11-06T11:00:00Z' }],
  };

  const f = createBannerFixture({
    initialPatientId: 1,
    getAlerts: id => (id === 1 ? pendingA.promise : alertB),
  });

  const mountPromise = f.mount();
  assert.equal(f.c.loading.value, true);

  f.changePatient(2);
  await Promise.resolve();

  assert.equal(f.c.loading.value, false);
  assert.equal(f.c.data.value?.alerts[0]?.name, 'Hypertension');

  pendingA.resolve({
    hasHistory: true,
    alerts: [{ visitId: 1, type: 'Allergy', name: 'Allergy-Stale-1', detail: 'Late', recordedAt: '2026-11-06T10:00:00Z' }],
  });
  await mountPromise;

  assert.equal(f.c.data.value?.alerts[0]?.name, 'Hypertension');
  assert.equal(f.c.loading.value, false);
});

test('SafetyAlertBanner ignores late error if patient switched', async () => {
  const pendingA = deferred();
  const alertB = {
    hasHistory: true,
    alerts: [{ visitId: 2, type: 'Condition', name: 'Asthma', detail: '', recordedAt: '2026-11-06T11:00:00Z' }],
  };

  const f = createBannerFixture({
    initialPatientId: 1,
    getAlerts: id => (id === 1 ? pendingA.promise : alertB),
  });

  f.mount();
  f.changePatient(2);

  pendingA.reject(new Error('Network error on patient 1'));
  await Promise.resolve();

  assert.equal(f.c.error.value, '');
  assert.equal(f.c.data.value?.alerts[0]?.name, 'Asthma');
});

test('SafetyAlertBanner ignores responses after unmount', async () => {
  const pending = deferred();
  const f = createBannerFixture({
    initialPatientId: 5,
    getAlerts: () => pending.promise,
  });

  const mountPromise = f.mount();
  assert.equal(f.c.loading.value, true);

  f.unmount();
  pending.resolve({
    hasHistory: true,
    alerts: [{ visitId: 5, type: 'Allergy', name: 'Late', detail: '', recordedAt: '2026-11-06T10:00:00Z' }],
  });
  await mountPromise;

  assert.equal(f.c.data.value, null);
});

test('SafetyAlertBanner refresh re-fetches and updates alert data', async () => {
  let callCount = 0;
  const f = createBannerFixture({
    initialPatientId: 7,
    getAlerts: () => {
      callCount++;
      return {
        hasHistory: true,
        alerts: [{ visitId: 7, type: 'Allergy', name: `Call-${callCount}`, detail: '', recordedAt: '2026-11-06T10:00:00Z' }],
      };
    },
  });

  await f.mount();
  assert.equal(f.c.data.value?.alerts[0]?.name, 'Call-1');

  f.triggerRefresh();
  await Promise.resolve();
  assert.equal(f.c.data.value?.alerts[0]?.name, 'Call-2');
});
