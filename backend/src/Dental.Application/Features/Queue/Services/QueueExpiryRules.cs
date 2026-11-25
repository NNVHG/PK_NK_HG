using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Queue.Services;

/// <summary>MOD_CHK / DL-381. Mọi mốc qua ngày mới 00:00 được tính theo giờ Việt Nam tại máy chủ.</summary>
public static class QueueExpiryRules
{
    public const string DefaultAutoRolloverReason =
        "Tự động đóng lượt chưa khám qua ngày mới và chuyển sang yêu cầu đặt lịch chờ duyệt (DL-381)";

    public static DateOnly LastExpiredDate(DateTime vietnamNow)
        => DateOnly.FromDateTime(vietnamNow).AddDays(-1);

    public static bool IsExpired(QueueEntry entry, DateOnly lastExpiredDate)
        => entry.QueueDate <= lastExpiredDate && entry.Status is (QueueStatus.Waiting or QueueStatus.Calling or QueueStatus.Skipped) &&
            entry.Visit is { Status: VisitStatuses.Created, StartedAt: null, IsLocked: false };

    public static QueueStatusHistory Cancel(QueueEntry entry, int? actorId, DateTime utcNow, string? reason = null)
    {
        var previous = entry.Status;
        entry.Status = QueueStatus.Cancelled; entry.UpdatedAt = utcNow;
        entry.Visit!.Status = VisitStatuses.Cancelled; entry.Visit.EndedAt = utcNow; entry.Visit.UpdatedAt = utcNow;
        return new()
        {
            QueueEntryId = entry.QueueEntryId,
            FromStatus = previous,
            ToStatus = QueueStatus.Cancelled,
            ChangedByUserId = actorId,
            ChangedAt = utcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? DefaultAutoRolloverReason : reason
        };
    }
}
