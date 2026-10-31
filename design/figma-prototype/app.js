// Bộ định tuyến hash + trang bìa/mục lục + Design System + 52 chức năng + modal tương tác.
(function () {
  const U = window.UI;
  const { D, badge, fn, card, field, input, select, textarea, vnd, fdiChart, fdiLegend, surfaces, table } = U;
  const S = window.SCREENS;

  const state = {
    selectedTooth: 46,
    selectedChildTooth: 84,
    imgZoom: 1,
    imgRot: 0,
    imgInv: false,
    imgCap: 'Phim quanh chóp · Răng 46 · 31/10/2026 09:22',
    mobileViewport: false
  };

  // ---------- 01. Design system page ----------
  S.unshift({ id: 'design-system', group: '01. Design System', title: 'Màu, chữ, thành phần dùng chung', codes: [], view: 'components/*', render: () => `
    <div class="pp-main" style="max-width:1280px">
      <div class="row between"><div><a class="small" href="#/cover">← Mục lục</a><h1>Design System — Nha khoa Hoàng Gia</h1><p class="muted">Lấy từ logo & banner trong frontend/public · Chuẩn hóa trong .stitch/DESIGN.md</p></div><img src="assets/logo-ngang.svg" style="height:56px"></div>
      ${card('Màu thương hiệu', `<div class="grid g4">${[['Primary 600', '#1857B6'], ['Primary 500', '#397BCC'], ['Primary 100', '#D8ECFA'], ['Primary 50', '#F4FAFF'], ['Navy', '#102E56'], ['Ink', '#18354C'], ['Gold', '#E5B95E'], ['Sky', '#A6D7EF']].map(([n, c]) => `<div class="stack" style="gap:6px"><div style="height:72px;border-radius:12px;background:${c};border:1px solid var(--line)"></div><b class="small">${n}</b><span class="small muted">${c}</span></div>`).join('')}</div>`)}
      ${card('Màu trạng thái & cảnh báo an toàn', `<div class="row">${badge('Nguy cơ cao / Dị ứng', 'red')}${badge('Bệnh nền / Chú ý', 'amber')}${badge('An toàn / Hoàn tất', 'green')}${badge('Thông tin', 'blue')}${badge('Đang khám', 'violet')}${badge('X-quang', 'gold')}${badge('Nháp', 'gray')}</div>`)}
      ${card('Typography', `<div class="stack"><div style="font-size:32px;font-weight:800;color:var(--navy)">Display 32 / ExtraBold</div><h1>Heading 1 · 24 / Bold</h1><h2>Heading 2 · 19 / Bold</h2><h3>Heading 3 · 16 / SemiBold</h3><p>Body 14 / Regular — Chăm sóc răng miệng toàn diện.</p><p class="small muted">Caption 12.5 / Regular</p></div>`)}
      ${card('Nút & form', `<div class="stack"><div class="row"><button class="btn btn-primary">Primary</button><button class="btn btn-gold">Đặt lịch ngay →</button><button class="btn btn-outline">Outline</button><button class="btn btn-ghost">Ghost</button><button class="btn btn-danger">Danger</button><button class="btn btn-success">Success</button><button class="btn btn-primary" disabled>Disabled</button><button class="btn btn-primary btn-sm">Small</button></div>
        <div class="form-grid">${field('Input mặc định', input('Placeholder'))}${field('Input lỗi', input('', 'abc', 'text', 'is-error'), { err: 'Số điện thoại không hợp lệ' })}${field('Select', select(['Tùy chọn 1', 'Tùy chọn 2']))}${field('Segmented', '<div class="seg"><button class="on">CCCD</button><button>Hộ chiếu</button></div>')}</div></div>`)}
      ${card('Thông báo', `<div class="stack"><div class="alert alert-red safety"><div class="ai">⚠️</div><div><b>SafetyAlertBanner</b> — dị ứng / bệnh nền / huyết áp</div></div><div class="alert alert-amber"><div class="ai">💰</div><div>Nhắc công nợ / cảnh báo tồn</div></div><div class="alert alert-blue"><div class="ai">ℹ️</div><div>Thông tin hướng dẫn</div></div><div class="alert alert-green"><div class="ai">✅</div><div>Thành công</div></div></div>`)}
      ${card('Sơ đồ răng giải phẫu FDI (32 răng người lớn & 5 mặt răng)', fdiChart({ selected: 36 }) + '<div style="margin-top:12px">' + fdiLegend() + '</div><div style="margin-top:12px">' + surfaces(['O']) + '</div>')}
    </div>` });

  // ---------- 01b. 52 Functions Interactive Matrix ----------
  S.unshift({ id: 'functions', group: '01. Design System', title: 'Danh mục 12 phân hệ / 52 chức năng chuẩn', codes: ['F_AUTH_01', 'F_PAT_01', 'F_FDI_01', 'F_BIL_01'], view: 'chuc_nang_Gia_demo_11.xlsx', render: () => `
    <div class="pp-main" style="max-width:1280px">
      <div class="row between"><div><a class="small" href="#/cover">← Mục lục</a><h1>Ma trận 12 Phân hệ · 52 Chức năng chuẩn</h1><p class="muted">Đối chiếu 100% với chuc_nang_Gia_demo_11.xlsx — Bấm "Mở màn hình" để thao tác trực tiếp từng chức năng</p></div><a class="btn btn-primary" href="#/cover">Về trang bìa</a></div>
      ${D.functions52.map(([modName, list]) => card(modName, table(['Mã CN', 'Tên chức năng', 'Vai trò phân quyền', 'Trạng thái mẫu', ''], list.map(([code, name, route, roles]) => [
        fn(code), `<b>${name}</b>`, badge(roles, 'blue'), badge('Đã có giao diện & tương tác', 'green'), `<a class="btn btn-outline btn-sm" href="#/${route}">Mở màn hình →</a>`
      ])), '', false)).join('')}
    </div>` });

  // ---------- 00. Cover ----------
  const groups = () => {
    const g = {};
    S.forEach((s) => { (g[s.group] = g[s.group] || []).push(s); });
    return Object.keys(g).sort().map((k) => [k, g[k]]);
  };
  const cover = () => {
    const total = S.length;
    return `<div class="cover">
      <div class="cover-hero"><div class="container">
        <img src="assets/logo-ngang-nen-toi.svg" alt="">
        <h1>Hệ thống quản lý phòng khám nha khoa Hoàng Gia</h1>
        <p style="opacity:.85;margin-top:8px;max-width:780px">Bộ giao diện mẫu tương tác đầy đủ: ${total} màn hình · 12 phân hệ · 52 chức năng chuẩn · 5 vai trò (Admin, Lễ tân/Thu ngân, Nha sĩ, Phụ tá, Bệnh nhân). Tích hợp sơ đồ răng giải phẫu FDI, đổi vai trò và giả lập khung Mobile 390px trực tiếp.</p>
        <div class="row" style="margin-top:20px">
          <a class="btn btn-gold" href="#/functions">📋 Tra cứu đủ 52 Chức năng</a>
          <a class="btn btn-outline" style="background:#fff" href="#/m-fdi?role=DENTIST">🦷 Sơ đồ răng giải phẫu FDI</a>
          <a class="btn btn-outline" style="background:#fff" href="#/home">🌐 Trang chủ công khai</a>
          <a class="btn btn-outline" style="background:#fff" href="#/m-dashboard?role=RECEPTIONIST">🏢 Khu nhân viên</a>
          <a class="btn btn-outline" style="background:#fff" href="#/p-dashboard">👤 Cổng bệnh nhân</a>
          <a class="btn btn-outline" style="background:#fff" href="#/design-system">🎨 Design System</a>
        </div>
      </div></div>
      <div class="container" style="margin-top:-32px;display:flex;flex-direction:column;gap:16px;padding-bottom:68px">
        <div class="card card-pad"><h3>Luồng cốt lõi (Bấm vào từng bước để trải nghiệm)</h3><div class="row small" style="margin-top:10px">${[['1. Đặt lịch', 'booking'], ['2. Duyệt lịch', 'm-booking-requests'], ['3. Tiếp đón CCCD', 'm-checkin'], ['4. Hàng đợi', 'm-queue'], ['5. Khám & Cảnh báo', 'm-diagnosis'], ['6. Sơ đồ răng FDI', 'm-fdi'], ['7. Chỉ định DV', 'm-fdi-services'], ['8. Ảnh X-quang', 'm-images'], ['9. Kê đơn thuốc', 'm-prescription'], ['10. Tiêu hao kho', 'm-stock-issues'], ['11. Hóa đơn', 'm-invoice-draft'], ['12. Biên lai', 'm-receipt'], ['13. Báo cáo', 'm-revenue']].map(([t, id], i) => `${i ? '<span class="muted">→</span>' : ''}<a class="badge b-blue" href="#/${id}">${t}</a>`).join('')}</div></div>
        ${groups().map(([g, items]) => `<div class="card idx-group"><div class="row between"><h2>${g}</h2>${badge(items.length + ' màn hình', 'gray')}</div>
          <div class="idx-list">${items.map((s) => `<a class="idx-item" href="#/${s.id}${s.defRole ? '?role=' + s.defRole : ''}"><b>${s.title}</b><span class="small muted">${s.view}</span><span>${fn(...s.codes.slice(0, 4))}</span></a>`).join('')}</div></div>`).join('')}
        <div class="card card-pad small muted">Dữ liệu trong bản mẫu là minh họa, phục vụ thiết kế; không phải dữ liệu thật và không thuộc mã production.</div>
      </div>
    </div>`;
  };

  // ---------- Modals ----------
  const modal = (title, body, foot) => `<div class="modal-back" onclick="if(event.target===this)APP.close()"><div class="modal"><div class="modal-h"><h3>${title}</h3><button class="x" onclick="APP.close()">×</button></div><div class="modal-b">${body}</div><div class="modal-f">${foot || '<button class="btn btn-ghost" onclick="APP.close()">Hủy</button><button class="btn btn-primary" onclick="APP.close();APP.toast(\'Đã lưu thao tác thành công\')">Xác nhận</button>'}</div></div></div>`;
  const MODALS = {
    payment: () => modal('Thu tiền mặt — HD-2610-0309 (F_BIL_03)', `<div class="row between"><span class="muted">Còn nợ</span><b class="money" style="font-size:20px;color:var(--amber)">${vnd(20100000)}</b></div>
      <div class="form-grid">${field('Số tiền thu đợt này (VND)', '<input id="pay-amount-input" class="input" value="5000000">', { req: 1, hint: 'Nguyên đồng VND, không vượt quá số còn nợ' })}${field('Tiền mặt khách đưa (VND)', input('', '5.500.000'))}${field('Tiền thừa trả lại khách', input('', '500.000 ₫'), { full: 1 })}</div>
      <label class="checkbox"><input type="checkbox" checked> Tôi xác nhận đã kiểm đếm đủ tiền mặt và ghi nhận vào sổ quỹ ca</label>`, '<button class="btn btn-ghost" onclick="APP.close()">Hủy</button><button class="btn btn-primary" onclick="APP.confirmPayment()">💵 Xác nhận thu tiền mặt & in biên lai</button>'),
    'bank-transfer': () => modal('Xác nhận chuyển khoản thủ công — HD-2610-0309 (F_BIL_03 · DL-338)', `<div class="alert alert-blue"><div class="ai">🏦</div><div class="small">Chỉ Admin / Lễ tân đang hoạt động có quyền xác nhận sau khi đối soát tiền thật đã vào tài khoản. Chống trùng mã tham chiếu giao dịch.</div></div>
      <div class="form-grid">${field('Số tiền nhận thực tế (nguyên đồng VND)', input('', '7000000'), { req: 1 })}${field('Ngân hàng thụ hưởng', select(['Vietcombank — CN Bình Dương', 'BIDV — CN Thủ Dầu Một']))}${field('Mã tham chiếu giao dịch (FT/UNC)', input('', 'FT26310998412'), { req: 1, full: 1, hint: 'Bắt buộc đối chiếu sao kê/biến động số dư thật' })}${field('Ghi chú đối soát', textarea('', 'Đã kiểm tra biến động số dư lúc 10:18'), { full: 1 })}</div>
      <label class="checkbox"><input type="checkbox" checked> Tôi xác nhận khoản chuyển khoản này là tiền thật đã vào tài khoản phòng khám</label>`, '<button class="btn btn-ghost" onclick="APP.close()">Hủy</button><button class="btn btn-primary" onclick="APP.confirmPayment()">🏦 Xác nhận đối soát & sinh biên lai</button>'),
    refund: () => modal('Đề xuất / Xác nhận hoàn tiền hóa đơn (MOD_BIL · DL-318)', `<div class="alert alert-amber"><div class="ai">⚠️</div><div class="small">Hoàn tiền không tự động cộng ngược tồn kho thuốc/vật tư (DL-318). Mọi giao dịch hoàn tiền được lưu vào Nhật ký thao tác (Audit Log).</div></div>
      <div class="form-grid">${field('Số tiền hoàn (VND)', input('', '400000'), { req: 1 })}${field('Hình thức hoàn', select(['Tiền mặt tại quầy', 'Chuyển khoản hoàn trả']))}${field('Lý do hoàn tiền', textarea('', 'Khách hàng đổi kế hoạch điều trị trước khi thực hiện thủ thuật'), { req: 1, full: 1 })}</div>`),
    discount: () => modal('Áp dụng giảm giá (F_BIL_02)', `<div class="seg"><button class="on">Theo %</button><button>Số tiền</button></div>${field('Giá trị', input('', '5'))}${field('Lý do giảm', textarea('Bắt buộc', 'Khách hàng thân thiết'), { req: 1 })}<div class="small muted">Tổng tiền không được âm. Lý do được lưu vào lịch sử hóa đơn.</div>`),
    cancel: () => modal('Hủy / điều chỉnh hóa đơn nháp (F_BIL_07)', `<div class="alert alert-amber"><div class="ai">⚠️</div><div class="small">Chỉ hủy/điều chỉnh trước khi thanh toán. Hóa đơn không bị xóa vật lý.</div></div>${field('Thao tác', select(['Điều chỉnh dòng dịch vụ', 'Hủy hóa đơn nháp']))}${field('Lý do', textarea('', 'Cập nhật lại chỉ định dịch vụ'), { req: 1 })}`),
    'approve-booking': () => modal('Duyệt yêu cầu đặt lịch trực tuyến (F_APP_01)', `<div class="form-grid">${field('Mã yêu cầu', input('', 'YC-26110014'))}${field('Khách hàng', input('', 'Võ Thị Lan · 0909 123 312'))}${field('Slot xác nhận', select(['01/11/2026 · 09:00 (còn 2 chỗ)', '01/11/2026 · 09:30 (còn 3 chỗ)']), { req: 1 })}${field('Nha sĩ dự kiến', select(['Để phòng khám điều phối khi đến', 'BS. Nguyễn Hoàng Long', 'BS. Lê Thu Trang']))}${field('Ghi chú gọi xác nhận', textarea('', 'Đã gọi khách lúc 08:30, khách xác nhận đến đúng giờ.'), { full: 1 })}</div>`),
    'reject-booking': () => modal('Từ chối yêu cầu đặt lịch (F_APP_01)', `${field('Lý do từ chối', select(['Không liên hệ được qua số điện thoại sau 3 lần gọi', 'Khách báo hủy khi gọi xác nhận', 'Trùng yêu cầu đặt lịch']))}${field('Ghi chú chi tiết', textarea('', 'Đã gọi 3 lần không nghe máy.'), { req: 1 })}`),
    reschedule: () => modal('Đổi giờ hẹn (AppointmentRescheduling · F_APP_01/02)', `<div class="form-grid">${field('Lịch hiện tại', input('', '01/11/2026 · 09:00'))}${field('Ngày hẹn mới', input('', '02/11/2026'), { req: 1 })}${field('Khung giờ mới', select(['08:30 (còn 2 chỗ)', '09:00 (còn 3 chỗ)', '14:00 (còn 4 chỗ)']), { req: 1 })}${field('Lý do đổi lịch', input('', 'Khách bận công tác đột xuất'), { req: 1 })}</div>`),
    reminder: () => modal('Nhắc lịch hẹn khách hàng (AppointmentReminderPanel)', `<div class="stack small"><div class="row between"><span>Khách hàng</span><b>Võ Thị Lan (0909 ••• 312)</b></div><div class="row between"><span>Giờ hẹn</span><b>31/10/2026 · 09:00</b></div>${field('Kênh nhắc lịch', select(['Gọi điện thoại trực tiếp', 'Gửi tin nhắn SMS/Zalo phòng khám']))}${field('Kết quả nhắc lịch', select(['Khách xác nhận sẽ đến đúng giờ', 'Khách xin dời sang khung giờ khác', 'Chưa nghe máy — hẹn gọi lại']))}</div>`),
    'cancel-appt': () => modal('Hủy lịch hẹn (PatientAppointmentCancellation · F_APP_01)', `<div class="alert alert-amber"><div class="ai">ℹ️</div><div class="small">Vui lòng cho phòng khám biết lý do hủy để hỗ trợ sắp xếp lại lịch khi bạn cần.</div></div>${field('Lý do hủy lịch', textarea('', 'Bận việc gia đình, sẽ đặt lại vào tuần sau'), { req: 1 })}`),
    'phone-otp': () => modal('Đổi số điện thoại tài khoản kèm OTP (F_AUTH_03)', `<div class="steps"><span class="on"></span><span class="on"></span></div><div class="form-grid">${field('Số điện thoại hiện tại', input('', '0909 ••• 312'))}${field('Số điện thoại mới', input('09xx xxx xxx', '0912 345 678'), { req: 1 })}</div><div class="field"><label>Mã OTP 6 số gửi tới số điện thoại mới</label><div class="otp">${'619042'.split('').map((d) => `<input value="${d}" maxlength="1">`).join('')}</div></div><div class="small muted">Số điện thoại liên hệ có thể dùng chung giữa người giám hộ và trẻ em.</div>`),
    'staff-phone-change': () => modal('Nhân viên hỗ trợ đổi SĐT cho bệnh nhân (StaffPhoneChange)', `<div class="alert alert-blue"><div class="ai">📱</div><div class="small">Yêu cầu đối chiếu giấy tờ định danh của bệnh nhân trước khi gửi mã xác thực OTP về số điện thoại mới.</div></div><div class="form-grid">${field('Bệnh nhân', input('', 'Võ Thị Lan — BN-001024'))}${field('Số điện thoại mới', input('', '0912 345 678'), { req: 1 })}${field('Mã OTP bệnh nhân đọc tại quầy', input('6 chữ số', '839201'), { req: 1 })}${field('Lý do thay đổi', input('', 'Khách đổi SIM điện thoại mới'))}</div>`),
    'img-canvas': () => modal('Vẽ & đo đạc trên phim X-quang (DentalImageCanvas · F_IMG_04)', `<div class="row" style="margin-bottom:8px"><button class="btn btn-primary btn-sm">✏️ Bút vẽ vùng tổn thương</button><button class="btn btn-outline btn-sm">📏 Thước đo chiều dài ống tủy</button><button class="btn btn-outline btn-sm">⭕ Khoanh vùng chóp</button><button class="btn btn-ghost btn-sm">↺ Hoàn tác</button></div><div class="viewer" style="aspect-ratio:16/9"><div style="text-align:center"><div style="font-size:64px">🩻</div><div style="border:2px dashed #EF4444;padding:8px 16px;border-radius:999px;display:inline-block;margin-top:8px;color:#FCA5A5">Vùng thấu quang quanh chóp R46 · d = 2.5 mm · Chiều dài ống tủy ước tính 21.0 mm</div></div></div>`),
    'img-meta': () => modal('Sửa thông tin & phân loại ảnh (DentalImageMetadataEditor · F_IMG_02)', `<div class="form-grid">${field('Răng FDI gắn kèm', input('', '46'), { req: 1 })}${field('Loại hình ảnh', select(['X-quang quanh chóp (Periapical)', 'Toàn cảnh (Panorex)', 'Ảnh chụp trong miệng trước điều trị', 'Ảnh chụp trong miệng sau điều trị']))}${field('Ngày chụp', input('', '31/10/2026 09:22'))}${field('Người thực hiện', input('', 'Phạm Ngọc Mai (Phụ tá)'))}</div>`),
    'img-delete': () => modal('Xóa ảnh có kiểm soát (DentalImageDeletion · F_IMG_02)', `<div class="alert alert-red"><div class="ai">⚠️</div><div class="small">Chỉ cho phép xóa khi ảnh tải nhầm hồ sơ/răng và lần khám chưa khóa. Thao tác được ghi nhận lịch sử.</div></div>${field('Lý do xóa ảnh', textarea('', 'Ảnh chụp bị mờ do bệnh nhân cử động, đã chụp lại phim mới'), { req: 1 })}`),
    call: () => modal('Gọi số A012 vào phòng (F_CHK_03)', `${field('Nha sĩ đang làm việc', select(['BS. Nguyễn Hoàng Long — Phòng 01', 'BS. Lê Thu Trang — Phòng 02']))}${field('Ghế', select(['Ghế 2 (sẵn sàng)', 'Ghế 1 (đang điều trị)']))}<div class="small muted">Gọi vào phòng cần nha sĩ đang làm việc và phòng hợp lệ theo ngày. Lễ tân hoặc Phụ tá đều có thể gọi số.</div>`, '<button class="btn btn-ghost" onclick="APP.close()">Hủy</button><button class="btn btn-primary" onclick="APP.callNextQueue()">📢 Gọi & hiển thị lên TV</button>'),
    xray: () => modal('Điều phối sang X-quang (F_CHK_08)', `${field('Chỉ định chụp đã ghi nhận', select(['Phim quanh chóp răng 46', 'Chụp phim toàn cảnh (Panorex)']))}${field('Ghi chú cho KTV', textarea('', 'Chụp kiểm tra vùng chóp chân xa răng 46'))}<div class="small muted">Sau khi chụp xong, lượt khám quay lại nha sĩ.</div>`),
    'qr-scan': () => modal('Quét mã QR CCCD tại quầy (F_PAT_01, F_CHK_01)', `<div class="viewer" style="aspect-ratio:16/9;background:#0F172A;color:#fff"><div style="text-align:center"><div style="font-size:48px">📷</div><div>Đặt mặt trước thẻ CCCD gắn chip vào khung quét</div><div class="small" style="color:var(--gold);margin-top:6px">Đã nhận diện: 079190004521 · Võ Thị Lan · 12/04/1990</div></div></div><div class="alert alert-blue"><div class="ai">🔐</div><div class="small">Số CCCD được gửi trong body POST có JWT; máy chủ băm HMAC phiên bản khóa 1 và chỉ lưu số che <code>••••••••4521</code>.</div></div>`, '<button class="btn btn-ghost" onclick="APP.close()">Đóng</button><button class="btn btn-primary" onclick="APP.close();APP.toast(\'Đã điền thông tin từ QR CCCD & đối soát HMAC thành công\')">Sử dụng thông tin này</button>'),
    guardian: () => modal('Quan hệ người giám hộ & trẻ em (DL-367)', `${table(['Trẻ được giám hộ', 'Mã hồ sơ', 'Người giám hộ', 'Quan hệ', 'Trạng thái'], [['Trần Gia Bảo (2017)', 'BN-001026', 'Võ Thị Lan (BN-001024)', 'Mẹ ruột', badge('Đang hiệu lực', 'green')]])}<div class="form-grid" style="margin-top:10px">${field('Thêm trẻ được giám hộ', input('Nhập tên hoặc mã hồ sơ trẻ'))}${field('Quan hệ đại diện', select(['Mẹ ruột', 'Cha ruột', 'Người giám hộ hợp pháp']))}</div><div class="small muted">Không thay đổi chủ sở hữu hồ sơ của trẻ. Thu hồi quyền có hiệu lực từ yêu cầu tiếp theo.</div>`),
    'new-visit': () => modal('Tạo bản ghi khám (Visit) mới (F_PAT_09)', `<div class="form-grid">${field('Bệnh nhân', input('', 'Võ Thị Lan — BN-001024'), { full: 1 })}${field('Nha sĩ phụ trách', select(['BS. Nguyễn Hoàng Long — Phòng 01', 'BS. Lê Thu Trang — Phòng 02']))}${field('Phòng / Ghế', select(['Phòng 01 · Ghế 1', 'Phòng 01 · Ghế 2']))}${field('Lý do đến khám', textarea('', 'Đau nhức răng hàm dưới phải'), { full: 1 })}</div><div class="small muted">Mỗi lần đến tạo một Visit riêng biệt, không ghi đè Visit cũ.</div>`),
    'med-history': () => modal('Thêm bản ghi tiền sử & dị ứng (F_PAT_04)', `<div class="form-grid">${field('Phân loại', select(['Dị ứng thuốc', 'Bệnh nền tim mạch / huyết áp', 'Tiểu đường / nội tiết', 'Đang mang thai', 'Thuốc đang sử dụng']))}${field('Mức độ cảnh báo', select(['Nguy cơ cao (Đỏ)', 'Chú ý (Vàng)', 'Thông tin (Xanh)']))}${field('Nội dung chi tiết', textarea('', 'Dị ứng kháng sinh nhóm Penicillin (phát ban, khó thở).'), { full: 1, req: 1 })}</div><div class="small muted">Ghi theo kiểu bản ghi mới (append-only), tự động đồng bộ lên SafetyAlertBanner.</div>`),
    vitals: () => modal('Ghi nhận sinh hiệu lần khám (F_PAT_05)', `<div class="form-grid">${field('Huyết áp tâm thu (mmHg)', `<input id="v-sys" class="input" value="${D.vitals.sys}">`, { req: 1 })}${field('Huyết áp tâm trương (mmHg)', `<input id="v-dia" class="input" value="${D.vitals.dia}">`, { req: 1 })}${field('Mạch (lần/phút)', `<input id="v-pulse" class="input" value="${D.vitals.pulse}">`)}${field('Nhiệt độ (°C)', `<input id="v-temp" class="input" value="${D.vitals.temp}">`)}${field('SpO₂ (%)', `<input id="v-spo2" class="input" value="${D.vitals.spo2}">`)}</div><div class="small muted">Nếu huyết áp ≥ 140/90 mmHg, hệ thống tự động bật cảnh báo trước khi tiêm tê/làm thủ thuật.</div>`, '<button class="btn btn-ghost" onclick="APP.close()">Hủy</button><button class="btn btn-primary" onclick="APP.saveVitals()">Lưu sinh hiệu</button>'),
    'print-rx': () => modal('Bản in đơn thuốc điện tử có mã QR (F_RX_04)', `<div class="paper"><div class="paper-head"><img src="assets/logo-ngang.svg" alt=""><div class="right small">[ĐIỀN ĐỊA CHỈ]<br>ĐT: [ĐIỀN SĐT]</div></div><div class="row between"><div><h2>ĐƠN THUỐC</h2><div class="small muted">Mã đơn: RX-2610-8F3K2 · 31/10/2026</div></div><div style="border:2px solid var(--navy);padding:6px 10px;border-radius:8px;font-family:monospace;font-size:11px;text-align:center">[QR CODE]<br>RX-2610</div></div><div class="small" style="margin:10px 0"><b>Họ tên:</b> Võ Thị Lan · <b>Năm sinh:</b> 1990 · <b>Chẩn đoán:</b> K04.0 Viêm tủy răng 46</div>${table(['Thuốc', 'SL', 'Cách dùng'], D.rxItems.map((i) => [i.name, i.qty, i.usage]))}<div class="row between small" style="margin-top:24px"><div><b>Lời dặn:</b> Tái khám sau 7 ngày.</div><div style="text-align:center">Nha sĩ khám bệnh<br><span style="font-family:cursive;font-size:24px;color:var(--navy)">Hoàng Long</span><br><b>BS. Nguyễn Hoàng Long</b></div></div></div>`, '<button class="btn btn-ghost" onclick="APP.close()">Đóng</button><button class="btn btn-primary" onclick="APP.close();APP.toast(\'Đã gửi lệnh in đơn thuốc A5\')">🖨 In đơn thuốc</button>'),
    'new-patient': () => modal('Tạo hồ sơ bệnh nhân (F_PAT_01)', `<div class="form-grid">${field('Họ tên', input(), { req: 1 })}${field('Ngày sinh', input('dd/mm/yyyy'), { req: 1 })}${field('Giới tính', select(['Nữ', 'Nam']))}${field('Số điện thoại', input(), { req: 1 })}${field('Loại giấy tờ', select(['CCCD', 'Hộ chiếu', 'Trẻ em — không có']))}${field('Số giấy tờ', input())}</div><div class="alert alert-blue"><div class="ai">🔎</div><div class="small">Hệ thống kiểm tra trùng hồ sơ theo giấy tờ (HMAC) trước khi lưu.</div></div>`),
    lock: () => modal('Chốt hồ sơ lần khám (F_PAT_08)', `<div class="alert alert-amber"><div class="ai">🔒</div><div class="small">Sau khi chốt, chỉ Nha sĩ/Admin được điều chỉnh và mọi chỉnh sửa đặc biệt đều lưu lịch sử.</div></div>${field('Ghi chú chốt hồ sơ', textarea('', 'Đã hoàn tất khám và kê đơn lần 1.'))}`),
    'tooth-history': () => {
      const t = state.selectedTooth || 46;
      const hist = D.toothHistory[t] || [{ date: '20/06/2026 10:00', cond: 'Khỏe mạnh', surf: 'Toàn bộ', note: 'Khám định kỳ', doctor: 'BS. Hoàng Long' }];
      return modal(`Lịch sử trạng thái răng ${t} (F_FDI_06)`, `<div class="timeline small">${hist.map((h) => `<div class="tl-item"><b>${h.date}</b> · <b>${h.cond}</b> (${h.surf}) · ${h.doctor}<div class="muted">${h.note}</div></div>`).join('')}</div>`, '<button class="btn btn-primary" onclick="APP.close()">Đóng</button>');
    },
    upload: () => modal('Tải ảnh lên (F_IMG_02)', `<div class="viewer" style="background:#fff;border:2px dashed var(--border);color:var(--muted);aspect-ratio:3/1">Kéo thả ảnh JPG/PNG (không DICOM)</div><div class="form-grid">${field('Gắn với răng', input('', '46'))}${field('Loại ảnh', select(['X-quang quanh chóp', 'Toàn cảnh Panorex', 'Ảnh trong miệng']))}${field('Ghi chú', textarea(), { full: 1 })}</div>`),
    handover: () => modal('Xác nhận giao thuốc (F_RX_02)', `<div class="alert alert-green"><div class="ai">✅</div><div class="small">Tất cả thuốc trong đơn đều đủ tồn kho. Xác nhận giao sẽ trừ tồn thuốc cùng giao dịch (transaction) với nhật ký thao tác (Audit Log).</div></div>`, '<button class="btn btn-ghost" onclick="APP.close()">Đóng</button><button class="btn btn-primary" onclick="APP.close();APP.toast(\'Đã xác nhận bàn giao thuốc & trừ kho hợp lệ\')">✓ Xác nhận giao thuốc</button>'),
    'confirm-issue': () => modal('Xác nhận trừ kho ca A009 (F_INV_06)', `<div class="small">Bạn xác nhận số lượng thực tế đã dùng:</div><ul class="small"><li>Lidocaine 2%: 2 ống</li><li>Bông gạc: 3 gói</li><li>Kim tiêm 27G: 2 cái</li></ul><div class="small muted">Hệ thống sinh phiếu xuất (StockIssue), trừ tồn và ghi audit với người xác nhận: Phạm Ngọc Mai.</div>`),
    receipt: () => modal('Lập phiếu nhập kho (F_INV_03)', `<div class="form-grid">${field('Nhà cung cấp', input('', 'Cty Dược Nha Việt'))}${field('Ngày nhập', input('', '31/10/2026'))}</div>${U.table(['Vật tư', 'Đơn vị', 'Số lượng', 'Đơn giá'], [['Lidocaine 2%', 'Ống', '<input class="input" value="100">', '<input class="input" value="12.000">'], ['Composite A2', 'Tuýp', '<input class="input" value="10">', '<input class="input" value="350.000">']])}<div class="small muted">Số lượng > 0, bắt buộc đơn vị tính. Cộng tồn sau khi người có quyền xác nhận.</div>`),
    'new-staff': () => modal('Thêm nhân viên (F_MST_07)', `<div class="form-grid">${field('Họ tên', input(), { req: 1 })}${field('Số điện thoại', input(), { req: 1 })}${field('Vai trò', select(['Nha sĩ', 'Lễ tân / Thu ngân', 'Phụ tá', 'Quản trị viên']))}${field('Email', input())}</div>`),
    catalog: () => modal('Thêm mục danh mục (MOD_MST)', `<div class="form-grid">${field('Mã', input(), { req: 1 })}${field('Tên', input(), { req: 1 })}${field('Nhóm / đơn vị', input())}${field('Giá trị / ngưỡng', input())}</div>`)
  };

  // ---------- Router & Toolbar ----------
  const app = document.getElementById('app');
  const modalRoot = document.getElementById('modal-root');

  const protoBar = (curRole) => `
    <div class="proto-bar">
      <a class="gold-pill" href="#/functions">📋 52 Chức năng</a>
      <a href="#/cover">☰ Mục lục</a>
      <select onchange="APP.switchRole(this.value)" title="Đổi vai trò trải nghiệm">
        ${[['ADMIN', 'Vai trò: Admin'], ['RECEPTIONIST', 'Vai trò: Lễ tân/Thu ngân'], ['DENTIST', 'Vai trò: Nha sĩ'], ['ASSISTANT', 'Vai trò: Phụ tá'], ['PATIENT', 'Vai trò: Bệnh nhân']].map(([k, lbl]) => `<option value="${k}" ${curRole === k ? 'selected' : ''}>${lbl}</option>`).join('')}
      </select>
      <button onclick="APP.toggleViewport()">${state.mobileViewport ? '💻 Khung Desktop 1440px' : '📱 Khung Mobile 390px'}</button>
    </div>`;

  const render = () => {
    const raw = location.hash.replace(/^#\/?/, '') || 'cover';
    const [id, qs] = raw.split('?');
    const params = new URLSearchParams(qs || '');
    const role = params.get('role');
    modalRoot.innerHTML = '';
    if (id === 'cover') {
      app.innerHTML = cover() + protoBar(role || 'ADMIN');
    } else {
      const s = S.find((x) => x.id === id) || S.find((x) => x.id === 'not-found');
      const effRole = role || s.defRole || (id.startsWith('p-') ? 'PATIENT' : 'ADMIN');
      app.innerHTML = s.render(role) + (id === 'tv' ? '' : protoBar(effRole));
      document.title = s.title + ' — Nha khoa Hoàng Gia';
    }
    // FDI: chọn răng tương tác
    app.querySelectorAll('.tooth').forEach((t) => t.addEventListener('click', () => {
      const num = Number(t.dataset.tooth);
      if (num >= 51) state.selectedChildTooth = num;
      else state.selectedTooth = num;
      render();
    }));
    // Segmented controls & surface buttons
    app.querySelectorAll('.seg').forEach((seg) => seg.querySelectorAll('button').forEach((b) => b.addEventListener('click', () => {
      if (b.getAttribute('onclick')) return;
      seg.querySelectorAll('button').forEach((x) => x.classList.remove('on')); b.classList.add('on');
    })));
    app.querySelectorAll('.surf-btn').forEach((b) => b.addEventListener('click', () => {
      b.classList.toggle('btn-primary');
      b.classList.toggle('btn-outline');
    }));
  };

  window.APP = {
    state,
    modal: (k) => { modalRoot.innerHTML = MODALS[k] ? MODALS[k]() : ''; },
    close: () => { modalRoot.innerHTML = ''; },
    saveToothDirect: (code) => {
      const t = state.selectedTooth || 46;
      D.toothState[t] = code;
      const condName = (D.conditions.find((c) => c.code === code) || { name: code }).name;
      render();
      window.APP.toast(`Đã đổi hình thái răng ${t} sang "${condName}"`);
    },
    toast: (msg) => {
      const el = document.createElement('div');
      el.className = 'toast-box';
      el.innerHTML = `<span>✅</span><span>${msg}</span>`;
      document.body.appendChild(el);
      setTimeout(() => el.remove(), 2600);
    },
    toggleViewport: () => {
      state.mobileViewport = !state.mobileViewport;
      document.body.classList.toggle('vp-mobile', state.mobileViewport);
      render();
    },
    switchRole: (role) => {
      if (role === 'PATIENT') { location.hash = '#/p-dashboard'; return; }
      const raw = location.hash.replace(/^#\/?/, '') || 'm-dashboard';
      const [id] = raw.split('?');
      const target = id.startsWith('m-') ? id : 'm-dashboard';
      location.hash = `#/${target}?role=${role}`;
    },
    saveTooth: (isChild) => {
      const selEl = document.getElementById('fdi-cond-select');
      const noteEl = document.getElementById('fdi-note-input');
      const code = selEl ? selEl.value : 'CAR';
      const note = noteEl ? noteEl.value : '';
      const t = isChild ? state.selectedChildTooth : state.selectedTooth;
      if (isChild) D.childToothState[t] = code;
      else D.toothState[t] = code;
      const condName = (D.conditions.find((c) => c.code === code) || { name: code }).name;
      D.toothHistory[t] = D.toothHistory[t] || [];
      D.toothHistory[t].unshift({ date: '31/10/2026 09:35', cond: condName, surf: 'O, D', note: note || 'Cập nhật trên sơ đồ FDI', doctor: 'BS. Hoàng Long' });
      render();
      window.APP.toast(`Đã lưu tình trạng "${condName}" cho răng ${t} (bản ghi mới)`);
    },
    addService: () => {
      const toothEl = document.getElementById('svc-tooth-input');
      const idxEl = document.getElementById('svc-code-select');
      const tooth = (toothEl && toothEl.value) || '46';
      const svc = D.services[(idxEl && Number(idxEl.value)) || 0] || D.services[0];
      D.toothServices.unshift({ id: Date.now(), tooth, surf: 'O', name: svc.name, price: svc.price, status: 'in-exam' });
      render();
      window.APP.toast(`Đã chỉ định "${svc.name}" cho răng ${tooth} (giá snapshot ${vnd(svc.price)})`);
    },
    completeService: (id) => {
      const item = D.toothServices.find((x) => x.id === id);
      if (item) {
        item.status = 'done';
        if (item.tooth && item.tooth !== '—') D.toothState[Number(item.tooth)] = 'RCT';
      }
      render();
      window.APP.toast('Đã hoàn tất dịch vụ & tự động cập nhật trạng thái răng FDI (F_FDI_05)');
    },
    addRx: () => {
      const medEl = document.getElementById('rx-med-select');
      const useEl = document.getElementById('rx-usage-input');
      const name = (medEl && medEl.value) || 'Paracetamol 500mg';
      const usage = (useEl && useEl.value) || '10 viên · Uống sau ăn';
      if (name.includes('Amoxicillin')) {
        window.APP.toast('⚠️ Chặn kê đơn: Bệnh nhân dị ứng kháng sinh nhóm Penicillin (Amoxicillin)!');
        return;
      }
      D.rxItems.push({ name, qty: '10 viên', usage, stockStatus: 'green', stockText: 'Đủ' });
      render();
      window.APP.toast(`Đã thêm "${name}" vào đơn thuốc`);
    },
    removeRx: (idx) => {
      D.rxItems.splice(idx, 1);
      render();
    },
    cycleChair: (idx) => {
      const order = ['ready', 'busy', 'clean'];
      const cur = D.chairs[idx].status;
      const next = order[(order.indexOf(cur) + 1) % order.length] || 'ready';
      D.chairs[idx].status = next;
      render();
      window.APP.toast(`Đã cập nhật trạng thái ${D.chairs[idx].room} · ${D.chairs[idx].chair}`);
    },
    imgOp: (op, val) => {
      if (op === 'zoom') state.imgZoom = Math.min(2.2, Math.max(0.6, +(state.imgZoom + val).toFixed(2)));
      else if (op === 'rot') state.imgRot = (state.imgRot + val) % 360;
      else if (op === 'inv') state.imgInv = !state.imgInv;
      else if (op === 'reset') { state.imgZoom = 1; state.imgRot = 0; state.imgInv = false; }
      else if (op === 'select') state.imgCap = val + ' · 31/10/2026';
      render();
    },
    saveVitals: () => {
      const s = Number((document.getElementById('v-sys') || {}).value) || 158;
      const d = Number((document.getElementById('v-dia') || {}).value) || 96;
      const p = Number((document.getElementById('v-pulse') || {}).value) || 88;
      D.vitals.sys = s; D.vitals.dia = d; D.vitals.pulse = p;
      window.APP.close();
      render();
      window.APP.toast(`Đã ghi nhận sinh hiệu mới: HA ${s}/${d} mmHg, Mạch ${p} l/ph`);
    },
    confirmCheckIn: () => {
      D.queue.unshift({ no: 'A014', name: 'Võ T*** Lan', full: 'Võ Thị Lan', status: 'waiting', since: '09:36', reason: 'Đau răng hàm dưới', room: '—' });
      location.hash = '#/m-queue?role=RECEPTIONIST';
      window.APP.toast('Đã tiếp đón Võ Thị Lan (STT A014) và đưa vào hàng đợi chung');
    },
    callNextQueue: () => {
      const w = D.queue.find((q) => q.status === 'waiting');
      if (w) { w.status = 'called'; w.room = 'P.01 · Ghế 2'; }
      window.APP.close();
      render();
      window.APP.toast('Đã gọi số lên màn hình TV phòng chờ');
    },
    finishExam: (role) => {
      location.hash = `#/m-invoice-draft?role=${role || 'DENTIST'}`;
      window.APP.toast('Đã kết thúc ca khám & tự động tổng hợp Hóa đơn nháp (F_BIL_01)');
    },
    confirmPayment: () => {
      window.APP.close();
      location.hash = '#/m-receipt?role=RECEPTIONIST';
      window.APP.toast('Đã xác nhận thu tiền & sinh biên lai BL-2610-0572');
    }
  };
  window.addEventListener('hashchange', () => { render(); window.scrollTo(0, 0); });
  render();
})();
