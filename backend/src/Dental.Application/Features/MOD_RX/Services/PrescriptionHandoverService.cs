using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_RX.Services;
public sealed class PrescriptionDispenseValidator : AbstractValidator<PrescriptionDispenseRequest>
{
    public PrescriptionDispenseValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.ReceiverName).MaximumLength(100).Must(x => x is null || !x.Any(char.IsControl));
        RuleFor(x => x.ReceiverPhone).Must(x => string.IsNullOrWhiteSpace(x) || System.Text.RegularExpressions.Regex.IsMatch(x.Trim(), @"^0[0-9]{9}$"));
        RuleFor(x => x).Must(x => x.ReceivedByProxy || string.IsNullOrWhiteSpace(x.ReceiverName) && string.IsNullOrWhiteSpace(x.ReceiverPhone));
        RuleFor(x => x.OverrideReason).Must(x => x is null || x.Length <= 500 && !x.Any(char.IsControl));
        When(x => x.OverrideExpired, () => RuleFor(x => x.OverrideReason).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length >= 5));
    }
}
public sealed class PrescriptionUndoDispenseValidator : AbstractValidator<PrescriptionUndoDispenseRequest>
{
    public PrescriptionUndoDispenseValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Reason).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length >= 5 && x.Length <= 500 && !x.Any(char.IsControl));
    }
}
public sealed class PrescriptionHandoverService(IPrescriptionHandoverRepository repository)
{
    public static bool CanWrite(string? role) => role is RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Assistant;
    public Task<Result<PrescriptionHandoverStatus>> ReadAsync(int id, int actorId, string? role, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Dentist or RoleCodes.Patient)) return Denied();
        return id <= 0 ? Invalid() : repository.ReadAsync(id, actorId, role, ct);
    }
    public Task<Result<PrescriptionHandoverStatus>> DispenseAsync(int id, int actorId, string? role, PrescriptionDispenseRequest request, string? ip = null, CancellationToken ct = default)
    {
        if (actorId <= 0 || !CanWrite(role) || request.OverrideExpired && role != RoleCodes.Admin) return Denied();
        return id <= 0 || !new PrescriptionDispenseValidator().Validate(request).IsValid ? Invalid() : repository.DispenseAsync(id, actorId, role!, request, ip, ct);
    }
    public Task<Result<PrescriptionHandoverStatus>> UndoAsync(int id, int actorId, string? role, PrescriptionUndoDispenseRequest request, string? ip = null, CancellationToken ct = default)
    {
        if (actorId <= 0 || !CanWrite(role)) return Denied();
        return id <= 0 || !new PrescriptionUndoDispenseValidator().Validate(request).IsValid ? Invalid() : repository.UndoAsync(id, actorId, role!, request, ip, ct);
    }
    private static Task<Result<PrescriptionHandoverStatus>> Denied() => Task.FromResult(Result<PrescriptionHandoverStatus>.Failure(Error.Forbidden));
    private static Task<Result<PrescriptionHandoverStatus>> Invalid() => Task.FromResult(Result<PrescriptionHandoverStatus>.Failure(Error.Validation));
}
