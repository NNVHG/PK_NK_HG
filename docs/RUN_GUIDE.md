# Cẩm nang cài đặt và vận hành hệ thống PK_NK_HG

> Tài liệu hướng dẫn chi tiết dành cho giảng viên hướng dẫn, hội đồng chấm và thành viên phát triển.
> Thư mục gốc thực thi: `e:\Projects\PK_NK_HG` (hoặc thư mục clone của dự án).

---

## 1. Sơ đồ kiến trúc & Cổng kết nối

```text
[Trình duyệt Web]
       │
       ▼
[Frontend SPA (Vue 3 + Vite)] ── port 5173
       │ (Proxy /api/* hoặc CORS)
       ▼
[Backend Web API (ASP.NET Core 10)] ── port 5000 (HTTP) / port 5000/swagger
       │ (Entity Framework Core Npgsql)
       ▼
[PostgreSQL Database 16] ── port 5432 (Database: dental_clinic)
```

| Dịch vụ | Cổng mặc định | URL truy cập | Ghi chú |
|---|---|---|---|
| **Frontend Web** | `5173` | `http://localhost:5173` | Giao diện quản lý & đăng nhập Vue 3 |
| **Backend Web API** | `5000` | `http://localhost:5000` | Kestrel HTTP API (hỗ trợ CORS cho FE) |
| **Swagger UI** | `5000` | `http://localhost:5000/swagger` | Tài liệu tương tác API trực tiếp |
| **PostgreSQL** | `5432` | `localhost:5432` | Cơ sở dữ liệu quan hệ chính thức |

---

## 2. Yêu cầu môi trường cài đặt

Trước khi bắt đầu, đảm bảo máy tính đã cài đặt:
1. **.NET SDK 10.x**: Kiểm tra bằng lệnh `dotnet --version`.
2. **Node.js (LTS v18+ hoặc v20+) & npm**: Kiểm tra bằng `node -v` và `npm -v`.
3. **Cơ sở dữ liệu PostgreSQL 16**:
   - Khuyến nghị: **Docker Desktop** (tiện lợi, có sẵn `docker-compose.yml`).
   - Hoặc: Cài đặt PostgreSQL 16 trực tiếp trên Windows/Linux.

---

## 3. Khởi chạy nhanh 1 chạm (Quick Start cho Windows)

Nếu đang ở môi trường Windows và đã có Docker Desktop:
1. Mở PowerShell tại thư mục gốc dự án:
```powershell
.\start-dev.ps1
```
*Script sẽ tự động bật PostgreSQL container, khởi động Backend API và mở dev server Frontend.*

---

## 4. Hướng dẫn khởi chạy từng bước (Manual Setup)

### Bước 1: Khởi động Cơ sở dữ liệu PostgreSQL

**Cách 1: Sử dụng Docker Compose (Khuyên dùng)**
Tại thư mục gốc dự án:
```powershell
docker compose up -d postgres
```
Kiểm tra trạng thái container:
```powershell
docker compose ps
```
*(Nếu cần xem log: `docker compose logs -f postgres`)*

**Cách 2: Sử dụng PostgreSQL cài cục bộ**
- Đảm bảo service PostgreSQL đang chạy trên cổng `5432`.
- Tạo cơ sở dữ liệu tên `dental_clinic`.
- User: `postgres`, Password: `postgres` (hoặc cấu hình lại theo Bước 2).

---

### Bước 2: Cấu hình Môi trường Backend

Dự án đã cấu hình sẵn các thông số mặc định cho môi trường `Development` trong file `backend/src/Dental.Api/appsettings.Development.json`:
- **Database:** `Host=localhost;Port=5432;Database=dental_clinic;Username=postgres;Password=postgres`
- **JWT Secret:** Khóa bảo mật ngẫu nhiên đạt chuẩn 256-bit.
- **Tài khoản Seed:** Kích hoạt tự động khi chạy ở môi trường Development.

*(Tùy chọn nâng cao)* Nếu muốn ghi đè cấu hình bảo mật bằng .NET User Secrets:
```powershell
cd backend/src/Dental.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=dental_clinic;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Secret" "YOUR_VERY_LONG_SECRET_KEY_MINIMUM_32_CHARS"
cd ../../..
```

