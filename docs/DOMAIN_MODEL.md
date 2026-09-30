# DOMAIN_MODEL.md — Mô hình nghiệp vụ

> Nguồn: File đặc tả chức năng (chuc_nang_Gia_demo_7_CapNhat_LuongMoi), cột "Dữ liệu chính" và quy tắc nghiệp vụ

---

## 1. Các thực thể nghiệp vụ chính (Domain Entities)

### 1.1 User & Authentication

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `User` | UserId, Phone, PasswordHash, Email, Role, IsActive, CreatedAt | Tài khoản đăng nhập |
| `Role` | RoleId, RoleName, Description | Admin / Lễ tân / Nha sĩ / Phụ tá / Bệnh nhân |
| `Permission` | PermissionId, Code, Description | Quyền chi tiết |
| `AuditLog` | LogId, UserId, Action, EntityType, EntityId, Timestamp, Detail | Không được xóa/sửa |

**Quy tắc:**
- Phone là định danh duy nhất cho Bệnh nhân
- Tài khoản bị khóa (IsActive = false) không được đăng nhập
- Không xóa cứng tài khoản đã có dữ liệu nghiệp vụ

---

### 1.2 Patient (Bệnh nhân)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Patient` | PatientId, FullName, DOB, Gender, Phone, Address, CreatedAt | Hồ sơ định danh |
| `MedicalHistory` | HistoryId, PatientId, Condition, Allergy, Notes, UpdatedAt | Tiền sử & dị ứng |
| `VitalSigns` | VitalId, PatientId, VisitId, BloodPressure, Pulse, Temperature, RecordedAt | Nhập thủ công, không kết nối thiết bị |
| `SafetyAlert` | AlertId, PatientId, AlertType, Message, IsActive | Hiển thị khi mở hồ sơ |

**Quy tắc:**
- Không tạo trùng hồ sơ nếu đã có SĐT/mã định danh
- Bệnh nhân chỉ xem dữ liệu của chính mình

---

### 1.3 Appointment (Lịch hẹn)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Appointment` | AppointmentId, PatientId, DentistId, ServiceId, Date, SlotStart, SlotEnd, Status, Notes | Đơn vị lịch hẹn |
| `AppointmentHistory` | HistoryId, AppointmentId, OldStatus, NewStatus, Reason, ChangedBy, ChangedAt | Lịch sử đổi/hủy |

**Trạng thái lịch hẹn (State Machine):**
```
Pending → Confirmed → Checked-In → In-Progress → Completed
                    ↘ Cancelled (có thể từ nhiều trạng thái)
                    ↘ No-Show
```

**Quy tắc:**
- Bệnh nhân không được đặt quá 2 lịch Pending/ngày
- Đổi/hủy trước ít nhất 2 giờ (bệnh nhân tự thực hiện)
- Lễ tân xử lý đổi/hủy muộn

---

### 1.4 Visit & Clinical Records (Lần khám)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Visit` | VisitId, PatientId, DentistId, AppointmentId, StartTime, EndTime, Status, IsLocked | Phiên khám |
| `TreatmentPlan` | PlanId, VisitId, ToothId, ServiceId, Status, Notes | Dịch vụ theo răng |
| `ClinicalNote` | NoteId, VisitId, ToothId, Content, CreatedBy, CreatedAt | Ghi chú lâm sàng |
| `QueueEntry` | QueueId, AppointmentId, PatientId, Status, CalledAt, RoutedTo, CreatedAt | Mục hàng đợi cho mỗi lần khám |

**QueueStatus states:** `Đang đợi → Đã ghi nhận thông tin sức khỏe → Đang chụp X-quang → Chờ khám nha sĩ → Đang khám → Hoàn tất`

**Quy tắc:**
- Một nha sĩ chỉ có một Visit In-Progress tại một thời điểm
- Kết thúc Visit → trigger tạo Invoice nháp (BIL) + tự động trừ kho BOM (INV)
- Visit đã Locked chỉ được chỉnh sửa bởi người có quyền đặc biệt, phải ghi AuditLog
- Mỗi lần khám tạo một bản ghi Visit riêng — không ghi đè lịch sử cũ (F_PAT_09)

---

