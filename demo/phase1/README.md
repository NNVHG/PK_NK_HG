# Demo Phase 1 — Phòng khám nha khoa Hoàng Gia

**Cập nhật 24/10/2026 · Nhánh `codex/individual-accounts-demo-prep` · Commit V4–V6 `15f6c2b` · Chờ Gia UAT.**

## 1. Bắt đầu

Yêu cầu: Windows, PowerShell 7 (`pwsh`), .NET SDK 10, Node.js 22 trở lên, Docker Desktop đang chạy Linux containers. Lần chuẩn bị đầu cần mạng để tải các dependency đã khóa và PostgreSQL 16. Không thêm package.

Tại thư mục gốc của **worktree này**, chạy:

```powershell
pwsh -NoProfile -File demo/phase1/Prepare.ps1
pwsh -NoProfile -File demo/phase1/Start.ps1
```

Nếu PowerShell chặn script, mở từng file để xem nội dung trước; chạy trong phiên được máy cho phép thực thi script. Script không đổi chính sách máy.

| Thành phần | Địa chỉ / vị trí |
|---|---|
| Website | http://127.0.0.1:5173 |
| Swagger | http://127.0.0.1:5000/swagger |
| PostgreSQL demo | `127.0.0.1:55432`, database `dental_phase1_demo` |
| Nhật ký chạy | `demo/phase1/.runtime/api.log`, `api-error.log`, `web.log`, `web-error.log` |
| Cấu hình riêng | `.runtime/settings.json`, tự sinh và không đưa vào Git |

Start kiểm tra cổng, khởi động database, chạy migration/seed (cờ `Seed:Phase1=true`) và chờ API + website phản hồi. Nếu cổng 5000 hoặc 5173 đang bận, script dừng để Gia đóng ứng dụng đang dùng cổng đó. Cửa sổ tiến trình chạy ẩn.

Dừng bằng `pwsh -NoProfile -File demo/phase1/Stop.ps1`. Chạy lại Start để tiếp tục với dữ liệu đã lưu. Stop chỉ dừng tiến trình được script ghi nhận và container demo; không xóa volume. Giữ thư mục `.runtime` để dùng lại mật khẩu database đã sinh. Không chạy `docker compose down -v` trong buổi demo.

## 2. Tài khoản và dữ liệu mẫu

Đây là thông tin **chỉ dành cho database demo tổng hợp**. Không dùng dữ liệu bệnh nhân thật.

| Vai trò | Định danh đăng nhập | Mật khẩu ban đầu | Ghi chú |
|---|---|---|---|
| Admin | `0900000001` | `AdminPassword@123` | Danh mục, báo cáo, audit |
| Lễ tân/Thu ngân | `0900000002` | `DemoPassword@123` | Đặt lịch, check-in, thu tiền |
| Nha sĩ | `0900000003` | `DemoPassword@123` | Chọn ghế, khám, FDI, kê đơn |
| Phụ tá | `0900000004` | `DemoPassword@123` | Theo nha sĩ, vật tư, ảnh |
| Sáu Patient Demo | CCCD giả `000000000901`–`000000000906` | `0900000091` | Sáu tài khoản/hồ sơ riêng; dùng chung SĐT liên hệ |

Seed tạo sáu tài khoản Patient cá nhân riêng với CCCD tổng hợp `000000000901`–`000000000906`; tất cả dùng chung SĐT liên hệ `0900000091`. Database chỉ lưu hash/giá trị che CCCD. Mật khẩu khởi tạo là SĐT; OTP kích hoạt Development hiển thị trên giao diện và lần đầu phải đổi mật khẩu trước khi vào portal. Không có dữ liệu bệnh nhân thật.

Seed còn tạo ghế `DEMO01`, dịch vụ giá minh họa **100.000đ**, vật tư ngưỡng 5; lịch hôm nay ở bốn trạng thái (Scheduled, CheckedIn, Cancelled, Completed), một Visit đang khám và ba lượt hàng đợi (hai Waiting, một InConsultation). Hồ sơ tổng hợp có tiền sử/dị ứng, sinh hiệu và tình trạng sâu răng FDI ở răng 16. Không seed hóa đơn/doanh thu hoặc tồn kho. Danh mục bệnh lý và 30 tên thuốc giữ seed hiện hành; thuốc bắt đầu ngừng hoạt động, giá 0. Admin chọn sản phẩm/giá demo và kích hoạt trước khi nha sĩ kê. Không cung cấp sẵn liều thuốc hoặc phác đồ chưa được nha sĩ duyệt.

## 3. Quy tắc trình diễn

