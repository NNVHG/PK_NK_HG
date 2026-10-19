using Dental.Application.Common;
using FluentValidation;
namespace Dental.Application.Features.MOD_INV;

public sealed record StockIssueLineRequest(int MedicalSupplyId, int Quantity, int ExpectedSupplyVersion);
public sealed record StockIssueRequest(Guid RequestId, bool Confirmed, int VisitId, string? Notes, List<StockIssueLineRequest> Lines);
public sealed record StockIssueTarget(int VisitId, int PatientId, string PatientCode, string PatientName, DateTime? EndedAt);
public sealed record StockIssueTargets(StockIssueTarget[] Items, bool HasMore);
public sealed record StockIssueLineDto(int MedicalSupplyId, string Code, string Name, string Unit, int Quantity);
public sealed record StockIssueDto(int Id, string Code, int VisitId, string? Notes, DateTime ConfirmedAt, int ConfirmedByUserId, string ConfirmedByName, StockIssueLineDto[] Lines);
public sealed record StockIssuePage(StockIssueDto[] Items, bool HasMore);
public sealed record StockIssueSaved(StockIssueDto Issue, bool Replayed);
public interface IStockIssueRepository
{
    Task<Result<StockIssueTargets>> TargetsAsync(int? visitId, int beforeId, int actor, string role, CancellationToken ct);
    Task<Result<StockIssuePage>> ListAsync(int visitId, int beforeId, int actor, string role, CancellationToken ct);
    Task<Result<StockIssueSaved>> ConfirmAsync(StockIssueRequest request, int actor, string role, CancellationToken ct);
}
public sealed class StockIssueValidator : AbstractValidator<StockIssueRequest>
{
    public StockIssueValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("Thiếu mã gửi phiếu.");
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận số lượng thực tế trước khi trừ tồn.");
        RuleFor(x => x.VisitId).GreaterThan(0).WithMessage("Cần chọn lần khám đã hoàn tất.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Count: > 0 and <= 50 }).WithMessage("Phiếu cần từ 1 đến 50 dòng.");
        When(x => x.Lines is not null, () =>
        {
            RuleFor(x => x.Lines).Must(x => x.All(l => l is not null) && x.Select(l => l.MedicalSupplyId).Distinct().Count() == x.Count).WithMessage("Mỗi vật tư chỉ có một dòng.");
            RuleForEach(x => x.Lines).NotNull().ChildRules(line =>
            {
                line.RuleFor(x => x.MedicalSupplyId).GreaterThan(0);
                line.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng thực tế phải là số nguyên dương theo đơn vị danh mục.");
                line.RuleFor(x => x.ExpectedSupplyVersion).GreaterThan(0);
            });
        });
    }
}
public sealed class StockIssueService(IStockIssueRepository repository)
{
    private static Error? Access(int? actor, string? role) => actor is not > 0 ? Error.Unauthorized : !StockReceiptService.CanWrite(role) ? Error.Forbidden : null;
    private static Error Invalid(string message) => new("INV_101", message);
    public Task<Result<StockIssueTargets>> TargetsAsync(int? visit, int before, int? actor, string? role, CancellationToken ct = default)
    {
        var access = Access(actor, role);
        if (access is not null) return Task.FromResult(Result<StockIssueTargets>.Failure(access));
        if (visit is <= 0 || before < 0) return Task.FromResult(Result<StockIssueTargets>.Failure(Invalid("Bộ lọc ca không hợp lệ.")));
        return repository.TargetsAsync(visit, before, actor!.Value, role!, ct);
    }
    public Task<Result<StockIssuePage>> ListAsync(int visit, int before, int? actor, string? role, CancellationToken ct = default)
    {
        var access = Access(actor, role);
        if (access is not null) return Task.FromResult(Result<StockIssuePage>.Failure(access));
        if (visit <= 0 || before < 0) return Task.FromResult(Result<StockIssuePage>.Failure(Invalid("Mã ca hoặc mốc phiếu không hợp lệ.")));
        return repository.ListAsync(visit, before, actor!.Value, role!, ct);
    }
    public Task<Result<StockIssueSaved>> ConfirmAsync(StockIssueRequest req, int? actor, string? role, CancellationToken ct = default)
    {
        var access = Access(actor, role);
        if (access is not null) return Task.FromResult(Result<StockIssueSaved>.Failure(access));
        var clean = req with { Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim() };
        var valid = new StockIssueValidator().Validate(clean);
        if (!valid.IsValid) return Task.FromResult(Result<StockIssueSaved>.Failure(Invalid(valid.Errors[0].ErrorMessage)));
        return repository.ConfirmAsync(clean, actor!.Value, role!, ct);
    }
}
