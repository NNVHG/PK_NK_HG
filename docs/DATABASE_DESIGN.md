# DATABASE_DESIGN.md — Thiết kế cơ sở dữ liệu

> Nguồn: File đặc tả chức năng — cột "Dữ liệu chính" và quy tắc nghiệp vụ  
> Database: **PostgreSQL** | ORM: **Entity Framework Core** (Code First)

---

## 1. Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Bảng | PascalCase, số nhiều | `Patients`, `Appointments` |
| Cột | PascalCase | `PatientId`, `CreatedAt` |
| Primary Key | `{Entity}Id` | `PatientId` |
| Foreign Key | `{Referenced}Id` | `DentistId`, `ServiceId` |
| Timestamp | `CreatedAt`, `UpdatedAt` | kiểu `timestamptz` |
| Boolean | prefix `Is` | `IsActive`, `IsLocked` |
| Enum dạng string | varchar(50) | `'Pending'`, `'Confirmed'` |

---

## 2. Sơ đồ các nhóm bảng

```
═══════════════ AUTH & USERS ════════════════
Users ──── Roles ──── Permissions
       └── AuditLogs

═══════════════ PATIENTS ════════════════════
Patients ──── MedicalHistories
         ──── VitalSigns
         ──── SafetyAlerts
         ──── Teeth (1 patient → 20 hoặc 32 teeth)

═══════════════ APPOINTMENTS ════════════════
Appointments ──── AppointmentHistories
             └─── Visits

═══════════════ QUEUE ═══════════════════════
QueueEntries (AppointmentId → PatientId, Status, RoutedTo)

═══════════════ CLINICAL RECORDS ════════════
Visits ──── TreatmentPlans ──── Services
       ──── ToothConditions ──── Teeth, Pathologies
       ──── ToothHistories
       ──── ClinicalNotes
       ──── Prescriptions ──── PrescriptionItems ──── Drugs
       ──── Attachments, ImagingOrders
       ──── Invoices (1:1)
       └─── StockIssues (1:1 automatic)

═══════════════ BILLING ═════════════════════
Invoices ──── InvoiceItems
         ──── Payments
         ──── InstallmentPlans ──── InstallmentPayments
         ──── Discounts
         └─── InvoiceHistories

═══════════════ INVENTORY ═══════════════════
Items ──── Batches ──── StockReceiptItems ──── StockReceipts ──── Suppliers
      ──── BOMItems ──── Services
      ──── StockIssueItems ──── StockIssues
      ──── StockAdjustments
      └─── Consumptions

═══════════════ MASTER DATA ═════════════════
Services ──── BOMItems
Pathologies
Drugs
Chairs
SystemConfigs
```

---

## 3. Chi tiết schema các bảng

### 3.1 Users

```sql
CREATE TABLE Users (
    UserId        SERIAL PRIMARY KEY,
    Phone         VARCHAR(20)  UNIQUE NOT NULL,      -- định danh đăng nhập
    PasswordHash  VARCHAR(255) NOT NULL,              -- bcrypt
    FullName      VARCHAR(100) NOT NULL,
    Email         VARCHAR(100),
    RoleId        INT          NOT NULL REFERENCES Roles(RoleId),
    IsActive      BOOLEAN      NOT NULL DEFAULT TRUE,
    CreatedAt     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UpdatedAt     TIMESTAMPTZ
);

CREATE TABLE Roles (
    RoleId    SERIAL PRIMARY KEY,
    RoleName  VARCHAR(50) UNIQUE NOT NULL,  -- Admin, Nha sĩ, Lễ tân, Phụ tá, Bệnh nhân
    Description VARCHAR(200)
);

CREATE TABLE AuditLogs (
    LogId       BIGSERIAL PRIMARY KEY,
    UserId      INT         REFERENCES Users(UserId),
    Action      VARCHAR(50) NOT NULL,        -- CREATE, UPDATE, DELETE, LOGIN, etc.
    EntityType  VARCHAR(50) NOT NULL,        -- Patient, Invoice, Stock, etc.
    EntityId    INT,
    Detail      JSONB,
    IpAddress   VARCHAR(45),
    CreatedAt   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
-- Không cho UPDATE hoặc DELETE bảng AuditLogs
```

### 3.2 Patients

