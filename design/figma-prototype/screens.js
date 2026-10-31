// Màn hình: Công khai, Xác thực, Cổng bệnh nhân.
(function () {
  const { D, vnd, badge, fn, status, table, card, stat, field, input, select, textarea, pageHead, fdiChart, fdiLegend, publicShell, patientShell, ic } = window.UI;
  const S = (window.SCREENS = window.SCREENS || []);
  const add = (id, group, title, codes, view, render) => S.push({ id, group, title, codes, view, render });

  // ================= 02. PUBLIC =================
  add('home', '02. Công khai', 'Trang chủ phòng khám', ['F_APP_01', 'F_MST_01'], 'PublicHomeView.vue', () => publicShell(`
    <section class="hero"><img src="assets/banner-website-hero.svg" alt="Nụ cười Hoàng Gia tỏa sáng mỗi ngày"></section>
    <section class="section"><div class="container">
      <div class="grid g4">
        ${[['tooth', '15+ năm', 'Kinh nghiệm điều trị'], ['doctors', '8 nha sĩ', 'Chuyên khoa'], ['chair', '6 ghế', 'Trang thiết bị hiện đại'], ['smile', '20.000+', 'Khách hàng tin tưởng']].map(([i, v, l]) => `<div class="card stat"><div class="ic">${ic(i, 22)}</div><div><div class="val">${v}</div><div class="lbl">${l}</div></div></div>`).join('')}
      </div>
    </div></section>
    <section class="section" id="sec-services" style="padding-top:0"><div class="container">
      <div class="section-title"><div class="eyebrow">${ic('tooth', 14)} Dịch vụ chuyên khoa</div><h2>Chăm sóc răng miệng toàn diện</h2></div>
      <div class="grid g4">
        ${[['search', 'Khám & tư vấn', 'Khám tổng quát, chụp phim, lập kế hoạch điều trị rõ ràng.'], ['sparkle', 'Lấy cao & tẩy trắng', 'Làm sạch mảng bám, mang lại hàm răng sáng khỏe.'], ['tools', 'Trám & điều trị tủy', 'Phục hồi răng sâu, bảo tồn răng thật tối đa.'], ['crown', 'Răng sứ & Cấy ghép trụ răng', 'Phục hình thẩm mỹ, ăn nhai chắc chắn.'], ['smile', 'Chỉnh nha', 'Niềng răng mắc cài và khay trong suốt.'], ['baby', 'Nha khoa trẻ em', 'Chăm sóc răng sữa nhẹ nhàng, thân thiện.'], ['bandage', 'Tiểu phẫu', 'Nhổ răng khôn an toàn, ít đau.'], ['xray', 'X-quang kỹ thuật số', 'Chẩn đoán chính xác, giảm liều tia.']].map(([i, t, d]) => `<div class="card svc-card"><div class="ic">${ic(i, 22)}</div><h3>${t}</h3><p class="muted small">${d}</p></div>`).join('')}
      </div>
    </div></section>
    <section class="section" id="sec-pricing" style="background:#fff"><div class="container">
      <div class="section-title"><div class="eyebrow">${ic('tag', 14)} Bảng giá tham khảo</div><h2>Minh bạch chi phí</h2></div>
      ${card('', table(['Dịch vụ', 'Nhóm', 'Đơn vị', { t: 'Giá từ', r: 1 }], D.services.map((s) => [s.name, badge(s.group, 'blue'), s.unit, `<span class="money">${vnd(s.price)}</span>`])), '', false)}
      <p class="small muted" style="margin-top:10px">Giá thực tế được Nha sĩ tư vấn sau khi thăm khám. Hỗ trợ thanh toán nhiều đợt theo tiến độ điều trị.</p>
    </div></section>
    <section class="section" id="sec-doctors" style="padding-top:24px"><div class="container">
      <div class="section-title"><div class="eyebrow">${ic('doctors', 14)} Đội ngũ chuyên môn</div><h2>Bác sĩ tận tâm — Giàu kinh nghiệm</h2></div>
      <div class="grid g3">
        ${[['doctors', 'BS. CKI Nguyễn Hoàng Long', 'Trưởng phòng khám · Nội nha & Phục hình', '15 năm kinh nghiệm điều trị tủy, phục hình mão sứ và cấy ghép trụ răng.'], ['doctors', 'BS. Lê Thu Trang', 'Chuyên khoa Chỉnh nha & Nha khoa trẻ em', '10 năm kinh nghiệm niềng răng mắc cài, khay trong suốt và chăm sóc răng sữa.'], ['doctors', 'BS. Vũ Quốc Khánh', 'Chuyên khoa Tiểu phẫu & Nha chu', '8 năm kinh nghiệm nhổ răng khôn an toàn, điều trị nha chu và thẩm mỹ nụ cười.']].map(([i, n, r, desc]) => `<div class="card svc-card"><div class="row"><div class="ic">${ic(i, 20)}</div><div><h3>${n}</h3><div class="small" style="color:var(--primary-600);font-weight:700">${r}</div></div></div><p class="muted small">${desc}</p></div>`).join('')}
      </div>
    </div></section>
    <section class="section" style="padding-top:0"><div class="container">
      <div class="card card-pad" style="background:linear-gradient(120deg,var(--primary-600),var(--navy));color:#fff;display:flex;justify-content:space-between;align-items:center;gap:20px;flex-wrap:wrap">
        <div><h2 style="color:#fff">Đặt lịch khám chỉ trong 1 phút</h2><p style="opacity:.85;margin-top:6px">Không cần tạo tài khoản. Lễ tân sẽ gọi xác nhận lịch hẹn cho bạn.</p></div>
        <a class="btn btn-gold btn-lg" href="#/booking">${ic('calendar', 16)} <span>Đặt lịch ngay →</span></a>
      </div>
    </div></section>`, 'home'));

  add('booking', '02. Công khai', 'Đặt lịch trực tuyến (khách)', ['F_APP_01', 'F_AUTH_01'], 'GuestBookingView.vue', () => publicShell(`
    <section class="section"><div class="container" style="max-width:980px">
      <div class="grid g-side">
        <div class="card"><div class="card-head"><h2>Đặt lịch khám</h2>${fn('F_APP_01')}</div><div class="card-body stack">
          <div class="steps"><span class="on"></span><span class="on"></span><span></span></div>
          <div class="form-grid">
            ${field('Họ và tên', input('Nguyễn Văn A', 'Võ Thị Lan'), { req: 1 })}
            ${field('Số điện thoại', input('09xx xxx xxx', '0909 123 312', 'tel'), { req: 1, hint: 'Lễ tân gọi xác nhận qua số này' })}
            ${field('Năm sinh', input('1990', '1990'))}
            ${field('Giới tính', select(['Nữ', 'Nam', 'Khác']))}
            ${field('Nhu cầu khám', select(['Khám & tư vấn', 'Đau răng', 'Lấy cao răng', 'Niềng răng', 'Răng sứ / Cấy ghép trụ răng', 'Khác']), { full: 1 })}
          </div>
          <div class="field"><label>Chọn ngày khám <span style="color:var(--red)">*</span></label>
            <div class="row">${['T7 01/11', 'CN 02/11', 'T2 03/11', 'T3 04/11', 'T4 05/11'].map((d, i) => `<button class="btn btn-sm ${i === 0 ? 'btn-primary' : 'btn-outline'}">${d}</button>`).join('')}</div>
          </div>
          <div class="field"><label>Khung giờ còn trống <span style="color:var(--red)">*</span></label>
            <div class="row">${['07:30', '08:00', '08:30', '09:00', '09:30', '10:00', '10:30', '14:00', '14:30', '15:00'].map((t, i) => `<button class="btn btn-sm ${i === 3 ? 'btn-primary' : 'btn-outline'}" ${i === 1 || i === 6 ? 'disabled' : ''}>${t}</button>`).join('')}</div>
            <div class="hint">Khung mờ đã đủ công suất. Nhận yêu cầu đến 23:30 ngày hôm trước.</div>
          </div>
          ${field('Ghi chú cho phòng khám', textarea('Mô tả triệu chứng (không bắt buộc)', 'Đau nhức răng hàm dưới bên phải 3 ngày nay.'))}
          <label class="checkbox"><input type="checkbox" checked> Tôi đồng ý để phòng khám liên hệ xác nhận lịch hẹn.</label>
          <button class="btn btn-primary btn-lg btn-block">Gửi yêu cầu đặt lịch</button>
        </div></div>
        <div class="stack">
          <div class="alert alert-blue"><div class="ai">ℹ️</div><div class="small"><b>Không cần đăng nhập.</b> Bạn không cần chọn nha sĩ — phòng khám sẽ sắp xếp. Khi đến, vui lòng mang <b>CCCD hoặc hộ chiếu</b> để Lễ tân đối chiếu và tạo tài khoản.</div></div>
          ${card('Tóm tắt lịch hẹn', `<div class="stack small"><div class="row between"><span class="muted">Ngày</span><b>Thứ 7, 01/11/2026</b></div><div class="row between"><span class="muted">Giờ</span><b>09:00</b></div><div class="row between"><span class="muted">Nhu cầu</span><b>Đau răng</b></div><div class="row between"><span class="muted">Địa điểm</span><b>[ĐIỀN ĐỊA CHỈ]</b></div></div>`)}
          <div class="alert alert-green"><div class="ai">✅</div><div class="small"><b>Trạng thái sau khi gửi:</b> “Đã gửi yêu cầu — mã YC-26110014. Lễ tân sẽ gọi xác nhận trong giờ làm việc.”</div></div>
        </div>
      </div>
    </div></section>`, 'booking'));

  add('rx-public', '02. Công khai', 'Tra cứu đơn thuốc công khai (QR)', ['F_RX_04'], 'PublicPrescriptionView.vue', () => publicShell(`
    <section class="section"><div class="container" style="max-width:760px">
      <div class="card"><div class="card-head"><h2>Tra cứu đơn thuốc</h2>${fn('F_RX_04')}</div><div class="card-body stack">
        <div class="row"><div style="flex:1">${input('Nhập mã đơn thuốc in trên phiếu', 'RX-2610-8F3K2')}</div><button class="btn btn-primary" onclick="APP.toast(\'Đã tra cứu đơn thuốc RX-2610-8F3K2 hợp lệ\')">${ic('search', 14)} <span>Tra cứu</span></button><button class="btn btn-outline">${ic('camera', 14)} <span>Quét QR</span></button></div>
        <div class="divider"></div>
        <div class="row between"><div><div class="strong">Đơn thuốc RX-2610-8F3K2</div><div class="small muted">Ngày kê 30/10/2026 · BS. Nguyễn Hoàng Long</div></div>${badge('Còn hiệu lực', 'green')}</div>
        <div class="small muted">Bệnh nhân: V*** T*** L*** · Chẩn đoán và thông tin cá nhân được ẩn trên trang công khai.</div>
        ${table(['Thuốc', 'Số lượng', 'Cách dùng'], [['Amoxicillin 500mg', '15 viên', 'Uống 1 viên × 3 lần/ngày sau ăn, 5 ngày'], ['Paracetamol 500mg', '10 viên', 'Uống 1 viên khi đau, cách nhau ≥ 4 giờ'], ['Chlorhexidine 0.12%', '1 chai', 'Súc miệng 15ml × 2 lần/ngày']])}
        <div class="alert alert-amber"><div class="ai">💡</div><div class="small">Ngừng thuốc và liên hệ phòng khám nếu có dấu hiệu dị ứng (phát ban, khó thở).</div></div>
      </div></div>
    </div></section>`, 'rx-public'));

  add('tv', '02. Công khai', 'Màn hình TV phòng chờ (1920×1080)', ['F_CHK_02'], 'QueueDisplayView.vue', () => `
    <div class="tv">
      <div class="tv-head"><img src="assets/logo-ngang-nen-toi.svg" alt=""><div class="right"><div class="tv-clock">09:12</div><div style="opacity:.7">Thứ Sáu, 31/10/2026</div></div></div>
      <div class="tv-grid">
        <div class="tv-call">
          <div style="font-size:26px;opacity:.85">Mời khách hàng số</div>
          <div class="big">A010</div>
          <div style="font-size:40px;font-weight:800">Đặng M*** Linh</div>
          <div style="font-size:30px">vào <b style="color:var(--gold)">Phòng 02 · Ghế 3</b></div>
        </div>
        <div class="tv-list">
          <div style="font-size:26px;font-weight:800;margin-bottom:10px">Danh sách chờ</div>
          ${D.queue.filter((q) => q.status !== 'done').map((q) => `<div class="tv-row"><b style="color:var(--gold)">${q.no}</b><span>${q.name}</span><span style="opacity:.8">${{ waiting: 'Đang chờ', called: 'Mời vào', 'in-exam': 'Đang khám', xray: 'X-quang' }[q.status]}</span></div>`).join('')}
        </div>
      </div>
      <div style="opacity:.7;font-size:18px">Tên khách hàng được che để bảo mật. Vui lòng chú ý số thứ tự của bạn. ${'' /* [CẦN XÁC NHẬN] §3.1 TV công khai */}</div>
    </div>`);

  add('not-found', '02. Công khai', 'Trang 404', [], 'NotFoundView.vue', () => publicShell(`
    <section class="section"><div class="container" style="text-align:center;max-width:520px">
      <img src="assets/logo-icon.svg" alt="" style="height:90px;margin:0 auto 16px">
      <h1 style="font-size:48px">404</h1><p class="muted" style="margin:8px 0 20px">Không tìm thấy trang bạn yêu cầu hoặc bạn không có quyền truy cập.</p>
      <a class="btn btn-primary" href="#/home">Về trang chủ</a>
    </div></section>`));

  // ================= 03. AUTH =================
  const authLayout = (inner) => `<div class="auth">
    <div class="auth-art"><img src="assets/logo-ngang-nen-toi.svg" alt="">
      <div><h2>Nụ cười Hoàng Gia<br><span style="color:var(--gold)">tỏa sáng mỗi ngày</span></h2><p style="opacity:.8;margin-top:12px;max-width:420px">Hệ thống quản lý phòng khám: đặt lịch, tiếp đón, khám với sơ đồ răng FDI, kê đơn và thanh toán minh bạch.</p></div>
      <div class="small" style="opacity:.6">© 2026 Phòng khám Nha khoa Hoàng Gia</div></div>
    <div class="auth-form"><div class="auth-card">${inner}</div></div></div>`;

  add('login', '03. Xác thực', 'Đăng nhập', ['F_AUTH_02'], 'LoginView.vue', () => authLayout(`
    <img src="assets/logo-icon.svg" alt="" style="height:52px">
    <div><h1>Đăng nhập</h1><p class="muted">Chào mừng trở lại Nha khoa Hoàng Gia</p></div>
    <div class="seg"><button class="on">Bệnh nhân</button><button>Nhân viên</button></div>
    ${field('Loại giấy tờ', select(['CCCD', 'Hộ chiếu']))}
    ${field('Số CCCD', input('12 chữ số', '079190004521'), { req: 1 })}
    ${field('Mật khẩu', input('••••••••', '', 'password'), { req: 1 })}
    <div class="row between"><label class="checkbox small"><input type="checkbox"> Ghi nhớ đăng nhập</label><a class="small" href="#/forgot">Quên mật khẩu?</a></div>
    <a class="btn btn-primary btn-lg btn-block" href="#/p-dashboard">Đăng nhập</a>
    <div class="alert alert-blue"><div class="ai">ℹ️</div><div class="small">Chưa có tài khoản? Tài khoản bệnh nhân được tạo khi bạn đến khám và Lễ tân đối chiếu giấy tờ. <a href="#/booking">Đặt lịch khám</a></div></div>
    <div class="small muted">Nhân viên: chọn tab “Nhân viên” và đăng nhập bằng số điện thoại công việc.</div>`));

  add('activate', '03. Xác thực', 'Kích hoạt tài khoản lần đầu (OTP + đổi mật khẩu)', ['F_AUTH_01', 'F_AUTH_02'], 'PatientActivationView.vue', () => authLayout(`
    <div class="steps"><span class="on"></span><span class="on"></span><span></span></div>
    <div><h1>Xác minh số điện thoại</h1><p class="muted">Mã OTP 6 số đã gửi đến 0909 ••• 312. Lần đăng nhập web đầu tiên bắt buộc xác minh.</p></div>
    <div class="otp">${'482913'.split('').map((d) => `<input value="${d}" maxlength="1">`).join('')}</div>
    <div class="row between small"><span class="muted">Gửi lại mã sau 00:42</span><a href="#">Gửi lại</a></div>
    <div class="divider"></div>
    <h3>Đặt mật khẩu mới</h3>
    ${field('Mật khẩu mới', input('Tối thiểu 8 ký tự', '', 'password'), { req: 1, hint: 'Không được trùng mật khẩu khởi tạo (số điện thoại).' })}
    ${field('Nhập lại mật khẩu', input('', '', 'password'), { req: 1 })}
    <a class="btn btn-primary btn-lg btn-block" href="#/p-dashboard">Xác minh & vào cổng bệnh nhân</a>`));

  add('forgot', '03. Xác thực', 'Khôi phục mật khẩu (OTP)', ['F_AUTH_04'], 'PasswordRecoveryView.vue', () => authLayout(`
    <a class="small" href="#/login">← Quay lại đăng nhập</a>
    <div><h1>Khôi phục mật khẩu</h1><p class="muted">Nhập số điện thoại tài khoản và giấy tờ định danh để nhận OTP.</p></div>
    <div class="steps"><span class="on"></span><span></span><span></span></div>
    ${field('Số điện thoại tài khoản', input('09xx xxx xxx', '0909 123 312'), { req: 1 })}
    ${field('Loại giấy tờ', select(['CCCD', 'Hộ chiếu']))}
    <div class="form-grid">${field('Số giấy tờ', input('', 'P12347781'), { req: 1 })}${field('Quốc gia cấp', select(['KR — Hàn Quốc', 'VN — Việt Nam', 'US — Hoa Kỳ']), { req: 1, hint: 'Bắt buộc với hộ chiếu' })}</div>
    <button class="btn btn-primary btn-lg btn-block">Gửi mã OTP</button>
    <div class="small muted">Vì lý do bảo mật, hệ thống luôn hiển thị cùng một thông báo dù thông tin có khớp hay không.</div>`));

  add('m-profile', '03. Xác thực', 'Hồ sơ cá nhân & đổi mật khẩu (nhân viên)', ['F_AUTH_03'], 'ProfileView.vue', (role) => window.UI.staffShell('m-profile', role || 'RECEPTIONIST', profileBody(role || 'RECEPTIONIST')));
  function profileBody(role) {
    const r = D.roles[role];
    return `${pageHead('Hồ sơ cá nhân', 'Thông tin tài khoản, đổi mật khẩu và số điện thoại', 'Tài khoản', '', ['F_AUTH_03'])}
    <div class="grid g-side-l">
      ${card('', `<div class="stack" style="align-items:center;text-align:center"><div class="avatar" style="width:84px;height:84px;font-size:28px">${r.init}</div><h2>${r.user}</h2>${badge(r.label, 'blue')}<div class="small muted">Đăng nhập gần nhất: 31/10/2026 07:58</div></div>`)}
      <div class="stack">
        ${card('Thông tin tài khoản', `<div class="form-grid">${field('Họ tên', input('', r.user))}${field('Số điện thoại', input('', '0901 ••• 004'), { hint: 'Đổi số cần xác minh OTP số mới' })}${field('Email', input('', 'thuha@hoanggia.vn'))}${field('Vai trò', input('', r.label))}</div>`, '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'phone-otp\')">📱 Đổi số điện thoại (OTP)</button>')}
        ${card('Đổi mật khẩu', `<div class="form-grid">${field('Mật khẩu hiện tại', input('', '', 'password'), { full: 1 })}${field('Mật khẩu mới', input('', '', 'password'))}${field('Nhập lại', input('', '', 'password'))}</div><div class="row" style="margin-top:14px"><button class="btn btn-primary" onclick="APP.toast(\'Đã cập nhật mật khẩu tài khoản thành công\')">Cập nhật mật khẩu</button></div>`)}
      </div>
    </div>`;
  }
  window.UI.profileBody = profileBody;

  // ================= 04. PATIENT PORTAL =================
  const P = D.patients[0];
  add('p-dashboard', '04. Cổng bệnh nhân', 'Tổng quan bệnh nhân', ['F_AUTH_03', 'F_APP_01'], 'DashboardView.vue', () => patientShell('p-dashboard', `
    <div class="row between"><div><h1>Xin chào, ${P.name} 👋</h1><p class="muted">Mã hồ sơ ${P.code}</p></div><a class="btn btn-gold" href="#/p-book">+ Đặt lịch mới</a></div>
    <div class="grid g3">
      <div class="card card-pad stack"><div class="small muted">Lịch hẹn sắp tới</div><h2>Thứ 7, 08/11 · 09:00</h2><div>${status('confirmed')} <span class="small muted">Tái khám điều trị tủy răng 36</span></div><a class="small" href="#/p-appointments">Xem lịch hẹn →</a></div>
      <div class="card card-pad stack"><div class="small muted">Lần khám gần nhất</div><h2>30/10/2026</h2><div class="small">BS. Nguyễn Hoàng Long · Điều trị tủy 36 (lần 1/3)</div><a class="small" href="#/p-record">Xem hồ sơ →</a></div>
      <div class="card card-pad stack" style="border-color:#FCD34D"><div class="small muted">Còn phải thanh toán</div><h2 style="color:var(--amber)">${vnd(20100000)}</h2><div class="progress"><i style="width:37%"></i></div><div class="small muted">Đã thu ${vnd(12000000)} / ${vnd(32100000)}</div><a class="small" href="#/p-invoices">Xem hóa đơn →</a></div>
    </div>
    <div class="grid g-side">
      ${card('Tiến trình điều trị', `<div class="timeline">
        <div class="tl-item"><div class="row between"><b>30/10 · Điều trị tủy răng 36 (lần 1)</b>${badge('Hoàn tất', 'green')}</div><div class="small muted">Mở tủy, làm sạch ống tủy, đặt thuốc.</div></div>
        <div class="tl-item"><div class="row between"><b>08/11 · Điều trị tủy (lần 2)</b>${badge('Đã hẹn', 'blue')}</div><div class="small muted">Trám bít ống tủy.</div></div>
        <div class="tl-item"><div class="row between"><b>Dự kiến · Bọc mão sứ 36</b>${badge('Kế hoạch', 'gray')}</div><div class="small muted">Sau khi hoàn tất điều trị tủy.</div></div></div>`)}
      ${card('Người tôi giám hộ', `<div class="stack"><div class="row between"><div class="row"><div class="avatar">GB</div><div><b>Trần Gia Bảo</b><div class="small muted">9 tuổi · BN-001026</div></div></div><a class="btn btn-outline btn-sm" href="#/p-record?child=1">Xem hồ sơ</a></div><div class="small muted">Bạn có thể xem và đặt lịch cho trẻ được giám hộ.</div></div>`)}
    </div>`));

  add('p-record', '04. Cổng bệnh nhân', 'Hồ sơ sức khỏe & timeline điều trị', ['F_PAT_04', 'F_PAT_07', 'F_FDI_01'], 'MyPatientRecordView.vue / PatientDetailView.vue', () => patientShell('p-record', `
    <div class="row between"><div><h1>Hồ sơ của tôi</h1><p class="muted">${P.name} · ${P.dob} · ${P.gender}</p></div><div class="seg"><button class="on">Võ Thị Lan</button><button>Trần Gia Bảo (giám hộ)</button></div></div>
    <div class="tabs"><a class="on">Tổng quan</a><a href="#/p-visit-fdi">Sơ đồ răng</a><a href="#/p-visit-images">Hình ảnh</a><a href="#/p-visit-rx">Đơn thuốc</a><a href="#/p-invoices">Hóa đơn</a></div>
    <div class="grid g-side">
      <div class="stack">
        ${card('Dòng thời gian điều trị', `<div class="timeline">
          ${[['30/10/2026', 'Điều trị tủy răng 36', 'BS. Nguyễn Hoàng Long', 'Viêm tủy không hồi phục răng 36'], ['12/09/2026', 'Trám răng 26, 11', 'BS. Lê Thu Trang', 'Sâu ngà răng 26, mẻ răng 11'], ['20/06/2026', 'Khám tổng quát + lấy cao răng', 'BS. Nguyễn Hoàng Long', 'Viêm nướu nhẹ']].map(([d, t, b, c]) => `<div class="tl-item card card-pad" style="box-shadow:none"><div class="row between"><b>${t}</b><span class="small muted">${d}</span></div><div class="small muted">${b} · Chẩn đoán: ${c}</div><div class="row" style="margin-top:8px"><a class="btn btn-outline btn-sm" href="#/p-visit-fdi">Sơ đồ răng</a><a class="btn btn-outline btn-sm" href="#/p-visit-images">Ảnh</a><a class="btn btn-outline btn-sm" href="#/p-visit-rx">Đơn thuốc</a><a class="btn btn-outline btn-sm" href="#/p-visit-invoice">Hóa đơn</a></div></div>`).join('')}
        </div>`, fn('F_PAT_07'))}
      </div>
      <div class="stack">
        ${card('Tiền sử & dị ứng', `<div class="stack small"><div class="alert alert-red"><div class="ai">⚠️</div><div><b>Dị ứng Penicillin</b><br>Phản ứng: phát ban, khó thở</div></div><div><b>Bệnh nền:</b> Tăng huyết áp (đang điều trị)</div><div><b>Thuốc đang dùng:</b> Amlodipine 5mg</div></div>`, fn('F_PAT_04'))}
        ${card('Thông tin cá nhân', `<div class="stack small"><div class="row between"><span class="muted">CCCD</span><b>${P.doc}</b></div><div class="row between"><span class="muted">Điện thoại</span><b>${P.phone}</b></div><div class="row between"><span class="muted">Địa chỉ</span><b>Thủ Dầu Một</b></div></div>`)}
      </div>
    </div>`));

  add('p-visit-fdi', '04. Cổng bệnh nhân', 'Xem sơ đồ răng & chỉ định (chỉ đọc)', ['F_FDI_01', 'F_FDI_04', 'F_FDI_06'], 'FdiConditionView.vue / FdiServiceAssignmentView.vue', () => patientShell('p-record', `
    <div><a class="small" href="#/p-record">← Hồ sơ của tôi</a><h1>Sơ đồ răng · Lần khám 30/10/2026</h1></div>
    ${card('Sơ đồ răng FDI (chỉ xem)', fdiChart() + '<div style="margin-top:12px">' + fdiLegend() + '</div>', fn('F_FDI_01'))}
    ${card('Dịch vụ đã chỉ định theo răng', table(['Răng', 'Dịch vụ', 'Trạng thái', { t: 'Đơn giá', r: 1 }], [['36', 'Điều trị tủy răng hàm', status('in-exam'), vnd(1800000)], ['36', 'Mão sứ Zirconia', badge('Kế hoạch', 'gray'), vnd(5500000)], ['16', 'Trám răng Composite', badge('Kế hoạch', 'gray'), vnd(400000)]]), fn('F_FDI_04'), false)}`));

  add('p-visit-images', '04. Cổng bệnh nhân', 'Xem ảnh X-quang của tôi', ['F_IMG_04'], 'ToothImageLookupView.vue', () => patientShell('p-record', `
    <div><a class="small" href="#/p-record">← Hồ sơ của tôi</a><h1>Hình ảnh · Lần khám 30/10/2026</h1></div>
    <div class="grid g-side">
      <div class="viewer"><div class="viewer-tools"><button>＋</button><button>－</button><button>⟲</button><button>⛶</button></div><div style="text-align:center"><div style="font-size:64px">🩻</div>Phim quanh chóp răng 36 · 30/10/2026</div></div>
      <div class="thumbs">${['Panorex 30/10', 'Quanh chóp 36', 'Trong miệng – hàm dưới', 'Quanh chóp 36 (sau)'].map((c) => `<div class="thumb">🩻<div class="cap">${c}</div></div>`).join('')}</div>
    </div>`));

  add('p-visit-rx', '04. Cổng bệnh nhân', 'Đơn thuốc của tôi', ['F_RX_05', 'F_RX_04'], 'PrescriptionView.vue', () => patientShell('p-record', `
    <div class="row between"><div><a class="small" href="#/p-record">← Hồ sơ của tôi</a><h1>Đơn thuốc</h1></div><button class="btn btn-outline">⬇ Tải PDF</button></div>
    ${card('RX-2610-8F3K2 · 30/10/2026', table(['Thuốc', 'Số lượng', 'Cách dùng'], [['Amoxicillin 500mg', '15 viên', '1 viên × 3 lần/ngày, 5 ngày'], ['Paracetamol 500mg', '10 viên', '1 viên khi đau'], ['Chlorhexidine 0.12%', '1 chai', 'Súc miệng 2 lần/ngày']]), badge('Đã giao thuốc', 'green'), false)}
    ${card('Lịch sử đơn thuốc', table(['Mã đơn', 'Ngày', 'Bác sĩ', ''], [['RX-2610-8F3K2', '30/10/2026', 'BS. Hoàng Long', '<a href="#">Xem</a>'], ['RX-2609-1A9D0', '12/09/2026', 'BS. Thu Trang', '<a href="#">Xem</a>']]), fn('F_RX_05'), false)}`));

  add('p-visit-invoice', '04. Cổng bệnh nhân', 'Chi tiết hóa đơn lần khám', ['F_BIL_01', 'F_BIL_03'], 'InvoiceDraftView.vue', () => patientShell('p-invoices', `
    <div><a class="small" href="#/p-invoices">← Hóa đơn</a><h1>Hóa đơn HD-2610-0309</h1></div>
    ${window.UI.invoiceDetail ? window.UI.invoiceDetail(true) : ''}`));

  add('p-appointments', '04. Cổng bệnh nhân', 'Lịch hẹn của tôi', ['F_APP_01'], 'PatientAppointmentHistoryView.vue', () => patientShell('p-appointments', `
    <div class="row between"><h1>Lịch hẹn</h1><a class="btn btn-gold" href="#/p-book">+ Đặt lịch mới</a></div>
    <div class="seg"><button class="on">Sắp tới</button><button>Đã qua</button><button>Đã hủy</button></div>
    ${card('', table(['Ngày giờ', 'Người khám', 'Nhu cầu', 'Trạng thái', ''], [['08/11/2026 · 09:00', 'Võ Thị Lan', 'Tái khám điều trị tủy', status('confirmed'), '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'cancel-appt\')">Hủy lịch</button>'], ['15/11/2026 · 15:00', 'Trần Gia Bảo (giám hộ)', 'Khám định kỳ', status('pending'), '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'cancel-appt\')">Hủy lịch</button>'], ['30/10/2026 · 09:00', 'Võ Thị Lan', 'Đau răng', status('checked-in'), '']]), '', false)}`));

  add('p-book', '04. Cổng bệnh nhân', 'Đặt lịch (đã đăng nhập, cho mình/người giám hộ)', ['F_APP_01'], 'AppointmentsView.vue', () => patientShell('p-appointments', `
    <h1>Đặt lịch khám</h1>
    ${card('', `<div class="stack">
      <div class="field"><label>Đặt cho</label><div class="seg"><button class="on">Tôi — Võ Thị Lan</button><button>Trần Gia Bảo (giám hộ)</button></div></div>
      <div class="form-grid">${field('Ngày khám', input('', '08/11/2026', 'text'), { req: 1 })}${field('Khung giờ', select(['09:00', '09:30', '10:00', '14:00']), { req: 1 })}${field('Nhu cầu', select(['Tái khám', 'Khám mới', 'Lấy cao răng']), { full: 1 })}${field('Ghi chú', textarea('Không bắt buộc'), { full: 1 })}</div>
      <div class="small muted">Phòng khám sẽ sắp xếp nha sĩ phù hợp. Đặt lịch không tự động tiếp đón — vui lòng đến quầy khi tới phòng khám.</div>
      <div class="row"><button class="btn btn-primary" onclick="APP.toast(\'Đã gửi yêu cầu đặt lịch thành công\')">Xác nhận đặt lịch</button><a class="btn btn-ghost" href="#/p-appointments">Hủy</a></div></div>`)}`));

  add('p-invoices', '04. Cổng bệnh nhân', 'Hóa đơn & thanh toán của tôi', ['F_BIL_03', 'F_BIL_06'], 'PatientInvoicesView.vue', () => patientShell('p-invoices', `
    <h1>Hóa đơn của tôi</h1>
    <div class="grid g3">${stat('🧾', '3', 'Hóa đơn')}${stat('✅', vnd(12400000), 'Đã thanh toán')}${stat('⏳', vnd(20100000), 'Còn nợ', 'var(--amber-50)')}</div>
    ${card('', table(['Số hóa đơn', 'Ngày', { t: 'Tổng tiền', r: 1 }, { t: 'Đã thu', r: 1 }, 'Trạng thái', ''], [
      ['HD-2610-0309', '30/10/2026', vnd(32100000), vnd(12000000), status('partial'), '<a href="#/p-visit-invoice">Chi tiết</a>'],
      ['HD-2609-0177', '12/09/2026', vnd(800000), vnd(800000), status('paid'), '<a href="#/m-receipt">Biên lai</a>'],
      ['HD-2606-0042', '20/06/2026', vnd(400000), vnd(400000), status('paid'), '<a href="#/m-receipt">Biên lai</a>']]), '', false)}`));

  add('p-profile', '04. Cổng bệnh nhân', 'Tài khoản bệnh nhân', ['F_AUTH_03'], 'ProfileView.vue', () => patientShell('p-profile', `
    <h1>Tài khoản</h1>
    <div class="grid g2">
      ${card('Thông tin đăng nhập', `<div class="stack small"><div class="row between"><span class="muted">Giấy tờ đăng nhập</span><b>CCCD ${P.doc}</b></div><div class="row between"><span class="muted">Số điện thoại</span><b>${P.phone}</b></div><button class="btn btn-outline btn-sm" style="align-self:flex-start" onclick="APP.modal(\'phone-otp\')">📱 Đổi số điện thoại (OTP)</button></div>`)}
      ${card('Đổi mật khẩu', `<div class="stack">${field('Mật khẩu hiện tại', input('', '', 'password'))}${field('Mật khẩu mới', input('', '', 'password'))}${field('Nhập lại', input('', '', 'password'))}<button class="btn btn-primary" style="align-self:flex-start" onclick="APP.toast(\'Đã cập nhật mật khẩu thành công\')">Cập nhật</button></div>`)}
    </div>
    ${card('Người giám hộ / Người được giám hộ', table(['Họ tên', 'Quan hệ', 'Hiệu lực'], [['Trần Gia Bảo', 'Con (tôi là người giám hộ)', badge('Đang hiệu lực', 'green')]]), '', false)}`));
})();
