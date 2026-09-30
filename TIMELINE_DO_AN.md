# TIMELINE_DO_AN.md — Kế hoạch làm đồ án (29/09/2026 → 30/11/2026)

> Đề tài: Hệ thống quản lý phòng khám nha khoa (Vue 3 + ASP.NET Core .NET 10 + PostgreSQL + EF Core, JWT/RBAC).
> Phương pháp: Agile/Scrum (phát triển lặp theo sprint), **mỗi sprint = 1 tuần (Thứ Hai → Chủ Nhật)**.
> Cập nhật lần cuối: 29/09/2026 (Thứ Ba).

---

## 1. Giả định (cần bạn xác nhận / chỉnh lại)

| # | Giả định | Trạng thái |
|---|---|---|
| G1 | Mốc **30/11/2026** = bản chạy được toàn bộ luồng chính, đủ demo cho GVHD (T.s Ngô Thị Ngọc Diệu) | [CẦN XÁC NHẬN] hạn nộp/bảo vệ thật của khoa |
| G2 | Quỹ thời gian dành cho đồ án: **~20–25 giờ/tuần**, có AI agent hỗ trợ viết code | [CẦN XÁC NHẬN] |
| G3 | Song song còn: NCKH sinh viên (bắt đầu **10/10/2026**, kéo đến 10/01/2027) và chứng chỉ tiếng Anh EBT (dự kiến tháng 10/2026) | [CẦN XÁC NHẬN] ngày thi EBT |
| G4 | Phạm vi: **68/72 chức năng** (4 chức năng Phase 2: F_ASS_01, F_ASS_03, F_ASS_04, F_ASS_05) | Lưu ý: `PROJECT_SCOPE.md` hiện ghi F_ASS_01 là MVP, còn timeline này chuyển F_ASS_01 sang Phase 2 để giảm tải S7 → [CẦN XÁC NHẬN] rồi sửa PROJECT_SCOPE cho khớp |
| G5 | Làm **giao diện trước, logic sau** trong từng module, nhưng dữ liệu lấy từ **seed trong DB** (không hardcode/mock trong code chạy thật — AGENTS.md 2.6) | Đã chốt |
| G6 | Quy tắc kho: **hệ thống gợi ý theo BOM, Phụ tá xác nhận thủ công** (AGENTS.md 2.5) | Đã chốt |

> ⚠️ **Nhận định thẳng:** 68 chức năng trong ~9 tuần là **rất căng** (~8–9 chức năng/tuần), lại trùng EBT và NCKH. Vì vậy mỗi chức năng được gắn mức ưu tiên **T1/T2/T3** và có **danh sách cắt giảm** ở mục 6. Nếu trễ hơn 3 ngày ở bất kỳ sprint nào → cắt theo danh sách, **không dồn nợ sang sprint sau**.

---

## 2. Các mốc quan trọng

| Ngày | Mốc |
|---|---|
| 04/10 (CN) | **M0** — Bộ khung chạy end-to-end (đăng nhập → JWT → giao diện theo vai trò) |
| 10/10 (Thứ Bảy) | Bắt đầu thực hiện NCKH (nhớ dành thời gian cho nhóm) |
| 11/10 (CN) | **M1** — Xác thực + Admin quản lý nhân viên/dịch vụ |
| 25/10 (CN) | **M2** — Luồng tiếp đón: đặt lịch → check-in → hàng đợi |
| 08/11 (CN) | **M3** — Khám lâm sàng: FDI + đơn thuốc + hồ sơ |
| 15/11 (CN) | **M4 — "Luồng vàng" chạy trọn**: đặt lịch → khám → hóa đơn → thanh toán |
| 22/11 (CN) | **M5** — Kho: gợi ý BOM + Phụ tá xác nhận + cảnh báo |
| 30/11 (Thứ Hai) | **M6 — Đóng băng (freeze) bản demo**: báo cáo + UAT + dữ liệu demo |

