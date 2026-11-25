using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_PAT;

namespace Dental.Application.Features.Appointments.Services;

public sealed class AppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly VietnamClock _vietnamClock;
    private readonly IPatientGuardianRepository? _guardianRepository;
    private readonly IVisitRepository? _visitRepository;
    private readonly IQueueRepository? _queueRepository;
    private readonly IKeyedIdentityHasher? _identityHasher;
    private readonly IPasswordHasher? _passwordHasher;


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

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        VietnamClock vietnamClock,
        IPatientGuardianRepository guardianRepository)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _vietnamClock = vietnamClock;
        _guardianRepository = guardianRepository;
    }

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        VietnamClock vietnamClock,
        IPatientGuardianRepository guardianRepository,
        IVisitRepository visitRepository,
        IQueueRepository queueRepository,
        IKeyedIdentityHasher? identityHasher = null,
        IPasswordHasher? passwordHasher = null)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _vietnamClock = vietnamClock;
        _guardianRepository = guardianRepository;
        _visitRepository = visitRepository;
        _queueRepository = queueRepository;
        _identityHasher = identityHasher;
        _passwordHasher = passwordHasher;
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
        // DL337: retain the legacy request fields only to reject old clients explicitly.
        if (request.IsEmergency || request.EmergencyReason is not null ||
            request.ConfirmedDentistConflictAppointmentId.HasValue || request.ConfirmedDentistConflictRequestId.HasValue)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                "Phòng khám không tiếp nhận cấp cứu hoặc chen lịch. Chọn khung giờ khám và ghi điều cần lưu ý vào ghi chú.");
        if (request.DentistId.HasValue)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                "Không chọn hoặc chỉ định nha sĩ khi đặt lịch; nha sĩ trong phòng sẽ gọi số bệnh nhân.");
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
        if (!snapshot.Schedule.AllowsAppointmentStart(request.SlotTime))
            return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime);
        if (request.Notes?.Length > 500)
            return Result<AppointmentResponse>.Failure(Error.Validation);
        var patient = await _patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
            return Result<AppointmentResponse>.Failure(Error.NotFound);
        if (actorRoleCode == RoleCodes.Patient)
        {
            if (!await CanPatientAccessAsync(actorUserId, patient, ct))
            {
                return Result<AppointmentResponse>.Failure(Error.NotFound);
            }
        }
        if (!patient.IsActive)
            return Result<AppointmentResponse>.Failure(Error.PatientInactive);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        var isToday = request.AppointmentDate == today;
        var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
        var isCounterActor = actorRoleCode is RoleCodes.Admin or RoleCodes.Receptionist;
        var isCounterWalkIn = isCounterActor && isToday && nowVietnamTime >= request.SlotTime;

        if (isToday && !isCounterActor && request.SlotTime <= nowVietnamTime)
        {
            var closedMessage = !snapshot.Schedule.Slots().Any(t => t > nowVietnamTime)
                ? "Nha khoa đã hết giờ làm việc hôm nay, hãy đặt lịch cho ngày mai để hàm răng chắc khỏe."
                : "Không thể đặt lịch cho khung giờ đã qua trong ngày.";
            return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, closedMessage);
        }

        if (await _appointmentRepository.HasActiveAppointmentOnDateAsync(request.PatientId, request.AppointmentDate, ct))
            return Result<AppointmentResponse>.Failure(Error.AppointmentPatientHasActive);

        // DL050/DL316: tối đa100 chỗ đồng thời, kể cả lịch cũ dùng thời lượng khác.
        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct, durationMinutes: snapshot.Schedule.DurationMinutes);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        var patientAccountCreated = false;
        if (isCounterActor)
        {
            var contactPhone = request.ContactPhone?.Trim();
            if (contactPhone is not { Length: 10 } || contactPhone[0] != '0' ||
                contactPhone.Any(c => c is < '0' or > '9') ||
                !string.Equals(patient.Phone, contactPhone, StringComparison.Ordinal))
                return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                    "Số điện thoại liên hệ không khớp hồ sơ. Hãy đối chiếu thông tin trước khi tiếp đón.");

            var isChild = PatientAccountAge.IsUnderMinimumAge(patient.DateOfBirth, today);
            if (isChild)
            {
                if (!string.IsNullOrWhiteSpace(request.CitizenId))
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Trẻ dưới 14 tuổi không cần và không sử dụng CCCD riêng khi tiếp đón.");

                var guardianName = request.GuardianFullName?.Trim();
                var guardianRelationship = request.GuardianRelationship?.Trim();
                if (!request.GuardianVerifiedAtCounter || guardianName is not { Length: >= 2 and <= 100 } ||
                    guardianName.Any(char.IsControl) || string.IsNullOrWhiteSpace(guardianRelationship) || guardianRelationship.Length > 50)
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Vui lòng ghi nhận họ tên, quan hệ và xác nhận đã đối chiếu người giám hộ trực tiếp tại quầy.");

                var guardianNote = $"Người giám hộ đã xác minh tại quầy: {guardianName} ({guardianRelationship})";
                var notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? guardianNote
                    : $"{request.Notes.Trim()} | {guardianNote}";
                if (notes.Length > 500)
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Ghi chú đặt lịch quá dài khi kèm thông tin người giám hộ. Vui lòng rút gọn ghi chú.");
                request = request with { Notes = notes };
            }
            else
            {
                var identifier = request.CitizenId?.Trim();
                var documentType = request.DocumentType ?? IdentityDocumentType.CitizenId;
                var nationalityCode = documentType == IdentityDocumentType.Passport
                    ? request.NationalityCode?.Trim().ToUpperInvariant()
                    : null;
                var validIdentifier = documentType switch
                {
                    IdentityDocumentType.CitizenId => identifier is { Length: 12 } && identifier.All(char.IsAsciiDigit) &&
                        string.IsNullOrWhiteSpace(request.NationalityCode),
                    IdentityDocumentType.Passport => identifier is { Length: >= 6 and <= 12 } &&
                        identifier.All(char.IsAsciiLetterOrDigit) && nationalityCode is { Length: 2 or 3 } &&
                        nationalityCode.All(char.IsAsciiLetter),
                    _ => false
                };
                if (!validIdentifier)
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Vui lòng đối chiếu CCCD hoặc hộ chiếu kèm quốc gia cấp trước khi xác nhận tại quầy.");
                if (_identityHasher is null)
                    return Result<AppointmentResponse>.Failure(Error.ServerError);

                var identityHash = _identityHasher.Hash(identifier!, documentType, nationalityCode);
                if (patient.CitizenIdHash is not null && !_identityHasher.Matches(patient.CitizenIdHash, identifier!,
                        documentType, nationalityCode, expectedKeyVersion: patient.IdentityKeyVersion))
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Giấy tờ định danh không khớp hồ sơ bệnh nhân. Hãy đối soát thông tin trước khi tiếp đón.");

                var duplicate = await _patientRepository.FindByCitizenIdHashAsync(identityHash, patient.PatientId, ct);
                if (documentType == IdentityDocumentType.CitizenId)
                    duplicate ??= await _patientRepository.FindByCitizenIdHashAsync(
                        CitizenIdIdentity.Hash(identifier!), patient.PatientId, ct);
                if (duplicate is not null)
                    return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                        "Giấy tờ định danh đã liên kết với hồ sơ khác. Hãy đối soát thông tin trước khi tiếp đón.");

                Role? patientRole = null;
                var accountPasswordHasher = _passwordHasher;
                if (patient.UserId.HasValue)
                {
                    var owner = await _userRepository.FindByIdAsync(patient.UserId.Value, ct);
                    if (owner is null || !owner.IsActive || owner.Role?.RoleCode != RoleCodes.Patient)
                        return Result<AppointmentResponse>.Failure(Error.Conflict);
                }
                else
                {
                    patientRole = await _userRepository.FindRoleByCodeAsync(RoleCodes.Patient, ct);
                    if (patientRole is null || accountPasswordHasher is null)
                        return Result<AppointmentResponse>.Failure(Error.ServerError);
                }

                var identityChanged = patient.CitizenIdHash != identityHash ||
                    patient.DocumentType != documentType ||
                    patient.IssuingCountryCode != nationalityCode ||
                    patient.HashScheme != IdentityHashScheme.HmacCanonical ||
                    patient.IdentityKeyVersion != _identityHasher.CurrentVersion;
                if (identityChanged)
                {
                    patient.CitizenIdHash = identityHash;
                    patient.CitizenIdMasked = _identityHasher.Mask(identifier!, documentType);
                    patient.DocumentType = documentType;
                    patient.IssuingCountryCode = nationalityCode;
                    patient.HashScheme = IdentityHashScheme.HmacCanonical;
                    patient.IdentityKeyVersion = _identityHasher.CurrentVersion;
                    patient.CitizenIdVersion++;
                    patient.UpdatedAt = _vietnamClock.UtcNow;
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _auditLogger.LogRequiredAsync(AuditActions.PatientUpdated, "Patient", patient.PatientId, actorUserId,
                        JsonSerializer.Serialize(new
                        {
                            changedFields = new[] { nameof(patient.CitizenIdHash), nameof(patient.CitizenIdMasked) },
                            source = "CounterBookingIdentityVerification"
                        }), ipAddress, ct);
                }

                if (!patient.UserId.HasValue)
                {
                    var now = _vietnamClock.UtcNow;
                    var account = new User
                    {
                        Phone = patient.Phone,
                        PasswordHash = accountPasswordHasher!.Hash(patient.Phone),
                        LoginByCitizenId = true,
                        RequiresPhoneActivation = true,
                        FullName = patient.FullName,
                        Email = patient.Email,
                        DateOfBirth = patient.DateOfBirth,
                        Gender = patient.Gender,
                        RoleId = patientRole!.RoleId,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _userRepository.AddAsync(account, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    patient.UserId = account.UserId;
                    patient.UpdatedAt = now;
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _auditLogger.LogRequiredAsync("MOD_AUTH_PATIENT_ACCOUNT_CREATED", "User", account.UserId, actorUserId,
                        JsonSerializer.Serialize(new
                        {
                            patientId = patient.PatientId,
                            activationRequired = true,
                            loginIdentifier = documentType.ToString()
                        }), ipAddress, ct);
                    patientAccountCreated = true;
                }
            }
        }

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            AppointmentDate = request.AppointmentDate,
            SlotTime = request.SlotTime,
            SlotDurationMinutes = snapshot.Schedule.DurationMinutes,
            Status = isCounterWalkIn ? AppointmentStatuses.CheckedIn : AppointmentStatuses.Scheduled,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _appointmentRepository.AddAsync(appointment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        int? generatedQueueNumber = null;
        if (isCounterWalkIn && _visitRepository is not null && _queueRepository is not null)
        {
            var visit = new Visit
            {
                PatientId = request.PatientId,
                Status = VisitStatuses.Created,
                DentistId = null,
                CreatedByUserId = actorUserId,
                CreatedAt = _vietnamClock.UtcNow
            };
            await _visitRepository.AddAsync(visit, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var nextNumber = await _queueRepository.GetNextQueueNumberAsync(today, ct);
            generatedQueueNumber = nextNumber;
            var queueEntry = new QueueEntry
            {
                PatientId = request.PatientId,
                AppointmentId = appointment.AppointmentId,
                DentistId = null,
                VisitId = visit.VisitId,
                QueueDate = today,
                QueueNumber = nextNumber,
                IsPriority = false,
                CheckInTime = _vietnamClock.UtcNow,
                Status = QueueStatus.Waiting,
                Notes = request.Notes?.Trim(),
                CreatedAt = _vietnamClock.UtcNow
            };
            await _queueRepository.AddAsync(queueEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
            {
                QueueEntryId = queueEntry.QueueEntryId,
                FromStatus = null,
                ToStatus = QueueStatus.Waiting,
                ChangedByUserId = actorUserId,
                ChangedAt = _vietnamClock.UtcNow,
                Reason = "Đặt lịch tại quầy hôm nay - tự động tiếp đón (§1.6)"
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _auditLogger.LogRequiredAsync(
                action: AuditActions.QueueCheckedIn,
                entityType: "QueueEntry",
                entityId: queueEntry.QueueEntryId,
                userId: actorUserId,
                detail: JsonSerializer.Serialize(new
                {
                    source = "CounterWalkInAutoCheckIn",
                    appointmentId = appointment.AppointmentId,
                    visitId = visit.VisitId,
                    queueNumber = nextNumber
                }),
                ipAddress: ipAddress,
                ct: ct);
        }

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
                        nameof(Appointment.SlotDurationMinutes),
                        nameof(Appointment.Status)
                    },
                slotDurationMinutes = appointment.SlotDurationMinutes
            }),
            ipAddress: ipAddress,
            ct: ct);

        var created = await _appointmentRepository.GetByIdAsync(appointment.AppointmentId, ct);
        var resp = await ToResponseAsync(created ?? appointment, ct);
        if (generatedQueueNumber.HasValue)
        {
            resp = resp with { QueueNumber = generatedQueueNumber.Value };
        }
        resp = resp with { PatientAccountCreated = patientAccountCreated };
        return Result<AppointmentResponse>.Success(resp);
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

        var responseItems = await ToResponsesAsync(items, ct);

        var page = PagedResult<AppointmentResponse>.Create(
            responseItems,
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
            if (!await CanPatientAccessAsync(actorUserId, patient, ct))
                return Result<PagedResult<AppointmentResponse>>.Failure(Error.NotFound);
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
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId!.Value, ct);
            if (!await CanPatientAccessAsync(actorUserId, patient, ct))
                return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
        }
        return Result<AppointmentResponse>.Success(await ToResponseAsync(appointment, ct));
    }

    public async Task<Result<AppointmentResponse>> GetAppointmentByIdAsync(
        int appointmentId,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, ct);
        if (appointment is null)
            return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);

        return Result<AppointmentResponse>.Success(await ToResponseAsync(appointment, ct));
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
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId!.Value, ct);
            if (!await CanPatientAccessAsync(actorUserId, patient, ct))
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

        return Result<AppointmentResponse>.Success(await ToResponseAsync(appointment, ct));
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

        if (request.DentistId.HasValue)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code,
                "Không chọn hoặc chỉ định nha sĩ khi đổi lịch.");

        if (actorRoleCode == RoleCodes.Patient)
        {
            var patient = await _patientRepository.GetByIdAsync(appointment.PatientId!.Value, ct);
            if (!await CanPatientAccessAsync(actorUserId, patient, ct))
                return Result<AppointmentResponse>.Failure(Error.AppointmentNotFound);
            // Portal only moves the time; it cannot edit staff notes.
            if (request.Notes is not null)
                return Result<AppointmentResponse>.Failure(Error.Forbidden);
        }

        if (appointment.Status != AppointmentStatuses.Scheduled)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotReschedule);

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || request.Notes?.Length > 500)
            return Result<AppointmentResponse>.Failure(Error.Validation.Code, "Cần lý do đổi lịch tối đa 500 ký tự; ghi chú tối đa 500 ký tự.");
        if (appointment.AppointmentDate == request.AppointmentDate && appointment.SlotTime == request.SlotTime)
            return Result<AppointmentResponse>.Success(await ToResponseAsync(appointment, ct));
        var snapshot = await _appointmentRepository.GetScheduleAsync(ct) ?? new ScheduleSnapshot(ClinicSchedule.Default, "", true);
        if (request.ExpectedScheduleRevision is not null && request.ExpectedScheduleRevision != snapshot.Revision)
            return Result<AppointmentResponse>.Failure(new Error("APP_SCHEDULE", "Cấu hình giờ khám đã đổi. Tải lại và chọn lại khung giờ trước khi gửi."));
        if (!snapshot.Schedule.AllowsAppointmentStart(request.SlotTime)) return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime);

        if (actorRoleCode == RoleCodes.Patient && SelfReschedulingMessage(appointment) is string message)
            return Result<AppointmentResponse>.Failure(Error.AppointmentCannotReschedule.Code, message);

        var today = _vietnamClock.Today;
        if (request.AppointmentDate < today)
            return Result<AppointmentResponse>.Failure(Error.AppointmentDateInPast);

        if (request.AppointmentDate == today)
        {
            var nowVietnamTime = TimeOnly.FromDateTime(_vietnamClock.Now);
            if (request.SlotTime <= nowVietnamTime)
            {
                var closedMessage = !snapshot.Schedule.Slots().Any(t => t > nowVietnamTime)
                    ? "Nha khoa đã hết giờ làm việc hôm nay, hãy đặt lịch cho ngày mai để hàm răng chắc khỏe."
                    : "Không thể đổi lịch sang khung giờ đã qua trong ngày.";
                return Result<AppointmentResponse>.Failure(Error.AppointmentInvalidTime.Code, closedMessage);
            }
        }

        if (await _appointmentRepository.HasActiveAppointmentOnDateAsync(appointment.PatientId!.Value, request.AppointmentDate, ct, appointmentId))
            return Result<AppointmentResponse>.Failure(Error.AppointmentPatientHasActive);
        var currentSlotCount = await _appointmentRepository.GetSlotBookingCountAsync(request.AppointmentDate, request.SlotTime, ct, appointmentId, snapshot.Schedule.DurationMinutes);
        if (currentSlotCount >= AppointmentSlots.MaxBookingsPerSlot)
            return Result<AppointmentResponse>.Failure(Error.AppointmentSlotFull);

        if (appointment.RescheduleCount == int.MaxValue) return Result<AppointmentResponse>.Failure(Error.Validation);
        var oldDate = appointment.AppointmentDate;
        var oldTime = appointment.SlotTime;
        var oldDurationMinutes = appointment.SlotDurationMinutes;

        appointment.AppointmentDate = request.AppointmentDate;
        appointment.SlotTime = request.SlotTime;
        appointment.SlotDurationMinutes = snapshot.Schedule.DurationMinutes;
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
                oldDate, oldTime, oldDurationMinutes,
                newDate = appointment.AppointmentDate, newTime = appointment.SlotTime,
                newDurationMinutes = appointment.SlotDurationMinutes,
                rescheduleCount = appointment.RescheduleCount,
                changedFields = new[]
                {
                    nameof(Appointment.AppointmentDate),
                    nameof(Appointment.SlotTime),
                    nameof(Appointment.SlotDurationMinutes),
                    nameof(Appointment.Notes),
                    nameof(Appointment.RescheduleCount),
                    nameof(Appointment.RescheduleReason),
                    nameof(Appointment.UpdatedAt)
                }
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<AppointmentResponse>.Success(await ToResponseAsync(appointment, ct));
    }

    public async Task<Result<SelfReschedulingEligibilityResponse>> GetSelfReschedulingEligibilityAsync(
        int id, int actorUserId, CancellationToken ct = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, ct);
        if (appointment is null)
            return Result<SelfReschedulingEligibilityResponse>.Failure(Error.AppointmentNotFound);
        var patient = await _patientRepository.GetByIdAsync(appointment.PatientId!.Value, ct);
        if (!await CanPatientAccessAsync(actorUserId, patient, ct))
            return Result<SelfReschedulingEligibilityResponse>.Failure(Error.AppointmentNotFound);
        var message = SelfReschedulingMessage(appointment);
        return Result<SelfReschedulingEligibilityResponse>.Success(new(
            await ToResponseAsync(appointment, ct), message is null, message, Math.Max(0, 2 - appointment.RescheduleCount)));
    }

    private string? SelfReschedulingMessage(Appointment appointment)
        => appointment.Status != AppointmentStatuses.Scheduled ? "Lịch hẹn không còn ở trạng thái chờ khám. Vui lòng liên hệ Lễ tân phòng khám."
            : appointment.RescheduleCount >= 2 ? "Lịch hẹn đã đổi 2 lần. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ."
            : (appointment.AppointmentDate.ToDateTime(appointment.SlotTime) - _vietnamClock.Now).TotalMinutes < 120
                ? "Lịch hẹn còn dưới 2 giờ. Vui lòng liên hệ Lễ tân phòng khám để được hỗ trợ." : null;

    private async Task<bool> CanPatientAccessAsync(int actorUserId, Dental.Domain.Entities.Patient? patient, CancellationToken ct)
    {
        if (patient is null)
            return false;
        if (patient.UserId == actorUserId)
            return true;
        return _guardianRepository is not null &&
               await _guardianRepository.HasActiveGuardianshipAsync(actorUserId, patient.PatientId, ct);
    }

    private async Task<List<AppointmentResponse>> ToResponsesAsync(
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken ct)
    {
        if (appointments.Count == 0)
            return [];

        var today = _vietnamClock.Today;
        var childPatientIds = appointments
            .Where(a => a.PatientId.HasValue && a.Patient is not null && PatientAccountAge.IsUnderMinimumAge(a.Patient.DateOfBirth, today))
            .Select(a => a.PatientId!.Value)
            .Distinct()
            .ToArray();

        IReadOnlySet<int> activeGuardianWardIds = childPatientIds.Length > 0 && _guardianRepository is not null
            ? await _guardianRepository.GetWardPatientIdsWithActiveGuardianAsync(childPatientIds, ct) ?? new HashSet<int>()
            : new HashSet<int>();

        return appointments
            .Select(a => ToResponse(a, activeGuardianWardIds, today))
            .ToList();
    }

    private async Task<AppointmentResponse> ToResponseAsync(Appointment a, CancellationToken ct)
        => (await ToResponsesAsync([a], ct))[0];

    private static AppointmentResponse ToResponse(
        Appointment a,
        IReadOnlySet<int> activeGuardianWardIds,
        DateOnly today)
    {
        var patientId = a.PatientId ?? 0;
        DateOnly? patientDob = a.Patient?.DateOfBirth;
        var isChild = patientDob.HasValue && PatientAccountAge.IsUnderMinimumAge(patientDob.Value, today);
        var missingGuardianAccount = isChild && !activeGuardianWardIds.Contains(patientId);
        return new(
            a.AppointmentId,
            patientId,
            a.Patient?.PatientCode ?? string.Empty,
            a.Patient?.FullName ?? string.Empty,
            a.Patient?.Phone ?? string.Empty,
            a.AppointmentDate,
            a.SlotTime,
            a.Status,
            a.Notes,
            a.CreatedByUserId ?? 0,
            a.CreatedAt,
            a.RescheduleCount,
            a.RescheduleReason,
            a.IsEmergency,
            a.EmergencyReason,
            a.SlotDurationMinutes,
            PatientDateOfBirth: patientDob,
            IsChildPatient: isChild,
            MissingGuardianAccount: missingGuardianAccount
        );
    }

}
