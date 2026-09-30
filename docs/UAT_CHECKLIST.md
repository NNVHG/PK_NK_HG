# UAT_CHECKLIST.md — Checklist kiểm thử chấp nhận

> Dùng khi demo đồ án cho giảng viên hướng dẫn / hội đồng bảo vệ  
> Mỗi test case có: ID | Mô tả | Kết quả mong đợi | Kết quả thực tế | Pass/Fail

---

## Hướng dẫn sử dụng

- **Pass ✅** — Chức năng hoạt động đúng kết quả mong đợi
- **Fail ❌** — Không đúng hoặc lỗi
- **Partial ⚠️** — Hoạt động một phần
- Điền cột "Kết quả thực tế" và "Ngày test" khi kiểm thử

---

## 1. MOD_AUTH — Xác thực & Phân quyền

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| AUTH-01 | Đăng ký tài khoản bệnh nhân mới với SĐT hợp lệ | Tài khoản được tạo, đăng nhập được | | |
| AUTH-02 | Đăng ký với SĐT đã tồn tại | Thông báo lỗi trùng SĐT | | |
| AUTH-03 | Đăng nhập đúng SĐT + mật khẩu (vai trò Admin) | Vào được dashboard Admin | | |
| AUTH-04 | Đăng nhập đúng (vai trò Nha sĩ) | Vào được workspace Nha sĩ, không thấy báo cáo doanh thu | | |
| AUTH-05 | Đăng nhập đúng (vai trò Lễ tân) | Vào được màn hình lịch hẹn, check-in | | |
| AUTH-06 | Đăng nhập đúng (vai trò Phụ tá) | Vào được màn hình hỗ trợ điều trị | | |
| AUTH-07 | Đăng nhập đúng (vai trò Bệnh nhân) | Chỉ thấy dữ liệu của mình | | |
| AUTH-08 | Đăng nhập sai mật khẩu | Thông báo lỗi, không vào được | | |
| AUTH-09 | Tài khoản bị khóa (IsActive=false) cố đăng nhập | Thông báo tài khoản bị khóa | | |
| AUTH-10 | Admin xem AuditLog | Danh sách nhật ký hiển thị | | |
| AUTH-11 | Lễ tân cố truy cập trang AuditLog | Từ chối, thông báo không có quyền | | |

---

## 2. MOD_PAT — Bệnh nhân & Hồ sơ

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| PAT-01 | Lễ tân tạo hồ sơ bệnh nhân mới | Hồ sơ được lưu, có mã bệnh nhân | | |
| PAT-02 | Tạo hồ sơ trùng SĐT đã có | Cảnh báo trùng, không tạo | | |
| PAT-03 | Tìm kiếm bệnh nhân theo SĐT | Hiển thị kết quả đúng | | |
| PAT-04 | Tìm kiếm theo tên (gõ một phần) | Hiển thị danh sách khớp | | |
| PAT-05 | Nhập tiền sử bệnh + dị ứng | Lưu thành công | | |
| PAT-06 | Mở hồ sơ bệnh nhân có dị ứng | Cảnh báo dị ứng hiển thị rõ | | |
| PAT-07 | Bệnh nhân A đăng nhập, cố xem hồ sơ bệnh nhân B | Từ chối, chỉ xem được của mình | | |
| PAT-08 | Xem timeline lịch sử điều trị | Danh sách lần khám theo thứ tự thời gian | | |
| PAT-09 | Lần khám mới được tạo (không ghi đè lần khám cũ) | Visit mới có VisitId riêng, lịch sử lần cũ vẫn nguyên | | |

---

## 3. MOD_APP — Đặt lịch bệnh nhân

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| APP-01 | Bệnh nhân đặt lịch trực tuyến | Lịch được tạo trạng thái Pending, bệnh nhân nhận xác nhận | | |
| APP-02 | Lễ tân đặt lịch tại quầy cho bệnh nhân | Lịch được tạo trạng thái Confirmed, bệnh nhân vào hàng đợi sau check-in | | |

---

## 4. MOD_CHK — Tiếp đón & Hàng đợi

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| CHK-01 | Lễ tân check-in bệnh nhân theo SĐT | Trạng thái CheckedIn | | |
| CHK-02 | Bệnh nhân vào hàng đợi sau check-in | Queue item được tạo | | |
| CHK-03 | Nha sĩ gọi bệnh nhân tiếp theo | Trạng thái InProgress, Visit bắt đầu | | |
| CHK-04 | Nha sĩ kết thúc lần khám | Visit Completed, hóa đơn nháp tự động tạo | | |
| CHK-05 | Kiểm tra BOM gợi ý vật tư sau kết thúc khám, Phụ tá xác nhận | Hệ thống gợi ý danh sách; sau khi xác nhận thì StockIssue được tạo, tồn kho giảm | | |
| CHK-06 | Kết thúc khám khi kho thiếu vật tư | Cảnh báo thiếu kho, không cho hoàn tất (hoặc thông báo rõ) | | |
| CHK-07 | Lễ tân cập nhật trạng thái hàng đợi sau ghi nhận thông tin sức khỏe | Trạng thái đổi sang "Đã ghi nhận thông tin sức khỏe" | | |
| CHK-08 | Điều phối bệnh nhân sang X-quang | Trạng thái đổi sang "Đang chụp X-quang", RoutedTo = 'Xray' | | |
| CHK-09 | Điều phối bệnh nhân sang phòng nha sĩ | Trạng thái đổi sang "Chờ khám nha sĩ", RoutedTo = 'Dentist' | | |
| CHK-10 | Kiểm tra hàng đợi không truy cập công khai | Không có endpoint/màn hình công khai; Bệnh nhân không gọi được API hàng đợi | | |

