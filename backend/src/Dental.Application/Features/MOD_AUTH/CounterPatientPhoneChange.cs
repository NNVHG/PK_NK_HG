using Dental.Application.Common;
using FluentValidation;
namespace Dental.Application.Features.MOD_AUTH;

public sealed record CounterPhoneRequest(string NewPhone, bool ConfirmedAtCounter);
public sealed record CounterPhoneReceipt(Guid RequestId, bool RequiresOwnerConfirmation);
public sealed record CounterPhoneSuggestion(Guid RequestId, int AppointmentId, string NewPhone, DateTime CreatedAtUtc);
public interface ICounterPatientPhoneRepository
{
    Task<Result<CounterPhoneReceipt>> CreateAsync(int actorId, int appointmentId, CounterPhoneRequest request, string? ip, CancellationToken ct);
    Task<Result<CounterPhoneSuggestion?>> GetOwnAsync(int userId, CancellationToken ct);
}
public sealed class CounterPhoneRequestValidator : AbstractValidator<CounterPhoneRequest>
{
    public CounterPhoneRequestValidator()
    {
        RuleFor(x => x.NewPhone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
        RuleFor(x => x.ConfirmedAtCounter).Equal(true);
    }
}
public sealed class CounterPatientPhoneService(ICounterPatientPhoneRepository repository)
{
    public Task<Result<CounterPhoneReceipt>> CreateAsync(int actorId, int appointmentId, CounterPhoneRequest request, string? ip, CancellationToken ct)
        => appointmentId > 0 && new CounterPhoneRequestValidator().Validate(request).IsValid
            ? repository.CreateAsync(actorId, appointmentId, request, ip, ct)
            : Task.FromResult(Result<CounterPhoneReceipt>.Failure(Error.Validation));
    public Task<Result<CounterPhoneSuggestion?>> GetOwnAsync(int userId, CancellationToken ct) => repository.GetOwnAsync(userId, ct);
}
