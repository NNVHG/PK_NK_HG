using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Một lần khám độc lập, giữ nguyên lịch sử các lần khám trước.</summary>
public sealed class Visit : BaseEntity
{
    public int VisitId { get; set; }
    public int PatientId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DentistId { get; set; }
    public int CreatedByUserId { get; set; }
    public string? Diagnosis { get; set; }
    public string? ClinicalNotes { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public int? LockedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public User? Dentist { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
