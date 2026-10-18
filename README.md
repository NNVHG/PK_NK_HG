# Hệ thống Quản Lý Phòng Khám Nha Khoa Hoàng Gia (PK_NK_HG)

> **Đồ án tốt nghiệp ngành Công nghệ Thông tin**  
> **Trường Đại học Thủ Dầu Một — Viện Công nghệ số**  
> **Sinh viên thực hiện:** Nguyễn Ngô Vũ Hoàng Gia — **MSSV:** 2224802010628 — **Lớp:** D22CNTT02  
> **Giảng viên hướng dẫn:** TS. Ngô Thị Ngọc Dịu  
> **Trạng thái hiện hành (`main @ d10ecffa`):** Phạm vi chính thức gồm **52 chức năng trên 12 phân hệ và 5 vai trò** (theo nguồn chuẩn `nội dung/chuc_nang_Gia_demo_11.xlsx`). Đã tích hợp 46/52 chức năng cốt lõi trên `main` (**1514/1514 .NET tests PASS**, **651/651 Node tests PASS**, **559/559 PostgreSQL HTTP integration tests PASS**, 38 EF Core migrations).

---

## 1. Mục tiêu và Phạm vi đồ án

Hệ thống cung cấp giải pháp chuyển đổi số toàn diện cho **phòng khám nha khoa đơn lẻ** (Phòng khám Nha khoa Hoàng Gia), bám sát 100% Đề cương tốt nghiệp:
- **Chuẩn hóa hồ sơ răng:** Ứng dụng tiêu chuẩn FDI quốc tế (32 răng người lớn: 11–48; 20 răng trẻ em: 51–85) với chi tiết 5 mặt răng (`MOD_FDI`).
- **Phân quyền chặt chẽ (RBAC):** Kiểm soát truy cập nghiêm ngặt giữa 5 vai trò: `Admin`, `Lễ tân/Thu ngân`, `Nha sĩ`, `Phụ tá`, `Bệnh nhân` (`MOD_AUTH`).
- **Minh bạch tài chính & trả nhiều đợt:** Hỗ trợ thu tiền một lần hoặc **thu thành nhiều đợt (trả góp theo tiến độ các dịch vụ điều trị dài ngày)**, theo dõi số tiền đã đóng (`PaidAmount`), dư nợ còn lại (`RemainingAmount`) và cảnh báo tổng công nợ khi tái khám (`F_BIL_03`, `F_CHK_01`).
- **Quản lý kho an toàn:** Hỗ trợ nhập kho (`F_INV_03`) và gợi ý vật tư tiêu hao khi hoàn tất dịch vụ, bắt buộc Phụ tá/Admin xác nhận thủ công trước khi trừ tồn kho (`F_INV_06`).
- **Phạm vi loại trừ:** Không tích hợp BHYT; không xử lý chuẩn DICOM gốc (lưu trữ tệp hình ảnh X-quang JPG/PNG thông thường); không quản lý ca làm việc/lịch trực nhân sự (`MOD_ROS`).

---

## 2. Kiến trúc giải pháp & Công nghệ sử dụng

Hệ thống được xây dựng theo kiến trúc phân tầng Clean Architecture, tách biệt rõ ràng giữa Business Logic và Giao diện:

```text
[Trình duyệt Web - Single Page Application]
       │
       ▼ (RESTful API / JSON / JWT Bearer)
[Backend API: ASP.NET Core 10 Web API]
  ├── Dental.Api             (Controllers, Middlewares, Dependency Injection)
  ├── Dental.Application     (Features, DTOs, Business Logic, FluentValidation)
  ├── Dental.Domain          (Entities, Enums, Value Objects)
  └── Dental.Infrastructure  (EF Core Npgsql, Database Context, Seeders, Security)
       │
       ▼ (Npgsql Connection String)
[Hệ Quản trị Cơ sở Dữ liệu: PostgreSQL 16]
```

### Tech Stack chi tiết:
- **Backend:** ASP.NET Core 10, C# 13, Entity Framework Core 10 (PostgreSQL Npgsql provider), FluentValidation, BCrypt.Net-Next.
- **Frontend:** Vue 3 (Composition API, `<script setup>`), TypeScript, Vite, Pinia Store, Vue Router 4, Axios.
- **Database:** PostgreSQL 16 chạy container hóa qua Docker Compose (có cấu hình healthcheck tự động).
- **Kiểm thử tự động:** xUnit, FluentAssertions, Moq, Node Test Runner.

---

## 3. Danh mục 12 Module & 52 Chức năng chính thức (`chuc_nang_Gia_demo_11.xlsx`)

| Mã Module | Tên Module | Số CN | Trách nhiệm chính |
|---|---|:---:|---|
| `MOD_AUTH` | Xác thực & Phân quyền | **6** | Đăng ký bệnh nhân, Đăng nhập JWT & RBAC, Hồ sơ cá nhân & đổi mật khẩu, Khôi phục mật khẩu OTP (`F_AUTH_04`), Quản lý vai trò/quyền, Nhật ký Audit Log |
| `MOD_PAT` | Bệnh nhân & Hồ sơ | **9** | Tạo hồ sơ (kèm nhập/đối soát CCCD SHA-256), Tìm kiếm nhanh, Kiểm tra trùng, Tiền sử & dị ứng, Sinh hiệu, Cảnh báo an toàn, Lịch sử điều trị timeline, Khóa/chốt hồ sơ, Tạo bản ghi khám mới (`Visit`) |
| `MOD_APP` | Đặt lịch bệnh nhân | **2** | Đặt lịch trực tuyến (`F_APP_01`) và Đặt lịch tại quầy do Lễ tân thao tác hộ (`F_APP_02`, kèm ca cấp cứu & nhắc hẹn) |
| `MOD_CHK` | Tiếp đón & Hàng đợi | **6** | Xác nhận khách đến (kèm cảnh báo công nợ), Hàng đợi chung, Gọi số & màn hình TV, Quản lý trạng thái tiến trình, Điều phối Nha sĩ/X-quang (gộp chỉ định chụp), Bắt đầu/kết thúc lần khám |
| `MOD_FDI` | Sơ đồ răng FDI | **7** | Hiển thị sơ đồ 32/20 răng chuẩn FDI, Chọn răng & mặt răng, Gán tình trạng bệnh lý, Chỉ định dịch vụ theo răng, Cập nhật sau điều trị, Lịch sử trạng thái răng, Ghi chú lâm sàng theo răng |
| `MOD_IMG` | Hình ảnh hồ sơ | **2** | Tải lên tệp ảnh X-quang/chụp trong miệng gắn với lần khám/răng (`F_IMG_02` đơn giản hóa) và Xem/thu phóng ảnh (`F_IMG_04` đơn giản hóa) |
| `MOD_RX` | Đơn thuốc | **4** | Kê đơn thuốc điện tử (kèm cảnh báo dị ứng), Kiểm tra tồn/an toàn thuốc, In/xuất đơn thuốc A5 (kèm mã QR tra cứu), Lịch sử đơn thuốc |
| `MOD_BIL` | Thanh toán & Hóa đơn | **5** | Tạo hóa đơn nháp từ dịch vụ & đơn thuốc, Áp dụng giảm giá/khuyến mãi, Thu tiền viện phí & theo dõi các đợt trả (hỗ trợ trả nhiều đợt cho dịch vụ dài ngày), In/xuất biên lai K80 & hoàn tiền, Hủy/điều chỉnh hóa đơn nháp |
| `MOD_INV` | Kho & Vật tư | **2** | Lập phiếu nhập kho vật tư kèm cảnh báo tồn tối thiểu (`F_INV_03` đơn giản hóa) và Gợi ý/xác nhận trừ tồn kho vật tư tiêu hao khi hoàn tất dịch vụ (`F_INV_06` đơn giản hóa) |
| `MOD_ASS` | Nghiệp vụ phụ tá | **1** | Cập nhật và theo dõi trạng thái ghế/phòng điều trị (`F_ASS_06` đơn giản hóa) |
| `MOD_MST` | Danh mục hệ thống | **7** | Quản lý dịch vụ & bảng giá, Danh mục bệnh lý/triệu chứng, Danh mục thuốc, Danh mục vật tư & ngưỡng tồn, Danh mục phòng & ghế nha khoa, Cấu hình hệ thống (`F_MST_06` đơn giản hóa), Quản lý người dùng nhân viên |
| `MOD_RPT` | Báo cáo & Thống kê | **1** | Báo cáo tổng hợp doanh thu và công nợ phòng khám theo kỳ, phương thức thanh toán (`F_RPT_01`) |

---

## 4. Danh sách tài khoản thử nghiệm (Seed Data)

Khi khởi chạy ở môi trường `Development`, hệ thống tự động khởi tạo 5 tài khoản mẫu cho 5 vai trò hệ thống:

