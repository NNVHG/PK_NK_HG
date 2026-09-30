# PROJECT COMPLIANCE AUDIT — BÁO CÁO TUÂN THỦ ĐỒ ÁN TOÀN DIỆN

> **Đề tài:** Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)  
> **Sinh viên thực hiện:** Nguyễn Ngô Vũ Hoàng Gia (MSSV: 2224802010628) — Lớp: D22CNTT02  
> **Giảng viên hướng dẫn:** T.s Ngô Thị Ngọc Diệu — Trường Đại học Thủ Dầu Một  
> **Thời điểm Audit:** 30/09/2026 (Kết thúc Sprint 0 - Khởi tạo nền tảng hạ tầng)  
> **Nguồn đối chiếu tối cao:** `nội dung/chuc_nang_Gia_demo_8.xlsx` (Sheet `03_Danh_sach_chuc_nang`), `docs/PROJECT_SCOPE.md`, `AGENTS.md`

---

## 1. Executive Summary (Tóm tắt điều hành)

Hệ thống quản lý phòng khám nha khoa Hoàng Gia được thiết kế nhằm số hóa toàn diện quy trình vận hành của một cơ sở khám chữa bệnh nha khoa đơn lẻ. Đồ án xây dựng trên nền tảng công nghệ hiện đại gồm **Vue 3 (Vite + TypeScript + Pinia)** ở phía Client và **ASP.NET Core 10 Web API (.NET 10 LTS + PostgreSQL + Entity Framework Core)** ở phía Backend.

Tại thời điểm đánh giá (30/09/2026), dự án vừa kết thúc **Sprint 0 (S0)** theo lộ trình `TIMELINE_DO_AN.md`. Hạ tầng cốt lõi đã được thiết lập vững chắc theo mô hình **Clean Architecture 4 tầng**:
- Đã thiết lập xong Authentication & Authorization đa vai trò với JWT Bearer Tokens.
- Đã hoàn thiện và kiểm thử đạt **100% (30/30 Unit Tests)** cho 2 chức năng nghiệp vụ nền tảng: `F_AUTH_02` (Đăng nhập & RBAC) và `F_AUTH_03` (Thông tin cá nhân & Đổi mật khẩu).
- Toàn bộ 70 chức năng nghiệp vụ còn lại (bao gồm 66 chức năng MVP và 4 chức năng Phase 2) đang nằm trong kế hoạch phát triển tuần tự từ Sprint 1 đến Sprint 8 (kết thúc 30/11/2026).
- Mã nguồn tuân thủ nghiêm ngặt quy tắc không hardcode dữ liệu giả trong production code, cấu trúc thư mục phân tách độc lập, CI build frontend đạt 100% không lỗi TypeScript.

---

## 2. Phân loại trạng thái 72 Chức năng

Dựa trên kết quả rà soát chi tiết mã nguồn Controller, Service, DbContext, Migration và Giao diện UI:

| Phân loại trạng thái | Số lượng | Tỷ lệ (%) | Chức năng cụ thể |
|---|---|---|---|
| **FULL (Đầy đủ End-to-End)** | 2 | 2.78% | `F_AUTH_02`, `F_AUTH_03` |
| **PARTIAL (Triển khai một phần)** | 4 | 5.56% | `F_AUTH_06` (Có DB & Service, thiếu UI Admin), `F_APP_01`, `F_APP_02`, `F_PAT_01` (Có Stub View) |
| **NOT IMPLEMENTED (Chưa triển khai)** | 66 | 91.66% | Toàn bộ 66 chức năng thuộc 11 module nghiệp vụ lâm sàng, viện phí và kho bãi |
| **NOT TESTED (Chưa kiểm thử)** | 69 | 95.83% | Các chức năng chưa code hoặc chỉ có stub UI |
| **COMPLIANT (Đạt chuẩn quy định)** | 2 | 2.78% | `F_AUTH_02`, `F_AUTH_03` đạt chuẩn Clean Architecture, BCrypt, RBAC, Validation |
| **DOCUMENTATION CONFLICT** | 3 | — | Sai lệch thống kê số lượng chức năng trên module giữa tài liệu cũ và Excel v8; Lệch phạm vi F_ASS_01; Lệch công nghệ so với đề cương cũ |

---

## 3. Audit Chi tiết 12 Module Nghiệp vụ

