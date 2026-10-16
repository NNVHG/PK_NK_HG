using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Queue.Services;

public sealed class QueueService
{
    private readonly IQueueRepository _queueRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly VietnamClock _vietnamClock;
    private readonly IInvoiceDraftGenerator _invoiceDraft;

    public QueueService(
        IQueueRepository queueRepository,
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository,
        IVisitRepository visitRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        VietnamClock vietnamClock,
        IInvoiceDraftGenerator invoiceDraft)
    {
        _queueRepository = queueRepository;
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _vietnamClock = vietnamClock;
        _invoiceDraft = invoiceDraft;
    }

    public async Task<Result<QueueEntryResponse>> CheckInAsync(
        int actorUserId,
        CheckInRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
            return Result<QueueEntryResponse>.Failure(Error.NotFound);
        if (!patient.IsActive)
            return Result<QueueEntryResponse>.Failure(Error.PatientInactive);

        var today = _vietnamClock.Today;

        // Xử lý tồn đọng qua ngày và kiểm tra mục mở (DL-045, DL-051)
        var openQueueEntries = await _queueRepository.GetOpenQueueEntriesForPatientAsync(request.PatientId, ct);
        foreach (var openEntry in openQueueEntries)
        {
            if (openEntry.QueueDate < today)
            {
                openEntry.Status = QueueStatus.Cancelled;
                openEntry.UpdatedAt = DateTime.UtcNow;
                if (openEntry.Visit is { Status: VisitStatuses.Created or VisitStatuses.InProgress } v)
                {
                    v.Status = VisitStatuses.Cancelled;
                    v.EndedAt = DateTime.UtcNow;
                    v.UpdatedAt = DateTime.UtcNow;
                }

                await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
                {
                    QueueEntryId = openEntry.QueueEntryId,
                    FromStatus = openEntry.Status,
                    ToStatus = QueueStatus.Cancelled,
                    ChangedByUserId = actorUserId,
                    ChangedAt = DateTime.UtcNow,
                    Reason = "Tự động hủy qua ngày (DL-051)"
                }, ct);
            }
            else
            {
                return Result<QueueEntryResponse>.Failure(Error.QueueEntryAlreadyOpen);
            }
        }

        if (await _visitRepository.HasOpenVisitAsync(request.PatientId, ct))
            return Result<QueueEntryResponse>.Failure(Error.VisitAlreadyOpen);