| Vai trò | Mã quyền (`RoleCode`) | Số điện thoại đăng nhập | Mật khẩu mặc định |
|---|---|---|---|
| **Quản trị viên** | `ADMIN` | `0900000001` | `AdminPassword@123` |
| **Lễ tân / Thu ngân** | `RECEPTIONIST` | `0900000002` | `DemoPassword@123` |
| **Nha sĩ** | `DENTIST` | `0900000003` | `DemoPassword@123` |
| **Phụ tá** | `ASSISTANT` | `0900000004` | `DemoPassword@123` |
| **Bệnh nhân** | `PATIENT` | `0900000005` | `DemoPassword@123` |

---

## 5. Hướng dẫn Khởi chạy Hệ thống

### Cách 1: Khởi chạy nhanh 1 chạm (Khuyên dùng trên Windows)
Yêu cầu đã bật Docker Desktop. Tại thư mục gốc dự án:
```powershell
.\start-dev.ps1
```
*Script sẽ tự khởi động container PostgreSQL, kích hoạt Backend API trên cổng 5000 và Frontend Web trên cổng 5173.*

### Cách 2: Khởi chạy từng bước thủ công
1. **Khởi động Cơ sở dữ liệu:**
   ```powershell
   docker compose up -d postgres
   ```
2. **Khởi động Backend Web API (Terminal 1):**
   ```powershell
   dotnet run --project backend/src/Dental.Api
   ```
   *Swagger UI:* `http://localhost:5000/swagger`
3. **Khởi động Frontend SPA (Terminal 2):**
   ```powershell
   cd frontend
   npm install
   npm run dev
   ```
   *Web App:* `http://localhost:5173`

> Hướng dẫn cấu hình chi tiết, kết nối DBeaver, mẫu lệnh cURL/PowerShell và kịch bản UAT phản biện 3 phút xem tại:  
> 📖 [**Cẩm nang cài đặt và vận hành hệ thống (RUN_GUIDE.md)**](./docs/RUN_GUIDE.md)

---

## 6. Kết quả Kiểm thử Tự động (Automated Testing)

Toàn bộ logic bảo mật, mã hóa BCrypt, phát hành JWT và phân quyền của đồ án đều được kiểm thử tự động:

- **Backend Unit & Integration Tests:**
  ```powershell
  dotnet test backend/DentalClinic.sln
  ```
  *Kết quả:* **1514/1514 .NET tests PASSED** (kèm **559/559 PostgreSQL HTTP smoke tests** & **651/651 Node frontend tests PASSED**, 0 lỗi).
- **Frontend Type-check & Build:**
  ```powershell
  cd frontend
  npm run build
  ```
  *Kết quả:* `vue-tsc` và `vite build` thành công, không có cảnh báo kiểu dữ liệu.

---

## 7. Cấu trúc thư mục dự án

```text
PK_NK_HG/
├── backend/
│   ├── DentalClinic.sln                 # Solution chính thức
│   ├── src/
│   │   ├── Dental.Api/                  # Web API, Controllers, Middlewares
│   │   ├── Dental.Application/          # DTOs, Service Interfaces, Logic nghiệp vụ
│   │   ├── Dental.Domain/               # Entities, Enums, Domain Rules
│   │   └── Dental.Infrastructure/       # EF Core, DbContext, Migrations, Seeders
│   └── tests/
│       └── Dental.Tests/                # 501 tests cho các chức năng đã triển khai
├── frontend/                            # Single Page Application Vue 3 + Vite
│   ├── src/
│   │   ├── views/                       # Màn hình theo module (Login, Dashboard, v.v.)
│   │   ├── stores/                      # Pinia state management (auth store)
│   │   ├── services/                    # Axios interceptors & API client
│   │   └── router/                      # Vue Router & Navigation Guards
│   └── package.json
├── docs/                                # Tài liệu kỹ thuật, kiến trúc & cẩm nang
│   ├── README.md                        # Mục lục tra cứu tài liệu hệ thống
│   ├── RUN_GUIDE.md                     # Cẩm nang cài đặt, chạy và phản biện đồ án
│   ├── ROLE_PERMISSION_MATRIX.md        # Ma trận phân quyền RBAC
│   ├── ARCHITECTURE_OVERVIEW.md         # Tổng quan kiến trúc hệ thống
│   ├── DATABASE_DESIGN.md               # Thiết kế cơ sở dữ liệu
│   ├── testing/                         # Báo cáo, checklist & kế hoạch kiểm thử
│   └── ...
├── docker-compose.yml                   # Cấu hình container PostgreSQL 16 có healthcheck
├── start-dev.ps1                        # Script PowerShell khởi chạy 1 chạm
├── AGENTS.md                            # Quy tắc bắt buộc cho AI coding agent
├── HISTORY_LOG.md                       # Lịch sử ghi nhận thao tác phát triển
└── README.md                            # Tài liệu tổng quan đồ án
```

---

## 8. Quy ước phát triển & Đóng góp

- Mọi thành viên và AI Assistant phải đọc kỹ và tuân thủ các quy tắc trong [**`AGENTS.md`**](./AGENTS.md).
- Sau mỗi lần cập nhật mã nguồn hoặc tài liệu, bắt buộc bổ sung một mục ghi vết vào cuối file [**`HISTORY_LOG.md`**](./HISTORY_LOG.md).
- Không tự ý thêm thư viện ngoài danh mục đã phê duyệt; tuân thủ quy tắc đánh số răng chuẩn FDI quốc tế.

## Kiểm chứng tích hợp main (2026-10-11 08:56 — Asia/Ho_Chi_Minh)

Đã tích hợp chuỗi commit từ `bbe5e8d` đến `e75a6dd` bằng fast-forward. Nhánh `codex/phase1-invoice-draft` (`26de2da`) giữ riêng theo yêu cầu; `main` không chứa migration `AddInvoiceDrafts` hoặc endpoint hóa đơn WIP.

- Backend: `dotnet build` đạt, 0 warning / 0 error; `dotnet test --no-build` đạt 342/342, 0 fail / 0 skip.
- Frontend: `npm run build` đạt kiểm tra TypeScript và Vite, 149 modules. Chưa có frontend test runner.
- EF Core: `dotnet ef migrations has-pending-model-changes --project src/Dental.Infrastructure --startup-project src/Dental.Api --no-build` không phát hiện thay đổi model chưa có migration. CSDL local đã áp dụng đủ 11 migration trên main.
- Runtime PostgreSQL: 61/61 kiểm tra HTTP đạt, gồm đăng nhập 5 vai trò, đọc API, 401 khi chưa đăng nhập, quyền Admin, quyền hàng đợi và kiểm tra quyền ghi FDI trên ID không tồn tại. Không ghi dữ liệu lâm sàng trong kiểm tra runtime này.
- Giới hạn: chưa nghiệm thu UI có đăng nhập, ghi FDI/dịch vụ qua API trên CSDL thật, tải/concurrency hoặc toàn bộ luồng nghiệp vụ. Test hiện có không chứng minh các chức năng chưa triển khai hoạt động.

F_FDI_04 mới có thêm/đọc; xóa chỉ định còn phụ thuộc kiểm tra thanh toán. UI lịch tại quầy, điều phối hàng đợi, hóa đơn/thanh toán, kê đơn, hình ảnh và báo cáo vẫn còn phần chưa triển khai. Kho và nghiệp vụ phụ tá thuộc Phase 2.

## Kiểm chứng và tích hợp hóa đơn dịch vụ (2026-10-11 11:00 — Asia/Ho_Chi_Minh)

Đã fast-forward `codex/phase1-invoice-draft` vào `main` sau kiểm chứng. Phần hiện có gồm Draft dịch vụ, đồng bộ theo snapshot giá, kết thúc khám chuyển chờ thanh toán, Admin mở lại khám hủy hóa đơn chưa thu và sinh mã mới; lý do mở khóa nằm trong lịch sử bảo vệ, AuditLog chỉ tham chiếu ID theo quyết định đã xác nhận.

- Backend: restore/build đạt, 0 warning / 0 error; test 424/424, 0 fail / 0 skip, chạy lại trên main đạt.
- Frontend: vue-tsc và Vite build đạt, 154 modules; UI 26/26 kiểm tra tải/điều hướng bằng năm vai trò, 0 lỗi console mới sau sửa.
- PostgreSQL: toàn bộ 13 migration áp dụng được từ CSDL trống riêng; không pending model changes. Không thay đổi CSDL làm việc của người dùng.
- API thực tế: 113/113 kiểm tra trên CSDL riêng, gồm RBAC/401/403, Auth/Staff/Patients/Visits/History/Vitals/Catalog/Audit, lịch hẹn, FDI và luồng hóa đơn mở lại/tái khóa.
- Đã sửa lỗi thẻ script thừa trong preview FDI và menu lịch hẹn sai vai trò (kể cả liên kết dashboard). Không thay đổi quyền route/API.
- Script lặp lại: `pwsh -NoProfile -File backend/tests/Smoke/VerifyExistingFunctions.ps1 -IsolatedTestDatabase`. Chỉ chạy API localhost đã nối CSDL kiểm thử riêng, dùng cấu hình seed local; có tạo dữ liệu tổng hợp và không in token/mật khẩu/nội dung lâm sàng.

