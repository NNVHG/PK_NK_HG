namespace Dental.Domain.Enums;

/// <summary>
/// Trạng thái hàng đợi theo DL-047 (enum int với 5 giá trị).
/// 1 = Waiting, 2 = InConsultation, 3 = Completed, 4 = Cancelled, 5 = InImaging.
/// </summary>
public enum QueueStatus
{
    Waiting = 1,          // Đang chờ
    InConsultation = 2,   // Đang khám
    Completed = 3,        // Khám xong (hoàn tất)
    Cancelled = 4,        // Đã hủy
    InImaging = 5         // Đang chụp ảnh
}
