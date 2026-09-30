# FULL TEST CHECKLIST — DANH MỤC KIỂM THỬ TOÀN DIỆN 72 CHỨC NĂNG

> Đồ án: Hệ thống quản lý phòng khám nha khoa Hoàng Gia (PK_NK_HG)
> Quy chuẩn: Agile/Scrum, FDI 32/20, BOM gợi ý kho thủ công, RBAC 5 vai trò
> Cấu trúc checklist: Happy Path, Validation, Boundary, Duplicate, State Machine, RBAC, Ownership/IDOR, Test Data, Status.

## MOD_AUTH — Patient Registration (6 chức năng)

### `F_AUTH_01`: Đăng ký tài khoản bệnh nhân
- **Vai trò áp dụng:** Bệnh nhân
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tạo tài khoản để sử dụng hệ thống và theo dõi thông tin cá nhân.
- **Dữ liệu đầu vào:** Họ tên, SĐT, mật khẩu, ngày sinh, giới tính, email
- **Kết quả đầu ra:** Tài khoản mới
- **Quy tắc nghiệp vụ:** SĐT duy nhất; mật khẩu đủ độ mạnh.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Họ tên, SĐT, mật khẩu, ngày sinh, giới tính, email) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Bệnh nhân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_AUTH_02`: Đăng nhập & cấp quyền RBAC
- **Vai trò áp dụng:** Tất cả
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Xác thực và điều hướng người dùng đến workspace theo vai trò.
- **Dữ liệu đầu vào:** SĐT/Mã NV, mật khẩu
- **Kết quả đầu ra:** JWT + thông tin quyền
- **Quy tắc nghiệp vụ:** Tài khoản bị khóa không được đăng nhập.
- **Trạng thái kiểm thử:** `PASS`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (SĐT/Mã NV, mật khẩu) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Tất cả) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_AUTH_03`: Cập nhật hồ sơ cá nhân & đổi mật khẩu
- **Vai trò áp dụng:** Tất cả
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Cho phép sửa thông tin cá nhân và đổi mật khẩu.
- **Dữ liệu đầu vào:** Thông tin mới, mật khẩu cũ/mới
- **Kết quả đầu ra:** Hồ sơ cập nhật
- **Quy tắc nghiệp vụ:** Mật khẩu cũ phải đúng.
- **Trạng thái kiểm thử:** `PASS`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Thông tin mới, mật khẩu cũ/mới) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Tất cả) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_AUTH_04`: Quên/khôi phục mật khẩu
- **Vai trò áp dụng:** Tất cả
- **Mức ưu tiên / Phạm vi:** Trung bình | Phase 2/MVP
- **Mô tả nghiệp vụ:** Khôi phục bằng OTP hoặc kênh xác minh được cấu hình.
- **Dữ liệu đầu vào:** SĐT/email, OTP
- **Kết quả đầu ra:** Mật khẩu mới
- **Quy tắc nghiệp vụ:** OTP có thời hạn và dùng một lần.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (SĐT/email, OTP) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Tất cả) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_AUTH_05`: Quản lý vai trò và quyền
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tạo/gán vai trò và quyền cho tài khoản nhân viên.
- **Dữ liệu đầu vào:** User, Role, Permission
- **Kết quả đầu ra:** Ma trận quyền
- **Quy tắc nghiệp vụ:** Không cho xóa quyền hệ thống gây mất truy cập Admin.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (User, Role, Permission) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_AUTH_06`: Nhật ký thao tác hệ thống
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận ai thực hiện thao tác quan trọng trên bệnh án, hóa đơn, kho và tài khoản.
- **Dữ liệu đầu vào:** User, action, entity, timestamp
- **Kết quả đầu ra:** Audit log
- **Quy tắc nghiệp vụ:** Không cho người dùng tự sửa/xóa log.
- **Trạng thái kiểm thử:** `PARTIAL`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_AUTH_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_AUTH_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (User, action, entity, timestamp) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_AUTH_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_AUTH_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_AUTH_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_AUTH_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_AUTH_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_AUTH_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_PAT — Patient Profile (9 chức năng)

### `F_PAT_01`: Tạo hồ sơ bệnh nhân
- **Vai trò áp dụng:** Lễ tân, Phụ tá, Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tạo hồ sơ cho bệnh nhân mới khi tiếp nhận hoặc khi đặt lịch tại quầy; thu thập thông tin nhận dạng và thông tin ban đầu theo quy trình.
- **Dữ liệu đầu vào:** Họ tên, ngày sinh, giới tính, SĐT, địa chỉ, thông tin ban đầu
- **Kết quả đầu ra:** Patient profile
- **Quy tắc nghiệp vụ:** Kiểm tra hồ sơ trùng trước khi tạo; không tạo bản ghi mới nếu đã có hồ sơ hợp lệ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Họ tên, ngày sinh, giới tính, SĐT, địa chỉ, thông tin ban đầu) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_02`: Tìm kiếm hồ sơ bệnh nhân nhanh
- **Vai trò áp dụng:** Lễ tân, Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tra cứu theo SĐT, mã bệnh nhân hoặc họ tên để vận hành quầy và ghế khám.
- **Dữ liệu đầu vào:** SĐT/mã/tên
- **Kết quả đầu ra:** Danh sách hồ sơ
- **Quy tắc nghiệp vụ:** Có phân quyền dữ liệu.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (SĐT/mã/tên) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_03`: Kiểm tra trùng hồ sơ
- **Vai trò áp dụng:** Lễ tân
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Cảnh báo khi dữ liệu nhập có khả năng trùng bệnh nhân.
- **Dữ liệu đầu vào:** SĐT, họ tên, DOB
- **Kết quả đầu ra:** Cảnh báo trùng
- **Quy tắc nghiệp vụ:** Không tự động hợp nhất hồ sơ nếu chưa xác minh.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (SĐT, họ tên, DOB) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_04`: Ghi nhận tiền sử bệnh & dị ứng
- **Vai trò áp dụng:** Lễ tân, Phụ tá (Nha sĩ xem theo quyền)
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận và cập nhật tiền sử bệnh, dị ứng và các thông tin sức khỏe liên quan trước khi bệnh nhân gặp nha sĩ.
- **Dữ liệu đầu vào:** Bệnh nền, dị ứng, ghi chú
- **Kết quả đầu ra:** Medical history
- **Quy tắc nghiệp vụ:** Không ghi đè lịch sử của các lần khám trước; thông tin mới được gắn với hồ sơ hiện tại và lần khám tương ứng.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Bệnh nền, dị ứng, ghi chú) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá (Nha sĩ xem theo quyền)) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_05`: Ghi nhận sinh hiệu cơ bản
- **Vai trò áp dụng:** Lễ tân, Phụ tá (Nha sĩ xem theo quyền)
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận sinh hiệu cơ bản như huyết áp, nhịp tim và các chỉ số khác theo quy định thực tế của phòng khám trước khi khám.
- **Dữ liệu đầu vào:** Huyết áp, mạch, nhiệt độ (nếu dùng)
- **Kết quả đầu ra:** Vital record
- **Quy tắc nghiệp vụ:** Chỉ lưu số liệu do nhân viên nhập; dữ liệu phải gắn với đúng bệnh nhân và lần khám, không ghi đè bản ghi cũ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Huyết áp, mạch, nhiệt độ (nếu dùng)) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá (Nha sĩ xem theo quyền)) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_06`: Cảnh báo tiền sử an toàn
- **Vai trò áp dụng:** Hệ thống, Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Hiển thị cảnh báo rõ ràng về dị ứng/bệnh nền quan trọng khi mở hồ sơ khám.
- **Dữ liệu đầu vào:** Medical history
- **Kết quả đầu ra:** Safety alert
- **Quy tắc nghiệp vụ:** Cảnh báo không thay thế quyết định chuyên môn.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Medical history) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Hệ thống, Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_07`: Lịch sử điều trị dạng timeline
- **Vai trò áp dụng:** Bệnh nhân, Nha sĩ, Lễ tân/Phụ tá theo quyền
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Hiển thị các lần khám, dịch vụ, chẩn đoán và kết quả theo thời gian.
- **Dữ liệu đầu vào:** Patient ID
- **Kết quả đầu ra:** Treatment timeline
- **Quy tắc nghiệp vụ:** Bệnh nhân chỉ xem dữ liệu của mình.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient ID) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Bệnh nhân, Nha sĩ, Lễ tân/Phụ tá theo quyền) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_PAT_07_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_PAT_08`: Khóa/chốt hồ sơ điều trị
- **Vai trò áp dụng:** Nha sĩ, Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Đóng hồ sơ điều trị sau khi hoàn tất; chỉ người được quyền mới điều chỉnh.
- **Dữ liệu đầu vào:** Visit/Record status
- **Kết quả đầu ra:** Record locked
- **Quy tắc nghiệp vụ:** Sau khi chốt phải lưu nhật ký mọi chỉnh sửa đặc biệt.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_08_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_08_02 (Required Fields):** Bỏ trống các trường bắt buộc (Visit/Record status) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_08_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_08_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_08_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_08_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_08_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_08_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_PAT_09`: Tạo bản ghi khám mới cho từng lần khám
- **Vai trò áp dụng:** Lễ tân, Phụ tá; Hệ thống
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Tạo một bản ghi khám mới cho lần đến hiện tại; với bệnh nhân cũ, cập nhật hồ sơ hiện hành nhưng vẫn tạo visit mới để bảo toàn lịch sử.
- **Dữ liệu đầu vào:** Patient ID, thời điểm tiếp nhận, thông tin tiền khám
- **Kết quả đầu ra:** Visit/Encounter mới
- **Quy tắc nghiệp vụ:** Mỗi lần khám phải có bản ghi riêng; không ghi đè visit cũ; bản ghi mới liên kết đúng bệnh nhân.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_PAT_09_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_PAT_09_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient ID, thời điểm tiếp nhận, thông tin tiền khám) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_PAT_09_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_PAT_09_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_PAT_09_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_PAT_09_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_PAT_09_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá; Hệ thống) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_PAT_09_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_APP — Online Appointment Booking (2 chức năng)

### `F_APP_01`: Đặt lịch khám trực tuyến
- **Vai trò áp dụng:** Bệnh nhân
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Cho phép khách hàng tự đặt lịch khám trực tuyến và cung cấp thông tin cần thiết cho lần tiếp nhận.
- **Dữ liệu đầu vào:** Tài khoản bệnh nhân, ngày/giờ mong muốn, nội dung đặt lịch
- **Kết quả đầu ra:** Appointment record
- **Quy tắc nghiệp vụ:** Lịch đặt trực tuyến phải gắn với đúng hồ sơ bệnh nhân; sau khi đặt, hồ sơ được đưa vào danh sách chờ theo luồng mới.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_APP_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_APP_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tài khoản bệnh nhân, ngày/giờ mong muốn, nội dung đặt lịch) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_APP_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_APP_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_APP_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_APP_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_APP_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Bệnh nhân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_APP_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_APP_02`: Đặt lịch tại quầy do Lễ tân thao tác hộ
- **Vai trò áp dụng:** Lễ tân
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Lễ tân tiếp nhận yêu cầu của khách và nhập lịch trên hệ thống thay cho khách.
- **Dữ liệu đầu vào:** Thông tin bệnh nhân, yêu cầu đặt lịch
- **Kết quả đầu ra:** Appointment record
- **Quy tắc nghiệp vụ:** Nếu khách chưa có hồ sơ thì tạo hồ sơ trước; lịch phải gắn đúng bệnh nhân; sau khi hoàn tất, đưa vào danh sách chờ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_APP_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_APP_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Thông tin bệnh nhân, yêu cầu đặt lịch) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_APP_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_APP_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_APP_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_APP_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_APP_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_APP_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_CHK — Arrival Check-in (7 chức năng)

