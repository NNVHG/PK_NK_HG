namespace Dental.Application.Common;

/// <summary>
/// Mã lỗi nghiệp vụ kèm thông điệp.
/// Mã lỗi dạng GEN_xxx hoặc MOD_xxx theo quy ước dự án.
/// </summary>
public record Error(string Code, string Message)
{
    public static readonly Error None           = new(string.Empty, string.Empty);

    // Lỗi dùng chung (GEN_*)
    public static readonly Error NotFound       = new("GEN_001", "Không tìm thấy dữ liệu.");
    public static readonly Error Forbidden      = new("GEN_002", "Không đủ quyền truy cập.");
    public static readonly Error Validation     = new("GEN_003", "Dữ liệu đầu vào không hợp lệ.");
    public static readonly Error Conflict       = new("GEN_004", "Dữ liệu đã tồn tại.");
    public static readonly Error ServerError    = new("GEN_500", "Lỗi máy chủ nội bộ.");

    // Lỗi Auth (AUTH_*)
    public static readonly Error InvalidCredentials  = new("AUTH_001", "Số điện thoại hoặc mật khẩu không đúng.");
    public static readonly Error AccountLocked        = new("AUTH_002", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
    public static readonly Error Unauthorized        = new("AUTH_003", "Bạn chưa đăng nhập hoặc không có quyền truy cập.");
    public static readonly Error WrongOldPassword    = new("AUTH_004", "Mật khẩu cũ không chính xác.");
    public static readonly Error SameNewPassword     = new("AUTH_005", "Mật khẩu mới không được trùng với mật khẩu cũ.");
    public static readonly Error PhoneChangeNotAllowed = new("AUTH_006", "Chưa hỗ trợ thay đổi số điện thoại.");
    public static readonly Error RegistrationPhoneExists = new("AUTH_007", "Số điện thoại đã được đăng ký.");
    public static readonly Error RegistrationRoleUnavailable = new("AUTH_008", "Không thể tạo tài khoản lúc này. Vui lòng thử lại sau.");

    // Lỗi lần khám (MOD_PAT)
    public static readonly Error VisitAlreadyOpen = new("PAT_090", "Bệnh nhân đang có lần khám chưa kết thúc.");
    public static readonly Error PatientInactive = new("PAT_091", "Không thể tạo lần khám cho bệnh nhân đã ngừng hoạt động.");
    public static readonly Error MedicalHistoryVisitClosed = new("PAT_041", "Không thể ghi tiền sử cho lần khám đã kết thúc hoặc bị hủy.");
    public static readonly Error VitalSignsVisitClosed = new("PAT_052", "Không thể ghi sinh hiệu cho lần khám đã kết thúc hoặc bị hủy.");
    public static readonly Error DiagnosisVisitNotInProgress = new("PAT_111", "Chỉ có thể cập nhật chẩn đoán khi lần khám đang diễn ra.");
    public static readonly Error DiagnosisDentistMismatch = new("PAT_112", "Chỉ nha sĩ phụ trách hoặc Admin mới có quyền cập nhật chẩn đoán.");

    // Lỗi quản lý tài khoản nhân viên (MOD_MST)
    public static readonly Error StaffPhoneExists = new("MST_001", "Số điện thoại đã được sử dụng.");
    public static readonly Error InvalidStaffRole = new("MST_002", "Vai trò nhân viên không hợp lệ.");
    public static readonly Error LastActiveAdmin = new("MST_003", "Không thể hạ vai trò hoặc khóa Admin cuối cùng đang hoạt động.");
    public static readonly Error CannotLockSelf = new("MST_004", "Bạn không thể tự khóa tài khoản.");
    public static readonly Error ServiceCodeExists = new("MST_010", "Mã dịch vụ đã tồn tại.");
    public static readonly Error ServicePriceEffectiveDateInvalid = new("MST_011", "Ngày áp dụng giá mới không được trước ngày áp dụng giá gần nhất.");
    public static readonly Error ServiceInactive = new("MST_012", "Dịch vụ đã ngừng bán.");
    public static readonly Error ServicePriceUnavailable = new("MST_013", "Dịch vụ chưa có giá đang áp dụng.");

    // Lỗi đặt lịch khám (MOD_APP)
    public static readonly Error AppointmentNotFound           = new("APP_001", "Không tìm thấy lịch hẹn.");
    public static readonly Error AppointmentSlotFull           = new("APP_002", "Khung giờ này đã đủ số lượng đặt hẹn tối đa (100 khách). Vui lòng chọn khung giờ khác.");
    public static readonly Error AppointmentInvalidTime        = new("APP_003", "Giờ đặt hẹn không hợp lệ. Khung giờ khám: 08:00–11:30 và 13:30–16:30, slot 30 phút.");
    public static readonly Error AppointmentDateInPast         = new("APP_004", "Không thể đặt lịch hẹn trong quá khứ.");
    public static readonly Error AppointmentPatientHasActive   = new("APP_005", "Bệnh nhân đã có lịch hẹn chưa hoàn tất trong ngày này.");
    public static readonly Error AppointmentCannotCancel       = new("APP_006", "Chỉ có thể hủy lịch hẹn đang ở trạng thái chờ khám.");
    public static readonly Error AppointmentCannotReschedule   = new("APP_007", "Chỉ có thể đổi lịch hẹn đang ở trạng thái chờ khám.");
    public static readonly Error AppointmentCannotCheckIn      = new("APP_008", "Lịch hẹn không ở trạng thái chờ khám hoặc đã được tiếp đón.");

    // Lỗi tiếp đón & hàng đợi (MOD_CHK)
    public static readonly Error QueueEntryNotFound            = new("CHK_001", "Không tìm thấy lượt tiếp đón trong hàng đợi.");
    public static readonly Error QueueEntryAlreadyOpen         = new("CHK_002", "Bệnh nhân đang có số thứ tự trong hàng đợi chưa hoàn tất.");
    public static readonly Error QueueInvalidStateTransition   = new("CHK_003", "Chuyển trạng thái hàng đợi không hợp lệ.");
    public static readonly Error QueueTransitionForbidden      = new("CHK_004", "Bạn không có quyền thực hiện chuyển trạng thái này.");
    public static readonly Error QueueDentistRequired          = new("CHK_005", "Bắt buộc chỉ định Nha sĩ khi bắt đầu khám.");
    public static readonly Error QueueCannotRevertCompleted    = new("CHK_006", "Lượt khám đã hoàn tất, không thể thay đổi trạng thái.");
    public static readonly Error QueueCannotRevertCancelled    = new("CHK_007", "Lượt khám đã bị hủy, không thể thay đổi trạng thái.");


}
