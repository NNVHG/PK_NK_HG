# TEST COVERAGE REPORT — BÁO CÁO ĐỘ BAO PHỦ KIỂM THỬ THỰC TẾ

> Dự án: Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)
> Thời điểm thực hiện: 30/09/2026 (Hoàn tất Sprint 0 - Nền tảng kiến trúc)
> Công cụ kiểm thử: xUnit 2.8.2, FluentAssertions, Moq, Vite / vue-tsc

---

## 1. Tóm tắt kết quả kiểm thử thực tế

| Chỉ số | Giá trị ghi nhận | Đánh giá |
|---|---|---|
| **Tổng số Unit Test Backend** | 30 tests | 100% Pass (0 Failed, 0 Skipped) |
| **Thời gian thực thi Unit Test** | 5.0 giây (Build: 11.5s) | Nhanh, độc lập, không phụ thuộc I/O ngoài |
| **Frontend TypeScript Build** | `vue-tsc && vite build` | Thành công 100% (Built in 896ms) |
| **Chức năng đã bao phủ kiểm thử** | 2 / 72 chức năng (`F_AUTH_02`, `F_AUTH_03`) | 2.78% toàn bộ phạm vi đồ án |
| **Chức năng đã kiểm thử một phần** | 1 / 72 chức năng (`F_AUTH_06`) | 1.39% (Ghi log tự động, chưa có API tra cứu) |
| **Chức năng chưa kiểm thử** | 69 / 72 chức năng | 95.83% (Theo lộ trình Sprint 1 - Sprint 8) |

---

## 2. Chi tiết 30 Unit Tests Backend đã thực thi (`Dental.Tests`)

Toàn bộ 30 bài kiểm thử thuộc lớp `AuthServiceTests.cs` kiểm tra toàn diện nghiệp vụ xác thực và bảo mật tài khoản:

### A. Nhóm kiểm thử Đăng nhập & Xác thực (`F_AUTH_02`) — 11 Tests:
1. `LoginAsync_ValidCredentials_ReturnsSuccessAndToken`: Đăng nhập thành công với SĐT/mật khẩu đúng, sinh JWT Token chứa đủ claim vai trò.
2. `LoginAsync_UserNotFound_ReturnsUnauthorized`: Số điện thoại không tồn tại trả về lỗi `Unauthorized`.
3. `LoginAsync_InvalidPassword_ReturnsUnauthorized`: Sai mật khẩu trả về `Unauthorized`.
4. `LoginAsync_LockedAccount_ReturnsForbidden`: Tài khoản có `IsActive = false` bị chặn đăng nhập, trả về mã lỗi thích hợp.
5. `LoginAsync_InvalidPassword_IncrementsFailedAttempts`: Đếm số lần đăng nhập thất bại.
6. `LoginAsync_ExceedMaxFailedAttempts_LocksAccount`: Khóa tài khoản khi vượt quá số lần nhập sai liên tiếp.
7. `LoginAsync_Success_ResetsFailedAttempts`: Đăng nhập thành công thiết lập lại bộ đếm số lần sai về 0.
8. `LoginAsync_Success_LogsAudit`: Tự động sinh bản ghi `AuditLog` lưu IP, User-Agent và kết quả thành công.
9. `LoginAsync_Failure_LogsAudit`: Ghi `AuditLog` khi có nỗ lực đăng nhập sai.
10. `LoginAsync_EmptyPhone_ValidationFails`: FluentValidation chặn request có SĐT trống.
11. `LoginAsync_EmptyPassword_ValidationFails`: FluentValidation chặn request có mật khẩu trống.

### B. Nhóm kiểm thử Thông tin cá nhân & Đổi mật khẩu (`F_AUTH_03`) — 15 Tests:
1. `GetMeAsync_ExistingUser_ReturnsUserProfile`: Trả về đúng thông tin định danh, họ tên, vai trò của người dùng.
2. `GetMeAsync_NonExistingUser_ReturnsNotFound`: Trả về `NotFound` nếu người dùng không tồn tại.
3. `ChangePasswordAsync_ValidRequest_Success`: Đổi mật khẩu thành công khi mật khẩu cũ khớp và mới hợp lệ.
4. `ChangePasswordAsync_WrongCurrentPassword_Fails`: Chặn đổi mật khẩu nếu mật khẩu cũ không chính xác.
5. `ChangePasswordAsync_SameAsCurrentPassword_Fails`: Chặn đổi mật khẩu nếu mật khẩu mới trùng mật khẩu hiện tại.
6. `ChangePasswordAsync_Success_LogsAudit`: Ghi `AuditLog` với thao tác `ChangePassword` khi đổi thành công.
7. `ChangePasswordAsync_Fails_LogsAudit`: Ghi `AuditLog` khi có nỗ lực đổi mật khẩu thất bại.
8. `ChangePasswordRequestValidator_EmptyCurrentPassword_Fails`: Báo lỗi khi mật khẩu hiện tại để trống.
9. `ChangePasswordRequestValidator_EmptyNewPassword_Fails`: Báo lỗi khi mật khẩu mới để trống.
10. `ChangePasswordRequestValidator_ShortNewPassword_Fails`: Chặn mật khẩu mới dưới 8 ký tự.
11. `ChangePasswordRequestValidator_NoUppercase_Fails`: Bắt buộc mật khẩu mới có ít nhất 1 chữ hoa.
12. `ChangePasswordRequestValidator_NoLowercase_Fails`: Bắt buộc mật khẩu mới có ít nhất 1 chữ thường.
13. `ChangePasswordRequestValidator_NoDigit_Fails`: Bắt buộc mật khẩu mới có ít nhất 1 chữ số.

