# CẨM NĂNG & DỮ LIỆU DEMO 52 CHỨC NĂNG HỆ THỐNG NHA KHOA HOÀNG GIA
> **Căn cứ chuẩn:** Đề cương tốt nghiệp ĐH Thủ Dầu Một & `chuc_nang_Gia_demo_11.xlsx` (52 chức năng / 12 phân hệ / 5 vai trò).  
> **Mục tiêu:** Cung cấp tài khoản, chuỗi QR CCCD mẫu và kịch bản demo trơn tru, chuẩn xác từ đầu đến cuối cho buổi báo cáo / nghiệm thu đồ án.

---

## 1. THÔNG TIN KHỞI ĐỘNG VÀ TÀI KHOẢN ĐĂNG NHẬP 5 VAI TRÒ

### 1.1 Khởi động hệ thống
Mở PowerShell tại thư mục gốc `PK_NK_HG`:
```powershell
# Khởi động PostgreSQL 16
docker compose up -d postgres

# Khởi chạy toàn bộ hệ thống (Backend API :5000 + Frontend SPA :5173)
.\start-dev.ps1
```
- **Web App:** `http://localhost:5173`
- **Backend Swagger:** `http://localhost:5000/swagger`

---

### 1.2 Bảng tài khoản demo chuẩn trong CSDL

| Vai trò | Tên người dùng | Số điện thoại đăng nhập | Mật khẩu | Phạm vi thao tác chính |
|:---|:---|:---:|:---:|:---|
| **Quản trị viên** (`ADMIN`) | Quản trị viên | `0900000001` | `AdminPassword@123` | Toàn quyền danh mục, nhân sự, kho, báo cáo doanh thu, Audit Log |
| **Lễ tân / Thu ngân** (`RECEPTIONIST`) | Lễ tân Tiếp đón | `0900000002` | `DemoPassword@123` | Đặt lịch tại quầy, duyệt đặt lịch, tiếp đón QR CCCD, thu ngân, in biên lai |
| **Nha sĩ điều trị** (`DENTIST`) | Nha sĩ Demo (BS. CKI) | `0900000003` | `DemoPassword@123` | Gọi khám, sơ đồ răng FDI, chẩn đoán, xem ảnh X-quang, kê đơn, khóa hồ sơ |
| **Phụ tá nha khoa** (`ASSISTANT`) | Phụ tá Demo | `0900000004` | `DemoPassword@123` | Trạng thái ghế, đo sinh hiệu, lập phiếu nhập kho, xác nhận tiêu hao ca khám, giao thuốc |
| **Bệnh nhân** (`PATIENT`) | Nguyễn Văn An | `0900000005` | `DemoPassword@123` | Xem timeline điều trị, đơn thuốc, hóa đơn, đổi mật khẩu |

*(Ghi chú: Có thể dùng thêm Nha sĩ thứ hai: `0900000013` / `DemoPassword@123` hoặc Phụ tá thứ hai: `0900000014` / `DemoPassword@123`)*

---

## 2. BỘ DỮ LIỆU CHUỖI QR CCCD MẪU ĐỂ QUÉT NHANH

Chuỗi chuẩn 7 trường trên thẻ CCCD gắn chip Việt Nam (`Số_CCCD|Số_CMND_cũ|Họ_tên|Ngày_sinh_ddMMyyyy|Giới_tính|Địa_chỉ|Ngày_cấp_ddMMyyyy`).  
Có thể copy chuỗi này dán trực tiếp vào ô quét QR, ô tìm kiếm tiếp đón hoặc dùng trang tạo mã QR (như qrcode-monkey.com) để soi camera / súng quét barcode:

