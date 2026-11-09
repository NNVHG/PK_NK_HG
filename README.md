# Hệ thống quản lý phòng khám nha khoa Hoàng Gia

Đồ án tốt nghiệp ngành Công nghệ thông tin, Trường Đại học Thủ Dầu Một.

Hệ thống phục vụ một phòng khám nha khoa. Luồng trình diễn đi từ đặt lịch, tiếp đón và hàng đợi đến khám, sơ đồ răng FDI, kê đơn và thanh toán. Kho vật tư và báo cáo doanh thu hỗ trợ vận hành phòng khám.

## Chạy demo

Cần Windows, PowerShell 7, .NET SDK 10, Node.js 22 trở lên và Docker Desktop chạy Linux containers. Mở PowerShell tại thư mục gốc của kho mã rồi chạy:

    pwsh -NoProfile -File demo/phase1/Prepare.ps1
    pwsh -NoProfile -File demo/phase1/Start.ps1

Mở [ứng dụng demo](http://127.0.0.1:5173) hoặc [Swagger](http://127.0.0.1:5000/swagger). Demo dùng PostgreSQL 16 riêng ở cổng 55432 và tạo dữ liệu tổng hợp. Dừng phiên demo bằng lệnh sau:

    pwsh -NoProfile -File demo/phase1/Stop.ps1

- [Hướng dẫn và kịch bản demo](demo/phase1/README.md)
- [Bản HTML để đọc ngoại tuyến](demo/phase1/index.html)
- [Danh mục 52 chức năng](demo/phase1/FUNCTIONS.csv) và [phiếu nghiệm thu](demo/phase1/UAT.md)

## Mã nguồn

- [Frontend](frontend/) dùng Vue 3, TypeScript, Vite, Pinia và Vue Router.
- [Backend](backend/) dùng ASP.NET Core 10, EF Core và PostgreSQL. API thực thi quyền của Admin, Lễ tân/Thu ngân, Nha sĩ, Phụ tá và Bệnh nhân.
- [Mẫu giao diện](design/figma-prototype/index.html) dùng để đối chiếu thiết kế. Mở tệp bằng trình duyệt sau khi clone.

## Kiểm thử

Chạy từ thư mục gốc của kho mã:

    dotnet test backend/DentalClinic.sln
    npm --prefix frontend ci
    node --test frontend/tests/*.test.mjs
    npm --prefix frontend run build

Dùng dữ liệu tổng hợp và cơ sở dữ liệu cô lập cho các bài kiểm thử tích hợp.
