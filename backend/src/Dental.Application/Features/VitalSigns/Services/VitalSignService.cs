using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.VitalSigns.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.VitalSigns.Services;

public sealed class VitalSignService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public VitalSignService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository,
        IVitalSignRepository vitalSignRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
        _vitalSignRepository = vitalSignRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<Result<VitalSignRecordResponse>> RecordAsync(
        int visitId,
        int recordedByUserId,
        RecordVitalSignsRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var visit = await _visitRepository.GetByIdAsync(visitId, ct);
        if (visit is null)
            return Result<VitalSignRecordResponse>.Failure(Error.NotFound);

        if (visit.Status is VisitStatuses.Completed or VisitStatuses.Cancelled)
            return Result<VitalSignRecordResponse>.Failure(Error.VitalSignsVisitClosed);

        var record = new VitalSignRecord
        {
            PatientId = visit.PatientId,
            VisitId = visit.VisitId,
            SystolicBp = request.SystolicBp,
            DiastolicBp = request.DiastolicBp,
            PulseBpm = request.PulseBpm,
            TemperatureC = request.TemperatureC,
            Note = NormalizeOptional(request.Note),
            RecordedByUserId = recordedByUserId,
            CreatedAt = DateTime.UtcNow,
        };

        await _vitalSignRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
            action: AuditActions.VitalSignsRecorded,
            entityType: "VitalSignRecord",
            entityId: record.VitalSignRecordId,
            userId: recordedByUserId,
            detail: JsonSerializer.Serialize(new { visitId = visit.VisitId }),
            ipAddress: ipAddress,
            ct: ct);

        return Result<VitalSignRecordResponse>.Success(ToResponse(record));
    }

    public async Task<Result<PagedResult<VitalSignRecordResponse>>> GetPatientRecordsAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        VitalSignQueryRequest request,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null ||
            (currentRoleCode == RoleCodes.Patient && patient.UserId != currentUserId))
        {
            return Result<PagedResult<VitalSignRecordResponse>>.Failure(Error.NotFound);
        }

        var records = await _vitalSignRepository.GetPatientRecordsAsync(
            patientId, request.Page, request.PageSize, ct);
        var page = PagedResult<VitalSignRecordResponse>.Create(
            records.Items.Select(ToResponse).ToList(),
            records.TotalCount,
            records.Page,
            records.PageSize);

        return Result<PagedResult<VitalSignRecordResponse>>.Success(page);
    }

    private static VitalSignRecordResponse ToResponse(VitalSignRecord record)
        => new(
            record.VitalSignRecordId,
            record.PatientId,
            record.VisitId,
            record.SystolicBp,
            record.DiastolicBp,
            record.PulseBpm,
            record.TemperatureC,
            record.Note,
            record.RecordedByUserId,
            record.CreatedAt);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
