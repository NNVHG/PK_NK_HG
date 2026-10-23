# Cập nhật kiểm chứng — 24/10/2026

- Seed V4 hiện được gọi từ DatabaseSeeder khi Seed:Phase1=true; Start.ps1 đặt cờ này cho database demo riêng. Tạo sáu Patient cá nhân giả dùng chung SĐT, lịch bốn trạng thái, Visit/queue, tiền sử/dị ứng, sinh hiệu và tình trạng FDI răng 16.
- V5–V6: màn tiếp đón tự nạp lịch/hàng chờ, phân trang/tìm nhanh (CCCD chỉ băm tại client), ghi chú tùy chọn 0–500 ký tự và lỗi theo trường.
- Commit mã: 15f6c2b; chưa chạy database hoặc browser UAT. Docker Desktop API trả permission denied nên không chạy harness PostgreSQL; không seed dữ liệu vào DB nào.
- Kiểm chứng mới: frontend riêng V5/V6 19/19; npm run build đạt 445 modules; Infrastructure build 0 lỗi/3 cảnh báo; PostgreSQL harness build với BuildProjectReferences=false đạt 0 lỗi/4 cảnh báo. Nhóm .NET Queue/Patients/Auth đã đạt 714/714 sau Q-367a; lần chạy lại bị treo sau discovery. Toàn bộ Node: 1128 ca, 1099 đạt, 29 lỗi (xem reports/20261024-0022-V1-V7.md). Chưa nghiệm thu Gia.
- Q-368 đang chờ quyết định về việc sửa 29 lỗi ngoài V4–V6; không chạy lát cắt kế tiếp hoặc Phase 2 trước khi Gia trả lời.

---

# Báo cáo chuẩn bị Phase 1 — 23/10/2026

## Phạm vi bàn giao

- Gỡ API/service/repository/policy và UI liên kết nhiều hồ sơ; bỏ bộ chọn hồ sơ ở đặt lịch/lịch hẹn; đổi SĐT chỉ xác nhận một hồ sơ.
- `GET /api/patients/me` và `/my-record` đưa Patient tới đúng hồ sơ của mình; backend kiểm quyền sở hữu như trước.
- Mô hình User–Patient một-một, unique `Patients.UserId`; SĐT Patient vẫn không unique. Giữ các migration cũ. Bổ sung `RenameLegacyAccountConversionMarker` và `EnforceIndividualPatientOwnership`.
- Chuyển đổi legacy chạy giữa hai migration; đọc detached để EF không tự thay liên kết cũ trước preflight. Toàn bộ thay đổi tài khoản/dữ liệu/audit trong transaction serializable. Chưa thực thi trên database phòng khám thật.
- Sửa snapshot cũ đặt nhầm hai cờ tài khoản vào DentalChair. Snapshot hiện khớp model.
- Tích hợp phần bỏ cấp cứu đã chốt DL337 từ commit `0f1551cf` vào bản demo; giữ schema lịch sử.
- Seed Development: nhân viên, hai Patient riêng dùng cùng SĐT, một ghế/dịch vụ/vật tư mẫu. Có script chuẩn bị/chạy/dừng và hướng dẫn UAT đủ 52 mã.

## Bằng chứng thực hiện

| Kết quả | Lệnh / thao tác | Bằng chứng / giới hạn |
|---|---|---|
| Đạt | `dotnet restore backend/DentalClinic.sln --disable-parallel` | Khôi phục package đã khai báo; không thêm package |
| Đạt | `dotnet build backend/DentalClinic.sln --no-restore -m:1 /p:UseSharedCompilation=false` | 0 lỗi; gồm biên dịch project kiểm thử, chưa chạy test |
| Đạt | Build `backend/tests/CashPayment.Postgres/CashPayment.Postgres.csproj` | 0 lỗi sau đồng bộ chữ ký API chuyển khoản; chưa chạy smoke |
| Đạt | `npm ci --ignore-scripts --no-audit --no-fund` và `npm run build` | TypeScript/Vue build đạt, 444 modules |
| Đạt | `dotnet ef migrations has-pending-model-changes` | EF báo model không có thay đổi chưa được migration ghi nhận |
| Đạt | `Start.ps1` trên database demo mới | PostgreSQL riêng healthy; migration/seed hoàn tất; catalog API và website phản hồi HTTP 200 |
| Đạt | `Stop.ps1` rồi `Start.ps1` | Đúng tiến trình/container demo dừng và chạy lại; không xóa volume |
| Đạt | Rà mã nguồn ứng dụng | Không còn chức năng/nhãn nhiều hồ sơ; `font-family` chỉ là CSS, migration cũ giữ phục vụ nâng cấp |
| Đạt | Danh sách nghiệm thu | FUNCTIONS.csv gồm đúng 52 mã chính thức; tất cả để Chưa chạy |
| Chưa chạy | xUnit, Node Test Runner, PostgreSQL smoke | Lượt này chuẩn bị/build bộ demo; không công bố số test PASS hoặc dùng số cũ làm kết quả mới |
| Chưa chạy | Nghiệm thu trình duyệt xuyên suốt/di động | Gia ghi kết quả trong UAT.md; chưa có bằng chứng UAT 320–390px |
| Chưa chạy | Bulk conversion trên dữ liệu phòng khám | Chỉ startup DB demo trống và chạy lại đã thực hiện; đường nâng cấp có dữ liệu legacy cần kiểm chứng riêng trước áp dụng thật |

## Cảnh báo build và phạm vi kiểm thử

NuGet hiện cảnh báo NU1902/NU1903 cho dependency có sẵn `System.Security.Cryptography.Pkcs` 9.0.0. Không tự nâng package ngoài phạm vi đã duyệt. Có cảnh báo tham số `passwords` chưa dùng trong BookingApprovalRepository. Các cảnh báo này không làm build thất bại nhưng cần xử lý trước khi sử dụng thật.

Các fixture kiểm thử của chức năng đã bỏ được gỡ: PatientAccountLink (unit/HTTP/Node), FamilyBooking (HTTP/Node), hai trang browser fixture cũ; smoke BookingRequest/BookingApproval/PatientPhoneChange cũ dựa trên OTP đặt lịch hoặc nhiều hồ sơ cũng được gỡ khỏi runner và các cờ gọi tương ứng. Đã cập nhật unit/model/controller, frontend đổi SĐT/lịch hẹn và fixture sở hữu cá nhân còn dùng. **Không coi việc gỡ fixture là kiểm thử đạt**; cần bổ sung/kiểm chứng lại HTTP cho hợp đồng mới khi Gia yêu cầu lượt kiểm thử.

Không hợp nhất toàn bộ `main` ở commit `14c8a13`: commit đó thay chuỗi migration bằng baseline và đặt Patient.Phone unique, không phù hợp dữ liệu nâng cấp và yêu cầu dùng chung SĐT. Bộ demo được giữ trên nhánh riêng, không merge/push.

## Trạng thái

**Đã chuẩn bị mã nguồn và bộ file demo; đã build và khởi động môi trường riêng. Chờ Gia UAT Phase 1.** Các file quyết định/lịch sử cũ được giữ để truy vết, không dùng làm hướng dẫn vận hành hiện hành. Phase 2 chưa được mở.
