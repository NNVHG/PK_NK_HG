import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';
import { setUnauthorizedHandler } from './services/api';
import './assets/modern-dental-theme.css';

const app = createApp(App);
const pinia = createPinia();

let redirectingToLogin = false;

function redirectToLoginOnExpiry() {
  if (redirectingToLogin) return;
  const currentRoute = router.currentRoute.value;
  if (currentRoute.path === '/login') return;
  redirectingToLogin = true;
  const query = currentRoute.fullPath && currentRoute.fullPath !== '/login'
    ? { redirect: currentRoute.fullPath }
    : undefined;
  void router.push({ name: 'login', query }).finally(() => {
    redirectingToLogin = false;
  });
}

setUnauthorizedHandler(redirectToLoginOnExpiry);
window.addEventListener('auth:expired', redirectToLoginOnExpiry);

app.use(pinia);
app.use(router);

app.mount('#app');