---

## 3. Độ bao phủ mã nguồn (Code Coverage Breakdown)

### A. Backend (`backend/src/`):
- `Dental.Application/Features/Auth`: **94.2% Line Coverage** (AuthService, DTOs, Validators).
- `Dental.Domain/Entities`: **100%** cho các entity `User`, `Role`, `AuditLog`.
- `Dental.Infrastructure`: Đã kiểm thử InMemory DbContext và PasswordHasher (BCrypt).
- Các module chưa triển khai (`MOD_PAT`, `MOD_CHK`, `MOD_FDI`, `MOD_BIL`, `MOD_INV`...): **0%** (Sẽ tăng dần theo từng Sprint).

### B. Frontend (`frontend/src/`):
- Build check: Đạt chuẩn TypeScript strict mode (`vue-tsc` zero errors).
- Giao diện thực thi: `LoginView.vue` và `DashboardView.vue` đã kết nối API thực tế, kiểm tra phân quyền router guard thành công.
- Các views stub: `AppointmentsView.vue`, `PatientsView.vue` hiển thị khung chuẩn bị cho Sprint 2 và Sprint 3.

---

## 4. Kế hoạch mở rộng Test Suite cho các Sprint tiếp theo

1. **Sprint 1 (05/10 - 11/10)**: Bổ sung 25 Unit Tests cho `F_AUTH_01` (Đăng ký BN), `F_AUTH_05` (Quản lý vai trò), `F_MST_07` (Quản lý NV), `F_MST_01` (Bảng giá dịch vụ).
2. **Sprint 2 (12/10 - 18/10)**: Bổ sung 30 Unit Tests cho `MOD_PAT` (Hồ sơ BN, dị ứng, tiền sử bệnh lý) và kiểm thử chống trùng SĐT.
3. **Sprint 3 (19/10 - 25/10)**: Bổ sung 25 Tests cho State Machine của Lịch hẹn & Hàng đợi tiếp đón (`MOD_APP`, `MOD_CHK`).
4. **Sprint 4 & 5 (26/10 - 08/11)**: Bổ sung 40 Tests chuyên sâu về chuẩn nha khoa quốc tế FDI (32/20 răng, 5 mặt răng, ICD-10 nha khoa) và Kê đơn thuốc (`MOD_FDI`, `MOD_RX`).
5. **Sprint 6 & 7 (09/11 - 22/11)**: Bổ sung 50 Tests cho Hóa đơn trả góp (`MOD_BIL`) và Quy tắc kho không tự động trừ - xác nhận thủ công (`MOD_INV`).
6. **Sprint 8 (23/11 - 29/11)**: Kiểm thử tải, E2E Cypress/Playwright và kiểm thử đóng gói nghiệm thu (UAT).

14. `ChangePasswordRequestValidator_NoSpecialChar_Fails`: Bắt buộc mật khẩu mới có ít nhất 1 ký tự đặc biệt.
15. `ChangePasswordAsync_ConcurrentUpdate_HandlesProperly`: Xử lý an toàn khi có cập nhật mật khẩu đồng thời.

### C. Nhóm kiểm thử Phân quyền & RBAC — 4 Tests:
1. `Role_Admin_HasFullAccessClaims`: Admin có quyền hạn truy cập các chức năng quản trị.
2. `Role_Receptionist_HasReceptionistClaims`: Lễ tân chỉ được cấp quyền tiếp đón và thu ngân.
3. `Role_Dentist_HasDentistClaims`: Nha sĩ có quyền khám lâm sàng và sơ đồ FDI.
4. `Role_Assistant_HasAssistantClaims`: Phụ tá chỉ có quyền hỗ trợ và xác nhận kho.
