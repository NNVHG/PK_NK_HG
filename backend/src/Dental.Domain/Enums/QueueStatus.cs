namespace Dental.Domain.Enums;

/// <summary>
/// Queue status: preserve persisted values 1..5; Calling is the separate invitation step (DL-214/DL-258).
/// </summary>
public enum QueueStatus
{
    Waiting = 1,          // Đang chờ
    InConsultation = 2,   // Đang khám
    Completed = 3,        // Khám xong (hoàn tất)
    Cancelled = 4,        // Đã hủy
    InImaging = 5,        // Đang chụp ảnh
    Calling = 6,         // Đang mời vào phòng, chưa bắt đầu khám
    Skipped = 7          // Vắng mặt trước khi khám; giữ InImaging=5 (DL-259)
}