### 1.5 FDI Dental Chart (Sơ đồ răng)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Tooth` | ToothId, FDICode, PatientId, ToothType (adult/child) | 32 răng người lớn / 20 trẻ em |
| `ToothCondition` | ConditionId, ToothId, VisitId, PathologyId, Surface, Status, RecordedAt | Tình trạng bệnh lý |
| `ToothHistory` | HistoryId, ToothId, VisitId, OldStatus, NewStatus, ChangedAt | Lịch sử diễn tiến |

**Tiêu chuẩn FDI:**
- Người lớn: 32 răng, mã 11–18, 21–28, 31–38, 41–48
- Trẻ em: 20 răng, mã 51–55, 61–65, 71–75, 81–85
- Mặt răng: Buccal, Lingual, Mesial, Distal, Occlusal
- Không cho chọn răng không tồn tại trong bộ răng của bệnh nhân

---

### 1.6 Image (Hình ảnh)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `ImagingOrder` | OrderId, VisitId, ImageType, Notes, CreatedBy | Chỉ định chụp |
| `Attachment` | AttachmentId, VisitId, ToothId, FilePath, FileType, UploadedBy, UploadedAt | File ảnh đã tải lên |

**Quy tắc:**
- Không kết nối DICOM thiết bị vật lý
- Giới hạn định dạng và kích thước file
- Phân quyền xem ảnh theo role

---

### 1.7 Prescription (Đơn thuốc)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Prescription` | PrescriptionId, VisitId, DentistId, IssuedAt, IsPrinted | Đơn thuốc |
| `PrescriptionItem` | ItemId, PrescriptionId, DrugId, Qty, Dosage, Instructions | Chi tiết từng thuốc |

**Quy tắc:**
- Thuốc phải thuộc danh mục hệ thống (MOD_MST)
- Không ghi nhận xuất vượt tồn nếu phòng khám cấp thuốc tại chỗ
- Đơn thuốc do nha sĩ kê; không tạo lịch tái khám (F_RX_03 đã loại khỏi phạm vi)

---

### 1.8 Billing (Hóa đơn & Thanh toán)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Invoice` | InvoiceId, VisitId, PatientId, Status, TotalAmount, DiscountAmount, FinalAmount, CreatedAt | Hóa đơn |
| `InvoiceItem` | ItemId, InvoiceId, ServiceId, ToothId, UnitPrice, Qty, LineTotal | Chi tiết dịch vụ |
| `Payment` | PaymentId, InvoiceId, Amount, Method, PaidAt, ReceivedBy | Giao dịch thanh toán |
| `InstallmentPlan` | PlanId, InvoiceId, TotalAmount, Deposit, InstallmentCount | Kế hoạch trả góp |
| `InstallmentPayment` | PaymentId, PlanId, DueDate, Amount, PaidAmount, PaidAt, Status | Từng kỳ trả góp |
| `Discount` | DiscountId, Code, Type, Value, Reason | Giảm giá |
| `InvoiceHistory` | HistoryId, InvoiceId, Action, Reason, ChangedBy, ChangedAt | Lịch sử hóa đơn |

**Quy tắc:**
- Tổng tiền không được âm sau giảm giá
- Không xóa vật lý Invoice đã sinh — void hoặc adjust với lý do
- Tổng các kỳ trả góp phải khớp số phải thu
- Chỉ tính giao dịch đã thanh toán vào báo cáo doanh thu

---

### 1.9 Inventory (Kho & Vật tư)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Item` | ItemId, Code, Name, Unit, Category (drug/material), MinStock, PurchasePrice, SalePrice | Danh mục vật tư/thuốc |
| `Supplier` | SupplierId, Name, Contact, Address | Nhà cung cấp |
| `StockReceipt` | ReceiptId, SupplierId, CreatedAt, CreatedBy | Phiếu nhập kho |
| `StockReceiptItem` | ItemId, ReceiptId, ItemId, LotNo, ExpiryDate, Qty, UnitPrice | Chi tiết nhập |
| `Batch` | BatchId, ItemId, LotNo, ExpiryDate, Qty | Tồn theo lô |
| `BOM` | BOMId, ServiceId, ItemId, Qty, EffectiveDate | Định mức vật tư |
| `StockIssue` | IssueId, VisitId, IsAutomatic, CreatedAt | Phiếu xuất kho |
| `StockIssueItem` | ItemId, IssueId, ItemId, BatchId, Qty, Reason | Chi tiết xuất |
| `StockAdjustment` | AdjId, ItemId, BatchId, Qty, Reason, AdjustedBy, AdjustedAt | Điều chỉnh kiểm kê |
| `Consumption` | ConsumptionId, VisitId, ItemId, Qty, Type (BOM/extra), RecordedBy | Tiêu hao thực tế |