### 3.1. `MOD_AUTH` — Xác thực & Phân quyền (6 chức năng)
- `F_AUTH_01` (Đăng ký tài khoản): *Chưa triển khai*. Lên lịch Sprint 1.
- `F_AUTH_02` (Đăng nhập & RBAC): *Hoàn thành 100%*. Có LoginView, API, AuthService, DB Users/Roles, 11 tests pass.
- `F_AUTH_03` (Hồ sơ cá nhân & Đổi mật khẩu): *Hoàn thành 100%*. Có DashboardView, API me/change-password, 15 tests pass.
- `F_AUTH_04` (Quên/khôi phục mật khẩu): *Chưa triển khai*. Dự kiến Phase 2/Sprint 8.
- `F_AUTH_05` (Quản lý vai trò & quyền): *Chưa triển khai*. Lên lịch Sprint 1.
- `F_AUTH_06` (Audit Log): *Một phần*. Tự động lưu log vào DB `AuditLogs` khi login/đổi mật khẩu; chưa có màn hình tra cứu của Admin.

### 3.2. `MOD_PAT` — Quản lý Bệnh nhân & Hồ sơ (9 chức năng)
- `F_PAT_01` (Tiếp nhận & tạo hồ sơ): Mới có stub UI `PatientsView.vue`. Chưa có API/DB.
- `F_PAT_02` đến `F_PAT_09`: *Chưa triển khai*. Lên lịch Sprint 2 và Sprint 3. Hồ sơ y tế, cảnh báo dị ứng, tiền sử bệnh, upload tài liệu, timeline điều trị và cơ chế đóng băng hồ sơ (F_PAT_08).

### 3.3. `MOD_APP` — Quản lý Lịch hẹn (2 chức năng)
- `F_APP_01` (Đặt lịch hẹn khám) & `F_APP_02` (Quản lý & điều phối lịch hẹn): Mới có stub UI `AppointmentsView.vue`. Lên lịch triển khai toàn diện tại Sprint 3 (State Machine: Scheduled -> Confirmed -> Arrived -> InProgress -> Completed -> Cancelled).

### 3.4. `MOD_CHK` — Tiếp đón & Hàng đợi (7 chức năng)
- `F_CHK_01` đến `F_CHK_08`: *Chưa triển khai*. Lên lịch Sprint 3. Bao gồm check-in tại quầy, cấp số thứ tự, phân buồng/ghế khám, bảng điện tử hàng đợi thời gian thực.


### 3.5. `MOD_FDI` — Sơ đồ răng quốc tế FDI & Bệnh lý (7 chức năng)
- `F_FDI_01` đến `F_FDI_07`: *Chưa triển khai*. Trọng tâm Sprint 4 và 5. Tuân thủ tiêu chuẩn FDI: Bộ vĩnh viễn 32 răng (11-48), bộ sữa 20 răng (51-85), 5 mặt răng (B, L, M, D, O) và mô tả bệnh học ICD-10.

### 3.6. `MOD_IMG` — Quản lý Hình ảnh Nha khoa (4 chức năng)
- `F_IMG_01` đến `F_IMG_04`: *Chưa triển khai*. Lên lịch Sprint 6. Hỗ trợ tải lên ảnh X-quang, ảnh trước/sau điều trị, so sánh ảnh, lưu trữ an toàn. Loại trừ kết nối DICOM vật lý.

### 3.7. `MOD_RX` — Đơn thuốc Điện tử (4 chức năng)
- `F_RX_01`, `F_RX_02`, `F_RX_04`, `F_RX_05`: *Chưa triển khai*. Lên lịch Sprint 5. Kê đơn theo danh mục thuốc, in đơn chuẩn y tế, cảnh báo tương tác thuốc và dị ứng từ hồ sơ bệnh nhân.

### 3.8. `MOD_BIL` — Viện phí, Hóa đơn & Trả góp (7 chức năng)
- `F_BIL_01` đến `F_BIL_07`: *Chưa triển khai*. Lên lịch Sprint 6 và Sprint 8. Tính tiền dịch vụ, chiết khấu, VAT, thanh toán hỗn hợp (tiền mặt/chuyển khoản), lập kế hoạch trả góp nhiều kỳ (F_BIL_06, F_BIL_07).