> Ghi chú: 10/11/2026 là ngày .NET 8/.NET 9 hết hỗ trợ — dự án dùng **.NET 10 LTS** nên không bị ảnh hưởng.

---

## 3. Lịch tổng quan theo tuần

| Sprint | Thời gian | Trọng tâm | Số chức năng |
|---|---|---|---|
| **S0** | 29/09 – 04/10 | Chốt quyết định, đồng bộ tài liệu, dựng bộ khung | 0 (hạ tầng) |
| **S1** | 05/10 – 11/10 | MOD_AUTH + MOD_MST (nhân viên, dịch vụ) | 7 |
| **S2** | 12/10 – 18/10 | MOD_PAT (hồ sơ bệnh nhân) + danh mục bệnh lý/thuốc/ghế | 9 |
| **S3** | 19/10 – 25/10 | MOD_APP + MOD_CHK (tiếp đón, hàng đợi) + F_PAT_09 | 8 |
| **S4** | 26/10 – 01/11 | Bắt đầu/kết thúc khám + MOD_FDI (phần lõi) | 6 |
| **S5** | 02/11 – 08/11 | FDI (phần còn lại) + MOD_RX + timeline/khóa hồ sơ | 9 |
| **S6** | 09/11 – 15/11 | MOD_BIL (lõi) + MOD_IMG (bản tối giản) | 9 |
| **S7** | 16/11 – 22/11 | MOD_INV + MOD_ASS (phần MVP) + MST_04 | 12 |
| **S8** | 23/11 – 29/11 | MOD_RPT + trả góp + UAT + sửa lỗi + tài liệu | 8 |
| **Freeze** | 30/11 | Đóng băng, chuẩn bị demo | — |

---

## 4. Chi tiết từng sprint

> **Định nghĩa "Xong" (Definition of Done) cho MỖI chức năng:**
> ☐ API + validate (FluentValidation) · ☐ đúng phân quyền theo `ROLE_PERMISSION_MATRIX.md` · ☐ có AuditLog nếu là thao tác nhạy cảm · ☐ giao diện dùng được · ☐ có dữ liệu seed để demo · ☐ test case UAT tương ứng đã tick Pass · ☐ ghi mục vào `HISTORY_LOG.md`

### S0 — 29/09 → 04/10: Chốt nền tảng & dựng bộ khung *(Mục tiêu: M0)*

**Chốt quyết định (ưu tiên làm ngay, khoảng 1–2 buổi):**
- [ ] Sửa các mâu thuẫn tài liệu ở **mục 9** bên dưới
- [ ] Xác nhận với GVHD: đổi công nghệ so với đề cương (ReactJS→Vue.js, Flask→ASP.NET Core, SQL Server/MySQL→PostgreSQL) — điền lý do thật vào `DL-001…003` (hiện đang `[CẦN XÁC NHẬN]`)
- [ ] Chốt: JWT HS256/30 phút (tạm), nơi lưu ảnh (DL-P03), vai trò "Chủ phòng khám" có riêng không (DL-P05)
- [ ] Hỏi GVHD ngày báo cáo tiến độ + hạn nộp thật → cập nhật G1

**Dựng bộ khung:**
- [ ] Tạo repo Git, cấu trúc `backend/`, `frontend/`, `docs/`
- [ ] Chạy **PROMPT_KHOI_TAO_DO_AN.md — Phase 1** (Backend) → tự kiểm tra theo tiêu chí
- [ ] Chạy **Phase 2** (Frontend) → tự kiểm tra
- [ ] Tự đọc lại code khung: hiểu luồng `Controller → Service → Repository`, `Result<T>`, policy, interceptor (để còn bảo vệ được với GVHD)
- [ ] Cập nhật `HISTORY_LOG.md`, `DECISION_LOG.md` (DL-010/011/012)

**✅ Xong S0 khi:** đăng nhập 5 vai trò, thấy menu đúng vai trò; 401/403 đúng; AuditLog ghi đăng nhập; không có secret trong git.

---

### S1 — 05/10 → 11/10: Xác thực & quản trị nền *(M1)*

