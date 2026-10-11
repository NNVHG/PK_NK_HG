using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed class ReceiptPrintService(IReceiptRepository repository, ReceiptRequestValidator validator)
{
    public async Task<Result<ReceiptPrintDto>> GetAsync(ReceiptRequest request, int actorId, string? role,
        bool print, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<ReceiptPrintDto>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<ReceiptPrintDto>.Failure(Error.Validation);
        return print ? await repository.PrintAsync(request, actorId, ip, ct) : await repository.GetAsync(request, ct);
    }
}