### 3.9. `MOD_INV` — Quản lý Kho Dược & Vật tư tiêu hao (9 chức năng)
- `F_INV_01` đến `F_INV_09`: *Chưa triển khai*. Lên lịch Sprint 7. Tuân thủ quy tắc vàng: Gợi ý vật tư tiêu hao theo BOM khi hoàn tất dịch vụ, **chỉ xuất kho khi Phụ tá/Admin nhấn xác nhận thủ công trên UI**, tuyệt đối không tự động trừ tồn kho ngầm. Cảnh báo tồn tối thiểu và hạn dùng (F_INV_08).

### 3.10. `MOD_ASS` — Nghiệp vụ Phụ tá Nha khoa (6 chức năng)
- `F_ASS_01` đến `F_ASS_06`: *Chưa triển khai*. Lên lịch Sprint 7. Tiếp nhận ca phụ khám, chuẩn bị khay dụng cụ, xác nhận tiêu hao thực tế, ghi chú phụ tá.

### 3.11. `MOD_MST` — Quản lý Danh mục & Cấu hình Hệ thống (7 chức năng)
- `F_MST_01` đến `F_MST_07`: *Chưa triển khai*. Lên lịch Sprint 1 và 2. Danh mục dịch vụ, bảng giá, bệnh lý, danh mục thuốc, vật tư tiêu hao, ghế nha khoa và người dùng nhân viên.

### 3.12. `MOD_RPT` — Báo cáo & Thống kê Quản trị (4 chức năng)
- `F_RPT_01`, `F_RPT_02`, `F_RPT_04`, `F_RPT_05`: *Chưa triển khai*. Lên lịch Sprint 8. Thống kê doanh thu, hiệu suất bác sĩ, báo cáo xuất nhập tồn kho và tỷ lệ tiêu hao theo dịch vụ.

---

## 4. Audit 5 Vai trò & Ma trận RBAC

Hệ thống quy định chặt chẽ 5 vai trò theo `docs/ROLE_PERMISSION_MATRIX.md`:
1. **Admin (Quản trị viên):** Toàn quyền cấu hình danh mục, quản lý người dùng, xem audit log, báo cáo tài chính.
2. **Receptionist (Lễ tân / Thu ngân):** Tiếp đón, đặt lịch hẹn, check-in bệnh nhân, lập hóa đơn và thu tiền viện phí.
3. **Dentist (Nha sĩ):** Khám lâm sàng, ghi nhận sơ đồ răng FDI, chỉ định cận lâm sàng, kê đơn thuốc và lập phác đồ điều trị.
4. **Assistant (Phụ tá):** Hỗ trợ bác sĩ trong buồng khám, chụp ảnh, chuẩn bị dụng cụ, xác nhận xuất kho vật tư tiêu hao.
5. **Patient (Bệnh nhân):** Đăng ký tài khoản, xem lịch sử khám của chính mình, tra cứu hóa đơn và lịch hẹn cá nhân.

*Đánh giá hiện trạng:* Backend đã hiện thực xác thực JWT và kiểm tra Role claim trong `AuthService` và `AuthController`. Frontend đã có Navigation Guard (`router.beforeEach`) kiểm tra trạng thái đăng nhập và quyền truy cập route.

---

## 5. Kiến trúc Kỹ thuật & Clean Architecture

Dự án áp dụng mô hình **Clean Architecture 4 tầng** chuẩn mực của Microsoft:
1. `Dental.Domain`: Định nghĩa Entity cốt lõi (`User`, `Role`, `AuditLog`, `BaseEntity`), Domain Events, độc lập hoàn toàn với framework và thư viện ngoài.
2. `Dental.Application`: Chứa Business Logic, DTOs, FluentValidation (`LoginRequestValidator`, `ChangePasswordRequestValidator`), CQRS/Services interfaces (`IAuthService`).
3. `Dental.Infrastructure`: Triển khai EF Core `DentalDbContext`, Password Hasher (BCrypt.Net), Token Generator (JWT), Database Migrations với PostgreSQL (Npgsql).
4. `Dental.Api`: ASP.NET Core Web API Controllers, cấu hình Dependency Injection, Middleware xử lý ngoại lệ toàn cục, cấu hình Swagger/OpenAPI.

*Đánh giá:* Phân tách Dependency Inversion hoàn hảo. Không có rò rỉ công nghệ tầng dưới lên Domain.

---

## 6. Chuẩn Nha khoa FDI (FDI Dental Standard)

