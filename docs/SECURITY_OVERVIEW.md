# SECURITY_OVERVIEW.md — Tổng quan bảo mật

> Nguồn: File đặc tả chức năng — cột quy tắc nghiệp vụ, F_AUTH_*, F_AUTH_06  
> Stack: ASP.NET Core Web API + JWT + RBAC + PostgreSQL

---

## 1. Mô hình bảo mật tổng quan

```
┌─────────────────────────────────────────────────────────┐
│  Client (Vue.js)                                        │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Route Guard (Vue Router) — kiểm tra auth client   │ │
│  │  Axios Interceptor — đính kèm JWT vào header       │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────┬───────────────────────────────────────┘
                  │ HTTPS
                  ▼
┌─────────────────────────────────────────────────────────┐
│  ASP.NET Core Web API                                   │
│  ┌────────────────────────────────────────────────────┐ │
│  │  1. HTTPS Enforcement                              │ │
│  │  2. JWT Middleware — verify token, extract claims  │ │
│  │  3. RBAC Authorization — [Authorize(Roles="...")]  │ │
│  │  4. Input Validation (FluentValidation)            │ │
│  │  5. Global Error Handler — không lộ stack trace   │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────┬───────────────────────────────────────┘
                  │ EF Core (parameterized queries)
                  ▼
┌─────────────────────────────────────────────────────────┐
│  PostgreSQL                                             │
│  Audit Log — immutable, không xóa/sửa                  │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Xác thực (Authentication)

### 2.1 Cơ chế JWT

| Thuộc tính | Giá trị |
|---|---|
| Token type | JWT (JSON Web Token) |
| Algorithm | [CẦN XÁC NHẬN] — khuyến nghị RS256 hoặc HS256 |
| Access Token lifetime | [CẦN XÁC NHẬN] — khuyến nghị 15–60 phút |
| Refresh Token | [CẦN XÁC NHẬN] — cần implement nếu Access Token lifetime ngắn |
| Claims | `sub` (UserId), `role`, `name`, `phone`, `exp` |

**Luồng đăng nhập:**
1. Client POST `/api/auth/login` với `{phone, password}`
2. Backend kiểm tra `IsActive`, hash mật khẩu bằng bcrypt
3. Trả về `{accessToken, refreshToken, user: {name, role}}`
4. Client lưu accessToken trong **memory** (không localStorage)
5. Mọi request sau: `Authorization: Bearer <accessToken>`
6. Backend verify signature, kiểm tra `exp`, đọc claims

### 2.2 Mật khẩu

- Hash bằng **bcrypt** — không MD5, không SHA1 plain, không SHA256 plain
- Mật khẩu phải đủ độ mạnh (quy tắc cụ thể: [CẦN XÁC NHẬN])
- Khi đổi mật khẩu: phải nhập mật khẩu cũ đúng (F_AUTH_03)
- Khôi phục mật khẩu: OTP có thời hạn, dùng một lần (F_AUTH_04)

### 2.3 Tài khoản bị khóa

- `IsActive = false` → không được đăng nhập (F_AUTH_02)
- Không xóa cứng tài khoản đã có dữ liệu nghiệp vụ (F_MST_07)

---

## 3. Phân quyền (Authorization — RBAC)

Chi tiết đầy đủ → `ROLE_PERMISSION_MATRIX.md`

### 3.1 Các vai trò

`Admin` | `Nha sĩ` | `Lễ tân` | `Phụ tá` | `Bệnh nhân`

### 3.2 Triển khai trong ASP.NET Core

```csharp
// Program.cs — cấu hình JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // ...
        };
    });

builder.Services.AddAuthorization();

