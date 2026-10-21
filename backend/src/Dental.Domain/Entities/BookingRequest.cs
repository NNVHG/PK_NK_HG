using Dental.Domain.Common;
namespace Dental.Domain.Entities;

/// <summary>MOD_APP: Pending reservation before a User/Patient is created, DL326–328.</summary>
public sealed class BookingRequest : BaseEntity
{
    public int Id { get; set; }
    public Guid ClientRequestId { get; set; }
    public string IntentDigest { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int SlotDurationMinutes { get; set; }
    public int? DentistId { get; set; }
    public int? VerifiedPatientId { get; set; }
    public int? SubmittedByUserId { get; set; }
    public bool IsReturning { get; set; }
    public string Status { get; set; } = "Pending";
    public int Version { get; set; } = 1;
    public int? ReviewedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public int? ApprovedAppointmentId { get; set; }
    public bool CreatedAccount { get; set; }
}
