using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Application.Features.MOD_MST.Services;

namespace Dental.Application.Features.Appointments.Services;

public sealed class AppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly VietnamClock _vietnamClock;

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        VietnamClock vietnamClock)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _vietnamClock = vietnamClock;
    }

    public async Task<Result<AppointmentResponse>> CreateAppointmentAsync(
        int actorUserId,
        string? actorRoleCode,
        CreateAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Patient))
            return Result<AppointmentResponse>.Failure(Error.Forbidden);
        if (request.IsEmergency && actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Result<AppointmentResponse>.Failure(Error.Forbidden);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(
                transactionCt => CreateCoreAsync(actorUserId, actorRoleCode, request, ipAddress, transactionCt), ct);
        }
        catch (ConcurrentOperationException) { return Result<AppointmentResponse>.Failure(Error.AppointmentConcurrent); }
    }

    private async Task<Result<AppointmentResponse>> CreateCoreAsync(
        int actorUserId, string actorRoleCode, CreateAppointmentRequest request, string? ipAddress, CancellationToken ct)
    {
        var snapshot = await _appointmentRepository.GetScheduleAsync(ct) ?? new ScheduleSnapshot(ClinicSchedule.Default, "", true);
        if (request.ExpectedScheduleRevision is not null && request.ExpectedScheduleRevision != snapshot.Revision)
            return Result<AppointmentResponse>.Failure(new Error("APP_SCHEDULE", "Cấu hình giờ khám đã đổi. Tải lại và chọn lại khung giờ trước khi gửi."));
        var validTime = request.IsEmergency
            ? request.SlotTime.Ticks % TimeSpan.TicksPerMinute == 0
            : snapshot.Schedule.IsValidSlot(request.SlotTime);
        var reason = request.EmergencyReason?.Trim();
        if (!validTime || request.Notes?.Length > 500 || request.DentistId <= 0 ||
            (request.IsEmergency && (reason is null || reason.Length is < 5 or > 255)) ||
            (!request.IsEmergency && (request.EmergencyReason is not null || request.ConfirmedDentistConflictAppointmentId.HasValue)))
            return Result<AppointmentResponse>.Failure(Error.Validation);
        var patient = await _patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
            return Result<AppointmentResponse>.Failure(Error.NotFound);
        if (actorRoleCode == RoleCodes.Patient && patient.UserId != actorUserId)
            return Result<AppointmentResponse>.Failure(Error.NotFound);
        if (!patient.IsActive)
            return Result<AppointmentResponse>.Failure(Error.PatientInactive);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        // DL-229 permits a staff-entered emergency appointment to start immediately, including the current minute.
        if (request.AppointmentDate == today && !request.IsEmergency)
        {
            var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
            if (request.SlotTime <= nowVietnamTime)
                return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, "Không thể đặt lịch cho khung giờ đã qua trong ngày.");
        }

        if (await _appointmentRepository.HasActiveAppointmentOnDateAsync(request.PatientId, request.AppointmentDate, ct))
            return Result<AppointmentResponse>.Failure(Error.AppointmentPatientHasActive);

        // DL050/DL316: tối đa100 chỗ đồng thời, kể cả lịch cũ dùng thời lượng khác.
        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct, durationMinutes: snapshot.Schedule.DurationMinutes);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        var overriddenAppointmentId = (int?)null;
        if (request.DentistId.HasValue)
        {
            var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");

            var conflict = await _appointmentRepository.GetDentistBookingConflictAsync(
                request.DentistId.Value, request.AppointmentDate, request.SlotTime, 0, ct, snapshot.Schedule.DurationMinutes);
            if (conflict is not null)
            {
                if (!request.IsEmergency)
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Bác sĩ đã có lịch hẹn trong khung giờ này.");

                if (request.ConfirmedDentistConflictAppointmentId != conflict.AppointmentId)
                {
                    var dentistName = conflict.Dentist?.FullName ?? dentist.FullName;
                    var patientName = conflict.Patient?.FullName ?? "Bệnh nhân";
                    var warning = $"Nha sĩ {dentistName} đã có lịch với bệnh nhân {patientName} lúc {conflict.SlotTime:HH:mm}. Bạn có chắc chắn muốn xếp chen ngang ca cấp cứu này không?";
                    var details = new AppointmentDentistConflictDetails(
                        conflict.AppointmentId, dentistName, patientName, conflict.AppointmentDate, conflict.SlotTime);
                    return Result<AppointmentResponse>.Failure(new Error(
                        Error.AppointmentDentistConflict.Code, warning, details));
                }

                overriddenAppointmentId = conflict.AppointmentId;
            }
        }

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            AppointmentDate = request.AppointmentDate,
            SlotTime = request.SlotTime,
            SlotDurationMinutes = snapshot.Schedule.DurationMinutes,
            DentistId = request.DentistId,
            Status = AppointmentStatuses.Scheduled,
            Notes = request.Notes?.Trim(),
            IsEmergency = request.IsEmergency,
            EmergencyReason = request.IsEmergency ? reason : null,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _appointmentRepository.AddAsync(appointment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.AppointmentCreated,
            entityType: "Appointment",
            entityId: appointment.AppointmentId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                changedFields = request.IsEmergency
                    ? new[]
                    {
                        nameof(Appointment.PatientId),
                        nameof(Appointment.AppointmentDate),
                        nameof(Appointment.SlotTime),
                        nameof(Appointment.SlotDurationMinutes),
                        nameof(Appointment.DentistId),
                        nameof(Appointment.Status),
                        nameof(Appointment.IsEmergency),
                        nameof(Appointment.EmergencyReason)
                    }
                    : new[]
                    {
                        nameof(Appointment.PatientId),
                        nameof(Appointment.AppointmentDate),
                        nameof(Appointment.SlotTime),
                        nameof(Appointment.SlotDurationMinutes),
                        nameof(Appointment.DentistId),
                        nameof(Appointment.Status)
                    },
                slotDurationMinutes = appointment.SlotDurationMinutes,
                confirmedDentistConflictOverride = overriddenAppointmentId.HasValue,
                overriddenAppointmentId
            }),
            ipAddress: ipAddress,
            ct: ct);

        var created = await _appointmentRepository.GetByIdAsync(appointment.AppointmentId, ct);
        return Result<AppointmentResponse>.Success(ToResponse(created ?? appointment));
    }

    public async Task<Result<IReadOnlyList<BookingDentistResponse>>> GetBookingDentistsAsync(
        string? actorRoleCode, CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Result<IReadOnlyList<BookingDentistResponse>>.Failure(Error.Forbidden);
        var dentists = await _userRepository.GetActiveDentistsAsync(ct);
        return Result<IReadOnlyList<BookingDentistResponse>>.Success(
            dentists.Select(d => new BookingDentistResponse(d.UserId, d.FullName)).ToList());
    }

    public async Task<Result<BookingScheduleDto>> GetBookingScheduleAsync(string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Patient)) return Result<BookingScheduleDto>.Failure(Error.Forbidden);
        var snapshot = await _appointmentRepository.GetScheduleAsync(ct) ?? new ScheduleSnapshot(ClinicSchedule.Default, "", true);
        return Result<BookingScheduleDto>.Success(new(snapshot.Schedule.Slots().Select(x => x.ToString("HH:mm")).ToArray(), snapshot.Schedule.DurationMinutes, snapshot.Revision));
    }

    public async Task<Result<PagedResult<AppointmentResponse>>> GetAppointmentsAsync(
        AppointmentQueryRequest request,
        CancellationToken ct = default,
        int? ownerUserId = null)
    {
        var (items, totalCount) = await _appointmentRepository.GetAppointmentsAsync(
            request.Date,
            request.PatientId,
            request.Status,
            request.Page,
            request.PageSize,
            ct,
            ownerUserId);

        var page = PagedResult<AppointmentResponse>.Create(
            items.Select(ToResponse).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return Result<PagedResult<AppointmentResponse>>.Success(page);
    }

    public async Task<Result<PagedResult<AppointmentResponse>>> GetAccessibleAppointmentsAsync(
        AppointmentQueryRequest request, int actorUserId, string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Dentist or RoleCodes.Assistant or RoleCodes.Patient))
            return Result<PagedResult<AppointmentResponse>>.Failure(Error.Forbidden);
        if (role == RoleCodes.Patient && request.PatientId.HasValue)
        {
            var patient = await _patientRepository.GetByIdAsync(request.PatientId.Value, ct);
            if (patient?.UserId != actorUserId) return Result<PagedResult<AppointmentResponse>>.Failure(Error.NotFound);
        }
        return await GetAppointmentsAsync(request, ct, role == RoleCodes.Patient ? actorUserId : null);
    }

    public async Task<Result<AppointmentResponse>> GetAccessibleAppointmentByIdAsync(
        int id, int actorUserId, string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Dentist or RoleCodes.Assistant or RoleCodes.Patient))
            return Result<AppointmentResponse>.Failure(Error.Forbidden);
        var appointment = await _appointmentRepository.GetByIdAsync(id, ct);
        if (appointment is null) return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
        if (role == RoleCodes.Patient)
        {
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId, ct);
            if (patient?.UserId != actorUserId) return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
        }
        return Result<AppointmentResponse>.Success(ToResponse(appointment));
    }

    public async Task<Result<AppointmentResponse>> GetAppointmentByIdAsync(
        int appointmentId,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        return Result<AppointmentResponse>.Success(ToResponse(appointment));
    }

    public async Task<Result<SelfCancellationEligibilityResponse>> GetSelfCancellationEligibilityAsync(
        int id, int actorUserId, CancellationToken ct = default)
    {
        var result = await GetAccessibleAppointmentByIdAsync(id, actorUserId, RoleCodes.Patient, ct);
        if (result.IsFailure) return Result<SelfCancellationEligibilityResponse>.Failure(result.Error);
        var appointment = result.Value!;
        var scheduled = appointment.Status == AppointmentStatuses.Scheduled;
        var enoughTime = (appointment.AppointmentDate.ToDateTime(appointment.SlotTime) - _vietnamClock.Now).TotalMinutes >= 120;
        var message = !scheduled ? "Lịch hẹn không còn ở trạng thái chờ khám."
            : !enoughTime ? "Lịch hẹn còn dưới 2 giờ. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ." : null;
        var reasonLimit = Math.Max(0, 500 - (appointment.Notes?.Length ?? 0) - (string.IsNullOrEmpty(appointment.Notes) ? 0 : 1) - "[Lý do hủy: ]".Length);
        return Result<SelfCancellationEligibilityResponse>.Success(new(id, scheduled && enoughTime, message, reasonLimit));
    }

    public async Task<Result<AppointmentResponse>> CancelAppointmentAsync(
        int appointmentId,
        int actorUserId,
        string? actorRoleCode,
        CancelAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Patient))
            return Result<AppointmentResponse>.Failure(Error.Forbidden);
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        if (actorRoleCode == RoleCodes.Patient)
        {
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId, ct);
            if (patient?.UserId != actorUserId)
                return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
            if ((appointment.AppointmentDate.ToDateTime(appointment.SlotTime) - _vietnamClock.Now).TotalMinutes < 120)
                return Result<AppointmentResponse>.Failure(Error.AppointmentCannotCancel.Code, "Lịch hẹn còn dưới 2 giờ. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ.");
        }

        if (appointment.Status != AppointmentStatuses.Scheduled)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotCancel);

        var notes = appointment.Notes;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            notes = string.IsNullOrEmpty(appointment.Notes)
                ? $"[Lý do hủy: {request.Reason.Trim()}]"
                : $"{appointment.Notes} [Lý do hủy: {request.Reason.Trim()}]";
        }
        if (notes?.Length > 500)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Ghi chú và lý do hủy vượt 500 ký tự. Vui lòng rút ngắn lý do hủy.");
        appointment.Status = AppointmentStatuses.Cancelled;
        appointment.Notes = notes;
        appointment.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.AppointmentCancelled,
            entityType: "Appointment",
            entityId: appointment.AppointmentId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                changedFields = new[] { nameof(Appointment.Status), nameof(Appointment.Notes), nameof(Appointment.UpdatedAt) }
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<AppointmentResponse>.Success(ToResponse(appointment));
    }
    public async Task<Result<AppointmentResponse>> RescheduleAppointmentAsync(
        int appointmentId,
        int actorUserId,
        string? actorRoleCode,
        RescheduleAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Patient))
            return Result<AppointmentResponse>.Failure(Error.Forbidden);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(
                transactionCt => RescheduleCoreAsync(appointmentId, actorUserId, actorRoleCode, request, ipAddress, transactionCt), ct);
        }
        catch (ConcurrentOperationException) { return Result<AppointmentResponse>.Failure(Error.AppointmentConcurrent); }
    }

    private async Task<Result<AppointmentResponse>> RescheduleCoreAsync(
        int appointmentId, int actorUserId, string actorRoleCode, RescheduleAppointmentRequest request, string? ipAddress, CancellationToken ct)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        if (actorRoleCode == RoleCodes.Patient)
        {
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId, ct);
            if (patient?.UserId != actorUserId)
                return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
            // Portal only moves the time; it cannot reassign a dentist or edit staff notes.
            if ((request.DentistId.HasValue && request.DentistId != appointment.DentistId) || request.Notes is not null)
                return Result<AppointmentResponse>.Failure(Error.Forbidden);
        }

        if (appointment.Status != AppointmentStatuses.Scheduled)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotReschedule);

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || request.Notes?.Length > 500)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Cần lý do đổi lịch tối đa 500 ký tự; ghi chú tối đa 500 ký tự.");
        var dentistId = request.DentistId ?? appointment.DentistId;
        if (appointment.AppointmentDate == request.AppointmentDate && appointment.SlotTime == request.SlotTime && appointment.DentistId == dentistId)
            return Result<AppointmentResponse>.Success(ToResponse(appointment));
        var snapshot = await _appointmentRepository.GetScheduleAsync(ct) ?? new ScheduleSnapshot(ClinicSchedule.Default, "", true);
        if (request.ExpectedScheduleRevision is not null && request.ExpectedScheduleRevision != snapshot.Revision)
            return Result<AppointmentResponse>.Failure(new Error("APP_SCHEDULE", "Cấu hình giờ khám đã đổi. Tải lại và chọn lại khung giờ trước khi gửi."));
        if (!snapshot.Schedule.IsValidSlot(request.SlotTime)) return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime);

        if (actorRoleCode == RoleCodes.Patient && SelfReschedulingMessage(appointment) is string message)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotReschedule.Code, message);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        if (request.AppointmentDate == today)
        {
            var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
            if (request.SlotTime <= nowVietnamTime)
                return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, "Không thể đổi lịch sang khung giờ đã qua trong ngày.");
        }

        if (await _appointmentRepository.HasActiveAppointmentOnDateAsync(appointment.PatientId, request.AppointmentDate, ct, appointmentId))
            return Result<AppointmentResponse>.Failure(Error.AppointmentPatientHasActive);
        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct, appointmentId, snapshot.Schedule.DurationMinutes);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        if (dentistId.HasValue)
        {
            var dentist = await _userRepository.FindByIdAsync(dentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");
            if (await _appointmentRepository.HasDentistBookingAsync(dentistId.Value, request.AppointmentDate, request.SlotTime, appointmentId, ct, snapshot.Schedule.DurationMinutes))
                return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Bác sĩ đã có lịch hẹn trong khung giờ này.");
        }

        if (appointment.RescheduleCount == int.MaxValue) return Result<AppointmentResponse>.Failure(Error.Validation);
        var oldDate = appointment.AppointmentDate;
        var oldTime = appointment.SlotTime;
        var oldDurationMinutes = appointment.SlotDurationMinutes;
        var oldDentistId = appointment.DentistId;

        appointment.AppointmentDate = request.AppointmentDate;
        appointment.SlotTime = request.SlotTime;
        appointment.SlotDurationMinutes = snapshot.Schedule.DurationMinutes;
        appointment.DentistId = dentistId;
        appointment.RescheduleCount++;
        appointment.RescheduleReason = request.Reason.Trim();
        if (!string.IsNullOrWhiteSpace(request.Notes))
            appointment.Notes = request.Notes.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.AppointmentRescheduled,
            entityType: "Appointment",
            entityId: appointment.AppointmentId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                oldDate, oldTime, oldDentistId, oldDurationMinutes,
                newDate = appointment.AppointmentDate, newTime = appointment.SlotTime, newDentistId = dentistId,
                newDurationMinutes = appointment.SlotDurationMinutes,
                rescheduleCount = appointment.RescheduleCount,
                changedFields = new[]
                {
                    nameof(Appointment.AppointmentDate),
                    nameof(Appointment.SlotTime),
                    nameof(Appointment.SlotDurationMinutes),
                    nameof(Appointment.DentistId),
                    nameof(Appointment.Notes),
                    nameof(Appointment.RescheduleCount),
                    nameof(Appointment.RescheduleReason),
                    nameof(Appointment.UpdatedAt)
                }
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<AppointmentResponse>.Success(ToResponse(appointment));
    }

    public async Task<Result<SelfReschedulingEligibilityResponse>> GetSelfReschedulingEligibilityAsync(
        int id, int actorUserId, CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, ct);
        if (appointment is null)
            return Result<SelfReschedulingEligibilityResponse>.Failure(Error.AppointmentNotFound);
        var patient = await _patientRepository.GetByIdAsync(appointment.PatientId, ct);
        if (patient?.UserId != actorUserId)
            return Result<SelfReschedulingEligibilityResponse>.Failure(Error.AppointmentNotFound);
        var message = SelfReschedulingMessage(appointment);
        return Result<SelfReschedulingEligibilityResponse>.Success(new(
            ToResponse(appointment), message is null, message, Math.Max(0, 2 - appointment.RescheduleCount)));
    }

    private string? SelfReschedulingMessage(Appointment appointment)
        => appointment.Status != AppointmentStatuses.Scheduled ? "Lịch hẹn không còn ở trạng thái chờ khám. Vui lòng liên hệ Lễ tân phòng khám."
            : appointment.RescheduleCount >= 2 ? "Lịch hẹn đã đổi 2 lần. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ."
            : (appointment.AppointmentDate.ToDateTime(appointment.SlotTime) - _vietnamClock.Now).TotalMinutes < 120
                ? "Lịch hẹn còn dưới 2 giờ. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ." : null;

    private static AppointmentResponse ToResponse(Appointment a)
        => new(
            a.AppointmentId,
            a.PatientId,
            a.Patient?.PatientCode ?? string.Empty,
            a.Patient?.FullName ?? string.Empty,
            a.Patient?.Phone ?? string.Empty,
            a.AppointmentDate,
            a.SlotTime,
            a.DentistId,
            a.Dentist?.FullName,
            a.Status,
            a.Notes,
            a.CreatedByUserId,
            a.CreatedAt,
            a.RescheduleCount,
            a.RescheduleReason,
            a.IsEmergency,
            a.EmergencyReason,
            a.SlotDurationMinutes
        );

}