- **Quy định bắt buộc:** 
  - Người lớn: 32 răng vĩnh viễn (ký hiệu FDI 11-18, 21-28, 31-38, 41-48).
  - Trẻ em: 20 răng sữa (ký hiệu FDI 51-55, 61-65, 71-75, 81-85).
  - Mỗi răng quản lý độc lập 5 bề mặt giải phẫu: `M` (Mesial), `D` (Distal), `O` (Occlusal), `B` (Buccal), `L` (Lingual).

---

## 7. Quy tắc Kho & BOM (Định mức vật tư)

- **Nguyên tắc cốt lõi (AGENTS.md điều 2.5):**
  - Khi hoàn tất dịch vụ khám chữa bệnh, hệ thống căn cứ bảng định mức BOM để **gợi ý danh sách vật tư tiêu hao**.
  - **Nghiêm cấm tự động trừ kho ngầm trong cơ sở dữ liệu**.
  - Phụ tá hoặc Quản trị viên bắt buộc phải kiểm tra, điều chỉnh số lượng thực tế và **bấm nút xác nhận** trên màn hình UI. Khi đó hệ thống mới ghi nhận phiếu xuất kho (`StockIssue`) và giảm tồn kho.
  - Cảnh báo tồn kho dưới mức tối thiểu chỉ mang tính chất hiển thị thông báo, không tự sinh đơn đặt hàng.
- **Hiện trạng tuân thủ:** Đã đưa vào Test Matrix và Test Data Plan, sẵn sàng triển khai đúng luật tại Sprint 7.

---

## 8. Quản lý Lịch hẹn & Hàng đợi tiếp đón (State Machine)

Quy trình tiếp đón được kiểm soát qua máy trạng thái (State Machine):
`Lên lịch (Scheduled)` -> `Xác nhận (Confirmed)` -> `Đã đến / Check-in (Arrived)` -> `Đang khám (InProgress)` -> `Hoàn thành (Completed)` / `Hủy hẹn (Cancelled)`.
- Chặn xếp lịch chồng chéo cùng một ghế hoặc cùng một nha sĩ tại cùng một khung giờ.
- Hàng đợi phân luồng tự động theo giờ hẹn và mức độ ưu tiên cấp cứu.
- *Hiện trạng:* Đã có stub view `AppointmentsView.vue`; logic Backend sẽ hoàn thiện tại Sprint 3.

---

## 9. Hồ sơ Bệnh nhân, Tiền sử & Cảnh báo Dị ứng

- Mỗi bệnh nhân có một mã hồ sơ định danh duy nhất (`PAT_xxxx`).
- Bắt buộc kiểm tra tiền sử bệnh lý tim mạch, huyết áp, đái tháo đường và dị ứng thuốc (đặc biệt là dị ứng kháng sinh và thuốc tê).
- Cảnh báo an toàn lâm sàng phải hiển thị biểu tượng cảnh báo màu đỏ nổi bật trên đầu hồ sơ khám và màn hình kê đơn thuốc của nha sĩ.
- Cơ chế khóa hồ sơ y tế (`F_PAT_08`): Khi kết thúc ca khám và xuất hóa đơn, hồ sơ bệnh án chuyển sang trạng thái đóng băng (Read-Only) để đảm bảo tính pháp lý, chỉ Admin có quyền mở khóa đặc biệt kèm Audit Log.

---

## 10. Kê đơn thuốc & Tương tác thuốc

- Tích hợp danh mục thuốc chuẩn (`MOD_RX`, `MOD_MST`).
- Kiểm tra tự động chéo: Thuốc được kê có xung đột với tiền sử dị ứng đã khai báo của bệnh nhân hay không. Nếu có xung đột, hệ thống bắt buộc hiển thị Popup cảnh báo mức độ nghiêm trọng và yêu cầu bác sĩ xác nhận lý do nếu vẫn quyết định kê.
- Đơn thuốc sinh mã vạch / mã định danh hỗ trợ in ấn theo mẫu quy định của Bộ Y tế.

---

## 11. Hóa đơn, Viện phí & Trả góp

- Tính toán viện phí tự động từ các dịch vụ đã thực hiện trên sơ đồ FDI và đơn thuốc.
- Hỗ trợ chiết khấu chương trình khuyến mãi (chặn chiết khấu âm hoặc chiết khấu vượt quá 100% giá trị dịch vụ).
- Cơ chế chia kỳ trả góp (`F_BIL_06`, `F_BIL_07`): Cho phép chia thành nhiều đợt thanh toán (đặc biệt cho dịch vụ chỉnh nha, phục hình, cấy ghép implant). Quản lý chính xác công nợ từng đợt, ngày đến hạn và gửi nhắc nhở.

