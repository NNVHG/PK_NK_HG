using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Mục trong hàng đợi tiếp đón (DL-044, DL-045, DL-046, DL-047).
/// </summary>
public sealed class QueueEntry : BaseEntity
{
    public int QueueEntryId { get; set; }
    public int PatientId { get; set; }
    public int? AppointmentId { get; set; }
    public int? DentistId { get; set; }
    public int? VisitId { get; set; }
    public DateOnly QueueDate { get; set; }
    public int QueueNumber { get; set; }
    public bool IsPriority { get; set; }
    public DateTime CheckInTime { get; set; }
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;
    public string? Notes { get; set; }
    public bool ReturnedFromImaging { get; set; }
    public bool ReturnedFromSkipped { get; set; }
    public DateTime? PublicCalledAtUtc { get; set; }
    public int? PublicCalledByUserId { get; set; }
    public string? StatusHistoryJson { get; set; }

    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public User? Dentist { get; set; }
    public Visit? Visit { get; set; }
    public User? PublicCalledByUser { get; set; }
    [NotMapped]
    public ICollection<QueueStatusHistory> StatusHistories { get; set; } = new List<QueueStatusHistory>();

    public List<QueueStatusHistory> ReadStatusHistories()
        => string.IsNullOrEmpty(StatusHistoryJson)
            ? []
            : JsonSerializer.Deserialize<List<QueueStatusHistory>>(StatusHistoryJson) ?? [];

    public void AppendStatusHistory(QueueStatusHistory history)
    {
        var items = ReadStatusHistories();
        if (history.QueueStatusHistoryId <= 0)
            history.QueueStatusHistoryId = (items.Count == 0 ? 0 : items.Max(x => x.QueueStatusHistoryId)) + 1;
        history.QueueEntryId = QueueEntryId;
        items.Add(history);
        WriteStatusHistories(items);
    }

    public void SyncStatusHistoryFromCollection()
        => WriteStatusHistories(StatusHistories.ToList());

    private void WriteStatusHistories(List<QueueStatusHistory> items)
    {
        var id = 0;
        var hasImaging = false;
        var hasSkipped = false;
        foreach (var item in items)
        {
            if (item.QueueStatusHistoryId <= 0)
                item.QueueStatusHistoryId = ++id;
            else
                id = Math.Max(id, item.QueueStatusHistoryId);
            if (item.FromStatus == QueueStatus.InImaging && item.ToStatus == QueueStatus.Waiting)
                hasImaging = true;
            if (item.FromStatus == QueueStatus.Skipped && item.ToStatus == QueueStatus.Waiting)
                hasSkipped = true;
        }
        ReturnedFromImaging = hasImaging;
        ReturnedFromSkipped = hasSkipped;

        var last = items.LastOrDefault();
        if (last is not null &&
            last.ToStatus == QueueStatus.Calling &&
            (last.FromStatus == QueueStatus.Waiting || last.FromStatus == QueueStatus.Calling) &&
            last.Reason == QueueStatusHistory.PublicInvitationReason &&
            last.ChangedByUserId.HasValue &&
            DentistId == last.ChangedByUserId)
        {
            PublicCalledAtUtc = last.ChangedAt;
            PublicCalledByUserId = last.ChangedByUserId;
        }
        else
        {
            PublicCalledAtUtc = null;
            PublicCalledByUserId = null;
        }

        StatusHistoryJson = items.Count == 0 ? null : JsonSerializer.Serialize(items);
        StatusHistories = items;
    }
}
