using System.Globalization;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.Appointments.Services;

public sealed class AppointmentReminderService(IAppointmentReminderRepository repository, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock, TimeProvider time)
{
    public async Task<Result<SimulateAppointmentRemindersResponse>> SimulateAsync(int actorId, string? role, bool confirmed, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<SimulateAppointmentRemindersResponse>.Failure(Error.Forbidden);
        if (!confirmed) return Result<SimulateAppointmentRemindersResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var utcNow = time.GetUtcNow().UtcDateTime;
                var now = clock.ToVietnamTime(utcNow);
                var start = now.AddHours(23.5); var end = now.AddHours(24.5);
                var candidates = await repository.GetCandidatesAsync(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), transactionCt);
                var eligible = 0; var logs = new List<NotificationLog>();
                foreach (var a in candidates)
                {
                    var scheduledAt = a.AppointmentDate.ToDateTime(a.SlotTime);
                    if (a.Status != AppointmentStatuses.Scheduled || scheduledAt < start || scheduledAt > end ||
                        (scheduledAt - clock.ToVietnamTime(a.CreatedAt)).TotalHours < 24) continue;
                    eligible++;
                    if (await repository.HasOccurrenceAsync(a.AppointmentId, a.AppointmentDate, a.SlotTime, transactionCt)) continue;
                    var dentist = string.IsNullOrWhiteSpace(a.Dentist?.FullName) ? "nha sĩ được phân công" : $"BS {a.Dentist.FullName}";
                    var message = $"Nha Khoa HG xin nhắc: Quý khách {a.Patient.FullName} có lịch hẹn khám vào lúc {scheduledAt.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)} với {dentist} tại Phòng khám. Vui lòng đến trước 10 phút. Hotline hỗ trợ: 0901234567.";
                    var log = new NotificationLog { AppointmentId = a.AppointmentId, PatientId = a.PatientId, AppointmentDate = a.AppointmentDate,
                        SlotTime = a.SlotTime, RecipientPhone = a.Patient.Phone, MessageContent = message, SentAtUtc = utcNow };
                    await repository.AddAsync(log, transactionCt); logs.Add(log);
                }
                if (logs.Count > 0)
                {
                    await uow.SaveChangesAsync(transactionCt);
                    await audit.LogAsync("MOD_APP_REMINDERS_SIMULATED", "NotificationLog", userId: actorId,
                        detail: JsonSerializer.Serialize(new { count = logs.Count, notificationIds = logs.Select(l => l.Id) }), ipAddress: ip, ct: transactionCt);
                }
                return Result<SimulateAppointmentRemindersResponse>.Success(new(eligible, logs.Count, eligible - logs.Count, start, end));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<SimulateAppointmentRemindersResponse>.Failure(Error.AppointmentReminderConcurrent); }
    }
    public async Task<Result<PagedResult<ReminderLogResponse>>> GetLogsAsync(string? role, ReminderLogQueryRequest query, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<PagedResult<ReminderLogResponse>>.Failure(Error.Forbidden);
        var (rows, total) = await repository.GetLogsAsync(query.AppointmentId, query.Page, query.PageSize, ct);
        return Result<PagedResult<ReminderLogResponse>>.Success(PagedResult<ReminderLogResponse>.Create(
            rows.Select(l => new ReminderLogResponse(l.Id, l.AppointmentId, l.AppointmentDate, l.SlotTime, l.NotificationType, l.Channel, l.Status, l.SentAtUtc, l.MessageContent)).ToList(), total, query.Page, query.PageSize));
    }
}