### `F_CHK_01`: Xác nhận khách đến phòng khám
- **Vai trò áp dụng:** Lễ tân, Phụ tá
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Xác nhận khách đã đến, đối chiếu hồ sơ và kích hoạt trạng thái phục vụ để tiếp tục quy trình hàng đợi.
- **Dữ liệu đầu vào:** Mã hồ sơ, SĐT, thông tin đặt lịch (nếu có)
- **Kết quả đầu ra:** Arrival confirmed / Queue-ready
- **Quy tắc nghiệp vụ:** Có thể tiếp nhận khách đã đặt trước hoặc khách đến trực tiếp; không công khai thông tin sức khỏe.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Mã hồ sơ, SĐT, thông tin đặt lịch (nếu có)) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_02`: Đưa bệnh nhân vào hàng đợi
- **Vai trò áp dụng:** Lễ tân, Phụ tá, Nha sĩ, Admin theo quyền; Hệ thống
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Đưa hồ sơ khách vào một danh sách chờ chung sau khi đặt lịch hoặc tiếp nhận; quản lý thứ tự và trạng thái tiến trình của từng khách.
- **Dữ liệu đầu vào:** Patient/Visit, thời điểm đặt/tiếp nhận, thông tin ưu tiên nếu phòng khám có quy định
- **Kết quả đầu ra:** Queue item / Trạng thái hàng đợi
- **Quy tắc nghiệp vụ:** Danh sách chờ là danh sách chung; dữ liệu đầy đủ chỉ nhân viên được phân quyền xem; không gắn cố định bệnh nhân với nha sĩ/ghế từ bước xếp hàng.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient/Visit, thời điểm đặt/tiếp nhận, thông tin ưu tiên nếu phòng khám có quy định) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá, Nha sĩ, Admin theo quyền; Hệ thống) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_03`: Gọi số và dẫn bệnh nhân sang khu vực khám tiếp theo
- **Vai trò áp dụng:** Lễ tân, Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Lấy khách tiếp theo từ danh sách chờ, gọi số và dẫn bệnh nhân sang khu vực chờ/khám kế tiếp theo quy trình.
- **Dữ liệu đầu vào:** Queue item, người thực hiện
- **Kết quả đầu ra:** Called / Next-step status
- **Quy tắc nghiệp vụ:** Lễ tân và Phụ tá có thể thay thế nhau thực hiện; không hiển thị thông tin sức khỏe cho người không có quyền.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Queue item, người thực hiện) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_07`: Cập nhật trạng thái tiến trình hàng đợi
- **Vai trò áp dụng:** Lễ tân, Phụ tá, Nha sĩ theo quyền; Hệ thống
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Cập nhật trạng thái của từng khách theo tiến trình, ví dụ: Đang đợi, Đã ghi nhận thông tin sức khỏe, Đang chụp X-quang, Chờ khám nha sĩ, Đang khám, Hoàn tất.
- **Dữ liệu đầu vào:** Queue item, trạng thái mới, người thực hiện
- **Kết quả đầu ra:** Queue status history
- **Quy tắc nghiệp vụ:** Mỗi chuyển trạng thái phải lưu thời điểm và người/nguồn thực hiện; không hiển thị dữ liệu sức khỏe ra màn hình công khai.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Queue item, trạng thái mới, người thực hiện) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá, Nha sĩ theo quyền; Hệ thống) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_08`: Điều phối bệnh nhân sang Nha sĩ hoặc X-quang
- **Vai trò áp dụng:** Lễ tân, Phụ tá
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Sau khi hoàn tất ghi nhận thông tin trước khám, điều phối bệnh nhân sang gặp Nha sĩ trực tiếp hoặc sang chụp X-quang trước nếu có nhu cầu/chỉ định.
- **Dữ liệu đầu vào:** Queue item, kết quả tiền khám, chỉ định hình ảnh (nếu có)
- **Kết quả đầu ra:** Next-step assignment / Queue status
- **Quy tắc nghiệp vụ:** Chỉ cho phép chuyển sang X-quang khi có chỉ định/nhu cầu đã được ghi nhận; sau khi chụp xong, bệnh nhân chuyển tiếp sang Nha sĩ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_08_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_08_02 (Required Fields):** Bỏ trống các trường bắt buộc (Queue item, kết quả tiền khám, chỉ định hình ảnh (nếu có)) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_08_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_08_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_08_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_08_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_08_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_08_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_04`: Bắt đầu/ kết thúc lần khám
- **Vai trò áp dụng:** Nha sĩ; Lễ tân/Phụ tá theo quyền hỗ trợ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận thời điểm bắt đầu và kết thúc phiên khám.
- **Dữ liệu đầu vào:** Queue item, action
- **Kết quả đầu ra:** Visit timestamps
- **Quy tắc nghiệp vụ:** Kết thúc khám là điều kiện tạo hóa đơn nháp.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Queue item, action) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ; Lễ tân/Phụ tá theo quyền hỗ trợ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_CHK_05`: Chuyển bệnh nhân sang nha sĩ khác
- **Vai trò áp dụng:** Lễ tân, Nha sĩ, Phụ tá theo quyền
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Chuyển queue/visit khi cần chuyên khoa hoặc thay đổi nhân sự.
- **Dữ liệu đầu vào:** Patient, dentist, reason
- **Kết quả đầu ra:** Updated assignment
- **Quy tắc nghiệp vụ:** Phải lưu lý do, người chuyển và trạng thái trước/sau khi chuyển; không phụ thuộc lịch làm việc/ca trực.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_CHK_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_CHK_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient, dentist, reason) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_CHK_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_CHK_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_CHK_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_CHK_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_CHK_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Lễ tân, Nha sĩ, Phụ tá theo quyền) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_CHK_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_FDI — FDI Dental Chart (7 chức năng)

