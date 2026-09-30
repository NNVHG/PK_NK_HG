# DECISION_LOG.md — Nhật ký quyết định kỹ thuật

> Ghi lại các quyết định kỹ thuật quan trọng: lý do, lựa chọn đã xem xét, và tác động.  
> Cần đọc khi báo cáo với giảng viên hướng dẫn hoặc hội đồng bảo vệ.

---

## Định dạng mỗi quyết định

```
**ID:** DL-NNN
**Ngày:** YYYY-MM-DD
**Quyết định:** Tóm tắt một câu
**Lựa chọn:** Đã chọn gì
**Thay thế đã xem xét:** Những gì đã cân nhắc
**Lý do:** Tại sao chọn cái này
**Tác động:** Ảnh hưởng đến phần nào của dự án
**Trạng thái:** Đã chốt / Đang xem xét / Đã thay đổi
```

---

## DL-001 — Chọn Vue.js thay vì ReactJS

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Frontend dùng Vue.js, không phải ReactJS như đề cương gốc  
**Lựa chọn:** Vue.js  
**Thay thế đã xem xét:** ReactJS (đề cương tốt nghiệp ban đầu ghi ReactJS)  
**Lý do:**
- [CẦN XÁC NHẬN] — ghi rõ lý do thực tế khi báo cáo với GVHD
- Gợi ý hướng giải thích: Vue.js có learning curve thấp hơn, Composition API tương tự React Hooks, hệ sinh thái phù hợp với quy mô đồ án
- File đặc tả chức năng (xlsx) ghi rõ Vue.js là công nghệ được chọn  

**Tác động:** Toàn bộ frontend, cấu trúc thư mục, state management (Pinia thay Zustand/Redux)  
**Trạng thái:** Đã chốt (theo file xlsx)

---

## DL-002 — Chọn ASP.NET Core Web API thay vì Python Flask

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Backend dùng ASP.NET Core, không phải Flask  
**Lựa chọn:** ASP.NET Core Web API  
**Thay thế đã xem xét:** Python Flask (đề cương gốc liệt kê cả hai)  
**Lý do:**
- [CẦN XÁC NHẬN] — ghi rõ khi báo cáo
- Gợi ý: ASP.NET Core có built-in support mạnh cho JWT, Identity, middleware pipeline; strongly typed, phù hợp với PostgreSQL qua EF Core; Entity Framework Core cung cấp migration workflow rõ ràng
- Flask phù hợp prototype nhỏ; ASP.NET Core phù hợp hơn cho hệ thống có 84 chức năng  

**Tác động:** Backend architecture, ORM choice, auth implementation  
**Trạng thái:** Đã chốt (theo file xlsx)

---

## DL-003 — Chọn PostgreSQL thay vì SQL Server / MySQL

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Database dùng PostgreSQL  
**Lựa chọn:** PostgreSQL  
**Thay thế đã xem xét:** SQL Server, MySQL (đề cương gốc ghi "SQL Server/MySQL")  
**Lý do:**
- [CẦN XÁC NHẬN] — ghi rõ khi báo cáo
- Gợi ý: PostgreSQL miễn phí hoàn toàn (không cần license SQL Server), hỗ trợ JSONB (dùng cho AuditLog detail), hiệu năng tốt, EF Core hỗ trợ đầy đủ qua Npgsql, phổ biến trong ngành  

**Tác động:** Database setup, connection string, EF Core provider (Npgsql)  
**Trạng thái:** Đã chốt (theo file xlsx)

---

## DL-004 — Tự động trừ kho theo BOM khi hoàn tất dịch vụ

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Khi Visit kết thúc, hệ thống tự động sinh StockIssue theo BOM đã cấu hình  
**Lựa chọn:** Trigger tự động trong Service layer (không phải DB trigger)  
**Thay thế đã xem xét:** Database trigger, thủ công do phụ tá nhập  
**Lý do:**
- Yêu cầu nghiệp vụ rõ ràng: F_INV_06 — "Sinh phiếu xuất kho nội bộ khi dịch vụ hoàn tất"
- Xử lý trong Service layer giúp kiểm soát exception, cảnh báo thiếu kho trước khi commit
- Tránh DB trigger vì khó debug, khó test  

**Tác động:** VisitService.CompleteVisit(), InventoryService.AutoIssueByBOM(), quy trình F_CHK_04  
**Trạng thái:** Đã chốt

---

