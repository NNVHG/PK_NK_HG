using Dental.Domain.Common;
using Dental.Domain.Constants;

namespace Dental.Domain.Entities;

/// <summary>
/// Lịch hẹn khám & yêu cầu đặt lịch trực tuyến hợp nhất (DL-043, DL-050, DL-326–329, DL-380).
/// </summary>
public sealed class Appointment : BaseEntity
{
    public int AppointmentId { get; set; }
    public int? PatientId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int SlotDurationMinutes { get; set; } = 30;
    public string Status { get; set; } = AppointmentStatuses.Scheduled;
    public string Source { get; set; } = "Counter";
    public string? Notes { get; set; }
    public int RescheduleCount { get; set; }
    public string? RescheduleReason { get; set; }
    // Historical columns retained under DL337; new bookings cannot use emergency behavior.
    public bool IsEmergency { get; set; }
    public string? EmergencyReason { get; set; }
    public int? CreatedByUserId { get; set; }

    // Online booking request fields (Source == "Online")
    public Guid? ClientRequestId { get; set; }
    public string? IntentDigest { get; set; }
    public string? GuestFullName { get; set; }
    public string? GuestPhone { get; set; }
    public DateOnly? GuestDateOfBirth { get; set; }
    public string? GuestGender { get; set; }
    public string? GuestEmail { get; set; }
    public int? SubmittedByUserId { get; set; }
    public bool IsReturning { get; set; }
    public int Version { get; set; } = 1;
    public int? ReviewedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public bool CreatedAccount { get; set; }

    public Patient? Patient { get; set; }
    public User? CreatedByUser { get; set; }

    public string ToBookingRequestStatus()
        => Status is "Pending" or "Rejected" ? Status : "Approved";
}
