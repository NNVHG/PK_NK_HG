namespace Dental.Application.Features.Appointments.DTOs;

public sealed record ReminderWorkerStatusResponse(string State, int IntervalMinutes,
    DateTime? LastStartedAtUtc, DateTime? LastCompletedAtUtc, int? CreatedCount, int? AlreadyRemindedCount);