Giới hạn: chưa kiểm thử tải/concurrency, chưa nghiệm thu toàn bộ thao tác ghi trên UI. Thuốc thực, thu tiền/hoàn tiền, hình ảnh, báo cáo và phần chưa có source không thuộc lượt tích hợp này; F_BIL_01/F_PAT_08 vẫn PARTIAL theo phạm vi. Bộ test xanh không chứng minh các chức năng chưa triển khai hoạt động. Tài liệu nghiệp vụ và báo cáo kiểm chứng trong `docs/` vẫn local theo `.gitignore` hiện có.
## F_BIL_03 — Thu tiền mặt (DL-175)

Phần thu tiền mặt có backend và UI tại `/clinical/visits/{visitId}/invoice-draft`.
Admin hoặc Lễ tân/Thu ngân mở hóa đơn của ca đã kết thúc và khóa, nhập số tiền thu
không vượt số nợ cùng tiền khách đưa, rồi tích xác nhận đã nhận tiền và bấm thu.
Màn hình hiển thị mã khoản thu, tiền thối, trạng thái hóa đơn và lịch sử các lần thu.

Để nghiệm thu: thu một phần, kiểm tra số nợ giảm đúng; thu hết phần còn lại và kiểm
tra trạng thái Đã thanh toán. Tiền khách đưa lớn hơn khoản thu chỉ tạo tiền thối,
không làm tăng tiền đã thanh toán. Bệnh nhân/Nha sĩ/Phụ tá không có quyền thu.
Nếu kết quả lần thu chưa rõ do mất kết nối, tải lại lịch sử hoặc thử lại lần thu
đang chờ; hệ thống giữ cùng mã yêu cầu để tránh ghi nhận hai lần.

Kiểm chứng cục bộ phần tiền mặt: 473 unit test pass, frontend build 159 modules;
29 kiểm tra PostgreSQL/HTTP pass trên CSDL riêng với dữ liệu tổng hợp. Chạy từ
`backend/`:

```powershell
dotnet test --no-restore -p:UseAppHost=false
dotnet run --project tests/CashPayment.Postgres -p:UseAppHost=false -- --isolated-config src/Dental.Api/appsettings.Development.json
```

Smoke test tạo rồi xóa CSDL test có tên ngẫu nhiên; tài khoản CSDL cần quyền tạo
database. Test HTTP dùng host localhost và khóa JWT sinh riêng trong bộ nhớ;
không kiểm chứng đăng nhập của ứng dụng. Gia chưa nghiệm thu UI phần tiền mặt.
F_BIL_03 tổng thể còn chuyển khoản/VietQR; biên lai K80 thuộc F_BIL_06.

## F_BIL_03 — Chuyển khoản webhook và mô phỏng

Backend hỗ trợ payload SePay tại POST /api/webhooks/payment/bank-transfer:
Id, transferType=in, transferAmount, referenceCode, content và accountNumber.
Nội dung cần chứa đúng một mã PKNK INV-yyyyMMdd-XXXX. Callback thành công trả
HTTP200 với success=true; event ID và mã giao dịch chống ghi nhận trùng.
Payload/ack được đối chiếu với tài liệu chính thức:
[Tài liệu webhook SePay](https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook)
Casso chưa có adapter; chưa kết nối/tạo tài khoản ngân hàng hay webhook public thực.

Cấu hình thật không được seed giả: SystemConfigs cần ba khóa Payment:BankBin,
Payment:AccountNumber, Payment:AccountName. API key lấy từ cấu hình bảo mật
Payment:WebhookSecretKey (ít nhất32 ký tự; không commit secret). Cấu hình
Payment:WebhookActorUserId là một user đang hoạt động có vai trò Admin/Thu ngân,
được gán làm actor kiểm toán cho webhook đã xác thực. Không có actor/secret hợp lệ
thì callback trả401; tài khoản nhận khác cấu hình không được ghi tiền.
Phần CRUD cấu hình toàn hệ thống thuộc MOD_MST, chưa triển khai lượt này.

Mở hóa đơn của ca đã kết thúc ở /clinical/visits/{visitId}/invoice-draft bằng
Admin hoặc Lễ tân/Thu ngân. UI hiển thị VietQR nếu đã cấu hình, tự kiểm tra nợ mỗi
3giây, có nút kiểm tra ngay và dừng theo dõi. Khi Development, xác nhận mục
Mô phỏng rồi bấm Mô phỏng thanh toán chuyển khoản thành công. Có thể demo offline
khi chưa có cấu hình ngân hàng: QR thật không hiện, vẫn ghi khoản thu Simulation
có nhãn MÔ PHỎNG để kiểm chứng gạch nợ. Endpoint simulate yêu cầu JWT Admin/Thu ngân
và Development; Production trả404. Mỗi lần thử lại dùng cùng TransactionReference.

Chuyển thiếu tạo PartiallyPaid; chuyển đủ/dư tạo Paid. Amount là phần ghi vào
hóa đơn không vượt nợ; AmountTendered và BankReceivedAmount lưu số thực nhận,
ChangeAmount/Note lưu khoản dư cần đối soát. Hệ thống không tự hoàn tiền.
Một khoản tiền vào mới cho hóa đơn đã Paid bị trả409 để đối soát riêng.

Kiểm chứng lượt webhook:501 unit tests pass, frontend build164 modules;
60 kiểm tra PostgreSQL/HTTP pass trên CSDL riêng (bao gồm29 kiểm tra cash cũ).
Dùng lệnh smoke ở mục trước. Chưa nghiệm thu browserUI của Gia, ảnh QR qua
internet hay nhận webhook từ tài khoản ngân hàng thật; không coi các kiểm tra
HTTP trên test host là kiểm chứng production login/Casso.

### F_BIL_03 — VietQR dự phòng ngoại tuyến (DL-200)

Trong Development, modal thanh toán tự hiển thị SVG cục bộ và thông báo vàng
**Chế độ mô phỏng ngoại tuyến (Offline Demo)** khi ảnh VietQR tải lỗi hoặc chưa
cấu hình ngân hàng. Ảnh minh họa không chứa tài khoản, không dùng để chuyển tiền.
Thu ngân xác nhận mô phỏng và sử dụng nút mô phỏng hiện có để kiểm chứng khoản
thu, trạng thái hóa đơn và lịch sử thanh toán. Backend cục bộ vẫn cần hoạt động.

Để kiểm chứng lỗi tải ảnh, chặn riêng yêu cầu tới `img.vietqr.io` trong trình
duyệt rồi mở modal; giữ kết nối tới backend cục bộ. Bấm **Thử tải lại VietQR
trực tuyến** sau khi bỏ chặn để tải lại ảnh thật. Production không hiển thị
ảnh mô phỏng và endpoint simulate vẫn trả 404.

Kiểm thử logic hiển thị bằng `node --test tests/bankQrDisplay.test.mjs` từ
`frontend/` (9 trường hợp). Không cần thêm package. Phần timeout/TTL thuộc Q-181
đã chốt tại DL-205 và được triển khai như mô tả dưới đây.

### F_BIL_03 — Phiên VietQR 15 phút (DL-205)

Mở modal VietQR để bắt đầu phiên 15 phút với đồng hồ `15:00`. Khi hết hạn,
modal làm mờ mã, thông báo **Mã thanh toán đã hết hạn**, dừng polling và khóa
thao tác mô phỏng/kiểm tra trong phiên cũ. **Tạo lại mã QR** tải lại số dư từ
backend và mở phiên 15 phút mới. **Chọn phương thức khác (Tiền mặt)** đóng
modal và focus ô thu tiền mặt hiện có. Đóng modal/Escape hoặc rời màn hình
sẽ dọn timer; dừng theo dõi thủ công không kéo dài thời gian phiên.

TTL áp dụng cho phiên hiển thị/thăm dò trên UI. Hệ thống vẫn nhận và đối soát
webhook hợp lệ đến muộn theo số dư hóa đơn; hết hạn UI không hủy hóa đơn hoặc
hủy một khoản tiền ngân hàng đã chuyển. Tạo lại mã tải thông tin hiện hành,
không đổi mã hóa đơn hay reference của lần mô phỏng đang cần thử lại.

Chạy `node --test tests/bankQrDisplay.test.mjs tests/bankQrSession.test.mjs tests/bankQrModal.test.mjs`
từ `frontend/`: 22 trường hợp, gồm 9 QR fallback, 8 timer và 5 tích hợp script
component với clock/API test riêng. Đây chưa phải kiểm chứng hiển thị trình duyệt
hoặc ngân hàng thật; không dùng test clock trong source production.