```sql
CREATE TABLE Patients (
    PatientId   SERIAL PRIMARY KEY,
    UserId      INT REFERENCES Users(UserId),  -- nếu bệnh nhân có tài khoản
    PatientCode VARCHAR(20)  UNIQUE,            -- mã bệnh nhân nội bộ
    FullName    VARCHAR(100) NOT NULL,
    DOB         DATE,
    Gender      VARCHAR(10),                   -- Male/Female/Other
    Phone       VARCHAR(20)  UNIQUE NOT NULL,
    Address     TEXT,
    CreatedAt   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE MedicalHistories (
    HistoryId   SERIAL PRIMARY KEY,
    PatientId   INT  NOT NULL REFERENCES Patients(PatientId),
    Condition   TEXT,
    Allergy     TEXT,
    Notes       TEXT,
    UpdatedBy   INT  REFERENCES Users(UserId),
    UpdatedAt   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE VitalSigns (
    VitalId       SERIAL PRIMARY KEY,
    PatientId     INT  NOT NULL REFERENCES Patients(PatientId),
    VisitId       INT  REFERENCES Visits(VisitId),
    BloodPressure VARCHAR(20),  -- "120/80"
    Pulse         INT,
    Temperature   DECIMAL(4,1),
    RecordedBy    INT  REFERENCES Users(UserId),
    RecordedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE Teeth (
    ToothId    SERIAL PRIMARY KEY,
    PatientId  INT  NOT NULL REFERENCES Patients(PatientId),
    FDICode    VARCHAR(3) NOT NULL,   -- "11", "55", v.v.
    ToothType  VARCHAR(10) NOT NULL, -- adult / child
    UNIQUE(PatientId, FDICode)
);
```

### 3.3 Appointments

