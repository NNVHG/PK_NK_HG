using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class InvoiceDiscountService(IInvoiceDiscountRepository repository,
    DiscountPresetRequestValidator presetValidator, ApplyInvoiceDiscountRequestValidator applyValidator)
{
    public async Task<Result<IReadOnlyList<DiscountPresetResponse>>> GetPresetsAsync(string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<IReadOnlyList<DiscountPresetResponse>>.Failure(Error.Forbidden);
        return Result<IReadOnlyList<DiscountPresetResponse>>.Success((await repository.GetPresetsAsync(role == RoleCodes.Admin, ct)).Select(Map).ToArray());
    }
    public async Task<Result<DiscountPresetResponse>> SavePresetAsync(int? id, DiscountPresetRequest request,
        int actorId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<DiscountPresetResponse>.Failure(Error.Forbidden);
        if (id <= 0 || !(await presetValidator.ValidateAsync(request, ct)).IsValid) return Result<DiscountPresetResponse>.Failure(Error.Validation);
        var result = await repository.SavePresetAsync(id, request with { Code = request.Code.ToUpperInvariant(), Name = request.Name.Trim() }, actorId, ip, ct);
        return result.IsFailure ? Result<DiscountPresetResponse>.Failure(result.Error) : Result<DiscountPresetResponse>.Success(Map(result.Value!));
    }
    public async Task<Result<InvoiceDiscountResponse>> ApplyAsync(int invoiceId, ApplyInvoiceDiscountRequest request,
        int actorId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist) || request.Mode == "Custom" && role != RoleCodes.Admin)
            return Result<InvoiceDiscountResponse>.Failure(Error.Forbidden);
        if (invoiceId <= 0 || !(await applyValidator.ValidateAsync(request, ct)).IsValid) return Result<InvoiceDiscountResponse>.Failure(Error.Validation);
        return await repository.ApplyAsync(invoiceId, request, actorId, role, ip, ct);
    }
    private static DiscountPresetResponse Map(DiscountPreset x) => new(x.Id, x.Code, x.Name, x.DiscountType, x.Value, x.IsActive);
}
