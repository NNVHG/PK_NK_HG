<template>
  <div class="home">
    <header class="topbar">
      <div class="topbar-inner">
        <RouterLink class="brand" to="/" aria-label="Nha khoa Hoàng Gia - Trang chủ">
          <img src="/logo-ngang.svg" alt="" width="164" height="44" />
        </RouterLink>
        <nav class="navigation" aria-label="Điều hướng chính">
          <a class="nav-about" href="#cach-dat-lich">Hướng dẫn đặt lịch</a>
          <RouterLink class="login-link" to="/login">Đăng nhập</RouterLink>
          <RouterLink class="button button-gold nav-book" to="/book-appointment">Đặt lịch khám</RouterLink>
        </nav>
      </div>
    </header>

    <section class="hero" aria-labelledby="hero-heading">
      <div class="hero-inner">
        <div class="hero-copy">
          <p class="eyebrow"><span aria-hidden="true">✦</span> Phòng khám Nha khoa Hoàng Gia</p>
          <h1 id="hero-heading">Nụ cười Hoàng Gia <span>tỏa sáng mỗi ngày</span></h1>
          <p class="intro">Chọn ngày và khung giờ phù hợp để gửi yêu cầu đặt lịch khám. Bạn không cần đăng nhập hay nhập mã OTP khi đặt lịch.</p>
          <div class="hero-actions">
            <RouterLink class="button button-gold" to="/book-appointment">Đặt lịch khám <span aria-hidden="true">→</span></RouterLink>
            <a class="button button-outline" href="#cach-dat-lich">Tìm hiểu thêm</a>
          </div>
          <p class="hero-note">Lễ tân sẽ tiếp nhận và xác nhận yêu cầu đặt lịch của bạn.</p>
        </div>
        <div class="hero-art" aria-hidden="true">
          <div class="art-orbit art-orbit-outer"></div>
          <div class="art-orbit art-orbit-inner"></div>
          <div class="art-medallion"><img src="/logo-icon.svg" alt="" width="230" height="230" /></div>
          <span class="art-sparkle sparkle-one">✦</span><span class="art-sparkle sparkle-two">✧</span>
        </div>
      </div>
    </section>

    <section id="cach-dat-lich" class="guide" aria-labelledby="guide-heading">
      <div class="section-heading">
        <p class="eyebrow">Đồng hành cùng nụ cười của bạn</p>
        <h2 id="guide-heading">Đặt lịch khám thật đơn giản</h2>
        <p>Chỉ vài bước để gửi yêu cầu và chuẩn bị cho buổi khám.</p>
      </div>
      <div class="steps">
        <article class="step"><span class="step-number" aria-hidden="true">01</span><h3>Chọn lịch phù hợp</h3><p>Điền thông tin và chọn ngày, khung giờ bạn mong muốn.</p></article>
        <article class="step"><span class="step-number" aria-hidden="true">02</span><h3>Chờ xác nhận</h3><p>Lễ tân tiếp nhận và xác nhận yêu cầu đặt lịch của bạn.</p></article>
        <article class="step"><span class="step-number" aria-hidden="true">03</span><h3>Đến phòng khám</h3><p>Mang giấy tờ định danh để nhân viên đối chiếu thông tin tại quầy.</p></article>
      </div>
      <RouterLink class="guide-link" to="/book-appointment">Gửi yêu cầu đặt lịch <span aria-hidden="true">→</span></RouterLink>
    </section>
    <footer class="footer"><span>Nha khoa Hoàng Gia</span><RouterLink to="/login">Đăng nhập</RouterLink></footer>
  </div>
</template>