- Trang chủ mở công khai. Gửi yêu cầu đặt lịch không cần đăng nhập hoặc OTP.
- Mỗi tài khoản Patient chỉ sở hữu một hồ sơ. Cùng SĐT không tự liên kết hoặc cấp quyền xem hồ sơ khác.
- Duyệt yêu cầu tạo lịch/hồ sơ phù hợp; tài khoản mới được cấp khi check-in đã đối soát CCCD và SĐT tại quầy. Không tự check-in khi đặt lịch.
- Patient mới dùng CCCD để đăng nhập, mật khẩu ban đầu là SĐT; OTP lần đầu và đổi mật khẩu bắt buộc trước khi vào portal. Tài khoản cá nhân cũ giữ chính sách đăng nhập đã chốt.
- Ca nặng có lưu ý dùng ghi chú/tiền sử; không có chức năng cấp cứu hoặc chen lịch. TV giữ phạm vi đã chốt.
- Thu tiền mặt hoặc ghi nhận chuyển khoản thủ công sau đối soát. Mọi tiền trong database demo là số liệu minh họa; không chuyển tiền thật.
- Hoàn tất Phase 1 bằng phiếu UAT của Gia. Bộ file này không phải bằng chứng đã nghiệm thu; chưa mở Phase 2.

## 4. Chuẩn bị trước khi trình bày

1. Mở các phiên trình duyệt riêng cho Lễ tân, Nha sĩ, Phụ tá và Patient; tránh ghi đè tài khoản giữa các tab.
2. Admin xem dịch vụ/giá và ghế ở `/admin/services`, `/admin/chairs`. Nha sĩ vào `/staff/workspace` chọn ghế DEMO01; Phụ tá chọn nha sĩ làm cùng.
3. Seed Phase 1 chạy tự động khi Start với cờ `Seed:Phase1=true`. Dùng lịch `Scheduled` còn hiệu lực để trình diễn check-in; màn tiếp đón tự nạp lịch hôm nay/hàng chờ và hỗ trợ tìm nhanh, phân trang. Ghi chú tiếp đón được để trống hoặc tối đa 500 ký tự. Các lượt Waiting hiện có để trình diễn hàng đợi/TV; bấm gọi số để thử âm thanh trên máy TV.
4. Nếu demo kê/giao thuốc: Admin chuẩn bị thuốc và nhập số lượng thực trong `/admin/medicines`, `/admin/medicine-stock`. Liều/cách dùng do người demo nhập từ nội dung đã được nha sĩ duyệt.
5. Nếu demo tiêu hao: Phụ tá/Admin tạo phiếu nhập 10 cái vật tư DEMO01 tại `/inventory/receipts`, xác nhận; sau điều trị sẽ xác nhận tiêu hao 2 cái tại `/inventory/issues`.
6. Dùng `sample-upload.png` (ô xám tổng hợp, không phải ảnh y khoa) để demo thao tác tải/xem/thu phóng. Không dùng ảnh bệnh nhân thật. Tài liệu không yêu cầu tự suy diễn nội dung ảnh.
7. Chọn ngày/giờ còn nhận lịch theo giờ Việt Nam; lịch cho buổi demo phải còn trong giờ làm việc và trước cutoff 23:30. Khi ca đã hoàn tất, dùng lịch/hồ sơ mới cho lần diễn tiếp, không sửa dữ liệu đã thu.

## 5. Kịch bản xuyên suốt (khoảng 20–30 phút)

| Bước | Vai trò / màn hình | Thao tác | Kết quả cần quan sát |
|---|---|---|---|
| 1 | Khách · `/` → `/book-appointment` | Nhập thông tin giả, chọn giờ còn chỗ và gửi | Có xác nhận tiếp nhận; không màn hình OTP; yêu cầu Pending giữ chỗ |
| 2 | Lễ tân · `/appointments/requests` | Đối soát và duyệt đúng hồ sơ hoặc xác nhận hồ sơ mới | Có lịch Scheduled; chưa tự cấp số hàng đợi |
| 3 | Lễ tân · Bệnh nhân / `/queue/check-in` | Đối chiếu CCCD/SĐT, cập nhật CCCD khi cần, chọn lịch và xác nhận khách đến | Visit mới và số hàng đợi; thông tin cấp tài khoản mới nếu đủ điều kiện |
| 4 | Patient · `/login` | Dùng CCCD/SĐT ban đầu; nhận OTP Development, nhập mã và đặt mật khẩu mới | Chỉ vào portal sau bước đổi mật khẩu; `/my-record` chỉ có hồ sơ của mình |
| 5 | Lễ tân/Phụ tá · `/queue/today` | Gọi số và dẫn bệnh nhân tới nha sĩ | Trạng thái, nha sĩ và vị trí được cập nhật |
| 6 | Nha sĩ · hàng đợi → hồ sơ | Bắt đầu khám, ghi tiền sử/sinh hiệu và chẩn đoán | Ghế chiếm dụng; cảnh báo an toàn theo dữ liệu đã nhập |
| 7 | Nha sĩ · tab FDI | Chọn răng hợp lệ, lưu tình trạng, chỉ định dịch vụ DEMO01 | Lưu bản ghi lịch sử và đơn giá snapshot 100.000đ |
| 8 | Nha sĩ/Phụ tá · ảnh/đơn thuốc | Tải ảnh minh họa; kê đơn bằng nội dung được duyệt; xem bản in | Ảnh gắn đúng Visit; kê chỉ cảnh báo tồn, chưa trừ tồn |
| 9 | Nha sĩ → Lễ tân | Kết thúc khám, mở hóa đơn nháp, thu tiền | Hóa đơn lấy dịch vụ/thuốc hợp lệ; giữ chứng từ và lịch sử |
| 10 | Lễ tân · hóa đơn | Với ca chỉ có DEMO01: thu 40.000đ, rồi 60.000đ | Sau đợt 1 nợ 60.000đ; sau đợt 2 đã thu đủ 100.000đ; in biên lai |
| 11 | Người được giao quyền | Nếu có thuốc, xác nhận giao sau thanh toán đủ | Thiếu tồn bị chặn; đủ tồn thì bàn giao/trừ tồn/audit cùng giao dịch |
| 12 | Phụ tá/Admin | Xác nhận tiêu hao 2 cái ở Visit đã điều trị | Tồn DEMO01 từ 10 còn 8; có người xác nhận |
| 13 | Admin · `/admin/reports/revenue` | Chọn kỳ chứa giao dịch vừa thu, đối chiếu audit | Thực thu đúng các đợt; công nợ hiện tại hiển thị riêng kỳ lọc |