---

### Bước 3: Áp dụng Migration & Khởi động Backend API

Hệ thống đã tích hợp cơ chế tự động chạy Migration và Seed dữ liệu mẫu (`DatabaseSeeder`) ngay khi ứng dụng Backend khởi động.

Mở **Terminal 1** tại thư mục gốc:
```powershell
dotnet run --project backend/src/Dental.Api
```

Khi màn hình xuất hiện:
```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```
Backend đã sẵn sàng! Mở trình duyệt truy cập: `http://localhost:5000/swagger` để xem tài liệu API.

---

### Bước 4: Cài đặt và Khởi động Frontend SPA

Mở **Terminal 2** tại thư mục gốc:
```powershell
cd frontend
npm install
npm run dev
```

Mở trình duyệt truy cập: `http://localhost:5173` để vào hệ thống.

---

## 5. Danh sách tài khoản thử nghiệm (Seed Data)

Khi khởi chạy ở môi trường Development, hệ thống tự động khởi tạo 5 tài khoản thuộc 5 vai trò theo đúng ma trận RBAC (`docs/ROLE_PERMISSION_MATRIX.md`):

| Vai trò (Role) | Mã quyền (RoleCode) | Số điện thoại đăng nhập | Mật khẩu mặc định | Quyền hạn trên hệ thống |
|---|---|---|---|---|
| **Quản trị viên** | `ADMIN` | `0900000001` | `AdminPassword@123` | Toàn quyền cấu hình, nhân sự, kiểm toán |
| **Lễ tân / Thu ngân** | `RECEPTIONIST` | `0900000002` | `DemoPassword@123` | Tiếp đón, xếp lịch hẹn, thu ngân |
| **Nha sĩ** | `DENTIST` | `0900000003` | `DemoPassword@123` | Khám, lập bệnh án FDI, kê đơn thuốc |
| **Phụ tá** | `ASSISTANT` | `0900000004` | `DemoPassword@123` | Chụp X-quang, xác nhận xuất kho vật tư |
| **Bệnh nhân** | `PATIENT` | `0900000005` | `DemoPassword@123` | Xem thông tin cá nhân, tra cứu lịch sử |

---

## 6. Kịch bản kiểm thử trực quan trên Web (UAT Flow)

1. **Kiểm tra Đăng nhập & Phân quyền RBAC:**
   - Đăng nhập với tài khoản `0900000001` (`ADMIN`): Navbar hiển thị đầy đủ *Bảng điều khiển*, *Lịch hẹn*, *Bệnh nhân*. Khối module điều hành phòng khám hiện rõ trên Dashboard.
   - Đăng xuất (`Đăng xuất`), sau đó đăng nhập bằng `0900000005` (`PATIENT`): Hệ thống nhận diện quyền Bệnh nhân, tự động ẩn toàn bộ menu và module điều hành chuyên môn của nhân viên phòng khám, thay bằng Banner tra cứu dịch vụ cá nhân.
2. **Kiểm tra Chức năng Đổi Mật Khẩu (F_AUTH_03):**
   - Tại màn hình *Bảng điều khiển*, tìm khung **"Đổi Mật Khẩu"**.
   - Nhập mật khẩu hiện tại (`AdminPassword@123`), nhập mật khẩu mới (tối thiểu 6 ký tự: 1 hoa, 1 thường, 1 số như `NewPass123`).
   - Nhấn **Cập nhật mật khẩu** -> Nhận thông báo thành công xanh lá.
   - Thử đăng xuất và đăng nhập lại bằng mật khẩu mới vừa đổi.

---

## 7. Kiểm thử trực tiếp trên Swagger UI (Bearer Token)

Swagger UI đã cấu hình sẵn nút **Authorize** để hội đồng kiểm tra trực tiếp các API bảo mật:
1. Mở trình duyệt tại: `http://localhost:5000/swagger`.
2. Mở rộng endpoint `POST /api/auth/login`, bấm **Try it out**, nhập JSON đăng nhập Admin:
   ```json
   {
     "phone": "0900000001",
     "password": "AdminPassword@123"
   }
   ```
   Bấm **Execute**, copy chuỗi `accessToken` trong JSON kết quả.
