using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using FluentValidation;

namespace Dental.Application.Features.MOD_IMG;

public sealed record DeleteDentalImageRequest(int ExpectedVersion, string Reason, bool Confirmed);
public sealed record DentalImageDeletionItem(int Id, int PatientId, int? VisitId, int Version,
    string OriginalFileName, string Category, int? ToothNumber, DateTime CreatedAt,
    bool CanDelete, string? UnavailableReason);
public sealed record DentalImageDeletionPage(DentalImageDeletionItem[] Items, bool HasMore);

public static class DentalImageDeletionRules
{
    public static bool RoleAllowed(string? role) => role is RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant;
    public static bool Owns(DentalImage image, int actor, string role) => RoleAllowed(role) &&
        (role == RoleCodes.Admin || image.UploadedByUserId == actor);
    public static bool IsOpen(DentalImage image, Visit? visit, bool hasPayments) =>
        !image.IsDeleted && image.Version is >= 1 and < int.MaxValue &&
        (image.VisitId is null || visit is { Status: VisitStatuses.InProgress, IsLocked: false } && !hasPayments);
}

public sealed class DeleteDentalImageValidator : AbstractValidator<DeleteDentalImageRequest>
{
    public DeleteDentalImageValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Reason).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x))
            .MaximumLength(500).Must(x => x is null || !x.Contains('\0'));
    }
}

public interface IDentalImageDeletionRepository
{
    Task<Result<DentalImageDeletionPage>> ListAsync(int patient, int beforeId, int actor, string role, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(int image, int actor, string role, DeleteDentalImageRequest request, string? ip, CancellationToken ct);
}

public sealed class DentalImageDeletionService(IDentalImageDeletionRepository repository)
{
    public Task<Result<DentalImageDeletionPage>> ListAsync(int patient, int beforeId, int actor, string? role, CancellationToken ct = default)
        => actor <= 0 || !DentalImageDeletionRules.RoleAllowed(role)
            ? Task.FromResult(Result<DentalImageDeletionPage>.Failure(Error.Forbidden))
            : patient <= 0 || beforeId < 0
                ? Task.FromResult(Result<DentalImageDeletionPage>.Failure(Error.Validation))
                : repository.ListAsync(patient, beforeId, actor, role!, ct);

    public Task<Result<bool>> DeleteAsync(int image, int actor, string? role, DeleteDentalImageRequest request, string? ip, CancellationToken ct = default)
        => actor <= 0 || !DentalImageDeletionRules.RoleAllowed(role)
            ? Task.FromResult(Result<bool>.Failure(Error.Forbidden))
            : image <= 0 || !new DeleteDentalImageValidator().Validate(request).IsValid
                ? Task.FromResult(Result<bool>.Failure(Error.Validation))
                : repository.DeleteAsync(image, actor, role!, request, ip, ct);
}
