# PROJECT_SCOPE.md — Phạm vi dự án

> Nguồn: Đề cương tốt nghiệp + File đặc tả chức năng (chuc_nang_Gia_demo_7_CapNhat_LuongMoi)

---

## 1. Tổng quan

| Thuộc tính | Giá trị |
|---|---|
| Tên dự án | Hệ thống quản lý phòng khám nha khoa Hoàng Gia |
| Loại | Đồ án tốt nghiệp |
| Sinh viên | Nguyễn Ngô Vũ Hoàng Gia (2224802010628) |
| Lớp | D22CNTT02 |
| GVHD | T.s Ngô Thị Ngọc Diệu |
| Tổng số chức năng | 72 |
| Số module | 12 |
| Số vai trò | 5 |
| Phạm vi cơ sở | Một phòng khám đơn lẻ |
| Phương pháp | Agile/Scrum, UML design |

---

## 2. Mục tiêu dự án

- Số hóa toàn bộ quy trình vận hành phòng khám nha khoa
- Chuẩn hóa hồ sơ răng theo tiêu chuẩn FDI quốc tế
- Gợi ý trừ kho vật tư theo BOM khi hoàn tất dịch vụ (Phụ tá/Admin xác nhận thủ công, không tự động trừ)
- Quản lý lịch hẹn và hàng đợi khám hiệu quả
- Cung cấp báo cáo vận hành và doanh thu cho ban quản lý

---

## 3. Danh sách module và chức năng

### MOD_AUTH — Xác thực & Phân quyền (6 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_AUTH_01 | Đăng ký tài khoản bệnh nhân | Cao | MVP |
| F_AUTH_02 | Đăng nhập & cấp quyền RBAC | Bắt buộc | MVP |
| F_AUTH_03 | Cập nhật hồ sơ cá nhân & đổi mật khẩu | Cao | MVP |
| F_AUTH_04 | Quên/khôi phục mật khẩu | Trung bình | Phase 2/MVP |
| F_AUTH_05 | Quản lý vai trò và quyền | Cao | MVP |
| F_AUTH_06 | Nhật ký thao tác hệ thống (Audit Log) | Cao | MVP |

### MOD_PAT — Bệnh nhân & Hồ sơ (9 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_PAT_01 | Tạo hồ sơ bệnh nhân | Cao | MVP |
| F_PAT_02 | Tìm kiếm hồ sơ bệnh nhân nhanh | Cao | MVP |
| F_PAT_03 | Kiểm tra trùng hồ sơ | Trung bình | MVP |
| F_PAT_04 | Ghi nhận tiền sử bệnh & dị ứng | Cao | MVP |
| F_PAT_05 | Ghi nhận sinh hiệu cơ bản | Cao | MVP |
| F_PAT_06 | Cảnh báo tiền sử an toàn | Cao | MVP |
| F_PAT_07 | Lịch sử điều trị dạng timeline | Cao | MVP |
| F_PAT_08 | Khóa/chốt hồ sơ điều trị | Cao | MVP |
| F_PAT_09 | Tạo bản ghi lần khám mới | Bắt buộc | MVP |

### MOD_APP — Đặt lịch bệnh nhân (2 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_APP_01 | Đặt lịch trực tuyến (bệnh nhân tự đặt online) | Cao | MVP |
| F_APP_02 | Đặt lịch tại quầy (Lễ tân đặt hộ) | Bắt buộc | MVP |

### MOD_CHK — Tiếp đón & Hàng đợi (7 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_CHK_01 | Check-in bệnh nhân | Bắt buộc | MVP |
| F_CHK_02 | Đưa bệnh nhân vào hàng đợi | Cao | MVP |
| F_CHK_03 | Gọi bệnh nhân tiếp theo | Cao | MVP |
| F_CHK_07 | Cập nhật trạng thái tiến trình hàng đợi | Cao | MVP |
| F_CHK_08 | Điều phối bệnh nhân sang Nha sĩ hoặc X-quang | Cao | MVP |
| F_CHK_04 | Bắt đầu/kết thúc lần khám | Cao | MVP |
| F_CHK_05 | Chuyển bệnh nhân sang nha sĩ khác | Trung bình | MVP |

### MOD_FDI — Sơ đồ răng FDI (7 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_FDI_01 | Hiển thị sơ đồ răng FDI | Bắt buộc | MVP |
| F_FDI_02 | Chọn răng và mặt răng | Cao | MVP |
| F_FDI_03 | Gán tình trạng bệnh lý | Cao | MVP |
| F_FDI_04 | Chỉ định dịch vụ theo răng | Cao | MVP |
| F_FDI_05 | Cập nhật trạng thái sau điều trị | Cao | MVP |
| F_FDI_06 | Lịch sử trạng thái răng | Cao | MVP |
| F_FDI_07 | Ghi chú lâm sàng theo răng | Trung bình | MVP |

### MOD_IMG — Hình ảnh hồ sơ (4 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_IMG_01 | Tạo chỉ định chụp ảnh | Trung bình | MVP |
| F_IMG_02 | Tải lên ảnh X-quang/chụp trong miệng | Cao | MVP |
| F_IMG_03 | Gắn ảnh với răng/lần khám | Trung bình | MVP |
| F_IMG_04 | Xem/thu phóng ảnh | Trung bình | MVP |