3. Cuộn lên đầu trang Swagger, bấm nút **Authorize** (hình ổ khóa xanh lá góc trên bên phải).
4. Trong ô *Value*, nhập: `Bearer <token_vừa_copy>` (lưu ý có khoảng trắng sau chữ Bearer) -> Nhấn **Authorize** -> **Close**.
5. Test các API bảo mật:
   - `GET /api/auth/me`: Trả về `200 OK` chứa đầy đủ Profile Admin.
   - `GET /api/dev/admin-only`: Trả về `200 OK` (Admin hợp lệ).
   - `GET /api/dev/staff-only`: Trả về `200 OK` (Staff hợp lệ).

---

## 8. Mẫu lệnh kiểm thử bằng Terminal (PowerShell / cURL)

### Cách A: Dùng PowerShell (Windows)
```powershell
# 1. Đăng nhập lấy Token
$loginBody = @{ phone = "0900000001"; password = "AdminPassword@123" } | ConvertTo-Json
$res = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $res.accessToken
Write-Host "Token nhận được:" $token

# 2. Gọi API lấy thông tin người dùng (/api/auth/me)
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/me" -Method Get -Headers $headers

# 3. Gọi endpoint kiểm tra quyền Admin (/api/dev/admin-only)
Invoke-RestMethod -Uri "http://localhost:5000/api/dev/admin-only" -Method Get -Headers $headers
```

### Cách B: Dùng cURL (Linux / macOS / Bash)
```bash
# 1. Đăng nhập lấy Token
curl -X POST "http://localhost:5000/api/auth/login" \
     -H "Content-Type: application/json" \
     -d '{"phone":"0900000001","password":"AdminPassword@123"}'

# 2. Gọi endpoint bảo mật với Token nhận được
curl -X GET "http://localhost:5000/api/auth/me" \
     -H "Authorization: Bearer <TOKEN_TAI_DAY>"
```

---

## 9. Kịch bản phản biện UAT 3 phút cho Hội đồng chấm

1. **Phút 1 — Giới thiệu kiến trúc & CSDL:**
   - Mở terminal: chạy `docker compose ps` chứng minh PostgreSQL 16 đang chạy containerized.
   - Chạy `dotnet test backend/DentalClinic.sln`: 30 tests PASS (100%) chứng minh tính đúng đắn của logic nghiệp vụ và bảo mật.
2. **Phút 2 — Trình diễn Đăng nhập & Ma trận RBAC:**
   - Mở Web `http://localhost:5173`.
   - Đăng nhập `0900000001` (`ADMIN`): Navbar hiển thị đầy đủ module điều hành phòng khám.
   - Đăng xuất (`Đăng xuất`), sau đó đăng nhập `0900000005` (`PATIENT`): Navbar tự động ẩn các module chuyên môn nhân sự, bảo đảm an toàn dữ liệu nội bộ theo ma trận `ROLE_PERMISSION_MATRIX.md`.
3. **Phút 3 — Kiểm tra Đổi mật khẩu & Audit Log:**
   - Thực hiện đổi mật khẩu trên giao diện `DashboardView.vue`.
   - Mở DBeaver/pgAdmin soi bảng `AuditLogs` chứng minh hành động đăng nhập và đổi mật khẩu được hệ thống ghi vết an toàn kèm địa chỉ IP.

---

## 10. Kiểm tra kết nối Cơ sở dữ liệu bằng DBeaver / pgAdmin

Để trực tiếp thanh tra dữ liệu CSDL của đồ án:
- **Host:** `localhost`
- **Port:** `5432`
- **Database:** `dental_clinic`
- **Username:** `postgres`
- **Password:** `postgres`
- **Các bảng chính:**
  - `Roles`: Danh sách 5 vai trò hệ thống.
  - `Users`: Chứa danh sách người dùng, mật khẩu hash bằng BCrypt an toàn.
  - `AuditLogs`: Nhật ký thao tác hệ thống ghi nhận người dùng và địa chỉ IP.