### Chuỗi 1: Bệnh nhân đã có hồ sơ và tiền sử bệnh lý (Nguyễn Văn An)
```text
079088012345||Nguyễn Văn An|12051988|Nam|45 Lê Lợi, TP. Thủ Dầu Một, Bình Dương|10052021
```
- **Số CCCD:** `079088012345`
- **Đặc điểm:** Đã có hồ sơ. Cảnh báo nguy kịch: Dị ứng Penicillin, Huyết áp cao (145/92 mmHg).
- **Thử nghiệm:** Quét tại màn hình Tiếp đón (`/queue/check-in`) -> Hệ thống tự băm SHA-256, tìm thấy hồ sơ và tự điền CCCD + SĐT để check-in 1 chạm.

### Chuỗi 2: Bệnh nhân đang khám dở dang (Trần Thị Bích)
```text
079195009876||Trần Thị Bích|24101995|Nữ|128 Cách Mạng Tháng Tám, Thuận An, Bình Dương|15082021
```
- **Số CCCD:** `079195009876`
- **Đặc điểm:** Dị ứng Aspirin. Đang có ca khám dở dang tại Ghế G01 (tổn thương răng 21).

### Chuỗi 3: Khách mới hoàn toàn (để demo tiếp nhận mới từ đầu)
```text
079092004567||Vũ Đình Trọng|15041992|Nam|TP. Thủ Dầu Một, Bình Dương|12062021
```
- **Số CCCD:** `079092004567`
- **Đặc điểm:** Chưa có trong CSDL. Quét tại menu Bệnh nhân (`/patients`) để tạo mới 1 chạm qua QR, tự động điền họ tên, ngày sinh, giới tính, địa chỉ, số CCCD.

---

## 3. KỊCH BẢN DEMO 4 LUỒNG LIÊN THÔNG (BAO PHỦ 52 CHỨC NĂNG)

---

### LUỒNG 1: ĐẶT LỊCH, DUYỆT LỊCH, TIẾP ĐÓN NHANH BẰNG QR CCCD & HÀNG ĐỢI
*Bao phủ: `F_APP_01`, `F_APP_02`, `F_AUTH_01`, `F_PAT_01`, `F_PAT_02`, `F_PAT_03`, `F_CHK_01`, `F_CHK_02`, `F_CHK_03`, `F_CHK_07`, `F_CHK_08`.*

1. **Đặt lịch online (`F_APP_01`):**
   - Vào `http://localhost:5173/dat-lich` (Không cần đăng nhập).
   - Chọn ngày hôm nay hoặc ngày mai, chọn khung giờ còn trống (ví dụ: 10:00).
   - Điền: Họ tên `Vũ Đình Trọng`, SĐT `0911223344`, Ngày sinh `1992-04-15`, Giới tính `Nam`.
   - Bấm "Xác nhận gửi yêu cầu đặt lịch" -> Nhận thông báo thành công và mã yêu cầu.
2. **Lễ tân duyệt lịch & Tiếp đón tại quầy (`F_APP_02`, `F_AUTH_01`):**
   - Đăng nhập Lễ tân: `0900000002` / `DemoPassword@123`.
   - Vào menu **Yêu cầu đặt lịch** (`/appointment-requests`): Bấm xem yêu cầu của "Vũ Đình Trọng" và bấm **Duyệt**.
   - Khách đến quầy: Vào menu **Bệnh nhân** (`/patients`) -> Bấm **Tiếp nhận bằng QR CCCD** -> Dán chuỗi QR số 3 hoặc quét camera -> Thông tin họ tên, ngày sinh, giới tính tự điền -> Nhập SĐT `0911223344` -> Bấm **Tạo hồ sơ** (`F_PAT_01`, `F_PAT_03`).
3. **Tiếp đón vào hàng đợi bằng QR CCCD (`F_CHK_01`):**
   - Vào menu **Tiếp đón & Check-in** (`/queue/check-in`).
   - Bấm nút **Quét QR CCCD** (hoặc dán chuỗi QR số 1 của BN Nguyễn Văn An vào ô tìm kiếm).
   - Hệ thống tự nhận diện mã QR, tìm thấy BN Nguyễn Văn An, hiển thị cảnh báo công nợ cũ (nếu có), tự điền 12 số CCCD và SĐT.
   - Bấm **Xác nhận khách đã đến** -> In vé số thứ tự (`ticket`), đưa vào hàng đợi hôm nay.