### `F_FDI_01`: Hiển thị sơ đồ răng FDI
- **Vai trò áp dụng:** Nha sĩ, Phụ tá
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Hiển thị 32 răng người lớn hoặc 20 răng trẻ em theo chuẩn FDI.
- **Dữ liệu đầu vào:** Patient age/profile
- **Kết quả đầu ra:** Interactive chart
- **Quy tắc nghiệp vụ:** Đánh số đúng FDI; chọn đúng loại bộ răng.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient age/profile) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_01_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_02`: Chọn răng và mặt răng
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Chọn răng và mặt răng cần ghi nhận tổn thương/điều trị.
- **Dữ liệu đầu vào:** Tooth ID, surface
- **Kết quả đầu ra:** Selected tooth/surface
- **Quy tắc nghiệp vụ:** Không cho chọn răng không tồn tại trong bộ răng.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth ID, surface) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_02_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_03`: Gán tình trạng bệnh lý
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Gán bệnh lý cho răng và thể hiện trạng thái trực quan.
- **Dữ liệu đầu vào:** Tooth, pathology
- **Kết quả đầu ra:** Tooth condition
- **Quy tắc nghiệp vụ:** Bệnh lý lấy từ danh mục hệ thống.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth, pathology) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_03_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_04`: Chỉ định dịch vụ theo răng
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Gán dịch vụ điều trị cho răng đang chọn và đưa sang hóa đơn.
- **Dữ liệu đầu vào:** Tooth, service
- **Kết quả đầu ra:** Treatment plan
- **Quy tắc nghiệp vụ:** Đơn giá lấy từ bảng giá hiện hành.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth, service) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_04_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_05`: Cập nhật trạng thái sau điều trị
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Cập nhật tình trạng răng sau khi hoàn tất điều trị.
- **Dữ liệu đầu vào:** Tooth, new status
- **Kết quả đầu ra:** Updated chart
- **Quy tắc nghiệp vụ:** Phải gắn với lần khám.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth, new status) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_05_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_06`: Lịch sử trạng thái răng
- **Vai trò áp dụng:** Nha sĩ, Bệnh nhân theo quyền
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Xem thay đổi của từng răng qua các lần khám.
- **Dữ liệu đầu vào:** Tooth ID
- **Kết quả đầu ra:** Tooth history timeline
- **Quy tắc nghiệp vụ:** Bệnh nhân chỉ xem lịch sử của chính mình.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth ID) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Bệnh nhân theo quyền) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_06_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

### `F_FDI_07`: Ghi chú lâm sàng theo răng
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Ghi nhận mô tả lâm sàng riêng cho răng/mặt răng nếu cần.
- **Dữ liệu đầu vào:** Tooth, note
- **Kết quả đầu ra:** Clinical note
- **Quy tắc nghiệp vụ:** Không thay thế nội dung bệnh án chính.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_FDI_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_FDI_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Tooth, note) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_FDI_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_FDI_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_FDI_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_FDI_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_FDI_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_FDI_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_FDI_07_09 (FDI Standard):** Kiểm tra đúng mã răng FDI (11-48 người lớn, 51-85 trẻ em), 5 mặt răng (B, L, M, D, O). Chặn mã răng ngoài chuẩn.

## MOD_IMG — Imaging Order (4 chức năng)

### `F_IMG_01`: Tạo chỉ định chụp ảnh
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Ghi nhận/chốt nhu cầu chụp X-quang hoặc chụp trong miệng cho lần khám; hệ thống chỉ lưu chỉ định, không điều khiển thiết bị.
- **Dữ liệu đầu vào:** Visit, image type, note
- **Kết quả đầu ra:** Imaging order
- **Quy tắc nghiệp vụ:** Chỉ định phải do người có quyền chuyên môn tạo; khi đã có chỉ định, Lễ tân/Phụ tá có thể dùng thông tin này để điều phối bệnh nhân sang bước chụp.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_IMG_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_IMG_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Visit, image type, note) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_IMG_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_IMG_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_IMG_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_IMG_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_IMG_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_IMG_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_IMG_02`: Tải lên ảnh X-quang/chụp trong miệng
- **Vai trò áp dụng:** Phụ tá, Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tải file hình ảnh lên hồ sơ bệnh án.
- **Dữ liệu đầu vào:** Image file, visit
- **Kết quả đầu ra:** Attachment record
- **Quy tắc nghiệp vụ:** Giới hạn định dạng/kích thước; phân quyền truy cập.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_IMG_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_IMG_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Image file, visit) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_IMG_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_IMG_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_IMG_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_IMG_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_IMG_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá, Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_IMG_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_IMG_03`: Gắn ảnh với răng/lần khám
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Liên kết ảnh với visit hoặc răng cụ thể.
- **Dữ liệu đầu vào:** Attachment, tooth
- **Kết quả đầu ra:** Linked image
- **Quy tắc nghiệp vụ:** Không xóa file nếu chưa có quyền.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_IMG_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_IMG_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Attachment, tooth) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_IMG_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_IMG_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_IMG_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_IMG_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_IMG_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_IMG_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_IMG_04`: Xem/thu phóng ảnh
- **Vai trò áp dụng:** Nha sĩ, Bệnh nhân theo quyền
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Hiển thị ảnh có zoom và thông tin nguồn/lần khám.
- **Dữ liệu đầu vào:** Attachment ID
- **Kết quả đầu ra:** Image viewer
- **Quy tắc nghiệp vụ:** Ẩn khỏi người không có quyền.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_IMG_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_IMG_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Attachment ID) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_IMG_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_IMG_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_IMG_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_IMG_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_IMG_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Bệnh nhân theo quyền) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_IMG_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_RX — Electronic Prescription (4 chức năng)

