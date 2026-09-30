# REUSE_ANALYSIS.md — Phân tích tái sử dụng từ block-based-pkm

> **Phiên bản:** v2 (2026-09-27) — bổ sung dữ liệu đồ thị từ graphify (6620 nodes, 19270 edges, 227 communities)  
> **Nguồn tham khảo:** Local repo `block-based-pkm-main/` (Block Paged — Notion-like PKM)  
> **Dự án đích:** Hệ thống quản lý phòng khám nha khoa Hoàng Gia  
> **Nguyên tắc:** Chỉ ghi nhận điều thực sự có trong 4 nguồn. Mâu thuẫn → ưu tiên `md quan trọng/`. Không sửa trực tiếp `md quan trọng/`.

---

## MỤC LỤC

1. [Tổng quan repo tham khảo](#1-tổng-quan-repo-tham-khảo)
2. [Stack công nghệ chồng lấp](#2-stack-công-nghệ-chồng-lấp)
3. [Kiến trúc và pattern tái sử dụng được](#3-kiến-trúc-và-pattern-tái-sử-dụng-được)
4. [Logic tương tự — ánh xạ nghiệp vụ](#4-logic-tương-tự--ánh-xạ-nghiệp-vụ)
5. [Phần không tái sử dụng — lý do loại trừ](#5-phần-không-tái-sử-dụng--lý-do-loại-trừ)
6. [Đề xuất cụ thể cho dự án](#6-đề-xuất-cụ-thể-cho-dự-án)

---

## 1. Tổng quan repo tham khảo

### 1.1 Định danh dự án

**Block Paged** (tên repo: `block-based-pkm-main`) là hệ thống PKM (Personal Knowledge Management — Quản lý tri thức cá nhân) theo phong cách Notion. Hệ thống cho phép nhiều người dùng cộng tác chỉnh sửa page, quản lý task, nhắn tin và nhận gợi ý AI.

### 1.2 Stack công nghệ xác nhận từ repo

**Frontend (thư mục `client/`):**
- Vue 3 + Vite + TypeScript
- Pinia (state management — quản lý trạng thái)
- Axios (HTTP client)
- Bootstrap 5
- Editor.js (block-based rich text editor)
- SignalR client (WebSocket realtime)

**Backend (thư mục `server/src/`):**
- ASP.NET Core .NET 8 Web API
- PostgreSQL + Entity Framework Core (Code First)
- JWT Bearer authentication (xác thực)
- SignalR + Redis backplane (realtime và distributed lock)
- Cloudinary (lưu trữ file)
- SMTP (email)
- Docker + Docker Compose

**Cấu trúc project backend (4 layer Clean Architecture):**
```
Pkm.Api          — Controllers, Middleware, Contracts (Requests/Responses)
Pkm.Application  — Features (Commands/Queries/Services), Common/Abstractions, Common/Results
Pkm.Domain       — Entities, Domain Events, SharedKernel
Pkm.Infrastructure — Authentication, Cache, Email, Persistence, Realtime, Storage, Time
```

### 1.3 Cấu trúc đồ thị mã nguồn (graphify — 826 files)

Kết quả chạy `graphify . --code-only` trên toàn repo:

| Chỉ số | Giá trị |
|--------|---------|
| Nodes (ký hiệu code) | 6620 |
| Edges (quan hệ phụ thuộc) | 19270 |
| Communities (cụm chức năng) | 227 (197 hiển thị) |
| Tỉ lệ trích xuất | 90% EXTRACTED, 10% INFERRED |

**God Nodes — 10 abstraction trung tâm (nhiều edge nhất):**

| Rank | Node | Số edges | Ý nghĩa |
|------|------|----------|---------|
| 1 | `Pkm.Application.Common.UseCases` | 241 | Dispatcher pattern — điểm điều phối use case toàn hệ thống |
| 2 | `Pkm.Application.Common.Abstractions.Persistence` | 169 | Interface repository — hợp đồng data access |
| 3 | `Guid` | 151 | ID type chuẩn cho mọi entity |
| 4 | `Pkm.Application.Common.Abstractions.Authentication` | 150 | Interface xác thực — `ICurrentUser`, `IPasswordHasher` |
| 5 | `Pkm.Application.Common.Results` | 148 | Result pattern namespace |
| 6 | `Result<T>` | 141 | Kiểu trả về an toàn thay exception |
| 7 | `getApiErrorMessage()` | 135 | Frontend error normalization |
| 8 | `getApiResultErrorMessage()` | 128 | Frontend API result parsing |
| 9 | `ApiResult` | 124 | Backend response wrapper |
| 10 | `ICurrentUser` | 112 | Interface truy cập user hiện tại từ mọi layer |

**Phát hiện từ đồ thị:**
- `DataContext` (EF Core DbContext) có betweenness centrality 0.055 — là cầu nối giữa 31 entity khác nhau (User, Block, ActivityLog, RefreshToken, OutboxMessage...). Đây là điểm hội tụ của toàn bộ persistence layer.
- `Result<T>` có betweenness centrality 0.055 — là "ngôn ngữ chung" giữa mọi command handler và controller. 141 edges xác nhận đây không phải utility nhỏ mà là quy ước kiến trúc toàn hệ thống.
- **Không có import cycle** — kiến trúc Clean Architecture giữ đúng dependency rule (Domain ← Application ← Infrastructure ← Api).

**Communities liên quan nhất đến dental system:**
- Community 1 "ICurrentUser" (145 nodes): toàn bộ auth/identity — tái sử dụng trực tiếp
- Community 23 "Pkm.Infrastructure.Persistence.Outbox" (28 nodes): Outbox + Realtime infrastructure
- Community 55 "DataContext" (36 nodes): DbContext với 31 DbSet — pattern áp dụng cho DentalDbContext
- Community 124 "ActivityLog" (13 nodes): Immutable audit log entity — ánh xạ trực tiếp F_AUTH_06
- Community 163 "ResultStatus" (9 nodes): enum trạng thái kết quả (Conflict, Forbidden, NotFound...) — tái sử dụng nguyên

---

## 2. Stack công nghệ chồng lấp

### 2.1 Bảng đối chiếu stack

| Thành phần | Block Paged (tham khảo) | Dental System (đích) | Chồng lấp |
|-----------|------------------------|----------------------|-----------|
| Frontend framework | Vue 3 + Vite + TypeScript | Vue.js (DL-001, xlsx) | ✅ Đồng nhất |
| State management | Pinia | Pinia (DL-009, xlsx) | ✅ Đồng nhất |
| HTTP client | Axios | Axios (implied by Vue stack) | ✅ Đồng nhất |
| Backend framework | ASP.NET Core .NET 8 | ASP.NET Core (DL-002, xlsx) | ✅ Đồng nhất |
| ORM | Entity Framework Core | EF Core Code First (DL-008) | ✅ Đồng nhất |
| Database | PostgreSQL | PostgreSQL (DL-003, xlsx) | ✅ Đồng nhất |
| Auth | JWT Bearer | JWT/RBAC (DL-001–009, xlsx) | ✅ Đồng nhất |
| Validation | FluentValidation | FluentValidation (SECURITY_OVERVIEW.md) | ✅ Đồng nhất |
| CSS framework | Bootstrap 5 | Không xác định | ⚠️ Chưa quyết định |
| Realtime | SignalR + Redis | Không trong scope | ❌ Không áp dụng |
| File storage | Cloudinary | [CẦN XÁC NHẬN] (DL-P03) | ⚠️ Chờ quyết định |
| Email | SMTP | Notification channel chưa xác định | ⚠️ Chờ quyết định |
| AI/Recommendations | OpenAI (gợi ý task) | Không trong scope | ❌ Không áp dụng |

**Nhận xét:** 7/13 thành phần đồng nhất hoàn toàn. Đây là cơ sở vững chắc để tái sử dụng code, pattern và cấu hình từ Block Paged.

### 2.2 Dependency NuGet đã xác nhận (Community 94 — Pkm.Infrastructure)

```
Microsoft.EntityFrameworkCore               8.0.16
Microsoft.EntityFrameworkCore.Tools         8.0.16
Microsoft.AspNetCore.Authentication.JwtBearer 8.0.16
Microsoft.AspNetCore.SignalR.StackExchangeRedis 8.0.16
CloudinaryDotNet                            1.27.2
Npgsql.EntityFrameworkCore.PostgreSQL       (implied)
```

**Dental system nên dùng:** EF Core 8.x + Npgsql + JwtBearer — cùng version cho dễ tra cứu ví dụ.

### 2.3 Dependency npm đã xác nhận (Community 110 — dependencies)

```
axios, bootstrap, bootstrap-icons
@editorjs/* (checklist, code, header, list...)  — KHÔNG áp dụng cho dental
vue (implied), pinia (implied)
```

---

## 3. Kiến trúc và pattern tái sử dụng được

### 3.1 Bảng tổng hợp pattern

| # | Pattern | Vị trí trong Block Paged | Áp dụng cho Dental System | Ghi chú |
|---|---------|--------------------------|--------------------------|---------|
| P-01 | Clean Architecture 4 layers | `Pkm.Api / .Application / .Domain / .Infrastructure` | `Dental.Api / .Application / .Domain / .Infrastructure` | God Node `UseCases` (241 edges) xác nhận đây là xương sống |
| P-02 | Result\<T\> pattern | `Pkm.Application.Common.Results` (148 edges) | Mọi command/query handler trả `Result<T>` | Thay `throw` bằng `Result.Failure(Error.NotFound(...))` |
| P-03 | ICommand / IQuery + Dispatcher | `Pkm.Application.Common.UseCases` (241 edges) | Mỗi chức năng = 1 Command hoặc 1 Query | Tách rõ write-side và read-side |
| P-04 | Abstractions layer | `Pkm.Application.Common.Abstractions.*` (150–169 edges) | `ICurrentUser, IPasswordHasher, ICacheService, IEmailSender, IFileStorageService` | God Nodes #2, #4 xác nhận không thể thiếu |
| P-05 | Repository pattern (Read/Write tách biệt) | `IBlockReadRepository`, `IBlockWriteRepository` | `IPatientReadRepository`, `IVisitWriteRepository`... | Thấy rõ trong Community 78 |
| P-06 | Outbox pattern | `Pkm.Infrastructure.Persistence.Outbox` (Community 23) | Dùng cho BOM auto-trigger khi Visit hoàn tất (F_INV_06) | Đảm bảo không mất event khi transaction commit |
| P-07 | UnitOfWork | `UnitOfWork` (Community 201) | Bọc transaction khi CompleteVisit() + AutoIssueByBOM() | Đặc biệt quan trọng vì BOM cần atomic |
| P-08 | Policy-based Authorization | `WorkspaceCapabilitySet`, `DocumentCapabilitySet` (Community 114) | RBAC 5 vai trò — `[Authorize(Policy="...")]` | Phức tạp hơn `[Authorize(Roles="...")]`, phù hợp dental |
| P-09 | ActivityLog / AuditLog | `ActivityLog` entity (Community 124, 13 nodes) | F_AUTH_06 Nhật ký thao tác hệ thống | Immutable, không cho sửa/xóa — giống thiết kế đã có |
| P-10 | ExceptionMappingMiddleware | `ExceptionMappingMiddleware` (Community 178) | Global error handling — map exception → HTTP status | Tái sử dụng pattern, không copy code |
| P-11 | CurrentUser (HttpContext injection) | `CurrentUser` implements `ICurrentUser` (Community 169) | Inject vào handler qua constructor | ClaimsPrincipal → UserId, Role |
| P-12 | PagedResult\<T\> | `PagedResult` (Community 198) | Phân trang danh sách bệnh nhân, lịch hẹn, hóa đơn | HasNextPage, HasPreviousPage, TotalPages |
| P-13 | FluentValidation per command | `ForgotPasswordCommandValidator`, `AddWorkspaceMemberCommandValidator`... | 1 validator class per command — validate trước khi handler chạy | Thấy rõ trong Communities 184, 185, 118 |
| P-14 | Frontend composable pattern | `useNotificationCenter.ts`, `usePageEditor.ts`, `useRegister.ts` | `useAppointment.ts`, `useVisit.ts`, `useInventory.ts` | Community 116 (cohesion 0.23) — mẫu tốt nhất |
| P-15 | ApiResult wrapper (frontend) | `getApiErrorMessage()` + `getApiResultErrorMessage()` (135+128 edges) | Centralized error display cho mọi API call | 2 God Nodes frontend — bắt buộc áp dụng |

### 3.2 Cấu trúc thư mục đề xuất từ phân tích

**Backend (dựa trên Pkm.Application/Features structure):**
```
Dental.Application/
  Common/
    Abstractions/           ← ICurrentUser, ICacheService, IEmailSender, IFileStorageService
    Authorization/          ← Policy definitions
    Results/               ← Result<T>, Error, ResultStatus, PagedResult<T>
    UseCases/              ← ICommand, IQuery, IUseCaseDispatcher
    Validation/            ← ValidationResult, base validator
  Features/
    Auth/                  ← Commands: Register, Login, ForgotPassword, Logout
    Patient/               ← Commands: Create, Update; Queries: Get, Search
    Appointment/           ← Commands: Book, Reschedule, Cancel; Queries: GetSlots
    Visit/                 ← Commands: StartVisit, CompleteVisit (trigger BOM)
    DentalChart/           ← Commands: UpdateToothCondition; Queries: GetChart
    Billing/               ← Commands: CreateDraftInvoice, ConfirmPayment
    Inventory/             ← Commands: AutoIssueBOM, ReceiveStock; Queries: CheckStock
    Roster/                ← Commands: CreateShift; Queries: GetAvailableSlots
    ...
```

**Frontend (dựa trên Block Paged client/ structure):**
```
client/src/
  api/                     ← axios instances, API client per controller
  composables/             ← useAppointment, useVisit, usePatient, useInventory...
  stores/                  ← Pinia stores: authStore, appointmentStore...
  views/                   ← Page-level components theo module
  components/              ← Shared UI components
  router/                  ← Vue Router với route guards
  utils/                   ← getApiErrorMessage, auth-token utilities
```

---

## 4. Logic tương tự — ánh xạ nghiệp vụ

### 4.1 Bảng ánh xạ tương tự

| # | Logic trong Block Paged | Module/File | Logic tương tự trong Dental | Chức năng mã |
|---|------------------------|-------------|------------------------------|--------------|
| L-01 | Workspace member roles (Owner/Admin/Member/Viewer) + CapabilitySet | `WorkspaceRole`, Community 114 | 5 vai trò dental + ma trận quyền 15 nhóm chức năng | F_AUTH_05, ROLE_PERMISSION_MATRIX.md |
| L-02 | ActivityLog (immutable, ghi action/entity/actor/timestamp) | `ActivityLog` entity, Community 124 | F_AUTH_06 Audit Log — ghi thao tác nhạy cảm | F_AUTH_06 |
| L-03 | Block distributed edit lock (Redis) khi 2 user cùng edit | `RedisBlockEditLeaseService`, Community 51 | Transaction isolation khi 2 user đồng thời CompleteVisit() + AutoIssueByBOM() | F_INV_06 |
| L-04 | File upload → Cloudinary → lưu StoredFile record | `CloudinaryFileStorageService`, Community 49 | Upload ảnh X-quang/nội miệng → lưu Attachment record | F_IMG_02 |
| L-05 | Paged list với filter (workspace tasks, activity logs) | `WorkTaskListFilter`, Community 50 | Danh sách bệnh nhân, lịch hẹn, hóa đơn có filter + phân trang | F_PAT_02, F_APP_04 |
| L-06 | RefreshToken entity (rotate on use, revoke on logout) | `RefreshToken`, Community 89 | JWT Refresh Token logic — rotate và revoke | F_AUTH_02 |
| L-07 | SMTP email sender với template | `SmtpEmailSender`, Community 59 | Nhắc lịch hẹn qua email (F_APP_07) | F_APP_07 |
| L-08 | ForgotPassword flow (generate token → send email → reset) | `ForgotPasswordCommand`, Community 184 | F_AUTH_04 Quên/khôi phục mật khẩu | F_AUTH_04 |
| L-09 | OutboxMessage → dispatch integration event | `OutboxBatchProcessor` (Community 109), `OutboxMessageDispatcher` (Community 139) | BOM auto-trigger khi Visit.Complete() — emit event → InventoryService xử lý | F_INV_06, DL-004 |
| L-10 | PageTrashCleanup hosted service (background job) | `PageTrashCleanupHostedService`, Community 120 | Tự động hủy lịch hẹn Pending quá hạn (background job) | F_APP_06 |
| L-11 | SearchPages / ListWorkspaceTasks với pagination | `WorkTaskRepository` (Community 50, cohesion 0.13) | Tìm kiếm bệnh nhân nhanh, lọc lịch hẹn theo ngày/nha sĩ | F_PAT_02, F_APP_04 |
| L-12 | Notification unread count + mark as read | `NotificationDto`, Community 13; `GetUnreadCount` Community 63 | Thông báo nhắc lịch nội hệ thống | F_APP_07 |
| L-13 | DomainException → ExceptionMappingMiddleware → ApiErrorResponse | `ExceptionMappingMiddleware` (Community 178), `ApiErrorResponseFactory` (Community 132) | Global error handling — validate nghiệp vụ fail → 422 Unprocessable | Toàn hệ thống |
| L-14 | PageRevision (version history cho page) | `PageRevision`, Community 150 | Lịch sử thao tác trên bệnh án (AuditLog), lịch sử trạng thái răng | F_FDI_06, F_PAT_08 |

### 4.2 Ánh xạ đặc biệt: BOM Auto-Trigger ↔ Block Edit Lease

Đây là ánh xạ quan trọng nhất và tinh tế nhất:

**Block Paged:** Khi 2 user cùng edit 1 block, `RedisBlockEditLeaseService` cấp "lease" (phiếu thuê độc quyền) cho 1 user. User kia bị block. Khi lease hết hạn hoặc user release, block mở lại. Giải quyết bài toán **concurrent write**.

**Dental System:** Khi Visit hoàn tất và BOM auto-trigger chạy, cần đảm bảo:
1. Không có 2 request đồng thời cùng trigger BOM cho 1 Visit (duplicate stock issue)
2. Nếu stock không đủ, phải rollback cả Visit.Complete()

**Giải pháp ánh xạ:** Dùng `UnitOfWork` bọc `VisitService.CompleteVisit()` + `InventoryService.AutoIssueByBOM()` trong 1 transaction. Nếu cần distributed lock (nhiều server), học từ `RedisBlockEditLeaseService` pattern — nhưng MVP với 1 server dùng transaction PostgreSQL là đủ.

---

## 5. Phần không tái sử dụng — lý do loại trừ

| # | Thành phần trong Block Paged | Lý do không áp dụng |
|---|------------------------------|---------------------|
| N-01 | SignalR CollaborationHub (realtime co-editing) | Dental không có multi-user realtime edit cùng lúc |
| N-02 | Redis backplane cho SignalR | Không cần realtime collaboration |
| N-03 | Editor.js block-based editor + block CRUD API | Domain khác hoàn toàn — dental dùng FDI chart, không có rich text blocks |
| N-04 | AI Task Recommendations (`useTaskAiReminders.ts`, Community 83) | Dental không có AI feature trong scope |
| N-05 | Workspace social features (Friendship, FriendRequest, Message) | Dental không có social network |
| N-06 | WorkTask / TaskComment / TaskAssignee | Dental có Visit/TreatmentPlan riêng — khác về nghiệp vụ |
| N-07 | Page publication (public URL, SEO-friendly page share) | Dental không publish page public |
| N-08 | Cloudinary cho file storage | DL-P03 chưa quyết định — không dùng Cloudinary trừ khi DL-P03 xác nhận |
| N-09 | WorkspaceInvitation (invite via email link) | Dental dùng Admin tạo tài khoản nhân viên trực tiếp |
| N-10 | LexicographicOrderKeyGenerator (sắp xếp blocks) | Không có block ordering trong dental |
| N-11 | PagePresence / WorkspacePresence (ai đang online) | Không cần presence indicator |

**Tóm tắt loại trừ:** Toàn bộ realtime collaboration layer (SignalR, Redis, Editor.js, block lease, presence) và social/AI layer không áp dụng. Dental system đơn giản hơn về realtime nhưng phức tạp hơn về domain (FDI chart, BOM inventory, billing, roster).

---

## 6. Đề xuất cụ thể cho dự án

### 6.1 Đề xuất cập nhật tài liệu `md quan trọng/`

> **Lưu ý:** Chỉ đề xuất — không sửa trực tiếp. Người dùng quyết định từng mục.

**A1 — ARCHITECTURE_OVERVIEW.md:** Thêm mục "Kiến trúc Clean Architecture 4 layers" song song với mô tả 3-layer hiện tại. God Node `Pkm.Application.Common.UseCases` (241 edges) chứng minh kiến trúc 4-layer có thể scale tốt hơn. Thêm bảng tương ứng `Dental.Api / .Application / .Domain / .Infrastructure`.

**A2 — ARCHITECTURE_OVERVIEW.md:** Thêm mục "Abstractions Layer" — định nghĩa `ICurrentUser`, `IPasswordHasher`, `IFileStorageService`, `IEmailSender`. God Node #4 `Pkm.Application.Common.Abstractions.Authentication` (150 edges) xác nhận đây là điểm hội tụ của auth.

**A3 — ARCHITECTURE_OVERVIEW.md:** Thêm mục "Result\<T\> Pattern" — thay vì throw exception trong service, trả `Result<T>`. God Node `Result<T>` (141 edges, betweenness 0.055) cho thấy pattern này len lỏi vào mọi handler. Ví dụ:
```csharp
// Thay vì:
throw new NotFoundException("Patient not found");
// Dùng:
return Result.Failure(Error.NotFound("Patient.NotFound", "Bệnh nhân không tồn tại"));
```

**A4 — ARCHITECTURE_OVERVIEW.md:** Thêm mục "Outbox Pattern cho BOM auto-trigger" (DL-004). `VisitService.CompleteVisit()` emit event vào OutboxMessage table trong cùng transaction. `OutboxBatchProcessor` (background job) đọc và dispatch sang `InventoryService.AutoIssueByBOM()`. Đảm bảo không mất event khi server crash giữa chừng.

**A5 — AGENTS.md:** Cập nhật cấu trúc backend đề xuất:
```
Dental.Api/          Controllers/, Middleware/, Contracts/Requests/, Contracts/Responses/
Dental.Application/  Common/Abstractions/, Common/Results/, Features/[Module]/Commands/, Features/[Module]/Queries/
Dental.Domain/       Entities/, Events/, SharedKernel/
Dental.Infrastructure/ Authentication/, Persistence/, Email/, Storage/, Time/
```

**A6 — AGENTS.md:** Thêm quy tắc: "Mọi command handler trả `Result<T>`, không throw exception cho business error. Chỉ throw cho system error (DB connection, null reference)."

**A7 — AGENTS.md:** Thêm quy tắc frontend: "Mọi API call dùng composable pattern (`useXxx.ts`). Tập trung xử lý error qua `getApiErrorMessage()` helper — không xử lý rải rác trong từng component."

**A8 — DECISION_LOG.md:** Mở quyết định DL-P03 (file storage strategy). Block Paged dùng Cloudinary (Community 49, `CloudinaryFileStorageService`). Với dental system, ảnh X-quang/nội miệng (F_IMG_02) cần storage. Lựa chọn: (a) Cloudinary — có SDK, giống Block Paged; (b) Local filesystem — đơn giản hơn cho demo; (c) MinIO self-hosted. Cần xác nhận trước khi code MOD_IMG.

**A9 — DECISION_LOG.md:** Mở quyết định DL-P01 (JWT algorithm). Block Paged dùng JwtBearer 8.0.16. Recommended: RS256 (asymmetric) cho production, HS256 (symmetric) cho demo. Quyết định này ảnh hưởng `Dental.Infrastructure/Authentication/`.

**A10 — DECISION_LOG.md:** Xác nhận DL-009 Pinia. Block Paged dùng Pinia (thấy trong nhiều community: `useSidebarSettings`, `useNotificationCenter`...). Với Vue.js + Pinia đã trong stack (xlsx), cần ghi chú: 1 store per module (`appointmentStore`, `patientStore`, `visitStore`...).

**A11 — SECURITY_OVERVIEW.md:** Bổ sung "EF Core Migration workflow":
```bash
dotnet ef migrations add InitialCreate --project Dental.Infrastructure
dotnet ef database update --project Dental.Infrastructure
```
Học từ Block Paged `microsoft_entityframeworkcore` (Community 17).

**A12 — Mới — tạo file `OUTBOX_PATTERN.md`:** Tài liệu hóa cơ chế BOM auto-trigger dùng Outbox pattern (tham khảo Block Paged `Pkm.Infrastructure.Persistence.Outbox`, Community 23). Nội dung: sequence diagram từ `CompleteVisit()` → `OutboxMessage` → `AutoIssueByBOM()`, cách xử lý khi stock không đủ (rollback vs. ghi warning).

**A13 — Mới — tạo file `COMPOSABLE_GUIDE.md`:** Hướng dẫn viết Vue composable cho dental system, tham khảo `useNotificationCenter.ts` (Community 116, cohesion 0.23 — mẫu tốt nhất trong repo). Template:
```typescript
// useVisit.ts — mẫu composable
export function useVisit() {
  const visits = ref<VisitResponse[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function startVisit(patientId: string) { ... }
  async function completeVisit(visitId: string) { ... }

  return { visits, loading, error, startVisit, completeVisit }
}
```

**A14 — ARCHITECTURE_OVERVIEW.md:** Thêm ghi chú về `DataContext` trung tâm. Block Paged `DataContext` có betweenness 0.055, là DbContext chứa 31 DbSet. Dental `DentalDbContext` sẽ tương tự — cần cấu hình đầy đủ trong `Dental.Infrastructure/Persistence/`. Mỗi entity cần 1 `IEntityTypeConfiguration<T>` class riêng (học từ `FriendshipConfiguration`, `MessageReactionConfiguration`...).

### 6.2 Thứ tự ưu tiên triển khai

1. **Ngay bây giờ:** A3 (Result pattern) + A6 (AGENTS rule) — ảnh hưởng mọi handler, quyết định sớm tiết kiệm refactor sau
2. **Trước khi code backend:** A1 + A2 + A5 (kiến trúc 4-layer) + A11 (migration workflow)
3. **Trước khi code MOD_INV:** A4 + A12 (Outbox/BOM auto-trigger)
4. **Trước khi code frontend:** A7 + A13 (composable pattern)
5. **Chờ quyết định:** A8 (DL-P03 storage), A9 (DL-P01 JWT algorithm)

### 6.3 Tóm tắt giá trị thu được từ phân tích đồ thị

Graphify (6620 nodes, 19270 edges) xác nhận điều quan trọng nhất: **Block Paged không có circular dependency** — kiến trúc Clean Architecture giữ đúng dependency rule. Điều này chứng minh 4-layer không chỉ là lý thuyết mà thực sự có thể implement sạch. God Node `Result<T>` (141 edges) với betweenness bằng `Pkm.Application.Common.UseCases` (241 edges) cho thấy 2 pattern này quan trọng ngang nhau và không thể tách rời. Dental system nên adopt cả hai từ đầu.

---

*Tài liệu này là đề xuất — không thay thế quyết định của nhóm phát triển. Mọi quyết định cuối cùng cần ghi vào `md quan trọng/DECISION_LOG.md`.*