4. **Hàng đợi & Điều phối (`F_CHK_02`, `F_CHK_03`, `F_CHK_07`, `F_CHK_08`):**
   - Vào menu **Hàng đợi hôm nay** (`/queue/today`).
   - Hiển thị danh sách hàng đợi chung theo trạng thái `Đang chờ`.
   - Bấm **Gọi số** -> Bệnh nhân chuyển sang `Đang vào phòng`.
   - Bấm **Điều phối** -> Chọn chuyển sang chụp X-quang hoặc chuyển Nha sĩ điều trị (`F_CHK_08`).
   - Có thể mở màn hình TV phòng chờ `/queue/display` để xem hiển thị công khai mã số và tên che.

---

### LUỒNG 2: KHÁM LÂM SÀNG, SƠ ĐỒ RĂNG FDI, CẢNH BÁO AN TOÀN & HÌNH ẢNH
*Bao phủ: `F_CHK_04`, `F_PAT_04`, `F_PAT_05`, `F_PAT_06`, `F_PAT_07`, `F_PAT_08`, `F_PAT_09`, `F_FDI_01` - `07`, `F_IMG_02`, `F_IMG_04`.*

1. **Bắt đầu ca khám & Cảnh báo an toàn điều trị (`F_CHK_04`, `F_PAT_04`, `F_PAT_05`, `F_PAT_06`):**
   - Đăng nhập Nha sĩ: `0900000003` / `DemoPassword@123`.
   - Vào danh sách hàng đợi hoặc hồ sơ bệnh nhân Nguyễn Văn An (`/patients/1` hoặc từ hàng đợi).
   - Bấm **Bắt đầu khám** (`F_CHK_04`) -> Trạng thái ca khám chuyển `InProgress`.
   - **Điểm nhấn an toàn lâm sàng:** Banner màu đỏ tự động cảnh báo:
     - ⚠️ **Dị ứng Penicillin (Mức độ Nguy kịch)**
     - ⚠️ **Huyết áp cao: 145/92 mmHg (Đo ngày hôm nay)** -> Cảnh báo trước khi tiêm tê / làm thủ thuật.
2. **Sơ đồ răng điện tử tương tác FDI (`F_FDI_01` - `F_FDI_07`):**
   - Vào tab **Sơ đồ răng FDI** trên hồ sơ khám:
     - Hiển thị đầy đủ 32 răng vĩnh viễn (hoặc toggle 20 răng trẻ em nếu bệnh nhân dưới 12 tuổi - `F_FDI_01`).
     - Click chọn răng **36** (Hàm dưới bên trái - `F_FDI_02`).
     - Chọn mặt răng (Mặt nhai Occlusal).
     - Gán tình trạng bệnh lý: Sâu ngà sâu (`K02.1` - `F_FDI_03`).
     - Chỉ định dịch vụ theo răng: Trám răng thẩm mỹ Composite (`DV001` - `F_FDI_04`).
     - Ghi chú lâm sàng cho răng 36: "Xoang sâu sát tủy, đã đặt canxi che tủy" (`F_FDI_07`).
     - Bấm **Lưu cập nhật răng** (`F_FDI_05`) -> Hệ thống tạo bản ghi trạng thái mới, giữ nguyên lịch sử cũ (`F_FDI_06`).
3. **Hình ảnh X-quang & Trong miệng (`F_IMG_02`, `F_IMG_04`):**
   - Vào tab **Hình ảnh hồ sơ**:
     - Bấm tải lên ảnh chụp cận chóp răng 36 (`F_IMG_02`).
     - Bấm vào ảnh để mở popup xem chi tiết, phóng to, thu nhỏ đối chiếu (`F_IMG_04`).
