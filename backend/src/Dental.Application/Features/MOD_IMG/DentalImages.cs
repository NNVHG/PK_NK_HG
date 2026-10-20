using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using FluentValidation;
namespace Dental.Application.Features.MOD_IMG;

public sealed record DentalImageRequest(int PatientId, int? VisitId, int? ToothNumber, string Category, string? Notes = null);
public sealed record AttachDentalImageRequest(int VisitId, int ExpectedVersion);
public sealed record DentalImageDto(int Id, int PatientId, int? VisitId, int? ToothNumber, string Category,
    string OriginalFileName, string ContentType, long SizeBytes, int Width, int Height, DateTime CreatedAt, int Version, string? Notes = null);
public sealed record ImageVisitTarget(int VisitId, string Status);
public sealed record DentalImagePage(DentalImageDto[] Items, bool HasMore);
public sealed class DentalImageRequestValidator : AbstractValidator<DentalImageRequest>
{
    public static readonly string[] Categories = Enum.GetNames<DentalImageCategory>();
    public static bool ValidTooth(int? tooth) => tooth is null ||
        tooth / 10 is >= 1 and <= 4 && tooth % 10 is >= 1 and <= 8 ||
        tooth / 10 is >= 5 and <= 8 && tooth % 10 is >= 1 and <= 5;
    public DentalImageRequestValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.VisitId).GreaterThan(0).When(x => x.VisitId.HasValue);
        RuleFor(x => x.ToothNumber).Must(ValidTooth);
        RuleFor(x => x.Category).Must(x => Categories.Contains(x, StringComparer.Ordinal));
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
public interface IDentalImageRepository
{
    Task<Result<DentalImageDto>> UploadAsync(DentalImageRequest request, string name, string type, byte[] bytes, int width, int height, int actor, string role, string? ip, CancellationToken ct);
    Task<Result<DentalImagePage>> ListAsync(int patient, int? visit, int? tooth, int beforeId, int actor, string role, CancellationToken ct);
    Task<Result<ImageVisitTarget[]>> TargetsAsync(int patient, int actor, string role, CancellationToken ct);
    Task<Result<DentalImageDto>> AttachAsync(int id, AttachDentalImageRequest request, int actor, string role, string? ip, CancellationToken ct);
}
public interface IDentalImageStorage
{
    Task WriteAsync(string relativePath, byte[] bytes, CancellationToken ct);
    Task RemoveAsync(string relativePath);
}
public sealed class DentalImageService(IDentalImageRepository repository)
{
    public Task<Result<DentalImageDto>> UploadAsync(DentalImageRequest request, string name, string type, byte[] bytes, int actor, string role, string? ip, CancellationToken ct = default)
    {
        if (actor <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant)) return Task.FromResult(Result<DentalImageDto>.Failure(Error.Forbidden));
        if (!new DentalImageRequestValidator().Validate(request).IsValid || DentalImageFile.Inspect(name, type, bytes) is not { } dimensions)
            return Task.FromResult(Result<DentalImageDto>.Failure(new Error("IMG_001", "Chọn ảnh JPG, PNG hoặc WebP hợp lệ, tối đa 10MB và 4096px mỗi chiều; kiểm tra phân loại và số răng FDI.")));
        var clean = Path.GetFileName(name.Replace('\\', '/'));
        if (clean.Length is 0 or > 180 || clean.Any(char.IsControl)) return Task.FromResult(Result<DentalImageDto>.Failure(Error.Validation));
        return repository.UploadAsync(request, clean, type, bytes, dimensions.Width, dimensions.Height, actor, role, ip, ct);
    }
    public Task<Result<DentalImagePage>> ListAsync(int patient, int? visit, int? tooth, int beforeId, int actor, string role, CancellationToken ct = default) =>
        patient <= 0 || visit is <= 0 || !DentalImageRequestValidator.ValidTooth(tooth) || beforeId < 0
        ? Task.FromResult(Result<DentalImagePage>.Failure(Error.Validation)) : repository.ListAsync(patient, visit, tooth, beforeId, actor, role, ct);
    public Task<Result<ImageVisitTarget[]>> TargetsAsync(int patient, int actor, string role, CancellationToken ct = default) =>
        patient <= 0 ? Task.FromResult(Result<ImageVisitTarget[]>.Failure(Error.Validation)) : repository.TargetsAsync(patient, actor, role, ct);
    public Task<Result<DentalImageDto>> AttachAsync(int id, AttachDentalImageRequest request, int actor, string role, string? ip, CancellationToken ct = default) =>
        id <= 0 || request.VisitId <= 0 || request.ExpectedVersion <= 0 ? Task.FromResult(Result<DentalImageDto>.Failure(Error.Validation)) : repository.AttachAsync(id, request, actor, role, ip, ct);
}