Kịch bản 100.000đ chỉ áp dụng khi hóa đơn có đúng một dịch vụ DEMO01, không thuốc, không chiết khấu. Nếu có thuốc hoặc giảm giá, dùng số tiền hiển thị trên hóa đơn; không áp con số minh họa cố định.

## 6. Kiểm tra tài khoản cá nhân

Đăng nhập Patient Một và Patient Hai lần lượt qua hai phiên riêng. Kiểm tra tên/mã hồ sơ khác nhau dù cùng SĐT. Mở liên kết hồ sơ của tài khoản kia phải bị từ chối/404, không hiện bệnh án. Gửi lịch cho mình không có ô chọn người khác. Đổi số liên hệ chỉ hiển thị một hồ sơ để xác nhận. Khôi phục Patient cần CCCD + SĐT khớp chính tài khoản đó; gửi SMS khôi phục thật cần cấu hình nhà cung cấp, không thuộc mô phỏng OTP kích hoạt này.

## 7. Nâng cấp database cũ

Bộ demo dùng database riêng. Chưa chạy chuyển đổi trên database phòng khám. Migration lịch sử giữ nguyên; hai migration mới đổi tên cờ chuyển đổi và đặt unique index `Patients.UserId`. `Patients.Phone` không unique; tài khoản Patient đăng nhập CCCD được dùng chung SĐT.

Trước unique index, startup chuyển toàn bộ liên kết cũ trong một transaction theo Q-363b: mỗi hồ sơ có CCCD hash hợp lệ/duy nhất và SĐT hợp lệ nhận tài khoản riêng; tài khoản nguồn bị vô hiệu hóa/thu hồi phiên. Thiếu hoặc mơ hồ định danh ở bất kỳ hồ sơ nào thì dừng toàn bộ. Trùng SĐT giữa các hồ sơ **không phải lỗi**. Chuyển lại sau thành công không tạo thêm tài khoản. Giữ PatientId và toàn bộ lịch sử điều trị/tài chính. Khi áp dụng vào dữ liệu thật cần bản sao lưu và rà preflight; không lấy volume demo thay database hiện hữu.

## 8. Hồ sơ bàn giao

- `index.html`: bản HTML đọc ngoại tuyến, có mục lục và checklist chuẩn bị.
- `UAT.md`: phiếu ghi kết quả/ảnh bằng chứng và quyết định nghiệm thu.
- `FUNCTIONS.csv`: 52 chức năng chính thức để Gia đánh dấu từng mục, chưa gán PASS.
- `BUILD_REPORT.md`: phạm vi đã kiểm tra thực tế và giới hạn.
- `Prepare.ps1`, `Start.ps1`, `Stop.ps1`, `compose.yml`: chạy bộ demo độc lập.

Lỗi thường gặp: Docker chưa mở → mở Docker Desktop; cổng bận → dừng phiên cũ đúng cách; API lỗi migration → xem log và giữ nguyên database, không tự xóa volume; tài khoản mẫu đã đổi mật khẩu → dùng mật khẩu đã đổi; thiếu thuốc/tồn → chuẩn bị danh mục và phiếu nhập; lịch đã quá giờ → đặt lịch còn hiệu lực. Ghi lỗi vào UAT, không đánh PASS khi chưa quan sát được luồng.
