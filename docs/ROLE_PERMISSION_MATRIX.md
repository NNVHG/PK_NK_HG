# ROLE_PERMISSION_MATRIX.md — Ma trận phân quyền RBAC

> Nguồn: Sheet 05 (Ma trận RBAC) — File đặc tả chức năng

---

## 1. Các vai trò trong hệ thống

| Vai trò | Tên tiếng Anh | Mô tả |
|---|---|---|
| **Bệnh nhân** | Patient | Xem dữ liệu cá nhân, đặt lịch online, xem hồ sơ |
| **Lễ tân/Thu ngân** | Receptionist/Cashier | Tiếp đón, đặt lịch, check-in, thanh toán |
| **Nha sĩ** | Dentist | Khám, ghi hồ sơ FDI, kê đơn, kết thúc ca |
| **Phụ tá** | Assistant | Hỗ trợ điều trị, ghi tiêu hao, vô trùng |
| **Quản trị viên** | Admin | Toàn quyền hệ thống, báo cáo, danh mục, kho |

---

## 2. Ma trận phân quyền theo nhóm chức năng

| # | Nhóm chức năng | Bệnh nhân | Lễ tân/Thu ngân | Nha sĩ | Phụ tá | Quản trị viên |
|---|---|---|---|---|---|---|
| 1 | Đăng ký/Đăng nhập | ✅ Được phép | ✅ Được phép | ✅ Được phép | ✅ Được phép | ✅ Được phép |
| 2 | Quản lý hồ sơ bệnh nhân | 👁 Xem của mình | ✏️ Thêm/sửa nghiệp vụ | 👁 Xem | 👁 Xem nghiệp vụ | 🔑 Quản trị |
| 3 | Đặt lịch, tiếp đón & hàng đợi *(Bệnh nhân tự đặt online; Lễ tân đặt hộ tại quầy. Sau khi đặt, hồ sơ vào hàng đợi chung; hàng đợi không công khai. Lễ tân/Phụ tá có thể gọi số và dẫn bệnh nhân.)* | ✅ Được phép | ✅ Điều phối chính | 👁 Xem lịch của mình | 🤝 Hỗ trợ | 🔑 Quản trị |
| 4 | Bắt đầu/kết thúc lần khám *(Bản ghi khám mới được tạo riêng cho từng lần khám; không ghi đè lịch sử.)* | ❌ Không | 👁 Theo dõi | ✅ Được phép | 🤝 Hỗ trợ | ⚙️ Theo quyền |
| 5 | Sơ đồ răng FDI | 👁 Xem lịch sử mình | 👁 Xem theo quyền | ✏️ Chỉnh sửa | 👁 Hỗ trợ xem | 👁 Xem/điều hành |
| 6 | Ghi nhận ảnh hồ sơ | 👁 Xem ảnh mình | ❌/⚙️ Theo quyền | ✅ Được phép | ⬆️ Tải lên | 🔑 Quản trị truy cập |
| 7 | Kê đơn thuốc *(Đơn thuốc do nha sĩ kê; không tạo lịch tái khám.)* | 👁 Xem đơn mình | 🤝 Hỗ trợ | ✅ Được phép | 🤝 Hỗ trợ | ⚙️ Theo quyền |
| 8 | Thanh toán & hóa đơn *(Thu ngân là người xử lý thu tiền.)* | 👁 Xem của mình | ✅ Được phép | 👁 Xem | ❌ Không | ⚙️ Theo quyền |
| 9 | Quản lý kho | ❌ Không | ❌ Không | 👁 Xem tồn thuốc | ✏️ Ghi tiêu hao | ✅ Được phép |
| 10 | Danh mục hệ thống | ❌ Không | ❌ Không | 👁 Xem | 👁 Xem theo quyền | ✅ Được phép |
| 11 | Báo cáo quản trị | ❌ Không | 📊 Báo cáo vận hành | 📊 Hiệu suất cá nhân | 📊 Kho/tiêu hao | ✅ Được phép |
| 12 | Audit Log / nhật ký thao tác *(Chỉ Admin xem chi tiết.)* | ❌ Không | ❌ Không | ❌ Không | ❌ Không | ✅ Được phép |

**Chú thích:** ✅ Toàn quyền | 👁 Chỉ xem | ✏️ Thêm/sửa | ❌ Không có quyền | ⚙️ Tuỳ quyền được gán | 🔑 Quản trị | 🤝 Hỗ trợ hạn chế | ⬆️ Upload | 📞 Hành động đặc thù | 📊 Báo cáo giới hạn

---

## 3. Phân quyền chi tiết theo chức năng (từ Sheet 03)

### MOD_AUTH — Xác thực & Phân quyền

| Mã | Tên chức năng | Bệnh nhân | Lễ tân | Nha sĩ | Phụ tá | Admin |
|---|---|---|---|---|---|---|
| F_AUTH_01 | Đăng ký tài khoản bệnh nhân | ✅ | — | — | — | ✅ |
| F_AUTH_02 | Đăng nhập & cấp quyền RBAC | ✅ | ✅ | ✅ | ✅ | ✅ |
| F_AUTH_03 | Cập nhật hồ sơ cá nhân & đổi mật khẩu | ✅ (của mình) | ✅ | ✅ | ✅ | ✅ |
| F_AUTH_04 | Quên/khôi phục mật khẩu | ✅ | ✅ | ✅ | ✅ | ✅ |
| F_AUTH_05 | Quản lý vai trò và quyền | ❌ | ❌ | ❌ | ❌ | ✅ |
| F_AUTH_06 | Nhật ký thao tác hệ thống | ❌ | ❌ | ❌ | ❌ | ✅ |

