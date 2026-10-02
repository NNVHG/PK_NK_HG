using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MedicalHistory.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MedicalHistory.Services;

public sealed class MedicalHistoryService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;
    private readonly IMedicalHistoryRepository _medicalHistoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public MedicalHistoryService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository,
        IMedicalHistoryRepository medicalHistoryRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
        _medicalHistoryRepository = medicalHistoryRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<Result<MedicalHistoryRecordResponse>> RecordAsync(
        int visitId,
        int recordedByUserId,
        RecordMedicalHistoryRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var visit = await _visitRepository.GetByIdAsync(visitId, ct);
        if (visit is null)
            return Result<MedicalHistoryRecordResponse>.Failure(Error.NotFound);

        if (visit.Status is VisitStatuses.Completed or VisitStatuses.Cancelled)
            return Result<MedicalHistoryRecordResponse>.Failure(Error.MedicalHistoryVisitClosed);

        var record = new MedicalHistoryRecord
        {
            PatientId = visit.PatientId,
            VisitId = visit.VisitId,
            Note = NormalizeOptional(request.Note),
            RecordedByUserId = recordedByUserId,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(item => new MedicalHistoryItem
            {
                Type = item.Type,
                Name = item.Name.Trim(),
                IsCritical = item.IsCritical,
                Detail = NormalizeOptional(item.Detail),
            }).ToList(),
        };

        await _medicalHistoryRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.MedicalHistoryRecorded,
            entityType: "MedicalHistoryRecord",
            entityId: record.RecordId,
            userId: recordedByUserId,
            detail: JsonSerializer.Serialize(new { visitId = visit.VisitId, itemCount = record.Items.Count }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<MedicalHistoryRecordResponse>.Success(ToResponse(record));
    }

    public async Task<Result<MedicalHistoryRecordResponse>> GetLatestAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        CancellationToken ct = default)
    {
        var access = await CheckPatientAccessAsync(patientId, currentUserId, currentRoleCode, ct);
        if (access.IsFailure)
            return Result<MedicalHistoryRecordResponse>.Failure(access.Error);

        var record = await _medicalHistoryRepository.GetLatestByPatientIdAsync(patientId, ct);
        return record is null
            ? Result<MedicalHistoryRecordResponse>.Failure(Error.NotFound)
            : Result<MedicalHistoryRecordResponse>.Success(ToResponse(record));
    }

    public async Task<Result<PagedResult<MedicalHistoryRecordResponse>>> GetHistoryAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        MedicalHistoryQueryRequest request,
        CancellationToken ct = default)
    {
        var access = await CheckPatientAccessAsync(patientId, currentUserId, currentRoleCode, ct);
        if (access.IsFailure)
            return Result<PagedResult<MedicalHistoryRecordResponse>>.Failure(access.Error);

        var records = await _medicalHistoryRepository.GetPatientHistoryAsync(
            patientId, request.Page, request.PageSize, ct);
        var page = PagedResult<MedicalHistoryRecordResponse>.Create(
            records.Items.Select(ToResponse).ToList(),
            records.TotalCount,
            records.Page,
            records.PageSize);

        return Result<PagedResult<MedicalHistoryRecordResponse>>.Success(page);
    }

    private async Task<Result> CheckPatientAccessAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        CancellationToken ct)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null ||
            (currentRoleCode == RoleCodes.Patient && patient.UserId != currentUserId))
        {
            return Result.Failure(Error.NotFound);
        }

        return Result.Success();
    }

    private static MedicalHistoryRecordResponse ToResponse(MedicalHistoryRecord record)
        => new(
            record.RecordId,
            record.PatientId,
            record.VisitId,
            record.Note,
            record.RecordedByUserId,
            record.CreatedAt,
            record.Items.Select(item => new MedicalHistoryItemResponse(
                item.ItemId,
                item.Type,
                item.Name,
                item.IsCritical,
                item.Detail)).ToList());

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
