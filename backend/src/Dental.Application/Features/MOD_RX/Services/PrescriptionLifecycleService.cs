using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_RX.Services;
public sealed class PrescriptionCancellationValidator : AbstractValidator<PrescriptionCancellationRequest>
{
    public PrescriptionCancellationValidator() => RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
}
public sealed partial class PrescriptionService
{
    public Task<Result<PrescriptionCancellationResult>> CancelAsync(int visitId, int prescriptionId, int actorId, string? role,
        PrescriptionCancellationRequest request, string? ip = null, CancellationToken ct = default)
    {
        if (role != RoleCodes.Dentist || actorId <= 0) return Task.FromResult(Result<PrescriptionCancellationResult>.Failure(Error.Forbidden));
        if (visitId <= 0 || prescriptionId <= 0 || !new PrescriptionCancellationValidator().Validate(request).IsValid)
            return Task.FromResult(Result<PrescriptionCancellationResult>.Failure(Error.Validation));
        return repository.CancelAsync(visitId, prescriptionId, actorId, request, ip, ct);
    }
    public Task<Result<PagedResult<PrescriptionResponse>>> HistoryAsync(int visitId, int actorId, string? role, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient))
            return Task.FromResult(Result<PagedResult<PrescriptionResponse>>.Failure(Error.Forbidden));
        if (visitId <= 0 || page is < 1 or > 1000000 || pageSize is < 1 or > 50)
            return Task.FromResult(Result<PagedResult<PrescriptionResponse>>.Failure(Error.Validation));
        return repository.HistoryAsync(visitId, actorId, role, page, pageSize, ct);
    }
}