### F_BIL_06 — Biên lai K80 và in lại (DL-152/180/210)

Admin/Lễ tân mở lịch sử thu tại hóa đơn và bấm **Xem / in biên lai** của khoản thu.
Xem trước không tăng lượt in. **In biên lai K80** ghi lượt in + audit rồi mở
`window.print()`; chọn giấy rộng 80mm và tắt header/footer của trình duyệt.
Phiếu gồm thông tin phòng khám, bệnh nhân/thu ngân, các dòng chi phí snapshot,
giao dịch kỳ này (khách đưa/thối), tổng đã thu hiện tại/còn nợ, mã tra cứu và lời cảm ơn.
Khoản thu mô phỏng giữ nhãn MÔ PHỎNG trên bản in.

`GET /api/invoices/{id}/payments/{txId}/receipt` trả dữ liệu xem trước;
`POST /api/invoices/{id}/payments/{txId}/reprint` ghi yêu cầu in và trả dữ liệu
đã xác nhận lượt in. Cả hai yêu cầu JWT Admin/RECEPTIONIST và trả `no-store`.
In đầu số 1 có tiêu đề BIÊN LAI THU TIỀN; từ số 2 có BẢN IN LẠI - LẦN N
và thời gian in lại theo giờ Việt Nam. Yêu cầu đồng thời được serialize/concurrency
guard; 409 cần tải lại để đối chiếu, không tự retry POST in khi kết quả chưa rõ.

PrintCount là số yêu cầu in đã ghi vào hệ thống, không chứng minh giấy đã ra khỏi
máy in: hủy hộp thoại/máy in lỗi vẫn giữ lượt và audit. Khoản thu tài chính bất biến;
chỉ PrintCount được tăng đúng 1 cùng audit trong transaction. Preview không tăng đếm.
Địa chỉ/hotline đọc từ SystemConfigs `Clinic:Address` / `Clinic:Phone`; thiếu cấu hình
thì UI thông báo, không tạo thông tin phòng khám giả. CRUD cấu hình chưa triển khai.

