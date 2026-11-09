# Hệ thống quản lý phòng khám nha khoa Hoàng Gia

Đồ án tốt nghiệp ngành Công nghệ thông tin, Trường Đại học Thủ Dầu Một.

Ứng dụng quản lý đặt lịch, tiếp đón, hàng đợi, khám, sơ đồ răng FDI, kê đơn và thanh toán. Hệ thống còn theo dõi kho vật tư và báo cáo doanh thu.

## Cài đặt và chạy trên Windows

Cài Git, PowerShell 7.2 trở lên, .NET SDK 10, Node.js 22 trở lên và Docker Desktop ở chế độ Linux containers. Lần đầu cần kết nối mạng để tải image PostgreSQL và các gói NuGet, npm. Mở Docker Desktop, sau đó clone kho mã trong PowerShell.

```powershell
git clone https://github.com/NNVHG/PK_NK_HG.git
Set-Location PK_NK_HG
```

### 1. Khởi động PostgreSQL

Lần đầu, tạo container và chọn mật khẩu PostgreSQL gồm chữ và số. Giữ mật khẩu này để kết nối ở những lần chạy sau.

```powershell
$env:POSTGRES_PASSWORD = Read-Host 'Mật khẩu PostgreSQL cục bộ' -MaskInput
docker run --name pk-nk-hg-dev --env POSTGRES_PASSWORD --env POSTGRES_DB=dental_clinic --publish 127.0.0.1:55432:5432 --detach postgres:16-alpine
```

Các lần sau, dùng `docker start pk-nk-hg-dev` nếu container đang dừng. Kiểm tra PostgreSQL và đợi đến khi lệnh báo `accepting connections`.

```powershell
docker exec pk-nk-hg-dev pg_isready -h 127.0.0.1 -U postgres -d dental_clinic
```

### 2. Chạy API

Trong PowerShell ở thư mục gốc, nhập lại mật khẩu PostgreSQL nếu mở cửa sổ mới. Chuẩn bị hai khóa ngẫu nhiên riêng cho JWT và HMAC, mỗi khóa ít nhất 32 byte. Lưu chúng ở nơi an toàn ngoài Git và dùng lại khóa HMAC với cùng cơ sở dữ liệu.

Số điện thoại Admin gồm 10 chữ số và bắt đầu bằng 0. Chọn mật khẩu từ 8 ký tự, có chữ hoa, chữ thường và chữ số, tối đa 72 byte UTF-8.

```powershell
if (-not $env:POSTGRES_PASSWORD) {
    $env:POSTGRES_PASSWORD = Read-Host 'Mật khẩu PostgreSQL đã đặt' -MaskInput
}
$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=55432;Database=dental_clinic;Username=postgres;Password=$env:POSTGRES_PASSWORD"
$env:Jwt__Secret = Read-Host 'Khóa JWT cục bộ' -MaskInput
$env:IdentitySecurity__HmacKey = Read-Host 'Khóa HMAC cục bộ' -MaskInput
$env:Seed__AdminPhone = Read-Host 'Số điện thoại Admin'
$env:Seed__AdminPassword = Read-Host 'Mật khẩu Admin' -MaskInput
dotnet run --project backend/src/Dental.Api
```

Lần chạy đầu, API tự áp dụng migration và tạo tài khoản Admin. Đợi API lắng nghe ở cổng 5000. Khi dùng lại cơ sở dữ liệu, đăng nhập bằng mật khẩu đã tạo hoặc đã đổi trong ứng dụng. Có thể kiểm tra API tại [Swagger](http://localhost:5000/swagger).

### 3. Chạy giao diện

Mở cửa sổ PowerShell thứ hai tại thư mục gốc của kho mã. Chạy `npm.cmd ci` khi mới clone hoặc khi `frontend/package-lock.json` thay đổi.

```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run dev -- --strictPort
```

Mở [ứng dụng](http://localhost:5173) và đăng nhập bằng tài khoản Admin. Để dừng, nhấn `Ctrl+C` ở hai cửa sổ và chạy `docker stop pk-nk-hg-dev`. Container giữ dữ liệu cho lần chạy sau.

## Mã nguồn

- [Frontend](frontend/) dùng Vue 3, TypeScript, Vite, Pinia và Vue Router.
- [Backend](backend/) dùng ASP.NET Core 10, EF Core và PostgreSQL. API thực thi quyền của Admin, Lễ tân/Thu ngân, Nha sĩ, Phụ tá và Bệnh nhân.

## Kiểm thử

Chạy từ thư mục gốc của kho mã.

```powershell
dotnet test backend/DentalClinic.sln
npm.cmd --prefix frontend ci
node --test frontend/tests/*.test.mjs
npm.cmd --prefix frontend run build
```

Dùng dữ liệu tổng hợp và cơ sở dữ liệu cô lập cho các bài kiểm thử tích hợp.