4. **Timeline & Khóa hồ sơ (`F_PAT_07`, `F_PAT_08`):**
   - Vào tab **Timeline điều trị**: xem chuỗi sự kiện lịch sử khám, đơn thuốc, chẩn đoán theo dòng thời gian (`F_PAT_07`).
   - Bấm **Khóa/Chốt hồ sơ ca khám**: hoàn tất khám lâm sàng, bảo vệ dữ liệu không bị sửa tùy tiện (`F_PAT_08`).

---

### LUỒNG 3: KÊ ĐƠN ĐIỆN TỬ, QUẢN LÝ GHẾ KHÁM & XÁC NHẬN TIÊU HAO KHO
*Bao phủ: `F_RX_01`, `F_RX_02`, `F_RX_04`, `F_RX_05`, `F_ASS_06`, `F_INV_03`, `F_INV_06`.*

1. **Kê đơn thuốc điện tử & Kiểm tra tồn (`F_RX_01`, `F_RX_02`):**
   - Vẫn ở phiên Nha sĩ `0900000003`, vào tab **Đơn thuốc** của ca khám.
   - Thêm thuốc:
     - `MED011` - Paracetamol 500mg: Số lượng 10 viên, Ngày uống 2 lần, mỗi lần 1 viên sau ăn.
     - `MED020` - Nước súc miệng Chlorhexidine 0.12%: Số lượng 1 chai, Súc miệng sáng tối.
   - Hệ thống hiển thị số lượng tồn kho khả dụng hiện hành bên cạnh (`F_RX_02`).
   - Ký đơn thuốc điện tử và bấm **Lưu đơn thuốc** (`F_RX_01`, `F_RX_05`).
2. **In đơn thuốc kèm QR tra cứu bảo mật (`F_RX_04`):**
   - Bấm nút **In đơn thuốc**: Xem bản in chuẩn A4/A5 có chữ ký bác sĩ và mã QR tra cứu đơn thuốc.
   - Quét mã QR trên đơn: Dẫn đến trang công khai `/public/prescriptions/...`, yêu cầu nhập đúng số CCCD của bệnh nhân mới hiển thị nội dung đơn (bảo mật dữ liệu y tế theo DL-292).
3. **Nghiệp vụ Phụ tá: Trạng thái ghế & Tiêu hao kho (`F_ASS_06`, `F_INV_06`):**
   - Đăng nhập Phụ tá: `0900000004` / `DemoPassword@123`.
   - Vào menu **Trạng thái ghế/phòng điều trị** (`/chairs`):
     - Xem trạng thái Ghế G01: Đang có ca khám.
     - Cập nhật trạng thái ghế: `Sẵn sàng` / `Đang khử trùng` / `Bảo trì` (`F_ASS_06`).
   - Vào menu **Xuất tiêu hao ca khám** (`/inventory/stock-issues`):
     - Chọn ca khám vừa thực hiện của BN Nguyễn Văn An.
     - Nhập số lượng vật tư tiêu hao thực tế: 1 Găng tay y tế, 1 Ống hút nước bọt, 1 Tuýp Composite (`F_INV_06`).
     - Bấm **Xác nhận xuất kho**: Hệ thống chính thức trừ tồn kho và ghi Audit Log.
4. **Nhập kho & Cảnh báo tồn tối thiểu (`F_INV_03`):**
   - Vào menu **Phiếu nhập kho** (`/inventory/stock-receipts`):
     - Xem danh sách vật tư cảnh báo màu vàng: Mã `VT003` (Bông gòn) tồn 3 cuộn < ngưỡng tối thiểu 5 cuộn.
     - Bấm **Tạo phiếu nhập kho**: Nhập thêm 20 cuộn bông gòn, đơn giá 15.000đ.
     - Bấm **Xác nhận nhập kho**: Tồn kho lập tức cộng thêm 20, cảnh báo màu vàng tự động biến mất (`F_INV_03`).