---

## 12. Quản lý Hình ảnh Nha khoa

- Lưu trữ ảnh X-quang Panorex, Cephalo, Periapical và ảnh chụp cận cảnh trong miệng trước/sau điều trị.
- Lưu trữ file trên hệ thống tập tin nội bộ (Local Secure Storage) hoặc Blob Storage, mã hóa đường dẫn và phân quyền truy cập thông qua API có kiểm tra quyền sở hữu. Không public trực tiếp đường dẫn file ra ngoài.
- Ngoài phạm vi: Không tích hợp trực tiếp máy quét DICOM phần cứng (tuân thủ mục Ngoài phạm vi của đồ án).


---

## 13. Báo cáo & Thống kê Quản trị

- Báo cáo doanh thu theo mốc thời gian (ngày, tuần, tháng, quý, năm) và theo phương thức thanh toán.
- Báo cáo hiệu suất công việc của từng bác sĩ (số ca khám, doanh thu mang lại).
- Báo cáo biến động kho vật tư (xuất - nhập - tồn) và đối soát chênh lệch định mức tiêu hao thực tế so với BOM lý thuyết.
- Toàn bộ báo cáo chỉ mở quyền truy cập cho vai trò `Admin`.

---

## 14. Bảo mật, Xác thực & Quyền sở hữu Dữ liệu (IDOR)

- **Mật khẩu:** Bắt buộc băm bằng thuật toán BCrypt với độ muối (work factor) tiêu chuẩn. Nghiêm cấm lưu mật khẩu plain-text hoặc MD5/SHA1.
- **Xác thực:** JWT Token ký số HMAC-SHA256, thời hạn sống ngắn (30 phút), chứa Claim định danh (`UserId`, `Role`, `Phone`).
- **Phòng chống IDOR (Insecure Direct Object References):** Mọi endpoint truy xuất dữ liệu bệnh nhân (`/api/patients/{id}/...`) phải kiểm tra quyền sở hữu: Bệnh nhân chỉ được đọc dữ liệu có `PatientId` trùng với claim `UserId` trong token của chính họ; nhân viên y tế được truy cập theo phạm vi ca trực được phân công.
- **Phân quyền hai lớp (Defense in Depth):** Kiểm tra ở cả tầng Client (Vue Router Guard, ẩn hiện menu) và tầng Backend (`[Authorize(Roles = "...")]` attributes trên Controller/Action).

---

## 15. Kiểm tra Hợp lệ Dữ liệu (Validation Framework)

- **Backend:** Áp dụng FluentValidation kiểm tra tự động trước khi request đi vào Controller Service:
  - Kiểm tra độ dài, regex số điện thoại Việt Nam (10 chữ số).
  - Kiểm tra độ phức tạp mật khẩu: tối thiểu 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.
  - Chặn giá trị âm cho đơn giá, số lượng vật tư, số tiền thanh toán.
- **Frontend:** Kiểm tra form thời gian thực bằng regex và hiển thị tooltip lỗi trước khi gửi HTTP request.

---

## 16. Audit Log & Giám sát Hệ thống

- Mọi thao tác trọng yếu (Đăng nhập, đăng nhập thất bại, đổi mật khẩu, thay đổi tồn kho, điều chỉnh hóa đơn, đóng/mở hồ sơ bệnh án) đều được ghi nhận vào bảng `AuditLogs`.
- Dữ liệu lưu vết: `UserId`, `Action`, `EntityName`, `EntityId`, `OldValues`, `NewValues`, `IpAddress`, `UserAgent`, `Timestamp`.
- Bản ghi Audit Log là bất biến (Append-Only), không cung cấp API chỉnh sửa hoặc xóa log.

---

## 17. Cơ sở Dữ liệu & EF Core Migrations

- Hệ quản trị CSDL: **PostgreSQL** kết nối qua Npgsql Entity Framework Core Provider.
- Thiết kế đạt chuẩn hóa 3NF, bảo đảm toàn vẹn dữ liệu thông qua Foreign Keys và Unique Indexes (`IX_Users_Phone`).
- Quản lý lược đồ CSDL bằng EF Core Code-First Migrations.
- Lịch sử Migration hiện tại: `InitialCreate` (Bảng Users, Roles, AuditLogs).
- Quy tắc: Mọi thay đổi cấu trúc bảng mới phải sinh Migration mới, không chỉnh sửa trực tiếp vào database production.

