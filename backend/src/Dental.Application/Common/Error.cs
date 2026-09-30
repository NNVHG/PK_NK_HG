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

}