---

### LUỒNG 4: HÓA ĐƠN NHÁP, THANH TOÁN NHIỀU ĐỢT, BÁO CÁO DOANH THU & ADMIN
*Bao phủ: `F_BIL_01`, `F_BIL_02`, `F_BIL_03`, `F_BIL_06`, `F_BIL_07`, `F_RPT_01`, `F_MST_01` - `07`, `F_AUTH_02` - `06`.*

1. **Sinh hóa đơn nháp & Giảm giá (`F_BIL_01`, `F_BIL_02`, `F_BIL_07`):**
   - Đăng nhập Lễ tân / Thu ngân: `0900000002` / `DemoPassword@123`.
   - Vào menu **Hóa đơn & Thu ngân** (`/invoices`).
   - Chọn ca khám vừa kết thúc của BN Nguyễn Văn An:
     - Hệ thống tự gom các dịch vụ đã chỉ định: Trám răng Composite (350.000đ), Cạo vôi răng (200.000đ), Đơn thuốc (85.000đ) -> Tổng: 635.000đ (`F_BIL_01`).
     - Chọn mẫu giảm giá: `FIX50` (Giảm trực tiếp 50.000đ tri ân khách hàng thân thiết) -> Còn lại: 585.000đ (`F_BIL_02`).
2. **Thu tiền nhiều đợt / Trả góp tiến độ & In biên lai (`F_BIL_03`, `F_BIL_06`):**
   - Thu đợt 1: Khách trả trước 300.000đ Tiền mặt.
     - Chọn hình thức: **Tiền mặt** (`Cash`), nhập số tiền: `300000`. Bấm **Xác nhận thu tiền**.
     - Hệ thống ghi nhận: Đã thu 300.000đ, Còn nợ 285.000đ.
     - Bấm **In biên lai**: Mở xem mẫu in K80 chuẩn nhiệt (`F_BIL_06`).
   - Thu đợt 2 (hoàn tất): Khách chuyển khoản phần còn lại 285.000đ.
     - Chọn hình thức: **Chuyển khoản** (`BankTransfer`), nhập số tiền `285000` và mã tham chiếu giao dịch.
     - Bấm **Xác nhận chuyển khoản thủ công** -> Trạng thái hóa đơn chuyển sang `Đã thanh toán đủ` (`Paid`).
3. **Báo cáo doanh thu tài chính (`F_RPT_01`):**
   - Đăng nhập Quản trị viên: `0900000001` / `AdminPassword@123`.
   - Vào menu **Báo cáo doanh thu** (`/reports/revenue`):
     - Xem doanh thu theo ngày, tháng, phương thức thanh toán (Tiền mặt / Chuyển khoản).
     - Biểu đồ và bảng thống kê chỉ tính các giao dịch đã thực thu (`Paid`).
4. **Quản trị danh mục hệ thống & Nhật ký Audit Log (`F_MST_01` - `F_MST_07`, `F_AUTH_05`, `F_AUTH_06`):**
   - Menu **Danh mục**:
     - Bảng giá dịch vụ (`/catalog/services` - `F_MST_01`).
     - Bệnh lý chuẩn ICD-10 nha khoa (`/catalog/diagnoses` - `F_MST_02`).
     - Thuốc & Vật tư y tế (`/catalog/medicines`, `/catalog/supplies` - `F_MST_03`, `F_MST_04`).
     - Phòng khám & Ghế điều trị (`/catalog/chairs` - `F_MST_05`).
     - Người dùng & Nhân sự (`/staff` - `F_MST_07`, `F_AUTH_05`).
     - Cấu hình hệ thống & Lịch làm việc (`/settings` - `F_MST_06`).
   - Menu **Nhật ký thao tác** (`/audit-logs` - `F_AUTH_06`):
     - Chỉ Quản trị viên có quyền xem.
     - Ghi nhận chi tiết từng thao tác: thời gian, người thực hiện, hành động (thu tiền, sửa bảng giá, nhập kho, duyệt đặt lịch), đảm bảo an toàn và minh bạch dữ liệu.

