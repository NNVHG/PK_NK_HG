using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_MST.Services;
public sealed record ClinicPrintSettings(string? Address, string? Phone, string Revision);
public sealed record ClinicPrintSettingsRequest(string Address, string Phone, string ExpectedRevision);
public sealed class ClinicPrintSettingsValidator : AbstractValidator<ClinicPrintSettingsRequest>
{
    public ClinicPrintSettingsValidator()
    {
        RuleFor(x => x.Address).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(500).Must(x => x is null || !x.Any(char.IsControl));
        RuleFor(x => x.Phone).NotEmpty().Matches("^[0-9+ ().-]{8,30}$");
        RuleFor(x => x.ExpectedRevision).NotEmpty().Matches("^[A-F0-9]{64}$");
    }
}
public sealed class ClinicPrintSettingsService(IClinicPrintSettingsRepository repository)
{
    public Task<Result<ClinicPrintSettings>> ReadAsync(int actor, string? role, CancellationToken ct = default) =>
        role != RoleCodes.Admin || actor <= 0 ? Task.FromResult(Result<ClinicPrintSettings>.Failure(Error.Forbidden)) : repository.ReadAsync(actor, ct);
    public Task<Result<ClinicPrintSettings>> SaveAsync(int actor, string? role, ClinicPrintSettingsRequest request, string? ip, CancellationToken ct = default) =>
        role != RoleCodes.Admin || actor <= 0 ? Task.FromResult(Result<ClinicPrintSettings>.Failure(Error.Forbidden)) :
        !new ClinicPrintSettingsValidator().Validate(request).IsValid ? Task.FromResult(Result<ClinicPrintSettings>.Failure(Error.Validation)) : repository.SaveAsync(actor, request, ip, ct);
}
