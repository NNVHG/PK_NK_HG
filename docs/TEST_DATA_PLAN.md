# TEST DATA PLAN — KẾ HOẠCH DỮ LIỆU KIỂM THỬ CHUẨN HÓA

> Dự án: Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)
> Mục đích: Cung cấp tập dữ liệu mẫu chuẩn hóa, dữ liệu biên, dữ liệu dị thường phục vụ kiểm thử đơn vị, kiểm thử tích hợp, kiểm thử bảo mật và UAT.

---

## 1. Dữ liệu Người dùng & Xác thực (MOD_AUTH / MOD_MST)

| Mã định danh | Họ và tên | Số điện thoại | Vai trò (RoleCode) | Mật khẩu mẫu | Mục đích kiểm thử |
|---|---|---|---|---|---|
| `USR_ADMIN_01` | Nguyễn Quản Trị | `0900000001` | `ADMIN` | `AdminPassword@123` | Happy path quản trị, toàn quyền cấu hình |
| `USR_RECEP_01` | Trần Thị Lễ Tân | `0900000002` | `RECEPTIONIST` | `DemoPassword@123` | Tiếp đón, đặt lịch, check-in, thu ngân |
| `USR_DENT_01` | Bác sĩ Lê Nha Khoa | `0900000003` | `DENTIST` | `DemoPassword@123` | Khám lâm sàng, FDI, kê đơn thuốc |
| `USR_ASSIST_01` | Phạm Phụ Tá | `0900000004` | `ASSISTANT` | `DemoPassword@123` | Hỗ trợ điều trị, chụp ảnh, xác nhận kho |
| `USR_PATIENT_01` | Hoàng Bệnh Nhân | `0900000005` | `PATIENT` | `DemoPassword@123` | Bệnh nhân xem hồ sơ cá nhân |
| `USR_PATIENT_02` | Vũ Bệnh Nhân B | `0900000006` | `PATIENT` | `DemoPassword@123` | Kiểm thử Data Ownership / IDOR |
| `USR_LOCKED_01` | Đặng Tài Khoản Khóa | `0900000009` | `PATIENT` | `DemoPassword@123` | Kiểm thử chặn đăng nhập khi IsActive = false |

---

## 2. Dữ liệu Hồ sơ Bệnh nhân (MOD_PAT)

| Mã hồ sơ | Họ tên bệnh nhân | Số điện thoại | Giới tính | Ngày sinh | Tiền sử bệnh lý / Dị ứng | Ghi chú kiểm thử |
|---|---|---|---|---|---|---|
| `PAT_001` | Nguyễn Văn An | `0911223344` | Nam | `1990-05-15` | Dị ứng Penicillin, Huyết áp cao | Cảnh báo an toàn lâm sàng (F_PAT_06) |
| `PAT_002` | Trần Thị Bình | `0922334455` | Nữ | `2018-09-20` | Không có tiền sử | Bệnh nhân nhi (kiểm thử bộ răng sữa FDI 51-85) |
| `PAT_003` | Lê Hoàng Long Đỗ Trọng | `0933445566` | Nam | `1945-01-01` | Đái tháo đường Type 2, Đặt stent tim | Bệnh nhân cao tuổi, tên dài tối đa |
| `PAT_ERR_DUP` | Trùng SĐT | `0911223344` | Nữ | `2000-01-01` | Bình thường | Kiểm thử chặn trùng số điện thoại (F_PAT_03) |
| `PAT_ERR_FUT` | Ngày sinh tương lai | `0988776655` | Nam | `2030-01-01` | Không | Kiểm thử chặn ngày sinh không hợp lệ (Validation) |

---

## 3. Dữ liệu Chuẩn Nha Khoa Quốc Tế FDI (MOD_FDI)

### A. Bộ răng người lớn (Permanent Teeth - 32 răng):
- Hàm trên: Cung 1 (18, 17, 16, 15, 14, 13, 12, 11) & Cung 2 (21, 22, 23, 24, 25, 26, 27, 28)
- Hàm dưới: Cung 3 (38, 37, 36, 35, 34, 33, 32, 31) & Cung 4 (41, 42, 43, 44, 45, 46, 47, 48)

### B. Bộ răng trẻ em (Deciduous Teeth - 20 răng):
- Hàm trên: Cung 5 (55, 54, 53, 52, 51) & Cung 6 (61, 62, 63, 64, 65)
- Hàm dưới: Cung 7 (75, 74, 73, 72, 71) & Cung 8 (81, 82, 83, 84, 85)

### C. Ký hiệu mặt răng tiêu chuẩn:
- `M` (Mesial - Mặt gần)
- `D` (Distal - Mặt xa)
- `O` (Occlusal - Mặt nhai) / `I` (Incisal - Rìa cắn)
- `B` (Buccal - Mặt ngoài/má)
- `L` (Lingual - Mặt trong/lưỡi)