---

## 4. BẢNG TRA CỨU ĐẦY ĐỦ 52 MÃ CHỨC NĂNG (DEMO_11 CHEAT SHEET)

| Mã chức năng | Tên chức năng | URL Frontend | Vai trò thao tác chính |
|:---|:---|:---|:---|
| **F_AUTH_01** | Đăng ký tài khoản bệnh nhân (tại bước check-in) | `/queue/check-in`, `/patients` | Lễ tân, Bệnh nhân |
| **F_AUTH_02** | Đăng nhập & RBAC | `/login` | Tất cả 5 vai trò |
| **F_AUTH_03** | Hồ sơ cá nhân & đổi mật khẩu | `/profile` | Tất cả 5 vai trò |
| **F_AUTH_04** | Khôi phục mật khẩu (OTP) | `/password-recovery` | Khách / Người dùng quên pass |
| **F_AUTH_05** | Quản lý vai trò / quyền | `/role-permissions`, `/staff` | Quản trị viên |
| **F_AUTH_06** | Nhật ký thao tác (Audit Log) | `/audit-logs` | Quản trị viên |
| **F_PAT_01** | Tạo hồ sơ (gộp đối soát CCCD) | `/patients` | Lễ tân, Quản trị viên |
| **F_PAT_02** | Tìm kiếm nhanh hồ sơ | `/patients`, `/queue/check-in` | Lễ tân, Nha sĩ, Phụ tá |
| **F_PAT_03** | Kiểm tra trùng hồ sơ (CCCD/SĐT) | `/patients` | Lễ tân, Quản trị viên |
| **F_PAT_04** | Tiền sử bệnh lý & Dị ứng | `/patients/:id` (tab Tiền sử) | Nha sĩ, Phụ tá |
| **F_PAT_05** | Đo và ghi nhận sinh hiệu | `/patients/:id` (tab Sinh hiệu) | Phụ tá, Nha sĩ |
| **F_PAT_06** | Cảnh báo an toàn điều trị | `/patients/:id`, `/clinical/visits/:id` | Nha sĩ, Phụ tá |
| **F_PAT_07** | Timeline điều trị | `/patients/:id` (tab Timeline) | Nha sĩ, Lễ tân, Bệnh nhân |
| **F_PAT_08** | Khóa / Chốt hồ sơ điều trị | `/clinical/visits/:id` | Nha sĩ, Quản trị viên |
| **F_PAT_09** | Tạo bản ghi khám (Visit) mới | `/queue/today`, `/patients/:id` | Nha sĩ, Lễ tân |
| **F_APP_01** | Đặt lịch trực tuyến (Public) | `/dat-lich` | Khách vãng lai, Bệnh nhân |
| **F_APP_02** | Đặt lịch tại quầy (Lễ tân đặt hộ) | `/counter-booking`, `/appointment-requests` | Lễ tân |
| **F_CHK_01** | Xác nhận khách đến (nhắc công nợ) | `/queue/check-in` | Lễ tân |
| **F_CHK_02** | Hàng đợi chung & TV phòng chờ | `/queue/today`, `/queue/display` | Lễ tân, Phụ tá, Công khai TV |
| **F_CHK_03** | Gọi số & dẫn bệnh nhân vào ghế | `/queue/today` | Lễ tân, Phụ tá |
| **F_CHK_04** | Bắt đầu / Kết thúc ca khám | `/queue/today`, `/clinical/visits/:id` | Nha sĩ |
| **F_CHK_07** | Trạng thái tiến trình hàng đợi | `/queue/today` | Lễ tân, Phụ tá, Nha sĩ |
| **F_CHK_08** | Điều phối Nha sĩ / Chụp X-quang | `/queue/today` | Lễ tân, Phụ tá, Nha sĩ |
| **F_FDI_01** | Hiển thị sơ đồ 32/20 răng FDI | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |
| **F_FDI_02** | Chọn răng / Mặt răng | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |
| **F_FDI_03** | Gán tình trạng bệnh lý theo răng | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |

