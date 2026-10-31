---
name: "Nha Khoa Hoàng Gia — Design System"
colors:
  primary_600: "#1857B6"
  primary_500: "#397BCC"
  primary_100: "#D8ECFA"
  primary_50: "#F4FAFF"
  navy_900: "#102E56"
  ink_800: "#18354C"
  muted_500: "#5B6B80"
  accent_gold: "#E5B95E"
  sky_300: "#A6D7EF"
  border_200: "#C8DEEB"
  surface_white: "#FFFFFF"
  canvas_bg: "#F4F8FC"
  clinical_danger: "#DC2626"
  clinical_warning: "#B45309"
  clinical_safe: "#15803D"
  clinical_endodontic: "#7C3AED"
---

# Design System: Hệ Thống Quản Lý Phòng Khám Nha Khoa Hoàng Gia (PK_NK_HG)

## 1. Visual Theme & Atmosphere

Hệ thống Quản lý Phòng khám Nha khoa Hoàng Gia mang ngôn ngữ thiết kế **"Clinical Royal Precision"** — kết hợp sự chuẩn xác, minh bạch của phần mềm y tế lâm sàng với nét sang trọng, tin cậy từ bộ nhận diện thương hiệu Hoàng Gia (`frontend/public`). Không gian giao diện thoáng đãng trên nền xanh băng nhạt (`#F4F8FC`), nổi bật các vùng làm việc trắng tinh khiết (`#FFFFFF`) và điểm nhấn thương hiệu Xanh Hoàng Gia (`#1857B6`) phối cùng Vàng Vương Miện (`#E5B95E`).

Mật độ hiển thị được cân chỉnh theo từng vai trò trong 5 nhóm người dùng: cổng Bệnh nhân và trang Công khai ưu tiên sự nhẹ nhàng, dễ thao tác trên điện thoại di động (`320px–390px`); trong khi bàn làm việc Lâm sàng của Nha sĩ, Phụ tá và Bàn tiếp đón Lễ tân/Thu ngân được thiết kế theo bố cục điều phối (Cockpit Balanced) giúp quan sát đồng thời **Banner Cảnh báo An toàn (`F_PAT_06`)**, **Sơ đồ răng giải phẫu FDI 32/20 răng (`F_FDI_01`)** và **Tiến trình hàng đợi (`F_CHK_02`)** mà không gây quá tải thị giác.

---

## 2. Color Palette & Roles

### Primary Foundation (Nền tảng thương hiệu từ `frontend/public`)
- **Ice Clinical Canvas** (`#F4F8FC`) — Nền tổng thể của ứng dụng, giảm mỏi mắt khi bác sĩ và lễ tân làm việc liên tục nhiều giờ.
- **Hero Sky Tint** (`#F4FAFF`) — Nền vùng giới thiệu và thẻ nổi bật nhẹ.
- **Pure Clinical Surface** (`#FFFFFF`) — Bề mặt thẻ (Card), bảng dữ liệu, hộp thoại (Modal) và phiếu in.
- **Royal Dental Blue** (`#1857B6`) — Màu chủ đạo thương hiệu cho nút hành động chính (Primary CTA), trạng thái đang chọn trên sơ đồ răng FDI và mục điều hướng đang mở.
- **Cerulean Hover Blue** (`#397BCC`) — Màu chuyển tiếp khi di chuột, viền tập trung (focus ring) và biểu đồ doanh thu.
- **Soft Sky Highlight** (`#D8ECFA`) — Nền huy hiệu (Badge), vùng phân tư hàm răng FDI và trạng thái hover nhẹ.

### Accent & Interactive (Điểm nhấn tương tác)
- **Royal Crown Gold** (`#E5B95E`) — Điểm nhấn thương hiệu từ biểu tượng vương miện trên logo, dùng cho nút kêu gọi đặt lịch ("Đặt lịch ngay"), trạng thái chờ X-quang/khử khuẩn và biểu diễn **Mão răng sứ (`CRN`)** trên sơ đồ FDI.
- **Warm Gold Wash** (`#FBF1DC`) — Nền cảnh báo nhẹ và huy hiệu vàng.

### Typography & Text Hierarchy
- **Deep Royal Navy** (`#102E56`) — Màu chữ tiêu đề chính (`H1`–`H3`), thanh bên (Sidebar) nhân viên và nền màn hình TV phòng chờ.
- **Clinical Slate Ink** (`#18354C`) — Màu chữ nội dung chính (Body text), đảm bảo độ tương phản chuẩn AAA.
- **Muted Steel Blue** (`#5B6B80`) — Nhãn phụ, chú thích thời gian, đơn vị tính và gợi ý nhập liệu.
- **Structural Ice Border** (`#C8DEEB` / `#E4EDF5`) — Đường kẻ chia bảng, viền ô nhập liệu và đường phân cách dọc/ngang giữa 4 phần tư hàm răng.

