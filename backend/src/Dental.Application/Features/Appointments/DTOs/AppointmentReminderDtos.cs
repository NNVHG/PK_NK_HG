namespace Dental.Application.Features.Appointments.DTOs;

public sealed record SimulateAppointmentRemindersRequest(bool Confirmed);
public sealed record SimulateAppointmentRemindersResponse(int EligibleCount, int CreatedCount, int AlreadyRemindedCount, DateTime WindowStart, DateTime WindowEnd);
public sealed record ReminderLogQueryRequest(int? AppointmentId = null, int Page = 1, int PageSize = 20);
public sealed record ReminderLogResponse(int Id, int AppointmentId, DateOnly AppointmentDate, TimeOnly SlotTime,
    string NotificationType, string Channel, string Status, DateTime SentAtUtc, string MessageContent);
