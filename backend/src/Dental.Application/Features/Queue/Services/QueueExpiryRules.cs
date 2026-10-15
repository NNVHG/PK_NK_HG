using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Queue.Services;

/// <summary>MOD_CHK / DL-260. All cutoffs are computed by the server in Vietnam time.</summary>
public static class QueueExpiryRules
{
    public static readonly TimeOnly Cutoff = new(23, 30);
    public static DateOnly LastExpiredDate(DateTime vietnamNow)
        => DateOnly.FromDateTime(vietnamNow).AddDays(TimeOnly.FromDateTime(vietnamNow) >= Cutoff ? 0 : -1);

    public static bool IsExpired(QueueEntry entry, DateOnly lastExpiredDate)
        => entry.QueueDate <= lastExpiredDate && entry.Status is (QueueStatus.Waiting or QueueStatus.Calling or QueueStatus.Skipped) &&
            entry.Visit is { Status: VisitStatuses.Created, StartedAt: null, IsLocked: false };

    public static QueueStatusHistory Cancel(QueueEntry entry, int? actorId, DateTime utcNow)
    {
        var previous = entry.Status;
        entry.Status = QueueStatus.Cancelled; entry.UpdatedAt = utcNow;
        entry.Visit!.Status = VisitStatuses.Cancelled; entry.Visit.EndedAt = utcNow; entry.Visit.UpdatedAt = utcNow;
        return new() { QueueEntryId = entry.QueueEntryId, FromStatus = previous, ToStatus = QueueStatus.Cancelled,
            ChangedByUserId = actorId, ChangedAt = utcNow, Reason = "Tự động hủy lượt chưa khám quá hạn (DL-260)" };
    }
}
