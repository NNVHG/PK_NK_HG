# AGENTS.md — Quy tắc cho AI Coding Agent

> File này định nghĩa các quy tắc bắt buộc khi AI assistant (GitHub Copilot, Claude, Cursor, v.v.) tham gia viết code cho dự án này.  
> **Đọc file này TRƯỚC khi thực hiện bất kỳ thay đổi nào.**

---

## 1. Ngữ cảnh dự án

Đây là **đồ án tốt nghiệp** — hệ thống quản lý phòng khám nha khoa đơn lẻ (không phải enterprise SaaS). Mọi giải pháp phải phù hợp với quy mô đồ án sinh viên: đơn giản, rõ ràng, có thể demo được.

**Tech stack chính thức:**
- Frontend: **Vue.js**
- Backend: **ASP.NET Core Web API**
- Database: **PostgreSQL** + **Entity Framework Core**
- Auth: **JWT / RBAC**

---

## 2. Quy tắc bắt buộc

### 2.1 Không tự ý thêm công nghệ

- Không thêm thư viện/package nào chưa được xác nhận trong tài liệu
- Không chuyển đổi giữa các ORM hoặc database mà không có lệnh rõ ràng
- Không tự ý chọn giữa ASP.NET Core và Flask — **đã chốt ASP.NET Core**

### 2.2 Tuân thủ phân quyền RBAC

Trước khi viết bất kỳ endpoint hoặc component nào, kiểm tra ma trận trong `docs/ROLE_PERMISSION_MATRIX.md`.

Các vai trò: `Admin`, `Lễ tân/Thu ngân`, `Nha sĩ`, `Phụ tá`, `Bệnh nhân`

### 2.3 Đặt tên theo quy ước module

Sử dụng mã module làm prefix khi đặt tên:
```
MOD_AUTH, MOD_PAT, MOD_APP, MOD_CHK, MOD_FDI,
MOD_IMG, MOD_RX, MOD_BIL, MOD_INV,
MOD_ASS, MOD_MST, MOD_RPT
```

Ví dụ: function F_CHK_01 thuộc MOD_CHK, endpoint `/api/checkin/...`

### 2.4 Tiêu chuẩn FDI

- Bộ răng người lớn: 32 răng (ký hiệu FDI: 11–18, 21–28, 31–38, 41–48)
- Bộ răng trẻ em: 20 răng (ký hiệu FDI: 51–55, 61–65, 71–75, 81–85)
- Không dùng hệ thống Universal (Mỹ) hay Palmer
- Mỗi răng có thể có nhiều mặt (buccal, lingual, mesial, distal, occlusal)

### 2.5 Quy tắc MOD_INV — Kho hỗ trợ/cảnh báo, không tự động hoàn toàn

- Khi kết thúc khám, hệ thống **gợi ý** danh sách vật tư tiêu hao dựa trên BOM (F_INV_05)
- Không implement logic tự động trừ kho mà không có xác nhận người dùng
- Phụ tá (hoặc Admin) phải bấm **xác nhận** trên UI trước khi hệ thống sinh StockIssue và trừ tồn kho
- Cảnh báo tồn kho (F_INV_08) chỉ hiển thị thông báo — không tự động đặt hàng hay điều chỉnh tồn
- Mọi thay đổi tồn kho phải có audit log ghi rõ người xác nhận

### 2.6 Không tạo dữ liệu giả (mock data) trong production code

- Mock data chỉ dùng trong file test/seed
- API phải trả lỗi rõ ràng, không trả hardcoded response

---

## 3. Quy tắc ghi `[CẦN XÁC NHẬN]`

Nếu có thông tin chưa rõ ràng trong tài liệu, **không tự quyết định** — ghi comment:

```csharp
// [CẦN XÁC NHẬN] Chưa rõ logic tính giá khi áp dụng đồng thời nhiều khuyến mãi
```

```vue
<!-- [CẦN XÁC NHẬN] Thiết kế màn hình này chưa có wireframe -->
```

---

## 4. Cấu trúc thư mục backend (đề xuất)

```text
/backend
├── Controllers/         ← API endpoints (theo module)
├── Services/            ← Business logic
├── Repositories/        ← Data access (EF Core)
├── Models/              ← Domain entities
├── DTOs/                ← Request/Response objects
├── Middleware/          ← Auth, error handling
└── Migrations/          ← EF Core migrations
```

## 5. Cấu trúc thư mục frontend (đề xuất)

```text
/frontend
├── src/
│   ├── views/           ← Màn hình theo module
│   ├── components/      ← UI components tái sử dụng
│   ├── stores/          ← Pinia state management
│   ├── services/        ← API calls
│   └── router/          ← Vue Router
```

---

## 6. Security rules

- Mọi endpoint phải có `[Authorize]` attribute trừ `/auth/login` và `/auth/register`
- Validate input ở cả frontend (Vue) và backend (ASP.NET Core FluentValidation)
- Không log sensitive data (password, token, thông tin bệnh nhân) vào console/log file
- Mật khẩu phải hash bằng bcrypt — không MD5, không SHA1 plain

---

## 7. Những việc AI KHÔNG được làm

- ❌ Không xóa migration đã chạy
- ❌ Không thay đổi schema database mà không tạo migration mới
- ❌ Không viết SQL raw khi có thể dùng EF Core LINQ
- ❌ Không tự thêm module ngoài 12 module đã xác định
- ❌ Không tích hợp BHYT hoặc DICOM (ngoài phạm vi đồ án)
- ❌ Không implement MOD_ROS (quản lý ca làm việc/lịch trực) — đã loại khỏi phạm vi đồ án

---

## 8. Quy tắc ghi lịch sử làm việc (HISTORY_LOG)

Sau **mỗi lần trao đổi có dẫn đến thay đổi thực tế** trên code hoặc tài liệu (tạo file, sửa file, xóa file), AI agent **phải tự động ghi một mục mới** vào file `HISTORY_LOG.md` (cùng cấp với file này).

**Không cần ghi** các câu hỏi thuần thông tin không dẫn đến hành động nào.

### Định dạng bắt buộc mỗi mục:

```markdown
### [YYYY-MM-DD HH:MM] - [Tiêu đề ngắn gọn của việc đã làm]

- **Câu hỏi/yêu cầu:** [Tóm tắt ngắn gọn điều người dùng hỏi/yêu cầu]
- **Trả lời/đề xuất:** [Tóm tắt ngắn gọn hướng AI đã đề xuất]
- **Hành động đã thực hiện:** [Liệt kê cụ thể file nào đã tạo/sửa/xóa]
```

### Quy tắc bổ sung:

- Luôn **THÊM VÀO CUỐI** file (append) — không xóa hoặc ghi đè lịch sử cũ
- Ghi đúng ngày giờ thực tế khi thực hiện thay đổi
- Mỗi phiên làm việc có thể có nhiều mục, mỗi mục tương ứng một nhóm thay đổi có liên quan