| **F_FDI_04** | Chỉ định dịch vụ điều trị theo răng | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |
| **F_FDI_05** | Cập nhật sau điều trị | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |
| **F_FDI_06** | Lịch sử trạng thái răng | `/clinical/visits/:id` (FDI History) | Nha sĩ |
| **F_FDI_07** | Ghi chú lâm sàng theo răng | `/clinical/visits/:id` (FDI Chart) | Nha sĩ |
| **F_IMG_02** | Tải ảnh X-quang / Chụp trong miệng | `/clinical/visits/:id` (tab Hình ảnh) | Phụ tá, Nha sĩ |
| **F_IMG_04** | Xem / Thu phóng ảnh X-quang | `/clinical/visits/:id` (tab Hình ảnh) | Nha sĩ, Phụ tá |
| **F_RX_01** | Kê đơn thuốc điện tử | `/clinical/visits/:id` (tab Đơn thuốc) | Nha sĩ |
| **F_RX_02** | Kiểm tra tồn thuốc khả dụng | `/clinical/visits/:id` (tab Đơn thuốc) | Nha sĩ |
| **F_RX_04** | In / Xuất đơn thuốc kèm mã QR | `/clinical/prescriptions/:id/print` | Nha sĩ, Lễ tân |
| **F_RX_05** | Lịch sử đơn thuốc | `/patients/:id` (tab Đơn thuốc) | Nha sĩ, Bệnh nhân |
| **F_BIL_01** | Sinh hóa đơn nháp từ ca khám | `/invoices` | Lễ tân / Thu ngân |
| **F_BIL_02** | Áp dụng mã giảm giá / khuyến mãi | `/invoices/:id` | Lễ tân / Thu ngân |
| **F_BIL_03** | Thu tiền & Theo dõi nhiều đợt | `/invoices/:id` | Lễ tân / Thu ngân |
| **F_BIL_06** | In / Xuất biên lai nhiệt K80 | `/invoices/:id/receipt` | Lễ tân / Thu ngân |
| **F_BIL_07** | Hủy / Điều chỉnh hóa đơn nháp | `/invoices/:id` | Lễ tân, Quản trị viên |
| **F_INV_03** | Phiếu nhập kho & Cảnh báo tồn | `/inventory/stock-receipts` | Phụ tá, Quản trị viên |
| **F_INV_06** | Ghi nhận & xác nhận trừ kho ca khám | `/inventory/stock-issues` | Phụ tá, Quản trị viên |
| **F_ASS_06** | Trạng thái ghế / phòng điều trị | `/chairs` | Phụ tá, Quản trị viên |
| **F_MST_01** | Bảng giá dịch vụ điều trị | `/catalog/services` | Quản trị viên |
| **F_MST_02** | Danh mục bệnh lý / triệu chứng | `/catalog/diagnoses` | Quản trị viên |
| **F_MST_03** | Danh mục thuốc | `/catalog/medicines` | Quản trị viên |
| **F_MST_04** | Vật tư y tế & Ngưỡng tồn | `/catalog/supplies` | Quản trị viên |
| **F_MST_05** | Danh mục phòng & ghế điều trị | `/catalog/chairs` | Quản trị viên |
| **F_MST_06** | Cấu hình hệ thống & Lịch khám | `/settings` | Quản trị viên |
| **F_MST_07** | Quản lý người dùng nhân viên | `/staff` | Quản trị viên |
| **F_RPT_01** | Báo cáo doanh thu tài chính | `/reports/revenue` | Quản trị viên |

