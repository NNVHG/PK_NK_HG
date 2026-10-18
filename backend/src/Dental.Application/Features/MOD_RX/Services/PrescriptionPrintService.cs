using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_RX.Services;
public sealed class PrescriptionPrintValidator : AbstractValidator<PrescriptionPrintRequest>
{
    public PrescriptionPrintValidator() => RuleFor(x => x.ExpectedVersion).GreaterThan(0);
}
public sealed partial class PrescriptionService
{
    public Task<Result<PrescriptionPrintDocument>> PreparePrintAsync(int visitId, int prescriptionId, int actorId, string? role,
        PrescriptionPrintRequest request, string? ip = null, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient))
            return Task.FromResult(Result<PrescriptionPrintDocument>.Failure(Error.Forbidden));
        if (visitId <= 0 || prescriptionId <= 0 || !new PrescriptionPrintValidator().Validate(request).IsValid)
            return Task.FromResult(Result<PrescriptionPrintDocument>.Failure(Error.Validation));
        return repository.PreparePrintAsync(visitId, prescriptionId, actorId, role, request, ip, ct);
    }
}
