# Hệ Thống Quản Lý Phòng Khám Nha Khoa Hoàng Gia (PK_NK_HG)

Ứng dụng web quản lý vận hành và hồ sơ lâm sàng cho phòng khám nha khoa đơn lẻ theo quy trình khép kín: **Đặt lịch → Tiếp đón & Hàng đợi → Khám & Sơ đồ răng FDI → Kê đơn → Thanh toán → Kho vật tư & Báo cáo**.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![EF Core](https://img.shields.io/badge/EF_Core-10.0.0-512BD4)
![Vue.js](https://img.shields.io/badge/Vue.js-3.5.13-4FC08D?logo=vuedotjs)
![TypeScript](https://img.shields.io/badge/TypeScript-5.7.2-3178C6?logo=typescript)
![Vite](https://img.shields.io/badge/Vite-6.2.0-646CFF?logo=vite)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)

## Mục lục

1. [Giới thiệu dự án](#1-giới-thiệu-dự-án)
2. [Kiến trúc và công nghệ](#2-kiến-trúc-và-công-nghệ)
3. [Yêu cầu chuẩn bị trước khi chạy](#3-yêu-cầu-chuẩn-bị-trước-khi-chạy)
4. [Chạy nhanh trên Windows](#4-chạy-nhanh-trên-windows)
5. [Cơ sở dữ liệu demo và tài khoản thử nghiệm](#5-cơ-sở-dữ-liệu-demo-và-tài-khoản-thử-nghiệm)
6. [Chạy thủ công từng bước và cấu hình tùy chọn](#6-chạy-thủ-công-từng-bước-và-cấu-hình-tùy-chọn)
7. [Kiểm tra biên dịch và xử lý sự cố](#7-kiểm-tra-biên-dịch-và-xử-lý-sự-cố)
8. [Lưu ý triển khai thực tế (Production)](#8-lưu-ý-triển-khai-thực-tế-production)
9. [Thông tin đồ án và giấy phép](#9-thông-tin-đồ-án-và-giấy-phép)

---

## 1. Giới thiệu dự án

### 1.1. Bài toán và mục tiêu
Hệ thống được xây dựng cho **Phòng khám Nha khoa Hoàng Gia** nhằm chuẩn hóa quy trình phối hợp giữa các bộ phận trong phòng khám, thay thế sổ sách rời rạc và giảm sai sót trong điều trị nha khoa thông qua 4 mục tiêu trọng tâm:

1. **Phối hợp nhân sự theo vai trò:** Phân tách không gian làm việc rõ ràng cho 5 vai trò với cơ chế RBAC (Role-Based Access Control – phân quyền dựa trên vai trò): Quản trị viên (`ADMIN`), Lễ tân/Thu ngân (`RECEPTIONIST`), Nha sĩ (`DENTIST`), Phụ tá (`ASSISTANT`) và Bệnh nhân (`PATIENT`).
2. **Sơ đồ răng điện tử chuẩn FDI (Fédération Dentaire Internationale – Liên đoàn Nha khoa Thế giới):** Hỗ trợ bộ răng người lớn (32 răng: `11–18`, `21–28`, `31–38`, `41–48`) và bộ răng sữa trẻ em (20 răng: `51–55`, `61–65`, `71–75`, `81–85`) cùng 5 mặt răng (`M`, `D`, `B`, `L`, `O`), lưu vết lịch sử tình trạng bệnh lý và thủ thuật qua từng đợt khám.
3. **An toàn trong điều trị lâm sàng:** Tự động hiển thị biểu ngữ cảnh báo dị ứng thuốc, bệnh nền (tim mạch, tiểu đường, rối loạn đông máu) và chỉ số sinh hiệu (huyết áp, mạch) ngay khi Nha sĩ mở ca khám; cơ chế khóa/chốt hồ sơ sau khi hoàn tất khám.
4. **Minh bạch tài chính và kho vật tư:** Tạo hóa đơn nháp từ các dịch vụ đã thực hiện, hỗ trợ thanh toán một lần hoặc trả góp nhiều đợt theo tiến độ điều trị, theo dõi công nợ, cảnh báo tồn thuốc khi kê đơn và xác nhận tiêu hao vật tư theo ca khám.

### 1.2. Danh mục 12 phân hệ và 52 chức năng

| Mã phân hệ | Tên phân hệ | Số CN | Các chức năng chính |
|---|---|:---:|---|
| `MOD_AUTH` | Xác thực & phân quyền | 6 | Kích hoạt tài khoản bệnh nhân, đăng nhập JWT + RBAC, hồ sơ cá nhân & đổi mật khẩu, khôi phục mật khẩu (OTP), quản lý vai trò, nhật ký thao tác (Audit Log). |
| `MOD_PAT` | Bệnh nhân & hồ sơ | 9 | Tạo hồ sơ (kèm đối soát/quét QR CCCD), tìm kiếm nhanh, kiểm tra trùng, tiền sử & dị ứng, sinh hiệu, cảnh báo an toàn, dòng thời gian điều trị, khóa hồ sơ, tạo đợt khám (Visit). |
| `MOD_APP` | Đặt lịch bệnh nhân | 2 | Đặt lịch trực tuyến công khai (không cần đăng nhập) và đặt lịch tại quầy (Lễ tân đặt hộ). |
| `MOD_CHK` | Tiếp đón & hàng đợi | 6 | Xác nhận khách đến (kèm nhắc công nợ), hàng đợi chung, gọi số & dẫn bệnh nhân, bắt đầu/kết thúc khám, lịch sử trạng thái hàng đợi, điều phối Nha sĩ/X-quang. |
| `MOD_FDI` | Sơ đồ răng FDI | 7 | Hiển thị sơ đồ 32/20 răng, chọn răng/5 mặt răng, gán bệnh lý, chỉ định dịch vụ theo răng (lưu giá tại thời điểm chỉ định), cập nhật sau điều trị, lịch sử răng, ghi chú lâm sàng. |
| `MOD_IMG` | Hình ảnh hồ sơ | 2 | Tải ảnh X-quang/ảnh chụp trong miệng gắn với ca khám và răng; xem/thu phóng ảnh qua API có kiểm tra quyền truy cập. |
| `MOD_RX` | Đơn thuốc | 4 | Kê đơn thuốc điện tử, kiểm tra và cảnh báo tồn thuốc, in/xuất đơn thuốc (kèm mã QR tra cứu), lịch sử đơn thuốc. |
| `MOD_BIL` | Thanh toán & hóa đơn | 5 | Tạo hóa đơn nháp từ dịch vụ ca khám, áp dụng giảm giá/khuyến mãi, thu tiền một lần hoặc nhiều đợt (tiền mặt/chuyển khoản), in biên lai, hủy/điều chỉnh hóa đơn nháp. |
| `MOD_INV` | Kho & vật tư | 2 | Lập phiếu nhập kho vật tư (kèm cảnh báo ngưỡng tồn tối thiểu) và xác nhận xuất/tiêu hao vật tư thực tế theo ca khám. |
| `MOD_ASS` | Nghiệp vụ phụ tá | 1 | Quản lý trạng thái phòng điều trị, ghế nha khoa và phân công hỗ trợ theo ngày. |
| `MOD_MST` | Danh mục hệ thống | 7 | Quản lý dịch vụ & bảng giá, bệnh lý/triệu chứng, danh mục thuốc, vật tư & ngưỡng tồn, phòng & ghế, cấu hình hệ thống, tài khoản nhân viên. |
| `MOD_RPT` | Báo cáo & thống kê | 1 | Báo cáo doanh thu theo kỳ và phương thức thanh toán (chỉ tính các giao dịch đã thu tiền thực tế). |

### 1.3. Phạm vi và giới hạn của hệ thống
- **Mô hình áp dụng:** Một phòng khám nha khoa đơn lẻ (không bao gồm mô hình chuỗi nhiều chi nhánh).
- **Không bao gồm trong phạm vi:** Tích hợp Bảo hiểm Y tế (BHYT), chuẩn ảnh y tế DICOM hoặc phân tích ảnh bằng AI, xếp lịch trực ca nhân sự (`MOD_ROS`), nghiệp vụ cấp cứu/chen lịch ưu tiên.
- **Thanh toán & dịch vụ bên ngoài:** Ghi nhận Tiền mặt và Chuyển khoản ngân hàng xác nhận thủ công tại quầy; tính năng gửi tin nhắn SMS OTP và đối soát Webhook ngân hàng/VietQR được thiết kế ở dạng tùy chọn cấu hình (mặc định tắt khi chạy thử nghiệm cục bộ).

---

## 2. Kiến trúc và công nghệ

### 2.1. Sơ đồ kiến trúc tổng quan

```text
[ Trình duyệt Web (Máy tính / Điện thoại) ]
                   │
                   ▼  HTTP (http://localhost:5173)
┌──────────────────────────────────────────────────────────┐
│ Frontend SPA: Vue 3.5 + TypeScript + Vite + Pinia        │
│ (Tự động chuyển tiếp /api/* sang http://localhost:5000)  │
└──────────────────────────────────────────────────────────┘
                   │
                   ▼  REST API + JWT Bearer (http://localhost:5000)
┌──────────────────────────────────────────────────────────┐
│ Backend Web API: ASP.NET Core 10.0 (Clean Architecture)  │
│  ├─ Dental.Api            (Controllers, Middleware, RBAC)│
│  ├─ Dental.Application    (Services, DTOs, Validators)   │
│  ├─ Dental.Infrastructure (EF Core 10, Repositories,Seed)│
│  └─ Dental.Domain         (Entities, Enums, Constants)   │
└──────────────────────────────────────────────────────────┘
                   │
                   ▼  TCP Port 5432 (Database: dental_clinic)
┌──────────────────────────────────────────────────────────┐
│ Cơ sở dữ liệu: PostgreSQL 16 (Docker container dental_db)│
└──────────────────────────────────────────────────────────┘
```

### 2.2. Cấu trúc thư mục dự án

```text
PK_NK_HG/
├── backend/
│   ├── DentalClinic.sln                  # Solution .NET 10 gồm 4 tầng kiến trúc
│   └── src/
│       ├── Dental.Api/                   # Tầng giao tiếp API, cấu hình JWT/RBAC, Swagger
│       ├── Dental.Application/           # Tầng nghiệp vụ (Features theo từng phân hệ MOD_*)
│       ├── Dental.Domain/                # Thực thể (Entities), hằng số và kiểu liệt kê
│       └── Dental.Infrastructure/        # EF Core DbContext, migrations và dữ liệu Development
├── frontend/
│   ├── src/                              # Mã nguồn Vue 3 (views, components, stores, services, router)
│   ├── package.json                      # Khai báo thư viện Frontend
│   └── vite.config.ts                    # Cấu hình cổng 5173 và proxy /api -> cổng 5000
├── docker-compose.yml                    # Cấu hình container PostgreSQL 16 (dental_db, cổng 5432)
└── start-dev.ps1                         # Kịch bản khởi chạy tự động toàn hệ thống trên Windows
```

---

## 3. Yêu cầu chuẩn bị trước khi chạy

Các phần mềm nền tảng dưới đây cần được cài trước; `start-dev.ps1` không tự cài đặt chúng. Hướng dẫn khởi chạy tự động dành cho **Windows 10/11 64-bit**:

| Phần mềm cần chuẩn bị | Phiên bản yêu cầu | Lệnh kiểm tra trên PowerShell | Ghi chú / Nguồn tải chính thức |
|---|---|---|---|
| **1. Git** | `>= 2.30` | `git --version` | Dùng để tải kho mã nguồn: [git-scm.com](https://git-scm.com/). |
| **2. .NET SDK** | **`10.0.x`** | `dotnet --version` | Cần SDK (không chỉ Runtime) vì dự án nhắm tới `net10.0`: [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0). |
| **3. Node.js và npm** | Node.js `20.x` trở lên | `node -v` và `npm.cmd -v` | Cài bản LTS từ [nodejs.org](https://nodejs.org/). npm đi kèm Node.js. |
| **4. Docker Desktop** | Bản hiện hành, Linux containers | `docker --version` và `docker info` | Cần để chạy PostgreSQL theo cách được khuyên dùng. Mở Docker Desktop và đợi Docker Engine sẵn sàng trước khi chạy dự án. |
| **5. PowerShell** | Windows PowerShell `5.1+` hoặc PowerShell `7+` | `$PSVersionTable.PSVersion` | `start-dev.ps1` dành cho Windows. |

> Cổng `5000` (API) và `5173` (web) cần còn trống. Cổng `5432` được PostgreSQL local hoặc Docker sử dụng. Lần chạy đầu cần Internet để .NET restore package và npm tải package theo lock file.

---

## 4. Chạy nhanh trên Windows

Sau khi cài các công cụ ở mục 3 và mở **Docker Desktop**, mở **PowerShell** rồi chạy lần lượt:

### Bước 1: Clone mã nguồn từ GitHub
Mở PowerShell tại thư mục bạn muốn lưu đồ án (không cần tạo thư mục `PK_NK_HG` trước), rồi chạy:
```powershell
git clone https://github.com/NNVHG/PK_NK_HG.git
Set-Location PK_NK_HG
```

Nếu đã clone trước đó, vào thư mục repo và cập nhật mã nguồn trước khi chạy:
```powershell
Set-Location <đường-dẫn-tới-thư-mục-PK_NK_HG>
git pull
```

Không cần tải riêng các thư mục `backend` hoặc `frontend`; hãy giữ nguyên cấu trúc repo. Không commit `appsettings.Development.json`, dữ liệu database, file upload hoặc thông tin thật.

### Bước 2: Khởi động hệ thống
Lệnh này chạy tại thư mục gốc `PK_NK_HG`. Nếu Windows chặn script, dùng lệnh dưới đây; không cần đổi Execution Policy toàn máy.
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start-dev.ps1
```

Script kiểm tra .NET SDK 10 và Node.js, tạo cấu hình Development mẫu nếu chưa có, khởi động PostgreSQL 16 bằng Docker Compose, đợi database sẵn sàng, cài frontend bằng `npm ci` theo `package-lock.json` nếu cần, rồi mở cửa sổ riêng cho API và web. Backend khôi phục NuGet package khi chạy lần đầu. Khi API khởi động, ứng dụng tự áp dụng các EF Core migration hiện có và chạy seeder Development theo cấu hình.

Script không xóa database hoặc Docker volume hiện có. Đóng cửa sổ API/web để dừng ứng dụng; PostgreSQL vẫn chạy trong Docker.

Lần chạy đầu cần Internet để Git clone, tải NuGet package và npm package. Nếu Windows báo chặn script, dùng đúng lệnh PowerShell ở trên; không cần đổi Execution Policy toàn máy.

Luồng demo chính: khách đặt lịch công khai hoặc PATIENT đặt lịch trong portal → Lễ tân đối soát và xác nhận tiếp đón tại quầy → hàng đợi chung → Nha sĩ vào phòng do Admin phân công, xem cảnh báo an toàn, khám và cập nhật FDI → kê đơn/kết thúc khám → thu ngân xử lý hóa đơn. Đặt lịch công khai không tạo lượt chờ; gọi số không tự bắt đầu khám. Với trẻ dưới 14 tuổi không nhập CCCD trẻ; liên kết người giám hộ chỉ sau khi nhân viên xác minh trực tiếp. Menu không thay thế phân quyền backend.

### Bước 3: Mở trình duyệt và trải nghiệm
- **Giao diện ứng dụng (Frontend):** `http://localhost:5173`
- **Tài liệu API (Swagger UI):** `http://localhost:5000/swagger`

Nếu cửa sổ API báo lỗi khởi động, hãy xử lý lỗi ở cửa sổ đó trước; giao diện web có thể mở được nhưng các chức năng cần API sẽ chưa hoạt động.

---

## 5. Cơ sở dữ liệu demo và tài khoản thử nghiệm

Khi chạy ở môi trường `Development` với cấu hình mẫu (`Seed:DemoUsers = true`), hệ thống nạp dữ liệu tổng hợp phục vụ demo, như danh mục, tài khoản theo vai trò và một số kịch bản lịch hẹn/khám/hóa đơn. Đây là dữ liệu phát triển cục bộ; không dùng trong môi trường thực tế. Dữ liệu seed có thể thay đổi theo mã nguồn nên không dựa vào số lượng bản ghi cố định.

### 5.1. Bảng tài khoản thử nghiệm 5 vai trò
> **Lưu ý bảo mật:** Toàn bộ số điện thoại, CCCD, mật khẩu và khóa cấu hình dưới đây **chỉ dùng cho mục đích thử nghiệm cục bộ (Demo/Development)**, tuyệt đối không dùng cho môi trường thực tế.

Tại màn hình đăng nhập (`http://localhost:5173/login`), giữ tùy chọn mặc định **`SĐT / CCCD`** và đăng nhập bằng một trong các tài khoản sau:

| Vai trò | Mã vai trò | Tài khoản đăng nhập (SĐT / CCCD) | Mật khẩu thử nghiệm | Chức năng trải nghiệm chính |
|---|---|---|---|---|
| **Quản trị viên (Admin)** | `ADMIN` | `0900000001` | `AdminPassword@123` | Quản lý danh mục hệ thống, nhân sự, cấu hình, nhập/xuất kho, xem báo cáo doanh thu và nhật ký thao tác (Audit Log). |
| **Lễ tân / Thu ngân** | `RECEPTIONIST` | `0900000002` | `DemoPassword@123` | Đặt lịch tại quầy, quét QR CCCD tiếp đón khách, quản lý hàng đợi, lập hóa đơn nháp, thu tiền mặt/chuyển khoản. |
| **Nha sĩ** | `DENTIST` | `0900000003` | `DemoPassword@123` | Mở ca khám, xem cảnh báo an toàn, tương tác sơ đồ răng FDI 32/20 răng, chỉ định dịch vụ, kê đơn thuốc, chốt hồ sơ. |
| **Phụ tá** | `ASSISTANT` | `0900000004` | `DemoPassword@123` | Quản lý trạng thái phòng/ghế nha, gọi số dẫn bệnh nhân, tải ảnh X-quang, lập phiếu nhập và xác nhận tiêu hao vật tư ca khám. |
| **Bệnh nhân** | `PATIENT` | `074088001234` *(CCCD)* | `DemoPassword@123` | Xem hồ sơ khám răng cá nhân, lịch sử sơ đồ răng FDI, đơn thuốc điện tử, hóa đơn/công nợ và đặt lịch khám. |

*(Ngoài ra còn có các tài khoản nhân sự/bệnh nhân phụ trong dữ liệu mẫu dùng chung mật khẩu `DemoPassword@123`: Lễ tân 2 `0900000012`, Nha sĩ 2 `0900000013`, Phụ tá 2 `0900000014`, Bệnh nhân 2 CCCD `074095005678`, Bệnh nhân 3 CCCD `074082009012`).*

### 5.2. Dữ liệu QR CCCD
Chỉ dùng QR/CCCD tổng hợp dành cho demo hoặc dữ liệu do bạn tự tạo. Không đưa CCCD thật vào README, issue, log, URL hoặc commit. Khi đối soát, dữ liệu QR được gửi trong request body đến backend; không đặt CCCD trên URL.

---

## 6. Chạy thủ công từng bước và cấu hình tùy chọn

Nếu bạn không muốn dùng kịch bản tự động `start-dev.ps1` hoặc muốn chạy trên máy cài **PostgreSQL 16 trực tiếp (không dùng Docker)**, hãy thực hiện tuần tự các bước sau từ thư mục gốc `PK_NK_HG`:

### Bước 1: Chuẩn bị tệp cấu hình Development
Chỉ tạo file này nếu chưa có; lệnh không dùng `-Force` để tránh ghi đè cấu hình local.
```powershell
Copy-Item backend/src/Dental.Api/appsettings.Development.example.json backend/src/Dental.Api/appsettings.Development.json
```

### Bước 2: Khởi động PostgreSQL 16
- **Nếu dùng Docker:**
  ```powershell
  docker compose up -d postgres
  docker exec dental_db pg_isready -U postgres -d dental_clinic
  ```
- **Nếu dùng PostgreSQL 16 cài trực tiếp trên máy (không dùng Docker):**
  Tạo cơ sở dữ liệu trống tên `dental_clinic` bằng `psql` (hoặc pgAdmin), sau đó sửa `Username`/`Password` trong file Development cho khớp máy của bạn. Không sửa file `.example.json` và không commit `appsettings.Development.json`:
  ```powershell
  psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE dental_clinic;"
  ```
  Có thể dùng script để mở API và web nhưng bỏ qua Docker:
  ```powershell
  .\start-dev.ps1 -SkipDocker
  ```
  Hoặc tiếp tục chạy thủ công ở các bước bên dưới.

### Bước 3: Chạy Backend API (PowerShell 1 – thư mục gốc)
Ở Development, backend tự áp dụng các migration còn thiếu và nạp dữ liệu mẫu theo cấu hình:
```powershell
dotnet run --project backend/src/Dental.Api
```

### Bước 4: Cài package và chạy Frontend (PowerShell 2 – thư mục gốc)
```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run dev -- --strictPort
```

### Cấu hình tùy chọn (Mặc định tắt)
- **SMS OTP (`PasswordRecovery:Sms:Enabled` và `PatientActivation:Sms:Enabled`):** Mặc định đặt `false` trong cấu hình mẫu. Khi tắt, luồng khôi phục mật khẩu/kích hoạt ở môi trường `Development` hoạt động ở chế độ mô phỏng nội bộ mà không cần tài khoản nhà mạng.
- **Webhook ngân hàng:** Mặc định tắt; thu ngân xác nhận giao dịch chuyển khoản thủ công tại quầy sau khi đối chiếu tham chiếu giao dịch.

---

## 7. Kiểm tra biên dịch và xử lý sự cố

### 7.1. Kiểm tra biên dịch Backend và Frontend
Chạy các lệnh sau từ thư mục gốc `PK_NK_HG` để kiểm tra đóng gói mã nguồn:

```powershell
# 1. Kiểm tra biên dịch toàn bộ 4 tầng Backend (.NET 10)
dotnet build backend/DentalClinic.sln

# 2. Kiểm tra kiểu TypeScript và đóng gói giao diện Frontend (Vite build)
npm.cmd --prefix frontend run build
```

### 7.2. Bảng xử lý sự cố thường gặp (Troubleshooting)

| Triệu chứng / Thông báo lỗi | Nguyên nhân | Cách khắc phục |
|---|---|---|
| PowerShell báo `...cannot be loaded because running scripts is disabled on this system` | Chính sách `ExecutionPolicy` mặc định của Windows chặn chạy file `.ps1`. | Chạy bằng lệnh: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start-dev.ps1`. |
| Backend báo thiếu khóa HMAC hoặc lỗi đăng nhập PostgreSQL | File Development bị thiếu, cấu hình sai hoặc không khớp PostgreSQL đang chạy. | Chạy lại `start-dev.ps1` để tạo file mẫu nếu file chưa có. Nếu đã có file, kiểm tra `ConnectionStrings:Default` và đối chiếu username/password với Docker Compose hoặc PostgreSQL cục bộ. Không ghi thông tin kết nối thật vào Git. |
| Lỗi `error during connect... dockerDesktopLinuxEngine` khi chạy `docker compose up -d` | Ứng dụng **Docker Desktop** chưa được mở hoặc chưa khởi động xong. | Mở ứng dụng Docker Desktop trên Windows, đợi biểu tượng góc dưới bên trái báo *Engine running* rồi chạy lại. |
| Lỗi báo cổng `5000`, `5173` hoặc `5432` đã được sử dụng (`Address already in use`) | Đã có một cửa sổ Backend/Frontend hoặc dịch vụ PostgreSQL khác đang chiếm cổng. | Đóng cửa sổ PowerShell đang chạy phiên cũ, hoặc kiểm tra tiến trình chiếm cổng bằng `Get-NetTCPConnection -LocalPort 5000,5173,5432`. |
| Muốn xóa database demo | Docker volume `pgdata` giữ dữ liệu qua các lần chạy. | **Thao tác này xóa vĩnh viễn database cục bộ.** Chỉ khi chắc chắn không cần dữ liệu hiện tại: dừng backend, chạy `docker compose down -v`, rồi chạy lại `start-dev.ps1`. Không chạy nếu cần giữ dữ liệu. |

---

## 8. Lưu ý triển khai thực tế (Production)

Kho mã nguồn này được cấu hình tối ưu cho mục đích chạy thử nghiệm và đánh giá đồ án tốt nghiệp. Hiện chưa kèm kịch bản triển khai máy chủ Production tự động; nếu triển khai thực tế cần thực hiện tối thiểu các bước sau:
1. **Thay toàn bộ khóa và mật khẩu:** Tạo khóa ngẫu nhiên tối thiểu 32 byte cho `Jwt:Secret` và `IdentitySecurity:HmacKey` qua biến môi trường hoặc trình quản lý bí mật; đổi mật khẩu PostgreSQL và mật khẩu tài khoản `ADMIN`.
2. **Tắt nạp dữ liệu thử nghiệm:** Đặt `ASPNETCORE_ENVIRONMENT=Production` và `Seed:DemoUsers=false`.
3. **Bảo mật đường truyền & Lưu trữ:** Bật chứng chỉ HTTPS (TLS) qua Reverse Proxy (Nginx/IIS), cấu hình sao lưu định kỳ cơ sở dữ liệu PostgreSQL và thư mục lưu ảnh X-quang (`uploads/`).

---

## 9. Thông tin đồ án và giấy phép

- **Đề tài:** Xây dựng Hệ thống quản lý phòng khám nha khoa Hoàng Gia
- **Trường / Đơn vị:** Trường Đại học Thủ Dầu Một – Viện Công nghệ số
- **Ngành:** Công nghệ thông tin
- **Sinh viên thực hiện:** Nguyễn Ngô Vũ Hoàng Gia
- **MSSV:** `2224802010628` – **Lớp:** `D22CNTT02` – **Khóa:** `2022–2027`
- **Giảng viên hướng dẫn:** ThS. Ngô Thị Ngọc Dịu
- **Giấy phép:** Dự án phục vụ mục đích học tập và nghiên cứu khoa học.
