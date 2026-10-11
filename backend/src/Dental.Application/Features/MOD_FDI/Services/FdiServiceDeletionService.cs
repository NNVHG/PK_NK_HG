using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_FDI.Services;
public sealed class FdiServiceDeletionService(IFdiServiceDeletionRepository repository, DeleteVisitServiceRequestValidator validator)
{
    public async Task<Result> DeleteAsync(DeleteVisitServiceRequest request, int actorId, string? role, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist)) return Result.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result.Failure(Error.Validation);
        return await repository.DeleteAsync(request with { Reason = request.Reason.Trim() }, actorId, role, ip, ct);
    }
}
