# BÁO CÁO THỰC THI KIỂM THỬ (TEST EXECUTION REPORT)
## Hệ thống quản lý phòng khám nha khoa Hoàng Gia

---

## 1. Mục đích

Báo cáo này ghi nhận kết quả kiểm toán, kiểm thử thực tế (compile, unit test, integration smoke test, RBAC policies, database migrations, audit logs) trên mã nguồn của dự án phòng khám nha khoa Hoàng Gia, phục vụ đánh giá tiến độ và chuẩn bị bảo vệ đồ án tốt nghiệp.

---

## 2. Môi trường kiểm thử

- **Hệ điều hành:** Windows 11 Pro 64-bit (win32)
- **Runtime / SDK:** .NET SDK 10.0.104, C# 13 / net10.0
- **Database Engine:** PostgreSQL 18.6 on x86_64-windows (port 5432)
- **Node / Frontend Runtime:** Node.js v22.14.0, npm 10.9.2 (frontend source chưa scaffold)
- **IDE / Editor:** Visual Studio Code
- **Công cụ dòng lệnh:** PowerShell 5.1, dotnet CLI, dotnet-ef 10.0.12, psql 18.6

---

## 3. Commit / Version đã kiểm thử

- **Trạng thái:** Phiên kiểm toán Sprint 0 / Khởi tạo nền tảng MOD_AUTH
- **Ngày kiểm thử:** 2026-09-29

---

## 4. Công cụ kiểm thử

- **xUnit v2.9.3 + NSubstitute v5.3.0 + Microsoft.NET.Test.Sdk v17.12.0** cho Unit Tests
- **Microsoft.AspNetCore.Authorization** cho RBAC Policy testing
- **PowerShell `Invoke-RestMethod`** cho REST API Smoke Testing
- **EF Core CLI (`dotnet-ef`)** cho Migration & Schema verification
- **psql CLI** cho Database Inspection

---

## 5. Kết quả biên dịch (Build Result)

- **Lệnh thực thi:** `dotnet build backend/DentalClinic.sln`
- **Kết quả:** `Build succeeded: 0 Error(s), 16 Warning(s)`
- **Ghi chú:** 16 cảnh báo `NU1903` liên quan gói phụ thuộc chuyển tiếp `System.Security.Cryptography.Xml` 9.0.0. Không có lỗi compile.

---

## 6. Kết quả kiểm thử Backend (Backend Test Result)

- **Lệnh thực thi:** `dotnet test backend/DentalClinic.sln`
- **Số lượng test:** 26 tests
- **Passed:** 26 (100%)
- **Failed:** 0
- **Skipped:** 0
- **Thời gian chạy:** 4.3s
- **Phân nhóm test:**
  - `AuthServiceTests`: 7 tests (Login thành công, sai pass, sai phone, khóa tài khoản, audit log, GetMeAsync)
  - `RbacPolicyTests`: 11 tests (Danh mục `StaffRoles`, `AdminOnly` policy cho 5 role, `StaffAny` policy cho 5 role)
  - `BCryptPasswordHasherTests`: 5 tests (Hash, Verify đúng pass, Verify sai pass, Salt ngẫu nhiên)
  - `ResultTests`: 3 tests (Success, Failure, Exception khi truy cập Value thất bại)

---

## 7. Kết quả kiểm thử Frontend (Frontend Test Result)

- **Trạng thái:** `BLOCKED`
- **Chi tiết:** Thư mục `frontend/` chưa được khởi tạo mã nguồn trong repository (chưa có `package.json`). Cần bước scaffold Vue 3 + Vite + TypeScript trước khi có thể chạy `npm install` và kiểm thử UI.

---

## 8. Kết quả kiểm thử Cơ sở dữ liệu (Database Test Result)

- **Trạng thái kết nối:** `PASS` (kết nối PostgreSQL localhost:5432, db `dental_clinic`)
- **Migration:** `20260929164033_InitialAuth` áp dụng thành công.
- **Bảng dữ liệu đã tạo:**
  - `public."__EFMigrationsHistory"`
  - `public."Roles"`
  - `public."Users"`
  - `public."AuditLogs"`
- **Dữ liệu Seeding:**
  - 5 vai trò hệ thống đã nạp: `ADMIN`, `RECEPTIONIST`, `DENTIST`, `ASSISTANT`, `PATIENT`.
  - 5 tài khoản mẫu đã nạp tương ứng từng vai trò, mật khẩu mã hóa BCrypt an toàn.

---

## 9. Kết quả API Smoke Test

Chạy trực tiếp trên ứng dụng backend `http://localhost:5000`:

