# Hệ thống quản lý phòng khám nha khoa Hoàng Gia

Đồ án tốt nghiệp ngành Công nghệ thông tin, Trường Đại học Thủ Dầu Một.

Hệ thống phục vụ một phòng khám nha khoa. Luồng trình diễn đi từ đặt lịch, tiếp đón và hàng đợi đến khám, sơ đồ răng FDI, kê đơn và thanh toán. Kho vật tư và báo cáo doanh thu hỗ trợ vận hành phòng khám.

## Chạy ứng dụng cục bộ

Cần PowerShell 7, .NET SDK 10, Node.js 22 trở lên và Docker chạy Linux containers. Chạy các lệnh từ thư mục gốc của kho mã.

Tạo PostgreSQL 16 riêng cho dữ liệu phát triển. Lần đầu chọn mật khẩu gồm chữ và số.

    $env:POSTGRES_PASSWORD = Read-Host 'Mật khẩu PostgreSQL cục bộ' -MaskInput
    docker run --name pk-nk-hg-dev --env POSTGRES_PASSWORD --env POSTGRES_DB=dental_clinic --publish 127.0.0.1:55432:5432 --detach postgres:16-alpine

Chạy `docker start pk-nk-hg-dev` để mở lại container đã dừng. Chờ PostgreSQL sẵn sàng trước khi mở API và dùng mật khẩu đã đặt lúc tạo container.

Trong cửa sổ PowerShell chạy API, đặt lại biến `POSTGRES_PASSWORD` nếu mở phiên mới. Dùng khóa JWT và khóa HMAC riêng, mỗi khóa ít nhất 32 byte. Giữ cùng khóa HMAC khi dùng lại cơ sở dữ liệu.

    $env:ConnectionStrings__Default = "Host=127.0.0.1;Port=55432;Database=dental_clinic;Username=postgres;Password=$env:POSTGRES_PASSWORD"
    $env:Jwt__Secret = Read-Host 'Khóa JWT cục bộ' -MaskInput
    $env:IdentitySecurity__HmacKey = Read-Host 'Khóa HMAC cục bộ' -MaskInput
    $env:Seed__AdminPhone = Read-Host 'Số điện thoại Admin thử nghiệm'
    $env:Seed__AdminPassword = Read-Host 'Mật khẩu Admin thử nghiệm' -MaskInput
    dotnet run --project backend/src/Dental.Api

Trong cửa sổ PowerShell khác, chạy frontend.

    npm --prefix frontend ci
    npm --prefix frontend run dev

Mở [ứng dụng](http://localhost:5173) hoặc [Swagger](http://localhost:5000/swagger).

## Mã nguồn

- [Frontend](frontend/) dùng Vue 3, TypeScript, Vite, Pinia và Vue Router.
- [Backend](backend/) dùng ASP.NET Core 10, EF Core và PostgreSQL. API thực thi quyền của Admin, Lễ tân/Thu ngân, Nha sĩ, Phụ tá và Bệnh nhân.

## Kiểm thử

Chạy từ thư mục gốc của kho mã:

    dotnet test backend/DentalClinic.sln
    npm --prefix frontend ci
    node --test frontend/tests/*.test.mjs
    npm --prefix frontend run build

Dùng dữ liệu tổng hợp và cơ sở dữ liệu cô lập cho các bài kiểm thử tích hợp.