Mã tra cứu được render SVG Code 39 cục bộ, không gửi thông tin bệnh nhân tới dịch vụ
ngoài. Bảng mã đối chiếu [nguồn ZXing Code39Reader](https://github.com/zxing/zxing/blob/master/core/src/main/java/com/google/zxing/oned/Code39Reader.java);
không thêm thư viện. Không thêm giảm giá/thuốc chưa có source hoặc hoàn tiền trong lượt in.
Chưa nghiệm thu bản in trình duyệt/máy K80 hoặc scanner thật.

Kiểm chứng lượt biên lai: 516 backend unit, 79 PostgreSQL/HTTP, 36 frontend logic/script
tests pass; build backend/frontend OK. Chạy `node --test tests/*.test.mjs` từ frontend,
smoke PostgreSQL bằng lệnh đã nêu ở mục thanh toán. Tests dùng dữ liệu tổng hợp/CSDL riêng.

## Kiểm chứng tích hợp thanh toán vào main — 2026-10-11 13:45 (Asia/Ho_Chi_Minh)

Nguồn codex/phase1-vietqr-offline tại c329aa9, được người dùng chọn merge trực tiếp nếu đạt. Phần code gồm tiền mặt, SePay webhook/Development simulation và VietQR offline theo DL-175/DL-190/DL-195/DL-196/DL-200.

- Backend restore/build đạt 0 warning/error; unit 501/501 pass, 0 fail/skip.
- Frontend vue-tsc/Vite build đạt 166 modules; node --test tests/bankQrDisplay.test.mjs 9/9 pass.
- PostgreSQL: toàn bộ 16 migration áp dụng từ CSDL trống riêng; model không pending changes. Payment.Postgres kiểm tra 60/60 đạt gồm cash/bank, RBAC, idempotency và callback đồng thời; tự xóa đúng database test sở hữu.
- API ứng dụng thật: VerifyExistingFunctions.ps1 trên CSDL riêng 113/113 đạt với 5 seed roles và các luồng đã có.
- Browser: 29/29 kiểm tra tải/điều hướng/route guards và luồng ghi test tiền mặt+mô phỏng đạt, 0 console error. Fallback thiếu bank config hiển thị SVG cục bộ/cảnh báo không chuyển tiền; mô phỏng100.000 + cash300.000/tendered400.000 → Paid, nợ0, thối100.000.
- Diff không có secret cấu hình local hoặc file build/thừa; không thêm package, không đổi schema/nghiệp vụ trong lượt kiểm chứng.

Giới hạn: chưa có giao dịch ngân hàng live hoặc cấu hình ngân hàng production; mô phỏng không chứng minh tiền đã vào ngân hàng. Nhánh ảnh trực tuyến bị mất mạng được kiểm tra ở helper 9 case, chưa chặn mạng ảnh qua browser. Không nghiệm thu mọi thao tác ghi UI hoặc toàn bộ tính năng còn thiếu (thuốc, giảm giá, in/hoàn tiền, TTL, Phase2). F_BIL_03 vẫn PARTIAL. CSDL làm việc của người dùng không được thay đổi bởi lượt kiểm chứng; API/start-dev có cơ chế migration khi khởi động theo code hiện có. Báo cáo và ảnh/JSON local trong docs/testing/, giữ ignore.
# Xóa chỉ định dịch vụ trước thanh toán (F_FDI_04)

- Mở màn hình **Chỉ định dịch vụ** của lượt khám `InProgress`, chưa khóa. Admin hoặc nha sĩ phụ trách chọn **Xóa chỉ định**, nhập lý do rồi **Xác nhận xóa**; **Hủy** đóng bước xác nhận.
- API `DELETE /api/visits/{visitId}/services/{assignmentId}` nhận JSON `{ "reason": "Điều chỉnh chỉ định" }`; `assignmentId` là ID dòng chỉ định, không phải ID danh mục dịch vụ. Thành công `204`; ID/lý do không hợp lệ `400`, thiếu dòng `404`, không đủ quyền `403`, lượt khám khóa/kết thúc hoặc đã thu tiền/xung đột đồng thời `409`.
- Xóa cứng, giảm dòng hóa đơn chưa thu và lưu audit `VisitServiceDeleted` cùng transaction. Lý do tối đa 500 ký tự; không nhập dữ liệu cá nhân bệnh nhân. Hóa đơn đã hủy được giữ để truy vết. Khi kết quả mạng chưa rõ, tải lại hồ sơ để đối chiếu trước khi thao tác tiếp.
- Kiểm thử: `dotnet test -p:UseAppHost=false` trong `backend`; `dotnet run --project tests/CashPayment.Postgres -p:UseAppHost=false -- --isolated-config src/Dental.Api/appsettings.Development.json` dùng PostgreSQL cô lập; `node --test tests/*.test.mjs` và `npm run build` trong `frontend`. Chờ Gia nghiệm thu UI có đăng nhập; đây chưa phải xác nhận demo Phase 1.

## Lịch sử răng qua các lần khám (F_FDI_06)

- Mở `/clinical/visits/{visitId}/fdi`, chọn một răng hoặc mặt răng để mở drawer **Lịch sử răng** bên phải. Có thể dùng nút **Lịch sử răng** ở dòng tình trạng đã ghi. Lượt khám đã khóa vẫn cho chọn để xem; quyền ghi tình trạng vẫn do backend và `canEdit` kiểm tra.
- Drawer mặc định chỉ răng đang chọn; bật **Toàn bộ cung hàm** để xem các răng khác, răng sữa và chỉ định tổng quát. Có tải lại, phân trang, trạng thái đang tải/lỗi/không có dữ liệu. Đóng hoặc đổi hồ sơ sẽ xóa dữ liệu drawer.
- `GET /api/patients/{patientId}/teeth/{toothNumber}/history` và `GET /api/patients/{patientId}/treatment-timeline?toothNumber=11` trả `PagedResult`, mặc định `page=1&pageSize=20`, tối đa50 dòng/trang. Bỏ `toothNumber` ở timeline để xem toàn hàm. Các vai trò nhân viên được đọc; Patient chỉ đọc hồ sơ có `UserId` thuộc mình, hồ sơ khác trả404. Response `no-store`; ID FDI/phân trang không hợp lệ400.
- Mỗi bản ghi tình trạng và chỉ định là sự kiện riêng, sắp thời điểm ghi mới nhất trước; giữ ghi chú, mặt răng, lần khám, nha sĩ và giá snapshot dịch vụ. Chỉ định không chứng minh thủ thuật đã thực hiện. Chỉ định đã xóa cứng không xuất hiện trong timeline; audit xóa vẫn theo F_FDI_04. Không đổi schema/mã mặt răng trong lượt này; source nhiều mặt `Surfaces` theo DL-201 còn chưa triển khai.
- Kiểm thử tự động kiểm API thật trên PostgreSQL cô lập và script drawer; vẫn cần Gia nghiệm thu drawer bằng trình duyệt có đăng nhập.

## Ghi chú lâm sàng theo răng (F_FDI_07)

- Tại màn hình tình trạng răng, Admin hoặc nha sĩ phụ trách chọn răng/mặt răng, tình trạng và nhập **Ghi chú lâm sàng theo răng** tùy chọn, tối đa500 ký tự. Lưu tạo bản ghi tình trạng mới; ghi chú cũ được giữ và đọc trong danh sách hoặc drawer lịch sử.
- `POST /api/visits/{visitId}/tooth-conditions` nhận thêm `note` nullable. Payload cũ không có `note` vẫn hợp lệ; khoảng trắng đầu/cuối được bỏ, chuỗi trắng lưu null. Ghi chú dài quá500 trả400; ca khám khóa/kết thúc trả409; chỉ Admin hoặc nha sĩ phụ trách được ghi. Nội dung y khoa không được ghi vào audit log.
- Ghi chú gắn tình trạng mới theo DL-093/110/184, chưa có thao tác sửa trực tiếp bản ghi cũ hoặc ghi chú hậu thanh toán DL-227. Chờ Gia nghiệm thu UI có đăng nhập.


## Xung đột thu tiền mặt (F_BIL_03)

Thu tiền mặt nhận diện PostgreSQL 40001/40P01/23505 trong chuỗi ngoại lệ, rollback và trả409/BIL_006. Lỗi khác không đổi thành lỗi đồng thời. Thu ngân đối chiếu hóa đơn, xác nhận lại, dùng nút kiểm tra/thử lại cùng yêu cầu; không nhận thêm tiền khách. UI giữ mã yêu cầu và số tiền, không tự retry.

Kiểm chứng bằng backend unit, CashPayment.Postgres với --isolated-config, frontend Node tests và build theo lệnh hiện có. Fault injection trong transaction PostgreSQL riêng chứng minh rollback tiền thu/công nợ/audit và retry một receipt. Chưa UAT browser; chưa gộp bản sửa vào integrate.

## Đặt lịch cho tài khoản gia đình (F_APP_01, DL-079)

Tại màn hình đặt lịch, Patient chọn hồ sơ bằng họ tên/ngày sinh/mã rồi ngày và slot. Một User có nhiều Patient đã liên kết; chỉ hồ sơ hoạt động thuộc tài khoản được đặt lịch. Lịch trùng kiểm theo từng thành viên. Chưa có UI tạo/liên kết thành viên trong phạm vi lượt sửa này; không tạo dữ liệu mẫu trong production.

Migration `20261011133134_AllowFamilyPatientProfiles` chuyển index `Patients.UserId` sang không unique, giữ `Users.Phone` unique. Đã áp dụng local; không chạy Down khi còn nhiều hồ sơ cùng User. API mine trả no-store. Kiểm thử cùng các lệnh hiện có: backend unit, CashPayment.Postgres cô lập và frontend Node/build. Gia cần nghiệm thu UI gia đình có đăng nhập.

## Kiểm chứng tích hợp đầy đủ — 2026-10-11

Đã kiểm chứng chuỗi VietQR TTL, biên lai K80, xóa chỉ định, lịch sử răng, ghi chú răng và đặt lịch gia đình; bổ sung bản sửa `8600381` nhận diện ngoại lệ thu tiền đồng thời lồng nhau, giữ rollback/BIL_006 và request idempotency.

- Backend build: 0 cảnh báo, 0 lỗi; 584/584 unit test đạt, không fail/skip; EF không pending model changes.
- PostgreSQL 16 Docker Compose riêng: 18 migration từ CSDL trống và seeder Development đạt; 175/175 kiểm tra PostgreSQL/HTTP và 113/113 API trên 5 vai trò đạt.
- Frontend vue-tsc/Vite build: 176 modules; 74/74 Node test đạt; 16 route imports đều có file tương ứng.
- Backup: `backup/main-20261011-2054`. Lịch sử merge được giữ; không xóa/sửa migration cũ hoặc thêm package. Chưa push trong lượt tích hợp này.

Các chức năng PARTIAL vẫn chờ nghiệm thu tương ứng: ngân hàng live, máy in K80, UI gia đình và nhiều mặt răng. Bộ chạy ban đầu tạo dữ liệu tổng hợp trong CSDL Development Windows trước khi được sửa để dùng đúng Docker; dữ liệu đó được giữ nguyên, sự cố được ghi trong báo cáo local `docs/testing/MAIN_INTEGRATION_20261011_REPORT.md` và HISTORY_LOG, không che kết quả lỗi trước đó.

## Đặt lịch tại quầy (F_APP_02)
Lễ tân/Admin mở Đặt lịch tại quầy ở /appointments/counter, tìm hồ sơ theo tên/SĐT/mã, đối chiếu họ tên/ngày sinh/mã, chọn ngày/slot, notes tùy chọn rồi xác nhận với khách. Tối đa20 kết quả; thu hẹp từ khóa nếu chưa thấy hồ sơ. Hồ sơ không tài khoản vẫn đặt được.
Đặt lịch không tự check-in. API POST /api/appointments kiểm giờ chuẩn/lịch trùng theo bệnh nhân; chưa phân nha sĩ/cấp cứu/đổi-hủy UI/nhắc hẹn. Gia cần nghiệm thu UI có đăng nhập.

## Hàng đợi nội bộ hôm nay (F_CHK_02)
Nhân viên mở Hàng đợi hôm nay tại /queue/today. Mặc định Đang chờ; chọn trạng thái hoặc Tất cả, dùng Trang trước/Trang sau và Tải lại. Danh sách chỉ ngày đang xét theo giờ Việt Nam của server,20dòng/trang; API nội bộ cũng hỗ trợ Date cho tra cứu hiện hành, UI này không chọn ngày lịch sử.
Khách hẹn đúng giờ xếp trước theo giờ hẹn; khách thường/trễ hẹn theo thời điểm check-in. UI dùng thứ tự backend. STT A-001 cho thường/có hẹn, ưu tiên lịch hẹn không phải cấp cứuU. Patient không có quyền truy cập.
Đây là danh sách nội bộ. TV phòng chờ và gọi tiếp theo đã có source sau tích hợp main; hướng dẫn gọi tiếp theo bên dưới. Cấp cứu/phân ghế và dọn lượt qua ngày DL-051 chưa được nghiệm thu. Chờ Gia nghiệm thu UI có đăng nhập.
## Tra cứu và hủy lịch tại quầy (F_APP_03)

Lễ tân/Admin mở Lịch hẹn tại quầy tại /appointments/desk, chọn ngày/trạng thái, tìm lịch và bấm Hủy lịch này. Đối chiếu tên/mã hồ sơ/ngày/giờ với khách, nhập lý do tùy chọn rồi xác nhận; Quay lại giữ nguyên lịch. Danh sách20dòng/trang; chỉ Scheduled được hủy.

Lý do được nối vào ghi chú hiện có, tổng tối đa500ký tự; form hiển thị số ký tự còn lại, không cắt ghi chú cũ. Thành công hủy và tải lại danh sách; nếu kết quả mạng chưa rõ, tải lại lịch để đối chiếu trước khi thử tiếp.

GET /api/appointments/desk chỉ Admin/Receptionist và no-store. Backend hủy Patient kiểm ownership và mốc120phút DL-082; sát giờ liên hệ lễ tân, lễ tân/Admin vẫn hỗ trợ được. Lượt này chưa có UI đổi lịch hoặc tra cứu/hủy Patient; chờ Gia UAT trước khi đánh dấu toàn luồng hoàn thành.

## Lịch hẹn của gia đình (F_APP_03)

Patient mở Lịch hẹn gia đình tại /appointments/mine. Mặc định hiển thị mọi lịch của các hồ sơ liên kết với tài khoản,20dòng/trang và thời gian tăng dần; có thể chọn hồ sơ hoạt động bằng họ tên/ngày sinh/mã và lọc trạng thái. Tải lại, Trang trước/Trang sau chỉ thực hiện thao tác đọc.

API GET /api/appointments và GET /api/appointments/{id} kiểm ownership bằng JWT UserId; lọc trước đếm/phân trang và no-store, hồ sơ/lịch của tài khoản khác trả404. Không lấy owner từ tham số do client gửi. Lỗi bộ chọn hồ sơ không làm mất quyền lọc trên backend.

Lượt này chỉ tra cứu, chưa có UI tự hủy hoặc tự đổi lịch; liên hệ lễ tân nếu cần hỗ trợ. Chờ Gia nghiệm thu UI có đăng nhập.

## Patient tự hủy lịch (F_APP_03, DL-082)

Tại /appointments/mine, Patient chọn Tự hủy lịch ở dòng Scheduled. Hệ thống kiểm điều kiện từ server; nếu còn ít nhất120phút, đối chiếu hồ sơ/ngày/giờ, nhập lý do tùy chọn và tích xác nhận rồi hủy. Quay lại hoặc ESC trước khi gửi giữ nguyên lịch. Sát giờ hoặc đã tiếp đón không tự hủy được, liên hệ lễ tân để hỗ trợ.

Sau thành công danh sách tải lại. Nếu kết quả mạng chưa rõ, đối chiếu lại lịch trước khi thử tiếp; UI không tự gửi lại thao tác hủy. Lý do giới hạn theo phần ghi chú còn lại, không cắt nội dung cũ.

GET cancellation-eligibility chỉ Patient/ownership/no-store; PUT cancel vẫn kiểm lại điều kiện lúc ghi, nên lần đọc hợp lệ trước đó không vượt được ngưỡng thời gian. Tự đổi lịch được mô tả bên dưới. Chờ Gia nghiệm thu dialog bằng browser có đăng nhập.

## Đổi lịch tại quầy (F_APP_03)

Lễ tân/Admin mở /appointments/desk, chọn Đổi lịch trên dòng Scheduled. Đối chiếu lịch hiện tại, chọn ngày và slot30phút mới, nhập lý do bắt buộc (tối đa500ký tự), tích xác nhận rồi gửi. Giữ nha sĩ đang gán; không chuyển nha sĩ qua UI này. Quay lại/ESC trước gửi giữ lịch cũ.

Đổi lịch giữ ID hiện có, tăng số lần đổi và lưu lý do gần nhất riêng với Notes. Sau thành công, tra cứu ngày mới để thấy lịch cập nhật. API reschedule hiện yêu cầu thêm reason; client/script cũ cần gửi trường này. Same-target replay không tăng thêm count.

Migration20261012175813_AddAppointmentRescheduleMetadata thêm Count default0/Reason nullable và đã áp dụng local. Không chạy Down nếu cần giữ hai trường lịch sử này. Đổi lịch dùng Serializable, kiểm trùng nha sĩ/bệnh nhân/slot và trả409 khi có thao tác đồng thời; UI không tự retry.

Giới hạn: chưa cột AppointmentCode riêng. Kiểm trùng nha sĩ khi tạo lịch được bổ sung trong phần đặt lịch tại quầy bên dưới. Chờ Gia nghiệm thu UI có đăng nhập.

## Bệnh nhân tự đổi lịch (F_APP_03)

Patient đăng nhập, mở /appointments/mine và chọn Đổi lịch trên lịch Scheduled của hồ sơ gia đình. Dialog tải trạng thái mới nhất, số lần còn lại và điều kiện từ server; chỉ cho đổi khi còn ít nhất 120 phút trước giờ hẹn hiện tại và RescheduleCount < 2 theo câu trả lời Q-238. Bộ đếm tính tổng lần đổi trên cùng lịch, kể cả lần đổi tại quầy. Khi hết lượt hoặc sát giờ, liên hệ Lễ tân để được hỗ trợ.

Chọn ngày/slot mới, nhập lý do bắt buộc tối đa 500 ký tự, tích xác nhận rồi gửi. Giữ ID, hồ sơ, nha sĩ đang gán và Notes của nhân viên. Thành công tải lại danh sách; Quay lại/ESC trước gửi giữ lịch cũ. Khi lỗi, giữ bản nháp, đọc lại điều kiện và yêu cầu xác nhận lại; không tự gửi lại mutation.

GET /api/appointments/{id}/rescheduling-eligibility chỉ Patient, ownership theo JWT và no-store; trả appointment, canReschedule, message, remainingChanges. PUT reschedule mở thêm Patient theo policy AppointmentReschedule và kiểm lại ownership/120 phút/count trong transaction Serializable; lịch ngoài tài khoản trả 404, sửa nha sĩ hoặc Notes bị 403. Admin/Receptionist vẫn hỗ trợ khi Patient hết lượt. Same-target replay chỉ đọc trạng thái, không tăng count, có thể trả 200 sau mốc/giới hạn; UI không mở thao tác mới khi eligibility=false.

Lượt này không thay schema, dùng migration metadata có sẵn. Kiểm chứng: 681 backend unit, 118 frontend Node, 291 PostgreSQL/HTTP đạt; build backend/frontend OK. Chưa browser UAT. F_APP_03 tổng thể vẫn PARTIAL/chờ Gia nghiệm thu; Phase 2 chưa được mở.

## Đặt lịch thường tại quầy, chọn nha sĩ (F_APP_02)

Admin/Lễ tân mở /appointments/counter, tìm hồ sơ theo tên/SĐT/mã, chọn đúng bệnh nhân, chọn nha sĩ hoạt động hoặc “Chưa phân nha sĩ”, ngày và slot chuẩn 30 phút. Đối chiếu thông tin với khách, tích xác nhận rồi đặt. Tải lại nha sĩ nếu danh sách cũ; nha sĩ bị ngừng hoạt động sau khi tải vẫn bị backend từ chối lúc ghi.

GET /api/appointments/booking-dentists chỉ Admin/Receptionist, no-store, trả dentistId/dentistName của nha sĩ hoạt động. POST appointments giữ DentistId tùy chọn; có nha sĩ thì chặn trùng lịch Scheduled cùng ngày/slot với thông báo 400 “Bác sĩ đã có lịch hẹn trong khung giờ này”. Kiểm tra này áp dụng cho cả request của Patient, vẫn kiểm ownership hồ sơ gia đình.

Tạo lịch và đổi lịch đều dùng Serializable: khi hai thao tác ghi cùng slot nha sĩ, API trả APP_009/409 cho bên xung đột. UI giữ bản nháp, bỏ xác nhận và yêu cầu đối chiếu lịch trước khi thử tiếp; không tự gửi lại. Đặt thành công chỉ tạo Appointment, khách vào hàng đợi khi check-in. Không thay schema/package trong lượt này.

Kiểm chứng 704 backend unit / 132 frontend Node / 324 PostgreSQL/HTTP đạt; backend build 0 warning/error và frontend build 191 modules. Đã kiểm race tạo-vs-tạo và tạo-vs-đổi bằng barrier, mỗi slot chỉ có một lịch; chưa browser UAT. Ca cấp cứu/chen ngang DL-229/DL-234 và nhắc hẹn chưa triển khai; F_APP_02 vẫn PARTIAL/chờ Gia nghiệm thu.

## Mô phỏng nhắc hẹn 24 giờ và xem nhật ký (F_APP_04)

Admin/Lễ tân mở /appointments/desk → Nhật ký nhắc lịch. Tích xác nhận rồi bấm Mô phỏng gửi nhắc lịch. Backend dùng giờ Việt Nam hiện tại để quét lịch Scheduled trong khoảng 23,5–24,5 giờ tới, bỏ qua lịch được đặt cách giờ khám dưới 24 giờ. Kết quả 0 là hợp lệ nếu chưa có lịch đủ điều kiện. Không có dữ liệu mẫu trong API; để demo cần lịch được đặt trước ít nhất 24 giờ và đang trong cửa sổ quét.

Tin nhắc chỉ lưu nội bộ ở NotificationLogs với Channel=SMS_SIMULATED, Status=Sent; không gọi dịch vụ SMS. Nội dung tiếng Việt, giờ hẹn, họ tên, nha sĩ và Hotline theo DL-248. Khi chưa phân nha sĩ, nội dung ghi “nha sĩ được phân công”. Nhật ký lưu snapshot ngày/giờ hẹn để nhắc lại khi đổi sang mốc mới, bảo toàn nội dung cũ và không tạo trùng khi bấm lại cùng mốc. Quay lại đúng mốc từng nhắc sẽ dùng nhật ký cũ.

Panel cho lọc ID lịch hẹn, phân trang 20 tin và tải lại; nội dung hiển thị dạng text. API chỉ Admin/Receptionist, GET no-store, không trả RecipientPhone trong DTO xem nhật ký. Audit chỉ ghi số lượng và ID, không ghi nội dung tin/số điện thoại. Nếu lỗi mạng hoặc APP_010/409, đối chiếu nhật ký và xác nhận lại; UI không tự gửi lại mutation.

Migration20261013044654_AddAppointmentReminderLogs đã áp dụng local: bảng mới/FK Restrict và index, không sửa lịch cũ. Không chạy Down nếu cần giữ nhật ký. Kiểm chứng phần thủ công: 737 backend unit / 142 frontend Node / 359 PostgreSQL/HTTP đạt; build backend 0 warning/error, frontend 195 modules. Worker được bổ sung ở phần tiếp theo. Phase 2 chưa được mở.

### Nhắc hẹn tự động mỗi 30 phút (F_APP_04)

API đăng ký AppointmentReminderWorker: quét một lần khi máy chủ khởi động, sau đó mỗi 30 phút trong lúc API chạy. Mỗi lần dùng scope riêng và cùng logic/transaction với nút mô phỏng; các lần quét của worker chạy tuần tự. Hai nguồn dùng chung unique occurrence nên không tạo trùng tin. Lỗi/conflict được ghi trạng thái để kiểm tra, lần tiếp theo theo lịch 30 phút; không gửi lại tức thì.

Nhật ký vẫn là SMS_SIMULATED, không SMS thực. Audit phân biệt source=Automatic với UserId/IP=null cho hệ thống; source=Manual giữ UserId nhân viên. Worker không log nội dung exception có thể chứa dữ liệu bệnh nhân. Không thay schema/package trong lượt này.

Tại /appointments/desk → Nhật ký nhắc lịch, xem trạng thái nhắc tự động, thời gian hoàn tất gần nhất và số tin tạo/đã nhắc. Bấm Tải trạng thái nhắc tự động để cập nhật; trạng thái này thuộc lần chạy máy chủ hiện tại và reset khi khởi động lại, nhật ký trong DB vẫn giữ. GET /api/appointments/reminder-worker-status chỉ Admin/Receptionist, no-store, không chứa tên/số điện thoại/nội dung tin. Đóng panel không dừng worker trên máy chủ.

Kiểm chứng 744 backend unit / 147 frontend Node / 380 PostgreSQL/HTTP đạt; build backend 0 warning/error và frontend 195 modules. Chu kỳ 30 phút được kiểm bằng timer điều khiển trong test, cùng worker thật và DB test; không tuyên bố đã chờ 30 phút thực tế hoặc đã browser UAT. F_APP_04 vẫn PARTIAL/chờ Gia nghiệm thu; API dừng thì không quét và không bù các cửa sổ đã bỏ lỡ khi tắt máy.

## Kiểm chứng tích hợp lịch hẹn và hàng đợi — 2026-10-13

Đã tích hợp 11 nhánh đặt lịch tại quầy, hàng đợi/TV, hủy–đổi lịch và nhắc hẹn thủ công/tự động trên nhánh integrate/20261013. Bản sửa fixture gọi số giữ các assertion cũ, dùng nha sĩ/JWT thật trong CSDL kiểm thử, ngày Việt Nam và kiểm ca thực tế được chọn. Hai GET TV công khai được Gia duyệt theo DL-090/DL-132; API hàng đợi nội bộ vẫn JWT/RBAC.

- Backend build: 0 warning/error; 778/778 unit test, không fail/skip; EF không pending model changes.
- PostgreSQL 16 Docker Compose riêng: 20 migration từ CSDL trống và seeder Development đạt; 433/433 kiểm tra PostgreSQL/HTTP; 113/113 API ứng dụng thật với 5 vai trò đạt.
- Frontend vue-tsc/Vite: 201 modules; 159/159 Node test; 22 route imports có đủ file, không trùng path.
- Giữ cả chức năng của hai chuỗi, không thêm package hoặc sửa/xóa migration cũ. Backup: backup/main-20261013-1946. Báo cáo local: docs/testing/MAIN_INTEGRATION_20261013_REPORT.md. Không push trong lượt này.

Chưa nghiệm thu mọi thao tác UI/TV/âm thanh trình duyệt; nhắc hẹn là mô phỏng SMS và worker theo nguồn hiện có, không gửi SMS thật. Luồng cấp cứu, bank live, máy K80 và các chức năng PARTIAL khác vẫn theo hiện trạng. Worker được kiểm bằng timer điều khiển và PostgreSQL thật, không tuyên bố đã chờ 30 phút thực tế.

## Gọi tiếp theo an toàn (F_CHK_03)

Admin/Nha sĩ mở /queue/today. Admin tải danh sách nha sĩ hoạt động, chọn nha sĩ rồi bấm Gọi tiếp theo; Nha sĩ gọi cho chính mình theo tài khoản đăng nhập. Lễ tân/Phụ tá chỉ xem hàng đợi, không có nút gọi. Ca được chọn theo thứ tự backend, không phụ thuộc bộ lọc hoặc trang đang xem.

Gọi ca cập nhật QueueEntry, Visit và lịch sử trong giao dịch Serializable. Hai lượt gọi đồng thời chỉ có một lượt nhận được ca; bên xung đột nhận CHK_010/409. Nếu lỗi hoặc chưa tải lại được sau thành công, đối chiếu hàng đợi/nha sĩ rồi tải lại thành công trước khi gọi tiếp; UI không tự gửi lại POST. Nếu nha sĩ có Visit InProgress khác thì trả 409. Ca trở về Waiting sau chụp ảnh có thể tiếp tục chính Visit đó, giữ StartedAt cũ.

Kiểm chứng riêng trên CSDL tạm, không dùng dữ liệu bệnh nhân thật (chạy từ backend):
dotnet run --project tests/CashPayment.Postgres -p:UseAppHost=false --no-restore -- --isolated-config src/Dental.Api/appsettings.Development.json --queue-call-next-only

Kết quả: 788/788 backend unit, 169/169 frontend Node, 58/58 PostgreSQL/HTTP cho hàng đợi/gọi khám; build backend 0 warning/error và frontend 201 modules; EF không pending model changes. Bộ PostgreSQL/HTTP đầy đủ dừng tại fixture nhắc hẹn: SlotTime cộng 20 phút qua nửa đêm nhưng AppointmentDate không đổi (AppointmentReminderHttpSmoke.cs), chưa sửa vì ngoài phạm vi F_CHK_03. Không coi kết quả 433/113 của lần tích hợp trước là kết quả lượt này.

Giới hạn: chưa browser UAT/âm thanh TV trên thiết bị thật; race với PUT status hoặc các luồng ghi Visit khác chưa được kiểm chứng trong lượt này. AuditLogger dùng cơ chế best-effort hiện hữu. DL-214 mô tả Calling/InExam/Skipped/ghế và Phụ tá gọi khác với 5 trạng thái source theo DL-047 và quyền gọi DL-120; giữ DL nguyên văn, ghi nhận chênh lệch. F_CHK_03 tổng thể vẫn PARTIAL, chờ Gia nghiệm thu Phase 1.

## Hủy lượt chờ chưa khám (F_CHK_07)

Admin/Lễ tân mở /queue/today, chọn Hủy lượt chưa khám trên dòng Đang chờ. Đối chiếu số thứ tự, tên/mã bệnh nhân và ngày tiếp đón, nhập lý do tùy chọn (tối đa 500 ký tự), tích xác nhận rồi bấm Xác nhận hủy lượt. Quay lại hoặc tải lại trước khi gửi sẽ đóng form; không hủy lượt.

Backend POST /api/queue/{id}/cancel-waiting yêu cầu confirmed=true, chỉ Admin/Receptionist. Chỉ lượt hôm nay ở Waiting với Visit Created, chưa StartedAt và không khóa được hủy. Hủy QueueEntry/Visit và thêm lịch sử trong giao dịch Serializable; giữ Appointment CheckedIn vì khách đã đến, không xóa hồ sơ/lịch hẹn hoặc đổi thành no-show. Visit đóng nên bệnh nhân có thể check-in mới.

Nếu ca vừa được gọi, đã hủy, thuộc ngày khác hoặc đã bắt đầu khám/trở về từ chụp ảnh, trả CHK_011/409. Khi hai thao tác tranh cùng lượt, cancellation nhận CHK_012/409; call-next vẫn dùng CHK_010/409. UI không tự gửi lại POST, giữ lý do khi lỗi, bỏ xác nhận và yêu cầu tải lại thành công rồi đối chiếu lại. Lý do lưu trong lịch sử nội bộ, không đưa vào audit Detail.

Kiểm chứng 2026-10-14: 809/809 backend unit, 180/180 frontend Node và 484/484 PostgreSQL/HTTP full harness PASS; build backend 0 warning/error, frontend 201 modules, EF không pending model changes. Có barrier race hủy-vs-hủy/hủy-vs-call-next, kiểm queue/Visit/history/audit chỉ theo một kết quả đã commit, rollback sau write và check-in lại sau hủy.

F_CHK_07 tổng thể PARTIAL: lượt này chỉ UI/endpoint hủy trước khám; các nút chuyển trạng thái khác và browser UAT còn chờ. PUT /queue/{id}/status cũ vẫn có luồng riêng, chưa chứng minh race với endpoint mới; DL-057 hủy InImaging khác source state machine hiện tại và DL-214 còn lệch tên trạng thái/ghế, đã ghi nhận, không sửa DL trong lượt này. Fixture nhắc hẹn có lỗi biên gần nửa đêm đã ghi từ lượt trước, hôm nay PASS tại giờ thực chạy nhưng fixture đó chưa được sửa. Phase 2 chưa mở.

## Bắt đầu khám từ lượt đã chọn (F_CHK_04)

Nha sĩ/Admin mở /clinical/diagnosis và chọn lượt Đang chờ hôm nay. Nha sĩ bắt đầu cho chính mình; Admin chọn nha sĩ hoạt động từ danh sách đã tải. Nếu lượt/Visit đã được gán nha sĩ, phải giữ nha sĩ đó; chuyển người phụ trách không thuộc thao tác bắt đầu này.

Bấm Bắt đầu khám chuyển QueueEntry sang InConsultation và Visit sang InProgress trong giao dịch Serializable, lưu lịch sử và audit actor/ID/status. Nha sĩ đã có Visit InProgress khác bị 409; không được giả mạo DentistId khác. Lượt trở lại sau chụp ảnh tiếp tục chính Visit/nha sĩ, giữ StartedAt. Ca đã hủy/khóa/xong hoặc ngày khác bị từ chối.

Thành công đọc lại cả QueueEntry và Visit rồi mở form ghi chẩn đoán hiện hữu. Khi lỗi hoặc refresh thất bại, màn hình khóa thao tác ghi, yêu cầu tải lại hồ sơ và đối chiếu; không tự gửi lại PUT. Chưa lưu chẩn đoán thì không đổi bệnh nhân; unmount xóa dữ liệu và bỏ response muộn.

API giữ PUT /api/queue/{id}/status với newStatus=2, không thêm endpoint/package/schema. Các trạng thái khác giữ luồng cũ. Kiểm chứng: 830/830 backend unit,190/190 frontend Node,505/505 PostgreSQL/HTTP full harness, backend build0warning/error và frontend201modules; EF không pending model. Có barrier race hai lượt khác nhau cùng nha sĩ, start-vs-call-next/start-vs-cancel và rollback.

Implementation trên codex/phase1-start-consultation, worktree riêng kế thừa15ffad2/ce5ecb9; main55a0a00 chưa chứa ba commit này. F_CHK_04 tổng thể PARTIAL/chờ Gia UAT, ghế/Calling/InExam theoDL214 chưa có. DL057 quyền hủy InImaging còn lệchsource ngoài scope; không sửaDL. AuditLoggerbest-effort và lỗi fixture reminder gần nửa đêm đã ghi trước chưa đổi; bộ505 PASS tại giờ chạy không chứng minh hết biên thời gian này.

## Tìm dịch vụ để chỉ định theo răng (F_FDI_04)

Admin/nha sĩ phụ trách mở /clinical/visits/{visitId}/services khi Visit đang khám và chưa khóa. Chọn răng người lớn/trẻ em hoặc dịch vụ toàn hàm, nhập tên/mã dịch vụ rồi bấm Tìm dịch vụ. Từ khóa tối đa100ký tự, được trim; tìm chuỗi không phân biệt hoa/thường, vẫn phân biệt dấu tiếng Việt. Phân trang giữ từ khóa đã áp dụng; đang nhập từ khóa mới chưa bấm Tìm thì kết quả cũ vẫn dùng bộ lọc đã hiển thị. Xóa tìm kiếm trở về trang1.

Đổi tìm kiếm/trang xóa lựa chọn dịch vụ cũ nhưng giữ răng/mặt/số lượng để chọn tiếp. Kết quả rỗng được hiển thị rõ; lỗi tải xóa options và chặn chỉ định từ ID cũ. Giá chưa hiệu lực hiển thị rõ và không chọn được. Route change/unmount xóa từ khóa và bỏ response muộn; tìm kiếm chỉ đọc, không tự chỉ định.

GET /api/services hiện hữu dùng keyword/isActive=true/page/pageSize20, phân trang backend tối đa1000000/pageSize100, keyword100; response no-store. Quyền catalog hiện hữu Admin/Dentist/Assistant giữ nguyên, UI chỉ định chỉAdmin/nha sĩ phụ trách. Khi lưu chỉ định, API kiểm lại dịch vụ active và chốt giá hiệu lực trong CSDL, không dùng giá tra cứu cũ.

Kiểm chứng 2026-10-14:842/842unit,202/202Node,536/536PostgreSQL/HTTP fullPASS; backend/harnessbuild0warning/error và frontend201modules; EFkhôngpendingmodel. Đã kiểm tìm tên/mã/chữhoa-tiếngViệt/ký tự%literal, inactive/futureprice, phân trang lọc, search→chỉ định cho răng11/51 mặtB và giá thay đổi sau tra cứu.

Nhánh codex/phase1-fdi-service-search kế thừae2f197d trongworktree riêng, chưa tích hợp main55a0a00. F_FDI_04 vẫnPARTIAL/chờGiaUAT/gợi ýdịchvụ và nhiều mặt; không xử lý mâu thuẫn độ dài Surfaces10(DL168)/20(DL201), không sửaDL/mởPhase2. Không thay logic lưu/snapshot/chuyển trạng thái, không thêm schema/package.

### Khôi phục mật khẩu qua SMS (F_AUTH_04, demo_11)

Từ trang đăng nhập chọn **Quên mật khẩu?**, nhập SĐT đã đăng ký, nhận OTP qua SMS rồi đặt mật khẩu mới. Mã 6 chữ số có hiệu lực 5 phút, tối đa 5 lần sai; xin mã mới cách ít nhất 60 giây. Reset thành công yêu cầu đăng nhập lại, thu hồi JWT cũ. Tài khoản Admin đã khóa vẫn bị khóa.

Luồng mặc định tắt gửi SMS. Cấu hình phía máy chủ bằng user-secrets/biến môi trường:

- PasswordRecovery:Sms:Enabled — true chỉ khi sẵn sàng gửi thật.
- PasswordRecovery:Sms:AccountSid và PasswordRecovery:Sms:AuthToken — tài khoản provider.
- PasswordRecovery:Sms:From — số gửi E.164 đã cấu hình tại provider.

Adapter dùng [Twilio Messages REST API](https://www.twilio.com/docs/messaging/api/message-resource) qua HttpClient, không SDK. Thiếu cấu hình API trả 503 với thông báo liên hệ phòng khám. Không có OTP giả lập/endpoint đọc mã trong production. SMS gửi bởi worker với queue bounded trong tiến trình; restart cần xin mã khác. Chưa cấu hình/gửi SMS thật trong kiểm thử tự động.

Migration 20261018184345_AddPasswordRecoveryTokenVersion thêm Users.TokenVersion; cần áp migration trước khi chạy bản mới. HTTP/PostgreSQL checks dùng mode --password-recovery-only trong harness backend/tests/CashPayment.Postgres với SMS test double chỉ ở tests. Fixture UI tổng hợp: frontend/tests/browser-password-recovery.html, không kết nối tài khoản thật.
Migration tiếp nối 20261018185811_ProtectRecoveredCredentials chỉ cập nhật metadata concurrency trong model/snapshot, không đổi schema. Giữ nguyên migration thêm cột đã chạy trên DB kiểm thử.

### Tải và gán ảnh hồ sơ (F_IMG_02, demo_11)

Mở hồ sơ bệnh nhân → **Ảnh hồ sơ** → chọn JPG/PNG/WebP, phân loại, răng FDI tùy chọn và ca có thể gắn; hoặc lưu riêng theo bệnh nhân. Admin/Nha sĩ/Phụ tá được tải lên. Admin hoặc nha sĩ phụ trách có thể gán ảnh đang độc lập vào ca; liên kết đã gán được giữ cố định. Q-327a chỉ cho gắn vào ca đang mở/đính chính hợp lệ, chưa có thanh toán.

Backend kiểm tra MIME/đuôi/header, giới hạn 10MB/tệp, 4096px mỗi chiều, 20 ảnh/ca, 3 ảnh/răng và 100MB/ca. Tệp nằm ngoài web root ở uploads/images/{patientId}/{UUID}.{ext}, cần sao lưu cùng CSDL; không có URL tĩnh công khai. Giới hạn 4 upload đồng thời để khống chế bộ nhớ. Audit và metadata cùng transaction; lưu CSDL lỗi thì dọn tệp vừa tạo.

Migration 20261018192034_AddDentalImageAttachments tạo DentalImages. Mode kiểm thử --dental-images-only dùng DB/container riêng, dữ liệu tổng hợp. Danh sách metadata dành cho Admin/Nha sĩ/Lễ tân hoặc bệnh nhân sở hữu hồ sơ theo Q-247; quyền tải lên của Phụ tá không tự cấp quyền xem. Xem tệp/thu phóng thuộc lát cắt F_IMG_04 tiếp theo; chưa nghiệm thu trình duyệt/Gia UAT.

Danh mục ảnh theo Q-274: Panorama, Periapical, Bitewing, Cephalometric, Intraoral; ghi chú tối đa500ký tự. Migration20261018193500_AddDentalImageNotes bổ sung Notes; enum lưu tên trong cột Category. Giữ nguyên migration đầu đã chạy ở DB kiểm thử.
