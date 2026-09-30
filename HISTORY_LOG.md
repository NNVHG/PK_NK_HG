# HISTORY_LOG.md — Lịch sử làm việc với AI Agent

> File này ghi lại các phiên làm việc có dẫn đến thay đổi thực tế trên code hoặc tài liệu.  
> **Luôn thêm vào cuối file (append) — không xóa hoặc ghi đè lịch sử cũ.**  
> Định dạng mỗi mục: xem `AGENTS.md` mục 8.

---

### [2026-09-27] - Rà soát toàn bộ .md theo nguồn sự thật xlsx v7 (72 chức năng, 12 module)

- **Câu hỏi/yêu cầu:** Rà soát tính nhất quán toàn bộ bộ file .md hiện có (README.md, AGENTS.md, docs/*.md) với 2 nguồn sự thật mới nhất: đề cương tốt nghiệp (docx) và file đặc tả chức năng v7 (xlsx). Ưu tiên xlsx khi mâu thuẫn.
- **Trả lời/đề xuất:** Phân tích 5 tiêu chí (công nghệ, module/chức năng, RBAC, phạm vi, chức năng đã loại). Phát hiện 8 file cần sửa, 2 file giữ nguyên. Thay đổi lớn nhất: loại MOD_ROS hoàn toàn, MOD_APP từ 8→2 chức năng, thêm F_PAT_09/F_CHK_07/F_CHK_08, cập nhật RBAC từ 15→12 nhóm.
- **Hành động đã thực hiện:**
  - Sửa: `README.md` — 84→72 chức năng, 13→12 module, xóa MOD_ROS
  - Sửa: `AGENTS.md` — xóa MOD_ROS khỏi danh sách prefix, 13→12 module
  - Sửa: `docs/PROJECT_SCOPE.md` — cập nhật nguồn sang v7, tổng 72/12, MOD_APP 2 chức năng, thêm F_PAT_09, thêm F_CHK_07/08, xóa F_CHK_06/F_RX_03/F_RPT_03/toàn bộ MOD_ROS
  - Sửa: `docs/ARCHITECTURE_OVERVIEW.md` — cập nhật workflow 5.1 (2 kênh đặt lịch, hàng đợi nội bộ, routing X-quang/nha sĩ, bỏ tái khám)
  - Sửa: `docs/DOMAIN_MODEL.md` — xóa section 1.10 Roster, thêm QueueEntry, thêm quy tắc F_PAT_09, cập nhật bảng quy tắc nghiệp vụ
  - Sửa: `docs/ROLE_PERMISSION_MATRIX.md` — tái cấu trúc từ 15→12 nhóm, xóa "Ca làm việc", thêm ghi chú đặc thù từng nhóm, thêm F_PAT_09, xóa F_RPT_03
  - Sửa: `docs/DATABASE_DESIGN.md` — xóa ROSTER section (Shifts/Rosters), thêm QueueEntries table, thêm index hàng đợi, thêm ghi chú F_PAT_09
  - Sửa: `docs/UAT_CHECKLIST.md` — MOD_APP từ 8→2 test case, thêm PAT-09, thêm CHK-07/08/09/10, xóa RPT-04 cũ (F_RPT_03)
  - Giữ nguyên: `docs/SECURITY_OVERVIEW.md`, `docs/DECISION_LOG.md`

---

### [2026-09-27] - Cập nhật MOD_INV sang bản chất hỗ trợ/cảnh báo; thêm quy tắc HISTORY_LOG

- **Câu hỏi/yêu cầu:** (A) Đổi MOD_INV từ "tự động trừ kho" sang "hỗ trợ/cảnh báo — nhân viên xác nhận thủ công". (B) Thêm quy tắc ghi HISTORY_LOG vào AGENTS.md, tạo file HISTORY_LOG.md.
- **Trả lời/đề xuất:** Phân tích 9 chức năng F_INV_01→F_INV_09. Chỉ F_INV_06 cần thay đổi bản chất (từ "Hệ thống" tự động → Phụ tá xác nhận). Các chức năng còn lại đã là thủ công hoặc cảnh báo thuần túy. Cập nhật RBAC Phụ tá thêm quyền xác nhận xuất kho.
- **Hành động đã thực hiện:**
  - Sửa: `AGENTS.md` — đổi mục 2.5 từ "Quy tắc BOM tự động" → "MOD_INV hỗ trợ/cảnh báo, không tự động hoàn toàn"; thêm mục 8 quy tắc HISTORY_LOG
  - Sửa: `docs/ARCHITECTURE_OVERVIEW.md` — đổi dòng workflow 5.1 "Tự động trừ kho BOM" → "Hệ thống gợi ý — Phụ tá xác nhận"; đổi tên và nội dung section 5.2
  - Sửa: `docs/DOMAIN_MODEL.md` — cập nhật quy tắc Inventory: bỏ "BOM tự động trigger", thêm mô tả gợi ý + xác nhận thủ công
  - Tạo: `HISTORY_LOG.md` (file này)


---

### [2026-09-29 23:45] - Sửa lỗi build/model EF Core, tạo migration InitialAuth, kích hoạt DB PostgreSQL và live test MOD_AUTH

- **Câu hỏi/yêu cầu:** Sửa các lỗi biên dịch backend .NET 10, cấu hình môi trường DB PostgreSQL, tạo migration khởi tạo, áp dụng seeder và kiểm tra hoạt động của module xác thực MOD_AUTH.
- **Trả lời/đề xuất:** 
  - Khắc phục lỗi tham chiếu namespace và role policy trong `DevController.cs` và `Policies.cs`.
  - Bổ sung các phương thức phụ trợ `Result.Success`, `Result.Failure`, `IsFailure` và các mã lỗi chuẩn dự án.
  - Sửa lỗi mapping shadow foreign key `AuditLog.UserId1` trong `AuditLogConfiguration.cs`.
  - Cấu hình thông số phát triển trong `appsettings.Development.json` và tạo `Properties/launchSettings.json`.
  - Tạo EF Core Migration `InitialAuth` và thực thi cập nhật DB PostgreSQL `dental_clinic`.
  - Chạy backend xác nhận luồng seeding tự động các vai trò (ADMIN, RECEPTIONIST, DENTIST, ASSISTANT, PATIENT) cùng người dùng mẫu.
  - Smoke test live API endpoints: `POST /api/auth/login`, `GET /api/auth/me`, kiểm tra ghi nhận audit log và mã phản hồi 401 khi sai mật khẩu.
- **Hành động đã thực hiện:**
  - Sửa: `backend/src/Dental.Api/Controllers/DevController.cs`
  - Sửa: `backend/src/Dental.Api/Policies/Policies.cs`
  - Sửa: `backend/src/Dental.Application/Common/Error.cs`
  - Sửa: `backend/src/Dental.Application/Common/Result.cs`
  - Sửa: `backend/src/Dental.Infrastructure/Data/Configurations/AuditLogConfiguration.cs`
  - Sửa: `backend/src/Dental.Api/appsettings.Development.json`
  - Tạo: `backend/src/Dental.Api/Properties/launchSettings.json`
  - Tạo: `backend/src/Dental.Infrastructure/Migrations/20260929164033_InitialAuth.cs`
  - Sửa: `HISTORY_LOG.md`

---

### [2026-09-29 23:55] - Đồng bộ tài liệu MOD_INV, bổ sung RBAC tests, smoke test phân quyền và hoàn tất báo cáo kiểm toán

- **Câu hỏi/yêu cầu:** Đồng bộ mâu thuẫn tài liệu về MOD_INV, thực hiện kiểm thử RBAC (401/403), hoàn thành bộ tài liệu `docs/TEST_EXECUTION_REPORT.md` (17 mục), `docs/RUN_GUIDE.md` (23 mục), cập nhật `README.md` và xác nhận toàn bộ test suite.
- **Trả lời/đề xuất:**
  - Sửa mâu thuẫn trong `docs/PROJECT_SCOPE.md` và `docs/UAT_CHECKLIST.md` (khẳng định bản chất MOD_INV là gợi ý vật tư và nhân viên xác nhận thủ công, xóa F_APP_07 thừa).
  - Thêm ProjectReference `Dental.Api` vào `Dental.Tests.csproj` và viết 11 test cases trong `backend/tests/Dental.Tests/Auth/RbacPolicyTests.cs` kiểm tra chính xác `AdminOnly` và `StaffAny` policies.
  - Chạy toàn bộ 26/26 unit tests đạt 100% pass.
  - Smoke test trực tiếp trên server: kiểm tra xác thực JWT (401 khi thiếu token), phân quyền RBAC (403 khi Bệnh nhân gọi Admin/Staff endpoint, 403 khi Phụ tá gọi Admin endpoint, 200 khi quyền hợp lệ).
  - Soạn thảo và khởi tạo tài liệu `docs/TEST_EXECUTION_REPORT.md` (đủ 17 mục) và `docs/RUN_GUIDE.md` (đủ 23 mục theo chuẩn đồ án).
  - Cập nhật mục 8 và mục 12 trong `README.md` dẫn liên kết tới tài liệu chạy và báo cáo kiểm thử.
- **Hành động đã thực hiện:**
  - Sửa: `docs/PROJECT_SCOPE.md`
  - Sửa: `docs/UAT_CHECKLIST.md`
  - Sửa: `backend/tests/Dental.Tests/Dental.Tests.csproj`
  - Tạo: `backend/tests/Dental.Tests/Auth/RbacPolicyTests.cs`
  - Tạo: `docs/TEST_EXECUTION_REPORT.md`
  - Tạo: `docs/RUN_GUIDE.md`
  - Sửa: `README.md`
  - Sửa: `HISTORY_LOG.md`

---

### [2026-09-30 00:30] - Sửa lỗ hổng NuGet, đồng bộ PostgreSQL, hoàn thiện MOD_AUTH, scaffold Vue 3 SPA và cập nhật RUN_GUIDE

- **Câu hỏi/yêu cầu:** Khắc phục triệt để 5 vấn đề tồn đọng từ phiên trước: (1) Đồng bộ user/pass PostgreSQL trong docker-compose.yml, (2) Khắc phục 16 cảnh báo NU1903 trong Dental.Infrastructure, (3) Hoàn thiện các endpoint F_AUTH_02 (logout) và F_AUTH_03 (change-password) kèm unit test, (4) Khởi tạo và scaffold toàn bộ frontend Vue 3 SPA theo kiến trúc đồ án, (5) Soạn thảo lại docs/RUN_GUIDE.md đầy đủ và rõ ràng.
- **Trả lời/đề xuất:**
  - Cập nhật `docker-compose.yml` đồng bộ với `appsettings.Development.json` (POSTGRES_USER: postgres, POSTGRES_PASSWORD: postgres).
  - Thay thế PackageReference lỗi thời `Microsoft.AspNetCore.Http.Abstractions` bằng `<FrameworkReference Include="Microsoft.AspNetCore.App" />` trong `Dental.Infrastructure.csproj`, loại bỏ hoàn toàn 16 cảnh báo bảo mật NU1903 (đạt 0 warning).
  - Bổ sung `ChangePasswordRequest`, `ChangePasswordRequestValidator`, triển khai `ChangePasswordAsync` và `LogoutAsync` trong `AuthService`, thêm 2 endpoint tương ứng vào `AuthController`, viết 4 unit test mới nâng tổng số test lên 30 (100% pass).
  - Khởi tạo thư mục `frontend/` với Vue 3, Vite, TypeScript, Pinia, Vue Router, cấu hình proxy `/api` sang backend cổng 5000, tạo các view LoginView, DashboardView, AppointmentsView, PatientsView, NotFoundView; chạy `npm run build` thành công 100%.
  - Cập nhật lại toàn diện `docs/RUN_GUIDE.md` mạch lạc, logic từ chuẩn bị DB, cấu hình, chạy migration/seed, khởi động backend, khởi chạy frontend và kiểm thử.
- **Hành động đã thực hiện:**
  - Sửa: `docker-compose.yml`
  - Sửa: `backend/src/Dental.Infrastructure/Dental.Infrastructure.csproj`
  - Sửa: `backend/src/Dental.Application/Common/Error.cs`
  - Tạo: `backend/src/Dental.Application/Features/Auth/DTOs/ChangePasswordRequest.cs`
  - Tạo: `backend/src/Dental.Application/Features/Auth/Validators/ChangePasswordRequestValidator.cs`
  - Sửa: `backend/src/Dental.Application/Features/Auth/Services/AuthService.cs`
  - Sửa: `backend/src/Dental.Api/Controllers/AuthController.cs`
  - Sửa: `backend/src/Dental.Api/Program.cs`
  - Sửa: `backend/tests/Dental.Tests/Auth/AuthServiceTests.cs`
  - Tạo: `frontend/package.json`
  - Tạo: `frontend/tsconfig.json`
  - Tạo: `frontend/tsconfig.node.json`
  - Tạo: `frontend/vite.config.ts`
  - Tạo: `frontend/index.html`
  - Tạo: `frontend/src/vite-env.d.ts`
  - Tạo: `frontend/src/main.ts`
  - Tạo: `frontend/src/App.vue`
  - Tạo: `frontend/src/services/api.ts`
  - Tạo: `frontend/src/stores/auth.ts`
  - Tạo: `frontend/src/router/index.ts`
  - Tạo: `frontend/src/views/LoginView.vue`
  - Tạo: `frontend/src/views/DashboardView.vue`
  - Tạo: `frontend/src/views/AppointmentsView.vue`
  - Tạo: `frontend/src/views/PatientsView.vue`
  - Tạo: `frontend/src/views/NotFoundView.vue`
  - Tạo: `frontend/.gitignore`
  - Sửa: `docs/RUN_GUIDE.md`
  - Sửa: `HISTORY_LOG.md`

---

### [2026-09-30 01:15] - Sửa lỗi HTTPS Redirection, bổ sung CORS origin, thêm UI đổi mật khẩu và viết lại RUN_GUIDE chi tiết

- **Câu hỏi/yêu cầu:** Kiểm tra, tìm và liệt kê toàn bộ các lỗi tiềm ẩn còn lại, sửa lỗi triệt để, sau đó viết lại tài liệu hướng dẫn chạy dự án chi tiết hơn phục vụ hội đồng chấm và người mới tiếp cận.
- **Trả lời/đề xuất:**
  - Phát hiện và sửa 4 vấn đề:
    1. Bọc `UseHttpsRedirection()` trong điều kiện `!app.Environment.IsDevelopment()` để tránh warning và redirect lỗi cổng Kestrel HTTP 5000.
    2. Bổ sung `http://127.0.0.1:5173` vào CORS policy `FrontendDev` để tránh lỗi CORS khi truy cập qua IP loopback.
    3. Thêm UI khối Đổi Mật Khẩu (F_AUTH_03) vào màn hình `DashboardView.vue` với validate khớp mật khẩu và hiển thị thông báo.
    4. Thống nhất mật khẩu mẫu trong tài liệu khớp chuẩn xác với `appsettings.Development.json` (`AdminPassword@123` và `DemoPassword@123`).
  - Viết lại toàn diện `docs/RUN_GUIDE.md` đủ 11 mục: Sơ đồ luồng dữ liệu, Cổng dịch vụ, Chuẩn bị môi trường, Khởi chạy DB, Cấu hình, Seed data, Chạy BE/FE, Kịch bản UAT trực quan trên Web, Kiểm thử bằng cURL/PowerShell, Chạy 30 unit tests và Bảng cẩm nang khắc phục 6 lỗi phổ biến nhất.
- **Hành động đã thực hiện:**
  - Sửa: `backend/src/Dental.Api/Program.cs`
  - Sửa: `frontend/src/views/DashboardView.vue`
  - Sửa: `docs/RUN_GUIDE.md`
  - Sửa: `HISTORY_LOG.md`

---

### [2026-09-30 01:30] - Khắc phục menu RBAC Frontend, thêm Healthcheck Docker, tạo Script start-dev.ps1 và hoàn thiện cẩm nang RUN_GUIDE

- **Câu hỏi/yêu cầu:** Kiểm tra, tìm và liệt kê toàn bộ các lỗi tiềm ẩn còn lại, khắc phục triệt để và làm lại file hướng dẫn chạy dự án chi tiết hơn phục vụ hội đồng chấm và thành viên mới.
- **Trả lời/đề xuất:**
  - Phát hiện 4 vấn đề:
    1. Menu điều hướng hiển thị link nhân viên cho vai trò bệnh nhân `PATIENT`. Sửa: thêm computed `isStaff` trong `frontend/src/App.vue`.
    2. Cấu hình `docker-compose.yml` thiếu `healthcheck`, dễ gây race condition khi backend khởi động cùng lúc. Sửa: thêm `healthcheck` pg_isready.
    3. Thiếu script tự động hóa khởi chạy 1 chạm cho môi trường Windows. Sửa: tạo `start-dev.ps1`.
    4. Tài liệu `RUN_GUIDE.md` trước đó bị nhầm lẫn về việc bắt buộc dùng user-secrets trong khi `appsettings.Development.json` đã có sẵn giá trị, thiếu hướng dẫn reset DB và kiểm tra qua pgAdmin/DBeaver. Sửa: viết lại toàn diện cẩm nang `docs/RUN_GUIDE.md` đủ 11 mục chuẩn mực.
  - Chạy kiểm tra: 30/30 unit test backend pass (100%), frontend `vue-tsc && vite build` thành công không cảnh báo.
- **Hành động đã thực hiện:**
  - Sửa: `frontend/src/App.vue`
  - Sửa: `docker-compose.yml`
  - Tạo: `start-dev.ps1`
  - Sửa: `docs/RUN_GUIDE.md`
  - Sửa: `HISTORY_LOG.md`


---

### [2026-09-30 01:50] - Tối ưu UX Dashboard theo vai trò, sửa Axios 401 SPA redirect và bổ sung hướng dẫn Swagger Bearer Token

- **Câu hỏi/yêu cầu:** Triển khai các sửa lỗi theo kế hoạch: ẩn thẻ chức năng với bệnh nhân trên Dashboard, chuyển interceptor 401 sang router.push tránh reload trắng trang, bổ sung chi tiết Swagger Authorize và kịch bản UAT phản biện 3 phút vào RUN_GUIDE.
- **Trả lời/đề xuất:**
  - Thêm computed `isStaff` và thay thế `modules-grid` bằng `patient-banner` trên `frontend/src/views/DashboardView.vue` đối với vai trò `PATIENT`. Cập nhật placeholder mật khẩu mới chuẩn format `Tối thiểu 6 ký tự: 1 hoa, 1 thường, 1 số`.
  - Tối ưu `frontend/src/services/api.ts`: thay `window.location.href = '/login'` bằng `router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })` để giữ trải nghiệm mượt mà của SPA và lưu lại route người dùng đang truy cập.
  - Bổ sung vào `docs/RUN_GUIDE.md`:
    - Mục 7: Chi tiết các bước xác thực nút **Authorize** với Bearer token trên giao diện Swagger UI.
    - Mục 8: Mẫu lệnh thực thi đầy đủ với JSON body cho cả PowerShell `Invoke-RestMethod` và `curl`.
    - Mục 9: Kịch bản phản biện UAT 3 phút cho Giảng viên / Hội đồng chấm đồ án.
  - Chạy xác minh: 30/30 unit tests backend pass (100%), frontend `vue-tsc && vite build` hoàn tất không phát sinh cảnh báo hay lỗi.
- **Hành động đã thực hiện:**
  - Sửa: `frontend/src/views/DashboardView.vue`
  - Sửa: `frontend/src/services/api.ts`
  - Sửa: `docs/RUN_GUIDE.md`
  - Sửa: `HISTORY_LOG.md`


### [2026-09-30 01:18] - Rà soát lỗi và viết lại hướng dẫn chạy project

- **Câu hỏi/yêu cầu:** Kiểm tra, liệt kê lỗi, sửa lỗi và viết chi tiết hơn hướng dẫn chạy project.
- **Trả lời/đề xuất:** Đối chiếu hướng dẫn với cấu hình, seed, API và frontend; sửa cách lưu JWT trái với quy tắc bảo mật và ghi các sai lệch thực tế vào hướng dẫn chạy.

---

### [2026-09-30 02:05] - Cập nhật .gitignore loại trừ file rác, untrack graphify-out và viết lại toàn diện README.md

- **Câu hỏi/yêu cầu:** Cập nhật file `.gitignore` không up git các file không liên quan đến đồ án, viết lại file `README.md` tổng của đồ án.
- **Trả lời/đề xuất:**
  - Bổ sung nhóm file hệ điều hành (`desktop.ini`, `Thumbs.db`, `.DS_Store`), thư mục AI Agent/tooling (`graphify-out/`, `skills/`, `.cursor/`, `.cline/`), cache frontend/backend (`.vite/`, `dist/`, `obj/`, `bin/`) và database backup (`*.dump`, `postgres-data/`) vào `.gitignore`.
  - Hủy theo dõi toàn bộ thư mục `graphify-out/` khỏi Git index (`git rm -r --cached graphify-out/`).
  - Viết lại toàn diện `README.md` chuẩn đồ án tốt nghiệp: Thông tin đề tài/SV/GVHD (ĐH Thủ Dầu Một), mục tiêu chuyển đổi số phòng khám nha khoa đơn lẻ, chuẩn FDI, kiến trúc Clean Architecture ASP.NET Core 10 + Vue 3, danh mục 12 module, bảng tài khoản seed, hướng dẫn chạy nhanh 1 chạm `start-dev.ps1`, kết quả 30 unit tests và chỉ mục tài liệu.
- **Hành động đã thực hiện:**
  - Sửa: `.gitignore`
  - Sửa: `README.md`
  - Xóa khỏi Git index: `graphify-out/`
  - Sửa: `HISTORY_LOG.md`


---

### [2026-09-30 02:40] - Hoàn thành Báo cáo Tuân thủ Đồ án (Audit) và Bộ Ma trận Kiểm thử 72 Chức năng

- **Câu hỏi/yêu cầu:** Thực hiện audit toàn diện độ tuân thủ đồ án tốt nghiệp PK_NK_HG, lập bộ ma trận và danh mục kiểm thử chi tiết cho 72 chức năng, 12 module, 5 vai trò theo file đặc tả `nội dung/chuc_nang_Gia_demo_8.xlsx`.
- **Trả lời/đề xuất:**
  - Đối chiếu trực tiếp với Sheet `03_Danh_sach_chuc_nang` trong file Excel v8, trích xuất chính xác 72 chức năng thuộc 12 module, chỉ ra 3 điểm lệch chuẩn tài liệu (`DOCUMENTATION_CONFLICT`).
  - Đánh giá hiện trạng Sprint 0: Đã hoàn thiện và vượt qua kiểm thử 100% (30/30 unit tests pass) cho `F_AUTH_02` và `F_AUTH_03`; `F_AUTH_06` có DB/Service; `F_APP_01`, `F_APP_02`, `F_PAT_01` có stub view; 66 chức năng còn lại theo kế hoạch Sprint 1 - 8.
  - Xây dựng trọn bộ 6 tài liệu kỹ thuật chuẩn mực vào thư mục `docs/`:
    1. `docs/PROJECT_COMPLIANCE_AUDIT.md`: Báo cáo tuân thủ đồ án toàn diện gồm đủ 22 mục bắt buộc theo MASTER PROMPT.
    2. `docs/TRACEABILITY_MATRIX.md`: Ma trận truy vết 72 chức năng qua từng tầng kiến trúc (UI, Logic, API, Service, DB, Validation, RBAC, Audit Log, Test Status).
    3. `docs/FULL_TEST_CHECKLIST.md`: Danh mục kiểm thử 1350 dòng bao phủ toàn diện 72 chức năng, 12 module, 5 roles, FDI, Kho, RBAC, IDOR, E2E.
    4. `docs/FULL_TEST_MATRIX.md`: Bảng ma trận kiểm thử tổng hợp 72 chức năng qua 12 khía cạnh kiểm thử.
    5. `docs/TEST_DATA_PLAN.md`: Kế hoạch dữ liệu kiểm thử chuẩn hóa cho 5 vai trò, FDI 32/20 răng, BOM kho, hóa đơn trả góp, dị ứng thuốc và payloads bảo mật.
    6. `docs/TEST_COVERAGE_REPORT.md`: Báo cáo độ phủ kiểm thử thực tế với bằng chứng 30/30 tests pass và frontend build thành công.
- **Hành động đã thực hiện:**
  - Tạo: `docs/PROJECT_COMPLIANCE_AUDIT.md`
  - Tạo: `docs/TRACEABILITY_MATRIX.md`
  - Tạo: `docs/FULL_TEST_CHECKLIST.md`
  - Tạo: `docs/FULL_TEST_MATRIX.md`
  - Tạo: `docs/TEST_DATA_PLAN.md`
  - Tạo: `docs/TEST_COVERAGE_REPORT.md`
  - Sửa: `HISTORY_LOG.md`

- **Hành động đã thực hiện:** Sửa `frontend/src/services/api.ts` và `frontend/src/stores/auth.ts` để chỉ giữ JWT trong bộ nhớ; viết lại `docs/RUN_GUIDE.md` theo cấu hình User Secrets, Docker Compose, migration tự động và phạm vi UI hiện có; xác nhận backend 30/30 test và frontend build thành công.