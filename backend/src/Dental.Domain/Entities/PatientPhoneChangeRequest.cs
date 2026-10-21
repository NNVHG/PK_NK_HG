namespace Dental.Domain.Entities;

// A counter suggestion, never an OTP proof or permission to change identity.
public sealed class PatientPhoneChangeRequest
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int AppointmentId { get; set; }
    public int CreatedByUserId { get; set; }
    public string CurrentPhone { get; set; } = string.Empty;
    public string NewPhone { get; set; } = string.Empty;
    public int TokenVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
