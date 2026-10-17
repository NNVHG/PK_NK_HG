using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_PAT;

public sealed record CitizenIdRequest(string CitizenId, int ExpectedVersion);
public sealed record CitizenIdStatus(int PatientId, string? Masked, int Version);

public sealed class CitizenIdRequestValidator : AbstractValidator<CitizenIdRequest>
{
    public CitizenIdRequestValidator()
    {
        RuleFor(x => x.CitizenId).NotEmpty().Length(12).Matches("^[0-9]{12}$");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public static class CitizenIdIdentity
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Mask(string value) => value[..3] + "******" + value[^3..];
    public static bool Matches(string? hash, string value) => hash is not null && hash.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(value)));
}

public sealed class PatientCitizenIdService(IPatientCitizenIdRepository repository)
{
    public Task<Result<CitizenIdStatus>> ReadAsync(int patientId, int actorId, string? role, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Receptionist or RoleCodes.Dentist or RoleCodes.Assistant or RoleCodes.Patient))
            return Task.FromResult(Result<CitizenIdStatus>.Failure(Error.Forbidden));
        if (patientId <= 0) return Task.FromResult(Result<CitizenIdStatus>.Failure(Error.Validation));
        return repository.ReadAsync(patientId, actorId, role, ct);
    }
    public Task<Result<CitizenIdStatus>> SaveAsync(int patientId, int actorId, string? role, CitizenIdRequest request, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Task.FromResult(Result<CitizenIdStatus>.Failure(Error.Forbidden));
        if (patientId <= 0 || !new CitizenIdRequestValidator().Validate(request).IsValid)
            return Task.FromResult(Result<CitizenIdStatus>.Failure(Error.Validation));
        return repository.SaveAsync(patientId, actorId, role, request, ip, ct);
    }
}
