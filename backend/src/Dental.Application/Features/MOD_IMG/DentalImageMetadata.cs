using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.MOD_IMG;

public sealed record UpdateDentalImageMetadataRequest(int ExpectedVersion, string Category, int? ToothNumber, string? Notes, bool Confirmed);
public sealed record DentalImageMetadataContext(int Id, int PatientId, int? VisitId, int Version, string OriginalFileName,
    string Category, int? ToothNumber, string? Notes, bool CanEdit);

public sealed class UpdateDentalImageMetadataValidator : AbstractValidator<UpdateDentalImageMetadataRequest>
{
    public UpdateDentalImageMetadataValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Category).Must(x => DentalImageRequestValidator.Categories.Contains(x, StringComparer.Ordinal));
        RuleFor(x => x.ToothNumber).Must(DentalImageRequestValidator.ValidTooth);
        RuleFor(x => x.Notes).MaximumLength(500).Must(x => x is null || !x.Contains('\0'));
    }
}

public interface IDentalImageMetadataRepository
{
    Task<Result<DentalImageMetadataContext>> ReadAsync(int image, int actor, string role, CancellationToken ct);
    Task<Result<DentalImageMetadataContext>> UpdateAsync(int image, int actor, string role, UpdateDentalImageMetadataRequest request, string? ip, CancellationToken ct);
}

public sealed class DentalImageMetadataService(IDentalImageMetadataRepository repository)
{
    private static bool Allowed(int actor, string? role) => actor > 0 && role is Dental.Domain.Constants.RoleCodes.Admin or Dental.Domain.Constants.RoleCodes.Dentist;
    public Task<Result<DentalImageMetadataContext>> ReadAsync(int image, int actor, string? role, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<DentalImageMetadataContext>.Failure(Error.Forbidden))
            : image <= 0 ? Task.FromResult(Result<DentalImageMetadataContext>.Failure(Error.Validation))
            : repository.ReadAsync(image, actor, role!, ct);
    public Task<Result<DentalImageMetadataContext>> UpdateAsync(int image, int actor, string? role, UpdateDentalImageMetadataRequest request, string? ip, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<DentalImageMetadataContext>.Failure(Error.Forbidden))
            : image <= 0 || !new UpdateDentalImageMetadataValidator().Validate(request).IsValid
                ? Task.FromResult(Result<DentalImageMetadataContext>.Failure(Error.Validation))
                : repository.UpdateAsync(image, actor, role!, request, ip, ct);
}
