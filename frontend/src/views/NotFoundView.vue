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
  padding: 4rem 1rem;
}
.not-found h1 {
  font-size: 3rem;
  color: #e74c3c;
  margin-bottom: 0.5rem;
}
.not-found p {
  color: #666;
  margin-bottom: 1.5rem;
}
.not-found a {
  color: #3498db;
  text-decoration: none;
  font-weight: 500;
}
</style>
