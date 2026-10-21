using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using FluentValidation;
namespace Dental.Application.Features.MOD_IMG;

public sealed record ImageClinicalContext(int ImageId, int PatientId, int? VisitId, int Version, string? ClinicalNotes,
    string? DoctorDiagnosis, int? UpdatedByUserId, string? UpdatedByName, DateTime? UpdatedAt, bool CanReplace, bool CanAppend);
public sealed record ImageClinicalWriteRequest(int ExpectedVersion, string? ClinicalNotes, string? DoctorDiagnosis, bool Confirmed);
public sealed record ImageClinicalAppendRequest(int ExpectedVersion, string Note, Guid RequestId, bool Confirmed);
public sealed record ImageClinicalAddendumDto(int Id, int ImageId, int ImageVersion, int CreatedByUserId, string AuthorName, string AuthorRole, DateTime CreatedAt, string Note);
public sealed record ImageClinicalState(DentalImage Image, Visit? Visit, bool HasPayments);
public static class ImageClinicalRules
{
    public static bool MayReplace(ImageClinicalState state) => !state.Image.IsDeleted && state.Image.Version < int.MaxValue &&
        (state.Image.VisitId is null || state.Visit is { IsLocked: false } visit && VisitStatuses.IsEditable(visit.Status) && !state.HasPayments);
    public static bool MayAppend(ImageClinicalState state) => !state.Image.IsDeleted && state.Image.Version < int.MaxValue && state.Visit?.Status == VisitStatuses.Completed;
    public static bool Owns(ImageClinicalState state, int actor, string role) => role == RoleCodes.Admin || role == RoleCodes.Dentist && state.Visit?.DentistId == actor;
}
public sealed class ImageClinicalWriteValidator : AbstractValidator<ImageClinicalWriteRequest>
{
    public ImageClinicalWriteValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.ClinicalNotes).MaximumLength(1000).Must(x => x is null || !x.Contains('\0'));
        RuleFor(x => x.DoctorDiagnosis).MaximumLength(255).Must(x => x is null || !x.Contains('\0'));
    }
}
public sealed class ImageClinicalAppendValidator : AbstractValidator<ImageClinicalAppendRequest>
{
    public ImageClinicalAppendValidator()
    {
        RuleFor(x => x.ExpectedVersion).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(1000).Must(x => x is null || !x.Contains('\0'));
    }
}
public sealed class DentalImageClinicalService(IDentalImageClinicalRepository repository)
{
    private static bool ReadRole(int actor, string? role) => actor > 0 && role is RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Patient;
    private static bool WriteRole(int actor, string? role) => actor > 0 && role is RoleCodes.Admin or RoleCodes.Dentist;
    public Task<Result<ImageClinicalContext>> ReadAsync(int image, int actor, string? role, CancellationToken ct = default)
        => !ReadRole(actor, role) ? Task.FromResult(Result<ImageClinicalContext>.Failure(Error.Forbidden)) : image <= 0 ? Task.FromResult(Result<ImageClinicalContext>.Failure(Error.Validation)) : repository.ReadAsync(image, actor, role!, ct);
    public Task<Result<PagedResult<ImageClinicalAddendumDto>>> HistoryAsync(int image, int actor, string? role, int page, int pageSize, CancellationToken ct = default)
        => !ReadRole(actor, role) ? Task.FromResult(Result<PagedResult<ImageClinicalAddendumDto>>.Failure(Error.Forbidden)) : image <= 0 || page is < 1 or > 1000000 || pageSize is < 1 or > 50 ? Task.FromResult(Result<PagedResult<ImageClinicalAddendumDto>>.Failure(Error.Validation)) : repository.HistoryAsync(image, actor, role!, page, pageSize, ct);
    public Task<Result<ImageClinicalContext>> ReplaceAsync(int image, int actor, string? role, ImageClinicalWriteRequest request, string? ip, CancellationToken ct = default)
        => !WriteRole(actor, role) ? Task.FromResult(Result<ImageClinicalContext>.Failure(Error.Forbidden)) : image <= 0 || !new ImageClinicalWriteValidator().Validate(request).IsValid ? Task.FromResult(Result<ImageClinicalContext>.Failure(Error.Validation)) : repository.ReplaceAsync(image, actor, role!, request, ip, ct);
    public Task<Result<ImageClinicalAddendumDto>> AppendAsync(int image, int actor, string? role, ImageClinicalAppendRequest request, string? ip, CancellationToken ct = default)
        => !WriteRole(actor, role) ? Task.FromResult(Result<ImageClinicalAddendumDto>.Failure(Error.Forbidden)) : image <= 0 || !new ImageClinicalAppendValidator().Validate(request).IsValid ? Task.FromResult(Result<ImageClinicalAddendumDto>.Failure(Error.Validation)) : repository.AppendAsync(image, actor, role!, request, ip, ct);
}
