using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Interfaces;
namespace Dental.Infrastructure.Services;

public sealed class AppointmentReminderWorkerStatus : IAppointmentReminderWorkerStatus
{
    public const int IntervalMinutes = 30;
    private readonly object gate = new();
    private ReminderWorkerStatusResponse status = new("NotStarted", IntervalMinutes, null, null, null, null);
    public ReminderWorkerStatusResponse GetStatus() { lock (gate) return status; }
    public void Start(DateTime utcNow) { lock (gate) status = status with { State = "Running", LastStartedAtUtc = utcNow, LastCompletedAtUtc = null, CreatedCount = null, AlreadyRemindedCount = null }; }
    public void Complete(string state, DateTime utcNow, SimulateAppointmentRemindersResponse? result = null)
    { lock (gate) status = status with { State = state, LastCompletedAtUtc = utcNow, CreatedCount = result?.CreatedCount, AlreadyRemindedCount = result?.AlreadyRemindedCount }; }
    public void Stop() { lock (gate) status = status with { State = "Stopped" }; }
}
