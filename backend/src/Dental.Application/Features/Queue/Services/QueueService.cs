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

    public async Task<Result<QueueEntryResponse>> UpdateQueueStatusAsync(
        int queueEntryId,
        int actorUserId,
        string actorRole,
        UpdateQueueStatusRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var entry = await _queueRepository.GetByIdAsync(queueEntryId, ct);
        if (entry is null)
            return Result<QueueEntryResponse>.Failure(Error.QueueEntryNotFound);

        var targetStatus = (QueueStatus)request.NewStatus;
        var transitionValidation = QueueStateMachine.ValidateTransition(entry.Status, targetStatus, actorRole);
        if (transitionValidation.IsFailure)
            return Result<QueueEntryResponse>.Failure(transitionValidation.Error);

        if (targetStatus == QueueStatus.InConsultation)
        {
            if (request.DentistId.HasValue)
            {
                var dentist = await _userRepository.FindByIdAsync(request.DentistId.Value, ct);
                if (dentist is null || !dentist.IsActive || dentist.Role?.RoleCode != RoleCodes.Dentist)
                    return Result<QueueEntryResponse>.Failure(Error.Validation.Code, "Nha sĩ được chọn không hợp lệ hoặc không hoạt động.");
                entry.DentistId = request.DentistId.Value;
            }

            // DL-013: Bắt buộc chỉ định Nha sĩ khi bắt đầu khám
            if (!entry.DentistId.HasValue)
                return Result<QueueEntryResponse>.Failure(Error.QueueDentistRequired);

            if (entry.Visit is not null)
            {
                entry.Visit.Status = VisitStatuses.InProgress;
                entry.Visit.DentistId = entry.DentistId;
                entry.Visit.StartedAt ??= DateTime.UtcNow;
                entry.Visit.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (targetStatus == QueueStatus.Waiting && entry.Status == QueueStatus.InImaging)
        {
            // DL-053: Sau khi chụp X-quang xong quay lại hàng chờ với độ ưu tiên cao nhất, giữ DentistId
            entry.IsPriority = true;
        }
        else if (targetStatus == QueueStatus.Completed)
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
            q.CreatedAt
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

}

