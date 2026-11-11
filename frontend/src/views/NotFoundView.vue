<template>
  <div class="not-found">
    <h1>404</h1>
    <p>Trang bạn tìm kiếm không tồn tại.</p>
    <router-link to="/">Về trang chủ</router-link>
  </div>
</template>

<style scoped>
.not-found {
  text-align: center;
  max-width: 480px;
  margin: 4rem auto;
  padding: 2.5rem 1.5rem;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(15, 45, 89, 0.06);
  font-family: inherit;
}
.not-found h1 {
  font-size: 3.5rem;
  font-weight: 800;
  color: #0f2d59;
  margin: 0 0 0.5rem;
  line-height: 1;
}
.not-found p {
  color: #475569;
  font-size: 1.05rem;
  margin-bottom: 2rem;
}
.not-found a {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 0.625rem 1.5rem;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: 8px;
  background: #0f2d59;
  color: #ffffff;
  text-decoration: none;
  transition: all 0.15s ease-in-out;
}
.not-found a:hover {
  background: #1e3a8a;
}
.not-found a:focus-visible {
  outline: 3px solid rgba(2, 132, 199, 0.35);
  outline-offset: 2px;
}
</style>