### `F_RX_01`: Kê đơn thuốc điện tử
- **Vai trò áp dụng:** Nha sĩ
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Chọn thuốc, số lượng, liều dùng sau khám.
- **Dữ liệu đầu vào:** Drug, qty, dosage
- **Kết quả đầu ra:** Prescription
- **Quy tắc nghiệp vụ:** Thuốc phải thuộc danh mục hệ thống.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RX_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RX_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Drug, qty, dosage) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RX_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RX_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RX_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RX_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RX_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RX_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RX_02`: Kiểm tra tồn kho thuốc
- **Vai trò áp dụng:** Nha sĩ, Hệ thống
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Kiểm tra số lượng còn trước khi ghi nhận cấp thuốc nội bộ.
- **Dữ liệu đầu vào:** Drug, qty
- **Kết quả đầu ra:** Availability status
- **Quy tắc nghiệp vụ:** Không cho ghi nhận xuất vượt tồn nếu phòng khám cấp thuốc tại chỗ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RX_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RX_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Drug, qty) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RX_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RX_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RX_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RX_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RX_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Hệ thống) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RX_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RX_04`: In/xuất đơn thuốc
- **Vai trò áp dụng:** Nha sĩ, Lễ tân
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Xuất đơn thuốc thành PDF/in để bệnh nhân sử dụng.
- **Dữ liệu đầu vào:** Prescription
- **Kết quả đầu ra:** PDF/print
- **Quy tắc nghiệp vụ:** Thông tin thuốc phải đúng bản ghi đã chốt.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RX_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RX_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Prescription) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RX_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RX_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RX_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RX_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RX_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Lễ tân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RX_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RX_05`: Lịch sử đơn thuốc
- **Vai trò áp dụng:** Nha sĩ, Bệnh nhân theo quyền
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Tra cứu đơn thuốc theo từng lần khám.
- **Dữ liệu đầu vào:** Patient/visit
- **Kết quả đầu ra:** Prescription history
- **Quy tắc nghiệp vụ:** Bệnh nhân chỉ xem đơn của mình.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RX_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RX_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Patient/visit) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RX_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RX_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RX_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RX_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RX_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Nha sĩ, Bệnh nhân theo quyền) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RX_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_BIL — Draft Invoice (7 chức năng)

### `F_BIL_01`: Tạo hóa đơn nháp từ dịch vụ
- **Vai trò áp dụng:** Hệ thống, Thu ngân
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Khi kết thúc khám, tổng hợp dịch vụ thành hóa đơn nháp.
- **Dữ liệu đầu vào:** Treatment plan, prices
- **Kết quả đầu ra:** Draft invoice
- **Quy tắc nghiệp vụ:** Chỉ lấy dịch vụ đã ghi nhận.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Treatment plan, prices) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Hệ thống, Thu ngân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_01_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_02`: Áp dụng giảm giá/khuyến mãi
- **Vai trò áp dụng:** Thu ngân, Admin
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Áp dụng mức giảm được cấu hình.
- **Dữ liệu đầu vào:** Coupon/promo, invoice
- **Kết quả đầu ra:** Adjusted invoice
- **Quy tắc nghiệp vụ:** Không để tổng tiền âm; lưu lý do giảm.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Coupon/promo, invoice) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_02_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_03`: Xác nhận thanh toán
- **Vai trò áp dụng:** Thu ngân
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Ghi nhận đã thu bằng tiền mặt hoặc chuyển khoản thủ công.
- **Dữ liệu đầu vào:** Invoice, payment method, amount
- **Kết quả đầu ra:** Paid transaction
- **Quy tắc nghiệp vụ:** Tổng tiền thu khớp số phải thu; không tự động xác thực ngân hàng.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Invoice, payment method, amount) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_03_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_04`: Lập kế hoạch trả góp
- **Vai trò áp dụng:** Thu ngân, Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Tạo các kỳ phải thu cho dịch vụ chi phí cao.
- **Dữ liệu đầu vào:** Total, deposit, installment count/dates
- **Kết quả đầu ra:** Installment plan
- **Quy tắc nghiệp vụ:** Tổng các kỳ phải khớp số phải thu.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Total, deposit, installment count/dates) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_04_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_05`: Thu từng kỳ trả góp & theo dõi công nợ
- **Vai trò áp dụng:** Thu ngân
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận từng lần thu, số còn lại và trạng thái quá hạn.
- **Dữ liệu đầu vào:** Installment, payment
- **Kết quả đầu ra:** Paid/remaining balance
- **Quy tắc nghiệp vụ:** Không ghi nhận vượt số còn phải thu.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Installment, payment) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_05_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_06`: In/xuất biên lai
- **Vai trò áp dụng:** Thu ngân
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** In hoặc xuất chứng từ sau thanh toán.
- **Dữ liệu đầu vào:** Paid invoice
- **Kết quả đầu ra:** Receipt/PDF
- **Quy tắc nghiệp vụ:** Không sửa nội dung đã thanh toán.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Paid invoice) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_06_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

