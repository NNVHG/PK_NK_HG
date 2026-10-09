namespace Dental.Domain.Constants;

/// <summary>
/// Trạng thái lịch hẹn theo DL-043, DL-044.
/// </summary>
public static class AppointmentStatuses
{
    public const string Scheduled  = "Scheduled";  // Đã đặt lịch
    public const string CheckedIn  = "CheckedIn";  // Đã tiếp đón tại quầy
    public const string Cancelled  = "Cancelled";  // Đã hủy
    public const string Completed  = "Completed";  // Đã hoàn tất khám

    public static readonly string[] All = [Scheduled, CheckedIn, Cancelled, Completed];
}