| Endpoint | Method | Input / Auth | Kết quả thực tế | Trạng thái |
|---|---|---|---|---|
| `/swagger/v1/swagger.json` | GET | Anonymous | HTTP 200, OpenAPI v1 Docs hợp lệ | PASS |
| `/api/auth/login` | POST | SĐT Admin `0900000001` + Đúng pass | HTTP 200, trả JWT + Claims | PASS |
| `/api/auth/login` | POST | SĐT Admin + Sai pass | HTTP 401, JSON `{code: "AUTH_001", message: "..."}` | PASS |
| `/api/auth/login` | POST | SĐT chưa đăng ký | HTTP 401, không lộ thông tin tồn tại SĐT | PASS |
| `/api/auth/me` | GET | Không có Bearer token | HTTP 401 Unauthorized | PASS |
| `/api/auth/me` | GET | Token Admin hợp lệ | HTTP 200, trả đúng thông tin Admin | PASS |
| `/api/dev/admin-only` | GET | Anonymous (không token) | HTTP 401 Unauthorized | PASS |
| `/api/dev/admin-only` | GET | Token Admin | HTTP 200 OK | PASS |
| `/api/dev/admin-only` | GET | Token Bệnh nhân | HTTP 403 Forbidden | PASS |
| `/api/dev/admin-only` | GET | Token Phụ tá | HTTP 403 Forbidden | PASS |
| `/api/dev/staff-only` | GET | Token Phụ tá | HTTP 200 OK | PASS |
| `/api/dev/staff-only` | GET | Token Bệnh nhân | HTTP 403 Forbidden | PASS |

---

## 10. Kết quả kiểm thử Phân quyền (RBAC Test)

- **Bảo vệ tầng Web API:** Đạt. Attribute `[Authorize(Policy = ...)]` chặn triệt để token không đủ thẩm quyền (trả HTTP 403 Forbidden).
- **Danh mục vai trò nội bộ:** Đạt. Policy `StaffAny` chỉ chấp nhận Admin, Receptionist, Dentist, Assistant; Bệnh nhân (Patient) bị chặn 100%.

---

## 11. Kết quả kiểm thử Bảo mật (Security Test)

- **Lưu trữ mật khẩu:** Đạt. Mật khẩu được băm bằng thư viện `BCrypt.Net-Next` với salt ngẫu nhiên, không lưu plain text.
- **Che giấu thông tin nhạy cảm:** Đạt. Số điện thoại trong bảng `AuditLogs` được mask dạng `09*****001`, không lưu password/token vào log.
- **Bảo vệ rò rỉ tài khoản:** Đạt. Đăng nhập sai SĐT hoặc sai mật khẩu đều trả cùng mã lỗi `AUTH_001` (chống vét cạn tài khoản theo UAT AUTH-09).
- **Khóa tài khoản:** Đạt. Chỉ khi mật khẩu đúng mà `IsActive = false` hệ thống mới trả lỗi `AUTH_002` (Tài khoản bị khóa).



---

## 12. Kết quả kiểm thử UAT Checklist

Theo đối chiếu với `docs/UAT_CHECKLIST.md` (tổng 45 kịch bản UAT):

| Nhóm module | Số lượng test | PASS | FAIL | PARTIAL | BLOCKED | NOT TESTED |
|---|---|---|---|---|---|---|
| 1. MOD_AUTH | 11 | 4 | 0 | 0 | 0 | 7 |
| 2. MOD_PAT | 9 | 0 | 0 | 0 | 0 | 9 |
| 3. MOD_APP | 2 | 0 | 0 | 0 | 0 | 2 |
| 4. MOD_CHK | 10 | 0 | 0 | 0 | 0 | 10 |
| 5. MOD_FDI | 6 | 0 | 0 | 0 | 0 | 6 |
| 6. MOD_INV | 7 | 0 | 0 | 0 | 0 | 7 |
| **Tổng cộng** | **45** | **4** | **0** | **0** | **0** | **41** |

*Chi tiết các test case MOD_AUTH đã PASS:*
- `AUTH-03`: Đăng nhập đúng SĐT + mật khẩu Admin → Đã sinh token và nhận diện đúng quyền.
- `AUTH-07`: Đăng nhập vai trò Bệnh nhân → Token cấp đúng quyền Patient, bị chặn vào API nhân viên.
- `AUTH-08`: Đăng nhập sai mật khẩu → Trả mã lỗi 401 `AUTH_001`.
- `AUTH-09`: Đăng nhập khi tài khoản bị khóa → Unit test xác nhận trả lỗi `AUTH_002`.

*Ghi chú 41 test cases NOT TESTED:* Các module nghiệp vụ (Hồ sơ bệnh nhân, Lịch hẹn, Tiếp đón hàng đợi, Sơ đồ răng FDI, Kho vật tư) thuộc lộ trình các Sprint tiếp theo (chưa có code backend/frontend).