| Chức năng | Ưu tiên |
|---|---|
| F_AUTH_02 Đăng nhập & RBAC (bổ sung **Refresh Token — httpOnly cookie**, đăng xuất) | T1 |
| F_AUTH_01 Đăng ký tài khoản bệnh nhân | T1 |
| F_AUTH_03 Hồ sơ cá nhân & đổi mật khẩu | T1 |
| F_AUTH_05 Quản lý vai trò & quyền (màn hình xem/gán) | T2 |
| F_AUTH_06 Xem Audit Log (chỉ Admin, có lọc theo entity/người dùng/ngày) | T1 |
| F_MST_07 Quản lý người dùng nhân viên (tạo/khóa, **không xóa cứng**) | T1 |
| F_MST_01 Dịch vụ & bảng giá | T1 |

- [ ] Xóa `DevController` sau khi có test phân quyền thật
- [ ] UAT: AUTH-01 → AUTH-11, SEC-01 → SEC-06 (phần đã làm)

> Sprint này nhẹ hơn về nghiệp vụ; dùng thời gian dư để **đọc kỹ và test bộ khung**. Tuần này có NCKH khởi động (10/10) → giữ khối lượng vừa phải.

---

### S2 — 12/10 → 18/10: Hồ sơ bệnh nhân

| Chức năng | Ưu tiên |
|---|---|
| F_PAT_01 Tạo hồ sơ bệnh nhân | T1 |
| F_PAT_02 Tìm kiếm nhanh (SĐT, tên một phần, có phân trang) | T1 |
| F_PAT_03 Kiểm tra trùng hồ sơ (SĐT) | T1 |
| F_PAT_04 Tiền sử bệnh & dị ứng | T1 |
| F_PAT_05 Sinh hiệu cơ bản | T2 |
| F_PAT_06 Cảnh báo tiền sử an toàn khi mở hồ sơ | T1 |
| F_MST_02 Bệnh lý/triệu chứng | T1 |
| F_MST_03 Thuốc (cần cho MOD_RX sau này) | T1 |
| F_MST_05 Phòng & ghế | T2 |

- [ ] Bệnh nhân A **không** xem được hồ sơ bệnh nhân B (kiểm ở tầng Service — data-level authorization)
- [ ] Nhập seed: ~10 dịch vụ, ~15 bệnh lý, ~15 thuốc, 3 ghế, ~10 bệnh nhân mẫu
- [ ] UAT: PAT-01 → PAT-07

> ⚠️ Tuần này có nguy cơ trùng lịch thi EBT → nếu trùng, cắt F_MST_05 và F_PAT_05 sang S3.

---

### S3 — 19/10 → 25/10: Đặt lịch, tiếp đón, hàng đợi *(M2)*

| Chức năng | Ưu tiên |
|---|---|
| F_APP_01 Đặt lịch trực tuyến (bệnh nhân tự đặt; tối đa 2 lịch Pending/ngày; đổi/hủy trước ≥ 2 giờ) | T1 |
| F_APP_02 Đặt lịch tại quầy (Lễ tân đặt hộ) | T1 |
| F_CHK_01 Check-in bệnh nhân | T1 |
| F_CHK_02 Đưa vào hàng đợi (**nội bộ, không công khai**) | T1 |
| F_CHK_03 Gọi bệnh nhân tiếp theo | T1 |
| F_CHK_07 Cập nhật trạng thái hàng đợi | T1 |
| F_CHK_08 Điều phối sang Nha sĩ / X-quang | T1 |
| F_PAT_09 Tạo bản ghi lần khám mới (không ghi đè lịch sử) | T1 |

- [ ] Trạng thái hàng đợi đúng chuỗi: Đang đợi → Đã ghi nhận thông tin sức khỏe → Đang chụp X-quang → Chờ khám nha sĩ → Đang khám → Hoàn tất
- [ ] Chặn trùng khung giờ cùng nha sĩ/ghế
- [ ] UAT: APP-01/02, CHK-01/02/03/07/08/09/10, PAT-09

