using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_INV;

public sealed record StockReceiptLineRequest(int MedicalSupplyId, int Quantity, int ExpectedSupplyVersion);
public sealed record StockReceiptRequest(Guid RequestId, bool Confirmed, string SupplierName, string? Notes, List<StockReceiptLineRequest> Lines);
public sealed record StockItemDto(int MedicalSupplyId, string Code, string Name, string Unit, int OnHand, int MinimumStockThreshold, bool IsActive, int SupplyVersion);
public sealed record StockReceiptLineDto(int MedicalSupplyId, string Code, string Name, string Unit, int Quantity);
public sealed record StockReceiptDto(int Id, string Code, string SupplierName, string? Notes, DateTime ConfirmedAt, int ConfirmedByUserId, string ConfirmedByName, StockReceiptLineDto[] Lines);
public sealed record StockReceiptPage(StockReceiptDto[] Items, bool HasMore);
public sealed record StockReceiptSaved(StockReceiptDto Receipt, bool Replayed);

public interface IStockReceiptRepository
{
    Task<Result<PagedResult<StockItemDto>>> StockAsync(string keyword, int page, int actor, string role, CancellationToken ct);
    Task<Result<StockReceiptPage>> ListAsync(int beforeId, int actor, string role, CancellationToken ct);
    Task<Result<StockReceiptDto>> GetAsync(int id, int actor, string role, CancellationToken ct);
    Task<Result<StockReceiptSaved>> ConfirmAsync(StockReceiptRequest request, int actor, string role, CancellationToken ct);
}

public sealed class StockReceiptValidator : AbstractValidator<StockReceiptRequest>
{
    public StockReceiptValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("Thiếu mã gửi phiếu.");
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận phiếu trước khi cộng tồn.");
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(200).Must(x => x is not null && !x.Any(char.IsControl)).WithMessage("Nhà cung cấp không hợp lệ (tối đa 200 ký tự).");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Count: > 0 and <= 50 }).WithMessage("Phiếu cần từ 1 đến 50 dòng.");
        When(x => x.Lines is not null, () =>
        {
            RuleFor(x => x.Lines).Must(x => x.All(l => l is not null) && x.Select(l => l.MedicalSupplyId).Distinct().Count() == x.Count).WithMessage("Mỗi vật tư chỉ có một dòng.");
            RuleForEach(x => x.Lines).NotNull().ChildRules(line =>
            {
                line.RuleFor(x => x.MedicalSupplyId).GreaterThan(0);
                line.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng phải là số nguyên dương theo đơn vị danh mục.");
                line.RuleFor(x => x.ExpectedSupplyVersion).GreaterThan(0);
            });
        });
    }
}

public sealed class StockReceiptService(IStockReceiptRepository repository)
{
    public static bool CanWrite(string? role) => role is RoleCodes.Admin or RoleCodes.Assistant;
    public static bool CanRead(string? role) => CanWrite(role) || role == RoleCodes.Dentist;
    private static Error Invalid(string message) => new("INV_001", message);
    public Task<Result<PagedResult<StockItemDto>>> StockAsync(string? keyword, int page, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0) return Task.FromResult(Result<PagedResult<StockItemDto>>.Failure(Error.Unauthorized));
        if (!CanRead(role)) return Task.FromResult(Result<PagedResult<StockItemDto>>.Failure(Error.Forbidden));
        if (page is < 1 or > 100000 || keyword?.Length > 200) return Task.FromResult(Result<PagedResult<StockItemDto>>.Failure(Invalid("Bộ lọc kho không hợp lệ.")));
        return repository.StockAsync(keyword?.Trim() ?? "", page, actor.Value, role!, ct);
    }
    public Task<Result<StockReceiptPage>> ListAsync(int beforeId, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0) return Task.FromResult(Result<StockReceiptPage>.Failure(Error.Unauthorized));
        if (!CanWrite(role)) return Task.FromResult(Result<StockReceiptPage>.Failure(Error.Forbidden));
        if (beforeId < 0) return Task.FromResult(Result<StockReceiptPage>.Failure(Invalid("Mốc phiếu không hợp lệ.")));
        return repository.ListAsync(beforeId, actor.Value, role!, ct);
    }
    public Task<Result<StockReceiptDto>> GetAsync(int id, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0) return Task.FromResult(Result<StockReceiptDto>.Failure(Error.Unauthorized));
        if (!CanWrite(role)) return Task.FromResult(Result<StockReceiptDto>.Failure(Error.Forbidden));
        if (id <= 0) return Task.FromResult(Result<StockReceiptDto>.Failure(Invalid("Mã phiếu không hợp lệ.")));
        return repository.GetAsync(id, actor.Value, role!, ct);
    }
    public Task<Result<StockReceiptSaved>> ConfirmAsync(StockReceiptRequest req, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0) return Task.FromResult(Result<StockReceiptSaved>.Failure(Error.Unauthorized));
        if (!CanWrite(role)) return Task.FromResult(Result<StockReceiptSaved>.Failure(Error.Forbidden));
        var clean = req with { SupplierName = req.SupplierName?.Trim() ?? "", Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim() };
        var valid = new StockReceiptValidator().Validate(clean);
        if (!valid.IsValid) return Task.FromResult(Result<StockReceiptSaved>.Failure(Invalid(valid.Errors[0].ErrorMessage)));
        return repository.ConfirmAsync(clean, actor.Value, role!, ct);
    }
}