<style scoped>
.home{min-height:100vh;color:#18354c;background:#fff}
.topbar{background:#fff;border-bottom:1px solid #e6eff6}
.topbar-inner{max-width:1200px;min-height:76px;margin:auto;padding:0 24px;display:flex;align-items:center;justify-content:space-between;gap:20px}
.brand{display:inline-flex;flex-shrink:0;align-items:center}.brand img{display:block;width:164px;height:auto}
.navigation{display:flex;align-items:center;gap:24px}.navigation>a{font-size:.9rem;font-weight:700;text-decoration:none}.nav-about{color:#425c73}.login-link{color:#1857b6;border:1px solid #c5d8eb;border-radius:9px;padding:11px 17px}
.button{display:inline-flex;min-height:48px;box-sizing:border-box;align-items:center;justify-content:center;gap:12px;padding:0 22px;border-radius:10px;text-decoration:none;font-weight:750;transition:background-color .15s,color .15s}
.button-gold{background:#e5b95e;color:#102e56}.button-gold:hover{background:#d9a948}.button-outline{border:1px solid #b9d0e6;background:#fff;color:#1857b6}.button-outline:hover{background:#edf6fd}
.hero{overflow:hidden;background:#f1f9ff}.hero-inner{max-width:1200px;min-height:490px;margin:auto;padding:0 24px;display:grid;grid-template-columns:1.08fr .92fr;align-items:center;gap:24px;box-sizing:border-box}
.hero-copy{position:relative;z-index:1;padding:64px 0}.eyebrow{color:#1857b6;text-transform:uppercase;letter-spacing:.09em;font-size:.76rem;font-weight:800}.eyebrow span{color:#cc9b3a;margin-right:5px}
h1{margin:16px 0 20px;color:#102e56;max-width:13ch;font-size:clamp(2.6rem,4.8vw,4.35rem);line-height:1.1;letter-spacing:-.035em}h1 span{color:#397bcc}.intro{max-width:520px;color:#4a6479;font-size:1.05rem;line-height:1.75}.hero-actions{display:flex;flex-wrap:wrap;gap:12px;margin-top:28px}.hero-note{margin:22px 0 0;color:#526c80;font-size:.88rem;line-height:1.5}
.hero-art{position:relative;align-self:stretch;display:grid;place-items:center;isolation:isolate}.hero-art:before{content:"";position:absolute;z-index:-2;inset:-80px -35vw -80px 5%;border-radius:50% 0 0 50%;background:#d8ecfa}.art-orbit{position:absolute;border:1px solid #a6d7ef;border-radius:50%;aspect-ratio:1}.art-orbit-outer{width:min(94%,400px)}.art-orbit-inner{width:min(78%,330px);border-color:#fff}.art-medallion{width:clamp(180px,20vw,270px);aspect-ratio:1;display:grid;place-items:center;background:#fff;border-radius:50%;box-shadow:0 20px 60px #1857b61c}.art-medallion img{width:72%;height:auto}.art-sparkle{position:absolute;color:#d4a84e;font-size:2rem}.sparkle-one{top:23%;right:18%}.sparkle-two{bottom:23%;left:15%}
.guide{max-width:1200px;margin:auto;padding:72px 24px 76px;box-sizing:border-box;scroll-margin-top:20px}.section-heading{text-align:center}.section-heading h2{margin:10px 0;color:#102e56;font-size:clamp(1.8rem,3vw,2.4rem)}.section-heading>p:last-child{color:#526c80;line-height:1.6}.steps{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px;margin:32px 0 24px}.step{padding:25px;border:1px solid #e4eef6;border-radius:16px;background:#fff;box-shadow:0 8px 30px #1857b609}.step-number{display:grid;width:42px;height:42px;place-items:center;border-radius:12px;background:#e8f4fd;color:#1857b6;font-weight:800}.step h3{margin:18px 0 8px;color:#102e56;font-size:1.05rem}.step p{margin:0;color:#526c80;line-height:1.65}.guide-link{display:table;margin:auto;padding:10px;color:#1857b6;font-weight:750;text-decoration:none}.footer{border-top:1px solid #e6eff6;padding:24px max(24px,calc((100% - 1152px)/2));display:flex;justify-content:space-between;gap:20px;color:#526c80;font-size:.875rem}.footer a{color:#1857b6}
a:focus-visible{outline:3px solid #c3881d;outline-offset:4px}
@media(max-width:700px){.topbar-inner{min-height:68px;padding:0 16px;gap:8px}.brand img{width:130px}.navigation{gap:10px}.nav-about{display:none}.navigation>a{font-size:.8rem}.login-link{padding:10px}.nav-book{min-height:40px;padding:0 10px}.hero-inner{grid-template-columns:1fr;gap:0;padding:0 20px}.hero-copy{padding:46px 0 20px}h1{font-size:clamp(2.4rem,9vw,3.3rem)}.hero-art{min-height:230px}.hero-art:before{inset:0 -35vw -120px -35vw;border-radius:50% 50% 0 0}.art-medallion{width:170px}.art-orbit-outer{width:220px}.art-orbit-inner{width:190px}.sparkle-one{top:18%;right:20%}.sparkle-two{bottom:22%;left:20%}.guide{padding:54px 20px}.steps{grid-template-columns:1fr;gap:12px}.step{padding:20px}.footer{padding:24px 20px}}
@media(max-width:350px){.brand img{width:108px}.navigation{gap:6px}.navigation>a{font-size:.74rem}.nav-book{padding:0 8px}.login-link{padding:9px 7px}}
</style>