---

### S4 — 26/10 → 01/11: Bắt đầu/kết thúc khám & sơ đồ răng FDI (lõi)

| Chức năng | Ưu tiên |
|---|---|
| F_CHK_04 Bắt đầu/kết thúc lần khám (một nha sĩ chỉ 1 Visit In-Progress) | T1 |
| F_CHK_05 Chuyển bệnh nhân sang nha sĩ khác | T3 |
| F_FDI_01 Hiển thị sơ đồ răng FDI (32 răng người lớn / 20 răng trẻ em) | T1 |
| F_FDI_02 Chọn răng và mặt răng | T1 |
| F_FDI_03 Gán tình trạng bệnh lý | T1 |
| F_FDI_04 Chỉ định dịch vụ theo răng | T1 |

- [ ] Xây **component sơ đồ răng** (SVG hoặc CSS grid) dùng lại được — đây là phần giao diện nặng nhất, làm sớm trong tuần
- [ ] Không cho chọn răng không tồn tại trong bộ răng của bệnh nhân
- [ ] UAT: CHK-03/04, FDI-01 → FDI-04

> **Kết thúc khám ở sprint này chỉ đổi trạng thái Visit + tạo hóa đơn nháp sẽ nối ở S6; phần gợi ý kho nối ở S7.** Tạo sẵn "điểm móc" (event/service call) nhưng chưa xử lý.

---

### S5 — 02/11 → 08/11: FDI còn lại, đơn thuốc, hồ sơ *(M3)*

| Chức năng | Ưu tiên |
|---|---|
| F_FDI_05 Cập nhật trạng thái sau điều trị | T1 |
| F_FDI_06 Lịch sử trạng thái răng | T2 |
| F_FDI_07 Ghi chú lâm sàng theo răng | T2 |
| F_RX_01 Kê đơn thuốc điện tử (chỉ thuốc trong danh mục; **không tạo lịch tái khám**) | T1 |
| F_RX_02 Kiểm tra tồn kho thuốc | T2 |
| F_RX_04 In/xuất đơn thuốc | T2 |
| F_RX_05 Lịch sử đơn thuốc | T3 |
| F_PAT_07 Timeline lịch sử điều trị | T1 |
| F_PAT_08 Khóa/chốt hồ sơ điều trị (sửa sau khóa cần quyền đặc biệt + AuditLog) | T2 |

- [ ] UAT: FDI-05, FDI-06, PAT-08, các case đơn thuốc (tự bổ sung vào `UAT_CHECKLIST.md` — hiện chưa có mục MOD_RX)

---

### S6 — 09/11 → 15/11: Thanh toán & hình ảnh *(M4 — luồng vàng)*

| Chức năng | Ưu tiên |
|---|---|
| F_BIL_01 Tạo hóa đơn nháp tự động khi kết thúc khám | T1 |
| F_BIL_02 Giảm giá/khuyến mãi (tổng không âm) | T2 |
| F_BIL_03 Xác nhận thanh toán (tiền mặt/chuyển khoản ghi nhận thủ công) | T1 |
| F_BIL_06 In/xuất biên lai (PDF) | T2 |
| F_BIL_07 Hủy/điều chỉnh hóa đơn nháp (void, có lý do, không xóa vật lý) | T2 |
| F_IMG_01 Chỉ định chụp ảnh | T3 |
| F_IMG_02 Tải ảnh X-quang/nội miệng (giới hạn định dạng + dung lượng; lưu đĩa local theo DL-P03) | T2 |
| F_IMG_03 Gắn ảnh với răng/lần khám | T3 |
| F_IMG_04 Xem/thu phóng ảnh | T3 |

- [ ] **Chạy thử trọn "luồng vàng" 3 lần liên tiếp** không lỗi (đặt lịch → check-in → hàng đợi → khám FDI → kê đơn → hóa đơn → thanh toán)
- [ ] UAT: BIL-01, 02, 03, 06, 07, 08

---

### S7 — 16/11 → 22/11: Kho & nghiệp vụ phụ tá *(M5)*

