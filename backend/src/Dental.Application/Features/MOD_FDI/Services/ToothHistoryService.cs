using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_FDI.Services;
public sealed class ToothHistoryService(IPatientRepository patients, IToothHistoryRepository history, ToothHistoryRequestValidator validator)
{
    public async Task<Result<PagedResult<ToothHistoryItemResponse>>> GetAsync(ToothHistoryRequest request,
        int actorId, string? role, CancellationToken ct = default)
    {
        if (actorId <= 0 || role != RoleCodes.Patient && !RoleCodes.StaffRoles.Contains(role))
            return Result<PagedResult<ToothHistoryItemResponse>>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid)
            return Result<PagedResult<ToothHistoryItemResponse>>.Failure(Error.Validation);
        var patient = await patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null || role == RoleCodes.Patient && patient.UserId != actorId)
            return Result<PagedResult<ToothHistoryItemResponse>>.Failure(Error.NotFound);
        return Result<PagedResult<ToothHistoryItemResponse>>.Success(await history.GetAsync(request, ct));
    }
}