        // Xử lý lịch hẹn và tính độ ưu tiên (DL-046 & DL-054)
        var isPriority = false;
        if (request.AppointmentId.HasValue)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId.Value, ct);
            if (appointment is null || appointment.PatientId != request.PatientId)
                return Result<QueueEntryResponse>.Failure(Error.AppointmentNotFound);

            if (appointment.AppointmentDate != today)
                return Result<QueueEntryResponse>.Failure(Error.AppointmentCannotCheckIn);

            if (appointment.Status != AppointmentStatuses.Scheduled)
                return Result<QueueEntryResponse>.Failure(Error.AppointmentCannotCheckIn);

            var scheduledDateTime = appointment.AppointmentDate.ToDateTime(appointment.SlotTime);
            var nowVietnam = _vietnamClock.Now;
            isPriority = nowVietnam >= scheduledDateTime.AddMinutes(-30) &&
                         nowVietnam <= scheduledDateTime.AddMinutes(15);

            appointment.Status = AppointmentStatuses.CheckedIn;
            appointment.UpdatedAt = DateTime.UtcNow;
        }

        if (request.DentistId.HasValue)
        {
            var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<QueueEntryResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");
        }

        return await ExecuteCheckInTransactionAsync(actorUserId, request, today, isPriority, ipAddress, ct);
    }

    private async Task<Result<QueueEntryResponse>> ExecuteCheckInTransactionAsync(
        int actorUserId,
        CheckInRequest request,
        DateOnly today,
        bool isPriority,
        string? ipAddress,
        CancellationToken ct)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            var visit = new Visit
            {
                PatientId = request.PatientId,
                Status = VisitStatuses.Created,
                DentistId = null,
                CreatedByUserId = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _visitRepository.AddAsync(visit, transactionCt);
            await _unitOfWork.SaveChangesAsync(transactionCt);

            QueueEntry entry;
            const int maxRetries = 3;
            var attempt = 0;

            while (true)
            {
                attempt++;
                var nextNumber = await _queueRepository.GetNextQueueNumberAsync(today, transactionCt);

                entry = new QueueEntry
                {
                    PatientId = request.PatientId,
                    AppointmentId = request.AppointmentId,
                    DentistId = request.DentistId,
                    VisitId = visit.VisitId,
                    QueueDate = today,
                    QueueNumber = nextNumber,
                    IsPriority = isPriority,
                    CheckInTime = DateTime.UtcNow,
                    Status = QueueStatus.Waiting,
                    Notes = request.Notes?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                await _queueRepository.AddAsync(entry, transactionCt);

                try
                {
                    await _unitOfWork.SaveChangesAsync(transactionCt);
                    break;
                }
                catch (Exception) when (attempt < maxRetries)
                {
                }
            }

            var history = new QueueStatusHistory
            {
                QueueEntryId = entry.QueueEntryId,
                FromStatus = null,
                ToStatus = QueueStatus.Waiting,
                ChangedByUserId = actorUserId,
                ChangedAt = DateTime.UtcNow,
                Reason = isPriority ? "Tiếp đón check-in (Ưu tiên đúng giờ)" : "Tiếp đón check-in"
            };

            await _queueRepository.AddStatusHistoryAsync(history, transactionCt);
            await _unitOfWork.SaveChangesAsync(transactionCt);

            await _auditLogger.LogAsync(
                action: AuditActions.QueueCheckedIn,
                entityType: "QueueEntry",
                entityId: entry.QueueEntryId,
                userId: actorUserId,
                detail: JsonSerializer.Serialize(new
                {
                    changedFields = new[]
                    {
                        nameof(QueueEntry.QueueNumber),
                        nameof(QueueEntry.IsPriority),
                        nameof(QueueEntry.Status),
                        nameof(QueueEntry.VisitId)
                    }
                }),
                ipAddress: ipAddress,
                ct: transactionCt);

            var createdEntry = await _queueRepository.GetByIdAsync(entry.QueueEntryId, transactionCt);
            return Result<QueueEntryResponse>.Success(ToResponse(createdEntry ?? entry));
        }, ct);
    }

    public async Task<Result<QueueEntryResponse>> CancelWaitingAsync(
        int queueEntryId, int actorUserId, string actorRole, CancelWaitingRequest request,
        string? ipAddress, CancellationToken ct = default)
    {
        if (actorRole is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        if (queueEntryId <= 0 || !request.Confirmed || request.Reason?.Length > 500)
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var entry = await _queueRepository.GetByIdAsync(queueEntryId, transactionCt);
                if (entry is null) return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);
                // This action only cancels the pre-consultation check-in, never an imaging return or clinical Visit.
                if (entry.QueueDate != _vietnamClock.Today || entry.Status != QueueStatus.Waiting ||
                    entry.Visit is null || entry.Visit.Status != VisitStatuses.Created || entry.Visit.IsLocked ||
                    entry.Visit.StartedAt.HasValue)
                    return Result<QueueEntryResponse>.Failure(Error.QueueWaitingCancellationBlocked);
                var now = DateTime.UtcNow;
                entry.Status = QueueStatus.Cancelled; entry.UpdatedAt = now;
                entry.Visit.Status = VisitStatuses.Cancelled; entry.Visit.EndedAt = now; entry.Visit.UpdatedAt = now;
                await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
                {
                    QueueEntryId = entry.QueueEntryId, FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.Cancelled,
                    ChangedByUserId = actorUserId, ChangedAt = now,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
                }, transactionCt);
                await _unitOfWork.SaveChangesAsync(transactionCt);
                await _auditLogger.LogAsync(AuditActions.QueueStatusChanged, "QueueEntry", entry.QueueEntryId, actorUserId,
                    JsonSerializer.Serialize(new { action = "CancelWaiting", fromStatus = (int)QueueStatus.Waiting,
                        toStatus = (int)QueueStatus.Cancelled, visitId = entry.VisitId }), ipAddress, transactionCt);
                return Result<QueueEntryResponse>.Success(ToResponse(entry));
            }, ct);
        }
        catch (ConcurrentOperationException)
        { return Result<QueueEntryResponse>.Failure(Error.QueueWaitingCancellationConcurrent); }
    }

    // MOD_CHK / DL-126, DL-259: absence never pauses or closes a clinical Visit.
    public async Task<Result<QueueEntryResponse>> ChangeAbsenceAsync(
        int queueEntryId, int actorUserId, string actorRole, bool recall, QueueAbsenceRequest request,
        string? ipAddress, CancellationToken ct = default)
    {
        if (actorRole is not (RoleCodes.Admin or RoleCodes.Receptionist) && (recall || actorRole != RoleCodes.Dentist))
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        if (queueEntryId <= 0 || actorUserId <= 0 || !request.Confirmed || request.Reason?.Length > 500)
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var entry = await _queueRepository.GetByIdAsync(queueEntryId, transactionCt);
                if (entry is null) return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);
                if (actorRole == RoleCodes.Dentist)
                {
                    var actor = await _userRepository.FindByIdAsync(actorUserId, transactionCt);
                    if (entry.DentistId != actorUserId || actor is null || !actor.IsActive || actor.Role?.RoleCode != RoleCodes.Dentist)
                        return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
                }
                var from = recall ? QueueStatus.Skipped : QueueStatus.Calling;
                var target = recall ? QueueStatus.Waiting : QueueStatus.Skipped;
                if (entry.QueueDate != _vietnamClock.Today || entry.Status != from || entry.Visit is null ||
                    entry.Visit.Status != VisitStatuses.Created || entry.Visit.IsLocked || entry.Visit.StartedAt.HasValue ||
                    (entry.Visit.DentistId.HasValue && entry.Visit.DentistId != entry.DentistId))
                    return Result<QueueEntryResponse>.Failure(Error.QueueAbsenceBlocked);
                var now = DateTime.UtcNow;
                entry.Status = target; entry.UpdatedAt = now;
                // Keep patient, appointment, queue number, assigned dentist and the same open Created Visit.
                await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
                {
                    QueueEntryId = entry.QueueEntryId, FromStatus = from, ToStatus = target,
                    ChangedByUserId = actorUserId, ChangedAt = now,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
                }, transactionCt);
                await _unitOfWork.SaveChangesAsync(transactionCt);
                await _auditLogger.LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", entry.QueueEntryId,
                    actorUserId, JsonSerializer.Serialize(new { action = recall ? "Recall" : "MarkAbsent",
                        fromStatus = (int)from, toStatus = (int)target, visitId = entry.VisitId }), ipAddress, transactionCt);
                return Result<QueueEntryResponse>.Success(ToResponse(entry));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<QueueEntryResponse>.Failure(Error.QueueConcurrent); }
    }

    private async Task<Result<QueueEntryResponse>> StartSelectedAsync(
        int queueEntryId, int actorUserId, string actorRole, UpdateQueueStatusRequest request,
        string? ipAddress, CancellationToken ct)
    {
        if (actorRole is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        if (queueEntryId <= 0 || request.DentistId is <= 0)
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        if (actorRole == RoleCodes.Dentist && request.DentistId.HasValue && request.DentistId != actorUserId)
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var entry = await _queueRepository.GetByIdAsync(queueEntryId, transactionCt);
                if (entry is null) return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);
                var transition = QueueStateMachine.ValidateTransition(entry.Status, QueueStatus.InConsultation, actorRole);
                if (transition.IsFailure) return Result<QueueEntryResponse>.Failure(transition.Error);
                var dentistId = actorRole == RoleCodes.Dentist ? actorUserId : request.DentistId ?? entry.DentistId;
                if (!dentistId.HasValue) return Result<QueueEntryResponse>.Failure(Error.QueueDentistRequired);
                var dentist = await _userRepository.FindByIdAsync(dentistId.Value, transactionCt);
                if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                    return Result<QueueEntryResponse>.Failure(Error.Validation);
                if (entry.QueueDate != _vietnamClock.Today || entry.Visit is null || entry.Visit.IsLocked ||
                    entry.Visit.Status is not (VisitStatuses.Created or VisitStatuses.InProgress))
                    return Result<QueueEntryResponse>.Failure(Error.QueueInvalidStateTransition);
                if ((entry.DentistId.HasValue && entry.DentistId != dentistId) ||
                    (entry.Visit.DentistId.HasValue && entry.Visit.DentistId != dentistId))
                    return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
                if (await _visitRepository.HasActiveVisitForDentistAsync(dentistId.Value, transactionCt, entry.VisitId ?? entry.Visit.VisitId))
                    return Result<QueueEntryResponse>.Failure(Error.DentistHasActiveVisit);
                var now = DateTime.UtcNow;
                if (await _queueRepository.HasCallingForDentistAsync(_vietnamClock.Today, dentistId.Value, transactionCt, entry.QueueEntryId))
                    return Result<QueueEntryResponse>.Failure(Error.QueueDentistAlreadyCalling);
                var previousStatus = entry.Status;
                entry.Status = QueueStatus.InConsultation; entry.DentistId = dentistId; entry.UpdatedAt = now;
                entry.Visit.Status = VisitStatuses.InProgress; entry.Visit.DentistId = dentistId;
                entry.Visit.StartedAt ??= now; entry.Visit.UpdatedAt = now;
                await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
                {
                    QueueEntryId = entry.QueueEntryId, FromStatus = previousStatus, ToStatus = QueueStatus.InConsultation,
                    ChangedByUserId = actorUserId, ChangedAt = now,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
                }, transactionCt);
                await _unitOfWork.SaveChangesAsync(transactionCt);
                await _auditLogger.LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", entry.QueueEntryId, actorUserId,
                    JsonSerializer.Serialize(new { action = "StartSelected", dentistId, visitId = entry.VisitId,
                        fromStatus = (int)previousStatus, toStatus = (int)QueueStatus.InConsultation }), ipAddress, transactionCt);
                var refreshed = await _queueRepository.GetByIdAsync(entry.QueueEntryId, transactionCt);
                return Result<QueueEntryResponse>.Success(ToResponse(refreshed ?? entry));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<QueueEntryResponse>.Failure(Error.QueueConcurrent); }
    }
    private async Task<Result<QueueEntryResponse>> RouteImagingAsync(
        int queueEntryId, int actorUserId, string actorRole, UpdateQueueStatusRequest request,
        string? ipAddress, CancellationToken ct)
    {
        var target = (QueueStatus)request.NewStatus;
        var source = target == QueueStatus.InImaging ? QueueStatus.InConsultation : QueueStatus.InImaging;
        var permission = QueueStateMachine.ValidateTransition(source, target, actorRole);
        if (permission.IsFailure) return Result<QueueEntryResponse>.Failure(permission.Error);
        if (actorUserId <= 0 || queueEntryId <= 0 || request.DentistId.HasValue || request.Reason?.Length > 500 ||
            (target == QueueStatus.InImaging && string.IsNullOrWhiteSpace(request.Reason)))
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var entry = await _queueRepository.GetByIdAsync(queueEntryId, transactionCt);
                if (entry is null) return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);
                if (entry.Status != source || entry.QueueDate != _vietnamClock.Today || entry.Visit is null ||
                    entry.Visit.Status != VisitStatuses.InProgress || entry.Visit.IsLocked ||
                    !entry.DentistId.HasValue || entry.Visit.DentistId != entry.DentistId)
                    return Result<QueueEntryResponse>.Failure(Error.QueueInvalidStateTransition);
                if (actorRole == RoleCodes.Dentist && entry.DentistId != actorUserId)
                    return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
                var dentist = await _userRepository.FindByIdAsync(entry.DentistId.Value, transactionCt);
                if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                    return Result<QueueEntryResponse>.Failure(Error.Validation);
                var now = DateTime.UtcNow;
                entry.Status = target; entry.UpdatedAt = now;
                if (target == QueueStatus.Waiting) entry.IsPriority = true;
                await _queueRepository.AddStatusHistoryAsync(new QueueStatusHistory
                {
                    QueueEntryId = entry.QueueEntryId, FromStatus = source, ToStatus = target,
                    ChangedByUserId = actorUserId, ChangedAt = now,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
                }, transactionCt);
                await _unitOfWork.SaveChangesAsync(transactionCt);
                // Clinical indication belongs in protected status history, never in the audit payload.
                await _auditLogger.LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", entry.QueueEntryId, actorUserId,
                    JsonSerializer.Serialize(new { action = "ImagingRouting", visitId = entry.VisitId,
                        dentistId = entry.DentistId, fromStatus = (int)source, toStatus = (int)target }), ipAddress, transactionCt);
                return Result<QueueEntryResponse>.Success(ToResponse(entry));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<QueueEntryResponse>.Failure(Error.QueueConcurrent); }
    }

    public async Task<Result<QueueEntryResponse>> UpdateQueueStatusAsync(
        int queueEntryId,
        int actorUserId,
        string actorRole,
        UpdateQueueStatusRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // Calling must go through the dedicated call-next operation, never the generic status update.
        if (request.NewStatus is (int)QueueStatus.Calling or (int)QueueStatus.Skipped)
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        if (request.NewStatus == (int)QueueStatus.InConsultation)
            return await StartSelectedAsync(queueEntryId, actorUserId, actorRole, request, ipAddress, ct);
        if (request.NewStatus is (int)QueueStatus.InImaging or (int)QueueStatus.Waiting)
            return await RouteImagingAsync(queueEntryId, actorUserId, actorRole, request, ipAddress, ct);
        var entry = await _queueRepository.GetByIdAsync(queueEntryId, ct);
        if (entry is null)
            return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);

        var targetStatus = (QueueStatus)request.NewStatus;
        var transitionValidation = QueueStateMachine.ValidateTransition(entry.Status, targetStatus, actorRole);
        if (transitionValidation.IsFailure)
            return Result<QueueEntryResponse>.Failure(transitionValidation.Error);

        if (targetStatus == QueueStatus.Completed)
        {
            if (entry.Visit is null || entry.Visit.Status != VisitStatuses.InProgress || entry.Visit.IsLocked)
                return Result<QueueEntryResponse>.Failure(Error.Validation);
            if (actorRole == RoleCodes.Dentist && entry.Visit.DentistId != actorUserId)
                return Result<QueueEntryResponse>.Failure(Error.Forbidden);
            if (entry.Visit is not null)
            {
                entry.Visit.IsLocked = true;
                entry.Visit.LockedAt = DateTime.UtcNow;
                entry.Visit.LockedBy = actorUserId;
                entry.Visit.Status = VisitStatuses.Completed;
                entry.Visit.EndedAt = DateTime.UtcNow;
                entry.Visit.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (targetStatus == QueueStatus.Cancelled)
        {
            if (entry.Visit is not null)
            {
                entry.Visit.Status = VisitStatuses.Cancelled;
                entry.Visit.EndedAt = DateTime.UtcNow;
                entry.Visit.UpdatedAt = DateTime.UtcNow;
            }
        }

        var history = new QueueStatusHistory
        {
            QueueEntryId = entry.QueueEntryId,
            FromStatus = entry.Status,
            ToStatus = targetStatus,
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow,
            Reason = request.Reason?.Trim()
        };

        entry.Status = targetStatus;
        entry.UpdatedAt = DateTime.UtcNow;

        await _queueRepository.AddStatusHistoryAsync(history, ct);
        if (targetStatus == QueueStatus.Completed)
        {
            var draft = await _invoiceDraft.GenerateAsync(entry.Visit!, actorUserId, actorRole, ipAddress, ct);
            if (draft.IsFailure) return Result<QueueEntryResponse>.Failure(draft.Error);
        }
        else await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.QueueStatusChanged,
            entityType: "QueueEntry",
            entityId: entry.QueueEntryId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                changedFields = new[]
                {
                    nameof(QueueEntry.Status),
                    nameof(QueueEntry.IsPriority),
                    nameof(QueueEntry.DentistId),
                    nameof(QueueEntry.UpdatedAt)
                }
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<QueueEntryResponse>.Success(ToResponse(entry));
    }

    public async Task<Result<PagedResult<QueueEntryResponse>>> GetTodayQueueAsync(
        QueueQueryRequest request,
        CancellationToken ct = default)
    {
        var targetDate = request.Date ?? _vietnamClock.Today;
        QueueStatus? status = request.Status.HasValue ? (QueueStatus)request.Status.Value : null;

        var (items, totalCount) = await _queueRepository.GetTodayQueueAsync(
            targetDate,
            status,
            request.DentistId,
            request.Page,
            request.PageSize,
            ct);

        var page = PagedResult<QueueEntryResponse>.Create(
            items.Select(ToResponse).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return Result<PagedResult<QueueEntryResponse>>.Success(page);
    }

    public async Task<Result<QueueEntryDetailResponse>> GetQueueEntryDetailAsync(
        int queueEntryId,
        CancellationToken ct = default)
    {
        var entry = await _queueRepository.GetByIdAsync(queueEntryId, ct);
        if (entry is null)
            return Result<QueueEntryDetailResponse>.Failure(Error.QueueEntryNotFound);

        var histories = await _queueRepository.GetStatusHistoriesAsync(queueEntryId, ct);
        var detail = new QueueEntryDetailResponse(
            ToResponse(entry),
            histories.Select(ToHistoryResponse).ToList());

        return Result<QueueEntryDetailResponse>.Success(detail);
    }

    public async Task<Result<IReadOnlyList<CallingDentistResponse>>> GetCallingDentistsAsync(string? actorRole, CancellationToken ct = default)
    {
        if (actorRole is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant))
            return Result<IReadOnlyList<CallingDentistResponse>>.Failure(Error.Forbidden);
        var dentists = await _userRepository.GetActiveDentistsAsync(ct);
        return Result<IReadOnlyList<CallingDentistResponse>>.Success(dentists.Select(d => new CallingDentistResponse(d.UserId, d.FullName)).ToArray());
    }

    public async Task<Result<QueueEntryResponse>> CallNextAsync(
        int actorUserId,
        string actorRole,
        CallNextRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // DL-258: invitation is distinct from starting a clinical Visit.
        if (actorRole is not (RoleCodes.Dentist or RoleCodes.Admin or RoleCodes.Assistant))
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        if (actorUserId <= 0 || request.DentistId is <= 0)
            return Result<QueueEntryResponse>.Failure(Error.Validation);
        if (actorRole == RoleCodes.Dentist && request.DentistId.HasValue && request.DentistId != actorUserId)
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);

        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(
                transactionCt => CallNextCoreAsync(actorUserId, actorRole, request, ipAddress, transactionCt), ct);
        }
        catch (ConcurrentOperationException) { return Result<QueueEntryResponse>.Failure(Error.QueueConcurrent); }
    }

    private async Task<Result<QueueEntryResponse>> CallNextCoreAsync(
        int actorUserId, string actorRole, CallNextRequest request, string? ipAddress, CancellationToken ct)
    {

        int targetDentistId;
        if (actorRole == RoleCodes.Dentist)
        {
            targetDentistId = actorUserId;
            var actor = await _userRepository.FindByIdAsync(actorUserId, ct);
            if (actor is null || !actor.IsActive || actor.Role?.RoleCode != RoleCodes.Dentist)
                return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);
        }
        else
        {
            if (!request.DentistId.HasValue)
                return Result<QueueEntryResponse>.Failure(Error.QueueDentistRequired);

            var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
            if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                return Result<QueueEntryResponse>.Failure(Error.Validation.Code, "Nha sĩ được chỉ định không hợp lệ hoặc không hoạt động.");

            targetDentistId = request.DentistId.Value;
        }

        var today = _vietnamClock.Today;
        if (await _queueRepository.HasCallingForDentistAsync(today, targetDentistId, ct))
            return Result<QueueEntryResponse>.Failure(Error.QueueDentistAlreadyCalling);
        var nextEntry = await _queueRepository.GetNextWaitingEntryAsync(today, targetDentistId, ct, assignedOnly: actorRole == RoleCodes.Assistant);
        // Returning from imaging resumes the same Visit; only a different active Visit blocks the dentist.
        if (await _visitRepository.HasActiveVisitForDentistAsync(targetDentistId, ct, nextEntry?.VisitId ?? nextEntry?.Visit?.VisitId))
            return Result<QueueEntryResponse>.Failure(Error.DentistHasActiveVisit);
        if (nextEntry is null)
            return Result<QueueEntryResponse>.Failure(Error.QueueNoWaitingPatients);

        if (nextEntry.Status != QueueStatus.Waiting || nextEntry.QueueDate != today || nextEntry.Visit is null ||
            nextEntry.Visit.IsLocked || nextEntry.Visit.Status is not (VisitStatuses.Created or VisitStatuses.InProgress) ||
            (nextEntry.DentistId.HasValue && nextEntry.DentistId != targetDentistId) ||
            (nextEntry.Visit.DentistId.HasValue && nextEntry.Visit.DentistId != targetDentistId))
            return Result<QueueEntryResponse>.Failure(Error.QueueInvalidStateTransition);
        if (actorRole == RoleCodes.Assistant && nextEntry.DentistId != targetDentistId)
            return Result<QueueEntryResponse>.Failure(Error.QueueTransitionForbidden);

        nextEntry.Status = QueueStatus.Calling;
        nextEntry.DentistId = targetDentistId;
        nextEntry.UpdatedAt = DateTime.UtcNow;

        var history = new QueueStatusHistory
        {
            QueueEntryId = nextEntry.QueueEntryId,
            FromStatus = QueueStatus.Waiting,
            ToStatus = QueueStatus.Calling,
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow,
            Reason = "Mời vào phòng (Calling - DL-258)"
        };

        await _queueRepository.AddStatusHistoryAsync(history, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogRequiredAsync(
            action: AuditActions.QueueStatusChanged,
            entityType: "QueueEntry",
            entityId: nextEntry.QueueEntryId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                action = "CallNext",
                queueEntryId = nextEntry.QueueEntryId,
                dentistId = targetDentistId,
                queueNumber = nextEntry.QueueNumber,
                fromStatus = (int)QueueStatus.Waiting,
                toStatus = (int)QueueStatus.Calling
            }),
            ipAddress: ipAddress,
            ct: ct);

        var refreshed = await _queueRepository.GetByIdAsync(nextEntry.QueueEntryId, ct);
        return Result<QueueEntryResponse>.Success(ToResponse(refreshed ?? nextEntry));
    }

    private static QueueEntryResponse ToResponse(QueueEntry q)
        => new(
            q.QueueEntryId,
            q.PatientId,
            q.Patient?.PatientCode ?? string.Empty,
            q.Patient?.FullName ?? string.Empty,
            q.Patient?.Phone ?? string.Empty,
            q.AppointmentId,
            q.DentistId,
            q.Dentist?.FullName,
            q.VisitId,
            q.QueueDate,
            q.QueueNumber,
            q.IsPriority,
            q.CheckInTime,
            (int)q.Status,
            QueueStatusDescriptions.GetDescription(q.Status),
            q.Notes,
            q.CreatedAt,
            q.Status == QueueStatus.Calling && q.Visit is { Status: VisitStatuses.Created, IsLocked: false, StartedAt: null }
        );

    private static QueueStatusHistoryResponse ToHistoryResponse(QueueStatusHistory h)
        => new(
            h.QueueStatusHistoryId,
            h.QueueEntryId,
            (int?)h.FromStatus,
            h.FromStatus.HasValue ? QueueStatusDescriptions.GetDescription(h.FromStatus.Value) : null,
            (int)h.ToStatus,
            QueueStatusDescriptions.GetDescription(h.ToStatus),
            h.ChangedByUserId,
            h.ChangedByUser?.FullName ?? string.Empty,
            h.ChangedAt,
            h.Reason
        );

    public async Task<Result<PublicQueueMonitorResponse>> GetPublicQueueDisplayAsync(CancellationToken ct = default)
    {
        var today = _vietnamClock.Today;
        var entries = await _queueRepository.GetPublicDisplayQueueAsync(today, ct);

        var inConsultation = entries
            .Where(q => q.Status is QueueStatus.InConsultation or QueueStatus.Calling)
            .OrderByDescending(q => q.Status == QueueStatus.Calling)
            .ThenByDescending(q => q.UpdatedAt ?? q.CreatedAt)
            .Take(5)
            .Select(ToPublicResponse)
            .ToList();

        var waiting = entries
            .Where(q => q.Status == QueueStatus.Waiting)
            .OrderByDescending(q => q.StatusHistories.Any(h => h.FromStatus == QueueStatus.InImaging && h.ToStatus == QueueStatus.Waiting))
            .ThenByDescending(q => q.StatusHistories.Any(h => h.FromStatus == QueueStatus.Skipped && h.ToStatus == QueueStatus.Waiting))
            .ThenByDescending(q => q.IsPriority)
            .ThenBy(q => q.IsPriority && q.Appointment != null ? q.Appointment.SlotTime : TimeOnly.MaxValue)
            .ThenBy(q => q.CheckInTime)
            .ThenBy(q => q.QueueNumber)
            .Take(15)
            .Select(ToPublicResponse)
            .ToList();

        return Result<PublicQueueMonitorResponse>.Success(new PublicQueueMonitorResponse(inConsultation, waiting));
    }

    public static string MaskPatientName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Bệnh nhân";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            var w = parts[0];
            return w.Length <= 2 ? w : $"{w[0]}***{w[^1]}";
        }

        if (parts.Length == 2)
        {
            return $"{parts[0]} *** {parts[1]}";
        }

        var first = parts[0];
        var last = parts[^1];
        var middle = string.Join(" ", parts.Skip(1).Take(parts.Length - 2).Select(m => $"{m[0]}***"));
        return $"{first} {middle} {last}";
    }

    public static string FormatQueueNumber(int queueNumber, bool isPriority)
    {
        var prefix = isPriority ? "A" : "A";
        return $"{prefix}-{queueNumber:D3}";
    }

    private static PublicQueueItemResponse ToPublicResponse(QueueEntry q)
        => new(
            q.QueueEntryId,
            q.QueueNumber,
            FormatQueueNumber(q.QueueNumber, q.IsPriority),
            MaskPatientName(q.Patient?.FullName),
            q.Dentist?.FullName,
            (int)q.Status,
            QueueStatusDescriptions.GetDescription(q.Status),
            q.IsPriority
        );

}
