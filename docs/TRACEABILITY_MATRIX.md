# TRACEABILITY MATRIX — MA TRẬN TRUY VẾT 72 CHỨC NĂNG

> Đồ án: Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)
> Nguồn đối chiếu: chuc_nang_Gia_demo_8.xlsx (Sheet 03_Danh_sach_chuc_nang)
> Thời điểm audit: Sprint 0 (30/09/2026)

| Function ID | Module | Tên chức năng | Vai trò | UI | Frontend Logic | API Endpoint | Service/Handler | DB Support | Validation | RBAC | Audit Log | Test Status | Kết luận |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| F_AUTH_01 | MOD_AUTH | Đăng ký tài khoản bệnh nhân | Bệnh nhân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_AUTH_02 | MOD_AUTH | Đăng nhập & cấp quyền RBAC | Tất cả | YES (LoginView.vue) | YES (Pinia authStore) | YES (POST /api/auth/login) | YES (AuthService.LoginAsync) | YES (Users, Roles, AuditLogs) | YES (FluentValidation) | YES (Token + Role claims) | YES (AuditLog) | PASS (11 unit tests) | **FULL** |
| F_AUTH_03 | MOD_AUTH | Cập nhật hồ sơ cá nhân & đổi mật khẩu | Tất cả | YES (DashboardView.vue) | YES (apiClient calls) | YES (GET /me, POST /change-pwd) | YES (AuthService.ChangePasswordAsync) | YES (Users, AuditLogs) | YES (FluentValidation) | YES ([Authorize]) | YES (AuditLog) | PASS (15 unit tests) | **FULL** |
| F_AUTH_04 | MOD_AUTH | Quên/khôi phục mật khẩu | Tất cả | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_AUTH_05 | MOD_AUTH | Quản lý vai trò và quyền | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_AUTH_06 | MOD_AUTH | Nhật ký thao tác hệ thống | Admin | NO | NO | NO | PARTIAL (Ghi log ngầm) | YES (AuditLogs) | N/A | NO (Chưa có API query) | YES (Entity lưu DB) | PARTIAL (Được verify qua Auth) | **PARTIAL** |
| F_PAT_01 | MOD_PAT | Tạo hồ sơ bệnh nhân | Lễ tân, Phụ tá, Admin | PARTIAL (PatientsView stub) | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **PARTIAL** |
| F_PAT_02 | MOD_PAT | Tìm kiếm hồ sơ bệnh nhân nhanh | Lễ tân, Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_03 | MOD_PAT | Kiểm tra trùng hồ sơ | Lễ tân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_04 | MOD_PAT | Ghi nhận tiền sử bệnh & dị ứng | Lễ tân, Phụ tá (Nha sĩ xem theo quyền) | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_05 | MOD_PAT | Ghi nhận sinh hiệu cơ bản | Lễ tân, Phụ tá (Nha sĩ xem theo quyền) | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_06 | MOD_PAT | Cảnh báo tiền sử an toàn | Hệ thống, Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_07 | MOD_PAT | Lịch sử điều trị dạng timeline | Bệnh nhân, Nha sĩ, Lễ tân/Phụ tá theo quyền | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_08 | MOD_PAT | Khóa/chốt hồ sơ điều trị | Nha sĩ, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_PAT_09 | MOD_PAT | Tạo bản ghi khám mới cho từng lần khám | Lễ tân, Phụ tá; Hệ thống | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_APP_01 | MOD_APP | Đặt lịch khám trực tuyến | Bệnh nhân | PARTIAL (AppointmentsView stub) | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **PARTIAL** |
| F_APP_02 | MOD_APP | Đặt lịch tại quầy do Lễ tân thao tác hộ | Lễ tân | PARTIAL (AppointmentsView stub) | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **PARTIAL** |
| F_CHK_01 | MOD_CHK | Xác nhận khách đến phòng khám | Lễ tân, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_02 | MOD_CHK | Đưa bệnh nhân vào hàng đợi | Lễ tân, Phụ tá, Nha sĩ, Admin theo quyền; Hệ thống | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_03 | MOD_CHK | Gọi số và dẫn bệnh nhân sang khu vực khám tiếp theo | Lễ tân, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_07 | MOD_CHK | Cập nhật trạng thái tiến trình hàng đợi | Lễ tân, Phụ tá, Nha sĩ theo quyền; Hệ thống | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_08 | MOD_CHK | Điều phối bệnh nhân sang Nha sĩ hoặc X-quang | Lễ tân, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_04 | MOD_CHK | Bắt đầu/ kết thúc lần khám | Nha sĩ; Lễ tân/Phụ tá theo quyền hỗ trợ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_CHK_05 | MOD_CHK | Chuyển bệnh nhân sang nha sĩ khác | Lễ tân, Nha sĩ, Phụ tá theo quyền | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_01 | MOD_FDI | Hiển thị sơ đồ răng FDI | Nha sĩ, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_02 | MOD_FDI | Chọn răng và mặt răng | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_03 | MOD_FDI | Gán tình trạng bệnh lý | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_04 | MOD_FDI | Chỉ định dịch vụ theo răng | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_05 | MOD_FDI | Cập nhật trạng thái sau điều trị | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_06 | MOD_FDI | Lịch sử trạng thái răng | Nha sĩ, Bệnh nhân theo quyền | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_FDI_07 | MOD_FDI | Ghi chú lâm sàng theo răng | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_IMG_01 | MOD_IMG | Tạo chỉ định chụp ảnh | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_IMG_02 | MOD_IMG | Tải lên ảnh X-quang/chụp trong miệng | Phụ tá, Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_IMG_03 | MOD_IMG | Gắn ảnh với răng/lần khám | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_IMG_04 | MOD_IMG | Xem/thu phóng ảnh | Nha sĩ, Bệnh nhân theo quyền | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RX_01 | MOD_RX | Kê đơn thuốc điện tử | Nha sĩ | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RX_02 | MOD_RX | Kiểm tra tồn kho thuốc | Nha sĩ, Hệ thống | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RX_04 | MOD_RX | In/xuất đơn thuốc | Nha sĩ, Lễ tân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RX_05 | MOD_RX | Lịch sử đơn thuốc | Nha sĩ, Bệnh nhân theo quyền | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_01 | MOD_BIL | Tạo hóa đơn nháp từ dịch vụ | Hệ thống, Thu ngân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_02 | MOD_BIL | Áp dụng giảm giá/khuyến mãi | Thu ngân, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_03 | MOD_BIL | Xác nhận thanh toán | Thu ngân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_04 | MOD_BIL | Lập kế hoạch trả góp | Thu ngân, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_05 | MOD_BIL | Thu từng kỳ trả góp & theo dõi công nợ | Thu ngân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_06 | MOD_BIL | In/xuất biên lai | Thu ngân | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_BIL_07 | MOD_BIL | Hủy hóa đơn nháp/điều chỉnh trước thanh toán | Thu ngân, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_01 | MOD_INV | Quản lý danh mục vật tư và thuốc kho | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_02 | MOD_INV | Quản lý nhà cung cấp | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_03 | MOD_INV | Lập phiếu nhập kho | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_04 | MOD_INV | Quản lý lô & hạn sử dụng | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_05 | MOD_INV | Cấu hình BOM/định mức theo dịch vụ | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_06 | MOD_INV | Tự động trừ kho theo dịch vụ hoàn tất | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_07 | MOD_INV | Ghi nhận tiêu hao phát sinh | Phụ tá, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_08 | MOD_INV | Cảnh báo tồn tối thiểu & hết hạn | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_INV_09 | MOD_INV | Kiểm kê & điều chỉnh tồn | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_01 | MOD_ASS | Chuẩn bị khay dụng cụ theo dịch vụ | Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_02 | MOD_ASS | Ghi nhận tiêu hao tại ghế | Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_03 | MOD_ASS | Theo dõi quy trình vô trùng bằng checklist | Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_04 | MOD_ASS | Bàn giao ca | Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_05 | MOD_ASS | Checklist vệ sinh/đóng cửa cuối ngày | Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_ASS_06 | MOD_ASS | Trạng thái ghế/phòng điều trị | Phụ tá, Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_01 | MOD_MST | Quản lý dịch vụ & bảng giá | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_02 | MOD_MST | Quản lý bệnh lý/triệu chứng | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_03 | MOD_MST | Quản lý thuốc | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_04 | MOD_MST | Quản lý vật tư | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_05 | MOD_MST | Quản lý phòng & ghế nha khoa | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_06 | MOD_MST | Quản lý trạng thái và cấu hình hệ thống | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_MST_07 | MOD_MST | Quản lý người dùng nhân viên | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RPT_01 | MOD_RPT | Báo cáo doanh thu | Admin, Chủ phòng khám | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RPT_02 | MOD_RPT | Báo cáo hiệu suất nha sĩ | Admin, Chủ phòng khám | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RPT_04 | MOD_RPT | Báo cáo xuất-nhập-tồn | Admin, Phụ tá | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
| F_RPT_05 | MOD_RPT | Báo cáo tiêu hao theo dịch vụ | Admin | NO | NO | NO | NO | NO | NO | NO | NO | NOT TESTED | **NOT IMPLEMENTED** |
