# ARCHITECTURE_OVERVIEW.md — Kiến trúc hệ thống

> Nguồn: Đề cương tốt nghiệp + File đặc tả chức năng

---

## 1. Tổng quan kiến trúc

Hệ thống theo mô hình **Client–Server** với frontend Vue.js giao tiếp qua REST API với backend ASP.NET Core. Database PostgreSQL lưu toàn bộ dữ liệu nghiệp vụ.

```
┌─────────────────────────────────────────────────────────┐
│                     CLIENT SIDE                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │          Vue.js SPA (Single Page Application)     │  │
│  │  Vue Router │ Pinia Store │ Axios HTTP Client     │  │
│  └───────────────────────────────────────────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS / REST API (JSON)
                         │ JWT Bearer Token
┌────────────────────────▼────────────────────────────────┐
│                    SERVER SIDE                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │        ASP.NET Core Web API                       │  │
│  │  Controllers → Services → Repositories            │  │
│  │  Middleware: JWT Auth │ RBAC │ Error Handling      │  │
│  └───────────────────────────────────────────────────┘  │
│                         │                               │
│  ┌──────────────────────▼────────────────────────────┐  │
│  │     Entity Framework Core (ORM)                   │  │
│  └──────────────────────┬────────────────────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                   DATA LAYER                            │
│  ┌───────────────────────────────────────────────────┐  │
│  │          PostgreSQL Database                      │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Lớp kiến trúc backend (Layered Architecture)

```
Controllers (API Layer)
    │  Nhận HTTP request, validate input, gọi Service
    ▼
Services (Business Logic Layer)
    │  Xử lý nghiệp vụ, quy tắc, orchestration
    ▼
Repositories (Data Access Layer)
    │  CRUD qua Entity Framework Core
    ▼
PostgreSQL (Database)
```

### Chi tiết các lớp

| Lớp | Trách nhiệm | Công nghệ |
|---|---|---|
| Controllers | Route HTTP, validate DTO, trả response | ASP.NET Core MVC |
| Services | Business logic, BOM tự động trừ kho, validate nghiệp vụ | C# |
| Repositories | Truy vấn DB, Unit of Work pattern | EF Core |
| Domain Models | Entities ánh xạ với PostgreSQL tables | EF Core Code First |
| DTOs | Request/Response objects, tách biệt domain và API | C# Records |
| Middleware | JWT auth, RBAC authorization, global error handling | ASP.NET Core Pipeline |

---

## 3. Lớp kiến trúc frontend

```
Views (Màn hình theo module)
    │  Hiển thị dữ liệu, nhận user input
    ▼
Components (UI tái sử dụng)
    │  Form, Table, Modal, Chart components
    ▼
Stores (Pinia)
    │  Global state, auth state, cache dữ liệu dùng chung
    ▼
Services/API Layer (Axios)
    │  HTTP calls, interceptor thêm JWT header
    ▼
Vue Router
    │  Route guard kiểm tra auth trước khi vào page