### D. Dữ liệu kiểm thử bệnh lý răng (Pathologies / ICD-10 Nha khoa):
- `K02.1`: Sâu ngà răng (Caries of dentine) - Vị trí: Răng 46 mặt O, M
- `K04.0`: Viêm tủy răng (Pulpitis) - Vị trí: Răng 21
- `K05.3`: Viêm quanh răng mạn (Chronic periodontitis) - Vị trí: Toàn hàm
- `ERR_TOOTH_99`: Mã răng không tồn tại `99` -> Phải nhận lỗi Validation `400 Bad Request`

---

## 4. Dữ liệu Quản lý Kho & Định mức Vật tư (MOD_INV / MOD_MST)

| Mã VT | Tên vật tư / Thuốc | Đơn vị tính | Số lượng tồn | Tồn tối thiểu | Hạn sử dụng | Trạng thái kiểm thử |
|---|---|---|---|---|---|---|
| `MED_001` | Amoxicillin 500mg | Viên | 500 | 50 | `2027-12-31` | Bình thường |
| `MED_002` | Paracetamol 500mg | Viên | 1000 | 100 | `2027-06-30` | Bình thường |
| `INV_001` | Găng tay y tế Nitrile (Hộp 50 đôi) | Hộp | 12 | 15 | `2028-01-01` | Cảnh báo tồn kho dưới mức tối thiểu (F_INV_08) |
| `INV_002` | Thuốc tê Lidocaine 2% | Ống | 40 | 10 | `2026-10-05` | Cảnh báo cận hạn sử dụng (< 30 ngày) |
| `INV_003` | Composite trám răng 3M Z250 | Tuýp | 0 | 5 | `2027-01-01` | Hết hàng (Out of Stock) - Chặn kê đơn / chỉ định |

### Bảng BOM (Định mức tiêu hao theo dịch vụ):
- Dịch vụ `SRV_TRAM_RANG` (Trám răng Composite):
  - 1 x Khay dụng cụ vô trùng
  - 1 x Ống hút nước bọt
  - 0.1 x Tuýp Composite
  - 1 x Mũi khoan kim cương (hao mòn)
- Quy tắc kiểm thử kho:
  - Khi bác sĩ hoàn tất khám: Hệ thống **chỉ hiển thị gợi ý** xuất các vật tư trên.
  - Tồn kho của `INV_001`, `INV_002` **không được giảm** cho đến khi Phụ tá bấm nút **"Xác nhận xuất kho"**.

---

## 5. Dữ liệu Hóa đơn & Thanh toán (MOD_BIL)

| Mã hóa đơn | Bệnh nhân | Tổng tiền dịch vụ | Giảm giá | Phải thu | Trả góp | Ghi chú kiểm thử |
|---|---|---|---|---|---|---|
| `INV_HAPPY_01` | `PAT_001` | 500,000 đ | 0 | 500,000 đ | Không | Thanh toán 1 lần đủ tiền mặt |
| `INV_DISCOUNT_01` | `PAT_002` | 2,000,000 đ | 200,000 đ | 1,800,000 đ | Không | Giảm giá hợp lệ 10% |
| `INV_INSTAL_01` | `PAT_003` | 30,000,000 đ | 0 | 30,000,000 đ | 3 kỳ (mỗi kỳ 10tr) | Lập kế hoạch trả góp niềng răng / cấy implant |
| `INV_ERR_NEG` | `PAT_001` | 1,000,000 đ | -50,000 đ | - | - | Giảm giá âm -> Phải chặn `400 Bad Request` |
| `INV_ERR_OVERPAY`| `PAT_001` | 500,000 đ | 0 | 500,000 đ | Thu 600,000 đ | Thu tiền vượt quá công nợ -> Phải chặn |

---

## 6. Dữ liệu Kiểm thử Bảo mật & IDOR

| Kịch bản | Token sử dụng | Đối tượng truy cập | Kết quả kỳ vọng |
|---|---|---|---|
| IDOR xem bệnh án | Token của `USR_PATIENT_01` (ID: 5) | `GET /api/patients/6/medical-record` | `403 Forbidden` (Chỉ xem được ID: 5) |
| SQL Injection | Không có token | Nhập `0900000001' OR '1'='1` vào ô Phone | `400 Bad Request` hoặc `401 Unauthorized` |
| XSS Injection | Token Receptionist | Nhập `<script>alert(1)</script>` vào Tên BN | Mã hóa HTML / Input sanitized an toàn |
| Token hết hạn | Expired JWT | Bất kỳ request bảo mật nào | `401 Unauthorized`, FE redirect `/login` |
| Giả mạo vai trò | Token của `PATIENT` | `GET /api/dev/admin-only` | `403 Forbidden` |
