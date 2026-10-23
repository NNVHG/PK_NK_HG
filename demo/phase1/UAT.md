# Phiếu nghiệm thu Phase 1

Ngày/giờ: __________ · Người nghiệm thu: Gia __________ · Commit: __________

Máy/trình duyệt: __________ · Database demo: `dental_phase1_demo` · Phiên bản file: 24/10/2026 · Commit mã V4–V6: `15f6c2b`.

Điền **Đạt / Không đạt / Chưa chạy / Không áp dụng**, kèm bằng chứng. Không dùng thông tin bệnh nhân thật trong ảnh.

| Mục | Kỳ vọng | Kết quả | Ảnh / ghi chú lỗi |
|---|---|---|---|
| Trang công khai | Mở trang chủ khi đăng xuất, đặt lịch không OTP | Chưa chạy | |
| Tiếp nhận | Duyệt lịch, CCCD/SĐT tại quầy, check-in sinh Visit/số | Chưa chạy | |
| Seed cá nhân | Sáu tài khoản Patient độc lập dùng CCCD giả, có thể chung SĐT; không có dữ liệu thật | Chưa chạy | |
| Seed lịch/ca | Lịch bốn trạng thái, một Visit đang khám, hai lượt Waiting và một InConsultation | Chưa chạy | |
| Tiếp nhận | Tự tải lịch hôm nay/hàng chờ; tìm tên/SĐT/mã hồ sơ và CCCD; phân trang/đang tải/rỗng/lỗi | Chưa chạy | |
| Ghi chú tiếp đón | Notes rỗng hợp lệ; 500 ký tự được nhận; 501 bị chặn với lỗi trường | Chưa chạy | |
| Tài khoản mới | CCCD đăng nhập; OTP và bắt đổi mật khẩu | Chưa chạy | |
| Cùng SĐT | Hai tài khoản riêng, không thấy dữ liệu của nhau | Chưa chạy | |
| Quyền truy cập | Patient không xem hàng đợi/audit/hồ sơ khác | Chưa chạy | |
| TV/hàng đợi | Gọi lượt Waiting trên màn nhân viên; TV cập nhật và phát tiếng Việt/âm báo | Chưa chạy | |
| Điều phối | Gọi số, bắt đầu/kết thúc khám và trạng thái ghế đúng | Chưa chạy | |
| An toàn | Cảnh báo tiền sử/dị ứng/huyết áp có hiển thị cho nhân viên | Chưa chạy | |
| FDI | Bộ 32/20 răng; seed có sâu răng tổng hợp ở răng 16; chọn mặt/lưu/xem lịch sử | Chưa chạy | |
| Ảnh | Tải ảnh vào ca hợp lệ, mở/thu phóng theo quyền | Chưa chạy | |
| Kê đơn | Lưu đơn, in A5/QR, quyền xem đúng; thuốc tồn thiếu bị chặn giao | Chưa chạy | |
| Thu nhiều đợt | 40.000 + 60.000 cho dịch vụ 100.000; hết nợ | Chưa chạy | |
| Chuyển khoản | Xác nhận thủ công có đối soát/tham chiếu; không thu trùng | Chưa chạy | |
| Giảm giá | Phần trăm chỉ giảm dịch vụ, làm tròn nguyên đồng | Chưa chạy | |
| Kho | Nhập 10, xác nhận tiêu hao 2 còn 8; có audit | Chưa chạy | |
| Báo cáo | Đúng thực thu theo kỳ và công nợ hiện tại | Chưa chạy | |
| Di động | Rộng 320–390px không tràn ngang, thao tác được | Chưa chạy | |
| Chạy lại | Stop → Start giữ dữ liệu và mật khẩu đã đổi | Chưa chạy | |

## Lỗi cần xử lý

| Mã | Vai trò / bước | Mong đợi | Thực tế | Cách tái hiện | Mức độ | Bằng chứng |
|---|---|---|---|---|---|---|
| | | | | | | |

## Kết luận của Gia

- [ ] Phase 1 demo đạt; ghi rõ phạm vi được nghiệm thu: __________
- [ ] Cần sửa và demo lại; danh sách lỗi: __________
- [ ] Chưa nghiệm thu.

Chưa có lựa chọn/xác nhận của Gia thì trạng thái vẫn **Chờ UAT**, không tự mở Phase 2.
