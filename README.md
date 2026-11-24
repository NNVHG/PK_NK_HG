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
├── secrets.example.json                  # Mẫu cấu hình trống có thể đưa lên Git
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

Giữ nguyên cấu trúc thư mục của repo. Git bỏ qua `secrets.json`, cấu hình Development riêng và thư mục tải ảnh.

### Bước 2: Tạo cấu hình cục bộ

Chỉ [secrets.example.json](secrets.example.json) được đưa lên Git. Các ô trong file mẫu để trống. Từ thư mục gốc, tạo bản riêng rồi mở để điền:

```powershell
if (-not (Test-Path -LiteralPath .\secrets.json)) {
    Copy-Item -LiteralPath .\secrets.example.json -Destination .\secrets.json
}
notepad .\secrets.json
```

Điền chuỗi kết nối, khóa JWT và khóa HMAC. Xem [các trường cấu hình và cách tạo khóa](#61-tệp-bí-mật-cục-bộ) ở mục 6.1. Cơ sở dữ liệu có sẵn phải dùng lại khóa HMAC cũ để dữ liệu định danh vẫn đối soát được.

### Bước 3: Khởi động hệ thống

Chạy ở thư mục gốc `PK_NK_HG`:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\start-dev.ps1
```

Script đọc `secrets.json` và cấp đúng nhóm biến môi trường cho API, Vite và Docker Compose. Sau khi kiểm tra công cụ và khôi phục NuGet, script chờ PostgreSQL sẵn sàng, cài frontend bằng `npm ci` nếu cần rồi mở hai cửa sổ API và web. API áp dụng các migration EF Core còn thiếu và nạp dữ liệu Development theo cấu hình.

Script không xóa database hoặc Docker volume hiện có. Đóng cửa sổ API/web để dừng ứng dụng; PostgreSQL vẫn chạy trong Docker.

Lần chạy đầu cần Internet để Git clone, tải NuGet package và npm package. Nếu Windows báo chặn script, dùng đúng lệnh PowerShell ở trên; không cần đổi Execution Policy toàn máy.

Luồng demo chính: khách đặt lịch công khai hoặc PATIENT đặt lịch trong portal → Lễ tân đối soát và xác nhận tiếp đón tại quầy → hàng đợi chung → Nha sĩ chọn phòng, xem cảnh báo an toàn, khám và cập nhật FDI → kê đơn/kết thúc khám → thu ngân xử lý hóa đơn. Sau đăng nhập, Nha sĩ chọn phòng và Phụ tá chọn nha sĩ hỗ trợ hoặc xác nhận làm việc độc lập. Đặt lịch công khai không tạo lượt chờ; gọi số không tự bắt đầu khám. Với trẻ dưới 14 tuổi không nhập CCCD trẻ; liên kết người giám hộ chỉ sau khi nhân viên xác minh trực tiếp. Menu không thay thế phân quyền backend.

Khi đặt hoặc đổi lịch, chọn ngày khám và **Buổi sáng** hoặc **Buổi chiều**. Giao diện chọn mốc giờ từ lịch cấu hình (DL-378). Máy chủ kiểm tra thời lượng, trùng lịch và công suất trước khi ghi.

### Bước 4: Mở trình duyệt và trải nghiệm
- **Giao diện ứng dụng (Frontend):** `http://localhost:5173`
- **Tài liệu API (Swagger UI):** `http://localhost:5000/swagger`

Nếu cửa sổ API báo lỗi khởi động, hãy xử lý lỗi ở cửa sổ đó trước; giao diện web có thể mở được nhưng các chức năng cần API sẽ chưa hoạt động.

---

## 5. Cơ sở dữ liệu demo và tài khoản thử nghiệm

Để nạp dữ liệu demo trên cơ sở dữ liệu cục bộ, điền `Seed__DemoUsers` là `true` và chọn `Seed__DemoPassword`. Điền `Seed__AdminPhone` và `Seed__AdminPassword` để tạo Admin ban đầu khi chưa có. Seeder tạo dữ liệu tổng hợp cho lịch hẹn, khám và hóa đơn. Thay đổi mật khẩu trong tệp cấu hình không đổi mật khẩu của tài khoản đã tồn tại.

### 5.1. Bảng tài khoản thử nghiệm 5 vai trò
> SĐT và CCCD bên dưới là dữ liệu mẫu dùng để chạy thử. Mật khẩu đọc từ `secrets.json` của bạn.

Tại màn hình đăng nhập (`http://localhost:5173/login`), giữ tùy chọn mặc định **`SĐT / CCCD`** và đăng nhập bằng một trong các tài khoản sau:

| Vai trò | Mã vai trò | Tài khoản đăng nhập (SĐT / CCCD) | Mật khẩu thử nghiệm | Chức năng trải nghiệm chính |
|---|---|---|---|---|
| **Quản trị viên (Admin)** | `ADMIN` | Giá trị `Seed__AdminPhone` | `Seed__AdminPassword` | Quản lý danh mục, nhân sự, cấu hình, kho, doanh thu và nhật ký thao tác. |
| **Lễ tân / Thu ngân** | `RECEPTIONIST` | `0900000002` | `Seed__DemoPassword` | Đặt lịch tại quầy, quét QR CCCD, tiếp đón, quản lý hàng đợi và thu tiền. |
| **Nha sĩ** | `DENTIST` | `0900000003` | `Seed__DemoPassword` | Chọn phòng, mở ca khám, xem cảnh báo an toàn, cập nhật FDI, kê đơn và chốt hồ sơ. |
| **Phụ tá** | `ASSISTANT` | `0900000004` | `Seed__DemoPassword` | Chọn Nha sĩ hỗ trợ, gọi số theo quyền, tải ảnh và xác nhận nhập hoặc tiêu hao vật tư. |
| **Bệnh nhân** | `PATIENT` | `074088001234` *(CCCD mẫu)* | `Seed__DemoPassword` | Xem hồ sơ, đơn thuốc, hóa đơn và đặt lịch. Người giám hộ xem được hồ sơ trẻ đã liên kết và còn hiệu lực. |

Tài khoản mẫu bổ sung cũng dùng mật khẩu `Seed__DemoPassword`. Lễ tân 2 `0900000012`, Nha sĩ 2 `0900000013`, Phụ tá 2 `0900000014`, Bệnh nhân 2 CCCD `074095005678` và Bệnh nhân 3 CCCD `074082009012`.

### 5.2. Dữ liệu QR CCCD
Chỉ dùng QR/CCCD tổng hợp dành cho demo hoặc dữ liệu do bạn tự tạo. Không đưa CCCD thật vào README, issue, log, URL hoặc commit. Khi đối soát, dữ liệu QR được gửi trong request body đến backend; không đặt CCCD trên URL.

---

## 6. Chạy thủ công từng bước và cấu hình tùy chọn

### 6.1. Tệp bí mật cục bộ

Điền vào `secrets.json` ở thư mục gốc. Các tên có dấu `__` là tên biến môi trường của ASP.NET Core.

| Trường | Giá trị cần điền |
|---|---|
| `ConnectionStrings__Default` | Chuỗi kết nối PostgreSQL cục bộ. Ví dụ cấu trúc `Host=localhost;Port=5432;Database=dental_clinic;Username=postgres;Password=<mật khẩu cục bộ>`. |
| `POSTGRES_PASSWORD` | Khi dùng Docker Compose, điền mật khẩu của cơ sở dữ liệu Docker và dùng cùng giá trị trong chuỗi kết nối. Với volume có sẵn, dùng mật khẩu đã tạo trước đó. |
| `Jwt__Secret`, `IdentitySecurity__HmacKey` | Hai khóa độc lập có ít nhất 32 byte. Với bộ khóa HMAC theo phiên bản, thêm `IdentitySecurity__Keys__1`, `IdentitySecurity__Keys__2` theo phiên bản cần giữ và đặt `IdentitySecurity__CurrentVersion`. |
| `Seed__AdminPhone`, `Seed__AdminPassword`, `Seed__DemoUsers`, `Seed__DemoPassword` | Mật khẩu do bạn đặt cho tài khoản thử. Bật `Seed__DemoUsers` để nạp dữ liệu tổng hợp. `Seed__PatientTestFixtures` dành cho bộ fixture cục bộ có sẵn. |
| `PasswordRecovery__Sms__*`, `PatientActivation__Sms__*`, `GuestBooking__Sms__*` | Cấu hình SMS nếu dùng. Chỉ đặt `Enabled` thành `true` khi có thông tin gửi hợp lệ. Đặt lịch công khai theo luồng hiện hành không yêu cầu OTP. |
| `Payment__WebhookSecretKey`, `Payment__WebhookActorUserId` | Tùy chọn khi dùng webhook ngân hàng. Có thể để trống khi xác nhận chuyển khoản thủ công. |

| `ViettelTts__Token`, `ViettelTts__Voice`, `ViettelTts__Speed` | Cấu hình backend cho Viettel AI Text to Speech. Giọng mặc định `hcm-diemmy`, tốc độ `0.95`. Token lưu trong `secrets.json` cục bộ hoặc hệ thống bí mật của máy chủ. `start-dev.ps1` cũng nhận các tên `VIETTEL_TTS_*` cũ trong `secrets.json`. |

Mở `/queue/display` và bật âm thanh để nhận lời mời từ Nha sĩ. Backend gửi câu đọc tên công khai tới Viettel AI và trả MP3 khi lời mời còn hiệu lực. Nếu token hết hạn, hết hạn mức hoặc dịch vụ gặp lỗi, TV phát tiếng **Bing Bong** và vẫn hiện số, tên và phòng. **Nghe thử chuông** phát tại thiết bị. [Tài liệu API Viettel AI](https://viettelai.vn/en/tai-lieu)

Nếu đang chuyển từ `backend/src/Dental.Api/appsettings.Development.json` và `frontend/.env.local`, chép các giá trị đang dùng sang `secrets.json`. Những ô có giá trị trong tệp trung tâm ghi đè cấu hình cùng tên. Ô trống không ghi đè cấu hình cũ. Để tắt SMS đang bật trong cấu hình cũ, điền rõ `Enabled` là `false`. Đóng và chạy lại API/Vite sau khi đổi cấu hình.

Nếu cài mới và chưa có khóa, đoạn lệnh sau chỉ sinh các khóa còn trống và ghi thẳng vào tệp riêng:

```powershell
$local = Get-Content -LiteralPath .\secrets.json -Raw -Encoding UTF8 | ConvertFrom-Json
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    foreach ($name in @('Jwt__Secret', 'IdentitySecurity__HmacKey')) {
        if ([string]::IsNullOrWhiteSpace($local.$name)) {
            $bytes = New-Object byte[] 48
            $rng.GetBytes($bytes)
            $local.$name = [Convert]::ToBase64String($bytes)
        }
    }
}
finally { $rng.Dispose() }
$local | ConvertTo-Json | Set-Content -LiteralPath .\secrets.json -Encoding UTF8
```

Kiểm tra trước khi đưa mã lên Git:

```powershell
git check-ignore -v -- secrets.json
git diff --cached --name-only
```

Tệp được chia sẻ là `secrets.example.json` với các ô trống. Giữ `secrets.json` và bản sao lưu ở nơi riêng.

### 6.2. Chạy từng dịch vụ

Chạy lệnh nạp cấu hình trong **cùng cửa sổ PowerShell** với lệnh dịch vụ sau đó, từ thư mục gốc. Nếu PowerShell chặn chạy script, bật cho phiên đó bằng `Set-ExecutionPolicy -Scope Process Bypass`.

**PostgreSQL trong Docker**:

```powershell
.\start-dev.ps1 -LoadSecretsOnly Docker
docker compose up -d postgres
docker exec dental_db pg_isready -U postgres -d dental_clinic
```

Nếu đã cài PostgreSQL 16 trực tiếp, tạo cơ sở dữ liệu cục bộ bằng pgAdmin hoặc `psql` và cập nhật chuỗi kết nối. Có thể dùng `.\start-dev.ps1 -SkipDocker` khi PostgreSQL đang chạy tại cổng 5432 hoặc tiếp tục chạy từng dịch vụ bên dưới.

**API**, trong một cửa sổ mới:

```powershell
.\start-dev.ps1 -LoadSecretsOnly Backend
dotnet run --project backend/src/Dental.Api
```

**Frontend**, trong một cửa sổ mới:

```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run dev -- --strictPort
```

Để xem bản build, chạy `npm.cmd --prefix frontend run build` rồi `npm.cmd --prefix frontend run preview`. Khi không cấu hình SMS, màn hình khôi phục mật khẩu và kích hoạt có thể hiển thị OTP thử trong Development. Thu ngân có thể xác nhận chuyển khoản thủ công khi không cấu hình webhook.

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
| Backend báo thiếu khóa JWT/HMAC hoặc lỗi đăng nhập PostgreSQL | Thiếu trường trong `secrets.json` hoặc dùng sai mật khẩu cơ sở dữ liệu. | Kiểm tra các trường bắt buộc ở mục 6.1. Đối với volume hiện có, xác nhận chuỗi kết nối dùng mật khẩu đã tạo cho volume đó. Khởi động lại dịch vụ sau khi cập nhật. |
| Lỗi `error during connect... dockerDesktopLinuxEngine` khi chạy `docker compose up -d` | Ứng dụng **Docker Desktop** chưa được mở hoặc chưa khởi động xong. | Mở ứng dụng Docker Desktop trên Windows, đợi biểu tượng góc dưới bên trái báo *Engine running* rồi chạy lại. |
| Lỗi báo cổng `5000`, `5173` hoặc `5432` đã được sử dụng (`Address already in use`) | Đã có một cửa sổ Backend/Frontend hoặc dịch vụ PostgreSQL khác đang chiếm cổng. | Đóng cửa sổ PowerShell đang chạy phiên cũ, hoặc kiểm tra tiến trình chiếm cổng bằng `Get-NetTCPConnection -LocalPort 5000,5173,5432`. |
| Muốn xóa database demo | Docker volume `pgdata` giữ dữ liệu qua các lần chạy. | **Thao tác này xóa vĩnh viễn database cục bộ.** Chỉ khi chắc chắn không cần dữ liệu hiện tại: dừng backend, chạy `docker compose down -v`, rồi chạy lại `start-dev.ps1`. Không chạy nếu cần giữ dữ liệu. |

---

## 8. Lưu ý triển khai thực tế (Production)

Kho mã nguồn này được cấu hình tối ưu cho mục đích chạy thử nghiệm và đánh giá đồ án tốt nghiệp. Hiện chưa kèm kịch bản triển khai máy chủ Production tự động; nếu triển khai thực tế cần thực hiện tối thiểu các bước sau:
1. **Cấp bí mật riêng cho môi trường triển khai:** Dùng biến môi trường hoặc trình quản lý bí mật cho JWT, HMAC, PostgreSQL và các nhà cung cấp. `secrets.json` và `start-dev.ps1` dành cho máy phát triển. Với dữ liệu định danh đã tồn tại, giữ các phiên bản HMAC cần dùng để đối soát.
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
