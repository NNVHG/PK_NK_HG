using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Nhật ký chuyển trạng thái hàng đợi (DL-047, append-only).
/// </summary>
public sealed class QueueStatusHistory
{
    public int QueueStatusHistoryId { get; set; }
    public int QueueEntryId { get; set; }
    public QueueStatus? FromStatus { get; set; }
    public QueueStatus ToStatus { get; set; }
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    public QueueEntry QueueEntry { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
