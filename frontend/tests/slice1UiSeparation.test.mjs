import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parse } from '@vue/compiler-sfc';

test('router config defines separated public, patient (/patient/*) and management (/management/*) zones', async () => {
  const routerSrc = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');

  // Public / standalone routes
  assert.match(routerSrc, /path:\s*'\/'[\s\S]*?name:\s*'public-home'[\s\S]*?requiresAuth:\s*false/);
  assert.match(routerSrc, /path:\s*'\/book-appointment'[\s\S]*?name:\s*'guest-booking'[\s\S]*?requiresAuth:\s*false/);
  assert.match(routerSrc, /path:\s*'\/portal\/prescriptions\/:code'[\s\S]*?name:\s*'public-prescription'[\s\S]*?requiresAuth:\s*false/);
  assert.match(routerSrc, /path:\s*'\/queue\/display'[\s\S]*?name:\s*'queue-display'[\s\S]*?requiresAuth:\s*false/);
  assert.match(routerSrc, /path:\s*'\/queue\/public-monitor'[\s\S]*?name:\s*'queue-public-monitor'[\s\S]*?requiresAuth:\s*false/);

  // Patient portal routes (/patient/*)
  assert.match(routerSrc, /path:\s*'\/patient\/my-record'[\s\S]*?roles:\s*\['PATIENT'\]/);
  assert.match(routerSrc, /path:\s*'\/patient\/appointments'[\s\S]*?roles:\s*\['PATIENT'\]/);
  assert.match(routerSrc, /path:\s*'\/patient\/appointments\/mine'[\s\S]*?roles:\s*\['PATIENT'\]/);
  assert.match(routerSrc, /path:\s*'\/patient\/invoices\/mine'[\s\S]*?roles:\s*\['PATIENT'\]/);
  assert.match(routerSrc, /path:\s*'\/patient\/dashboard'[\s\S]*?roles:\s*\['PATIENT'\]/);
  assert.match(routerSrc, /path:\s*'\/patient\/profile'[\s\S]*?roles:\s*\['PATIENT'\]/);

  // Staff / management routes (/management/*)
  assert.match(routerSrc, /path:\s*'\/management\/dashboard'[\s\S]*?roles:\s*\['ADMIN',\s*'RECEPTIONIST',\s*'DENTIST',\s*'ASSISTANT'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/queue\/today'[\s\S]*?roles:\s*\['ADMIN',\s*'RECEPTIONIST',\s*'DENTIST',\s*'ASSISTANT'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/queue\/check-in'[\s\S]*?roles:\s*\['ADMIN',\s*'RECEPTIONIST'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/patients'[\s\S]*?roles:\s*\['ADMIN',\s*'RECEPTIONIST',\s*'DENTIST',\s*'ASSISTANT'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/invoices'[\s\S]*?roles:\s*\['ADMIN',\s*'RECEPTIONIST'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/admin\/staff'[\s\S]*?roles:\s*\['ADMIN'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/admin\/chairs'[\s\S]*?roles:\s*\['ADMIN'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/admin\/services'[\s\S]*?roles:\s*\['ADMIN'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/admin\/medicines'[\s\S]*?roles:\s*\['ADMIN'\]/);
  assert.match(routerSrc, /path:\s*'\/management\/admin\/reports\/revenue'[\s\S]*?roles:\s*\['ADMIN'\]/);

  // Backward compatibility routes
  assert.match(routerSrc, /path:\s*'\/my-record'/);
  assert.match(routerSrc, /path:\s*'\/dashboard'/);
  assert.match(routerSrc, /path:\s*'\/admin\/staff'/);
  assert.match(routerSrc, /path:\s*'\/queue\/today'/);
});

test('navigation guard isolates Patient and Staff zones for all 5 roles', async () => {
  const routerSrc = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8');

  // Verify guard logic source contains zone isolation
  assert.match(routerSrc, /isPatient\s*&&\s*to\.path\.startsWith\('\/management\/'\)/);
  assert.match(routerSrc, /isStaff\s*&&\s*to\.path\.startsWith\('\/patient\/'\)/);
  assert.match(routerSrc, /to\.meta\.requiresAuth\s*&&\s*!authStore\.isAuthenticated/);
  assert.match(routerSrc, /to\.meta\.guestOnly\s*&&\s*authStore\.isAuthenticated/);

  function simulateGuard(to, authState) {
    let result = null;
    const next = (arg) => { result = arg || 'ALLOW'; };

    const staffRoles = ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'];
    const userRole = authState.role;
    const isStaff = staffRoles.includes(userRole);
    const isPatient = userRole === 'PATIENT';

    if (to.meta?.requiresAuth && !authState.isAuthenticated) {
      next({ name: 'login', query: { redirect: to.fullPath } });
      return result;
    }

    if (to.meta?.guestOnly && authState.isAuthenticated) {
      if (isPatient) {
        next({ path: '/patient/my-record' });
      } else {
        next({ path: '/management/dashboard' });
      }
      return result;
    }

    if (authState.isAuthenticated) {
      if (isPatient && to.path.startsWith('/management/')) {
        next({ path: '/patient/my-record' });
        return result;
      }

      if (isStaff && to.path.startsWith('/patient/')) {
        next({ path: '/management/dashboard' });
        return result;
      }

      if (to.meta?.roles && Array.isArray(to.meta.roles)) {
        if (!to.meta.roles.includes(userRole)) {
          if (isPatient) {
            next({ path: '/patient/my-record' });
          } else {
            next({ path: '/management/dashboard' });
          }
          return result;
        }
      }
    }

    next();
    return result;
  }

  // 1. Unauthenticated checks
  assert.deepEqual(
    simulateGuard({ path: '/management/dashboard', fullPath: '/management/dashboard', meta: { requiresAuth: true } }, { isAuthenticated: false, role: null }),
    { name: 'login', query: { redirect: '/management/dashboard' } }
  );
  assert.deepEqual(
    simulateGuard({ path: '/patient/my-record', fullPath: '/patient/my-record', meta: { requiresAuth: true } }, { isAuthenticated: false, role: null }),
    { name: 'login', query: { redirect: '/patient/my-record' } }
  );
  assert.equal(
    simulateGuard({ path: '/', fullPath: '/', meta: { requiresAuth: false } }, { isAuthenticated: false, role: null }),
    'ALLOW'
  );

  // 2. PATIENT role checks
  const patientAuth = { isAuthenticated: true, role: 'PATIENT' };
  assert.equal(
    simulateGuard({ path: '/patient/my-record', meta: { requiresAuth: true, roles: ['PATIENT'] } }, patientAuth),
    'ALLOW'
  );
  assert.deepEqual(
    simulateGuard({ path: '/management/dashboard', meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } }, patientAuth),
    { path: '/patient/my-record' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/management/admin/staff', meta: { requiresAuth: true, roles: ['ADMIN'] } }, patientAuth),
    { path: '/patient/my-record' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/login', meta: { guestOnly: true } }, patientAuth),
    { path: '/patient/my-record' }
  );

  // 3. ADMIN role checks
  const adminAuth = { isAuthenticated: true, role: 'ADMIN' };
  assert.equal(
    simulateGuard({ path: '/management/dashboard', meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } }, adminAuth),
    'ALLOW'
  );
  assert.equal(
    simulateGuard({ path: '/management/admin/staff', meta: { requiresAuth: true, roles: ['ADMIN'] } }, adminAuth),
    'ALLOW'
  );
  assert.deepEqual(
    simulateGuard({ path: '/patient/my-record', meta: { requiresAuth: true, roles: ['PATIENT'] } }, adminAuth),
    { path: '/management/dashboard' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/login', meta: { guestOnly: true } }, adminAuth),
    { path: '/management/dashboard' }
  );

  // 4. RECEPTIONIST role checks
  const recepAuth = { isAuthenticated: true, role: 'RECEPTIONIST' };
  assert.equal(
    simulateGuard({ path: '/management/queue/today', meta: { requiresAuth: true, roles: ['ADMIN', 'RECEPTIONIST', 'DENTIST', 'ASSISTANT'] } }, recepAuth),
    'ALLOW'
  );
  assert.deepEqual(
    simulateGuard({ path: '/management/admin/staff', meta: { requiresAuth: true, roles: ['ADMIN'] } }, recepAuth),
    { path: '/management/dashboard' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/patient/my-record', meta: { requiresAuth: true, roles: ['PATIENT'] } }, recepAuth),
    { path: '/management/dashboard' }
  );

  // 5. DENTIST role checks
  const dentistAuth = { isAuthenticated: true, role: 'DENTIST' };
  assert.equal(
    simulateGuard({ path: '/management/clinical/diagnosis', meta: { requiresAuth: true, roles: ['ADMIN', 'DENTIST'] } }, dentistAuth),
    'ALLOW'
  );
  assert.deepEqual(
    simulateGuard({ path: '/management/admin/staff', meta: { requiresAuth: true, roles: ['ADMIN'] } }, dentistAuth),
    { path: '/management/dashboard' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/patient/my-record', meta: { requiresAuth: true, roles: ['PATIENT'] } }, dentistAuth),
    { path: '/management/dashboard' }
  );

  // 6. ASSISTANT role checks
  const assistantAuth = { isAuthenticated: true, role: 'ASSISTANT' };
  assert.equal(
    simulateGuard({ path: '/management/staff/chair-status', meta: { requiresAuth: true, roles: ['ADMIN', 'ASSISTANT'] } }, assistantAuth),
    'ALLOW'
  );
  assert.deepEqual(
    simulateGuard({ path: '/management/admin/staff', meta: { requiresAuth: true, roles: ['ADMIN'] } }, assistantAuth),
    { path: '/management/dashboard' }
  );
  assert.deepEqual(
    simulateGuard({ path: '/patient/my-record', meta: { requiresAuth: true, roles: ['PATIENT'] } }, assistantAuth),
    { path: '/management/dashboard' }
  );
});

test('App.vue layout renders portal navigation only inside the matching patient or staff zone', async () => {
  const appSrc = await readFile(new URL('../src/App.vue', import.meta.url), 'utf8');
  const { descriptor } = parse(appSrc);
  const css = descriptor.styles.map(s => s.content).join('\n');

  // Area badges
  assert.match(descriptor.template.content, /class="area-tag patient-tag">Cổng Bệnh Nhân<\/span>/);
  assert.match(descriptor.template.content, /class="area-tag staff-tag">Khu Quản Lý<\/span>/);

  // Navbar zone classes
  assert.match(descriptor.template.content, /:class="isPatient \? 'navbar-patient' : 'navbar-staff'"/);

  // Public pages and TV routes do not inherit an authenticated portal menu.
  assert.match(descriptor.template.content, /v-if="authStore\.isAuthenticated && isPortalRoute"/);
  assert.match(descriptor.scriptSetup.content, /route\.path\.startsWith\('\/patient\/'\) \|\| route\.path\.startsWith\('\/management\/'\)/);

  // Brand link destinations
  assert.match(descriptor.template.content, /:to="isPatient \? '\/patient\/my-record' : '\/management\/dashboard'"/);
  assert.match(descriptor.template.content, /to="\/patient\/appointments"/);
  assert.match(descriptor.template.content, /to="\/management\/queue\/today"/);

  // CSS contains area tags and borders
  assert.match(css, /\.navbar-patient\s*\{[^}]*border-bottom:\s*3px solid/s);
  assert.match(css, /\.navbar-staff\s*\{[^}]*border-bottom:\s*3px solid/s);
  assert.match(css, /\.area-tag\s*\{[^}]*border-radius:/s);
  assert.match(css, /\.patient-tag\s*\{[^}]*background-color:/s);
  assert.match(css, /\.staff-tag\s*\{[^}]*background-color:/s);

  // Responsive constraint unchanged
  assert.doesNotMatch(css, /overflow-x:\s*hidden/);
});

test('LoginView redirects patients to /patient/my-record and staff to /management/dashboard by default', async () => {
  const loginSrc = await readFile(new URL('../src/views/LoginView.vue', import.meta.url), 'utf8');

  assert.match(loginSrc, /const defaultRoute = authStore\.role === 'PATIENT' \? '\/patient\/my-record' : '\/management\/dashboard'/);
  assert.match(loginSrc, /const redirect = \(route\.query\.redirect as string\) \|\| defaultRoute/);
});