### `F_BIL_07`: Hủy hóa đơn nháp/điều chỉnh trước thanh toán
- **Vai trò áp dụng:** Thu ngân, Admin
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Cho phép void hóa đơn nháp hoặc điều chỉnh trước khi thanh toán.
- **Dữ liệu đầu vào:** Draft invoice, reason
- **Kết quả đầu ra:** Voided/updated invoice
- **Quy tắc nghiệp vụ:** Không xóa vật lý hóa đơn đã sinh; lưu lịch sử.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_BIL_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_BIL_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Draft invoice, reason) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_BIL_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_BIL_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_BIL_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_BIL_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_BIL_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Thu ngân, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_BIL_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_BIL_07_09 (Billing Calculation):** Kiểm tra tính toán tổng tiền, chiết khấu, VAT, lịch trình đợt trả góp, chặn chiết khấu âm hoặc vượt tiền.

## MOD_INV — Inventory Item Master (9 chức năng)

### `F_INV_01`: Quản lý danh mục vật tư và thuốc kho
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Quản lý mã, tên, đơn vị, giá nhập, giá bán và nhóm hàng. Nhân viên quản lý dữ liệu danh mục thủ công trên hệ thống; hệ thống có thể cảnh báo khi mặt hàng chưa đủ thông tin hoặc khi tồn xuống dưới ngưỡng đã cấu hình.
- **Dữ liệu đầu vào:** Item master
- **Kết quả đầu ra:** Item catalog
- **Quy tắc nghiệp vụ:** Mã vật tư duy nhất; các thay đổi danh mục phải do người có quyền thực hiện và được ghi nhận.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Item master) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_01_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_02`: Quản lý nhà cung cấp
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Lưu và cập nhật thông tin đơn vị cung ứng vật tư/thuốc. Người dùng có quyền nhập và chỉnh sửa thủ công; hệ thống có thể cảnh báo dữ liệu nhà cung cấp còn thiếu hoặc trùng.
- **Dữ liệu đầu vào:** Supplier info
- **Kết quả đầu ra:** Supplier record
- **Quy tắc nghiệp vụ:** Mã/tên nhà cung cấp không trùng bất hợp lý; thay đổi do người có quyền thực hiện.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Supplier info) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_02_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_03`: Lập phiếu nhập kho
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Nhân viên lập và xác nhận phiếu nhập kho thủ công theo nhà cung cấp, mặt hàng, số lượng, lô và hạn sử dụng. Hệ thống kiểm tra dữ liệu và có thể cảnh báo số lượng bất thường hoặc thiếu thông tin; không tự sinh giao dịch nhập kho ngoài thao tác xác nhận của người dùng.
- **Dữ liệu đầu vào:** Supplier, item, qty, lot, expiry
- **Kết quả đầu ra:** Stock receipt
- **Quy tắc nghiệp vụ:** Số lượng > 0; phải xác định đơn vị tính; phiếu nhập chỉ có hiệu lực sau khi người có quyền xác nhận.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Supplier, item, qty, lot, expiry) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_03_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_04`: Quản lý lô & hạn sử dụng
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Theo dõi tồn kho theo từng lô và ngày hết hạn. Hệ thống cảnh báo lô sắp hết hạn hoặc đã hết hạn để nhân viên kiểm tra và xử lý; mọi cập nhật hoặc xuất/điều chỉnh liên quan đến lô do người có quyền thực hiện thủ công.
- **Dữ liệu đầu vào:** Lot, expiry, qty
- **Kết quả đầu ra:** Batch stock
- **Quy tắc nghiệp vụ:** Luôn hiển thị cảnh báo đối với lô hết hạn/sắp hết hạn; giao dịch kho không tự thực hiện, người có quyền phải xác nhận.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Lot, expiry, qty) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_04_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_05`: Cấu hình BOM/định mức theo dịch vụ
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Khai báo lượng vật tư dự kiến tiêu hao cho từng dịch vụ. Hệ thống dùng BOM để tính/gợi ý mức tiêu hao tham khảo cho nhân viên khi ghi nhận thực tế và cảnh báo khi tiêu hao vượt định mức; BOM không tự động tạo phiếu xuất hoặc trừ kho.
- **Dữ liệu đầu vào:** Service, item, qty
- **Kết quả đầu ra:** BOM
- **Quy tắc nghiệp vụ:** Định mức không âm; phiên bản định mức phải có hiệu lực; BOM chỉ là cơ sở gợi ý/cảnh báo.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Service, item, qty) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_05_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_06`: Tự động trừ kho theo dịch vụ hoàn tất
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Khi một dịch vụ hoàn tất, hệ thống tham khảo BOM để gợi ý lượng vật tư dự kiến cần xuất và cảnh báo nếu tồn không đủ hoặc mức dùng vượt định mức. Nhân viên kiểm tra vật tư thực tế đã sử dụng và chủ động xác nhận giao dịch xuất kho; hệ thống chỉ ghi nhận/trừ tồn sau khi có xác nhận của người có quyền.
- **Dữ liệu đầu vào:** Completed service, BOM, actual consumption
- **Kết quả đầu ra:** Stock issue proposal; confirmed stock transaction
- **Quy tắc nghiệp vụ:** Không tự động trừ kho; chỉ gợi ý/cảnh báo. Giao dịch xuất kho chỉ được ghi nhận sau khi Admin hoặc Phụ tá xác nhận số lượng thực tế.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Completed service, BOM, actual consumption) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_06_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_07`: Ghi nhận tiêu hao phát sinh
- **Vai trò áp dụng:** Phụ tá, Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi nhận số lượng vật tư thực tế đã sử dụng tại ca điều trị, kể cả phần ngoài BOM. Hệ thống có thể gợi ý mức theo BOM và cảnh báo khi số lượng nhập vượt mức tham khảo; việc xác nhận và cập nhật tồn kho do người có quyền thực hiện.
- **Dữ liệu đầu vào:** Visit, item, qty, reason
- **Kết quả đầu ra:** Extra consumption
- **Quy tắc nghiệp vụ:** Dữ liệu tiêu hao phải gắn với lần khám; số lượng thực tế phải được người có quyền xác nhận trước khi cập nhật giao dịch kho.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Visit, item, qty, reason) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_07_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_08`: Cảnh báo tồn tối thiểu & hết hạn
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Hiển thị cảnh báo vật tư dưới ngưỡng tồn tối thiểu, sắp hết hạn hoặc đã hết hạn dựa trên dữ liệu kho đã nhập. Hệ thống không tự tạo phiếu nhập, không tự điều chỉnh và không tự xử lý hàng hóa.
- **Dữ liệu đầu vào:** Min level, expiry dates
- **Kết quả đầu ra:** Alert list
- **Quy tắc nghiệp vụ:** Ngưỡng cảnh báo cấu hình theo từng mặt hàng; cảnh báo chỉ hỗ trợ nhân viên quyết định, mọi xử lý tiếp theo phải do người dùng thực hiện.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_08_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_08_02 (Required Fields):** Bỏ trống các trường bắt buộc (Min level, expiry dates) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_08_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_08_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_08_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_08_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_08_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_08_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_08_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

### `F_INV_09`: Kiểm kê & điều chỉnh tồn
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Nhân viên kiểm đếm thực tế, nhập số lượng kiểm kê và lập phiếu chênh lệch. Hệ thống so sánh số liệu đã nhập với tồn hệ thống, hiển thị chênh lệch và cảnh báo bất thường; người có quyền phải xác nhận việc điều chỉnh tồn.
- **Dữ liệu đầu vào:** Count, reason
- **Kết quả đầu ra:** Adjustment
- **Quy tắc nghiệp vụ:** Điều chỉnh phải lưu người thực hiện, thời gian và lý do; không tự động điều chỉnh tồn chỉ từ kết quả kiểm kê.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_INV_09_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_INV_09_02 (Required Fields):** Bỏ trống các trường bắt buộc (Count, reason) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_INV_09_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_INV_09_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_INV_09_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_INV_09_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_INV_09_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_INV_09_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.
  - [ ] **TC_F_INV_09_09 (Inventory BOM Rule):** Kiểm tra logic gợi ý vật tư theo BOM; tồn kho KHÔNG tự động trừ; chỉ trừ khi Phụ tá/Admin xác nhận thủ công.

## MOD_ASS — Chairside Setup (6 chức năng)

### `F_ASS_01`: Chuẩn bị khay dụng cụ theo dịch vụ
- **Vai trò áp dụng:** Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Xem dịch vụ tiếp theo và checklist dụng cụ cần chuẩn bị.
- **Dữ liệu đầu vào:** Queue + service
- **Kết quả đầu ra:** Ready status/checklist
- **Quy tắc nghiệp vụ:** Phải theo danh mục hướng dẫn nội bộ phòng khám.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Queue + service) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_ASS_02`: Ghi nhận tiêu hao tại ghế
- **Vai trò áp dụng:** Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Ghi vật tư phát sinh thực tế tại ca điều trị.
- **Dữ liệu đầu vào:** Visit, item, qty
- **Kết quả đầu ra:** Consumption record
- **Quy tắc nghiệp vụ:** Liên kết ca khám.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Visit, item, qty) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_ASS_03`: Theo dõi quy trình vô trùng bằng checklist
- **Vai trò áp dụng:** Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | Phase 2/MVP
- **Mô tả nghiệp vụ:** Ghi các bước ngâm/ rửa/ đóng gói/ hấp dưới dạng checklist.
- **Dữ liệu đầu vào:** Date, batch/cycle note, operator
- **Kết quả đầu ra:** Sterilization log
- **Quy tắc nghiệp vụ:** Hệ thống chỉ ghi nhận quy trình; không điều khiển Autoclave.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Date, batch/cycle note, operator) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_ASS_04`: Bàn giao ca
- **Vai trò áp dụng:** Phụ tá
- **Mức ưu tiên / Phạm vi:** Trung bình | Phase 2
- **Mô tả nghiệp vụ:** Bàn giao ca điều trị dở dang và công việc còn lại.
- **Dữ liệu đầu vào:** Visit, handover note
- **Kết quả đầu ra:** Handover record
- **Quy tắc nghiệp vụ:** Hai bên xác nhận theo quyền.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Visit, handover note) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_ASS_05`: Checklist vệ sinh/đóng cửa cuối ngày
- **Vai trò áp dụng:** Phụ tá
- **Mức ưu tiên / Phạm vi:** Trung bình | Phase 2
- **Mô tả nghiệp vụ:** Tick các hạng mục đóng cửa và vệ sinh.
- **Dữ liệu đầu vào:** Checklist items
- **Kết quả đầu ra:** Closing record
- **Quy tắc nghiệp vụ:** Phải hoàn thành các mục bắt buộc trước khi đóng ca.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Checklist items) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_ASS_06`: Trạng thái ghế/phòng điều trị
- **Vai trò áp dụng:** Phụ tá, Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Cập nhật trạng thái ghế/phòng để nhân sự biết vị trí nào đang sẵn sàng, đang sử dụng, bảo trì hoặc tạm ngưng.
- **Dữ liệu đầu vào:** Chair status
- **Kết quả đầu ra:** Chair availability
- **Quy tắc nghiệp vụ:** Ghế/phòng ở trạng thái bảo trì hoặc tạm ngưng không được chọn để phục vụ lượt khám.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_ASS_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_ASS_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Chair status) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_ASS_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_ASS_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_ASS_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_ASS_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_ASS_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Phụ tá, Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_ASS_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_MST — Service & Price Management (7 chức năng)