// Controller — áp dụng quyền
[Authorize]                          // phải đăng nhập
[Authorize(Roles = "Admin")]         // chỉ Admin
[Authorize(Roles = "Admin,Nha sĩ")]  // Admin hoặc Nha sĩ
```

### 3.3 Data-level authorization (quan trọng)

RBAC ở controller chỉ kiểm tra role. Cần kiểm tra thêm data ownership trong Service:

```csharp
// Bệnh nhân chỉ xem hồ sơ/lịch của chính mình
public async Task<PatientDto> GetMyProfile(int requestingUserId)
{
    var patient = await _repo.GetByUserId(requestingUserId);
    if (patient == null) throw new ForbiddenException();
    return _mapper.Map<PatientDto>(patient);
}
```

---

## 4. Bảo vệ dữ liệu

### 4.1 SQL Injection

- Sử dụng **EF Core LINQ** — tất cả query đều parameterized
- Không dùng raw SQL string concatenation
- Nếu cần raw SQL, dùng `FromSqlRaw` với parameterized values

### 4.2 Input Validation

- Frontend: Vue form validation (báo lỗi sớm cho user)
- Backend: **FluentValidation** hoặc Data Annotations — validate trước khi xử lý
- Không tin tưởng dữ liệu từ client

### 4.3 Sensitive Data

- Không log: password, token, số điện thoại, thông tin bệnh nhân vào console/file log
- File ảnh X-quang: phân quyền xem theo role trước khi trả URL
- HTTPS bắt buộc — không HTTP thuần

### 4.4 CORS

```csharp
// Chỉ cho phép origin của Vue.js frontend
builder.Services.AddCors(options => {
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins("http://localhost:5173")  // dev
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});
```

---

## 5. Audit & Traceability

### 5.1 AuditLog (F_AUTH_06)

Ghi nhận mọi thao tác nhạy cảm:

| Hành động | Entities cần audit |
|---|---|
| CRUD nhạy cảm | Patient, Invoice, Stock, User |
| Thay đổi quyền | Role, Permission |
| Chốt/mở hồ sơ | Visit (IsLocked) |
| Xuất kho | StockIssue |
| Điều chỉnh tồn | StockAdjustment |
| Đăng nhập thành công/thất bại | User |
| Void hóa đơn | Invoice |

### 5.2 Quy tắc AuditLog

- **Bất biến:** Không có endpoint UPDATE hay DELETE cho AuditLogs
- Chỉ Admin được đọc AuditLogs (F_AUTH_06, ma trận RBAC nhóm 15)
- Ghi cả UserId, IP, timestamp, action, entity, detail (JSONB)

---

## 6. Bảo mật API

| Biện pháp | Mô tả |
|---|---|
| Rate limiting | [CẦN XÁC NHẬN] — nên có cho `/api/auth/login` để chống brute force |
| Global error handler | Không trả stack trace cho client; log đầy đủ phía server |
| HTTP Security Headers | [CẦN XÁC NHẬN] — X-Content-Type-Options, X-Frame-Options |
| Endpoint documentation | Swagger chỉ bật trên môi trường Development |

---

## 7. Những việc KHÔNG được làm

- ❌ Không lưu JWT token vào `localStorage` (XSS risk)
- ❌ Không log mật khẩu, token, thông tin bệnh nhân
- ❌ Không trả stack trace trong response production
- ❌ Không cho Admin xóa AuditLogs
- ❌ Không dùng HTTP thuần (chỉ HTTPS)
- ❌ Không hash mật khẩu bằng MD5 hoặc SHA1
- ❌ Không xóa cứng User đã có dữ liệu (dùng IsActive = false)

---

## 8. Checklist bảo mật trước demo

- [ ] JWT secret key không hardcode trong source code (dùng environment variable)
- [ ] Mật khẩu mặc định tài khoản demo đủ mạnh, không phải `123456`
- [ ] HTTPS bật trên môi trường demo
- [ ] Swagger UI không expose sensitive endpoints
- [ ] Kiểm tra: Bệnh nhân A không xem được hồ sơ bệnh nhân B
- [ ] Kiểm tra: Lễ tân không truy cập được AuditLog
- [ ] Kiểm tra: Nha sĩ không thấy báo cáo doanh thu
- [ ] AuditLog ghi nhận khi chốt hồ sơ, xuất kho, void hóa đơn
