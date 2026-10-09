using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

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
        CreateAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
            return Result<AppointmentResponse>.Failure(Error.NotFound);
        if (!patient.IsActive)
            return Result<AppointmentResponse>.Failure(Error.PatientInactive);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        if (request.AppointmentDate == today)
        {
            var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
            if (request.SlotTime <= nowVietnamTime)
                return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, "Không thể đặt lịch cho khung giờ đã qua trong ngày.");
        }

        if (await _appointmentRepository.HasActiveAppointmentOnDateAsync(request.PatientId, request.AppointmentDate, ct))
            return Result<AppointmentResponse>.Failure(Error.AppointmentPatientHasActive);

        // Trần tiếp nhận tối đa 100 khách / slot 30 phút theo DL-050
        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        if (request.DentistId.HasValue)
        {
            var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");
        }

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            AppointmentDate = request.AppointmentDate,
            SlotTime = request.SlotTime,
            DentistId = request.DentistId,
            Status = AppointmentStatuses.Scheduled,
            Notes = request.Notes?.Trim(),
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
                changedFields = new[]
                {
                    nameof(Appointment.PatientId),
                    nameof(Appointment.AppointmentDate),
                    nameof(Appointment.SlotTime),
                    nameof(Appointment.Status)
                }
            }),
            ipAddress: ipAddress,
            ct: ct);

        var created = await _appointmentRepository.GetByIdAsync(appointment.AppointmentId, ct);
        return Result<AppointmentResponse>.Success(ToResponse(created ?? appointment));
    }

    public async Task<Result<PagedResult<AppointmentResponse>>> GetAppointmentsAsync(
        AppointmentQueryRequest request,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _appointmentRepository.GetAppointmentsAsync(
            request.Date,
            request.PatientId,
            request.Status,
            request.Page,
            request.PageSize,
            ct);

        var page = PagedResult<AppointmentResponse>.Create(
            items.Select(ToResponse).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return Result<PagedResult<AppointmentResponse>>.Success(page);
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

    public async Task<Result<AppointmentResponse>> CancelAppointmentAsync(
        int appointmentId,
        int actorUserId,
        CancelAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        if (appointment.Status != AppointmentStatuses.Scheduled)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotCancel);

        appointment.Status = AppointmentStatuses.Cancelled;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            appointment.Notes = string.IsNullOrEmpty(appointment.Notes)
                ? $"[Lý do hủy: {request.Reason.Trim()}]"
                : $"{appointment.Notes} [Lý do hủy: {request.Reason.Trim()}]";
        }
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
        RescheduleAppointmentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        if (appointment.Status != AppointmentStatuses.Scheduled)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotReschedule);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        if (request.AppointmentDate == today)
        {
            var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
            if (request.SlotTime <= nowVietnamTime)
                return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, "Không thể đổi lịch sang khung giờ đã qua trong ngày.");
        }

        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        if (request.DentistId.HasValue)
        {
            var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");
            appointment.DentistId = request.DentistId.Value;
        }

        appointment.AppointmentDate = request.AppointmentDate;
        appointment.SlotTime = request.SlotTime;
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
                changedFields = new[]
                {
                    nameof(Appointment.AppointmentDate),
                    nameof(Appointment.SlotTime),
                    nameof(Appointment.DentistId),
                    nameof(Appointment.Notes),
                    nameof(Appointment.UpdatedAt)
                }
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<AppointmentResponse>.Success(ToResponse(appointment));
    }

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
            a.CreatedAt
        );

}