### `F_MST_01`: Quản lý dịch vụ & bảng giá
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Thêm/sửa/ngừng dịch vụ, giá và thời lượng chuẩn.
- **Dữ liệu đầu vào:** Service, price, duration
- **Kết quả đầu ra:** Service catalog
- **Quy tắc nghiệp vụ:** Dịch vụ ngừng bán không được đặt mới.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Service, price, duration) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_02`: Quản lý bệnh lý/triệu chứng
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Khai báo danh mục dùng cho bệnh án và sơ đồ răng.
- **Dữ liệu đầu vào:** Pathology/summary
- **Kết quả đầu ra:** Pathology master
- **Quy tắc nghiệp vụ:** Mã bệnh lý duy nhất.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Pathology/summary) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_03`: Quản lý thuốc
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Khai báo thuốc, hoạt chất, hàm lượng, đơn vị.
- **Dữ liệu đầu vào:** Drug master
- **Kết quả đầu ra:** Drug catalog
- **Quy tắc nghiệp vụ:** Thông tin phải đủ để kê đơn và quản lý kho.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_03_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_03_02 (Required Fields):** Bỏ trống các trường bắt buộc (Drug master) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_03_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_03_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_03_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_03_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_03_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_03_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_04`: Quản lý vật tư
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Khai báo vật tư tiêu hao, đơn vị, ngưỡng tồn.
- **Dữ liệu đầu vào:** Item data
- **Kết quả đầu ra:** Item catalog
- **Quy tắc nghiệp vụ:** Ngưỡng tồn không âm.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Item data) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_05`: Quản lý phòng & ghế nha khoa
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Khai báo phòng và ghế nha khoa, trạng thái hoạt động, vị trí và thông tin nhận diện.
- **Dữ liệu đầu vào:** Room/chair
- **Kết quả đầu ra:** Chair master
- **Quy tắc nghiệp vụ:** Ghế/phòng bảo trì hoặc ngừng sử dụng không được đánh dấu sẵn sàng phục vụ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Room/chair) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_06`: Quản lý trạng thái và cấu hình hệ thống
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** Quản lý các enum/trạng thái và tham số dùng chung.
- **Dữ liệu đầu vào:** Config key/value
- **Kết quả đầu ra:** System config
- **Quy tắc nghiệp vụ:** Chỉ Admin; log mọi thay đổi nhạy cảm.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_06_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_06_02 (Required Fields):** Bỏ trống các trường bắt buộc (Config key/value) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_06_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_06_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_06_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_06_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_06_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_06_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_MST_07`: Quản lý người dùng nhân viên
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Tạo tài khoản nhân viên và trạng thái active/locked.
- **Dữ liệu đầu vào:** Staff profile
- **Kết quả đầu ra:** Employee user
- **Quy tắc nghiệp vụ:** Không xóa cứng tài khoản đã có dữ liệu nghiệp vụ.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_MST_07_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_MST_07_02 (Required Fields):** Bỏ trống các trường bắt buộc (Staff profile) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_MST_07_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_MST_07_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_MST_07_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_MST_07_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_MST_07_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_MST_07_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

