namespace Dental.Domain.Entities;

public sealed class NotificationLog
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string NotificationType { get; set; } = "APPOINTMENT_REMINDER_24H";
    public string Channel { get; set; } = "SMS_SIMULATED";
    public string RecipientPhone { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public string Status { get; set; } = "Sent";
    public DateTime SentAtUtc { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