| Chức năng | Ưu tiên |
|---|---|
| F_MST_04 Danh mục vật tư | T1 |
| F_INV_01 Danh mục vật tư & thuốc kho | T1 |
| F_INV_02 Nhà cung cấp | T3 |
| F_INV_03 Phiếu nhập kho | T1 |
| F_INV_04 Quản lý lô & hạn sử dụng (không xuất lô hết hạn) | T1 |
| F_INV_05 Cấu hình BOM/định mức theo dịch vụ | T1 |
| F_INV_06 **Gợi ý** tiêu hao theo BOM khi kết thúc khám → **Phụ tá xác nhận** → mới sinh StockIssue & trừ tồn (AuditLog ghi người xác nhận) | T1 |
| F_INV_07 Tiêu hao phát sinh ngoài BOM | T2 |
| F_INV_08 Cảnh báo tồn tối thiểu & sắp hết hạn (**chỉ hiển thị**, không tự đặt hàng) | T2 |
| F_INV_09 Kiểm kê & điều chỉnh tồn | T3 |
| F_ASS_02 Ghi nhận tiêu hao tại ghế | T2 |
| F_ASS_06 Trạng thái ghế/phòng điều trị | T3 |

- [ ] Đặt transaction (giao dịch DB) bao quanh bước xác nhận: thiếu tồn → cảnh báo rõ, **không tự trừ ngầm**
- [ ] **Sửa cả tài liệu lỗi thời về INV** (DL-004, DOMAIN_MODEL, DATABASE_DESIGN `IsAutomatic`, UAT CHK-05/INV-03) cho khớp AGENTS 2.5
- [ ] UAT: INV-01 → INV-07 (cập nhật lại nội dung theo mô hình "gợi ý + xác nhận")

> Đây là **sprint nặng nhất** (12 mục). Nếu S6 trễ → cắt F_INV_02, F_INV_09, F_ASS_06 trước.

---

### S8 — 23/11 → 29/11: Báo cáo, hoàn thiện, kiểm thử

| Chức năng | Ưu tiên |
|---|---|
| F_RPT_01 Báo cáo doanh thu (chỉ tính giao dịch đã thanh toán) | T1 |
| F_RPT_02 Hiệu suất nha sĩ | T2 |
| F_RPT_04 Báo cáo xuất-nhập-tồn | T2 |
| F_RPT_05 Tiêu hao theo dịch vụ | T3 |
| F_BIL_04 Lập kế hoạch trả góp | T3 (stretch) |
| F_BIL_05 Thu từng kỳ & theo dõi công nợ | T3 (stretch) |
| F_MST_06 Trạng thái & cấu hình hệ thống | T3 |
| F_AUTH_04 Quên/khôi phục mật khẩu | T3 (stretch) |

**Hoàn thiện (làm trước các mục stretch):**
- [ ] Chạy **toàn bộ `UAT_CHECKLIST.md`** — điền Kết quả thực tế + Pass/Fail; sửa lỗi Fail
- [ ] Chạy checklist bảo mật (`SECURITY_OVERVIEW.md` mục 8): secret không nằm trong git, mật khẩu demo mạnh, HTTPS, Swagger không lộ, BN-A không xem được BN-B, Lễ tân không vào AuditLog…
- [ ] Bộ dữ liệu demo hoàn chỉnh (script seed 1 lệnh)
- [ ] Đồng bộ mọi file `docs/` với hiện trạng code (số chức năng, module, RBAC, INV)
- [ ] Hướng dẫn cài đặt & chạy trong `README.md` — thử trên máy sạch/VM

---

### 30/11 — Freeze (M6)

- [ ] Gắn tag Git `v0.9-demo`; **ngừng thêm tính năng**, chỉ sửa lỗi nghiêm trọng
- [ ] Quay video demo dự phòng (luồng vàng + phân quyền + kho)
- [ ] Soạn kịch bản demo 10–15 phút; chuẩn bị trả lời câu hỏi "vì sao đổi công nghệ so với đề cương?", "vì sao kho không trừ tự động?", "làm sao bảo vệ dữ liệu bệnh nhân?"