**Quy tắc:**
- Không xuất lô đã hết hạn
- Không cho tồn âm — cảnh báo nhân viên khi tồn kho không đủ
- Khi kết thúc khám: hệ thống gợi ý tiêu hao theo BOM (F_INV_05, F_INV_06); Phụ tá xem lại và xác nhận thủ công trước khi trừ tồn
- Mọi thay đổi tồn kho phải có AuditLog

---

### 1.10 Master Data (Danh mục hệ thống)

| Entity | Thuộc tính chính | Ghi chú |
|---|---|---|
| `Service` | ServiceId, Name, Price, Duration, IsActive | Dịch vụ & bảng giá |
| `Pathology` | PathologyId, Code, Name, Description | Bệnh lý/triệu chứng răng |
| `Drug` | DrugId, Code, Name, ActiveIngredient, Strength, Unit | Danh mục thuốc |
| `Chair` | ChairId, RoomNo, Name, Status (active/maintenance) | Ghế/phòng điều trị |
| `SystemConfig` | ConfigKey, ConfigValue, UpdatedBy, UpdatedAt | Cấu hình hệ thống |

---

## 2. Quan hệ giữa các thực thể (Entity Relationships)

```
Patient ─── MedicalHistory (1:N)
Patient ─── Appointment (1:N)
Patient ─── Visit (1:N)
Patient ─── Tooth (1:N, 32 hoặc 20 răng)

Appointment ─── Visit (1:1)
Visit ─── TreatmentPlan (1:N)
Visit ─── ToothCondition (1:N)
Visit ─── Prescription (1:1 hoặc 1:N)
Visit ─── Invoice (1:1)
Visit ─── StockIssue (1:1 automatic)
Visit ─── Attachment (1:N)

Invoice ─── InvoiceItem (1:N)
Invoice ─── Payment (1:N)
Invoice ─── InstallmentPlan (0:1)
InstallmentPlan ─── InstallmentPayment (1:N)

Service ─── BOM (1:N)
BOM ─── Item (N:1)
StockIssue ─── StockIssueItem (1:N)
StockReceiptItem ─── Batch (1:1)
```

---

## 3. Quy tắc nghiệp vụ tổng hợp

| # | Quy tắc | Nguồn |
|---|---|---|
| 1 | Phone bệnh nhân là định danh duy nhất | F_PAT_01 |
| 2 | Bệnh nhân tối đa 2 lịch Pending/ngày | F_APP_02 |
| 3 | Đổi/hủy trước ít nhất 2 giờ (bệnh nhân) | F_APP_05 |
| 4 | Kết thúc Visit → tạo Invoice nháp + BOM tự động trừ kho | F_INV_06, F_BIL_01 |
| 5 | Không xuất kho quá tồn, không xuất lô hết hạn | F_INV_06, F_INV_04 |
| 6 | Tổng tiền hóa đơn không âm sau giảm giá | F_BIL_02 |
| 7 | Tổng kỳ trả góp = số phải thu | F_BIL_04 |
| 8 | AuditLog ghi nhận mọi thao tác nhạy cảm | F_AUTH_06 |
| 9 | Visit đã Locked chỉ chỉnh với quyền đặc biệt + audit | F_PAT_08 |
| 10 | Cảnh báo dị ứng/bệnh nền khi mở hồ sơ khám | F_PAT_06 |
| 11 | FDI phải đúng chuẩn số răng theo độ tuổi | F_FDI_01 |
| 12 | Mỗi lần khám tạo bản ghi Visit mới — không ghi đè lịch sử | F_PAT_09 |
| 13 | Nha sĩ kê đơn thuốc; không tạo lịch tái khám | F_RX_01 |
| 14 | Hàng đợi chỉ nhân viên thấy — không công khai | F_CHK_02 |