## MOD_RPT — Revenue Report (4 chức năng)

### `F_RPT_01`: Báo cáo doanh thu
- **Vai trò áp dụng:** Admin, Chủ phòng khám
- **Mức ưu tiên / Phạm vi:** Bắt buộc | MVP
- **Mô tả nghiệp vụ:** Tổng hợp doanh thu theo ngày/tháng/quý/năm và phương thức thanh toán.
- **Dữ liệu đầu vào:** Date range, filters
- **Kết quả đầu ra:** Revenue dashboard
- **Quy tắc nghiệp vụ:** Chỉ tính giao dịch đã thanh toán.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RPT_01_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RPT_01_02 (Required Fields):** Bỏ trống các trường bắt buộc (Date range, filters) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RPT_01_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RPT_01_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RPT_01_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RPT_01_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RPT_01_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Chủ phòng khám) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RPT_01_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RPT_02`: Báo cáo hiệu suất nha sĩ
- **Vai trò áp dụng:** Admin, Chủ phòng khám
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Thống kê số ca, bệnh nhân và doanh thu theo nha sĩ.
- **Dữ liệu đầu vào:** Date range, dentist
- **Kết quả đầu ra:** Dentist performance
- **Quy tắc nghiệp vụ:** Định nghĩa rõ doanh thu quy về nha sĩ từ service/visit.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RPT_02_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RPT_02_02 (Required Fields):** Bỏ trống các trường bắt buộc (Date range, dentist) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RPT_02_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RPT_02_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RPT_02_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RPT_02_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RPT_02_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Chủ phòng khám) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RPT_02_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RPT_04`: Báo cáo xuất-nhập-tồn
- **Vai trò áp dụng:** Admin, Phụ tá
- **Mức ưu tiên / Phạm vi:** Cao | MVP
- **Mô tả nghiệp vụ:** Theo dõi nhập, xuất, tồn và chênh lệch kho.
- **Dữ liệu đầu vào:** Date range, item group
- **Kết quả đầu ra:** Inventory report
- **Quy tắc nghiệp vụ:** Đối chiếu phiếu nhập/xuất/điều chỉnh.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RPT_04_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RPT_04_02 (Required Fields):** Bỏ trống các trường bắt buộc (Date range, item group) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RPT_04_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RPT_04_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RPT_04_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RPT_04_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RPT_04_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin, Phụ tá) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RPT_04_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