---

## 5. Nhịp làm việc mỗi tuần

| Ngày | Việc |
|---|---|
| **Thứ Hai** | Chọn hạng mục sprint; viết/đọc lại yêu cầu từng chức năng trong `PROJECT_SCOPE.md` + phân quyền; giao việc cho AI agent theo từng module |
| **Thứ Ba – Thứ Năm** | Mỗi chức năng: **giao diện trước → API đọc dữ liệu seed → logic nghiệp vụ → validate/phân quyền → test** |
| **Thứ Sáu** | Tự demo luồng vừa làm; tick UAT |
| **Thứ Bảy/CN** | Cập nhật `HISTORY_LOG.md`, `DECISION_LOG.md`; sửa nợ; nếu trễ → áp dụng danh sách cắt giảm; commit + tag cuối sprint |

**Mẹo dùng AI agent hiệu quả:** mỗi lần giao **1 module hoặc 1 nhóm chức năng nhỏ**, dán kèm: mã chức năng, quy tắc nghiệp vụ, dòng phân quyền liên quan, và tiêu chí "Xong" ở trên. Luôn yêu cầu agent **dán kết quả chạy test thật**, không chấp nhận "đã pass" nếu không có đầu ra.

---

## 6. Danh sách cắt giảm (khi trễ tiến độ) — cắt từ trên xuống

1. F_BIL_04, F_BIL_05 (trả góp) — chuyển Phase 2
2. F_RPT_05, F_RPT_02
3. F_IMG_01, F_IMG_03, F_IMG_04 (giữ lại F_IMG_02 tải ảnh tối giản)
4. F_INV_02, F_INV_09, F_ASS_06, F_MST_06
5. F_AUTH_04, F_RX_05, F_CHK_05

> **Không được cắt:** AUTH_02, PAT_01/02/06/09, APP_01/02, CHK_01→04, 07, 08, FDI_01→04, RX_01, BIL_01/03, INV_05/06 (dạng gợi ý + xác nhận), RPT_01, F_AUTH_06 (AuditLog). Đây là xương sống của luồng vàng.

---

## 7. Rủi ro chính

| Rủi ro | Mức | Cách giảm |
|---|---|---|
| Trùng lịch thi EBT / NCKH → mất 1–2 tuần thực chất | Cao | Xác nhận ngày thi sớm; tuần thi giảm 50% khối lượng; dùng danh sách cắt giảm |
| Sơ đồ răng FDI (giao diện) tốn thời gian hơn dự kiến | Cao | Làm component ngay đầu S4, bản đơn giản trước, đẹp sau |
| Dồn nợ sang cuối, không kịp UAT | Cao | Mỗi sprint kết thúc bằng UAT phần vừa làm; S8 chỉ chạy tổng |
| Tài liệu lệch code (đặc biệt INV) làm agent sinh sai | Trung bình | Sửa tài liệu ở S0 (mục 9) và S7 |
| Hiểu code do AI sinh không đủ để bảo vệ | Trung bình | Cuối mỗi sprint tự giải thích lại luồng chính; ghi `DECISION_LOG.md` |
| Tham khảo repo ngoài (Block Paged) | Thấp–TB | Chỉ học **pattern**, tự viết code; nếu dùng lại đoạn code thì trích nguồn hoặc xin phép tác giả repo |
| Dữ liệu bệnh nhân thật lọt vào demo | Trung bình | Chỉ dùng dữ liệu giả/ẩn danh trong seed |

---

## 8. Câu hỏi cần chốt sớm (ưu tiên trong S0)

