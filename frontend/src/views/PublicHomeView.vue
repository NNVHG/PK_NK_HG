<template>
  <div class="home">
    <!-- 1. Thanh điều hướng chính (1 hàng gọn gàng, 4 mục ngắn + 2 nút cố định không bao giờ bị che) -->
    <header class="topbar">
      <div class="topbar-inner">
        <RouterLink class="brand" to="/" aria-label="Nha khoa Hoàng Gia - Trang chủ">
          <img src="/logo-ngang.svg" alt="" width="158" height="42" />
        </RouterLink>

        <nav class="nav-center" aria-label="Điều hướng chính">
          <a class="nav-link" href="#chuyen-khoa" @click.prevent="scrollToSection('chuyen-khoa')">
            <svg class="nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M6.5 4c2.5-1.5 5 0 5.5 1 .5-1 3-2.5 5.5-1 3 1.8 3.5 6 3 9.5-.7 4.5-2 9.5-3.5 9.5-1.2 0-2-3.5-3-5-.5-.8-1.5-.8-2 0-1 1.5-1.8 5-3 5-1.5 0-2.8-5-3.5-9.5-.5-3.5 0-7.7 3-9.5z"/></svg>
            <span>Dịch vụ</span>
          </a>
          <a class="nav-link" href="#so-do-fdi" @click.prevent="scrollToSection('so-do-fdi')">
            <svg class="nav-ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
            <span>Sơ đồ FDI</span>
          </a>
          <a class="nav-link" href="#minh-bach-chi-phi" @click.prevent="scrollToSection('minh-bach-chi-phi')">
            <svg class="nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            <span>Bảng giá</span>
          </a>
          <a class="nav-link" href="#cach-dat-lich" @click.prevent="scrollToSection('cach-dat-lich')">
            <svg class="nav-ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
            <span>Quy trình</span>
          </a>
          <a class="nav-link" href="#cau-hoi-thuong-gap" @click.prevent="scrollToSection('cau-hoi-thuong-gap')">
            <svg class="nav-ic" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
            <span>Hỏi đáp</span>
          </a>
        </nav>

        <div class="nav-actions">
          <RouterLink class="login-link" to="/login">Đăng nhập</RouterLink>
          <RouterLink class="button button-gold nav-book" to="/book-appointment">Đặt lịch khám</RouterLink>
        </div>
      </div>
    </header>

    <!-- 2. Khối Hero Thương hiệu Hoàng Gia (Phối màu Trắng Sứ & Xanh Lam Ngọc hài hòa) -->
    <section class="hero" aria-labelledby="hero-heading">
      <div class="hero-inner">
        <div class="hero-copy">
          <p class="eyebrow"><span aria-hidden="true">✦</span> Phòng khám Nha khoa Hoàng Gia · Chuẩn Y Khoa &amp; Thẩm Mỹ</p>
          <h1 id="hero-heading">Nụ cười Hoàng Gia <span>tỏa sáng mỗi ngày</span></h1>
          <p class="intro">
            Chọn ngày và khung giờ phù hợp để gửi yêu cầu đặt lịch khám chỉ trong vài bước. Bạn không cần đăng nhập hay nhập mã OTP khi đặt lịch — mọi ca khám đều được theo dõi trên <strong>Sơ đồ răng giải phẫu FDI điện tử</strong>, kiểm soát an toàn sinh hiệu &amp; tiền sử dị ứng và minh bạch chi phí theo từng giai đoạn điều trị.
          </p>
          <div class="hero-actions">
            <RouterLink class="button button-gold" to="/book-appointment">Đặt lịch khám ngay <span aria-hidden="true">→</span></RouterLink>
            <RouterLink class="button button-outline" to="/login">Đăng nhập Cổng bệnh nhân</RouterLink>
            <a class="button button-ghost" href="#so-do-fdi" @click.prevent="scrollToSection('so-do-fdi')">Xem Sơ đồ răng FDI mẫu</a>
          </div>
          <div class="hero-metrics-strip" aria-label="Chỉ số tiêu chuẩn y khoa">
            <div class="hero-metric-item">
              <strong>32 &amp; 20</strong>
              <span>Bộ răng chuẩn FDI</span>
            </div>
            <div class="hero-metric-item">
              <strong>05 Mặt</strong>
              <span>Khám chi tiết B·L·M·D·O</span>
            </div>
            <div class="hero-metric-item">
              <strong>03 Lớp</strong>
              <span>Sàng lọc dị ứng &amp; HA</span>
            </div>
            <div class="hero-metric-item">
              <strong>0% Phí ẩn</strong>
              <span>Chia đợt trả góp</span>
            </div>
          </div>
          <p class="hero-note">Giờ làm việc: Thứ 2 – Thứ 7 (07:30 – 20:00) · Chủ Nhật (07:30 – 12:00). Lễ tân sẽ liên hệ xác nhận lịch hẹn của bạn.</p>
        </div>

        <div class="hero-stage">
          <div class="hero-float-badge badge-top-left">
            <div class="float-ic">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
            </div>
            <div>
              <strong>An toàn lâm sàng 3 lớp</strong>
              <span>Tự động cảnh báo Dị ứng &amp; Huyết áp</span>
            </div>
          </div>

          <div class="hero-art" aria-hidden="true">
            <div class="art-orbit art-orbit-outer"></div>
            <div class="art-orbit art-orbit-inner"></div>
            <div class="art-medallion"><img src="/logo-icon.svg" alt="" width="220" height="220" /></div>
            <span class="art-sparkle sparkle-one">✦</span><span class="art-sparkle sparkle-two">✧</span>
          </div>

          <div class="hero-float-badge badge-bottom-right">
            <div class="float-ic float-ic-green">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6.5 4c2.5-1.5 5 0 5.5 1 .5-1 3-2.5 5.5-1 3 1.8 3.5 6 3 9.5-.7 4.5-2 9.5-3.5 9.5-1.2 0-2-3.5-3-5-.5-.8-1.5-.8-2 0-1 1.5-1.8 5-3 5-1.5 0-2.8-5-3.5-9.5-.5-3.5 0-7.7 3-9.5z"/></svg>
            </div>
            <div>
              <strong>Hồ sơ Sơ đồ Răng FDI</strong>
              <span>Lưu lịch sử điều trị trọn đời</span>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 3. Thanh Đặt lịch Nhanh Nổi (Floating Booking Dock) -->
    <section class="booking-dock-wrap" aria-label="Điểm truy cập đặt lịch nhanh">
      <div class="container">
        <div class="booking-dock">
          <div class="dock-head">
            <div class="dock-title-group">
              <span class="dock-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
              </span>
              <div>
                <h2>Đặt lịch hẹn khám trực tuyến nhanh — Không cần tạo tài khoản trước</h2>
                <p>Điền họ tên, số điện thoại và chọn khung giờ mong muốn; Lễ tân phòng khám sẽ liên hệ xác nhận ngay.</p>
              </div>
            </div>
            <span class="status-dot">Đang nhận lịch hôm nay</span>
          </div>
          <div class="dock-pillars">
            <div class="dock-pillar">
              <strong>1. Chọn chuyên khoa &amp; giờ hẹn</strong>
              <span>Khám tổng quát, Phục hình, Chỉnh nha hoặc Nha khoa trẻ em</span>
            </div>
            <div class="dock-pillar">
              <strong>2. Đối chiếu CCCD / Hộ chiếu tại quầy</strong>
              <span>Kích hoạt tài khoản Cổng bệnh nhân ngay khi đến khám</span>
            </div>
            <div class="dock-pillar">
              <strong>3. Hỗ trợ Hồ sơ Người giám hộ</strong>
              <span>Đặt lịch và xem sơ đồ 20 răng sữa cho các bé trong gia đình</span>
            </div>
            <RouterLink class="button button-primary dock-cta" to="/book-appointment">
              Chọn khung giờ &amp; Đặt lịch <span aria-hidden="true">→</span>
            </RouterLink>
          </div>
        </div>
      </div>
    </section>

    <!-- 4. Hệ sinh thái Chuyên khoa theo Bố cục Bento 12 Cột (Đồng bộ Tông Sáng Trắng Sứ & Xanh Lam Ngọc) -->
    <section id="chuyen-khoa" class="section-block section-white" aria-labelledby="specialties-heading">
      <div class="container">
        <div class="section-split-head">
          <div>
            <p class="eyebrow">Hệ sinh thái 8 chuyên khoa mũi nhọn</p>
            <h2 id="specialties-heading">Giải pháp Nha khoa Chuyên sâu cho Mọi Nụ cười</h2>
            <p>Phân tầng rõ ràng giữa điều trị phục hình chuyên sâu, chỉnh nha dài hạn và chăm sóc răng miệng định kỳ trên Sơ đồ răng FDI.</p>
          </div>
          <RouterLink class="button button-outline" to="/book-appointment">Đặt lịch tư vấn chuyên khoa →</RouterLink>
        </div>

        <div class="bento-grid">
          <!-- Ô Flagship 1 (7 cột - Nền Lam Ngọc sáng thanh lịch, viền Vàng Hoàng Gia): Phục hình Răng sứ & Cấy ghép trụ răng -->
          <article class="bento-card bento-7 bento-flagship">
            <div class="bento-top">
              <span class="pill-badge pill-gold">★ Chuyên khoa Phục hình Cao cấp · Hỗ trợ trả góp</span>
              <span class="bento-duration">2–3 buổi hẹn</span>
            </div>
            <h3>Phục hình Răng sứ &amp; Cấy ghép Trụ răng Titanium</h3>
            <p class="bento-desc">
              Khôi phục trọn vẹn hình thể giải phẫu, lực ăn nhai vững chắc và thẩm mỹ tự nhiên cho răng hư tổn nặng hoặc răng đã mất độc lập mà không xâm lấn răng thật kế cận.
            </p>
            <div class="flagship-subgrid">
              <div class="flagship-subitem">
                <strong>Mão sứ Zirconia chính hãng (5.500.000 ₫ / răng)</strong>
                <span>Khít sát đường hoàn tất viền nướu, màu sắc trong tự nhiên như răng thật</span>
              </div>
              <div class="flagship-subitem">
                <strong>Cấy ghép trụ răng Titanium (18.000.000 ₫ / trụ)</strong>
                <span>Thay thế chân răng mất bền vững, lưu hồ sơ theo đúng vị trí răng FDI</span>
              </div>
            </div>
            <div class="bento-foot">
              <span class="bento-highlight">Khóa đơn giá cố định (snapshot) ngay khi thống nhất phác đồ</span>
              <RouterLink class="button button-primary button-sm" to="/book-appointment">Đặt lịch tư vấn Phục hình →</RouterLink>
            </div>
          </article>

          <!-- Ô Flagship 2 (5 cột - Nền Trắng Sứ viền Lam Ngọc): Chỉnh nha — Niềng răng -->
          <article class="bento-card bento-5 bento-sky">
            <div class="bento-top">
              <span class="pill-badge pill-blue">Chỉnh nha chuyên sâu</span>
              <span class="bento-duration">Lộ trình 12–24 tháng</span>
            </div>
            <h3>Chỉnh nha — Niềng răng Mắc cài &amp; Khay trong suốt</h3>
            <p class="bento-desc">
              Điều chỉnh khớp cắn lệch lạc, răng chen chúc, thưa, hô hoặc móm với kế hoạch dịch chuyển răng theo từng giai đoạn rõ ràng.
            </p>
            <ul class="bento-bullets">
              <li>Tùy chọn mắc cài kim loại, mắc cài sứ tự buộc hoặc khay trong suốt</li>
              <li>Chia nhỏ thanh toán theo tiến độ từng buổi hẹn tái khám</li>
              <li>Theo dõi toàn bộ lộ trình và biên lai các đợt trả trên Cổng bệnh nhân</li>
            </ul>
            <div class="bento-foot">
              <span class="bento-highlight">Thanh toán đợt đầu 30% – 40%</span>
              <RouterLink class="specialty-cta" to="/book-appointment">Đặt lịch Chỉnh nha →</RouterLink>
            </div>
          </article>

          <!-- Hàng Bento 2: 3 Ô Chuyên khoa (4 cột mỗi ô) -->
          <article v-for="item in bentoRowTwo" :key="item.title" class="bento-card bento-4">
            <div class="bento-top">
              <span class="specialty-tag">{{ item.group }}</span>
              <span class="specialty-time">{{ item.duration }}</span>
            </div>
            <h3>{{ item.title }}</h3>
            <p class="bento-desc">{{ item.description }}</p>
            <ul class="bento-bullets">
              <li v-for="point in item.highlights" :key="point">{{ point }}</li>
            </ul>
            <div class="bento-foot">
              <span class="specialty-note">{{ item.paymentMode }}</span>
              <RouterLink class="specialty-cta" to="/book-appointment">Đặt lịch →</RouterLink>
            </div>
          </article>

          <!-- Hàng Bento 3: Dải ngang 12 cột cho Chăm sóc định kỳ & Chẩn đoán hình ảnh -->
          <article class="bento-card bento-12 bento-strip">
            <div class="strip-main">
              <div>
                <span class="specialty-tag">Chăm sóc định kỳ &amp; Cận lâm sàng</span>
                <h3>Khám lập phác đồ Sơ đồ răng FDI · Lấy cao răng siêu âm · Tẩy trắng răng · Chụp X-quang kỹ thuật số</h3>
                <p>Thực hiện nhanh gọn trong 15–45 phút: kiểm tra toàn diện 5 mặt răng (Ngoài, Trong, Gần, Xa, Nhai), làm sạch mảng bám dưới nướu êm ái và chụp phim kỹ thuật số liều tia thấp gắn trực tiếp vào hồ sơ răng.</p>
              </div>
              <RouterLink class="button button-outline" to="/book-appointment">Đặt lịch khám định kỳ →</RouterLink>
            </div>
          </article>
        </div>
      </div>
    </section>

    <!-- 5. Điểm nhấn công nghệ: SƠ ĐỒ RĂNG GIẢI PHẪU FDI MẪU TƯƠNG TÁC (32 RĂNG & 20 RĂNG SỮA) & AN TOÀN 3 LỚP -->
    <section id="so-do-fdi" class="section-block section-sky" aria-labelledby="fdi-heading">
      <div class="container">
        <div class="section-split-head">
          <div>
            <p class="eyebrow">Điểm nhấn chuyên môn lâm sàng</p>
            <h2 id="fdi-heading">Sơ đồ Răng Giải phẫu FDI Mẫu &amp; Quy chuẩn An toàn 3 Lớp</h2>
            <p>Bấm chọn trực tiếp từng chiếc răng trên sơ đồ mẫu bên dưới để xem hình thái giải phẫu, 5 mặt răng (B·L·M·D·O) và tình trạng lâm sàng minh họa.</p>
          </div>
          <div class="dentition-switch" role="tablist" aria-label="Chuyển đổi bộ răng mẫu">
            <button
              type="button"
              class="dent-btn"
              :class="{ active: sampleDentition === 'adult' }"
              @click="switchSampleDentition('adult')"
            >
              Bộ 32 Răng Người lớn (11–48)
            </button>
            <button
              type="button"
              class="dent-btn"
              :class="{ active: sampleDentition === 'child' }"
              @click="switchSampleDentition('child')"
            >
              Bộ 20 Răng Sữa Trẻ em (51–85)
            </button>
          </div>
        </div>

        <div class="fdi-showcase-grid">
          <!-- Cột Trái: Sơ đồ răng giải phẫu FDI Mẫu (Hiển thị đầy đủ 32 chiếc răng / 20 chiếc răng sữa bằng SVG) -->
          <div class="fdi-sample-card">
            <div class="fdi-sample-head">
              <div>
                <strong>Sơ đồ mẫu ({{ sampleDentition === 'adult' ? '32 Răng vĩnh viễn · Ký hiệu 11–48' : '20 Răng sữa trẻ em · Ký hiệu 51–85' }})</strong>
                <span>Bấm vào chiếc răng bất kỳ để xem chi tiết giải phẫu &amp; mặt răng</span>
              </div>
              <span class="pill-badge pill-blue">Đang chọn: Răng #{{ selectedSampleTooth }}</span>
            </div>

            <!-- Khung Sơ đồ Răng Giải phẫu Trực quan -->
            <div class="fdi-chart-box">
              <div class="fdi-arch-label">
                <span>HÀM TRÊN ({{ sampleDentition === 'adult' ? 'Phần tư hàm 1 & 2 · 18 → 28' : 'Phần tư hàm 5 & 6 · 55 → 65' }})</span>
                <span class="fdi-side-hint">Bên phải bệnh nhân ← | → Bên trái bệnh nhân</span>
              </div>

              <!-- Hàm trên -->
              <div class="fdi-jaw-row">
                <div class="fdi-quadrant">
                  <button
                    v-for="t in currentUpperRight"
                    :key="t"
                    type="button"
                    class="sample-tooth-btn"
                    :class="[
                      { selected: selectedSampleTooth === t },
                      sampleStateClass(t)
                    ]"
                    :title="`Răng #${t} - ${getSampleToothMeta(t).name}`"
                    @click="selectedSampleTooth = t"
                  >
                    <span class="t-num">{{ t }}</span>
                    <!-- SVG Hình chiếc răng giải phẫu (Thân răng + Chân răng) -->
                    <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
                      <path :d="getToothSvgPaths(t, true).root" class="svg-root" />
                      <path d="M6,29 Q20,32 34,29" class="svg-gum" />
                      <path :d="getToothSvgPaths(t, true).crown" class="svg-crown" />
                      <path :d="getToothSvgPaths(t, true).groove" class="svg-groove" />
                      <circle v-if="sampleConditions[t]?.code === 'CAR'" cx="20" cy="43" r="4.5" class="svg-caries" />
                      <ellipse v-if="sampleConditions[t]?.code === 'FIL'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
                      <path v-if="sampleConditions[t]?.code === 'RCT'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
                    </svg>
                    <!-- SVG 5 Mặt răng (B/L/M/D/O) -->
                    <svg class="t-surf-svg" viewBox="0 0 28 28" aria-hidden="true">
                      <polygon points="2,2 26,2 19,9 9,9" class="surf-poly" :class="surfaceClass(t, 'B')" />
                      <polygon points="9,19 19,19 26,26 2,26" class="surf-poly" :class="surfaceClass(t, 'L')" />
                      <polygon points="2,2 9,9 9,19 2,26" class="surf-poly" :class="surfaceClass(t, 'M')" />
                      <polygon points="26,2 26,26 19,19 19,9" class="surf-poly" :class="surfaceClass(t, 'D')" />
                      <rect x="9" y="9" width="10" height="10" class="surf-poly" :class="surfaceClass(t, 'O')" />
                    </svg>
                  </button>
                </div>

                <div class="fdi-midline-v" aria-hidden="true"></div>

                <div class="fdi-quadrant">
                  <button
                    v-for="t in currentUpperLeft"
                    :key="t"
                    type="button"
                    class="sample-tooth-btn"
                    :class="[
                      { selected: selectedSampleTooth === t },
                      sampleStateClass(t)
                    ]"
                    :title="`Răng #${t} - ${getSampleToothMeta(t).name}`"
                    @click="selectedSampleTooth = t"
                  >
                    <span class="t-num">{{ t }}</span>
                    <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
                      <path :d="getToothSvgPaths(t, true).root" class="svg-root" />
                      <path d="M6,29 Q20,32 34,29" class="svg-gum" />
                      <path :d="getToothSvgPaths(t, true).crown" class="svg-crown" />
                      <path :d="getToothSvgPaths(t, true).groove" class="svg-groove" />
                      <circle v-if="sampleConditions[t]?.code === 'CAR'" cx="20" cy="43" r="4.5" class="svg-caries" />
                      <ellipse v-if="sampleConditions[t]?.code === 'FIL'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
                      <path v-if="sampleConditions[t]?.code === 'RCT'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
                    </svg>
                    <svg class="t-surf-svg" viewBox="0 0 28 28" aria-hidden="true">
                      <polygon points="2,2 26,2 19,9 9,9" class="surf-poly" :class="surfaceClass(t, 'B')" />
                      <polygon points="9,19 19,19 26,26 2,26" class="surf-poly" :class="surfaceClass(t, 'L')" />
                      <polygon points="2,2 9,9 9,19 2,26" class="surf-poly" :class="surfaceClass(t, 'M')" />
                      <polygon points="26,2 26,26 19,19 19,9" class="surf-poly" :class="surfaceClass(t, 'D')" />
                      <rect x="9" y="9" width="10" height="10" class="surf-poly" :class="surfaceClass(t, 'O')" />
                    </svg>
                  </button>
                </div>
              </div>

              <div class="fdi-occlusal-line" aria-hidden="true">
                <span></span><em>Mặt phẳng khớp cắn chuẩn FDI</em><span></span>
              </div>

              <!-- Hàm dưới -->
              <div class="fdi-jaw-row">
                <div class="fdi-quadrant">
                  <button
                    v-for="t in currentLowerRight"
                    :key="t"
                    type="button"
                    class="sample-tooth-btn"
                    :class="[
                      { selected: selectedSampleTooth === t },
                      sampleStateClass(t)
                    ]"
                    :title="`Răng #${t} - ${getSampleToothMeta(t).name}`"
                    @click="selectedSampleTooth = t"
                  >
                    <svg class="t-surf-svg" viewBox="0 0 28 28" aria-hidden="true">
                      <polygon points="2,2 26,2 19,9 9,9" class="surf-poly" :class="surfaceClass(t, 'L')" />
                      <polygon points="9,19 19,19 26,26 2,26" class="surf-poly" :class="surfaceClass(t, 'B')" />
                      <polygon points="2,2 9,9 9,19 2,26" class="surf-poly" :class="surfaceClass(t, 'M')" />
                      <polygon points="26,2 26,26 19,19 19,9" class="surf-poly" :class="surfaceClass(t, 'D')" />
                      <rect x="9" y="9" width="10" height="10" class="surf-poly" :class="surfaceClass(t, 'O')" />
                    </svg>
                    <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
                      <g transform="translate(0,60) scale(1,-1)">
                        <path :d="getToothSvgPaths(t, false).root" class="svg-root" />
                        <path d="M6,29 Q20,32 34,29" class="svg-gum" />
                        <path :d="getToothSvgPaths(t, false).crown" class="svg-crown" />
                        <path :d="getToothSvgPaths(t, false).groove" class="svg-groove" />
                        <circle v-if="sampleConditions[t]?.code === 'CAR'" cx="20" cy="43" r="4.5" class="svg-caries" />
                        <ellipse v-if="sampleConditions[t]?.code === 'FIL'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
                        <path v-if="sampleConditions[t]?.code === 'RCT'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
                      </g>
                    </svg>
                    <span class="t-num">{{ t }}</span>
                  </button>
                </div>

                <div class="fdi-midline-v" aria-hidden="true"></div>

                <div class="fdi-quadrant">
                  <button
                    v-for="t in currentLowerLeft"
                    :key="t"
                    type="button"
                    class="sample-tooth-btn"
                    :class="[
                      { selected: selectedSampleTooth === t },
                      sampleStateClass(t)
                    ]"
                    :title="`Răng #${t} - ${getSampleToothMeta(t).name}`"
                    @click="selectedSampleTooth = t"
                  >
                    <svg class="t-surf-svg" viewBox="0 0 28 28" aria-hidden="true">
                      <polygon points="2,2 26,2 19,9 9,9" class="surf-poly" :class="surfaceClass(t, 'L')" />
                      <polygon points="9,19 19,19 26,26 2,26" class="surf-poly" :class="surfaceClass(t, 'B')" />
                      <polygon points="2,2 9,9 9,19 2,26" class="surf-poly" :class="surfaceClass(t, 'M')" />
                      <polygon points="26,2 26,26 19,19 19,9" class="surf-poly" :class="surfaceClass(t, 'D')" />
                      <rect x="9" y="9" width="10" height="10" class="surf-poly" :class="surfaceClass(t, 'O')" />
                    </svg>
                    <svg class="t-anat-svg" viewBox="0 0 40 60" aria-hidden="true">
                      <g transform="translate(0,60) scale(1,-1)">
                        <path :d="getToothSvgPaths(t, false).root" class="svg-root" />
                        <path d="M6,29 Q20,32 34,29" class="svg-gum" />
                        <path :d="getToothSvgPaths(t, false).crown" class="svg-crown" />
                        <path :d="getToothSvgPaths(t, false).groove" class="svg-groove" />
                        <circle v-if="sampleConditions[t]?.code === 'CAR'" cx="20" cy="43" r="4.5" class="svg-caries" />
                        <ellipse v-if="sampleConditions[t]?.code === 'FIL'" cx="20" cy="43" rx="5.5" ry="3.8" class="svg-filling" />
                        <path v-if="sampleConditions[t]?.code === 'RCT'" d="M16,10 L18,42 M24,10 L22,42 M16,42 H24" class="svg-rct" />
                      </g>
                    </svg>
                    <span class="t-num">{{ t }}</span>
                  </button>
                </div>
              </div>

              <div class="fdi-arch-label fdi-arch-bottom">
                <span>HÀM DƯỚI ({{ sampleDentition === 'adult' ? 'Phần tư hàm 4 & 3 · 48 → 38' : 'Phần tư hàm 8 & 7 · 85 → 75' }})</span>
              </div>
            </div>

            <!-- Chú giải màu bệnh lý mẫu -->
            <div class="fdi-legend-row" aria-label="Chú giải tình trạng răng mẫu">
              <span class="leg-item"><i class="leg-dot dot-normal"></i> Răng khỏe mạnh</span>
              <span class="leg-item"><i class="leg-dot dot-caries"></i> Sâu răng (Caries)</span>
              <span class="leg-item"><i class="leg-dot dot-filling"></i> Đã trám (Filling)</span>
              <span class="leg-item"><i class="leg-dot dot-rct"></i> Đã chữa tủy (RCT)</span>
              <span class="leg-item"><i class="leg-dot dot-crown"></i> Mão sứ (Crown)</span>
              <span class="leg-item"><i class="leg-dot dot-implant"></i> Cấy ghép trụ răng</span>
            </div>

            <!-- Hộp thông tin chi tiết của chiếc răng đang chọn -->
            <div class="selected-tooth-inspector">
              <div class="inspector-badge">Răng #{{ selectedSampleTooth }}</div>
              <div class="inspector-body">
                <div class="inspector-title-row">
                  <h3>{{ currentToothDetail.name }}</h3>
                  <span class="pill-badge" :class="currentToothDetail.badgeClass">{{ currentToothDetail.statusLabel }}</span>
                </div>
                <p>{{ currentToothDetail.anatomy }} · <strong>{{ currentToothDetail.clinicalNote }}</strong></p>
              </div>
            </div>
          </div>

          <!-- Cột Phải: An toàn điều trị 3 lớp (Tông Sáng Y khoa Đồng nhất) -->
          <div class="safety-protocol-card">
            <div class="safety-card-head">
              <span class="pill-badge pill-red">Bắt buộc trước tiêm tê &amp; tiểu phẫu</span>
              <h3>Hệ thống Cảnh báo An toàn Lâm sàng 3 Lớp</h3>
              <p>Màn hình làm việc của Nha sĩ tự động kiểm tra và hiển thị cảnh báo từ hồ sơ bệnh nhân trước khi thực hiện thủ thuật:</p>
            </div>

            <div class="safety-alert-preview">
              <div class="safety-alert-title">⚠️ Cảnh báo Đỏ Tự động</div>
              <p>Nổi bật ngay trên đầu hồ sơ nếu bệnh nhân có <strong>dị ứng kháng sinh / thuốc gây tê</strong>, <strong>bệnh lý tim mạch</strong> hoặc <strong>huyết áp ≥ 140/90 mmHg</strong>.</p>
            </div>

            <div class="safety-steps">
              <div class="safety-step-item">
                <span class="step-number">01</span>
                <div>
                  <h4>Sàng lọc Tiền sử Dị ứng Thuốc</h4>
                  <p>Ghi nhận chi tiết dị ứng kháng sinh, thuốc giảm đau hoặc thuốc gây tê để ngăn chặn kê đơn trùng hoạt chất.</p>
                </div>
              </div>
              <div class="safety-step-item">
                <span class="step-number">02</span>
                <div>
                  <h4>Theo dõi Bệnh nền &amp; Đo Sinh hiệu</h4>
                  <p>Đo Huyết áp, Mạch, Nhiệt độ và SpO₂ trực tiếp tại ghế khám; lưu lịch sử append-only theo từng lần khám (Visit).</p>
                </div>
              </div>
              <div class="safety-step-item">
                <span class="step-number">03</span>
                <div>
                  <h4>Đồng bộ Ảnh X-quang &amp; Đơn thuốc QR</h4>
                  <p>Phim chụp X-quang kỹ thuật số và đơn thuốc điện tử gắn trực tiếp vào vị trí răng FDI để khách hàng tra cứu mọi lúc.</p>
                </div>
              </div>
            </div>

            <div class="safety-card-foot">
              <RouterLink class="button button-outline button-sm" to="/login">Đăng nhập xem Hồ sơ răng của tôi →</RouterLink>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 6. Minh bạch Tài chính & Thực đơn Bảng giá (Editorial Split 5 Cột + 7 Cột) -->
    <section id="minh-bach-chi-phi" class="section-block section-white" aria-labelledby="billing-heading">
      <div class="container">
        <div class="bento-grid">
          <!-- Cột Trái (5 cột): Ba Nguyên tắc Minh bạch & Trả góp -->
          <div class="bento-card bento-5 bento-sky-solid">
            <p class="eyebrow">Minh bạch chi phí &amp; Thanh toán linh hoạt</p>
            <h2 id="billing-heading" class="split-h2">Chính sách Chi phí Rõ ràng &amp; Hỗ trợ Thanh toán Nhiều Đợt</h2>
            <p class="bento-desc">Cam kết không phát sinh chi phí ẩn — khách hàng luôn nắm rõ tổng chi phí, số đã thu và dư nợ còn lại sau mỗi buổi hẹn.</p>
            <div class="billing-timeline">
              <div class="billing-tl-item">
                <strong>01. Khóa Đơn giá Snapshot</strong>
                <span>Đơn giá hiện hành được lưu cố định ngay khi chỉ định dịch vụ trên răng FDI, không bị ảnh hưởng nếu bảng giá thay đổi về sau.</span>
              </div>
              <div class="billing-tl-item">
                <strong>02. Linh hoạt Thu 1 Lần hoặc Trả góp</strong>
                <span>Với Chỉnh nha, Cấy ghép trụ răng hoặc Phục hình nhiều răng sứ, khách hàng chia nhỏ thanh toán theo tiến độ từng đợt tái khám.</span>
              </div>
              <div class="billing-tl-item">
                <strong>03. Biên lai &amp; Đối soát Trực tuyến</strong>
                <span>Mỗi giao dịch Tiền mặt hoặc Chuyển khoản đều xuất biên lai rõ ràng và cập nhật tức thì trên Cổng bệnh nhân.</span>
              </div>
            </div>
          </div>

          <!-- Cột Phải (7 cột): Thực đơn Bảng giá Niêm yết -->
          <div class="bento-card bento-7 price-menu-card">
            <div class="price-menu-head">
              <div>
                <h3>Bảng giá Tham khảo Các Nhóm Dịch vụ Chính</h3>
                <p>Tính theo nguyên đồng VND · Bác sĩ luôn tư vấn và thống nhất chi phí trước khi thực hiện</p>
              </div>
              <span class="pill-badge pill-gold">Niêm yết công khai</span>
            </div>
            <div class="price-list">
              <div v-for="row in priceMenu" :key="row.name" class="price-row">
                <div>
                  <div class="price-name">{{ row.name }}</div>
                  <div class="price-meta">{{ row.meta }}</div>
                </div>
                <div class="price-dots" aria-hidden="true"></div>
                <div class="price-right">
                  <div class="price-val">{{ row.price }}</div>
                  <span class="price-mode" :class="row.installment ? 'mode-gold' : 'mode-green'">{{ row.mode }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 7. Quy trình 5 bước đặt lịch & thăm khám (#cach-dat-lich giữ nguyên ID chuẩn) -->
    <section id="cach-dat-lich" class="guide" aria-labelledby="guide-heading">
      <div class="section-heading">
        <p class="eyebrow">Đồng hành cùng nụ cười của bạn</p>
        <h2 id="guide-heading">Đặt lịch khám thật đơn giản &amp; Khép kín 5 Bước</h2>
        <p>Chỉ vài bước để gửi yêu cầu trực tuyến và trải nghiệm quy trình thăm khám chuẩn mực tại Nha khoa Hoàng Gia.</p>
      </div>
      <div class="steps steps-five">
        <article class="step">
          <div class="step-top">
            <span class="step-number" aria-hidden="true">01</span>
            <span class="step-tag">Trực tuyến 30s</span>
          </div>
          <h3>Chọn lịch phù hợp</h3>
          <p>Điền họ tên, số điện thoại và chọn ngày, khung giờ bạn mong muốn mà không cần tạo tài khoản trước.</p>
        </article>
        <article class="step">
          <div class="step-top">
            <span class="step-number" aria-hidden="true">02</span>
            <span class="step-tag">Lễ tân hỗ trợ</span>
          </div>
          <h3>Chờ xác nhận</h3>
          <p>Lễ tân tiếp nhận yêu cầu, kiểm tra công suất khung giờ và liên hệ xác nhận lịch hẹn của bạn.</p>
        </article>
        <article class="step">
          <div class="step-top">
            <span class="step-number" aria-hidden="true">03</span>
            <span class="step-tag">Tại quầy tiếp đón</span>
          </div>
          <h3>Đến phòng khám &amp; đối chiếu</h3>
          <p>Mang CCCD hoặc Hộ chiếu để nhân viên đối chiếu thông tin tại quầy và kích hoạt tài khoản Cổng bệnh nhân.</p>
        </article>
        <article class="step">
          <div class="step-top">
            <span class="step-number" aria-hidden="true">04</span>
            <span class="step-tag">An toàn &amp; FDI</span>
          </div>
          <h3>Khám Sơ đồ FDI &amp; X-quang</h3>
          <p>Đo sinh hiệu, khám từng mặt răng trên sơ đồ răng FDI, chụp phim kỹ thuật số và thống nhất kế hoạch điều trị.</p>
        </article>
        <article class="step">
          <div class="step-top">
            <span class="step-number" aria-hidden="true">05</span>
            <span class="step-tag">Chăm sóc dài lâu</span>
          </div>
          <h3>Điều trị &amp; Theo dõi Hồ sơ</h3>
          <p>Thực hiện điều trị vô khuẩn, nhận đơn thuốc điện tử có mã QR và tra cứu lịch sử đợt trả trên Cổng bệnh nhân.</p>
        </article>
      </div>
      <RouterLink class="guide-link" to="/book-appointment">Gửi yêu cầu đặt lịch <span aria-hidden="true">→</span></RouterLink>
    </section>

    <!-- 8. Câu hỏi thường gặp (Editorial Split 4 Cột + 8 Cột) -->
    <section id="cau-hoi-thuong-gap" class="section-block section-white" aria-labelledby="faq-heading">
      <div class="container">
        <div class="bento-grid">
          <div class="bento-4 faq-side">
            <p class="eyebrow">Cẩm nang khách hàng</p>
            <h2 id="faq-heading" class="split-h2">Câu hỏi Thường gặp Khi Đến Khám tại Nha khoa Hoàng Gia</h2>
            <p class="bento-desc">Giải đáp nhanh các thắc mắc về quy trình đặt lịch, kích hoạt tài khoản Cổng bệnh nhân, quyền Người giám hộ và chăm sóc răng miệng.</p>
            <div class="faq-help-box">
              <strong>Bạn cần hỗ trợ chọn khung giờ khám?</strong>
              <p>Gửi yêu cầu đặt lịch trực tuyến, Lễ tân sẽ gọi lại tư vấn chi tiết cho bạn và gia đình.</p>
              <RouterLink class="button button-primary button-sm" to="/book-appointment">Đặt lịch hẹn ngay →</RouterLink>
            </div>
          </div>
          <div class="bento-8 faq-grid">
            <article v-for="faq in faqs" :key="faq.q" class="faq-card">
              <h3>{{ faq.q }}</h3>
              <p>{{ faq.a }}</p>
            </article>
          </div>
        </div>
      </div>
    </section>

    <!-- 9. Khối Kêu gọi Đặt lịch Cuối trang -->
    <section class="cta-banner" aria-label="Đặt lịch khám ngay hôm nay">
      <div class="container">
        <div class="cta-box">
          <div class="cta-copy">
            <img src="/logo-icon-nen-toi.svg" alt="" width="64" height="64" class="cta-emblem" />
            <div>
              <p class="cta-eyebrow">Sẵn sàng đồng hành cùng nụ cười của bạn</p>
              <h2>Đặt lịch khám ngay hôm nay — Không cần đăng nhập trước</h2>
              <p>Chọn khung giờ thuận tiện nhất cho bạn hoặc người thân. Lễ tân phòng khám sẽ liên hệ xác nhận và chuẩn bị chu đáo cho buổi thăm khám.</p>
            </div>
          </div>
          <div class="cta-buttons">
            <RouterLink class="button button-gold" to="/book-appointment">Đặt lịch khám ngay <span aria-hidden="true">→</span></RouterLink>
            <RouterLink class="button button-light" to="/login">Đăng nhập Cổng bệnh nhân</RouterLink>
          </div>
        </div>
      </div>
    </section>

    <!-- 10. Chân trang đầy đủ 4 cột -->
    <footer class="footer">
      <div class="footer-grid">
        <div class="footer-col">
          <img src="/logo-ngang-nen-toi.svg" alt="Nha khoa Hoàng Gia" width="180" height="46" class="footer-logo" />
          <p class="footer-desc">Hệ thống quản lý và chăm sóc răng miệng toàn diện — Khám răng, Điều trị chuyên sâu, Phục hình thẩm mỹ trên nền tảng Sơ đồ răng giải phẫu FDI.</p>
        </div>
        <div class="footer-col">
          <h4>Chuyên khoa chính</h4>
          <a href="#chuyen-khoa">Khám tổng quát &amp; Sơ đồ răng FDI</a>
          <a href="#chuyen-khoa">Trám răng &amp; Điều trị tủy</a>
          <a href="#chuyen-khoa">Răng sứ &amp; Cấy ghép trụ răng</a>
          <a href="#chuyen-khoa">Chỉnh nha &amp; Nha khoa trẻ em</a>
        </div>
        <div class="footer-col">
          <h4>Dành cho khách hàng</h4>
          <RouterLink to="/book-appointment">Đặt lịch khám trực tuyến</RouterLink>
          <RouterLink to="/login">Đăng nhập Cổng bệnh nhân</RouterLink>
          <RouterLink to="/activate-account">Kích hoạt tài khoản lần đầu (OTP)</RouterLink>
          <RouterLink to="/forgot-password">Khôi phục mật khẩu</RouterLink>
        </div>
        <div class="footer-col">
          <h4>Thời gian làm việc</h4>
          <span>Thứ 2 – Thứ 7: 07:30 – 20:00</span>
          <span>Chủ Nhật: 07:30 – 12:00</span>
          <span>Tiếp đón đối soát CCCD &amp; Hộ chiếu tại quầy</span>
        </div>
      </div>
      <div class="footer-bottom">
        <span>Nha khoa Hoàng Gia — Chăm sóc nụ cười chuẩn y khoa</span>
        <RouterLink to="/login">Đăng nhập</RouterLink>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';

// Cuộn trượt mượt mà xuống các phân đoạn chức năng thay vì nhảy chuyển trang
function scrollToSection(targetId: string) {
  const el = document.getElementById(targetId);
  if (!el) return;

  const header = document.querySelector('.topbar') as HTMLElement | null;
  const headerHeight = header ? header.offsetHeight : 70;
  const elementPosition = el.getBoundingClientRect().top;
  const offsetPosition = elementPosition + window.pageYOffset - headerHeight - 12;

  window.scrollTo({
    top: Math.max(0, offsetPosition),
    behavior: 'smooth'
  });

  if (window.history && window.history.replaceState) {
    window.history.replaceState(null, '', '#' + targetId);
  }
}

onMounted(() => {
  if (window.location.hash) {
    const hash = window.location.hash.replace('#', '');
    setTimeout(() => {
      scrollToSection(hash);
    }, 150);
  }
});

// --- SƠ ĐỒ RĂNG GIẢI PHẪU FDI MẪU (32 RĂNG NGƯỜI LỚN & 20 RĂNG SỮA) ---
const sampleDentition = ref<'adult' | 'child'>('adult');
const selectedSampleTooth = ref<number>(46);

const adultUpperRight = [18, 17, 16, 15, 14, 13, 12, 11];
const adultUpperLeft = [21, 22, 23, 24, 25, 26, 27, 28];
const adultLowerRight = [48, 47, 46, 45, 44, 43, 42, 41];
const adultLowerLeft = [31, 32, 33, 34, 35, 36, 37, 38];

const childUpperRight = [55, 54, 53, 52, 51];
const childUpperLeft = [61, 62, 63, 64, 65];
const childLowerRight = [85, 84, 83, 82, 81];
const childLowerLeft = [71, 72, 73, 74, 75];

const currentUpperRight = computed(() => (sampleDentition.value === 'adult' ? adultUpperRight : childUpperRight));
const currentUpperLeft = computed(() => (sampleDentition.value === 'adult' ? adultUpperLeft : childUpperLeft));
const currentLowerRight = computed(() => (sampleDentition.value === 'adult' ? adultLowerRight : childLowerRight));
const currentLowerLeft = computed(() => (sampleDentition.value === 'adult' ? adultLowerLeft : childLowerLeft));

function switchSampleDentition(mode: 'adult' | 'child') {
  sampleDentition.value = mode;
  selectedSampleTooth.value = mode === 'adult' ? 46 : 84;
}

// Tình trạng mẫu trên một số răng để người xem thấy rõ cách Sơ đồ FDI hoạt động
const sampleConditions: Record<number, { code: 'CAR' | 'FIL' | 'RCT' | 'CRN' | 'IMP'; surfaces: string[]; label: string; note: string }> = {
  16: {
    code: 'FIL',
    surfaces: ['O', 'M'],
    label: 'Đã trám Composite (Mặt O, M)',
    note: 'Đã phục hồi xoang sâu mặt nhai và mặt gần bằng Composite đa lớp.'
  },
  21: {
    code: 'CRN',
    surfaces: ['B', 'L', 'M', 'D', 'O'],
    label: 'Mão sứ Zirconia thẩm mỹ',
    note: 'Phục hình mão toàn sứ Zirconia khít sát viền nướu vùng răng cửa.'
  },
  26: {
    code: 'RCT',
    surfaces: ['O'],
    label: 'Đã điều trị tủy (Nội nha)',
    note: 'Đã làm sạch và trám bít 3 ống tủy dưới kiểm soát X-quang quanh chóp.'
  },
  36: {
    code: 'IMP',
    surfaces: ['O'],
    label: 'Cấy ghép trụ răng Titanium',
    note: 'Phục hồi răng cối lớn đã mất bằng trụ Titanium tích hợp xương độc lập.'
  },
  46: {
    code: 'CAR',
    surfaces: ['O', 'D'],
    label: 'Sâu ngà răng (Mặt O, D) — Cần điều trị',
    note: 'Ghi nhận tổn thương sâu mặt nhai và mặt xa, chỉ định trám phục hồi bảo tồn tủy.'
  },
  54: {
    code: 'FIL',
    surfaces: ['O'],
    label: 'Trám bít hố rãnh dự phòng',
    note: 'Đã trám bít hố rãnh mặt nhai răng hàm sữa bảo vệ men răng cho bé.'
  },
  84: {
    code: 'CAR',
    surfaces: ['O', 'M'],
    label: 'Sâu răng sữa (Mặt O, M)',
    note: 'Cần trám phục hồi sớm để giữ khoảng mọc cho mầm răng vĩnh viễn bên dưới.'
  }
};

function sampleStateClass(t: number): string {
  const c = sampleConditions[t]?.code;
  if (!c) return 'st-normal';
  return `st-${c.toLowerCase()}`;
}

function surfaceClass(t: number, surf: string): string {
  const cond = sampleConditions[t];
  if (!cond || !cond.surfaces.includes(surf)) return '';
  return `surf-${cond.code.toLowerCase()}`;
}

function getToothSvgPaths(num: number, isUpper: boolean) {
  const pos = num % 10;
  const isChild = num >= 51;
  const isMolar = isChild ? pos >= 4 : pos >= 6;
  const isPremolar = !isChild && (pos === 4 || pos === 5);
  const isCanine = pos === 3;

  if (isMolar) {
    return {
      root: isUpper
        ? 'M8,30 C6,16 8,5 12,4 C14,4 15,15 16,21 C18,12 19,3 20,3 C21,3 22,12 24,21 C25,15 26,4 28,4 C32,5 34,16 32,30 Z'
        : 'M8,30 C6,16 9,4 14,4 C17,5 18,15 20,21 C22,15 23,5 26,4 C31,4 34,16 32,30 Z',
      crown: 'M5,29 C3,35 3,47 7,53 C9,56 13,56 16,53 C18,56 22,56 24,53 C27,56 31,56 33,53 C37,47 37,35 35,29 C28,27 12,27 5,29 Z',
      groove: 'M10,43 Q20,46 30,43 M16,36 L16,51 M24,36 L24,51'
    };
  }
  if (isPremolar) {
    return {
      root: 'M12,30 C11,17 13,5 16,4 C18,4 19,12 20,19 C21,12 22,4 24,4 C27,5 29,17 28,30 Z',
      crown: 'M8,29 C5,34 5,47 9,53 C12,56 17,55 20,52 C23,55 28,56 31,53 C35,47 35,34 32,29 C26,27 14,27 8,29 Z',
      groove: 'M12,43 Q20,46 28,43 M20,37 L20,50'
    };
  }
  if (isCanine) {
    return {
      root: 'M14,30 C13,12 16,2 20,2 C24,2 27,12 26,30 Z',
      crown: 'M9,29 C7,34 8,45 11,51 L20,58 L29,51 C32,45 33,34 31,29 C26,27 14,27 9,29 Z',
      groove: 'M20,37 L20,53'
    };
  }
  return {
    root: 'M14,30 C13,14 16,3 20,3 C24,3 27,14 26,30 Z',
    crown: 'M9,29 C7,34 7,46 9,53 C13,55 27,55 31,53 C33,46 33,34 31,29 C26,27 14,27 9,29 Z',
    groove: 'M13,50 H27'
  };
}

function getSampleToothMeta(num: number) {
  const q = Math.floor(num / 10);
  const pos = num % 10;
  const arch = q === 1 || q === 2 || q === 5 || q === 6 ? 'hàm trên' : 'hàm dưới';
  const side = q === 1 || q === 4 || q === 5 || q === 8 ? 'bên phải' : 'bên trái';

  let typeName = 'Răng cửa';
  let anatomy = '1 chân răng thuôn, rìa cắn phẳng giữ vai trò thẩm mỹ và phát âm';
  if (pos === 3) {
    typeName = 'Răng nanh';
    anatomy = '1 chân răng dài vững chắc, đỉnh múi nhọn hướng dẫn khớp cắn';
  } else if (q <= 4 && (pos === 4 || pos === 5)) {
    typeName = `Răng tiền hàm thứ ${pos - 3}`;
    anatomy = 'Mặt nhai 2 múi (ngoài B & trong L), chuyển tiếp lực nhai';
  } else if (pos >= 4) {
    typeName = q >= 5 ? `Răng hàm sữa thứ ${pos - 3}` : pos === 8 ? 'Răng khôn (Răng số 8)' : `Răng cối lớn thứ ${pos - 5}`;
    anatomy = 'Bản nhai rộng nhiều múi, 2–3 chân răng chịu lực ăn nhai chính';
  } else {
    typeName = pos === 1 ? 'Răng cửa giữa' : 'Răng cửa bên';
  }

  const cond = sampleConditions[num];
  return {
    name: `${typeName} ${arch} ${side}`,
    anatomy,
    statusLabel: cond ? cond.label : 'Răng khỏe mạnh (5 mặt B·L·M·D·O nguyên vẹn)',
    badgeClass: cond
      ? cond.code === 'CAR'
        ? 'pill-red'
        : cond.code === 'CRN'
          ? 'pill-gold'
          : 'pill-blue'
      : 'pill-green',
    clinicalNote: cond
      ? cond.note
      : 'Mô men và ngà răng lành mạnh; khuyến nghị khám định kỳ và lấy cao răng siêu âm 6 tháng/lần.'
  };
}

const currentToothDetail = computed(() => getSampleToothMeta(selectedSampleTooth.value));

const bentoRowTwo = [
  {
    group: 'Phục hồi & Nội nha',
    duration: '45–60 phút / buổi hẹn',
    title: 'Trám răng Thẩm mỹ & Điều trị Tủy (Nội nha)',
    description: 'Phục hồi hình thể răng sâu, mẻ và điều trị viêm tủy bảo tồn răng thật tối đa dưới hỗ trợ của X-quang kỹ thuật số.',
    highlights: [
      'Trám phục hồi xoang sâu bằng vật liệu Composite đa lớp',
      'Làm sạch và tạo hình ống tủy chính xác theo chiều dài chân răng',
      'Theo dõi sát kết quả phục hồi qua từng lần tái khám'
    ],
    paymentMode: 'Hỗ trợ chia đợt theo lần hẹn'
  },
  {
    group: 'Nha khoa trẻ em',
    duration: '30 phút / buổi hẹn',
    title: 'Nha khoa Trẻ em & Dự phòng Sâu răng (20 Răng sữa)',
    description: 'Chăm sóc răng sữa thân thiện, nhẹ nhàng và theo dõi quá trình thay răng vĩnh viễn đúng độ tuổi cho trẻ.',
    highlights: [
      'Sử dụng Sơ đồ 20 răng sữa chuyên biệt (ký hiệu FDI 51–85)',
      'Trám bít hố rãnh và bôi Fluor dự phòng sâu răng sớm',
      'Phụ huynh xem hồ sơ và đặt lịch cho bé qua quyền Người giám hộ'
    ],
    paymentMode: 'Thanh toán theo lần khám'
  },
  {
    group: 'Tiểu phẫu an toàn',
    duration: '45 phút / ca',
    title: 'Tiểu phẫu Nhổ Răng khôn Mọc lệch Ít Sang chấn',
    description: 'Khảo sát kỹ vị trí chân răng trên phim X-quang và kiểm soát chặt chẽ sinh hiệu trước khi thực hiện tiểu phẫu.',
    highlights: [
      'Đánh giá tương quan chân răng và cấu trúc giải phẫu lân cận',
      'Kiểm tra huyết áp và tiền sử dị ứng thuốc tê trước thủ thuật',
      'Kê đơn thuốc điện tử có mã QR và hướng dẫn chăm sóc hậu phẫu'
    ],
    paymentMode: 'Thanh toán 1 lần'
  }
];

const priceMenu = [
  {
    name: 'Khám tổng quát & Lập phác đồ Sơ đồ răng FDI',
    meta: 'DV01 · Khám 32 răng người lớn / 20 răng sữa trẻ em',
    price: '100.000 ₫ / lần',
    mode: 'Thu 1 lần',
    installment: false
  },
  {
    name: 'Lấy cao răng siêu âm & Đánh bóng 2 hàm',
    meta: 'DV02 · Nha chu & Dự phòng viêm nướu',
    price: '250.000 ₫ / 2 hàm',
    mode: 'Thu 1 lần',
    installment: false
  },
  {
    name: 'Trám răng thẩm mỹ Composite (Theo mặt răng FDI)',
    meta: 'DV04 · Phục hồi mặt nhai (O) / mặt bên (M, D)',
    price: '400.000 ₫ / răng',
    mode: 'Thu 1 lần',
    installment: false
  },
  {
    name: 'Điều trị tủy răng hàm lớn (Nội nha 3 ống tủy)',
    meta: 'DV07 · Kèm kiểm tra X-quang quanh chóp kỹ thuật số',
    price: '1.800.000 ₫ / răng',
    mode: 'Chia theo buổi hẹn',
    installment: false
  },
  {
    name: 'Bọc răng sứ thẩm mỹ Zirconia chính hãng',
    meta: 'DV08 · Phục hình cố định bảo hành điện tử',
    price: '5.500.000 ₫ / răng',
    mode: 'Trả góp nhiều đợt',
    installment: true
  },
  {
    name: 'Cấy ghép trụ răng Titanium (Trọn gói 1 trụ)',
    meta: 'DV10 · Phục hồi chân răng mất độc lập trên cung hàm',
    price: '18.000.000 ₫ / trụ',
    mode: 'Trả góp nhiều đợt',
    installment: true
  },
  {
    name: 'Chỉnh nha — Niềng răng mắc cài kim loại (2 hàm)',
    meta: 'DV11 · Lộ trình 12–24 tháng · Thanh toán theo đợt tái khám',
    price: '32.000.000 ₫ / gói',
    mode: 'Trả góp nhiều đợt',
    installment: true
  }
];

const faqs = [
  {
    q: '1. Khi đặt lịch khám trên website, tôi có cần đăng nhập hay nhập mã OTP không?',
    a: 'Không cần. Bạn chỉ cần nhập họ tên, số điện thoại liên hệ và chọn ngày, khung giờ mong muốn. Lễ tân phòng khám sẽ tiếp nhận yêu cầu và liên hệ xác nhận lịch hẹn với bạn.'
  },
  {
    q: '2. Làm thế nào để tôi có tài khoản đăng nhập vào Cổng bệnh nhân?',
    a: 'Khi bạn đến phòng khám lần đầu, Lễ tân sẽ đối chiếu Căn cước công dân (12 số) hoặc Hộ chiếu kèm quốc gia cấp tại quầy để kích hoạt hồ sơ. Ở lần đăng nhập web đầu tiên, bạn xác minh OTP gửi về điện thoại và đặt mật khẩu cá nhân.'
  },
  {
    q: '3. Cha mẹ muốn đặt lịch và xem hồ sơ khám răng cho con nhỏ thì thực hiện ra sao?',
    a: 'Nhiều hồ sơ trong gia đình có thể sử dụng chung số điện thoại liên hệ. Sau khi nhân viên ghi nhận quan hệ Người giám hộ hợp pháp tại quầy, bạn có thể xem sơ đồ 20 răng sữa và đặt lịch khám cho bé ngay trên tài khoản của mình.'
  },
  {
    q: '4. Tôi có tiền sử dị ứng kháng sinh hoặc huyết áp cao thì khám răng có an toàn không?',
    a: 'Rất an toàn. Hệ thống bắt buộc ghi nhận sinh hiệu (Huyết áp, Mạch, SpO₂) và tiền sử bệnh lý/dị ứng để hiển thị Cảnh báo an toàn nổi bật trên màn hình của Nha sĩ trước khi kê đơn hay tiêm tê.'
  },
  {
    q: '5. Chính sách thanh toán nhiều đợt (trả góp) hoạt động như thế nào?',
    a: 'Với các dịch vụ điều trị dài ngày, hóa đơn ghi nhận rõ tổng chi phí đã khóa giá (snapshot), số tiền đã thu ở từng đợt và số dư nợ còn lại. Bạn có thể xem toàn bộ lịch sử biên lai trên Cổng bệnh nhân.'
  },
  {
    q: '6. Tôi muốn xem lại đơn thuốc hoặc hình ảnh X-quang của lần khám trước ở đâu?',
    a: 'Bạn có thể quét mã QR in trên phiếu đơn thuốc để tra cứu nhanh hoặc đăng nhập vào Cổng bệnh nhân để xem đầy đủ Sơ đồ răng FDI, hình ảnh X-quang và đơn thuốc của từng lần khám.'
  }
];
</script>

<style scoped>
/* Bảng màu Sáng Trắng Sứ & Xanh Lam Ngọc hài hòa chuẩn Nha khoa Hoàng Gia */
.home { min-height: 100vh; color: #18354c; background: #f8fbff; scroll-behavior: smooth; }
.container { max-width: 1200px; margin: 0 auto; padding: 0 24px; box-sizing: border-box; }

/* Trượt xuống mượt mà và cách đỉnh bù trừ chiều cao sticky header */
#chuyen-khoa, #so-do-fdi, #minh-bach-chi-phi, #cach-dat-lich, #cau-hoi-thuong-gap, .section-block, .guide {
  scroll-margin-top: 76px;
}

/* 1. Topbar (Gọn gàng 3 vùng: Logo - 4 liên kết ngắn - Cụm nút Đăng nhập/Đặt lịch luôn cố định) */
.topbar {
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid #e4eef6;
  position: sticky;
  top: 0;
  z-index: 30;
  padding-top: max(0px, env(safe-area-inset-top, 0px));
}
.topbar-inner {
  max-width: 1200px;
  min-height: 70px;
  margin: auto;
  padding: 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  box-sizing: border-box;
}
.brand { display: inline-flex; flex-shrink: 0; align-items: center; }
.brand img { display: block; width: 158px; height: auto; }

.nav-center {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}
.nav-link {
  color: #18354c;
  font-size: .88rem;
  font-weight: 700;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 12px;
  border-radius: 999px;
  white-space: nowrap;
  transition: background-color .16s, color .16s;
}
.nav-link:hover { background: #eff8ff; color: #1857b6; }
.nav-ic { width: 15px; height: 15px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; flex-shrink: 0; }

.nav-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}
.login-link {
  color: #1857b6;
  background: #fff;
  border: 1px solid #b9d0e6;
  border-radius: 10px;
  padding: 9px 15px;
  font-size: .86rem;
  font-weight: 750;
  text-decoration: none;
  white-space: nowrap;
  transition: background-color .15s;
}
.login-link:hover { background: #eff8ff; }

/* Buttons & Pills */
.button { display: inline-flex; min-height: 42px; box-sizing: border-box; align-items: center; justify-content: center; gap: 8px; padding: 0 18px; border-radius: 10px; text-decoration: none; font-size: .88rem; font-weight: 750; transition: transform .15s, background-color .15s, color .15s; white-space: nowrap; }
.button:hover { transform: translateY(-1px); }
.button-sm { min-height: 36px; padding: 0 14px; font-size: .82rem; border-radius: 9px; }
.button-gold { background: #e5b95e; color: #102e56; }
.button-gold:hover { background: #d9a948; }
.button-primary { background: #1857b6; color: #fff; }
.button-primary:hover { background: #124794; }
.button-outline { border: 1px solid #b9d0e6; background: #fff; color: #1857b6; }
.button-outline:hover { background: #edf6fd; }
.button-ghost { color: #1857b6; background: rgba(24,87,182,.07); }
.button-ghost:hover { background: rgba(24,87,182,.13); }
.button-light { background: #fff; color: #102e56; }
.button-light:hover { background: #f1f9ff; }

.pill-badge { display: inline-flex; align-items: center; padding: 4px 11px; border-radius: 999px; font-size: .75rem; font-weight: 700; }
.pill-green { background: #dcfce7; color: #15803d; }
.pill-blue { background: #d8ecfa; color: #124794; }
.pill-red { background: #fee2e2; color: #b91c1c; }
.pill-gold { background: #fbf1dc; color: #8a6414; }

/* 2. Editorial Hero */
.hero {
  overflow: hidden;
  background:
    radial-gradient(circle at 82% 28%, rgba(57, 123, 204, 0.12) 0%, transparent 45%),
    linear-gradient(180deg, #eff8ff 0%, #f8fbff 100%);
  border-bottom: 1px solid #e4eef6;
}
.hero-inner { max-width: 1200px; margin: auto; padding: 44px 24px 68px; display: grid; grid-template-columns: 1.15fr .85fr; align-items: center; gap: 36px; box-sizing: border-box; }
.hero-copy { position: relative; z-index: 1; }
.eyebrow { color: #1857b6; text-transform: uppercase; letter-spacing: .09em; font-size: .76rem; font-weight: 800; margin: 0; }
.eyebrow span { color: #cc9b3a; margin-right: 5px; }
h1 { margin: 12px 0 14px; color: #102e56; max-width: 14ch; font-size: clamp(2.4rem, 4.3vw, 3.75rem); line-height: 1.08; letter-spacing: -.035em; }
h1 span { color: #397bcc; }
.intro { max-width: 580px; color: #4a6479; font-size: .98rem; line-height: 1.7; margin: 0; }
.hero-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 22px; }
.hero-metrics-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid rgba(24, 87, 182, 0.14);
}
.hero-metric-item { display: flex; flex-direction: column; gap: 2px; }
.hero-metric-item strong { font-size: 1.2rem; font-weight: 800; color: #102e56; letter-spacing: -.02em; }
.hero-metric-item span { font-size: .75rem; color: #526c80; font-weight: 600; }
.hero-note { margin: 14px 0 0; color: #526c80; font-size: .82rem; line-height: 1.5; }

.hero-stage { position: relative; min-height: 340px; display: grid; place-items: center; }
.hero-art { position: relative; width: 100%; min-height: 280px; display: grid; place-items: center; isolation: isolate; }
.hero-art:before { content: ""; position: absolute; z-index: -2; width: 296px; height: 296px; border-radius: 50%; background: radial-gradient(circle, #d8ecfa 0%, rgba(216,236,250,.35) 70%, transparent 100%); }
.art-orbit { position: absolute; border: 1.5px solid #a6d7ef; border-radius: 50%; aspect-ratio: 1; }
.art-orbit-outer { width: 266px; border-style: dashed; }
.art-orbit-inner { width: 220px; border-color: #fff; }
.art-medallion { width: 178px; aspect-ratio: 1; display: grid; place-items: center; background: #fff; border-radius: 50%; box-shadow: 0 20px 50px #1857b622; border: 1px solid #e4eef6; }
.art-medallion img { width: 72%; height: auto; }
.art-sparkle { position: absolute; color: #d4a84e; font-size: 1.7rem; }
.sparkle-one { top: 14%; right: 16%; }
.sparkle-two { bottom: 14%; left: 16%; }

.hero-float-badge {
  position: absolute;
  background: rgba(255, 255, 255, 0.96);
  backdrop-filter: blur(12px);
  border: 1px solid #d8ecfa;
  border-radius: 15px;
  padding: 10px 14px;
  box-shadow: 0 14px 32px rgba(16, 46, 86, 0.10);
  display: flex;
  align-items: center;
  gap: 10px;
  z-index: 2;
}
.hero-float-badge strong { display: block; font-size: .8rem; color: #102e56; }
.hero-float-badge span { display: block; font-size: .71rem; color: #526c80; }
.badge-top-left { top: 6px; left: 0; }
.badge-bottom-right { bottom: 6px; right: 0; }
.float-ic { width: 34px; height: 34px; border-radius: 9px; background: #eff8ff; border: 1px solid #d8ecfa; color: #1857b6; display: grid; place-items: center; flex-shrink: 0; }
.float-ic svg { width: 17px; height: 17px; fill: none; stroke: currentColor; stroke-width: 1.9; }
.float-ic-green { background: #dcfce7; border-color: #bbf7d0; color: #15803d; }

/* 3. Floating Glass Booking Dock */
.booking-dock-wrap { margin-top: -34px; position: relative; z-index: 12; }
.booking-dock {
  background: #ffffff;
  border: 1px solid #d0e3f5;
  border-radius: 20px;
  padding: 20px 24px;
  box-shadow: 0 18px 44px rgba(16, 46, 86, 0.08);
}
.dock-head { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding-bottom: 14px; border-bottom: 1px solid #e6eff6; margin-bottom: 14px; flex-wrap: wrap; }
.dock-title-group { display: flex; align-items: center; gap: 12px; }
.dock-icon { width: 40px; height: 40px; border-radius: 11px; background: #eff8ff; border: 1px solid #cde2f5; color: #1857b6; display: grid; place-items: center; flex-shrink: 0; }
.dock-icon svg { width: 19px; height: 19px; fill: none; stroke: currentColor; stroke-width: 1.9; }
.dock-title-group h2 { margin: 0 0 2px; font-size: 1.02rem; color: #102e56; }
.dock-title-group p { margin: 0; font-size: .82rem; color: #526c80; }
.status-dot { background: #dcfce7; color: #15803d; padding: 4px 11px; border-radius: 999px; font-weight: 700; font-size: .75rem; }
.dock-pillars { display: grid; grid-template-columns: 1fr 1fr 1fr auto; gap: 12px; align-items: center; }
.dock-pillar { background: #f8fbff; border: 1px solid #e4eef6; border-radius: 12px; padding: 10px 13px; display: flex; flex-direction: column; gap: 2px; }
.dock-pillar strong { font-size: .82rem; color: #102e56; }
.dock-pillar span { font-size: .75rem; color: #526c80; }

/* Common Section Blocks */
.section-block { padding: 64px 0; scroll-margin-top: 74px; }
.section-white { background: #ffffff; border-top: 1px solid #e4eef6; border-bottom: 1px solid #e4eef6; }
.section-sky { background: linear-gradient(180deg, #f1f8ff 0%, #f8fbff 100%); }
.section-heading { text-align: center; max-width: 720px; margin: 0 auto 30px; }
.section-heading h2 { margin: 8px 0 10px; color: #102e56; font-size: clamp(1.65rem, 2.7vw, 2.2rem); letter-spacing: -.025em; }
.section-heading > p:last-child { margin: 0; color: #526c80; line-height: 1.6; }

.section-split-head { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; margin-bottom: 26px; flex-wrap: wrap; }
.section-split-head h2 { margin: 6px 0 8px; color: #102e56; font-size: clamp(1.65rem, 2.7vw, 2.2rem); letter-spacing: -.025em; }
.section-split-head p:last-child { margin: 0; color: #526c80; max-width: 650px; line-height: 1.6; font-size: .92rem; }

/* 4. Asymmetric 12-Column Bento Grid (Tông Sáng Hài Hòa) */
.bento-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 18px; }
.bento-7 { grid-column: span 7; }
.bento-5 { grid-column: span 5; }
.bento-4 { grid-column: span 4; }
.bento-8 { grid-column: span 8; }
.bento-12 { grid-column: span 12; }

.bento-card {
  background: #ffffff;
  border: 1px solid #e2edf7;
  border-radius: 18px;
  padding: 24px;
  box-shadow: 0 6px 22px rgba(16, 46, 86, 0.04);
  display: flex;
  flex-direction: column;
  gap: 12px;
  transition: transform .18s ease, box-shadow .18s ease;
}
.bento-card:hover { transform: translateY(-2px); box-shadow: 0 14px 32px rgba(16, 46, 86, 0.08); }

/* Ô Flagship 7 Cột: Tông Lam Ngọc & Vàng Hoàng Gia sáng thanh lịch, không gây chói hay tối nặng */
.bento-flagship {
  background: linear-gradient(145deg, #eef7ff 0%, #ffffff 65%, #fdfaf2 100%);
  border: 1.5px solid #bfd8f2;
}
.bento-flagship h3 { font-size: 1.3rem; color: #102e56; margin: 0; }
.flagship-subgrid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 4px 0; }
.flagship-subitem { background: #ffffff; border: 1px solid #d8ecfa; border-radius: 12px; padding: 12px 14px; display: flex; flex-direction: column; gap: 4px; }
.flagship-subitem strong { color: #1857b6; font-size: .88rem; }
.flagship-subitem span { color: #526c80; font-size: .78rem; line-height: 1.45; }

.bento-sky { background: linear-gradient(155deg, #f4faff 0%, #ffffff 100%); border-color: #cde2f5; }
.bento-sky-solid { background: #f2f8ff; border-color: #cde2f5; }
.bento-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.bento-duration { color: #5b6b80; font-size: .76rem; }
.bento-card h3 { margin: 2px 0 0; font-size: 1.08rem; color: #102e56; line-height: 1.35; }
.bento-desc { margin: 0; font-size: .86rem; color: #4a6479; line-height: 1.6; }
.bento-bullets { margin: 0; padding-left: 18px; color: #526c80; font-size: .82rem; line-height: 1.6; flex: 1; display: flex; flex-direction: column; gap: 5px; }

.bento-foot { margin-top: auto; padding-top: 12px; border-top: 1px solid #edf3f8; display: flex; align-items: center; justify-content: space-between; gap: 10px; font-size: .8rem; flex-wrap: wrap; }
.bento-highlight { color: #102e56; font-weight: 700; }

.specialty-tag { background: #e8f4fd; color: #1857b6; font-size: .74rem; font-weight: 700; padding: 4px 10px; border-radius: 999px; }
.specialty-time { color: #5b6b80; font-size: .75rem; }
.specialty-note { color: #5b6b80; font-weight: 600; }
.specialty-cta { color: #1857b6; font-weight: 750; text-decoration: none; }
.specialty-cta:hover { text-decoration: underline; }

.bento-strip { background: linear-gradient(90deg, #ffffff 0%, #f1f8ff 100%); padding: 20px 24px; }
.strip-main { display: flex; align-items: center; justify-content: space-between; gap: 24px; flex-wrap: wrap; }
.strip-main > div { max-width: 780px; }
.strip-main h3 { margin: 8px 0 6px; font-size: 1.05rem; color: #102e56; }
.strip-main p { margin: 0; font-size: .84rem; color: #4a6479; line-height: 1.55; }

/* 5. SƠ ĐỒ RĂNG GIẢI PHẪU FDI MẪU TƯƠNG TÁC (Interactive Anatomical Sample Chart) */
.dentition-switch {
  display: inline-flex;
  background: #e6f1fb;
  padding: 4px;
  border-radius: 12px;
  border: 1px solid #cde2f5;
  gap: 4px;
}
.dent-btn {
  border: 0;
  background: transparent;
  color: #4a6479;
  font: inherit;
  font-size: .8rem;
  font-weight: 750;
  padding: 8px 14px;
  border-radius: 9px;
  cursor: pointer;
  transition: background-color .15s, color .15s;
}
.dent-btn.active { background: #1857b6; color: #ffffff; box-shadow: 0 4px 12px rgba(24,87,182,.2); }

.fdi-showcase-grid { display: grid; grid-template-columns: 1.32fr .68fr; gap: 20px; align-items: stretch; }
.fdi-sample-card, .safety-protocol-card {
  background: #ffffff;
  border: 1px solid #d5e6f7;
  border-radius: 20px;
  padding: 22px;
  box-shadow: 0 10px 30px rgba(16, 46, 86, 0.05);
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.fdi-sample-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.fdi-sample-head strong { display: block; color: #102e56; font-size: .98rem; }
.fdi-sample-head span { font-size: .8rem; color: #526c80; }

.fdi-chart-box {
  background: #f8fbff;
  border: 1px solid #e2edf7;
  border-radius: 16px;
  padding: 14px 12px;
  overflow-x: auto;
}
.fdi-arch-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: .72rem;
  font-weight: 800;
  color: #526c80;
  letter-spacing: .04em;
  margin-bottom: 8px;
}
.fdi-arch-bottom { margin-top: 8px; margin-bottom: 0; }
.fdi-side-hint { font-weight: 600; color: #64748b; }

.fdi-jaw-row {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  min-width: 660px;
}
.fdi-quadrant { display: flex; gap: 3px; }
.fdi-midline-v { width: 2px; height: 86px; background: #cbd5e1; border-radius: 2px; margin: 0 2px; }
.fdi-occlusal-line {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 8px 0;
  min-width: 660px;
}
.fdi-occlusal-line span { flex: 1; border-top: 1px dashed #cbd5e1; }
.fdi-occlusal-line em { font-style: normal; font-size: .7rem; color: #64748b; font-weight: 700; }

.sample-tooth-btn {
  width: 38px;
  padding: 4px 2px;
  border: 1.5px solid #dce7f3;
  border-radius: 10px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  cursor: pointer;
  transition: transform .14s ease, border-color .14s ease, background-color .14s ease;
}
.sample-tooth-btn:hover { border-color: #397bcc; transform: translateY(-2px); background: #f4faff; }
.sample-tooth-btn.selected {
  border-color: #1857b6;
  background: #e8f4fd;
  box-shadow: 0 0 0 2px rgba(24, 87, 182, 0.18);
}
.t-num { font-size: .68rem; font-weight: 800; color: #102e56; line-height: 1; }
.t-anat-svg { width: 30px; height: 44px; display: block; }
.t-surf-svg { width: 22px; height: 22px; display: block; }

/* Màu giải phẫu SVG của chiếc răng */
.svg-root { fill: #f6e7cb; stroke: #948368; stroke-width: 1.3; stroke-linejoin: round; }
.svg-gum { fill: none; stroke: #f4a5ae; stroke-width: 2.2; stroke-linecap: round; opacity: .75; }
.svg-crown { fill: #ffffff; stroke: #64748b; stroke-width: 1.4; stroke-linejoin: round; }
.svg-groove { fill: none; stroke: #cbd5e1; stroke-width: 1.1; stroke-linecap: round; }
.svg-caries { fill: #ef4444; stroke: #991b1b; stroke-width: 1; }
.svg-filling { fill: #3b82f6; stroke: #1d4ed8; stroke-width: 1; }
.svg-rct { fill: none; stroke: #8b5cf6; stroke-width: 2; stroke-linecap: round; }

.st-crn .svg-crown { fill: #f7d98c; stroke: #b7831e; }
.st-imp .svg-root { fill: #bae6fd; stroke: #0284c7; }

.surf-poly { fill: #ffffff; stroke: #94a3b8; stroke-width: 1.1; }
.surf-car { fill: #ef4444; }
.surf-fil { fill: #3b82f6; }
.surf-rct { fill: #8b5cf6; }
.surf-crn { fill: #e5b95e; }
.surf-imp { fill: #0ea5e9; }

.fdi-legend-row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  font-size: .76rem;
  color: #4a6479;
  padding: 4px 2px;
}
.leg-item { display: inline-flex; align-items: center; gap: 6px; font-weight: 600; }
.leg-dot { width: 11px; height: 11px; border-radius: 3px; display: inline-block; border: 1px solid rgba(0,0,0,.12); }
.dot-normal { background: #ffffff; }
.dot-caries { background: #ef4444; }
.dot-filling { background: #3b82f6; }
.dot-rct { background: #8b5cf6; }
.dot-crown { background: #e5b95e; }
.dot-implant { background: #0ea5e9; }

.selected-tooth-inspector {
  background: #eff8ff;
  border: 1px solid #cde2f5;
  border-radius: 14px;
  padding: 14px 16px;
  display: flex;
  align-items: center;
  gap: 14px;
}
.inspector-badge {
  background: #102e56;
  color: #e5b95e;
  font-weight: 800;
  font-size: .88rem;
  padding: 10px 14px;
  border-radius: 11px;
  white-space: nowrap;
}
.inspector-body { flex: 1; min-width: 0; }
.inspector-title-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; margin-bottom: 4px; }
.inspector-title-row h3 { margin: 0; font-size: .96rem; color: #102e56; }
.inspector-body p { margin: 0; font-size: .82rem; color: #4a6479; line-height: 1.5; }

/* Cột Phải: An toàn 3 Lớp (Tông Sáng Y khoa Đồng nhất) */
.safety-card-head h3 { margin: 8px 0 4px; font-size: 1.12rem; color: #102e56; }
.safety-card-head p { margin: 0; font-size: .83rem; color: #526c80; line-height: 1.55; }
.safety-alert-preview {
  background: #fff5f5;
  border: 1px solid #fecaca;
  border-left: 4px solid #dc2626;
  border-radius: 12px;
  padding: 12px 14px;
  color: #7f1d1d;
}
.safety-alert-title { font-weight: 800; font-size: .86rem; margin-bottom: 4px; color: #b91c1c; }
.safety-alert-preview p { margin: 0; font-size: .8rem; line-height: 1.5; }
.safety-steps { display: flex; flex-direction: column; gap: 12px; flex: 1; }
.safety-step-item { display: flex; align-items: flex-start; gap: 12px; background: #f8fbff; border: 1px solid #e4eef6; border-radius: 12px; padding: 11px 13px; }
.safety-step-item h4 { margin: 0 0 3px; color: #102e56; font-size: .88rem; }
.safety-step-item p { margin: 0; color: #526c80; font-size: .79rem; line-height: 1.5; }
.safety-card-foot { padding-top: 8px; border-top: 1px solid #edf3f8; display: flex; justify-content: flex-end; }

/* 6. Editorial Split Pricing & Price Menu */
.split-h2 { margin: 6px 0 8px; color: #102e56; font-size: clamp(1.5rem, 2.3vw, 1.95rem); line-height: 1.22; letter-spacing: -.02em; }
.billing-timeline { display: flex; flex-direction: column; gap: 14px; margin-top: 6px; padding-left: 16px; border-left: 2px solid #93c5fd; }
.billing-tl-item { display: flex; flex-direction: column; gap: 3px; }
.billing-tl-item strong { color: #102e56; font-size: .9rem; }
.billing-tl-item span { color: #4a6479; font-size: .82rem; line-height: 1.55; }

.price-menu-card { justify-content: space-between; }
.price-menu-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding-bottom: 12px; border-bottom: 1px solid #e4eef6; }
.price-menu-head h3 { margin: 0 0 4px; font-size: 1.12rem; color: #102e56; }
.price-menu-head p { margin: 0; font-size: .8rem; color: #526c80; }
.price-list { display: flex; flex-direction: column; }
.price-row { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; padding: 11px 0; border-bottom: 1px dashed #d8e6f3; }
.price-row:last-child { border-bottom: 0; }
.price-name { font-weight: 700; color: #102e56; font-size: .9rem; }
.price-meta { font-size: .76rem; color: #5b6b80; margin-top: 2px; }
.price-dots { flex: 1; border-bottom: 1px dotted #cbd5e1; margin: 0 6px; min-width: 16px; }
.price-right { text-align: right; flex-shrink: 0; }
.price-val { font-weight: 800; color: #1857b6; font-size: .92rem; font-variant-numeric: tabular-nums; }
.price-mode { display: inline-block; margin-top: 2px; font-size: .69rem; font-weight: 700; padding: 2px 8px; border-radius: 999px; }
.mode-gold { background: #fbf1dc; color: #8a6414; }
.mode-green { background: #dcfce7; color: #15803d; }

/* 7. Guide 5 Steps */
.guide { max-width: 1200px; margin: auto; padding: 64px 24px; box-sizing: border-box; scroll-margin-top: 74px; }
.steps { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 16px; margin: 28px 0 24px; }
.step { padding: 20px; border: 1px solid #e4eef6; border-radius: 18px; background: #fff; box-shadow: 0 8px 24px #1857b608; display: flex; flex-direction: column; gap: 8px; }
.step-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.step-number { display: grid; width: 38px; height: 38px; place-items: center; border-radius: 10px; background: #1857b6; color: #fff; font-weight: 800; font-size: .86rem; flex-shrink: 0; }
.step-tag { font-size: .71rem; font-weight: 700; color: #1857b6; background: #e8f4fd; padding: 3px 9px; border-radius: 999px; }
.step h3 { margin: 4px 0 2px; color: #102e56; font-size: .96rem; }
.step p { margin: 0; color: #526c80; font-size: .83rem; line-height: 1.58; }
.guide-link { display: table; margin: auto; padding: 11px 20px; color: #1857b6; font-weight: 750; text-decoration: none; background: #e8f4fd; border-radius: 999px; }

/* 8. Editorial Split FAQ */
.faq-side { display: flex; flex-direction: column; gap: 14px; }
.faq-help-box { background: #eff8ff; border: 1px solid #cde2f5; border-radius: 16px; padding: 18px; display: flex; flex-direction: column; align-items: flex-start; gap: 8px; margin-top: 4px; }
.faq-help-box strong { color: #102e56; font-size: .94rem; }
.faq-help-box p { margin: 0; color: #4a6479; font-size: .82rem; line-height: 1.55; }
.faq-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
.faq-card { background: #f8fbff; border: 1px solid #e4eef6; border-radius: 16px; padding: 18px 20px; }
.faq-card h3 { margin: 0 0 8px; font-size: .94rem; color: #102e56; line-height: 1.4; }
.faq-card p { margin: 0; font-size: .83rem; color: #526c80; line-height: 1.58; }

/* 9. CTA Banner */
.cta-banner { padding: 24px 0 52px; }
.cta-box { background: linear-gradient(135deg, #102e56 0%, #1857b6 100%); color: #fff; border-radius: 22px; padding: 34px 32px; display: flex; align-items: center; justify-content: space-between; gap: 24px; flex-wrap: wrap; box-shadow: 0 18px 44px rgba(16,46,86,.16); }
.cta-copy { display: flex; align-items: center; gap: 18px; max-width: 700px; }
.cta-emblem { flex-shrink: 0; }
.cta-eyebrow { margin: 0 0 4px; color: #e5b95e; font-size: .75rem; font-weight: 800; text-transform: uppercase; letter-spacing: .08em; }
.cta-copy h2 { margin: 0 0 6px; color: #fff; font-size: clamp(1.3rem, 2.2vw, 1.75rem); }
.cta-copy p { margin: 0; color: #dce7f5; font-size: .86rem; line-height: 1.55; }
.cta-buttons { display: flex; flex-wrap: wrap; gap: 10px; }

/* 10. Footer */
.footer { background: #102e56; color: #c9d8ea; padding: 44px 24px 24px; }
.footer-grid { max-width: 1200px; margin: 0 auto 28px; display: grid; grid-template-columns: 1.4fr 1fr 1fr 1fr; gap: 24px; }
.footer-col { display: flex; flex-direction: column; gap: 8px; font-size: .85rem; }
.footer-logo { height: 42px; width: auto; margin-bottom: 4px; }
.footer-desc { margin: 0; color: #b8cbe1; line-height: 1.6; font-size: .83rem; }
.footer-col h4 { margin: 0 0 6px; color: #fff; font-size: .92rem; }
.footer-col a { color: #c9d8ea; text-decoration: none; }
.footer-col a:hover { color: #e5b95e; text-decoration: underline; }
.footer-bottom { max-width: 1200px; margin: 0 auto; padding-top: 18px; border-top: 1px solid rgba(255,255,255,.12); display: flex; justify-content: space-between; gap: 16px; font-size: .82rem; color: #9ab0ca; flex-wrap: wrap; }
.footer-bottom a { color: #e5b95e; font-weight: 700; text-decoration: none; }

a:focus-visible, button:focus-visible { outline: 3px solid #c3881d; outline-offset: 3px; }

@media (max-width: 1080px) {
  .bento-7, .bento-5, .bento-4, .bento-8 { grid-column: span 12; }
  .dock-pillars { grid-template-columns: 1fr 1fr; }
  .steps { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .fdi-showcase-grid, .footer-grid { grid-template-columns: 1fr; }
}

@media (max-width: 820px) {
  .topbar-inner { min-height: 62px; padding: 8px 14px; flex-wrap: wrap; gap: 8px; }
  .brand img { width: 132px; }
  .nav-actions { order: 2; }
  .nav-center {
    order: 3;
    width: 100%;
    justify-content: flex-start;
    overflow-x: auto;
    padding-top: 4px;
    scrollbar-width: none;
  }
  .nav-center::-webkit-scrollbar { display: none; }
  .nav-link { font-size: .8rem; padding: 6px 10px; background: #f1f5f9; }
  .login-link { padding: 7px 12px; font-size: .8rem; }
  .nav-book { min-height: 36px; padding: 0 12px; font-size: .8rem; }
  .hero-inner { grid-template-columns: 1fr; gap: 18px; padding: 28px 16px 52px; }
  .hero-metrics-strip { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .hero-float-badge { position: static; margin-top: 8px; }
  .flagship-subgrid, .dock-pillars, .steps, .faq-grid, .footer-grid { grid-template-columns: 1fr; }
  .selected-tooth-inspector { flex-direction: column; align-items: flex-start; }
  .cta-box { padding: 24px 20px; }
  .cta-copy { flex-direction: column; align-items: flex-start; }
}
</style>