---

## 13. Danh sách lỗi phát hiện (Bugs Found)

| Mã lỗi | Mức độ | Mô tả lỗi | Trạng thái |
|---|---|---|---|
| BUG-01 | **P0** | Lỗi compile thiếu tham chiếu `RoleCodes.StaffRoles` và namespace `Policies` trong `DevController.cs` | ĐÃ SỬA |
| BUG-02 | **P0** | Lỗi shadow foreign key conflict `AuditLog.UserId1` trong EF Core model config | ĐÃ SỬA |
| BUG-03 | **P0** | Database chưa có migration khởi tạo và `appsettings.Development.json` chưa cấu hình thông số kết nối PostgreSQL | ĐÃ SỬA |
| BUG-04 | **P0** | Thư mục `frontend/` chưa được khởi tạo trong kho lưu trữ | TỒN TẠI (Chờ scaffold) |
| BUG-05 | **P1** | Mâu thuẫn tài liệu: `PROJECT_SCOPE.md` và `UAT_CHECKLIST.md` mô tả tự động trừ kho, trái với `AGENTS.md` (hỗ trợ/xác nhận thủ công) | ĐÃ SỬA |
| BUG-06 | **P2** | `Dental.Tests.csproj` chưa tham chiếu `Dental.Api.csproj`, dẫn đến không thể kiểm thử policy trực tiếp từ tầng API | ĐÃ SỬA |
| BUG-07 | **P3** | Tham chiếu nhầm mã chức năng `F_APP_07` (đã loại bỏ khỏi phạm vi) trong mục Phân kỳ của `PROJECT_SCOPE.md` | ĐÃ SỬA |

---

## 14. Danh sách lỗi đã sửa (Bugs Fixed)

1. **BUG-01 (Compile Error):** Sửa `Policies.cs` sử dụng `RoleCodes.StaffRoles`, điều chỉnh using alias trong `DevController.cs`.
2. **BUG-02 (EF Core Model Error):** Cập nhật `AuditLogConfiguration.cs` chỉ định rõ navigation inverse `.WithMany(u => u.AuditLogs)`.
3. **BUG-03 (Database Configuration):** Cấu hình `appsettings.Development.json`, tạo `Properties/launchSettings.json`, tạo migration `20260929164033_InitialAuth` và áp dụng thành công.
4. **BUG-05 (Documentation Conflict):** Điều chỉnh `PROJECT_SCOPE.md` và `UAT_CHECKLIST.md` khẳng định rõ nghiệp vụ MOD_INV là gợi ý vật tư và nhân viên xác nhận thủ công, không tự động trừ kho.
5. **BUG-06 (Test Architecture):** Bổ sung tham chiếu `Dental.Api` vào `Dental.Tests.csproj` và viết bộ test `RbacPolicyTests` với 11 test cases.
6. **BUG-07 (Scope Cleanup):** Xóa tham chiếu `F_APP_07` khỏi `PROJECT_SCOPE.md`.

---

## 15. Lỗi còn tồn tại (Bugs Remaining)

1. **BUG-04 (Frontend Missing):** Chưa có thư mục frontend mã nguồn Vue.js. Cần scaffold dự án Vue 3 + Vite + TypeScript theo đúng kiến trúc đồ án.
2. **MOD_AUTH Endpoints:** Chưa triển khai chức năng đổi mật khẩu (`F_AUTH_03`) và Refresh Token (`F_AUTH_05`).

---

## 16. Các giới hạn đã biết (Known Limitations)

- Backend mới hoàn thiện nền tảng tầng Xác thực & Kiểm toán (Sprint 0).
- Hệ thống cơ sở dữ liệu hiện tại chỉ mới có các bảng thuộc phạm vi Auth và AuditLog (`Users`, `Roles`, `AuditLogs`). Các bảng nghiệp vụ (`Patients`, `Appointments`, `Visits`, `Invoices`, `Medicines`, `Materials`) sẽ được tạo qua các migration kế tiếp theo lộ trình Sprint.

---

## 17. Kết luận trạng thái hiện tại

- **Nền tảng backend .NET 10:** Đạt trạng thái hoạt động tốt (Clean build, 26/26 tests passed, Swagger UI sẵn sàng).
- **Cơ sở dữ liệu PostgreSQL:** Hoạt động ổn định, tự động migrate và seed dữ liệu chuẩn khi khởi động.
- **Bảo mật & Phân quyền:** Đã được kiểm chứng thực tế chống truy cập trái phép và bảo vệ đúng ma trận RBAC.
- **Độ sẵn sàng:** Đủ điều kiện để bước vào Sprint 1 (triển khai MOD_PAT và MOD_APP) cũng như scaffold frontend Vue.js.