1. Hạn nộp/bảo vệ thật của khoa? Có mốc báo cáo tiến độ giữa chừng không? (→ chỉnh lại mục 2)
2. Ngày thi EBT chính xác?
3. GVHD có chấp nhận việc đổi công nghệ so với đề cương? Cần văn bản/ghi chú gì?
4. Vai trò "Chủ phòng khám" có tách khỏi Admin không? (DL-P05)
5. Nơi lưu ảnh X-quang: đĩa local có đủ cho demo không? (DL-P03)
6. Chọn CSS: giữ Bootstrap 5 hay dùng thư viện khác? (DL-011)
7. File đặc tả chức năng hiện đã là bản v8 nhưng các file `.md` đang ghi nguồn v7 → có chức năng nào thay đổi giữa v7 và v8 không?

---

## 9. Việc đồng bộ tài liệu cần làm trong S0

| # | File | Lỗi/mâu thuẫn | Cần sửa |
|---|---|---|---|
| 1 | `PROJECT_SCOPE.md` | Mục tiêu và F_INV_06 ghi "tự động trừ kho" | Đổi thành "gợi ý theo BOM + Phụ tá xác nhận" |
| 2 | `PROJECT_SCOPE.md` | Phase 2 ghi F_APP_07 (không còn tồn tại) | Xóa; Phase 2 = F_ASS_01/03/04/05 (+ một phần F_AUTH_04) |
| 3 | `DECISION_LOG.md` | DL-004 "tự động trừ kho"; DL-002 ghi "84 chức năng" | Sửa thành gợi ý + xác nhận; 84 → 72 |
| 4 | `DOMAIN_MODEL.md` | Quy tắc #4 và quan hệ `Visit — StockIssue (1:1 automatic)`; `StockIssue.IsAutomatic` | Sửa theo mô hình xác nhận thủ công (bổ sung `ConfirmedBy`, `ConfirmedAt`) |
| 5 | `DATABASE_DESIGN.md` | `StockIssues.IsAutomatic DEFAULT TRUE`; `StockIssueItems.ItemId SERIAL` (sai tên khóa); `Roles` thiếu `RoleCode`; mới chỉ có schema ~40% số bảng đã nêu ở sơ đồ (thiếu Permissions, Drugs, Prescriptions, Attachments, Suppliers, StockReceipts…) | Sửa; bổ sung schema từng nhóm bảng **ngay trước sprint tương ứng** |
| 6 | `ARCHITECTURE_OVERVIEW.md` | Bảng lớp ghi "BOM tự động trừ kho" | Sửa thành gợi ý + xác nhận; bổ sung kiến trúc 4 project |
| 7 | `SECURITY_OVERVIEW.md` | Ghi "ma trận RBAC nhóm 15" | Nhóm 12 |
| 8 | `UAT_CHECKLIST.md` | CHK-05, INV-03 theo mô hình tự động; chưa có mục MOD_RX, MOD_IMG, MOD_ASS, MOD_MST | Sửa + bổ sung |
| 9 | `REUSE_ANALYSIS.md` | Còn Roster, 15 nhóm RBAC, F_APP_04/06/07, Outbox | Đánh dấu "lỗi thời" hoặc cập nhật |
| 10 | `README.md` (dự án) | Chưa thấy README của đồ án trong các file đã gửi (2 file README.md nhận được là của repo Block Paged và của bộ hướng dẫn Karpathy, không phải của đồ án) | Tạo README riêng cho đồ án |
| 11 | Tất cả file `docs/` | Ghi nguồn xlsx v7 nhưng file đặc tả hiện là v8 | Đối chiếu v7↔v8, cập nhật nguồn |
| 12 | `DECISION_LOG.md` | DL-001…008 ngày và lý do còn `[CẦN XÁC NHẬN]` | Điền thật trước khi gặp GVHD |

---

## 10. Theo dõi tiến độ

| Sprint | Kế hoạch | Thực tế | Trễ? | Đã cắt gì | Ghi chú |
|---|---|---|---|---|---|
| S0 | Bộ khung | | | | |
| S1 | 7 | | | | |
| S2 | 9 | | | | |
| S3 | 8 | | | | |
| S4 | 6 | | | | |
| S5 | 9 | | | | |
| S6 | 9 | | | | |
| S7 | 12 | | | | |
| S8 | 8 | | | | |