### MOD_RX — Đơn thuốc (4 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_RX_01 | Kê đơn thuốc điện tử | Cao | MVP |
| F_RX_02 | Kiểm tra tồn kho thuốc | Cao | MVP |
| F_RX_04 | In/xuất đơn thuốc | Trung bình | MVP |
| F_RX_05 | Lịch sử đơn thuốc | Trung bình | MVP |

### MOD_BIL — Thanh toán & Hóa đơn (7 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_BIL_01 | Tạo hóa đơn nháp từ dịch vụ | Bắt buộc | MVP |
| F_BIL_02 | Áp dụng giảm giá/khuyến mãi | Trung bình | MVP |
| F_BIL_03 | Xác nhận thanh toán | Bắt buộc | MVP |
| F_BIL_04 | Lập kế hoạch trả góp | Cao | MVP |
| F_BIL_05 | Thu từng kỳ trả góp & theo dõi công nợ | Cao | MVP |
| F_BIL_06 | In/xuất biên lai | Cao | MVP |
| F_BIL_07 | Hủy hóa đơn nháp/điều chỉnh trước thanh toán | Trung bình | MVP |

### MOD_INV — Kho & Vật tư (9 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_INV_01 | Quản lý danh mục vật tư và thuốc kho | Cao | MVP |
| F_INV_02 | Quản lý nhà cung cấp | Trung bình | MVP |
| F_INV_03 | Lập phiếu nhập kho | Cao | MVP |
| F_INV_04 | Quản lý lô & hạn sử dụng | Cao | MVP |
| F_INV_05 | Cấu hình BOM/định mức theo dịch vụ | Cao | MVP |
| F_INV_06 | Gợi ý trừ kho theo dịch vụ (nhân viên xác nhận) | Bắt buộc | MVP |
| F_INV_07 | Ghi nhận tiêu hao phát sinh | Cao | MVP |
| F_INV_08 | Cảnh báo tồn tối thiểu & hết hạn | Cao | MVP |
| F_INV_09 | Kiểm kê & điều chỉnh tồn | Trung bình | MVP |

### MOD_ASS — Nghiệp vụ phụ tá (6 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_ASS_01 | Chuẩn bị khay dụng cụ theo dịch vụ | Cao | MVP |
| F_ASS_02 | Ghi nhận tiêu hao tại ghế | Cao | MVP |
| F_ASS_03 | Theo dõi quy trình vô trùng bằng checklist | Cao | Phase 2/MVP |
| F_ASS_04 | Bàn giao ca | Trung bình | Phase 2 |
| F_ASS_05 | Checklist vệ sinh/đóng cửa cuối ngày | Trung bình | Phase 2 |
| F_ASS_06 | Trạng thái ghế/phòng điều trị | Cao | MVP |

### MOD_MST — Danh mục hệ thống (7 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_MST_01 | Quản lý dịch vụ & bảng giá | Bắt buộc | MVP |
| F_MST_02 | Quản lý bệnh lý/triệu chứng | Cao | MVP |
| F_MST_03 | Quản lý thuốc | Cao | MVP |
| F_MST_04 | Quản lý vật tư | Cao | MVP |
| F_MST_05 | Quản lý phòng & ghế nha khoa | Cao | MVP |
| F_MST_06 | Quản lý trạng thái và cấu hình hệ thống | Trung bình | MVP |
| F_MST_07 | Quản lý người dùng nhân viên | Bắt buộc | MVP |

### MOD_RPT — Báo cáo & Thống kê (4 chức năng)

| Mã | Tên chức năng | Ưu tiên | Phạm vi |
|---|---|---|---|
| F_RPT_01 | Báo cáo doanh thu | Bắt buộc | MVP |
| F_RPT_02 | Báo cáo hiệu suất nha sĩ | Cao | MVP |
| F_RPT_04 | Báo cáo xuất-nhập-tồn | Cao | MVP |
| F_RPT_05 | Báo cáo tiêu hao theo dịch vụ | Trung bình | MVP |

---

## 4. Ngoài phạm vi (Out of scope)

| Hạng mục | Lý do |
|---|---|
| Tích hợp BHYT (bảo hiểm y tế) | Không có trong đề cương; quy định phức tạp |
| Kết nối thiết bị DICOM vật lý | Ngoài phạm vi đồ án |
| Quản lý đa chi nhánh | Phòng khám đơn lẻ |
| Tự động xác thực thanh toán ngân hàng | Chỉ ghi nhận thủ công |
| Tích hợp SMS/Email thực (Phase 2) | MVP chỉ notification nội hệ thống |
| Hệ thống tính lương nhân viên (HRM) | Ngoài phạm vi |
| Phân tích hình ảnh AI | Ngoài phạm vi |
| Quản lý ca làm việc / lịch trực nhân sự | Đã loại khỏi phạm vi theo bản cập nhật chuc_nang_Gia_demo_7 |

---

## 5. Phân giai đoạn (MVP vs Phase 2)

**MVP:** Tất cả chức năng có nhãn "MVP" hoặc "Bắt buộc" — đây là mục tiêu demo và bảo vệ đồ án.

**Phase 2 (sau đồ án):** F_ASS_04, F_ASS_05, một phần F_AUTH_04, F_ASS_03.

> Tổng MVP ước tính: ~68/72 chức năng (chính xác cần rà soát lại khi implement).
