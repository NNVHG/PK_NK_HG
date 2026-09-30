import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';
import { setUnauthorizedHandler } from './services/api';

const app = createApp(App);
const pinia = createPinia();

setUnauthorizedHandler(() => {
  const currentRoute = router.currentRoute.value;
  if (currentRoute.path !== '/login') {
    void router.push({ name: 'login', query: { redirect: currentRoute.fullPath } });
  }
});

app.use(pinia);
app.use(router);

app.mount('#app');
