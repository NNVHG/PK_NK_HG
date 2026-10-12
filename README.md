# Hệ thống Quản Lý Phòng Khám Nha Khoa Hoàng Gia (PK_NK_HG)

> **Đồ án tốt nghiệp ngành Công nghệ Thông tin**  
> **Trường Đại học Thủ Dầu Một — Viện Công nghệ số**  
> **Sinh viên thực hiện:** Nguyễn Ngô Vũ Hoàng Gia — **MSSV:** 2224802010628 — **Lớp:** D22CNTT02  
> **Giảng viên hướng dẫn:** TS. Ngô Thị Ngọc Dịu  
> **Trạng thái:** Phase 1 — `main` đã tích hợp đặt lịch trực tuyến, check-in, khám/chẩn đoán, tình trạng răng FDI và thêm/đọc chỉ định dịch vụ; 342/342 backend tests pass. Hóa đơn WIP còn ở nhánh riêng, chưa đưa vào `main`.

---

## 1. Mục tiêu và Phạm vi đồ án

Hệ thống cung cấp giải pháp chuyển đổi số toàn diện cho **phòng khám nha khoa đơn lẻ**:
- **Chuẩn hóa hồ sơ răng:** Ứng dụng tiêu chuẩn FDI quốc tế (32 răng người lớn: 11–48; 20 răng trẻ em: 51–85) với chi tiết 5 mặt răng.
- **Phân quyền chặt chẽ (RBAC):** Kiểm soát truy cập nghiêm ngặt giữa 5 vai trò: `Admin`, `Lễ tân/Thu ngân`, `Nha sĩ`, `Phụ tá`, `Bệnh nhân`.
- **Quản lý kho an toàn:** Cơ chế gợi ý định mức vật tư (BOM) hỗ trợ nhân viên; bắt buộc xác nhận thủ công trước khi xuất kho, tuyệt đối không trừ kho tự động.
- **Phạm vi loại trừ:** Không tích hợp BHYT; không xử lý chuẩn DICOM gốc (lưu trữ tệp hình ảnh siêu âm/X-quang thông thường); không quản lý ca làm việc (MOD_ROS đã loại khỏi phạm vi đồ án).

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
- **Kiểm thử tự động:** xUnit, FluentAssertions, Moq.

---

## 3. Danh mục 12 Module chức năng

| Mã Module | Tên Module | Trách nhiệm chính |
|---|---|---|
| `MOD_AUTH` | Xác thực & Phân quyền | Đăng nhập JWT, Hồ sơ người dùng, Đổi mật khẩu, RBAC, Nhật ký Audit Log |
| `MOD_PAT` | Quản lý Bệnh nhân | Hồ sơ hành chính, Tiền sử bệnh lý, Dị ứng, Sinh hiệu |
| `MOD_APP` | Quản lý Lịch hẹn | Đặt lịch trực tuyến/tại quầy theo giờ hoạt động chung, slot 30 phút (DL-043), tối đa 100 khách/slot (DL-050); không quản lý ca làm việc MOD_ROS |
| `MOD_CHK` | Tiếp đón & Xếp hàng | Check-in, số thứ tự hàng đợi, điều phối vào buồng khám |
| `MOD_FDI` | Khám bệnh & Sơ đồ răng | Sơ đồ răng 32/20 chuẩn FDI, chẩn đoán ICD-10, kế hoạch điều trị |
| `MOD_IMG` | Quản lý Hình ảnh | Tải lên và lưu trữ phim X-quang, ảnh trước/sau điều trị |
| `MOD_RX` | Đơn thuốc điện tử | Kê đơn, cảnh báo tương tác thuốc và dị ứng tiền sử |
| `MOD_BIL` | Hóa đơn & Thanh toán | Lập phiếu thu, giảm giá, thanh toán tiền mặt/chuyển khoản |
| `MOD_INV` | Quản lý Kho & Vật tư | Danh mục thuốc/vật tư, cảnh báo tồn kho, phiếu xuất theo gợi ý BOM |
| `MOD_ASS` | Hỗ trợ Điều trị | Ghi nhận trợ thủ, vật tư tiêu hao thực tế của ca thủ thuật |
| `MOD_MST` | Danh mục Dòng máy/Dịch vụ | Bảng giá dịch vụ nha khoa, danh mục thủ thuật kỹ thuật |
| `MOD_RPT` | Báo cáo & Thống kê | Doanh thu phòng khám, hiệu suất bác sĩ, tỷ lệ quay lại |

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

- **Backend Unit Tests:**
  ```powershell
  dotnet test backend/DentalClinic.sln
  ```
  *Kết quả:* **501/501 tests PASSED** (100% thành công, 0 lỗi).
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

## Tra cứu và hủy lịch tại quầy (F_APP_03)

Lễ tân/Admin mở Lịch hẹn tại quầy tại /appointments/desk, chọn ngày/trạng thái, tìm lịch và bấm Hủy lịch này. Đối chiếu tên/mã hồ sơ/ngày/giờ với khách, nhập lý do tùy chọn rồi xác nhận; Quay lại giữ nguyên lịch. Danh sách20dòng/trang; chỉ Scheduled được hủy.

Lý do được nối vào ghi chú hiện có, tổng tối đa500ký tự; form hiển thị số ký tự còn lại, không cắt ghi chú cũ. Thành công hủy và tải lại danh sách; nếu kết quả mạng chưa rõ, tải lại lịch để đối chiếu trước khi thử tiếp.

GET /api/appointments/desk chỉ Admin/Receptionist và no-store. Backend hủy Patient kiểm ownership và mốc120phút DL-082; sát giờ liên hệ lễ tân, lễ tân/Admin vẫn hỗ trợ được. Lượt này chưa có UI đổi lịch hoặc tra cứu/hủy Patient; chờ Gia UAT trước khi đánh dấu toàn luồng hoàn thành.
