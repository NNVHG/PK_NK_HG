using Dental.Domain.Enums;

namespace Dental.Domain.Constants;

/// <summary>
/// Chuỗi hiển thị tiếng Việt tách riêng cho trạng thái hàng đợi (DL-014, DL-047).
/// </summary>
public static class QueueStatusDescriptions
{
    public const string Waiting = "Đang chờ";
    public const string InConsultation = "Đang khám";
    public const string Completed = "Khám xong";
    public const string Cancelled = "Đã hủy";
    public const string InImaging = "Đang chụp ảnh";
    public const string Calling = "Đang mời vào phòng";
    public const string Skipped = "Vắng mặt";

    public static string GetDescription(QueueStatus status) => status switch
    {
        QueueStatus.Waiting => Waiting,
        QueueStatus.InConsultation => InConsultation,
        QueueStatus.Completed => Completed,
        QueueStatus.Cancelled => Cancelled,
        QueueStatus.InImaging => InImaging,
        QueueStatus.Calling => Calling,
        QueueStatus.Skipped => Skipped,
        _ => "Không xác định"
    };
}