```sql
CREATE TABLE Appointments (
    AppointmentId SERIAL PRIMARY KEY,
    PatientId     INT  NOT NULL REFERENCES Patients(PatientId),
    DentistId     INT  NOT NULL REFERENCES Users(UserId),
    ServiceId     INT  NOT NULL REFERENCES Services(ServiceId),
    ChairId       INT  REFERENCES Chairs(ChairId),
    AppDate       DATE NOT NULL,
    SlotStart     TIME NOT NULL,
    SlotEnd       TIME NOT NULL,
    Status        VARCHAR(20) NOT NULL DEFAULT 'Pending',
    -- Pending | Confirmed | CheckedIn | InProgress | Completed | Cancelled | NoShow
    Notes         TEXT,
    BookedBy      INT  REFERENCES Users(UserId),  -- bệnh nhân tự đặt hay lễ tân đặt
    CreatedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE AppointmentHistories (
    HistoryId       SERIAL PRIMARY KEY,
    AppointmentId   INT         NOT NULL REFERENCES Appointments(AppointmentId),
    OldStatus       VARCHAR(20),
    NewStatus       VARCHAR(20) NOT NULL,
    Reason          TEXT,
    ChangedBy       INT         REFERENCES Users(UserId),
    ChangedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.3b Queue Entries (Hàng đợi)

```sql
CREATE TABLE QueueEntries (
    QueueId       SERIAL PRIMARY KEY,
    AppointmentId INT         NOT NULL REFERENCES Appointments(AppointmentId),
    PatientId     INT         NOT NULL REFERENCES Patients(PatientId),
    Status        VARCHAR(60) NOT NULL DEFAULT 'Đang đợi',
    -- Đang đợi | Đã ghi nhận thông tin sức khỏe | Đang chụp X-quang | Chờ khám nha sĩ | Đang khám | Hoàn tất
    CalledAt      TIMESTAMPTZ,
    RoutedTo      VARCHAR(20),  -- 'Dentist' | 'Xray'
    CreatedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
-- Hàng đợi nội bộ, không công khai ra ngoài sảnh (F_CHK_02)
```

### 3.4 Visits (Lần khám)

```sql
CREATE TABLE Visits (
    VisitId        SERIAL PRIMARY KEY,
    PatientId      INT  NOT NULL REFERENCES Patients(PatientId),
    DentistId      INT  NOT NULL REFERENCES Users(UserId),
    AppointmentId  INT  REFERENCES Appointments(AppointmentId),
    StartTime      TIMESTAMPTZ,
    EndTime        TIMESTAMPTZ,
    Status         VARCHAR(20) NOT NULL DEFAULT 'InProgress',
    -- InProgress | Completed | Cancelled
    IsLocked       BOOLEAN     NOT NULL DEFAULT FALSE,
    LockedBy       INT         REFERENCES Users(UserId),
    LockedAt       TIMESTAMPTZ,
    CreatedAt      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE TreatmentPlans (
    PlanId     SERIAL PRIMARY KEY,
    VisitId    INT  NOT NULL REFERENCES Visits(VisitId),
    ToothId    INT  REFERENCES Teeth(ToothId),
    ServiceId  INT  NOT NULL REFERENCES Services(ServiceId),
    Status     VARCHAR(20) NOT NULL DEFAULT 'Planned',
    -- Planned | InProgress | Completed | Skipped
    Notes      TEXT,
    CreatedBy  INT  REFERENCES Users(UserId),
    CreatedAt  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE ToothConditions (
    ConditionId  SERIAL PRIMARY KEY,
    ToothId      INT  NOT NULL REFERENCES Teeth(ToothId),
    VisitId      INT  NOT NULL REFERENCES Visits(VisitId),
    PathologyId  INT  REFERENCES Pathologies(PathologyId),
    Surface      VARCHAR(20),  -- Buccal|Lingual|Mesial|Distal|Occlusal
    Status       VARCHAR(50) NOT NULL,
    RecordedBy   INT  REFERENCES Users(UserId),
    RecordedAt   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 3.5 Billing

```sql
CREATE TABLE Invoices (
    InvoiceId     SERIAL PRIMARY KEY,
    VisitId       INT         NOT NULL REFERENCES Visits(VisitId),
    PatientId     INT         NOT NULL REFERENCES Patients(PatientId),
    Status        VARCHAR(20) NOT NULL DEFAULT 'Draft',
    -- Draft | Paid | PartiallyPaid | Voided
    TotalAmount   DECIMAL(12,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    FinalAmount   DECIMAL(12,2) NOT NULL DEFAULT 0,
    CreatedBy     INT         REFERENCES Users(UserId),
    CreatedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE InvoiceItems (
    ItemId      SERIAL PRIMARY KEY,
    InvoiceId   INT          NOT NULL REFERENCES Invoices(InvoiceId),
    ServiceId   INT          NOT NULL REFERENCES Services(ServiceId),
    ToothId     INT          REFERENCES Teeth(ToothId),
    UnitPrice   DECIMAL(12,2) NOT NULL,
    Qty         INT           NOT NULL DEFAULT 1,
    LineTotal   DECIMAL(12,2) NOT NULL  -- = UnitPrice * Qty
);

CREATE TABLE Payments (
    PaymentId   SERIAL PRIMARY KEY,
    InvoiceId   INT         NOT NULL REFERENCES Invoices(InvoiceId),
    Amount      DECIMAL(12,2) NOT NULL,
    Method      VARCHAR(30) NOT NULL,  -- Cash | BankTransfer
    PaidAt      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ReceivedBy  INT         REFERENCES Users(UserId)
);

CREATE TABLE InstallmentPlans (
    PlanId       SERIAL PRIMARY KEY,
    InvoiceId    INT         NOT NULL REFERENCES Invoices(InvoiceId),
    TotalAmount  DECIMAL(12,2) NOT NULL,
    Deposit      DECIMAL(12,2) NOT NULL DEFAULT 0,
    InstallmentCount INT     NOT NULL,
    CreatedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE InstallmentPayments (
    PaymentId    SERIAL PRIMARY KEY,
    PlanId       INT         NOT NULL REFERENCES InstallmentPlans(PlanId),
    DueDate      DATE        NOT NULL,
    Amount       DECIMAL(12,2) NOT NULL,
    PaidAmount   DECIMAL(12,2) NOT NULL DEFAULT 0,
    PaidAt       TIMESTAMPTZ,
    Status       VARCHAR(20) NOT NULL DEFAULT 'Pending'
    -- Pending | Paid | Overdue
);
```

### 3.6 Inventory (Kho)

```sql
CREATE TABLE Items (
    ItemId       SERIAL PRIMARY KEY,
    Code         VARCHAR(30)  UNIQUE NOT NULL,
    Name         VARCHAR(200) NOT NULL,
    Unit         VARCHAR(20)  NOT NULL,
    Category     VARCHAR(20)  NOT NULL,  -- Drug | Material
    MinStock     DECIMAL(12,3) NOT NULL DEFAULT 0,
    PurchasePrice DECIMAL(12,2),
    SalePrice    DECIMAL(12,2),
    IsActive     BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE Batches (
    BatchId    SERIAL PRIMARY KEY,
    ItemId     INT           NOT NULL REFERENCES Items(ItemId),
    LotNo      VARCHAR(50),
    ExpiryDate DATE,
    Qty        DECIMAL(12,3) NOT NULL DEFAULT 0,  -- không âm
    CONSTRAINT chk_qty_non_negative CHECK (Qty >= 0)
);

CREATE TABLE BOMs (
    BOMId         SERIAL PRIMARY KEY,
    ServiceId     INT           NOT NULL REFERENCES Services(ServiceId),
    ItemId        INT           NOT NULL REFERENCES Items(ItemId),
    Qty           DECIMAL(12,3) NOT NULL,
    EffectiveDate DATE          NOT NULL DEFAULT CURRENT_DATE,
    UNIQUE(ServiceId, ItemId, EffectiveDate)
);

CREATE TABLE StockIssues (
    IssueId      SERIAL PRIMARY KEY,
    VisitId      INT         NOT NULL REFERENCES Visits(VisitId),
    IsAutomatic  BOOLEAN     NOT NULL DEFAULT TRUE,  -- BOM trigger
    CreatedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE StockIssueItems (
    ItemId    SERIAL PRIMARY KEY,
    IssueId   INT           NOT NULL REFERENCES StockIssues(IssueId),
    BatchId   INT           NOT NULL REFERENCES Batches(BatchId),
    Qty       DECIMAL(12,3) NOT NULL,
    Reason    VARCHAR(100)
);
```

### 3.7 Master Data

```sql
CREATE TABLE Services (
    ServiceId  SERIAL PRIMARY KEY,
    Code       VARCHAR(20)  UNIQUE NOT NULL,
    Name       VARCHAR(200) NOT NULL,
    Price      DECIMAL(12,2) NOT NULL,
    Duration   INT          NOT NULL,  -- phút
    IsActive   BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE Chairs (
    ChairId    SERIAL PRIMARY KEY,
    RoomNo     VARCHAR(20)  NOT NULL,
    Name       VARCHAR(50)  NOT NULL,
    Status     VARCHAR(20)  NOT NULL DEFAULT 'Active'
    -- Active | Maintenance | Inactive
);

CREATE TABLE Pathologies (
    PathologyId SERIAL PRIMARY KEY,
    Code        VARCHAR(20) UNIQUE NOT NULL,
    Name        VARCHAR(200) NOT NULL,
    Description TEXT
);
```

---

## 4. Index quan trọng

```sql
-- Tra cứu bệnh nhân nhanh
CREATE INDEX idx_patients_phone ON Patients(Phone);
CREATE INDEX idx_patients_name ON Patients(FullName);

-- Tìm lịch hẹn theo ngày và nha sĩ
CREATE INDEX idx_appointments_date_dentist ON Appointments(AppDate, DentistId);
CREATE INDEX idx_appointments_patient ON Appointments(PatientId);

-- Hàng đợi theo trạng thái
CREATE INDEX idx_queue_status ON QueueEntries(Status, CreatedAt);

-- Kiểm tra tồn kho
CREATE INDEX idx_batches_item_expiry ON Batches(ItemId, ExpiryDate);

-- Audit log theo entity
CREATE INDEX idx_audit_entity ON AuditLogs(EntityType, EntityId);
CREATE INDEX idx_audit_user ON AuditLogs(UserId, CreatedAt);
```

---

## 5. Migration strategy (EF Core)

```bash
# Tạo migration mới
dotnet ef migrations add <MigrationName>

# Áp dụng lên DB
dotnet ef database update

# Rollback (nếu cần)
dotnet ef database update <PreviousMigrationName>
```

**Quy tắc migration:**
- Không xóa migration đã chạy trên môi trường có dữ liệu
- Mọi thay đổi schema phải tạo migration mới
- Tên migration phải mô tả thay đổi: `AddInstallmentPlan`, `AddFDITeethTable`

---

## 6. Ghi chú quan trọng

| Vấn đề | Quyết định |
|---|---|
| Tồn kho âm | Constraint + kiểm tra trong Service trước khi xuất |
| Lô hết hạn | Lọc `ExpiryDate > NOW()` khi xuất kho |
| Soft delete | Dùng `IsActive = false` thay vì DELETE vật lý |
| Hóa đơn không xóa | Status = 'Voided' + ghi InvoiceHistories |
| AuditLog immutable | Không có UPDATE/DELETE endpoint cho AuditLogs |
| Hàng đợi không công khai | QueueEntries chỉ nhân viên truy cập; không có endpoint/màn hình public nào (F_CHK_02) |
| Bản ghi lần khám | Mỗi lần khám tạo Visit mới, không ghi đè lịch sử cũ (F_PAT_09) |
| [CẦN XÁC NHẬN] | Strategy lưu file ảnh (local disk / S3 / cloud storage) |