### MOD_PAT — Bệnh nhân & Hồ sơ

| Mã | Tên chức năng | Bệnh nhân | Lễ tân | Nha sĩ | Phụ tá | Admin |
|---|---|---|---|---|---|---|
| F_PAT_01 | Tạo hồ sơ bệnh nhân | — | ✅ | — | — | ✅ |
| F_PAT_02 | Tìm kiếm hồ sơ bệnh nhân | — | ✅ | ✅ | — | ✅ |
| F_PAT_03 | Kiểm tra trùng hồ sơ | — | ✅ | — | — | ✅ |
| F_PAT_04 | Ghi tiền sử bệnh & dị ứng | — | ✅ | ✅ | — | ✅ |
| F_PAT_05 | Ghi nhận sinh hiệu cơ bản | — | ✅ | ✅ | ✅ | ✅ |
| F_PAT_06 | Cảnh báo tiền sử an toàn | Hệ thống | Hệ thống | Hệ thống | — | ✅ |
| F_PAT_07 | Lịch sử điều trị (timeline) | ✅ (mình) | ✅ hạn chế | ✅ | — | ✅ |
| F_PAT_08 | Khóa/chốt hồ sơ điều trị | — | — | ✅ | — | ✅ |
| F_PAT_09 | Tạo bản ghi lần khám mới | — | ✅ | ✅ | ✅ | ✅ |

### MOD_BIL — Thanh toán & Hóa đơn

| Mã | Tên chức năng | Bệnh nhân | Lễ tân/Thu ngân | Nha sĩ | Phụ tá | Admin |
|---|---|---|---|---|---|---|
| F_BIL_01 | Tạo hóa đơn nháp | Hệ thống | Hệ thống | — | — | ✅ |
| F_BIL_02 | Áp dụng giảm giá | — | ✅ | — | — | ✅ |
| F_BIL_03 | Xác nhận thanh toán | — | ✅ (Thu ngân) | — | — | ✅ |
| F_BIL_04 | Lập kế hoạch trả góp | — | ✅ | — | — | ✅ |
| F_BIL_05 | Thu kỳ trả góp | — | ✅ (Thu ngân) | — | — | ✅ |
| F_BIL_06 | In/xuất biên lai | 👁 mình | ✅ | — | — | ✅ |
| F_BIL_07 | Hủy/điều chỉnh hóa đơn nháp | — | ✅ | — | — | ✅ |

### MOD_RPT — Báo cáo

| Mã | Tên chức năng | Bệnh nhân | Lễ tân | Nha sĩ | Phụ tá | Admin |
|---|---|---|---|---|---|---|
| F_RPT_01 | Báo cáo doanh thu | ❌ | ❌ | ❌ | ❌ | ✅ |
| F_RPT_02 | Báo cáo hiệu suất nha sĩ | ❌ | ❌ | ✅ (mình) | ❌ | ✅ |
| F_RPT_04 | Báo cáo xuất-nhập-tồn | ❌ | ❌ | ❌ | ✅ | ✅ |
| F_RPT_05 | Báo cáo tiêu hao theo dịch vụ | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## 4. Nguyên tắc kiểm soát truy cập

- **Principle of Least Privilege:** Mỗi vai trò chỉ nhận quyền tối thiểu cần thiết
- **Data isolation:** Bệnh nhân chỉ thấy dữ liệu của chính mình (F_PAT_07, F_BIL_06, F_APP_02, v.v.)
- **Không xóa quyền hệ thống:** Không được gán/hủy quyền Admin duy nhất, tránh lockout
- **Audit mọi thao tác nhạy cảm:** Kho, hóa đơn, bệnh án, tài khoản — ghi AuditLog
- **Chỉ Admin xem AuditLog:** Nhóm 12 trong ma trận
- **[CẦN XÁC NHẬN]:** Quyền "Chủ phòng khám" — trong Sheet 04 Module RPT ghi "Admin, Chủ phòng khám" nhưng Sheet 05 RBAC không liệt kê vai trò này riêng. Xác nhận có cần thêm role này không.

---

## 5. Triển khai RBAC trong ASP.NET Core

```csharp
// Ví dụ controller với phân quyền
[Authorize(Roles = "Admin,Lễ tân")]
[HttpPost("/api/appointments")]
public IActionResult CreateAppointment([FromBody] CreateAppointmentDto dto) { ... }

[Authorize(Roles = "Nha sĩ")]
[HttpPut("/api/visits/{id}/complete")]
public IActionResult CompleteVisit(int id) { ... }

[Authorize] // Tất cả đã đăng nhập
[HttpGet("/api/patients/{id}/timeline")]
public IActionResult GetTimeline(int id)
{
    // Service kiểm tra PatientId == currentUser.PatientId nếu role là Bệnh nhân
}
```
