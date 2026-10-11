using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class VisitReopenService(IVisitReopenRepository repository, UnlockVisitRequestValidator validator)
{
    public async Task<Result> ReopenAsync(int visitId, int actor, string? role, UnlockVisitRequest request, string? ip, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result.Failure(Error.Forbidden);
        if (visitId <= 0 || !(await validator.ValidateAsync(request, ct)).IsValid) return Result.Failure(Error.Validation);
        var result = await repository.ReopenAsync(visitId, actor, request.UnlockReason.Trim(), ip, ct);
        if (result.IsFailure) return Result.Failure(result.Error);
        return Result.Success();
    }
}