---

## 18. Bộ Kiểm thử & Minh chứng Tự động hóa

- **Backend Unit Tests:** Thực hiện qua bộ test runner xUnit:
  - Tổng số bài kiểm thử: **30 bài**.
  - Kết quả: **30/30 Pass** (100% thành công, 0 Failed, 0 Skipped).
  - Thời gian chạy: 5.0 giây.
- **Frontend Build Pipeline:**
  - Lệnh kiểm tra: `vue-tsc && vite build`.
  - Kết quả: Biên dịch thành công 100% trong 896ms, không có lỗi kiểu dữ liệu TypeScript.
- **Bằng chứng kiểm thử:** Được lưu vết đầy đủ trong `docs/TEST_COVERAGE_REPORT.md` và `docs/FULL_TEST_MATRIX.md`.


---

## 19. Mâu thuẫn Tài liệu & Điểm lệch chuẩn (Documentation Conflicts)

Trong quá trình audit toàn diện, đã phát hiện 3 điểm lệch chuẩn tài liệu cần được lưu vết và làm rõ:
1. **Lệch số lượng chức năng trên từng Module:** Một số tài liệu cũ ghi MOD_APP có 8 chức năng, MOD_PAT có 7 chức năng, MOD_CHK có 5 chức năng. Tuy nhiên, theo file đặc tả chuẩn mới nhất `chuc_nang_Gia_demo_8.xlsx` (Sheet `03_Danh_sach_chuc_nang`) và `docs/PROJECT_SCOPE.md`, phân bổ chính xác là: MOD_AUTH: 6, MOD_PAT: 9, MOD_APP: 2, MOD_CHK: 7, MOD_FDI: 7, MOD_IMG: 4, MOD_RX: 4, MOD_BIL: 7, MOD_INV: 9, MOD_ASS: 6, MOD_MST: 7, MOD_RPT: 4. Tổng cộng chuẩn xác: **72 chức năng**.
2. **Xếp loại phạm vi F_ASS_01:** `PROJECT_SCOPE.md` xếp `F_ASS_01` vào MVP, trong khi `TIMELINE_DO_AN.md` tạm xếp vào Phase 2 để giảm tải cho Sprint 7. Khuyến nghị giữ nguyên MVP và triển khai bản đơn giản hóa.
3. **Thay đổi Tech Stack so với Đề cương ban đầu:** Đề cương bảo vệ tốt nghiệp đăng ký ReactJS + Flask + MySQL/SQL Server. Dự án đã chuyển đổi thành công sang Vue 3 + ASP.NET Core 10 + PostgreSQL. Cần hoàn tất văn bản giải trình lý do kỹ thuật (Clean Architecture, kiểu dữ liệu mạnh Type-Safe, khả năng mở rộng) để trình GVHD T.s Ngô Thị Ngọc Diệu phê duyệt chính thức.

---

## 20. Các Hạng mục Ngoài Phạm vi (Out of Scope)

Nhằm đảm bảo tính khả thi cho đồ án tốt nghiệp sinh viên đơn lẻ, các hạng mục sau được xác định rõ ràng nằm ngoài phạm vi thực hiện:
- Không tích hợp hệ thống Bảo hiểm Y tế (BHYT) quốc gia.
- Không kết nối phần cứng máy chụp DICOM hoặc máy X-quang vật lý.
- Không quản lý chuỗi phòng khám đa chi nhánh (chỉ áp dụng phòng khám đơn lẻ).
- Không tự động kết nối cổng thanh toán ngân hàng (chỉ ghi nhận hóa đơn thủ công).
- Không tích hợp SMS Brandname / Tổng đài viễn thông thực (chỉ hiển thị thông báo nội bộ).
- Không xây dựng phân hệ chấm công tính lương (HRM).
- Không triển khai module quản lý ca trực nhân sự (MOD_ROS - đã loại bỏ theo thỏa thuận đề tài).

---

## 21. Đánh giá Rủi ro & Ma trận Rủi ro (Risk Assessment)

