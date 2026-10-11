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
  *Kết quả:* **424/424 tests PASSED** (100% thành công, 0 lỗi).
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
│       └── Dental.Tests/                # 424 tests cho các chức năng đã triển khai
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