## DL-005 — Không tích hợp BHYT

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Không tích hợp bảo hiểm y tế (BHYT) trong phạm vi đồ án  
**Lựa chọn:** Bỏ hoàn toàn  
**Lý do:**
- Tích hợp BHYT đòi hỏi kết nối Cổng Bảo hiểm Xã hội, quy định phức tạp, không phù hợp quy mô đồ án
- Phòng khám nha khoa tư nhân thường không áp dụng BHYT cho phần lớn dịch vụ
- Được nêu rõ trong tài liệu đề cương là ngoài phạm vi  

**Tác động:** Không có module BHYT, không cần mã ICD, không cần kết nối VSSID  
**Trạng thái:** Đã chốt — không thay đổi

---

## DL-006 — Không kết nối thiết bị DICOM

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** MOD_IMG chỉ upload file ảnh thủ công, không kết nối máy X-quang DICOM  
**Lựa chọn:** Upload file thủ công (jpg, png, v.v.)  
**Thay thế đã xem xét:** Kết nối PACS/DICOM  
**Lý do:**
- Kết nối DICOM cần driver, protocol phức tạp, thiết bị vật lý — ngoài phạm vi đồ án
- Nêu rõ trong tài liệu: "Chỉ lưu chỉ định; không điều khiển thiết bị" (F_IMG_01), "Không DICOM" (F_IMG_02)  

**Tác động:** MOD_IMG — chỉ lưu file + metadata, không phân tích ảnh AI  
**Trạng thái:** Đã chốt — không thay đổi

---

## DL-007 — Dùng tiêu chuẩn FDI cho sơ đồ răng

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Sơ đồ răng sử dụng hệ thống FDI (Two-Digit System), không dùng Universal hay Palmer  
**Lựa chọn:** FDI (ISO 3950)  
**Thay thế đã xem xét:** Universal Numbering System (Mỹ), Palmer Notation  
**Lý do:**
- FDI là tiêu chuẩn quốc tế được sử dụng phổ biến tại Việt Nam
- Rõ ràng, phân biệt rõ răng người lớn (2 chữ số 11-48) và răng sữa trẻ em (51-85)
- Nêu rõ trong F_FDI_01: "Đánh số đúng FDI"  

**Tác động:** Toàn bộ MOD_FDI, bảng Teeth, ToothConditions, schema FDICode  
**Trạng thái:** Đã chốt

---

## DL-008 — Chọn Entity Framework Core (Code First) thay vì Dapper

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** ORM dùng EF Core Code First  
**Lựa chọn:** Entity Framework Core  
**Thay thế đã xem xét:** Dapper (micro-ORM)  
**Lý do:**
- EF Core cung cấp Migration workflow rõ ràng — phù hợp đồ án đang phát triển schema
- Code First giúp thiết kế từ C# entities, dễ maintain hơn so với SQL-first
- Dapper tốt hơn về performance nhưng đòi hỏi viết SQL thủ công — không phù hợp timeline đồ án
- Quy tắc trong AGENTS.md: ưu tiên EF Core LINQ thay raw SQL  

**Tác động:** Toàn bộ Data Access Layer, cấu trúc Repositories  
**Trạng thái:** Đã chốt (theo file xlsx)

---

## DL-009 — Pinia làm State Management cho Vue.js

**Ngày:** [CẦN XÁC NHẬN]  
**Quyết định:** Dùng Pinia thay vì Vuex  
**Lựa chọn:** Pinia  
**Thay thế đã xem xét:** Vuex 4  
**Lý do:**
- Pinia là state management chính thức được Vue core team khuyến nghị cho Vue 3
- API đơn giản hơn Vuex, không cần mutations
- Tốt hơn với TypeScript  

**Tác động:** Frontend stores, cách quản lý auth state và cache dữ liệu  
**Trạng thái:** Đề xuất — [CẦN XÁC NHẬN] nếu đã chốt

---

## Quyết định đang chờ xác nhận

| ID | Vấn đề | Deadline xác nhận |
|---|---|---|
| DL-P01 | JWT algorithm (RS256 hay HS256) | Trước khi implement auth |
| DL-P02 | Access Token lifetime (15 hay 60 phút) | Trước khi implement auth |
| DL-P03 | Storage strategy cho file ảnh (local disk / cloud) | Trước khi implement MOD_IMG |
| DL-P04 | Quy tắc mật khẩu mạnh (độ dài tối thiểu, ký tự đặc biệt) | Trước khi implement F_AUTH_01 |
| DL-P05 | Vai trò "Chủ phòng khám" có riêng biệt với Admin không | Trước khi implement RBAC |
| DL-P06 | Môi trường demo/UAT (localhost hay hosting cloud) | Trước ngày demo |
