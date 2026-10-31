// Dữ liệu minh họa cho giao diện mẫu Figma — KHÔNG dùng trong mã production.
window.DEMO = {
  clinic: { name: 'Phòng khám Nha khoa Hoàng Gia', phone: '0274 3xx xxx', address: 'Thủ Dầu Một, Bình Dương' },
  roles: {
    ADMIN: { label: 'Quản trị viên', user: 'Trần Minh Quân', init: 'MQ' },
    RECEPTIONIST: { label: 'Lễ tân / Thu ngân', user: 'Lê Thu Hà', init: 'TH' },
    DENTIST: { label: 'Nha sĩ', user: 'BS. Nguyễn Hoàng Long', init: 'HL' },
    ASSISTANT: { label: 'Phụ tá', user: 'Phạm Ngọc Mai', init: 'NM' },
    PATIENT: { label: 'Bệnh nhân', user: 'Võ Thị Lan', init: 'TL' }
  },
  patients: [
    { id: 1024, code: 'BN-001024', name: 'Võ Thị Lan', dob: '12/04/1990', gender: 'Nữ', phone: '0909 ••• 312', doc: '••••••••4521', alert: 'red', note: 'Dị ứng Penicillin · Tăng huyết áp' },
    { id: 1025, code: 'BN-001025', name: 'Nguyễn Văn Bình', dob: '03/09/1978', gender: 'Nam', phone: '0912 ••• 774', doc: '••••••••0198', alert: 'amber', note: 'Tiểu đường type 2' },
    { id: 1026, code: 'BN-001026', name: 'Trần Gia Bảo', dob: '21/06/2017', gender: 'Nam', phone: '0909 ••• 312', doc: 'Trẻ em (giám hộ)', alert: 'none', note: '—' },
    { id: 1027, code: 'BN-001027', name: 'Lâm Quốc Huy', dob: '30/01/1995', gender: 'Nam', phone: '0938 ••• 555', doc: 'P••••7781 (KR)', alert: 'none', note: '—' },
    { id: 1028, code: 'BN-001028', name: 'Đặng Mỹ Linh', dob: '15/11/1988', gender: 'Nữ', phone: '0977 ••• 120', doc: '••••••••7730', alert: 'amber', note: 'Đang mang thai 20 tuần' },
    { id: 1029, code: 'BN-001029', name: 'Phan Thanh Tùng', dob: '08/08/1969', gender: 'Nam', phone: '0903 ••• 889', doc: '••••••••2214', alert: 'red', note: 'Bệnh tim mạch · dùng thuốc chống đông' }
  ],
  queue: [
    { no: 'A012', name: 'Võ T*** Lan', full: 'Võ Thị Lan', status: 'waiting', since: '08:42', reason: 'Đau răng hàm dưới', room: '—' },
    { no: 'A013', name: 'Nguyễn V*** Bình', full: 'Nguyễn Văn Bình', status: 'waiting', since: '08:51', reason: 'Tái khám niềng', room: '—' },
    { no: 'A010', name: 'Đặng M*** Linh', full: 'Đặng Mỹ Linh', status: 'called', since: '08:20', reason: 'Lấy cao răng', room: 'P.02 · Ghế 3' },
    { no: 'A009', name: 'Phan T*** Tùng', full: 'Phan Thanh Tùng', status: 'in-exam', since: '08:05', reason: 'Nhổ răng 38', room: 'P.01 · Ghế 1' },
    { no: 'A011', name: 'Lâm Q*** Huy', full: 'Lâm Quốc Huy', status: 'xray', since: '08:31', reason: 'Chụp phim toàn cảnh', room: 'X-quang' },
    { no: 'A007', name: 'Trần G*** Bảo', full: 'Trần Gia Bảo', status: 'done', since: '07:45', reason: 'Trám răng sữa 75', room: 'P.02 · Ghế 2' }
  ],
  services: [
    { code: 'DV-KHAM', name: 'Khám và tư vấn', group: 'Khám', price: 100000, unit: 'Lần' },
    { code: 'DV-CAO', name: 'Lấy cao răng + đánh bóng', group: 'Nha chu', price: 300000, unit: 'Hai hàm' },
    { code: 'DV-TRAM', name: 'Trám răng Composite', group: 'Phục hồi', price: 400000, unit: 'Răng' },
    { code: 'DV-TUY', name: 'Điều trị tủy răng hàm', group: 'Nội nha', price: 1800000, unit: 'Răng' },
    { code: 'DV-NHO', name: 'Nhổ răng khôn tiểu phẫu', group: 'Phẫu thuật', price: 2500000, unit: 'Răng' },
    { code: 'DV-SU', name: 'Mão sứ Zirconia', group: 'Phục hình', price: 5500000, unit: 'Răng' },
    { code: 'DV-NIENG', name: 'Niềng răng mắc cài kim loại', group: 'Chỉnh nha', price: 32000000, unit: 'Liệu trình' },
    { code: 'DV-XQ', name: 'Chụp phim toàn cảnh (Panorex)', group: 'Cận lâm sàng', price: 150000, unit: 'Phim' }
  ],
  conditions: [
    { code: 'CAR', name: 'Sâu răng', color: '#EF4444' },
    { code: 'FIL', name: 'Đã trám', color: '#3B82F6' },
    { code: 'RCT', name: 'Điều trị tủy', color: '#8B5CF6' },
    { code: 'CRN', name: 'Mão / sứ', color: '#E5B95E' },
    { code: 'MIS', name: 'Mất răng / đã nhổ', color: '#64748B' },
    { code: 'IMP', name: 'Cấy ghép trụ răng', color: '#0EA5E9' },
    { code: 'HEA', name: 'Khỏe mạnh', color: '#FFFFFF' }
  ],
  toothState: { 16: 'CAR', 26: 'FIL', 36: 'RCT', 46: 'CRN', 38: 'MIS', 18: 'MIS', 24: 'CAR', 11: 'FIL', 47: 'IMP' },
  childToothState: { 75: 'FIL', 84: 'CAR', 54: 'CAR' },
  medicines: [
    { code: 'TH-AMOX', name: 'Amoxicillin 500mg', unit: 'Viên', stock: 420, min: 100 },
    { code: 'TH-PARA', name: 'Paracetamol 500mg', unit: 'Viên', stock: 860, min: 200 },
    { code: 'TH-IBU', name: 'Ibuprofen 400mg', unit: 'Viên', stock: 64, min: 100 },
    { code: 'TH-METRO', name: 'Metronidazole 250mg', unit: 'Viên', stock: 0, min: 80 },
    { code: 'TH-CHLO', name: 'Nước súc miệng Chlorhexidine 0.12%', unit: 'Chai', stock: 35, min: 20 }
  ],
  supplies: [
    { code: 'VT-GANG', name: 'Găng tay y tế (hộp 100)', unit: 'Hộp', stock: 18, min: 10 },
    { code: 'VT-TE', name: 'Thuốc tê Lidocaine 2% ống 1.8ml', unit: 'Ống', stock: 42, min: 50 },
    { code: 'VT-COMP', name: 'Composite A2 (tuýp 4g)', unit: 'Tuýp', stock: 9, min: 5 },
    { code: 'VT-GAC', name: 'Bông gạc vô trùng', unit: 'Gói', stock: 120, min: 40 },
    { code: 'VT-KIM', name: 'Kim tiêm nha khoa 27G', unit: 'Cái', stock: 230, min: 100 }
  ],
  chairs: [
    { room: 'Phòng 01', chair: 'Ghế 1', status: 'busy', dentist: 'BS. Hoàng Long', patient: 'Phan Thanh Tùng', since: '08:05' },
    { room: 'Phòng 01', chair: 'Ghế 2', status: 'ready', dentist: 'BS. Hoàng Long', patient: '—', since: '' },
    { room: 'Phòng 02', chair: 'Ghế 3', status: 'busy', dentist: 'BS. Thu Trang', patient: 'Đặng Mỹ Linh', since: '08:20' },
    { room: 'Phòng 02', chair: 'Ghế 4', status: 'clean', dentist: 'BS. Thu Trang', patient: '—', since: '08:40' },
    { room: 'Phòng 03', chair: 'Ghế 5', status: 'off', dentist: '—', patient: '—', since: '' },
    { room: 'X-quang', chair: 'Máy Panorex', status: 'busy', dentist: 'KTV. Minh', patient: 'Lâm Quốc Huy', since: '08:31' }
  ],
  invoices: [
    { no: 'HD-2610-0312', patient: 'Phan Thanh Tùng', date: '31/10/2026', total: 2650000, paid: 0, status: 'draft' },
    { no: 'HD-2610-0309', patient: 'Võ Thị Lan', date: '30/10/2026', total: 32100000, paid: 12000000, status: 'partial' },
    { no: 'HD-2610-0301', patient: 'Đặng Mỹ Linh', date: '29/10/2026', total: 400000, paid: 400000, status: 'paid' },
    { no: 'HD-2610-0295', patient: 'Nguyễn Văn Bình', date: '28/10/2026', total: 5600000, paid: 5600000, status: 'paid' },
    { no: 'HD-2610-0288', patient: 'Lâm Quốc Huy', date: '27/10/2026', total: 1800000, paid: 0, status: 'cancelled' }
  ],
  staff: [
    { name: 'Trần Minh Quân', role: 'ADMIN', phone: '0901 ••• 001', active: true },
    { name: 'BS. Nguyễn Hoàng Long', role: 'DENTIST', phone: '0901 ••• 002', active: true },
    { name: 'BS. Lê Thu Trang', role: 'DENTIST', phone: '0901 ••• 003', active: true },
    { name: 'Lê Thu Hà', role: 'RECEPTIONIST', phone: '0901 ••• 004', active: true },
    { name: 'Phạm Ngọc Mai', role: 'ASSISTANT', phone: '0901 ••• 005', active: true },
    { name: 'Đỗ Văn Khoa', role: 'ASSISTANT', phone: '0901 ••• 006', active: false }
  ],
  appointments: [
    { time: '08:00', patient: 'Trần Gia Bảo', service: 'Trám răng sữa', channel: 'Trực tuyến', status: 'checked-in' },
    { time: '08:30', patient: 'Phan Thanh Tùng', service: 'Nhổ răng khôn', channel: 'Tại quầy', status: 'checked-in' },
    { time: '09:00', patient: 'Võ Thị Lan', service: 'Khám đau răng', channel: 'Trực tuyến', status: 'checked-in' },
    { time: '09:30', patient: 'Hồ Thị Ngân', service: 'Tư vấn niềng răng', channel: 'Trực tuyến', status: 'confirmed' },
    { time: '10:00', patient: 'Bùi Anh Tuấn', service: 'Lấy cao răng', channel: 'Tại quầy', status: 'confirmed' },
    { time: '14:00', patient: 'Kiều Minh Châu', service: 'Tẩy trắng', channel: 'Trực tuyến', status: 'cancelled' }
  ],
  vitals: { sys: 158, dia: 96, pulse: 88, temp: '36,8', spo2: 98, time: '08:47' },
  toothHistory: {
    46: [
      { date: '31/10/2026 09:10', cond: 'Mão / sứ', surf: 'O, D', note: 'Sâu mặt nhai – xa, chỉ định điều trị tủy & bọc sứ', doctor: 'BS. Hoàng Long' },
      { date: '20/06/2026 10:00', cond: 'Khỏe mạnh', surf: 'Toàn bộ', note: 'Khám định kỳ bình thường', doctor: 'BS. Hoàng Long' }
    ],
    36: [
      { date: '30/10/2026 09:30', cond: 'Điều trị tủy', surf: 'O, M', note: 'Mở tủy lần 1, đặt thuốc Ca(OH)2', doctor: 'BS. Hoàng Long' }
    ],
    16: [
      { date: '31/10/2026 09:15', cond: 'Sâu răng', surf: 'O', note: 'Sâu ngà nông mặt nhai', doctor: 'BS. Hoàng Long' }
    ]
  },
  toothServices: [
    { id: 1, tooth: '46', surf: 'O, D', name: 'Điều trị tủy răng hàm', price: 1800000, status: 'in-exam' },
    { id: 2, tooth: '46', surf: '—', name: 'Mão sứ Zirconia', price: 5500000, status: 'pending' },
    { id: 3, tooth: '16', surf: 'O', name: 'Trám răng Composite', price: 400000, status: 'done' },
    { id: 4, tooth: '—', surf: '—', name: 'Chụp phim toàn cảnh (Panorex)', price: 150000, status: 'done' }
  ],
  rxItems: [
    { name: 'Clindamycin 300mg', qty: '15 viên', usage: 'Uống 1 viên × 3 lần/ngày sau ăn, 5 ngày', stockStatus: 'green', stockText: 'Đủ (210)' },
    { name: 'Ibuprofen 400mg', qty: '10 viên', usage: 'Uống 1 viên khi đau, sau ăn', stockStatus: 'amber', stockText: 'Sắp hết · 64' },
    { name: 'Nước súc miệng Chlorhexidine 0.12%', qty: '1 chai', usage: 'Súc miệng 15ml × 2 lần/ngày', stockStatus: 'green', stockText: 'Đủ (35)' }
  ],
  functions52: [
    ['MOD_AUTH · Xác thực & phân quyền (6)', [
      ['F_AUTH_01', 'Đăng ký / Kích hoạt tài khoản bệnh nhân tại quầy & OTP đầu', 'activate', 'RECEPTIONIST, PATIENT'],
      ['F_AUTH_02', 'Đăng nhập & kiểm soát phân quyền RBAC', 'login', 'Tất cả'],
      ['F_AUTH_03', 'Hồ sơ cá nhân, đổi mật khẩu & đổi SĐT OTP', 'm-profile', 'Tất cả'],
      ['F_AUTH_04', 'Khôi phục mật khẩu bằng SĐT & CCCD/Hộ chiếu (OTP)', 'forgot', 'Khách'],
      ['F_AUTH_05', 'Quản lý vai trò & ma trận phân quyền RBAC', 'm-roles', 'ADMIN'],
      ['F_AUTH_06', 'Nhật ký thao tác bảo mật (Audit Log)', 'm-audit', 'ADMIN']
    ]],
    ['MOD_PAT · Bệnh nhân & hồ sơ (9)', [
      ['F_PAT_01', 'Tạo hồ sơ bệnh nhân (gộp đối soát CCCD/Hộ chiếu HMAC)', 'm-checkin', 'ADMIN, RECEPTIONIST'],
      ['F_PAT_02', 'Tìm kiếm nhanh bệnh nhân', 'm-patients', 'Nhân viên'],
      ['F_PAT_03', 'Kiểm tra trùng hồ sơ định danh', 'm-checkin', 'ADMIN, RECEPTIONIST'],
      ['F_PAT_04', 'Ghi nhận tiền sử bệnh nền & dị ứng (append-only)', 'm-patient-detail', 'ADMIN, DENTIST, ASSISTANT'],
      ['F_PAT_05', 'Ghi nhận sinh hiệu (Huyết áp, Mạch, Nhiệt độ, SpO2)', 'm-patient-detail', 'ADMIN, DENTIST, ASSISTANT'],
      ['F_PAT_06', 'Hiển thị Banner cảnh báo an toàn lâm sàng', 'm-diagnosis', 'Nhân viên'],
      ['F_PAT_07', 'Timeline lịch sử điều trị qua các đợt khám', 'p-record', 'Nhân viên, PATIENT'],
      ['F_PAT_08', 'Khóa / chốt hồ sơ lần khám', 'm-patient-detail', 'ADMIN, DENTIST'],
      ['F_PAT_09', 'Tạo bản ghi khám (Visit) mới', 'm-patient-detail', 'ADMIN, RECEPTIONIST, DENTIST']
    ]],
    ['MOD_APP · Đặt lịch bệnh nhân (2)', [
      ['F_APP_01', 'Đặt lịch trực tuyến (Công khai & Cổng bệnh nhân)', 'booking', 'Khách, PATIENT'],
      ['F_APP_02', 'Đặt lịch tại quầy (Lễ tân đặt hộ)', 'm-counter-booking', 'ADMIN, RECEPTIONIST']
    ]],
    ['MOD_CHK · Tiếp đón & hàng đợi (6)', [
      ['F_CHK_01', 'Xác nhận khách đến tại quầy (kèm nhắc công nợ)', 'm-checkin', 'ADMIN, RECEPTIONIST'],
      ['F_CHK_02', 'Danh sách hàng đợi chung trong ngày & TV phòng chờ', 'm-queue', 'Nhân viên'],
      ['F_CHK_03', 'Gọi số & dẫn bệnh nhân vào phòng khám', 'm-queue', 'ADMIN, RECEPTIONIST, ASSISTANT'],
      ['F_CHK_04', 'Bắt đầu / kết thúc ca khám lâm sàng', 'm-workspace', 'ADMIN, DENTIST'],
      ['F_CHK_07', 'Theo dõi lịch sử trạng thái tiến trình hàng đợi', 'm-queue', 'Nhân viên'],
      ['F_CHK_08', 'Điều phối chuyển Nha sĩ / Chụp X-quang', 'm-queue', 'Nhân viên']
    ]],
    ['MOD_FDI · Sơ đồ răng điện tử FDI (7)', [
      ['F_FDI_01', 'Hiển thị sơ đồ giải phẫu 32 răng người lớn / 20 răng trẻ em', 'm-fdi', 'Nhân viên, PATIENT'],
      ['F_FDI_02', 'Chọn từng răng & 5 mặt răng (Ngoài, Trong, Gần, Xa, Nhai)', 'm-fdi', 'ADMIN, DENTIST, ASSISTANT'],
      ['F_FDI_03', 'Gán tình trạng bệnh lý theo danh mục lên răng', 'm-fdi', 'ADMIN, DENTIST'],
      ['F_FDI_04', 'Chỉ định dịch vụ điều trị theo răng (snapshot giá)', 'm-fdi-services', 'ADMIN, DENTIST'],
      ['F_FDI_05', 'Cập nhật trạng thái răng sau khi hoàn tất điều trị', 'm-fdi-services', 'ADMIN, DENTIST'],
      ['F_FDI_06', 'Xem lịch sử thay đổi trạng thái từng răng', 'm-fdi', 'Nhân viên, PATIENT'],
      ['F_FDI_07', 'Ghi chú lâm sàng chi tiết theo từng răng', 'm-fdi', 'ADMIN, DENTIST, ASSISTANT']
    ]],
    ['MOD_IMG · Hình ảnh hồ sơ (2)', [
      ['F_IMG_02', 'Tải ảnh X-quang / chụp trong miệng gắn lần khám & răng', 'm-images', 'ADMIN, DENTIST, ASSISTANT'],
      ['F_IMG_04', 'Xem, thu phóng, xoay và đảo tương phản ảnh cận lâm sàng', 'm-images', 'Nhân viên, PATIENT']
    ]],
    ['MOD_RX · Đơn thuốc điện tử (4)', [
      ['F_RX_01', 'Kê đơn thuốc điện tử (cảnh báo dị ứng thuốc)', 'm-prescription', 'ADMIN, DENTIST'],
      ['F_RX_02', 'Kiểm tra tồn thuốc & xác nhận bàn giao thuốc trừ kho', 'm-prescription', 'ADMIN, DENTIST, RECEPTIONIST, ASSISTANT'],
      ['F_RX_04', 'In / xuất đơn thuốc có mã QR & tra cứu công khai', 'rx-public', 'Tất cả'],
      ['F_RX_05', 'Xem lịch sử đơn thuốc của bệnh nhân', 'p-visit-rx', 'Nhân viên, PATIENT']
    ]],
    ['MOD_BIL · Thanh toán & hóa đơn (5)', [
      ['F_BIL_01', 'Tự động tổng hợp hóa đơn nháp sau khi kết thúc khám', 'm-invoice-draft', 'ADMIN, RECEPTIONIST, DENTIST'],
      ['F_BIL_02', 'Áp dụng giảm giá / khuyến mãi (lưu lý do, không âm)', 'm-invoice-draft', 'ADMIN, RECEPTIONIST'],
      ['F_BIL_03', 'Thu tiền mặt / Chuyển khoản đối soát & theo dõi đợt trả góp', 'm-invoices', 'ADMIN, RECEPTIONIST, PATIENT'],
      ['F_BIL_06', 'In / xuất biên lai thu tiền khổ A5 & nhiệt K80', 'm-receipt', 'ADMIN, RECEPTIONIST, PATIENT'],
      ['F_BIL_07', 'Hủy / điều chỉnh hóa đơn nháp trước thanh toán', 'm-invoice-draft', 'ADMIN, RECEPTIONIST']
    ]],
    ['MOD_INV · Kho & vật tư (2)', [
      ['F_INV_03', 'Lập phiếu nhập kho vật tư (kèm cảnh báo ngưỡng tồn tối thiểu)', 'm-stock-receipts', 'ADMIN, ASSISTANT, DENTIST'],
      ['F_INV_06', 'Nhập & xác nhận trừ kho vật tư tiêu hao thực tế theo ca khám', 'm-stock-issues', 'ADMIN, ASSISTANT']
    ]],
    ['MOD_ASS · Nghiệp vụ phụ tá (1)', [
      ['F_ASS_06', 'Theo dõi & cập nhật trạng thái ghế / phòng điều trị', 'm-chair-status', 'ADMIN, ASSISTANT']
    ]],
    ['MOD_MST · Danh mục hệ thống (7)', [
      ['F_MST_01', 'Quản lý danh mục dịch vụ & bảng giá niêm yết', 'm-services', 'ADMIN'],
      ['F_MST_02', 'Quản lý danh mục bệnh lý / triệu chứng & màu FDI', 'm-diagnoses', 'ADMIN'],
      ['F_MST_03', 'Quản lý danh mục thuốc', 'm-medicines', 'ADMIN'],
      ['F_MST_04', 'Quản lý danh mục vật tư y tế & ngưỡng tồn tối thiểu', 'm-supplies', 'ADMIN'],
      ['F_MST_05', 'Quản lý danh mục phòng khám & ghế điều trị', 'm-chairs', 'ADMIN'],
      ['F_MST_06', 'Cấu hình lịch khám, slot công suất & mẫu in ấn phòng khám', 'm-scheduling', 'ADMIN'],
      ['F_MST_07', 'Quản lý tài khoản nhân viên & chữ ký bác sĩ', 'm-staff', 'ADMIN']
    ]],
    ['MOD_RPT · Báo cáo & thống kê (1)', [
      ['F_RPT_01', 'Báo cáo doanh thu (chỉ tính giao dịch đã thanh toán)', 'm-revenue', 'ADMIN']
    ]]
  ]
};