| Rủi ro | Mức độ | Khả năng xảy ra | Biện pháp giảm thiểu |
|---|---|---|---|
| **Quá tải khối lượng công việc (68 chức năng MVP trong 8 tuần)** | Rất cao | Cao | Phân tầng ưu tiên T1/T2/T3 theo TIMELINE_DO_AN.md; sẵn sàng cắt giảm xuống core flow nếu trễ quá 3 ngày |
| **Xung đột thời gian với NCKH sinh viên (bắt đầu 10/10)** | Cao | Chắc chắn | Hoàn tất Sprint 0 và Sprint 1 sớm; tập trung làm giao diện mẫu trước, ghép API sau |
| **Mâu thuẫn công nghệ khi bảo vệ với Hội đồng chấm** | Trung bình | Thấp | Chuẩn bị sẵn phụ lục so sánh kỹ thuật chứng minh tính vượt trội của ASP.NET Core 10 và PostgreSQL |
| **Phức tạp khi vẽ sơ đồ răng FDI trên Web** | Cao | Trung bình | Sử dụng đồ họa SVG tương tác cho 32 răng người lớn và 20 răng trẻ em; tách component riêng biệt |
| **Nhầm lẫn trong logic trừ kho tự động** | Trung bình | Thấp | Tuân thủ tuyệt đối AGENTS.md 2.5: chỉ hiển thị bảng gợi ý BOM, bắt buộc phụ tá nhấn xác nhận thủ công |

---

## 22. Kết luận Đồ án & Kế hoạch Hành động Sprint 1 - Sprint 8

### Kết luận tổng kết:
Dự án **Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)** đang phát triển đúng hướng, bám sát các tiêu chuẩn kỹ thuật của một đồ án tốt nghiệp xuất sắc ngành Công nghệ Thông tin tại Trường Đại học Thủ Dầu Một. Nền tảng hạ tầng kiến trúc Clean Architecture, cơ chế xác thực JWT/RBAC và quy trình tự động hóa kiểm thử đã được thiết lập chuẩn chỉ ngay từ Sprint 0.

### Kế hoạch hành động chi tiết các Sprint tiếp theo:
- **Sprint 1 (05/10 - 11/10):** Triển khai `F_AUTH_01` (Đăng ký BN), `F_AUTH_05` (Quản lý quyền), `F_AUTH_06` (Giao diện Audit Log Admin), `F_MST_07` (Quản lý NV), `F_MST_01` (Danh mục dịch vụ & Bảng giá).
- **Sprint 2 (12/10 - 18/10):** Triển khai trọn vẹn `MOD_PAT` (Hồ sơ BN, tiền sử bệnh, dị ứng) và danh mục bệnh lý, thuốc, ghế khám (`MOD_MST`).
- **Sprint 3 (19/10 - 25/10):** Hoàn thiện phân hệ Đặt lịch khám (`MOD_APP`) và Tiếp đón / Hàng đợi (`MOD_CHK`).
- **Sprint 4 & 5 (26/10 - 08/11):** Xây dựng Phân hệ Khám lâm sàng cốt lõi: Sơ đồ răng quốc tế FDI 32/20 răng SVG (`MOD_FDI`), Kê đơn thuốc điện tử (`MOD_RX`), Khóa hồ sơ bệnh án (`F_PAT_08`).
- **Sprint 6 (09/11 - 15/11):** Triển khai Viện phí, Hóa đơn (`MOD_BIL`) và Quản lý Hình ảnh nha khoa (`MOD_IMG`).
- **Sprint 7 (16/11 - 22/11):** Triển khai Quản lý Kho vật tư tiêu hao (`MOD_INV`) theo quy tắc gợi ý BOM và Nghiệp vụ Phụ tá (`MOD_ASS`).
- **Sprint 8 (23/11 - 29/11):** Xây dựng Báo cáo Quản trị (`MOD_RPT`), Quản lý Trả góp (`F_BIL_06`, `F_BIL_07`), Chạy toàn bộ UAT checklist và đóng gói sản phẩm.
- **Ngày 30/11/2026:** Freeze toàn bộ hệ thống, chuẩn bị tài liệu thuyết minh và dữ liệu demo bảo vệ trước Hội đồng.

- **Hiện trạng tuân thủ:** Đã định nghĩa chuẩn dữ liệu trong `docs/TEST_DATA_PLAN.md` và `docs/FULL_TEST_CHECKLIST.md`. Mã nguồn thực tế sẽ triển khai tại Sprint 4 và 5. Tuyệt đối không dùng ký hiệu Palmer hay Universal.
