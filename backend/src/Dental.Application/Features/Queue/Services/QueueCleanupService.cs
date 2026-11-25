using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.Queue.Services;

/// <summary>
/// Tự động đóng các lượt chưa bắt đầu khám khi qua ngày mới (00:00 giờ Việt Nam)
/// và chuyển thành yêu cầu đặt lịch trực tuyến chờ duyệt cho ngày hôm sau (DL-381).
/// </summary>
public sealed class QueueCleanupService(
    IQueueRepository queues,
    IAppointmentRepository appointments,
    IUnitOfWork uow,
    IAuditLogger audit,
    VietnamClock clock)
{
    public Task<Result<QueueCleanupResponse>> RunAutomaticallyAsync(CancellationToken ct = default)
        => CleanupAsync(ct);

    private async Task<Result<QueueCleanupResponse>> CleanupAsync(CancellationToken ct)
    {
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var vietnamNow = clock.Now;
                var today = DateOnly.FromDateTime(vietnamNow);
                var cutoff = QueueExpiryRules.LastExpiredDate(vietnamNow);
                var candidates = await queues.GetExpiredUnstartedAsync(cutoff, transactionCt);
                if (candidates.Count == 0)
                    return Result<QueueCleanupResponse>.Success(new(0, 0, cutoff));

                var schedule = (await appointments.GetScheduleAsync(transactionCt))?.Schedule ?? ClinicSchedule.Default;
                var allSlots = schedule.Slots();
                if (allSlots.Length == 0)
                    allSlots = ClinicSchedule.Default.Slots();

                var nowTime = TimeOnly.FromDateTime(vietnamNow);
                var targetDate = today;
                var futureSlots = allSlots.Where(t => t > nowTime).ToArray();
                if (futureSlots.Length == 0)
                {
                    targetDate = today.AddDays(1);
                    futureSlots = allSlots;
                }

                var rolledOverPatientIds = new HashSet<int>();
                var cancelledCount = 0;
                var rolledOverCount = 0;

                foreach (var entry in candidates)
                {
                    if (!QueueExpiryRules.IsExpired(entry, cutoff)) continue;

                    var patient = entry.Patient;
                    var trimmedName = patient?.FullName?.Trim();
                    var trimmedPhone = patient?.Phone?.Trim();
                    var canRollover = patient is { IsActive: true }
                        && !string.IsNullOrWhiteSpace(trimmedName)
                        && !string.IsNullOrWhiteSpace(trimmedPhone)
                        && patient.DateOfBirth != default
                        && !rolledOverPatientIds.Contains(patient.PatientId)
                        && !await appointments.HasActiveAppointmentOnDateAsync(patient.PatientId, targetDate, transactionCt)
                        && !await appointments.HasPendingOnlineRequestAsync(trimmedPhone, trimmedName, patient.DateOfBirth, targetDate, transactionCt);

                    Appointment? bookingRequest = null;
                    TimeOnly? chosenSlot = null;
                    if (canRollover)
                    {
                        foreach (var slot in futureSlots)
                        {
                            var dbCount = await appointments.GetSlotBookingCountAsync(
                                targetDate, slot, transactionCt, durationMinutes: schedule.DurationMinutes);
                            if (dbCount < AppointmentSlots.MaxBookingsPerSlot)
                            {
                                chosenSlot = slot;
                                break;
                            }
                        }
                    }

                    if (chosenSlot is { } availableSlot)
                    {
                        var isReturning = await appointments.HasCompletedVisitForPatientAsync(patient!.PatientId, transactionCt);

                        bookingRequest = new Appointment
                        {
                            Source = "Online",
                            Status = "Pending",
                            PatientId = null,
                            SubmittedByUserId = null,
                            ClientRequestId = DeterministicGuid($"queue-rollover:{entry.QueueEntryId}:{entry.QueueDate:yyyy-MM-dd}"),
                            IntentDigest = Sha256Hex($"queue-rollover:{entry.QueueEntryId}:{entry.QueueDate:yyyy-MM-dd}:{targetDate:yyyy-MM-dd}:{availableSlot:HH:mm}"),
                            GuestFullName = trimmedName,
                            GuestPhone = trimmedPhone,
                            GuestDateOfBirth = patient.DateOfBirth,
                            GuestGender = NormalizeGender(patient.Gender),
                            GuestEmail = string.IsNullOrWhiteSpace(patient.Email) ? null : patient.Email.Trim(),
                            AppointmentDate = targetDate,
                            SlotTime = availableSlot,
                            SlotDurationMinutes = schedule.DurationMinutes,
                            IsReturning = isReturning,
                            Notes = BuildRolloverNotes(entry),
                            Version = 1,
                            CreatedAt = clock.UtcNow
                        };

                        await appointments.AddAsync(bookingRequest, transactionCt);
                        rolledOverPatientIds.Add(patient.PatientId);
                    }

                    var noCapacity = canRollover && chosenSlot is null;
                    var reason = bookingRequest is not null
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            "Tự động đóng lượt chưa khám ngày {0:dd/MM/yyyy} và chuyển sang yêu cầu đặt lịch chờ duyệt ngày {1:dd/MM/yyyy} (DL-381)",
                            entry.QueueDate,
                            targetDate)
                        : noCapacity
                            ? "Tự động đóng lượt chưa khám do ngày đích đã hết chỗ. Lễ tân cần liên hệ và đặt lịch lại (DL-381)."
                            : "Tự động đóng lượt chưa khám qua ngày mới (bệnh nhân đã có lịch/yêu cầu cùng ngày hoặc hồ sơ không hợp lệ) (DL-381)";

                    var history = QueueExpiryRules.Cancel(entry, null, clock.UtcNow, reason);
                    await queues.AddStatusHistoryAsync(history, transactionCt);
                    await uow.SaveChangesAsync(transactionCt);

                    await audit.LogRequiredAsync(
                        AuditActions.QueueStatusChanged,
                        "QueueEntry",
                        entry.QueueEntryId,
                        null,
                        JsonSerializer.Serialize(new
                        {
                            action = "AutoRolloverToBookingRequest",
                            fromStatus = (int?)history.FromStatus,
                            toStatus = (int)history.ToStatus,
                            visitId = entry.VisitId,
                            rolledOverToBookingRequest = bookingRequest is not null,
                            bookingRequestId = bookingRequest?.AppointmentId,
                            noCapacity,
                            targetDate = canRollover ? targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
                            source = "Automatic"
                        }),
                        ct: transactionCt);

                    if (bookingRequest is not null)
                    {
                        await audit.LogRequiredAsync(
                            "MOD_APP_REQUEST_AUTO_ROLLOVER",
                            "BookingRequest",
                            bookingRequest.AppointmentId,
                            null,
                            JsonSerializer.Serialize(new
                            {
                                fromQueueEntryId = entry.QueueEntryId,
                                fromQueueDate = entry.QueueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                targetDate = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                targetSlot = bookingRequest.SlotTime.ToString("HH:mm", CultureInfo.InvariantCulture)
                            }),
                            ct: transactionCt);
                        rolledOverCount++;
                    }

                    cancelledCount++;
                }

                return Result<QueueCleanupResponse>.Success(new(cancelledCount, rolledOverCount, cutoff));
            }, ct);
        }
        catch (ConcurrentOperationException)
        {
            return Result<QueueCleanupResponse>.Failure(Error.QueueConcurrent);
        }
    }

    private static string NormalizeGender(string? gender)
    {
        var g = gender?.Trim();
        if (string.Equals(g, "Nam", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g, "Male", StringComparison.OrdinalIgnoreCase))
            return "Male";
        if (string.Equals(g, "Nữ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g, "Female", StringComparison.OrdinalIgnoreCase))
            return "Female";
        return "Other";
    }

    private static string BuildRolloverNotes(QueueEntry entry)
    {
        var prefix = string.Format(
            CultureInfo.InvariantCulture,
            "Tự động chuyển từ hàng đợi ngày {0:dd/MM/yyyy} (số A-{1:D3}) do chưa kịp khám.",
            entry.QueueDate,
            entry.QueueNumber);

        var extra = entry.Notes?.Trim();
        if (string.IsNullOrWhiteSpace(extra))
            return prefix;

        var combined = $"{prefix} {extra}";
        return combined.Length <= 500 ? combined : combined[..500];
    }

    private static Guid DeterministicGuid(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string Sha256Hex(string seed)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
}