---

## 11. Hướng dẫn Reset Database về trạng thái ban đầu

Khi cần xóa trắng dữ liệu để demo lại từ đầu:

**Nếu dùng Docker Compose:**
```powershell
# 1. Dừng container và xóa volume lưu trữ
docker compose down -v

# 2. Khởi động lại container PostgreSQL mới tinh
docker compose up -d postgres

# 3. Khởi động lại Backend API (DB sẽ tự động Migrate và Seed lại tài khoản chuẩn)
dotnet run --project backend/src/Dental.Api
```

**Nếu dùng EF Core CLI thủ công:**
```powershell
dotnet ef database drop --project backend/src/Dental.Infrastructure --startup-project backend/src/Dental.Api --force
dotnet ef database update --project backend/src/Dental.Infrastructure --startup-project backend/src/Dental.Api
```

---

## 12. Kiểm tra tự động (Automated Tests)

Chạy bộ 30 unit tests kiểm tra logic đăng nhập, đổi mật khẩu và phân quyền RBAC:
```powershell
dotnet test backend/DentalClinic.sln
```
*Yêu cầu kết quả: Toàn bộ 30/30 tests đạt trạng thái `Passed` (100%).*

Kiểm tra biên dịch và type-check frontend:
```powershell
cd frontend
npm run build
```
*Yêu cầu: `vue-tsc` và Vite build hoàn tất không phát sinh bất kỳ lỗi nào.*

---

## 13. Bảng tra cứu và khắc phục lỗi thường gặp (Troubleshooting)

| STT | Triệu chứng lỗi | Nguyên nhân gốc | Biện pháp xử lý |
|:---:|---|---|---|
| **1** | Lỗi kết nối CSDL: `28P01: password authentication failed` | Mật khẩu CSDL trong cấu hình không khớp mật khẩu PostgreSQL đang chạy | Kiểm tra file `docker-compose.yml` (mặc định: `postgres`) hoặc cập nhật lại trong `appsettings.Development.json`. |
| **2** | `Npgsql.NpgsqlException: Connection refused (localhost:5432)` | PostgreSQL chưa được khởi động | Khởi chạy Docker container: `docker compose up -d postgres` hoặc kiểm tra dịch vụ PostgreSQL Service trong `services.msc`. |
| **3** | Cảnh báo cổng Kestrel: `Failed to bind to address http://localhost:5000: address already in use` | Cổng 5000 đang bị ứng dụng khác hoặc tiến trình .NET cũ chiếm dụng | Chạy lệnh PowerShell tìm và tắt process: `Get-Process -Id (Get-NetTCPConnection -LocalPort 5000).OwningProcess | Stop-Process -Force`. |
| **4** | Frontend báo lỗi: `Network Error` hoặc không nhận dữ liệu | Backend API chưa chạy hoặc proxy `/api` gặp sự cố | Đảm bảo Backend Terminal đang chạy trên cổng 5000 trước khi mở Frontend. |
| **5** | Đăng nhập báo `AUTH_001: Số điện thoại hoặc mật khẩu không chính xác` | Gõ sai số điện thoại hoặc mật khẩu mẫu | Sử dụng đúng số điện thoại (`0900000001` - `0900000005`) và mật khẩu chuẩn (`AdminPassword@123` hoặc `DemoPassword@123`). |
| **6** | Lỗi bảo mật PowerShell: `cannot be loaded because running scripts is disabled on this system` | Chính sách thực thi PowerShell trên Windows chặn script chưa ký | Mở PowerShell với quyền Administrator và chạy lệnh: `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser`. |
| **7** | Lỗi `npm install` hoặc không tìm thấy package | Thư mục `node_modules` bị xung đột hoặc phiên bản Node quá cũ | Nâng cấp Node.js lên v18/v20 LTS, xóa thư mục `node_modules` và chạy lại `npm install`. |

---

## 14. Tắt hệ thống khi kết thúc làm việc

- Nhấn `Ctrl + C` tại cửa sổ Terminal của Backend và Frontend.
- Dừng Docker container (vẫn giữ nguyên dữ liệu):
  ```powershell
  docker compose down
  ```