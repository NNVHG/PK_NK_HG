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

    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public User? Dentist { get; set; }
    public Visit? Visit { get; set; }
    public ICollection<QueueStatusHistory> StatusHistories { get; set; } = new List<QueueStatusHistory>();
}