---

## 5. MOD_FDI — Sơ đồ răng FDI

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| FDI-01 | Mở sơ đồ răng bệnh nhân người lớn | Hiển thị 32 răng, mã FDI đúng (11-18, 21-28, 31-38, 41-48) | | |
| FDI-02 | Mở sơ đồ răng bệnh nhân trẻ em | Hiển thị 20 răng, mã 51-55, 61-65, 71-75, 81-85 | | |
| FDI-03 | Chọn răng → gán bệnh lý | Màu/trạng thái răng thay đổi | | |
| FDI-04 | Chọn răng → chỉ định dịch vụ | Dịch vụ thêm vào TreatmentPlan | | |
| FDI-05 | Xem lịch sử trạng thái răng qua các lần khám | Timeline đúng thứ tự | | |
| FDI-06 | Bệnh nhân xem sơ đồ răng của mình | Chỉ xem, không sửa | | |

---

## 6. MOD_INV — Kho & Vật tư

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| INV-01 | Admin nhập kho vật tư mới | Tồn kho tăng, Batch được ghi | | |
| INV-02 | Cấu hình BOM: dịch vụ X cần 2 đơn vị vật tư Y | BOM lưu thành công | | |
| INV-03 | Hoàn tất dịch vụ X, Phụ tá xác nhận xuất kho → kiểm tra tồn kho vật tư Y | Giảm đúng 2 đơn vị sau khi xác nhận | | |
| INV-04 | Cố xuất vật tư khi tồn = 0 | Cảnh báo hoặc chặn | | |
| INV-05 | Cố xuất lô đã hết hạn | Từ chối, thông báo lô hết hạn | | |
| INV-06 | Phụ tá ghi tiêu hao phát sinh ngoài BOM | Lưu extra consumption | | |
| INV-07 | Tồn kho xuống dưới ngưỡng tối thiểu | Cảnh báo hiển thị | | |

---

## 7. MOD_BIL — Thanh toán & Hóa đơn

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| BIL-01 | Kết thúc khám → hóa đơn nháp tự động | Invoice Draft với đúng dịch vụ và đơn giá | | |
| BIL-02 | Thu ngân áp mã giảm giá | Tổng giảm đúng, không âm | | |
| BIL-03 | Thu ngân xác nhận thanh toán tiền mặt | Invoice Paid, Payment record được tạo | | |
| BIL-04 | Lập kế hoạch trả góp 3 kỳ | Tổng 3 kỳ = tổng hóa đơn | | |
| BIL-05 | Thu kỳ 1 trả góp | Kỳ 1 Paid, hiển thị còn lại đúng | | |
| BIL-06 | In/xuất biên lai PDF | File PDF tạo thành công | | |
| BIL-07 | Void hóa đơn nháp với lý do | Invoice Voided, lịch sử ghi lại | | |
| BIL-08 | Bệnh nhân xem hóa đơn của mình | Chỉ thấy hóa đơn của mình | | |

---

## 8. MOD_RPT — Báo cáo

| ID | Kịch bản test | Kết quả mong đợi | Kết quả thực tế | Pass/Fail |
|---|---|---|---|---|
| RPT-01 | Admin xem báo cáo doanh thu theo tháng | Số liệu đúng (chỉ giao dịch Paid) | | |
| RPT-02 | Nha sĩ xem báo cáo hiệu suất bản thân | Hiển thị đúng số ca của mình | | |
| RPT-03 | Nha sĩ cố xem báo cáo doanh thu | Từ chối hoặc không hiện menu | | |
| RPT-04 | Admin xem báo cáo xuất-nhập-tồn | Số liệu khớp phiếu nhập/xuất | | |
| RPT-05 | Admin xem báo cáo tiêu hao theo dịch vụ | Số liệu tiêu hao đúng theo BOM | | |

---

## 9. Kiểm thử phân quyền (Security UAT)

| ID | Kịch bản | Kết quả mong đợi | Pass/Fail |
|---|---|---|---|
| SEC-01 | Bệnh nhân A xem hồ sơ bệnh nhân B | ❌ Từ chối (403) | |
| SEC-02 | Lễ tân xem AuditLog | ❌ Từ chối (403) | |
| SEC-03 | Nha sĩ xem báo cáo doanh thu | ❌ Từ chối (403) | |
| SEC-04 | Phụ tá void hóa đơn | ❌ Từ chối (403) | |
| SEC-05 | Gọi API không có JWT token | ❌ 401 Unauthorized | |
| SEC-06 | Gọi API với JWT hết hạn | ❌ 401 Unauthorized | |

---

## 10. Tóm tắt kết quả

| Module | Tổng test | Pass | Fail | Partial |
|---|---|---|---|---|
| MOD_AUTH | 11 | | | |
| MOD_PAT | 9 | | | |
| MOD_APP | 2 | | | |
| MOD_CHK | 10 | | | |
| MOD_FDI | 6 | | | |
| MOD_INV | 7 | | | |
| MOD_BIL | 8 | | | |
| MOD_RPT | 5 | | | |
| Security | 6 | | | |
| **Tổng** | **64** | | | |

> Ngày test: ____________ | Người test: ____________ | Phiên bản: ____________