### `F_RPT_05`: Báo cáo tiêu hao theo dịch vụ
- **Vai trò áp dụng:** Admin
- **Mức ưu tiên / Phạm vi:** Trung bình | MVP
- **Mô tả nghiệp vụ:** So sánh tiêu hao theo BOM và thực tế.
- **Dữ liệu đầu vào:** Date range, service
- **Kết quả đầu ra:** Consumption variance
- **Quy tắc nghiệp vụ:** Cần dữ liệu BOM + actual consumption.
- **Trạng thái kiểm thử:** `NOT TESTED`
- **Danh mục Test Cases chi tiết:**
  - [ ] **TC_F_RPT_05_01 (Happy Path):** Thực hiện luồng chuẩn với dữ liệu hợp lệ đầy đủ -> Hệ thống xử lý thành công, trả status code 200/201.
  - [ ] **TC_F_RPT_05_02 (Required Fields):** Bỏ trống các trường bắt buộc (Date range, service) -> Trả lỗi 400 Bad Request kèm thông điệp FluentValidation cụ thể.
  - [ ] **TC_F_RPT_05_03 (Boundary & Format):** Nhập giá trị biên độ dài, định dạng ký tự đặc biệt, chuỗi cực đại -> Hệ thống chặn lỗi biên an toàn.
  - [ ] **TC_F_RPT_05_04 (Duplicate Data):** Kiểm tra trùng lặp khóa duy nhất (SĐT, mã hồ sơ, mã lịch hẹn) -> Hệ thống từ chối ghi nhận trùng lặp.
  - [ ] **TC_F_RPT_05_05 (State Machine):** Kiểm tra chuyển đổi trạng thái hợp lệ và bất hợp lệ của đối tượng dữ liệu.
  - [ ] **TC_F_RPT_05_06 (RBAC Unauthorized):** Không gửi JWT Token hoặc gửi token giả mạo -> Trả về mã lỗi 401 Unauthorized.
  - [ ] **TC_F_RPT_05_07 (RBAC Forbidden):** Đăng nhập vai trò không có quyền (Admin) -> Trả về mã lỗi 403 Forbidden.
  - [ ] **TC_F_RPT_05_08 (Data Ownership & IDOR):** Thao tác trên ID bản ghi của tài khoản/bệnh nhân khác -> Bị chặn 403 Forbidden.