```

---

## 4. Luồng xác thực (Authentication Flow)

```
1. User nhập SĐT/mã + mật khẩu
2. POST /api/auth/login
3. Backend validate, trả JWT Access Token + Refresh Token
4. Frontend lưu token (memory + httpOnly cookie)
5. Mọi request sau đính kèm: Authorization: Bearer <token>
6. Backend middleware verify JWT, load user role từ claims
7. [Authorize(Roles = "...")] kiểm tra quyền trước controller action
```

---

## 5. Quy trình nghiệp vụ cốt lõi (Core Workflows)

### 5.1 Quy trình khám bệnh

```
Đặt lịch 2 kênh (APP)
    ├── Bệnh nhân tự đặt trực tuyến (F_APP_01)
    └── Lễ tân đặt hộ tại quầy (F_APP_02)
    → Check-in bệnh nhân (F_CHK_01)
    → Đưa vào hàng đợi chung — nội bộ, không công khai (F_CHK_02)
    → Gọi bệnh nhân tiếp theo (F_CHK_03)
    → Ghi nhận tiền sử & sinh hiệu (F_PAT_04, F_PAT_05)
    → Tạo bản ghi lần khám mới — không ghi đè lịch sử (F_PAT_09)
    → Cập nhật trạng thái hàng đợi (F_CHK_07)
    → Điều phối: sang Nha sĩ hoặc X-quang (F_CHK_08)
    │
    ├── [X-quang] Chụp & tải ảnh (IMG)
    │
    └── [Khám nha sĩ]
            → Bắt đầu/kết thúc lần khám (F_CHK_04)
            → Ghi hồ sơ FDI (FDI)
            → Kê đơn thuốc (RX — không tạo lịch tái khám)
            → Tạo hóa đơn nháp tự động (BIL)
            → Hệ thống gợi ý tiêu hao theo BOM (INV) — Phụ tá xác nhận ← quan trọng
    → Thu ngân xác nhận thanh toán (BIL)
```

**Ghi chú:**
- Hàng đợi chỉ nhân viên thấy; không có màn hình công khai ngoài sảnh.
- Mỗi lần khám tạo bản ghi Visit mới — không ghi đè lịch sử cũ.
- Không có bước hẹn tái khám trong quy trình; nha sĩ kê đơn thuốc trực tiếp.

### 5.2 Quy trình xuất kho sau khám (gợi ý BOM + xác nhận thủ công)

```
Kết thúc khám (F_CHK_04)
    → Hệ thống đọc BOM cấu hình cho từng dịch vụ (F_INV_05)
    → Kiểm tra tồn kho đủ không
    → Nếu thiếu: cảnh báo nhân viên (không chặn hoàn toàn — nhân viên quyết định)
    → Hiển thị danh sách vật tư tiêu hao DỰ KIẾN theo BOM cho Phụ tá xem
    → Phụ tá xem lại, điều chỉnh số lượng thực tế nếu cần
    → Phụ tá bấm XÁC NHẬN → hệ thống sinh StockIssue, trừ tồn
    → Ghi Audit Log
```

> **Lưu ý:** Không có trừ kho hoàn toàn tự động. Mọi xuất kho đều cần Phụ tá (hoặc Admin) xác nhận thủ công. Hệ thống chỉ hỗ trợ bằng cách gợi ý dựa trên BOM.

---

## 6. Giao tiếp giữa các thành phần

| Kênh | Công nghệ | Ghi chú |
|---|---|---|
| Frontend ↔ Backend | REST API / HTTPS | JSON request/response |
| Authentication | JWT Bearer Token | Stateless |
| Backend ↔ Database | Entity Framework Core | Code First Migration |
| File upload (ảnh) | Multipart form data | Lưu local file system hoặc [CẦN XÁC NHẬN: storage strategy] |
| Notification (Phase 2) | [CẦN XÁC NHẬN] | MVP: in-app notification |

---

## 7. Môi trường triển khai (đề xuất cho đồ án)

| Môi trường | Mô tả |
|---|---|
| Development | Local machine: Vue dev server + ASP.NET Core dev + PostgreSQL local |
| Demo/UAT | [CẦN XÁC NHẬN] — có thể là localhost hoặc cloud miễn phí |
| Production | Ngoài phạm vi đồ án |

---

## 8. Các quyết định kiến trúc quan trọng

> Chi tiết lý do → xem `DECISION_LOG.md`

| Quyết định | Lựa chọn | Thay thế đã xem xét |
|---|---|---|
| Frontend framework | Vue.js | ReactJS (trong đề cương) |
| Backend framework | ASP.NET Core Web API | Python Flask (trong đề cương) |
| Database | PostgreSQL | SQL Server, MySQL (trong đề cương) |
| ORM | Entity Framework Core | Dapper |
| State management | Pinia | Vuex |
| Auth mechanism | JWT / RBAC | Session-based |