### Functional & Clinical States (Màu nghiệp vụ lâm sàng & FDI)
- **High-Risk Allergy / Caries Red** (`#DC2626`, nền `#FEE2E2`) — Cảnh báo dị ứng thuốc, huyết áp vượt ngưỡng trên `SafetyAlertBanner`, và vị trí **Sâu răng (`CAR`)** trên thân răng giải phẫu.
- **Medical History / Debt Amber** (`#B45309`, nền `#FEF3C7`) — Cảnh báo bệnh nền, nhắc công nợ khi tiếp đón (`F_CHK_01`) và vật tư chạm ngưỡng tồn tối thiểu (`F_INV_03`).
- **Sterile Safe Emerald** (`#15803D`, nền `#DCFCE7`) — Trạng thái ghế sẵn sàng (`F_ASS_06`), hoàn tất ca khám, đã thanh toán đủ.
- **Endodontic Violet** (`#7C3AED`, nền `#EDE9FE`) — Trạng thái đang khám và đường ống tủy trên răng **Điều trị tủy (`RCT`)**.
- **Restoration Blue** (`#3B82F6`) — Miếng trám trên thân răng **Đã trám (`FIL`)**.
- **Titanium Implant Cyan** (`#0EA5E9`) — Trụ vít ren Titan thay thế chân răng cho tình trạng **Cấy ghép trụ răng (`IMP`)**.

---

## 3. Typography Rules

### Hierarchy & Weights
- **Font Stack:** `"Be Vietnam Pro", "Inter", "Segoe UI", system-ui, sans-serif` — Tối ưu dấu tiếng Việt rõ nét ở mọi kích cỡ; mã chức năng, số thứ tự hàng đợi và khổ in nhiệt K80 dùng `ui-monospace, Consolas, monospace`.
- **Display / TV Callout:** `32px–120px`, `font-weight: 800–900` — Dành riêng cho màn hình TV phòng chờ (`1920×1080`) để đọc rõ từ khoảng cách 3–5m.
- **Heading 1 (Tiêu đề trang):** `24px` (Mobile `20px`), `font-weight: 700`, `line-height: 1.25`, màu `#102E56`.
- **Heading 2 (Tiêu đề phân vùng):** `19px`, `font-weight: 700`, màu `#102E56`.
- **Heading 3 (Tiêu đề thẻ / Modal):** `16px`, `font-weight: 600`, màu `#102E56`.
- **Body Regular:** `14px`, `line-height: 1.5`, màu `#18354C`.
- **Small / Caption / Helper:** `12px–12.5px`, màu `#5B6B80`.
- **Tabular Numbers:** Mọi con số tiền tệ VND (`font-variant-numeric: tabular-nums`), huyết áp, số lượng tồn kho và số hiệu răng FDI đều căn thẳng hàng.

---

## 4. Component Stylings

### Buttons
- Bo góc `10px` (`8px` cho nút nhỏ `btn-sm`), đệm `9px 16px`, chữ đậm `600`.
- Nút CTA đặt lịch dùng bo tròn viên thuốc (`border-radius: 999px`) nền Vàng `#E5B95E`, chữ Navy `#102E56`.

### Cards & Containers
- Bo góc `14px`, viền mảnh `1px solid #E4EDF5`, bóng đổ khuếch tán nhẹ `0 1px 2px rgba(16,46,86,.06), 0 6px 20px rgba(16,46,86,.06)`.

### Domain-Specific Components
1. **`SafetyAlertBanner` (`F_PAT_06`):** Khối cảnh báo viền trái dày `6px solid #DC2626`, nền `#FEE2E2`, ghim cố định phía trên hồ sơ khám của Nha sĩ, hiển thị rõ Dị ứng thuốc, Bệnh nền và Huyết áp đo trong ngày.
2. **Sơ đồ răng giải phẫu FDI (`FdiDentalChart` & `FdiTooth` — `F_FDI_01..07`):**
   - Hiển thị 32 răng vĩnh viễn (`11–48`) hoặc 20 răng sữa trẻ em (`51–85`) chia 4 phần tư hàm đúng chuẩn FDI.
   - Mỗi vị trí răng kết hợp **hình răng giải phẫu SVG** (răng cửa 1 chân hình xẻng, răng nanh 1 chân nhọn, răng tiền hàm 2 chân, răng hàm lớn 2–3 chân nhiều múi; hàm trên chân hướng lên, hàm dưới chân hướng xuống) và **sơ đồ 5 mặt răng** (`Ngoài B`, `Trong L`, `Gần M`, `Xa D`, `Nhai O`).
3. **Bản in Đơn thuốc & Biên lai (`A5` & `K80`):** Khung giấy trắng mô phỏng đúng kích thước thực tế kèm thông tin đầu phiếu phòng khám, mã QR tra cứu và chữ ký bác sĩ.

---

## 5. Layout Principles & Responsive Behavior

- **Desktop Staff Shell (`1440px`):** Lưới 2 cột cố định thanh bên trái `264px` (`#102E56`) + vùng nội dung chính linh hoạt có thanh công cụ trên cùng cao `64px`.
- **Patient Portal & Public (`max-width: 1200px`):** Thanh điều hướng ngang phía trên; tự động chuyển thành **Bottom Navigation Bar 5 mục** cố định cạnh dưới khi mở trên điện thoại (`≤ 640px`).
- **Chuẩn Mobile (`320px–390px`):** Tất cả lưới nhiều cột (`g2`, `g3`, `g4`, `g-side`) tự động thu về 1 cột dọc; bảng dữ liệu và sơ đồ răng FDI hỗ trợ cuộn ngang mượt nội bộ mà không làm tràn ngang toàn trang.
