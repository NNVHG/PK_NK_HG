using Dental.Domain.Common;
using Dental.Domain.Constants;

namespace Dental.Domain.Entities;

/// <summary>
/// Lịch hẹn khám (DL-043, DL-050).
/// </summary>
public sealed class Appointment : BaseEntity
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int? DentistId { get; set; }
    public string Status { get; set; } = AppointmentStatuses.Scheduled;
    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }

    public Patient Patient { get; set; } = null!;
    public User? Dentist { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
