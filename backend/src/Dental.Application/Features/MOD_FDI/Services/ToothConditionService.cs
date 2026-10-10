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
            CreatedAt = DateTime.UtcNow
        };
        await conditions.AddAsync(row, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync("MOD_FDI_CONDITION_CREATED", "ToothCondition", row.Id, userId,
            "{\"action\":\"condition_created\"}", ip, ct);
        return Result<ToothConditionResponse>.Success(Map(row));
    }

    private static ToothConditionResponse Map(ToothCondition x)
        => new(x.Id, x.VisitId, x.ToothNumber, x.Surface, x.ConditionCode, x.Note, x.CreatedAt);
}
