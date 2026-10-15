using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_FDI.Services;

public sealed class ToothConditionService(IVisitRepository visits, IPatientRepository patients,
    IToothConditionRepository conditions, IUnitOfWork unitOfWork, IAuditLogger audit,
    ToothConditionRequestValidator validator)
{
    public static readonly Error VisitClosed = new("FDI_003", "Chỉ ghi tình trạng răng khi lần khám đang diễn ra và chưa khóa.");

    public async Task<Result<IReadOnlyList<ToothConditionResponse>>> GetAsync(int visitId, int userId,
        string? role, CancellationToken ct = default)
    {
        if (role != RoleCodes.Patient && !RoleCodes.StaffRoles.Contains(role))
            return Result<IReadOnlyList<ToothConditionResponse>>.Failure(Error.Forbidden);
        var visit = await visits.GetByIdAsync(visitId, ct);
        if (visit is null) return Result<IReadOnlyList<ToothConditionResponse>>.Failure(Error.NotFound);
        if (role == RoleCodes.Patient)
        {
            var patient = await patients.GetByIdAsync(visit.PatientId, ct);
            if (patient?.UserId != userId)
                return Result<IReadOnlyList<ToothConditionResponse>>.Failure(Error.NotFound);
        }
        var rows = await conditions.GetForVisitAsync(visitId, ct);
        return Result<IReadOnlyList<ToothConditionResponse>>.Success(rows.Select(Map).ToArray());
    }

    public async Task<Result<IReadOnlyList<LatestToothConditionResponse>>> GetLatestForPatientAsync(int patientId,
        int userId, string? role, CancellationToken ct = default)
    {
        if (patientId <= 0) return Result<IReadOnlyList<LatestToothConditionResponse>>.Failure(Error.Validation);
        if (role != RoleCodes.Patient && !RoleCodes.StaffRoles.Contains(role))
            return Result<IReadOnlyList<LatestToothConditionResponse>>.Failure(Error.Forbidden);
        var patient = await patients.GetByIdAsync(patientId, ct);
        if (patient is null || role == RoleCodes.Patient && patient.UserId != userId)
            return Result<IReadOnlyList<LatestToothConditionResponse>>.Failure(Error.NotFound);
        return Result<IReadOnlyList<LatestToothConditionResponse>>.Success(
            await conditions.GetLatestForPatientAsync(patientId, ct));
    }

    public async Task<Result<ToothConditionResponse>> AddAsync(int visitId, ToothConditionRequest request,
        int userId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<ToothConditionResponse>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid)
            return Result<ToothConditionResponse>.Failure(Error.Validation);
        var visit = await visits.GetForUpdateAsync(visitId, ct);
        if (visit is null) return Result<ToothConditionResponse>.Failure(Error.NotFound);
        if (role == RoleCodes.Dentist && visit.DentistId != userId)
            return Result<ToothConditionResponse>.Failure(Error.Forbidden);
        if (visit.Status != VisitStatuses.InProgress || visit.IsLocked)
            return Result<ToothConditionResponse>.Failure(VisitClosed);
        var row = new ToothCondition
        {
            VisitId = visitId, ToothNumber = request.ToothNumber,
            Surface = FdiCodes.NormalizeSurface(request.Surface), ConditionCode = request.ConditionCode,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await conditions.AddAsync(row, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync("MOD_FDI_CONDITION_CREATED", "ToothCondition", row.Id, userId,
            "{\"action\":\"condition_created\"}", ip, ct);
        return Result<ToothConditionResponse>.Success(Map(row));
    }

    public async Task<Result<ToothConditionResponse>> UpdateAsync(int visitId, int conditionId,
        ToothConditionRequest request, int userId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<ToothConditionResponse>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid)
            return Result<ToothConditionResponse>.Failure(Error.Validation);
        var visit = await visits.GetForUpdateAsync(visitId, ct);
        if (visit is null) return Result<ToothConditionResponse>.Failure(Error.NotFound);
        if (role == RoleCodes.Dentist && visit.DentistId != userId)
            return Result<ToothConditionResponse>.Failure(Error.Forbidden);
        if (visit.Status != VisitStatuses.InProgress || visit.IsLocked)
            return Result<ToothConditionResponse>.Failure(VisitClosed);
        var row = await conditions.GetForUpdateAsync(visitId, conditionId, ct);
        if (row is null) return Result<ToothConditionResponse>.Failure(Error.NotFound);
        row.ToothNumber = request.ToothNumber;
        row.Surface = FdiCodes.NormalizeSurface(request.Surface);
        row.ConditionCode = request.ConditionCode;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync("MOD_FDI_CONDITION_UPDATED", "ToothCondition", row.Id, userId,
            "{\"action\":\"condition_updated\"}", ip, ct);
        return Result<ToothConditionResponse>.Success(Map(row));
    }

    public async Task<Result<bool>> DeleteAsync(int visitId, int conditionId, int userId,
        string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<bool>.Failure(Error.Forbidden);
        var visit = await visits.GetForUpdateAsync(visitId, ct);
        if (visit is null) return Result<bool>.Failure(Error.NotFound);
        if (role == RoleCodes.Dentist && visit.DentistId != userId)
            return Result<bool>.Failure(Error.Forbidden);
        if (visit.Status != VisitStatuses.InProgress || visit.IsLocked)
            return Result<bool>.Failure(VisitClosed);
        var row = await conditions.GetForUpdateAsync(visitId, conditionId, ct);
        if (row is null) return Result<bool>.Failure(Error.NotFound);
        conditions.Remove(row);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync("MOD_FDI_CONDITION_DELETED", "ToothCondition", row.Id, userId,
            "{\"action\":\"condition_deleted\"}", ip, ct);
        return Result<bool>.Success(true);
    }

    private static ToothConditionResponse Map(ToothCondition x)
        => new(x.Id, x.VisitId, x.ToothNumber, x.Surface, x.ConditionCode, x.Note, x.CreatedAt);
}
