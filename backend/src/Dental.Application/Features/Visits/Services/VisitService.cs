using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.Visits.Services;

public sealed class VisitService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public VisitService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<Result<VisitResponse>> CreateVisitAsync(
        int patientId,
        int createdByUserId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null)
            return Result<VisitResponse>.Failure(Error.NotFound);
        if (!patient.IsActive)
            return Result<VisitResponse>.Failure(Error.PatientInactive);

        // [CẦN XÁC NHẬN] Hiện từ chối tạo Visit mới nếu bệnh nhân còn lần khám Created/InProgress.
        if (await _visitRepository.HasOpenVisitAsync(patientId, ct))
            return Result<VisitResponse>.Failure(Error.VisitAlreadyOpen);

        var visit = new Visit
        {
            PatientId = patientId,
            Status = VisitStatuses.Created,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
        };

        await _visitRepository.AddAsync(visit, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.VisitCreated,
            entityType: "Visit",
            entityId: visit.VisitId,
            userId: createdByUserId,
            detail: JsonSerializer.Serialize(new { action = "create_visit" }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<VisitResponse>.Success(ToResponse(visit));
    }

    public async Task<Result<PagedResult<VisitResponse>>> GetPatientVisitsAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        VisitQueryRequest request,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null ||
            (currentRoleCode == RoleCodes.Patient && patient.UserId != currentUserId))
        {
            return Result<PagedResult<VisitResponse>>.Failure(Error.NotFound);
        }

        var visits = await _visitRepository.GetPatientVisitsAsync(patientId, request.Page, request.PageSize, ct);
        var page = PagedResult<VisitResponse>.Create(
            visits.Items.Select(ToResponse).ToList(),
            visits.TotalCount,
            visits.Page,
            visits.PageSize);

        return Result<PagedResult<VisitResponse>>.Success(page);
    }

    public async Task<Result<VisitResponse>> GetVisitByIdAsync(
        int visitId,
        int currentUserId,
        string? currentRoleCode,
        CancellationToken ct = default)
    {
        var visit = await _visitRepository.GetByIdAsync(visitId, ct);
        if (visit is null)
            return Result<VisitResponse>.Failure(Error.NotFound);

        if (currentRoleCode == RoleCodes.Patient)
        {
            var patient = await _patientRepository.GetByIdAsync(visit.PatientId, ct);
            if (patient is null || patient.UserId != currentUserId)
                return Result<VisitResponse>.Failure(Error.NotFound);
        }

        return Result<VisitResponse>.Success(ToResponse(visit));
    }

    public async Task<Result<VisitResponse>> UpdateDiagnosisAsync(
        int visitId,
        UpdateVisitDiagnosisRequest request,
        int currentUserId,
        string? currentRoleCode,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (currentRoleCode is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<VisitResponse>.Failure(Error.Forbidden);

        var visit = await _visitRepository.GetForUpdateAsync(visitId, ct);
        if (visit is null)
            return Result<VisitResponse>.Failure(Error.NotFound);

        // DL-085: Sửa chẩn đoán chỉ cho Nha sĩ phụ trách hoặc Admin
        if (currentRoleCode == RoleCodes.Dentist && visit.DentistId != currentUserId)
            return Result<VisitResponse>.Failure(Error.DiagnosisDentistMismatch);

        // DL-085: Chỉ khi InProgress
        if (visit.Status != VisitStatuses.InProgress)
            return Result<VisitResponse>.Failure(Error.DiagnosisVisitNotInProgress);

        visit.Diagnosis = request.Diagnosis.Trim();
        visit.ClinicalNotes = string.IsNullOrWhiteSpace(request.ClinicalNotes) ? null : request.ClinicalNotes.Trim();
        visit.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.VisitDiagnosisUpdated,
            entityType: "Visit",
            entityId: visit.VisitId,
            userId: currentUserId,
            detail: JsonSerializer.Serialize(new
            {
                visitId = visit.VisitId,
                changedFields = new[] { nameof(Visit.Diagnosis), nameof(Visit.ClinicalNotes) },
                hasClinicalNotes = !string.IsNullOrEmpty(visit.ClinicalNotes)
            }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<VisitResponse>.Success(ToResponse(visit));
    }

    private static VisitResponse ToResponse(Visit visit)
        => new(
            visit.VisitId,
            visit.PatientId,
            visit.Status,
            visit.StartedAt,
            visit.EndedAt,
            visit.DentistId,
            visit.CreatedByUserId,
            visit.CreatedAt,
            visit.Diagnosis,
            visit.ClinicalNotes,
            visit.IsLocked);
}
