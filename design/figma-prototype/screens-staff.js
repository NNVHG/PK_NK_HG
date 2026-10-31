// Màn hình: Khu vực nhân viên (Lễ tân, Nha sĩ, Phụ tá, Admin).
(function () {
  const U = window.UI;
  const { D, vnd, badge, fn, status, table, card, stat, field, input, select, textarea, pageHead, safetyBanner, fdiChart, fdiLegend, surfaces, staffShell, ic } = U;
  const S = window.SCREENS;
  const add = (id, group, title, codes, view, defRole, body) =>
    S.push({ id, group, title, codes, view, defRole, render: (role) => staffShell(id, role || defRole, body(role || defRole)) });
  const P = D.patients[0];
  const act = (...b) => `<div class="row" style="gap:6px;flex-wrap:nowrap">${b.join('')}</div>`;
  const roleLabel = (r) => D.roles[r].label;

  // ---------- Invoice detail shared ----------
  U.invoiceDetail = (readonly) => `
    <div class="grid g-side">
      ${card('Chi tiết dịch vụ', table(['Răng', 'Dịch vụ', 'SL', { t: 'Đơn giá (snapshot)', r: 1 }, { t: 'Thành tiền', r: 1 }], [
        ['—', 'Khám và tư vấn', '1', vnd(100000), vnd(100000)],
        ['36', 'Điều trị tủy răng hàm', '1', vnd(1800000), vnd(1800000)],
        ['—', 'Niềng răng mắc cài kim loại', '1', vnd(32000000), vnd(32000000)],
        ['—', 'Chụp phim toàn cảnh (Panorex)', '1', vnd(150000), vnd(150000)]]), readonly ? '' : fn('F_BIL_01'), false)}
      <div class="stack">
        ${card('Tổng hợp thanh toán', `<div class="stack">
          <div class="row between"><span class="muted">Tạm tính</span><span class="money">${vnd(34050000)}</span></div>
          <div class="row between"><span class="muted">Giảm giá <span class="small">(Khách hàng thân thiết 5%)</span></span><span class="money" style="color:var(--green)">− ${vnd(1950000)}</span></div>
          <div class="divider"></div>
          <div class="row between"><b>Tổng phải thu</b><b class="money" style="font-size:20px;color:var(--navy)">${vnd(32100000)}</b></div>
          <div class="row between"><span class="muted">Đã thu (2 đợt)</span><span class="money">${vnd(12000000)}</span></div>
          <div class="row between"><b style="color:var(--amber)">Còn nợ</b><b class="money" style="color:var(--amber)">${vnd(20100000)}</b></div>
          <div class="progress"><i style="width:37%"></i></div>
          ${readonly ? '' : `<div class="stack" style="gap:8px"><button class="btn btn-primary btn-block" onclick="APP.modal('payment')">💵 Thu tiền mặt (tính tiền thừa)</button><button class="btn btn-outline btn-block" onclick="APP.modal('bank-transfer')">🏦 Xác nhận chuyển khoản thủ công</button><button class="btn btn-ghost btn-sm btn-block" onclick="APP.modal('refund')">↩ Đề xuất / Xác nhận hoàn tiền</button></div>`}
        </div>`, fn('F_BIL_02', 'F_BIL_03'))}
        ${card('Lịch sử thanh toán', table(['Đợt', 'Ngày', 'Hình thức', { t: 'Số tiền', r: 1 }], [['1', '30/10', badge('Tiền mặt', 'green'), vnd(5000000)], ['2', '30/10', badge('Chuyển khoản', 'blue'), vnd(7000000)]]), '', false)}
      </div>
    </div>`;

  // ================= 05. RECEPTION / CASHIER =================
  add('m-dashboard', '05. Lễ tân & Thu ngân', 'Bảng điều khiển nhân viên', ['F_CHK_02', 'F_RPT_01'], 'DashboardView.vue', 'RECEPTIONIST', (role) => `
    ${pageHead('Chào buổi sáng, ' + D.roles[role].user.replace('BS. ', ''), 'Thứ Sáu, 31/10/2026 · ' + roleLabel(role), 'Tổng quan', '<a class="btn btn-primary" href="#/m-checkin?role=' + role + '">+ Tiếp đón khách</a>')}
    <div class="grid g4">${stat('📅', '24', 'Lịch hẹn hôm nay')}${stat('⏳', '6', 'Đang chờ khám', 'var(--amber-50)')}${stat('🦷', '3', 'Đang điều trị', 'var(--violet-50)')}${stat('💰', role === 'ADMIN' || role === 'RECEPTIONIST' ? '18,4 tr' : '9', role === 'ADMIN' || role === 'RECEPTIONIST' ? 'Doanh thu hôm nay' : 'Ca đã hoàn tất', 'var(--green-50)')}</div>
    <div class="grid g-side">
      ${card('Hàng đợi hiện tại', table(['STT', 'Bệnh nhân', 'Lý do', 'Vị trí', 'Trạng thái'], D.queue.slice(0, 5).map((q) => [`<b class="qno" style="font-size:15px">${q.no}</b>`, q.full, q.reason, q.room, status(q.status)])), `<a class="btn btn-outline btn-sm" href="#/m-queue?role=${role}">Mở hàng đợi</a>`, false)}
      <div class="stack">
        ${card('Cần xử lý', `<div class="stack small">
          <div class="row between"><span>📥 Yêu cầu đặt lịch chờ duyệt</span>${badge('5', 'amber')}</div>
          <div class="row between"><span>🧾 Hóa đơn nháp chờ thu</span>${badge('3', 'blue')}</div>
          <div class="row between"><span>⚠️ Vật tư dưới ngưỡng tồn</span>${badge('2', 'red')}</div>
          <div class="row between"><span>🧪 Tiêu hao ca khám chờ xác nhận</span>${badge('4', 'gold')}</div></div>`)}
        ${card('Ghế điều trị', `<div class="stack small">${D.chairs.slice(0, 4).map((c) => `<div class="row between"><span>${c.room} · ${c.chair}</span>${status(c.status)}</div>`).join('')}</div>`)}
      </div>
    </div>`);

  add('m-checkin', '05. Lễ tân & Thu ngân', 'Tiếp đón tại quầy & đối soát CCCD/hộ chiếu', ['F_CHK_01', 'F_PAT_01', 'F_PAT_02', 'F_PAT_03'], 'QueueCheckInView.vue', 'RECEPTIONIST', () => `
    ${pageHead('Tiếp đón khách đến', 'Đối chiếu giấy tờ, kiểm tra trùng hồ sơ, nhắc công nợ và đưa vào hàng đợi chung', 'Tiếp đón & lịch', '<button class="btn btn-outline" onclick="APP.modal(\'qr-scan\')">📷 Quét QR CCCD</button><button class="btn btn-outline" onclick="APP.modal(\'guardian\')">🧒 Giám hộ trẻ em</button>', ['F_CHK_01'])}
    <div class="grid g-side">
      <div class="stack">
        ${card('1. Tìm lịch hẹn / bệnh nhân', `<div class="row"><div style="flex:1">${input('Tên, SĐT, mã hồ sơ hoặc mã yêu cầu đặt lịch', '0909 123 312')}</div><button class="btn btn-primary" onclick="APP.toast('Đã tìm thấy 2 hồ sơ khớp SĐT 0909 123 312')">Tìm</button></div>
          <div style="margin-top:12px">${table(['', 'Họ tên', 'Năm sinh', 'SĐT', 'Lịch hẹn'], [['<input type="radio" name="chk_p" checked>', '<b>Võ Thị Lan</b> ' + badge('Hồ sơ cũ', 'blue'), '1990', '0909 ••• 312', '09:00 · Đau răng'], ['<input type="radio" name="chk_p">', 'Trần Gia Bảo ' + badge('Giám hộ', 'violet'), '2017', '0909 ••• 312', '—']])}</div>
          <div class="small muted" style="margin-top:8px">Nhiều hồ sơ có thể dùng chung số điện thoại.</div>`, fn('F_PAT_02'))}
        ${card('2. Đối chiếu giấy tờ định danh', `<div class="form-grid">
          ${field('Loại giấy tờ', `<div class="seg"><button class="on">CCCD</button><button>Hộ chiếu</button></div>`)}
          ${field('Số CCCD', input('12 chữ số', '079190004521'), { req: 1, hint: 'Gửi bằng POST có JWT · máy chủ băm HMAC, chỉ lưu số che' })}
          ${field('Số điện thoại tại quầy', input('', '0909 123 312'), { req: 1 })}
          ${field('Quốc gia cấp (hộ chiếu)', select(['— Không áp dụng —', 'VN — Việt Nam', 'KR — Hàn Quốc', 'US — Hoa Kỳ']))}
        </div>
        <div class="alert alert-green" style="margin-top:14px"><div class="ai">✅</div><div class="small"><b>Khớp hồ sơ BN-001024 (HMAC v1).</b> Không phát hiện hồ sơ trùng. ${fn('F_PAT_03')}</div></div>`, fn('F_PAT_01'))}
      </div>
      <div class="stack">
        <div class="alert alert-amber"><div class="ai">💰</div><div><b>Nhắc công nợ</b><div class="small">Khách còn nợ <b>${vnd(20100000)}</b> từ hóa đơn HD-2610-0309 (niềng răng, đợt 3/6). Nhắc khách trước khi vào khám.</div></div></div>
        ${card('3. Xác nhận tiếp đón', `<div class="stack small">
          <div class="row between"><span class="muted">Bệnh nhân</span><b>Võ Thị Lan</b></div>
          <div class="row between"><span class="muted">Lý do khám</span><b>Đau răng hàm dưới</b></div>
          <div class="row between"><span class="muted">Số thứ tự dự kiến</span><b class="qno">A014</b></div>
          <div class="row between"><span class="muted">Tài khoản cổng BN</span>${badge('Đã kích hoạt', 'green')}</div>
          <label class="checkbox"><input type="checkbox" checked> Đã đối chiếu giấy tờ gốc với khách</label>
          <button class="btn btn-primary btn-block" onclick="APP.confirmCheckIn()">✓ Xác nhận & đưa vào hàng đợi</button>
          <div class="muted">Lần đầu đến: hệ thống tạo tài khoản bệnh nhân, mật khẩu khởi tạo là SĐT, bắt buộc OTP ở lần đăng nhập đầu.</div></div>`)}
      </div>
    </div>`);

  add('m-queue', '05. Lễ tân & Thu ngân', 'Hàng đợi chung trong ngày', ['F_CHK_02', 'F_CHK_03', 'F_CHK_04', 'F_CHK_07', 'F_CHK_08'], 'DailyQueueView.vue', 'RECEPTIONIST', (role) => {
    const cols = [['waiting', 'Đang chờ'], ['called', 'Đã gọi / dẫn vào'], ['in-exam', 'Đang khám'], ['xray', 'X-quang'], ['done', 'Hoàn tất']];
    return `${pageHead('Hàng đợi hôm nay', 'Danh sách chung, nội bộ — không gán cố định nha sĩ/ghế từ lúc xếp hàng', 'Tiếp đón & lịch', '<div class="seg"><button class="on">Bảng</button><button>Danh sách</button></div><a class="btn btn-outline" href="#/tv">📺 Mở màn hình TV</a>', ['F_CHK_02', 'F_CHK_07'])}
    <div class="kanban">${cols.map(([k, t]) => `<div class="kcol"><div class="kcol-h">${t}${badge(String(D.queue.filter((q) => q.status === k).length), 'blue')}</div>
      ${D.queue.filter((q) => q.status === k).map((q) => `<div class="qcard"><div class="row between"><span class="qno">${q.no}</span><span class="small muted">từ ${q.since}</span></div><b>${q.full}</b><div class="small muted">${q.reason}</div><div class="small">📍 ${q.room}</div>
        <div class="row" style="margin-top:4px">${{ waiting: '<button class="btn btn-primary btn-sm" onclick="APP.modal(\'call\')">📢 Gọi số</button>', called: '<button class="btn btn-primary btn-sm">▶ Bắt đầu khám</button>', 'in-exam': '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'xray\')">🩻 Chuyển X-quang</button><button class="btn btn-success btn-sm">■ Kết thúc</button>', xray: '<button class="btn btn-outline btn-sm">↩ Quay lại nha sĩ</button>', done: '<a class="btn btn-outline btn-sm" href="#/m-invoice-draft?role=' + role + '">🧾 Hóa đơn</a>' }[k]}</div></div>`).join('')}
    </div>`).join('')}</div>
    ${card('Lịch sử chuyển trạng thái — A009 Phan Thanh Tùng', table(['Thời điểm', 'Từ', 'Sang', 'Người/nguồn thực hiện'], [['08:05', 'Đã gọi', 'Đang khám', 'BS. Nguyễn Hoàng Long'], ['08:02', 'Đang chờ', 'Đã gọi', 'Phạm Ngọc Mai (Phụ tá)'], ['07:51', '—', 'Đang chờ', 'Lê Thu Hà (Lễ tân) · tiếp đón']]), fn('F_CHK_07'), false)}`;
  });

  add('m-booking-requests', '05. Lễ tân & Thu ngân', 'Duyệt yêu cầu đặt lịch trực tuyến', ['F_APP_01'], 'BookingRequestReviewView.vue', 'RECEPTIONIST', () => `
    ${pageHead('Yêu cầu đặt lịch trực tuyến', 'Gọi xác nhận khách, xếp vào slot hoặc từ chối kèm lý do', 'Tiếp đón & lịch', '', ['F_APP_01'])}
    <div class="row"><div class="seg"><button class="on">Chờ duyệt (5)</button><button>Đã xác nhận</button><button>Đã từ chối</button></div><div style="flex:1"></div>${input('Tìm theo tên/SĐT')}</div>
    ${card('', table(['Mã YC', 'Khách', 'SĐT', 'Ngày/giờ mong muốn', 'Nhu cầu', 'Gửi lúc', ''], [
      ['YC-26110014', 'Võ Thị Lan', '0909 ••• 312', '01/11 · 09:00', 'Đau răng', '30/10 21:14', act('<button class="btn btn-primary btn-sm" onclick="APP.modal(\'approve-booking\')">Xác nhận</button>', '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'reschedule\')">Đổi giờ</button>', '<button class="btn btn-ghost btn-sm" onclick="APP.modal(\'reject-booking\')">Từ chối</button>')],
      ['YC-26110013', 'Hồ Thị Ngân', '0988 ••• 145', '01/11 · 09:30', 'Niềng răng', '30/10 19:02', act('<button class="btn btn-primary btn-sm" onclick="APP.modal(\'approve-booking\')">Xác nhận</button>', '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'reschedule\')">Đổi giờ</button>', '<button class="btn btn-ghost btn-sm" onclick="APP.modal(\'reject-booking\')">Từ chối</button>')],
      ['YC-26110012', 'Bùi Anh Tuấn', '0965 ••• 333', '01/11 · 10:00', 'Lấy cao răng', '30/10 15:40', act('<button class="btn btn-primary btn-sm" onclick="APP.modal(\'approve-booking\')">Xác nhận</button>', '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'reschedule\')">Đổi giờ</button>', '<button class="btn btn-ghost btn-sm" onclick="APP.modal(\'reject-booking\')">Từ chối</button>')],
      ['YC-26110011', 'Kiều Minh Châu', '0918 ••• 662', '02/11 · 08:00', 'Tẩy trắng', '30/10 10:11', act('<button class="btn btn-primary btn-sm" onclick="APP.modal(\'approve-booking\')">Xác nhận</button>', '<button class="btn btn-outline btn-sm" onclick="APP.modal(\'reschedule\')">Đổi giờ</button>', '<button class="btn btn-ghost btn-sm" onclick="APP.modal(\'reject-booking\')">Từ chối</button>')]]), '', false)}`);

  add('m-counter-booking', '05. Lễ tân & Thu ngân', 'Đặt lịch tại quầy (Lễ tân đặt hộ)', ['F_APP_02'], 'CounterBookingView.vue', 'RECEPTIONIST', () => `
    ${pageHead('Đặt lịch tại quầy', 'Lễ tân đặt hộ khách — kiểm tra slot/công suất và chặn trùng', 'Tiếp đón & lịch', '', ['F_APP_02'])}
    <div class="grid g-side">
      ${card('Thông tin lịch hẹn', `<div class="form-grid">
        ${field('Bệnh nhân', input('Tìm theo tên/SĐT/mã hồ sơ', 'Nguyễn Văn Bình — BN-001025'), { req: 1, full: 1 })}
        ${field('Dịch vụ dự kiến', select(['Tái khám niềng răng', 'Khám & tư vấn', 'Lấy cao răng']))}
        ${field('Nha sĩ (tùy chọn)', select(['Để phòng khám sắp xếp', 'BS. Nguyễn Hoàng Long', 'BS. Lê Thu Trang']))}
        ${field('Ngày', input('', '05/11/2026'), { req: 1 })}
        ${field('Khung giờ', select(['08:00 (còn 2)', '08:30 (còn 1)', '09:00 (đầy)', '09:30 (còn 3)']), { req: 1 })}
        ${field('Ghi chú', textarea('Ghi chú không phải mức ưu tiên'), { full: 1 })}</div>
        <div class="alert alert-red" style="margin-top:12px"><div class="ai">⛔</div><div class="small">Không hỗ trợ cấp cứu/chen lịch. Khung 09:00 đã đầy — vui lòng chọn khung khác.</div></div>
        <div class="row" style="margin-top:14px"><button class="btn btn-primary" onclick="APP.toast(\'Đã tạo lịch hẹn tại quầy thành công\')">Tạo lịch hẹn</button><button class="btn btn-outline" onclick="APP.toast(\'Đã mở bản in phiếu hẹn\')">🖨 In phiếu hẹn</button></div>`)}
      ${card('Công suất ngày 05/11', `<div class="stack small">${[['08:00', 4, 2], ['08:30', 4, 3], ['09:00', 4, 4], ['09:30', 4, 1], ['10:00', 4, 0], ['14:00', 4, 2]].map(([t, c, u]) => `<div><div class="row between"><b>${t}</b><span class="muted">${u}/${c}</span></div><div class="progress"><i style="width:${(u / c) * 100}%;${u >= c ? 'background:var(--red)' : ''}"></i></div></div>`).join('')}</div>`)}
    </div>`);

  add('m-appointment-desk', '05. Lễ tân & Thu ngân', 'Bàn lịch hẹn (lịch ngày/tuần)', ['F_APP_01', 'F_APP_02'], 'AppointmentDeskView.vue', 'RECEPTIONIST', () => `
    ${pageHead('Bàn lịch hẹn', 'Theo dõi, đổi giờ, hủy và nhắc lịch', 'Tiếp đón & lịch', '<div class="seg"><button class="on">Ngày</button><button>Tuần</button></div><button class="btn btn-outline">‹ 30/10</button><button class="btn btn-outline">01/11 ›</button>')}
    <div class="grid g4">${stat('📅', '24', 'Tổng lịch')}${stat('✅', '15', 'Đã tiếp đón', 'var(--green-50)')}${stat('🕘', '7', 'Chưa đến', 'var(--amber-50)')}${stat('✖', '2', 'Đã hủy', 'var(--red-50)')}</div>
    ${card('Lịch hẹn 31/10/2026', table(['Giờ', 'Bệnh nhân', 'Dịch vụ', 'Kênh', 'Trạng thái', ''], D.appointments.map((a) => [`<b>${a.time}</b>`, a.patient, a.service, badge(a.channel, a.channel === 'Trực tuyến' ? 'blue' : 'gray'), status(a.status), act('<button class="btn btn-outline btn-sm" onclick="APP.modal(\'reschedule\')">Đổi giờ</button>', '<button class="btn btn-ghost btn-sm" onclick="APP.modal(\'reminder\')">🔔 Nhắc lịch</button>')])), '', false)}`);

  add('m-invoices', '05. Lễ tân & Thu ngân', 'Danh sách hóa đơn & thu tiền', ['F_BIL_01', 'F_BIL_03', 'F_BIL_07'], 'InvoicesView.vue', 'RECEPTIONIST', (role) => `
    ${pageHead('Hóa đơn & thu tiền', 'Tiền mặt và chuyển khoản thủ công · tiền tính nguyên đồng VND', 'Thu ngân', '', ['F_BIL_01', 'F_BIL_03'])}
    <div class="grid g4">${stat('🧾', '3', 'Hóa đơn nháp')}${stat('⏳', vnd(20100000), 'Công nợ đang theo dõi', 'var(--amber-50)')}${stat('💵', vnd(11200000), 'Tiền mặt hôm nay', 'var(--green-50)')}${stat('🏦', vnd(7200000), 'Chuyển khoản hôm nay')}</div>
    <div class="row"><div class="seg"><button class="on">Tất cả</button><button>Nháp</button><button>Trả một phần</button><button>Đã thanh toán</button><button>Đã hủy</button></div><div style="flex:1"></div>${input('Tìm số hóa đơn / bệnh nhân')}</div>
    ${card('', table(['Số hóa đơn', 'Bệnh nhân', 'Ngày', { t: 'Tổng', r: 1 }, { t: 'Đã thu', r: 1 }, 'Trạng thái', ''], D.invoices.map((i) => [`<b>${i.no}</b>`, i.patient, i.date, vnd(i.total), vnd(i.paid), status(i.status), `<a class="btn btn-outline btn-sm" href="#/m-invoice-draft?role=${role}">Mở</a>`])), '', false)}`);

  add('m-invoice-draft', '05. Lễ tân & Thu ngân', 'Hóa đơn nháp / chi tiết & giảm giá', ['F_BIL_01', 'F_BIL_02', 'F_BIL_03', 'F_BIL_07'], 'InvoiceDraftView.vue', 'RECEPTIONIST', () => `
    ${pageHead('Hóa đơn HD-2610-0309', 'Võ Thị Lan · BN-001024 · Lần khám 30/10/2026', 'Thu ngân › Hóa đơn', '<button class="btn btn-outline" onclick="APP.modal(\'discount\')">🏷 Giảm giá</button><button class="btn btn-outline" onclick="APP.modal(\'cancel\')">Hủy / điều chỉnh nháp</button><a class="btn btn-outline" href="#/m-receipt">🖨 Biên lai</a>', ['F_BIL_01'])}
    ${U.invoiceDetail(false)}
    ${card('Lịch sử điều chỉnh hóa đơn', table(['Thời điểm', 'Thao tác', 'Người thực hiện', 'Lý do'], [['30/10 10:12', 'Áp dụng giảm giá 5%', 'Lê Thu Hà', 'Khách hàng thân thiết'], ['30/10 10:05', 'Tạo hóa đơn nháp', 'Hệ thống (kết thúc khám)', '—']]), fn('F_BIL_07'), false)}`);

  add('m-receipt', '05. Lễ tân & Thu ngân', 'Biên lai thu tiền (A5 & K80)', ['F_BIL_06'], 'PaymentReceiptModal.vue', 'RECEPTIONIST', () => `
    ${pageHead('Biên lai thu tiền', 'Xem trước bản in', 'Thu ngân › Biên lai', '<div class="seg"><button class="on">A5</button><button>K80</button></div><button class="btn btn-primary">🖨 In</button><button class="btn btn-outline">⬇ PDF</button>', ['F_BIL_06'])}
    <div class="grid g-side" style="align-items:start">
      <div class="paper">
        <div class="paper-head"><img src="assets/logo-ngang.svg" alt=""><div class="right small">[ĐIỀN ĐỊA CHỈ]<br>ĐT: [ĐIỀN SĐT]</div></div>
        <h2 style="text-align:center">BIÊN LAI THU TIỀN</h2><div class="small muted" style="text-align:center;margin-bottom:12px">Số: BL-2610-0571 · 30/10/2026 10:18</div>
        <div class="stack small"><div class="row between"><span>Khách hàng</span><b>Võ Thị Lan (BN-001024)</b></div><div class="row between"><span>Hóa đơn</span><b>HD-2610-0309</b></div><div class="row between"><span>Nội dung</span><b>Thu đợt 2 — Niềng răng</b></div><div class="row between"><span>Hình thức</span><b>Chuyển khoản (đã đối soát)</b></div></div>
        <div class="divider" style="margin:12px 0"></div>
        <div class="row between"><b>Số tiền</b><b style="font-size:20px">${vnd(7000000)}</b></div><div class="small muted">Bằng chữ: Bảy triệu đồng chẵn.</div>
        <div class="row between small" style="margin-top:12px"><span>Tổng hóa đơn: ${vnd(32100000)}</span><span>Còn nợ: <b>${vnd(20100000)}</b></span></div>
        <div class="row between small" style="margin-top:36px;text-align:center"><div style="flex:1">Khách hàng<br><br><br>(ký, ghi rõ họ tên)</div><div style="flex:1">Thu ngân<br><br><br>Lê Thu Hà</div></div>
      </div>
      <div class="paper k80"><div style="text-align:center"><b>NHA KHOA HOÀNG GIA</b><br>[ĐIỀN ĐỊA CHỈ]<br>--------------------------------<br><b>BIÊN LAI THU TIỀN</b><br>BL-2610-0571</div><br>KH: Võ Thị Lan<br>HĐ: HD-2610-0309<br>Đợt: 2 · Chuyển khoản<br>--------------------------------<br>SỐ TIỀN: <b>7.000.000 đ</b><br>Còn nợ: 20.100.000 đ<br>--------------------------------<br><div style="text-align:center">Cảm ơn quý khách!</div></div>
    </div>`);

  // ================= 06. CLINICAL =================
  add('m-workspace', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Không gian làm việc Nha sĩ / Phụ tá', ['F_CHK_04', 'F_ASS_06'], 'StaffWorkspacesView.vue', 'DENTIST', (role) => `
    ${pageHead('Không gian làm việc', 'Xác nhận phòng làm việc hôm nay và bệnh nhân của bạn', 'Tổng quan', '', ['F_CHK_04'])}
    <div class="alert alert-blue"><div class="ai">🚪</div><div style="flex:1"><div class="row between"><b>Phòng làm việc hôm nay: Phòng 01 (Ghế 1, Ghế 2)</b><button class="btn btn-outline btn-sm">Đổi phòng</button></div><div class="small">Nha sĩ xác nhận lại phòng mỗi ngày. Phụ tá hỗ trợ: Phạm Ngọc Mai, Đỗ Văn Khoa.</div></div></div>
    <div class="grid g-side">
      ${card('Bệnh nhân của tôi', table(['STT', 'Bệnh nhân', 'Lý do', 'Cảnh báo', 'Trạng thái', ''], [
        ['<b class="qno" style="font-size:15px">A009</b>', 'Phan Thanh Tùng', 'Nhổ răng 38', badge('Tim mạch', 'red'), status('in-exam'), `<a class="btn btn-primary btn-sm" href="#/m-diagnosis?role=${role}">Mở hồ sơ khám</a>`],
        ['<b class="qno" style="font-size:15px">A012</b>', 'Võ Thị Lan', 'Đau răng', badge('Dị ứng', 'red'), status('waiting'), '<button class="btn btn-outline btn-sm">📢 Gọi vào</button>'],
        ['<b class="qno" style="font-size:15px">A013</b>', 'Nguyễn Văn Bình', 'Tái khám niềng', badge('Tiểu đường', 'amber'), status('waiting'), '<button class="btn btn-outline btn-sm">📢 Gọi vào</button>']]), '', false)}
      ${card('Ca hôm nay', `<div class="stack small"><div class="row between"><span>Đã khám</span><b>5</b></div><div class="row between"><span>Đang khám</span><b>1</b></div><div class="row between"><span>Đang chờ</span><b>2</b></div><div class="divider"></div><a class="btn btn-outline btn-sm" href="#/m-chair-status?role=${role}">Trạng thái ghế</a></div>`)}
    </div>`);

  add('m-patients', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Danh sách hồ sơ bệnh nhân', ['F_PAT_01', 'F_PAT_02', 'F_PAT_03'], 'PatientsView.vue', 'RECEPTIONIST', (role) => `
    ${pageHead('Hồ sơ bệnh nhân', 'Tìm kiếm nhanh theo tên, SĐT, mã hồ sơ, giấy tờ (đã che)', 'Bệnh nhân', role === 'RECEPTIONIST' || role === 'ADMIN' ? '<button class="btn btn-primary" onclick="APP.modal(\'new-patient\')">+ Tạo hồ sơ</button>' : '', ['F_PAT_02'])}
    <div class="row"><div style="flex:1;min-width:220px">${input('Nhập ít nhất 2 ký tự…', 'Lan')}</div>${select(['Tất cả', 'Có cảnh báo', 'Trẻ em'])}</div>
    ${card('', table(['Mã hồ sơ', 'Họ tên', 'Ngày sinh', 'SĐT', 'Giấy tờ', 'Cảnh báo', ''], D.patients.map((p) => [p.code, `<b>${p.name}</b>`, p.dob, p.phone, p.doc, p.alert === 'none' ? '<span class="muted">—</span>' : badge(p.note, p.alert), `<a class="btn btn-outline btn-sm" href="#/m-patient-detail?role=${role}">Xem</a>`])), '', false)}`);

  add('m-patient-detail', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Chi tiết hồ sơ: tiền sử, sinh hiệu, timeline, khóa hồ sơ', ['F_PAT_04', 'F_PAT_05', 'F_PAT_06', 'F_PAT_07', 'F_PAT_08', 'F_PAT_09'], 'PatientDetailView.vue', 'DENTIST', (role) => `
    ${pageHead(P.name, `${P.code} · ${P.dob} · ${P.gender} · ${P.phone}`, 'Bệnh nhân › Chi tiết', `<button class="btn btn-outline" onclick="APP.modal('staff-phone-change')">📱 Đổi SĐT (OTP)</button><button class="btn btn-outline" onclick="APP.modal('guardian')">🧒 Người giám hộ</button><button class="btn btn-outline" onclick="APP.modal('new-visit')">+ Tạo lần khám mới</button><button class="btn btn-outline" onclick="APP.modal('lock')">🔒 Chốt hồ sơ</button><a class="btn btn-primary" href="#/m-diagnosis?role=${role}">Khám</a>`, ['F_PAT_09', 'F_PAT_08'])}
    ${safetyBanner(P)}
    <div class="tabs"><a class="on">Tổng quan</a><a href="#/m-fdi?role=${role}">Sơ đồ răng</a><a href="#/m-images?role=${role}">Hình ảnh</a><a href="#/m-prescription?role=${role}">Đơn thuốc</a><a href="#/m-invoice-draft?role=${role}">Hóa đơn</a></div>
    <div class="grid g3">
      ${card('Tiền sử & dị ứng', `<div class="stack small"><div>${badge('Dị ứng', 'red')} Penicillin — phát ban, khó thở</div><div>${badge('Bệnh nền', 'amber')} Tăng huyết áp</div><div>${badge('Thuốc', 'gray')} Amlodipine 5mg/ngày</div><button class="btn btn-outline btn-sm" style="align-self:flex-start" onclick="APP.modal('med-history')">+ Thêm bản ghi tiền sử</button><div class="muted">Ghi theo kiểu bản ghi mới, không ghi đè.</div></div>`, fn('F_PAT_04'))}
      ${card('Sinh hiệu lần khám', `<div class="grid g2 small"><div><div class="muted">Huyết áp</div><b style="color:${D.vitals.sys >= 140 ? 'var(--red)' : 'var(--green)'}">${D.vitals.sys}/${D.vitals.dia}</b> mmHg</div><div><div class="muted">Mạch</div><b>${D.vitals.pulse}</b> l/ph</div><div><div class="muted">Nhiệt độ</div><b>${D.vitals.temp}</b> °C</div><div><div class="muted">SpO₂</div><b>${D.vitals.spo2}</b> %</div></div><button class="btn btn-outline btn-sm" style="margin-top:12px" onclick="APP.modal('vitals')">+ Ghi sinh hiệu</button>`, fn('F_PAT_05'))}
      ${card('Trạng thái hồ sơ', `<div class="stack small"><div class="row between"><span>Lần khám 31/10</span>${badge('Đang mở', 'blue')}</div><div class="row between"><span>Lần khám 12/09</span>${badge('🔒 Đã chốt', 'gray')}</div><div class="muted">Sau khi chốt chỉ Nha sĩ/Admin được điều chỉnh, có lưu lịch sử.</div></div>`, fn('F_PAT_08'))}
    </div>
    ${card('Timeline điều trị', `<div class="timeline">${[['31/10/2026', 'Đang khám', 'Đau răng hàm dưới phải'], ['30/10/2026', 'Điều trị tủy 36 (lần 1)', 'Viêm tủy không hồi phục'], ['12/09/2026', 'Trám 26, 11', 'Sâu ngà']].map(([d, t, c]) => `<div class="tl-item"><div class="row between"><b>${t}</b><span class="small muted">${d}</span></div><div class="small muted">${c}</div></div>`).join('')}</div>`, fn('F_PAT_07'))}`);

  add('m-diagnosis', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Khám & chẩn đoán lâm sàng (kèm cảnh báo an toàn)', ['F_PAT_06', 'F_CHK_04', 'F_CHK_08', 'F_MST_02'], 'ClinicalDiagnosisView.vue', 'DENTIST', (role) => `
    ${pageHead('Khám bệnh — ' + P.name, 'Lượt A012 · Phòng 01 · Ghế 1 · Bắt đầu 09:05', 'Lâm sàng', `<button class="btn btn-outline" onclick="APP.modal('xray')">🩻 Chỉ định X-quang</button><button class="btn btn-success" onclick="APP.finishExam('${role}')">■ Kết thúc khám & Tạo HĐ nháp</button>`, ['F_CHK_04'])}
    ${safetyBanner(P)}
    <div class="tabs"><a class="on">Chẩn đoán</a><a href="#/m-fdi?role=${role}">Sơ đồ răng FDI</a><a href="#/m-fdi-services?role=${role}">Chỉ định dịch vụ</a><a href="#/m-images?role=${role}">Hình ảnh</a><a href="#/m-prescription?role=${role}">Kê đơn</a></div>
    <div class="grid g-side">
      ${card('Chẩn đoán', `<div class="form-grid">
        ${field('Lý do khám / triệu chứng', textarea('', 'Đau nhức răng hàm dưới phải 3 ngày, đau tăng khi nhai và về đêm.'), { full: 1 })}
        ${field('Chẩn đoán (danh mục bệnh lý)', input('Gõ để tìm…', 'K04.0 — Viêm tủy răng'), { full: 1, hint: 'Gợi ý từ danh mục F_MST_02' })}
        ${field('Răng liên quan', input('', '46, 47'))}
        ${field('Mức độ', select(['Trung bình', 'Nhẹ', 'Nặng']))}
        ${field('Kế hoạch điều trị', textarea('', 'Điều trị tủy 46, sau đó bọc mão sứ.'), { full: 1 })}</div>
        <div class="row" style="margin-top:14px"><button class="btn btn-primary" onclick="APP.toast('Đã lưu chẩn đoán lâm sàng cho lượt khám A012')">Lưu chẩn đoán</button><span class="small muted">Nội dung lâm sàng không ghi vào audit log.</span></div>`)}
      <div class="stack">
        ${card('Sinh hiệu trước thủ thuật', `<div class="grid g2 small"><div><div class="muted">Huyết áp</div><b style="color:${D.vitals.sys >= 140 ? 'var(--red)' : 'var(--green)'}">${D.vitals.sys}/${D.vitals.dia}</b></div><div><div class="muted">Mạch</div><b>${D.vitals.pulse}</b></div></div><div class="alert alert-amber" style="margin-top:10px"><div class="ai">⚠️</div><div class="small">Huyết áp cao hơn ngưỡng cấu hình. Đề nghị đo lại sau 5 phút.</div></div>`, `<button class="btn btn-outline btn-sm" onclick="APP.modal('vitals')">Đo lại</button>`)}
        ${card('Điều phối', `<div class="stack small"><div class="row between"><span>Chỉ định chụp</span>${badge('Phim quanh chóp 46', 'gold')}</div><div class="muted">Chỉ chuyển X-quang khi đã có chỉ định ghi nhận; chụp xong tự quay lại nha sĩ.</div></div>`, fn('F_CHK_08'))}
      </div>
    </div>`);

  add('m-fdi', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Sơ đồ răng FDI tương tác (32/20 răng)', ['F_FDI_01', 'F_FDI_02', 'F_FDI_03', 'F_FDI_06', 'F_FDI_07'], 'FdiConditionView.vue', 'DENTIST', (role) => {
    const t = (window.APP && window.APP.state.selectedTooth) || 46;
    const curCode = D.toothState[t] || 'HEA';
    const hist = D.toothHistory[t] || [{ date: '20/06/2026 10:00', cond: 'Khỏe mạnh', surf: 'Toàn bộ', note: 'Khám định kỳ bình thường', doctor: 'BS. Hoàng Long' }];
    return `${pageHead('Sơ đồ răng FDI — ' + P.name, 'Lần khám 31/10/2026 · bấm chọn từng chiếc răng để gán bệnh lý & lưu bản ghi mới', 'Lâm sàng › Sơ đồ răng', '<div class="seg"><button class="on">Người lớn (32)</button><button onclick="location.hash=\'#/m-fdi-child?role=' + role + '\'">Trẻ em (20)</button></div>', ['F_FDI_01'])}
    ${safetyBanner(P)}
    <div class="grid g-side">
      ${card('Sơ đồ răng giải phẫu (Bấm vào răng bất kỳ để chọn)', fdiChart({ selected: t }) + '<div style="margin-top:14px">' + fdiLegend() + '</div>', fn('F_FDI_02'))}
      <div class="stack">
        ${card(`Răng ${t} — Cập nhật tình trạng & ghi chú`, `<div class="stack">
          <div class="field"><label>Mặt răng (bấm để chọn nhiều mặt)</label>${surfaces(['O', 'D'])}</div>
          <div class="field"><label>Tình trạng (danh mục F_MST_02)</label>
            <select id="fdi-cond-select" class="select">${D.conditions.map((c) => `<option value="${c.code}" ${c.code === curCode ? 'selected' : ''}>${c.name}</option>`).join('')}</select>
            <div class="hint">Màu sắc hiển thị trực tiếp lên hình chiếc răng giải phẫu và 5 mặt răng</div>
          </div>
          ${field('Ghi chú lâm sàng theo răng', `<textarea id="fdi-note-input" class="textarea">Tổn thương mặt nhai – xa răng ${t}, gõ nhẹ có phản ứng.</textarea>`)}
          <div class="row"><button class="btn btn-primary" onclick="APP.saveTooth(false)">💾 Lưu tình trạng răng ${t}</button><button class="btn btn-outline" onclick="APP.modal('tooth-history')">🕘 Lịch sử răng</button><a class="btn btn-outline" href="#/m-fdi-services?role=${role}">+ Chỉ định DV</a></div></div>`, fn('F_FDI_03', 'F_FDI_07'))}
        ${card('Lịch sử răng ' + t + ' (append-only)', `<div class="timeline small">${hist.map((h) => `<div class="tl-item"><b>${h.date}</b> · <span class="strong">${h.cond}</span> (${h.surf}) — ${h.doctor}<div class="muted">${h.note}</div></div>`).join('')}</div>`, fn('F_FDI_06'))}
      </div>
    </div>`;
  });

  add('m-fdi-child', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Sơ đồ răng FDI — bộ răng sữa trẻ em (20 răng)', ['F_FDI_01', 'F_FDI_02'], 'FdiDentalChart.vue', 'DENTIST', (role) => {
    const t = (window.APP && window.APP.state.selectedChildTooth) || 84;
    const curCode = D.childToothState[t] || 'HEA';
    return `${pageHead('Sơ đồ răng FDI — Trần Gia Bảo (9 tuổi)', 'Bộ răng sữa 51–55, 61–65, 71–75, 81–85', 'Lâm sàng › Sơ đồ răng', '<div class="seg"><button onclick="location.hash=\'#/m-fdi?role=' + role + '\'">Người lớn (32)</button><button class="on">Trẻ em (20)</button></div>', ['F_FDI_01'])}
    <div class="grid g-side">
      ${card('Sơ đồ răng sữa giải phẫu (20 răng)', fdiChart({ child: true, selected: t }) + '<div style="margin-top:14px">' + fdiLegend() + '</div>')}
      ${card('Răng sữa ' + t, `<div class="stack"><div class="field"><label>Mặt răng</label>${surfaces(['O'])}</div>
        <div class="field"><label>Tình trạng</label><select id="fdi-cond-select" class="select">${D.conditions.map((c) => `<option value="${c.code}" ${c.code === curCode ? 'selected' : ''}>${c.name}</option>`).join('')}</select></div>
        ${field('Ghi chú lâm sàng', '<textarea id="fdi-note-input" class="textarea">Theo dõi răng sữa chuẩn bị thay.</textarea>')}
        <button class="btn btn-primary" style="align-self:flex-start" onclick="APP.saveTooth(true)">💾 Lưu tình trạng răng ${t}</button></div>`)}
    </div>`;
  });

  add('m-fdi-preview', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Kiểm thử toàn diện Sơ đồ răng giải phẫu FDI & Gợi ý dịch vụ', ['F_FDI_01', 'F_FDI_02', 'F_FDI_03', 'F_FDI_04', 'F_FDI_06'], 'FdiPreviewView.vue / ToothHistoryDrawer.vue', 'DENTIST', (role) => {
    const t = (window.APP && window.APP.state.selectedTooth) || 46;
    const curCode = D.toothState[t] || 'CAR';
    return `${pageHead('Bàn kiểm thử Sơ đồ răng giải phẫu FDI', 'Đối chiếu trực quan 7 tình trạng răng giải phẫu, 5 mặt răng (B/L/M/D/O), gợi ý dịch vụ và ngăn kéo lịch sử răng', 'Lâm sàng › Kiểm thử FDI', `<button class="btn btn-outline" onclick="APP.modal('tooth-history')">🗂 Mở ngăn kéo lịch sử răng ${t}</button><a class="btn btn-primary" href="#/m-fdi?role=${role}">Về hồ sơ khám FDI</a>`, ['F_FDI_01', 'F_FDI_03', 'F_FDI_06'])}
    <div class="grid g4">${D.conditions.map((c, idx) => `<div class="card card-pad row between" style="cursor:pointer" onclick="DEMO.toothState[${t}]='${c.code}';location.reload ? APP.saveToothDirect('${c.code}') : null"><div class="row"><i style="width:16px;height:16px;border-radius:4px;background:${c.color};border:1px solid #CBD5E1;display:inline-block"></i><b>${c.name}</b></div><span class="small muted">${c.code}</span></div>`).join('')}</div>
    <div class="grid g-side">
      ${card('Sơ đồ 32 răng giải phẫu (Bấm chọn răng · Bấm thẻ màu phía trên để đổi nhanh hình thái bệnh lý)', fdiChart({ selected: t }) + '<div style="margin-top:12px">' + fdiLegend() + '</div>')}
      <div class="stack">
        ${card(`Chi tiết giải phẫu răng ${t} (${curCode})`, `<div class="row" style="gap:18px;align-items:center"><div style="width:74px;height:110px;border:1px solid var(--line);border-radius:12px;display:grid;place-items:center;background:#F8FAFC">${U.toothAnatSvg ? U.toothAnatSvg(t, curCode, false) : ''}</div><div class="stack small" style="flex:1"><div><b>Phân nhóm:</b> ${t % 10 >= 6 ? 'Răng hàm lớn (Molar)' : t % 10 >= 4 ? 'Răng tiền cối (Premolar)' : t % 10 === 3 ? 'Răng nanh (Canine)' : 'Răng cửa (Incisor)'}</div><div><b>Cung hàm:</b> ${Math.floor(t / 10) <= 2 ? 'Hàm trên (chân hướng lên)' : 'Hàm dưới (chân hướng xuống)'}</div><div><b>Mặt răng chọn:</b></div>${surfaces(['O', 'M'])}</div></div>`)}
        ${card('Gợi ý dịch vụ theo tình trạng (ServiceSuggestionList)', table(['Bệnh lý', 'Dịch vụ gợi ý', { t: 'Đơn giá', r: 1 }, ''], [
          ['Sâu răng (CAR)', 'Trám răng Composite thẩm mỹ', vnd(400000), `<button class="btn btn-outline btn-sm" onclick="APP.toast('Đã thêm chỉ định Trám răng Composite cho răng ${t}')">+ Chỉ định</button>`],
          ['Đã lấy tủy (RCT)', 'Điều trị tủy răng hàm + Mão sứ Zirconia', vnd(7300000), `<button class="btn btn-outline btn-sm" onclick="APP.toast('Đã thêm chỉ định Điều trị tủy cho răng ${t}')">+ Chỉ định</button>`],
          ['Mất răng (MIS)', 'Cấy ghép trụ răng Titanium', vnd(18000000), `<button class="btn btn-outline btn-sm" onclick="APP.toast('Đã thêm chỉ định Cấy ghép trụ răng cho răng ${t}')">+ Chỉ định</button>`]
        ]), fn('F_FDI_04'), false)}
      </div>
    </div>`;
  });

  add('m-fdi-services', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Chỉ định dịch vụ theo răng & cập nhật sau điều trị', ['F_FDI_04', 'F_FDI_05'], 'FdiServiceAssignmentView.vue', 'DENTIST', () => `
    ${pageHead('Chỉ định dịch vụ theo răng', P.name + ' · Lần khám 31/10/2026', 'Lâm sàng › Chỉ định', '', ['F_FDI_04', 'F_FDI_05'])}
    <div class="grid g-side">
      ${card('Dịch vụ đã chỉ định', table(['Răng', 'Mặt', 'Dịch vụ', { t: 'Đơn giá snapshot', r: 1 }, 'Trạng thái', ''], D.toothServices.map((s) => [
        `<b>${s.tooth}</b>`, s.surf, s.name, vnd(s.price), status(s.status),
        s.status === 'done' ? '<span class="small muted">✓ Đã cập nhật FDI</span>' : `<button class="btn btn-success btn-sm" onclick="APP.completeService(${s.id})">✓ Hoàn tất</button>`
      ])), '', false)}
      ${card('Thêm chỉ định', `<div class="stack">
        ${field('Răng FDI', '<input id="svc-tooth-input" class="input" value="46">')}
        ${field('Dịch vụ (bảng giá hiện hành)', `<select id="svc-code-select" class="select">${D.services.map((s, idx) => `<option value="${idx}">${s.name} — ${vnd(s.price)}</option>`).join('')}</select>`)}
        ${field('Ghi chú', textarea())}
        <button class="btn btn-primary" onclick="APP.addService()">+ Thêm chỉ định</button>
        <div class="small muted">Giá được chụp (snapshot) tại thời điểm chỉ định. Hoàn tất điều trị tự động sinh bản ghi FDI mới.</div></div>`)}
    </div>`);

  add('m-images', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Tải & xem ảnh X-quang / trong miệng', ['F_IMG_02', 'F_IMG_04'], 'ToothImageLookupView.vue', 'ASSISTANT', () => {
    const z = (window.APP && window.APP.state.imgZoom) || 1;
    const rot = (window.APP && window.APP.state.imgRot) || 0;
    const inv = (window.APP && window.APP.state.imgInv) ? 'filter:invert(1);' : '';
    const cap = (window.APP && window.APP.state.imgCap) || 'Phim quanh chóp · Răng 46 · 31/10/2026 09:22';
    return `${pageHead('Hình ảnh lần khám', P.name + ' · 31/10/2026 · ảnh phục vụ qua API có xác thực', 'Lâm sàng › Hình ảnh', '<button class="btn btn-outline" onclick="APP.modal(\'img-canvas\')">🎨 Vẽ / Đo đạc trên phim</button><button class="btn btn-outline" onclick="APP.modal(\'img-meta\')">📝 Sửa thông tin ảnh</button><button class="btn btn-outline" onclick="APP.modal(\'img-delete\')">🗑 Xóa ảnh</button><button class="btn btn-primary" onclick="APP.modal(\'upload\')">⬆ Tải ảnh lên</button>', ['F_IMG_02', 'F_IMG_04'])}
    <div class="grid g-side">
      <div class="stack"><div class="viewer" style="overflow:hidden"><div class="viewer-tools"><button title="Phóng to" onclick="APP.imgOp('zoom',0.2)">＋</button><button title="Thu nhỏ" onclick="APP.imgOp('zoom',-0.2)">－</button><button title="Xoay 90°" onclick="APP.imgOp('rot',90)">⟲</button><button title="Đảo tương phản" onclick="APP.imgOp('inv')">◐</button><button title="Đặt lại" onclick="APP.imgOp('reset')">⟲0</button></div><div style="text-align:center;transition:transform .2s;transform:scale(${z}) rotate(${rot}deg);${inv}"><div style="font-size:76px">🩻</div>${cap}<div class="small muted">Tỷ lệ: ${Math.round(z * 100)}% · Góc: ${rot}°</div></div></div>
      ${card('Ghi chú lâm sàng trên ảnh (DentalImageClinicalNotes)', `<div class="stack small"><div>Thấu quang vùng chóp chân xa răng 46, đường kính ~2.5mm. — <b>BS. Nguyễn Hoàng Long</b> (31/10 09:25)</div><div class="row"><input class="input" placeholder="Nhập nhận xét lâm sàng mới cho phim X-quang này…" style="flex:1"><button class="btn btn-outline btn-sm" onclick="APP.toast('Đã thêm ghi chú lâm sàng vào ảnh X-quang')">+ Lưu ghi chú</button></div></div>`)}</div>
      <div class="stack">${card('Thư viện ảnh (Bấm để xem)', `<div class="thumbs">${['Quanh chóp 46', 'Toàn cảnh Panorex', 'Trong miệng – hàm dưới', 'Trong miệng – hàm trên', 'Cắn khớp bên phải'].map((c) => `<div class="thumb" onclick="APP.imgOp('select','${c}')">🩻<div class="cap">${c}</div></div>`).join('')}</div>`)}
      ${card('Bộ lọc', `<div class="stack">${field('Răng', select(['Tất cả', '46', '36', '16']))}${field('Loại ảnh', select(['Tất cả', 'X-quang quanh chóp', 'Toàn cảnh Panorex', 'Ảnh trong miệng']))}</div>`)}</div>
    </div>`;
  });

  add('m-prescription', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Kê đơn điện tử, kiểm tra tồn, in & giao thuốc', ['F_RX_01', 'F_RX_02', 'F_RX_04', 'F_RX_05'], 'PrescriptionView.vue', 'DENTIST', () => `
    ${pageHead('Kê đơn thuốc', P.name + ' · Lần khám 31/10/2026', 'Lâm sàng › Kê đơn', '<button class="btn btn-outline" onclick="APP.modal(\'print-rx\')">🖨 In đơn (QR)</button><button class="btn btn-outline" onclick="APP.modal(\'handover\')">📦 Xác nhận giao thuốc</button>', ['F_RX_01'])}
    <div class="alert alert-red"><div class="ai">⚠️</div><div class="small"><b>Bệnh nhân dị ứng Penicillin.</b> Amoxicillin thuộc nhóm Penicillin — vui lòng chọn thuốc thay thế.</div></div>
    <div class="grid g-side">
      ${card('Đơn thuốc điện tử', table(['Thuốc', 'SL', 'Cách dùng', 'Tồn kho', ''], D.rxItems.map((it, idx) => [
        `<b>${it.name}</b>`, it.qty, it.usage, badge(it.stockText, it.stockStatus), `<button class="btn btn-ghost btn-sm" onclick="APP.removeRx(${idx})">✕</button>`
      ])) + `<div class="card-body"><div class="form-grid">${field('Thêm thuốc từ danh mục', `<select id="rx-med-select" class="select">${D.medicines.map((m) => `<option value="${m.name}">${m.name} (Tồn: ${m.stock} ${m.unit})</option>`).join('')}</select>`)}${field('Số lượng & Liều dùng', '<input id="rx-usage-input" class="input" value="10 viên · Uống 1 viên × 2 lần/ngày sau ăn">')}${field('Lời dặn bác sĩ', input('', 'Chườm lạnh 24h đầu, tái khám sau 7 ngày'), { full: 1 })}</div><div class="row" style="margin-top:12px"><button class="btn btn-outline" onclick="APP.addRx()">+ Thêm vào đơn</button><button class="btn btn-primary" onclick="APP.toast('Đã lưu đơn thuốc điện tử RX-2610-9K4P1')">💾 Lưu đơn</button><span class="small muted">Kê đơn chỉ cảnh báo tồn, không giữ/trừ kho. ${fn('F_RX_02')}</span></div></div>`, '', false)}
      ${card('Lịch sử đơn thuốc', `<div class="stack small"><div class="row between"><b>RX-2610-8F3K2</b><span class="muted">30/10</span></div><div class="row between"><b>RX-2609-1A9D0</b><span class="muted">12/09</span></div></div>`, fn('F_RX_05'))}
    </div>`);

  add('m-signature', '06. Lâm sàng (Nha sĩ & Phụ tá)', 'Chữ ký bác sĩ (in đơn)', ['F_RX_04', 'F_MST_07'], 'DoctorSignatureView.vue', 'DENTIST', () => `
    ${pageHead('Chữ ký bác sĩ', 'Dùng trên đơn thuốc in', 'Nhân sự › Chữ ký', '', ['F_RX_04'])}
    ${card('BS. Nguyễn Hoàng Long', `<div class="grid g2"><div class="viewer" style="background:#fff;border:2px dashed var(--border);color:var(--muted);aspect-ratio:3/1">Kéo thả ảnh chữ ký (PNG nền trong)</div><div class="stack small"><div>Chữ ký hiện tại:</div><div style="font-family:cursive;font-size:32px;color:var(--navy)">Hoàng Long</div><div class="row"><button class="btn btn-primary btn-sm" onclick="APP.toast('Đã cập nhật chữ ký số của bác sĩ')">Tải lên</button><button class="btn btn-outline btn-sm">Xóa</button></div></div></div>`)}`);

  // ================= 07. INVENTORY & RESOURCES =================
  add('m-chair-status', '07. Kho & Tài nguyên', 'Trạng thái ghế / phòng điều trị', ['F_ASS_06'], 'ChairStatusView.vue', 'ASSISTANT', () => `
    ${pageHead('Trạng thái ghế & phòng', 'Bấm vào nút trên từng ghế để cập nhật trạng thái thời gian thực', 'Kho & tài nguyên', '', ['F_ASS_06'])}
    <div class="legend"><span><i style="background:var(--green)"></i>Sẵn sàng</span><span><i style="background:var(--red)"></i>Đang điều trị</span><span><i style="background:var(--gold)"></i>Chờ khử khuẩn</span><span><i style="background:#94A3B8"></i>Bảo trì</span></div>
    <div class="grid g3">${D.chairs.map((c, idx) => `<div class="chair ${c.status === 'ready' ? '' : c.status}"><div class="row between"><b>${c.room} · ${c.chair}</b>${status(c.status)}</div><div class="small muted">👨‍⚕️ ${c.dentist}</div><div class="small">🧑 ${c.patient}${c.since ? ' · từ ' + c.since : ''}</div><div class="row">${c.status === 'clean' ? `<button class="btn btn-success btn-sm" onclick="APP.cycleChair(${idx})">✓ Đã khử khuẩn → Sẵn sàng</button>` : c.status === 'busy' ? `<button class="btn btn-outline btn-sm" onclick="APP.cycleChair(${idx})">Chuyển Chờ khử khuẩn</button>` : `<button class="btn btn-outline btn-sm" onclick="APP.cycleChair(${idx})">Đổi trạng thái</button>`}</div></div>`).join('')}</div>`);

  add('m-stock-issues', '07. Kho & Tài nguyên', 'Xác nhận tiêu hao vật tư theo ca khám', ['F_INV_06'], 'StockIssuesView.vue', 'ASSISTANT', () => `
    ${pageHead('Tiêu hao vật tư theo ca', 'Nhập số lượng thực tế — chỉ trừ tồn sau khi bấm Xác nhận', 'Kho › Tiêu hao', '', ['F_INV_06'])}
    <div class="grid g-side">
      ${card('Ca A009 · Phan Thanh Tùng · Nhổ răng 38', table(['Vật tư', 'Đơn vị', 'Tồn hiện tại', 'SL thực tế'], [
        ['Thuốc tê Lidocaine 2%', 'Ống', '42', '<input class="input" style="width:90px" value="2">'],
        ['Găng tay y tế', 'Hộp', '18', '<input class="input" style="width:90px" value="0">'],
        ['Bông gạc vô trùng', 'Gói', '120', '<input class="input" style="width:90px" value="3">'],
        ['Kim tiêm nha khoa 27G', 'Cái', '230', '<input class="input" style="width:90px" value="2">']]) + `<div class="card-body row"><button class="btn btn-outline">+ Thêm vật tư</button><div style="flex:1"></div><button class="btn btn-primary" onclick="APP.modal('confirm-issue')">✓ Xác nhận trừ kho</button></div>`, badge('Chờ xác nhận', 'amber'), false)}
      ${card('Ca chờ xác nhận', `<div class="stack small">${[['A009', 'Phan Thanh Tùng', 'amber'], ['A007', 'Trần Gia Bảo', 'amber'], ['A005', 'Lê Văn Phúc', 'green']].map(([n, p, t]) => `<div class="row between"><span><b>${n}</b> ${p}</span>${badge(t === 'green' ? 'Đã xác nhận' : 'Chờ', t)}</div>`).join('')}<div class="muted">Mọi thay đổi tồn kho ghi audit log kèm người xác nhận.</div></div>`)}
    </div>`);

  add('m-stock-receipts', '07. Kho & Tài nguyên', 'Phiếu nhập kho & cảnh báo tồn tối thiểu', ['F_INV_03', 'F_MST_04'], 'StockReceiptsView.vue', 'ASSISTANT', (role) => `
    ${pageHead('Phiếu nhập kho', role === 'DENTIST' ? 'Nha sĩ chỉ xem tồn, không xác nhận nhập/xuất' : 'Lập phiếu nhập — cộng tồn sau khi người có quyền xác nhận', 'Kho › Nhập kho', role === 'DENTIST' ? '' : '<button class="btn btn-primary" onclick="APP.modal(\'receipt\')">+ Lập phiếu nhập</button>', ['F_INV_03'])}
    <div class="alert alert-amber"><div class="ai">⚠️</div><div class="small"><b>2 vật tư dưới ngưỡng tồn tối thiểu:</b> Thuốc tê Lidocaine 2% (42/50 ống) · Composite A2 sắp chạm ngưỡng. Chỉ cảnh báo — không tự đặt hàng.</div></div>
    <div class="grid g-side">
      ${card('Danh sách phiếu nhập', table(['Số phiếu', 'Ngày', 'Nhà cung cấp', 'Số mặt hàng', 'Người lập', 'Trạng thái'], [['PN-2610-021', '30/10', 'Cty Dược Nha Việt', '4', 'Phạm Ngọc Mai', badge('Chờ xác nhận', 'amber')], ['PN-2610-020', '25/10', 'Cty Vật tư Minh Long', '6', 'Trần Minh Quân', badge('Đã nhập kho', 'green')], ['PN-2610-019', '18/10', 'Cty Dược Nha Việt', '2', 'Phạm Ngọc Mai', badge('Đã nhập kho', 'green')]]), '', false)}
      ${card('Tồn vật tư', table(['Vật tư', 'Tồn', 'Ngưỡng'], D.supplies.map((s) => [s.name, s.stock < s.min ? `<b style="color:var(--red)">${s.stock}</b>` : s.stock, s.min])), '', false)}
    </div>`);

  add('m-medicine-stock', '07. Kho & Tài nguyên', 'Tồn kho thuốc & nhập thuốc', ['F_RX_02', 'F_MST_03'], 'MedicineStockView.vue', 'ADMIN', () => `
    ${pageHead('Tồn kho thuốc', 'Giao thuốc hợp lệ trừ tồn cùng giao dịch với audit; thiếu tồn thì chặn toàn bộ', 'Kho › Thuốc', '<button class="btn btn-primary">+ Nhập thuốc</button>', ['F_RX_02'])}
    ${card('', table(['Mã', 'Thuốc', 'Đơn vị', { t: 'Tồn', r: 1 }, { t: 'Ngưỡng', r: 1 }, 'Tình trạng'], D.medicines.map((m) => [m.code, `<b>${m.name}</b>`, m.unit, m.stock, m.min, m.stock === 0 ? badge('Hết hàng', 'red') : m.stock < m.min ? badge('Dưới ngưỡng', 'amber') : badge('Đủ', 'green')])), '', false)}`);

  // ================= 08. ADMIN =================
  add('m-revenue', '08. Quản trị & Báo cáo', 'Báo cáo doanh thu', ['F_RPT_01'], 'RevenueReportView.vue', 'ADMIN', () => {
    const v = [12, 18, 15, 22, 19, 26, 9, 21, 24, 17, 28, 31];
    return `${pageHead('Báo cáo doanh thu', 'Chỉ tính giao dịch đã thanh toán', 'Quản trị › Báo cáo', '<div class="seg"><button>Ngày</button><button class="on">Tháng</button><button>Năm</button></div><button class="btn btn-outline">⬇ Xuất Excel</button>', ['F_RPT_01'])}
    <div class="grid g4">${stat('💰', '486,2 tr', 'Doanh thu tháng 10', 'var(--green-50)')}${stat('💵', '58%', 'Tiền mặt')}${stat('🏦', '42%', 'Chuyển khoản')}${stat('⏳', '73,5 tr', 'Công nợ chưa thu', 'var(--amber-50)')}</div>
    <div class="grid g-side">
      ${card('Doanh thu theo ngày (triệu đồng)', `<div class="bars" style="margin-bottom:24px">${v.map((x, i) => `<div class="bar" style="height:${(x / 31) * 100}%"><span>${20 + i}/10</span></div>`).join('')}</div>`)}
      ${card('Theo nhóm dịch vụ', `<div class="stack small">${[['Chỉnh nha', 38], ['Phục hình', 24], ['Nội nha', 14], ['Phục hồi', 11], ['Nha chu', 8], ['Khác', 5]].map(([g, p]) => `<div><div class="row between"><span>${g}</span><b>${p}%</b></div><div class="progress"><i style="width:${p}%"></i></div></div>`).join('')}</div>`)}
    </div>
    ${card('Giao dịch đã thanh toán gần đây', table(['Thời điểm', 'Biên lai', 'Bệnh nhân', 'Hình thức', { t: 'Số tiền', r: 1 }], [['31/10 10:18', 'BL-2610-0571', 'Võ Thị Lan', badge('Chuyển khoản', 'blue'), vnd(7000000)], ['31/10 09:40', 'BL-2610-0570', 'Đặng Mỹ Linh', badge('Tiền mặt', 'green'), vnd(400000)], ['30/10 16:22', 'BL-2610-0566', 'Nguyễn Văn Bình', badge('Tiền mặt', 'green'), vnd(5600000)]]), '', false)}`;
  });

  add('m-staff', '08. Quản trị & Báo cáo', 'Quản lý người dùng nhân viên', ['F_MST_07', 'F_AUTH_02'], 'StaffView.vue', 'ADMIN', () => `
    ${pageHead('Nhân viên', 'Tạo tài khoản, gán vai trò, khóa/mở khóa', 'Quản trị › Nhân viên', '<button class="btn btn-primary" onclick="APP.modal(\'new-staff\')">+ Thêm nhân viên</button>', ['F_MST_07'])}
    ${card('', table(['Họ tên', 'Vai trò', 'SĐT', 'Trạng thái', ''], D.staff.map((s) => [`<div class="row"><div class="avatar">${s.name.replace('BS. ', '').split(' ').slice(-2).map((x) => x[0]).join('')}</div><b>${s.name}</b></div>`, badge(D.roles[s.role].label, 'blue'), s.phone, s.active ? badge('Đang hoạt động', 'green') : badge('Đã khóa', 'red'), act('<button class="btn btn-outline btn-sm">Sửa</button>', s.active ? '<button class="btn btn-ghost btn-sm">Khóa</button>' : '<button class="btn btn-ghost btn-sm">Mở khóa</button>')])), '', false)}`);

  add('m-roles', '08. Quản trị & Báo cáo', 'Vai trò & ma trận phân quyền (RBAC)', ['F_AUTH_05'], 'RolePermissionsView.vue', 'ADMIN', () => {
    const rows = [['Tiếp đón & hàng đợi', 1, 1, 1, 1, 0], ['Đặt lịch tại quầy', 1, 1, 0, 0, 0], ['Hồ sơ bệnh nhân (xem)', 1, 1, 1, 1, 'Chính mình'], ['Sơ đồ răng FDI (cập nhật)', 1, 0, 1, 0, 0], ['Kê đơn thuốc', 1, 0, 1, 0, 0], ['Tải ảnh X-quang', 1, 0, 1, 1, 0], ['Hóa đơn & thu tiền', 1, 1, 0, 0, 'Xem'], ['Phiếu nhập kho', 1, 0, 'Xem', 1, 0], ['Xác nhận tiêu hao', 1, 0, 0, 1, 0], ['Danh mục hệ thống', 1, 0, 0, 0, 0], ['Báo cáo doanh thu', 1, 0, 0, 0, 0], ['Nhật ký thao tác', 1, 0, 0, 0, 0]];
    const cell = (v) => v === 1 ? '<span class="dot-yes">✓</span>' : v === 0 ? '<span class="dot-no">—</span>' : `<span class="small">${v}</span>`;
    return `${pageHead('Vai trò & quyền', 'Đối chiếu docs/ROLE_PERMISSION_MATRIX.md — quyền thật nằm ở backend', 'Quản trị › Phân quyền', '', ['F_AUTH_05'])}
    ${card('', `<div class="table-wrap"><table class="matrix"><thead><tr><th>Chức năng</th><th>Admin</th><th>Lễ tân</th><th>Nha sĩ</th><th>Phụ tá</th><th>Bệnh nhân</th></tr></thead><tbody>${rows.map((r) => `<tr><td>${r[0]}</td>${r.slice(1).map((v) => `<td>${cell(v)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`, '', false)}`;
  });

  const catalog = (id, title, codes, view, cols, rows, sub) => add(id, '08. Quản trị & Báo cáo', title, codes, view, 'ADMIN', () => `
    ${pageHead(title, sub, 'Danh mục', '<button class="btn btn-primary" onclick="APP.modal(\'catalog\')">+ Thêm mới</button>', codes)}
    <div class="row"><div style="flex:1;min-width:200px">${input('Tìm kiếm…')}</div>${select(['Đang dùng', 'Ngừng dùng', 'Tất cả'])}</div>
    ${card('', table([...cols, ''], rows.map((r) => [...r, act('<button class="btn btn-outline btn-sm">Sửa</button>', '<button class="btn btn-ghost btn-sm">Ngừng</button>')])), '', false)}`);

  catalog('m-services', 'Dịch vụ & bảng giá', ['F_MST_01'], 'ServiceCatalogView.vue', ['Mã', 'Tên dịch vụ', 'Nhóm', 'Đơn vị', { t: 'Giá hiện hành', r: 1 }], D.services.map((s) => [s.code, `<b>${s.name}</b>`, badge(s.group, 'blue'), s.unit, vnd(s.price)]), 'Thay đổi giá không ảnh hưởng hóa đơn/chỉ định đã snapshot');
  catalog('m-diagnoses', 'Bệnh lý / triệu chứng', ['F_MST_02'], 'DiagnosisCatalogView.vue', ['Mã', 'Tên', 'Màu hiển thị FDI'], D.conditions.map((c) => [c.code, `<b>${c.name}</b>`, `<span class="row"><i style="display:inline-block;width:16px;height:16px;border-radius:4px;border:1px solid #CBD5E1;background:${c.color}"></i>${c.color}</span>`]), 'Dùng cho chẩn đoán và gán tình trạng răng');
  catalog('m-medicines', 'Danh mục thuốc', ['F_MST_03'], 'MedicineCatalogView.vue', ['Mã', 'Tên thuốc', 'Đơn vị', { t: 'Ngưỡng tồn', r: 1 }], D.medicines.map((m) => [m.code, `<b>${m.name}</b>`, m.unit, m.min]), 'Hoạt chất, quy cách, ngưỡng cảnh báo');
  catalog('m-supplies', 'Vật tư & ngưỡng tồn', ['F_MST_04'], 'MedicalSupplyCatalogView.vue', ['Mã', 'Vật tư', 'Đơn vị', { t: 'Tồn', r: 1 }, { t: 'Ngưỡng tối thiểu', r: 1 }], D.supplies.map((s) => [s.code, `<b>${s.name}</b>`, s.unit, s.stock, s.min]), 'Ngưỡng dùng cho cảnh báo nhập hàng');
  catalog('m-chairs', 'Phòng & ghế điều trị', ['F_MST_05'], 'DentalChairsView.vue', ['Phòng', 'Ghế', 'Loại', 'Trạng thái'], D.chairs.map((c) => [c.room, `<b>${c.chair}</b>`, c.room === 'X-quang' ? 'Cận lâm sàng' : 'Điều trị', status(c.status)]), 'Giữ lịch sử ghế cũ khi chuyển dữ liệu');

  add('m-scheduling', '08. Quản trị & Báo cáo', 'Cấu hình lịch khám & slot', ['F_MST_06'], 'SchedulingSettingsView.vue', 'ADMIN', () => `
    ${pageHead('Cấu hình lịch khám', 'Giờ làm việc, độ dài slot, công suất', 'Quản trị › Cấu hình', '<button class="btn btn-primary">Lưu cấu hình</button>', ['F_MST_06'])}
    <div class="grid g2">
      ${card('Giờ làm việc', table(['Ngày', 'Sáng', 'Chiều', 'Mở cửa'], [['Thứ 2 – Thứ 6', '07:30 – 11:30', '13:30 – 20:00', '<input type="checkbox" checked>'], ['Thứ 7', '07:30 – 11:30', '13:30 – 17:00', '<input type="checkbox" checked>'], ['Chủ nhật', '07:30 – 12:00', '—', '<input type="checkbox" checked>']]), '', false)}
      ${card('Slot & công suất', `<div class="form-grid">${field('Độ dài slot', select(['30 phút', '15 phút', '60 phút']))}${field('Công suất mỗi slot', input('', '4', 'number'))}${field('Giờ chốt nhận yêu cầu', input('', '23:30'), { hint: 'Yêu cầu sau giờ này tính cho ngày kế tiếp' })}${field('Ngưỡng cảnh báo huyết áp', input('', '≥ 140/90'))}</div>`)}
    </div>`);

  add('m-print-settings', '08. Quản trị & Báo cáo', 'Cấu hình in ấn phòng khám', ['F_MST_06', 'F_BIL_06', 'F_RX_04'], 'ClinicPrintSettingsView.vue', 'ADMIN', () => `
    ${pageHead('Cấu hình in ấn', 'Thông tin đầu phiếu đơn thuốc, biên lai', 'Quản trị › Cấu hình', '<button class="btn btn-primary">Lưu</button>', ['F_MST_06'])}
    <div class="grid g2">
      ${card('Thông tin phòng khám', `<div class="form-grid">${field('Tên phòng khám', input('', 'Phòng khám Nha khoa Hoàng Gia'), { full: 1 })}${field('Địa chỉ', input('', '[ĐIỀN ĐỊA CHỈ]'), { full: 1 })}${field('Điện thoại', input('', '[ĐIỀN SĐT]'))}${field('Khổ biên lai mặc định', select(['A5', 'K80', 'A4']))}${field('Logo', '<div class="row"><img src="assets/logo-ngang.svg" style="height:40px"><button class="btn btn-outline btn-sm">Đổi</button></div>', { full: 1 })}</div>`)}
      ${card('Xem trước đầu phiếu', `<div class="paper" style="max-width:none"><div class="paper-head"><img src="assets/logo-ngang.svg" alt=""><div class="right small">[ĐIỀN ĐỊA CHỈ]<br>ĐT: [ĐIỀN SĐT]</div></div><h3 style="text-align:center">ĐƠN THUỐC</h3></div>`)}
    </div>`);

  add('m-audit', '08. Quản trị & Báo cáo', 'Nhật ký thao tác (Audit Log)', ['F_AUTH_06'], 'AuditLogView.vue', 'ADMIN', () => `
    ${pageHead('Nhật ký thao tác', 'Chỉ Admin xem · không ghi nội dung y khoa hoặc dữ liệu định danh thô', 'Quản trị › Audit', '<button class="btn btn-outline">⬇ Xuất CSV</button>', ['F_AUTH_06'])}
    <div class="row">${input('', '31/10/2026')}${select(['Tất cả hành động', 'Thanh toán', 'Kho', 'Tài khoản', 'Hồ sơ'])}${select(['Tất cả người dùng', 'Lê Thu Hà', 'Phạm Ngọc Mai'])}</div>
    ${card('', table(['Thời điểm', 'Người thực hiện', 'Vai trò', 'Hành động', 'Đối tượng', 'IP'], [
      ['31/10 10:18:22', 'Lê Thu Hà', 'Lễ tân', badge('Xác nhận chuyển khoản', 'blue'), 'HD-2610-0309 · 7.000.000 ₫', '10.0.0.21'],
      ['31/10 10:02:10', 'Phạm Ngọc Mai', 'Phụ tá', badge('Xác nhận tiêu hao', 'gold'), 'Ca A007 · 4 vật tư', '10.0.0.34'],
      ['31/10 09:12:45', 'BS. Nguyễn Hoàng Long', 'Nha sĩ', badge('Chốt hồ sơ', 'gray'), 'Visit #5521', '10.0.0.40'],
      ['31/10 07:51:03', 'Lê Thu Hà', 'Lễ tân', badge('Tiếp đón', 'green'), 'BN-001029 · giấy tờ ••••2214', '10.0.0.21'],
      ['31/10 07:30:11', 'Trần Minh Quân', 'Admin', badge('Mở khóa tài khoản', 'red'), 'Đỗ Văn Khoa', '10.0.0.10']]), '', false)}`);
})();
