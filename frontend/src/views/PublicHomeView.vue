<template>
  <main class="home">
    <header class="topbar">
      <RouterLink class="brand" to="/">Nha khoa Hoàng Gia</RouterLink>
      <nav aria-label="Điều hướng chính">
        <RouterLink to="/login">Đăng nhập</RouterLink>
      </nav>
    </header>
    <section class="hero">
      <p class="eyebrow">Phòng khám nha khoa Hoàng Gia</p>
      <h1>Chăm sóc nụ cười của bạn</h1>
      <p>Chọn ngày và khung giờ phù hợp. Bạn có thể gửi yêu cầu đặt lịch mà không cần đăng nhập hoặc nhập mã OTP.</p>
      <RouterLink class="book" to="/book-appointment">Đặt lịch khám</RouterLink>
      <p class="note">Lễ tân sẽ xác nhận lịch hẹn. Khi đến phòng khám, vui lòng mang theo CCCD để đối chiếu.</p>
    </section>
  </main>
</template>

<style scoped>
.home{min-height:100vh;background:linear-gradient(145deg,#f0fdfa,#eff6ff);color:#123047;padding:0 1rem}
.topbar{max-width:70rem;margin:auto;min-height:4.5rem;display:flex;align-items:center;justify-content:space-between;gap:1rem}
.brand{font-weight:750;text-decoration:none;color:inherit;font-size:1.1rem}
.topbar nav a{color:#164e63}
.hero{box-sizing:border-box;max-width:54rem;margin:clamp(3rem,12vh,8rem) auto;padding:clamp(1.5rem,6vw,4rem);border-radius:1.5rem;background:#fff;box-shadow:0 1.5rem 4rem #164e6320}
.eyebrow{color:#0f766e;font-weight:700;text-transform:uppercase;letter-spacing:.08em;font-size:.8rem}
h1{font-size:clamp(2rem,6vw,4rem);line-height:1.08;margin:.6rem 0 1rem;max-width:12ch}
.hero>p:not(.eyebrow){max-width:42rem;line-height:1.65;color:#475569}
.book{display:inline-flex;align-items:center;justify-content:center;min-height:2.9rem;margin:1rem 0;padding:0 1.25rem;border-radius:.6rem;background:#0f766e;color:white;font-weight:700;text-decoration:none}
.book:focus-visible,.topbar a:focus-visible{outline:3px solid #f59e0b;outline-offset:3px}
.note{font-size:.9rem}
</style>
