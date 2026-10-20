using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_RX;

public sealed record MedicineStockReceiptRequest(Guid RequestId, int Quantity, int ExpectedMedicineVersion,
    string SupplierName, string? Notes, bool Confirmed);
public sealed record MedicineStockReceiptDto(int Id, string Code, int MedicineId, string MedicineCode, string MedicineName,
    string Unit, int Quantity, string SupplierName, string? Notes, DateTime ConfirmedAt, string ConfirmedByName);
public sealed record MedicineStockDetail(int MedicineId, string Code, string Name, string Unit, int MedicineVersion,
    bool IsActive, int OnHand, MedicineStockReceiptDto[] Receipts, bool HasMore);
public sealed record MedicineStockReceiptSaved(MedicineStockReceiptDto Receipt, bool Replayed);
public sealed record MedicineAvailability(int MedicineId, string Code, string Name, string Unit, int OnHand, bool IsActive);
public sealed record PrescriptionStockLine(int MedicineId, string Code, string Name, string Unit, int Quantity, int OnHand, bool UnitMatches);
public sealed record PrescriptionStockStatus(bool NeedsDeduction, bool LegacyHandover, bool CanFulfill, PrescriptionStockLine[] Lines);

public interface IMedicineStockRepository
{
    Task<Result<MedicineStockDetail>> ReadAsync(int id, int beforeId, int actor, CancellationToken ct);
    Task<Result<MedicineStockReceiptSaved>> ReceiveAsync(int id, MedicineStockReceiptRequest request, int actor, CancellationToken ct);
    Task<Result<MedicineAvailability[]>> AvailabilityAsync(int visitId, int[] ids, int actor, CancellationToken ct);
}
public sealed class MedicineStockReceiptValidator : AbstractValidator<MedicineStockReceiptRequest>
{
    public MedicineStockReceiptValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ExpectedMedicineVersion).GreaterThan(0); RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(200).Must(s => s is not null && !s.Any(char.IsControl));
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
public sealed class MedicineStockService(IMedicineStockRepository repository)
{
    public Task<Result<MedicineStockDetail>> ReadAsync(int id, int beforeId, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0 || role != RoleCodes.Admin) return Task.FromResult(Result<MedicineStockDetail>.Failure(Error.Forbidden));
        if (id <= 0 || beforeId < 0) return Task.FromResult(Result<MedicineStockDetail>.Failure(Error.Validation));
        return repository.ReadAsync(id, beforeId, actor.Value, ct);
    }
    public Task<Result<MedicineStockReceiptSaved>> ReceiveAsync(int id, MedicineStockReceiptRequest request, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0 || role != RoleCodes.Admin) return Task.FromResult(Result<MedicineStockReceiptSaved>.Failure(Error.Forbidden));
        request = request with { SupplierName = request.SupplierName?.Trim() ?? "", Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim() };
        if (id <= 0 || !new MedicineStockReceiptValidator().Validate(request).IsValid) return Task.FromResult(Result<MedicineStockReceiptSaved>.Failure(Error.Validation));
        return repository.ReceiveAsync(id, request, actor.Value, ct);
    }
    public Task<Result<MedicineAvailability[]>> AvailabilityAsync(int visitId, int[] ids, int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0 || role != RoleCodes.Dentist) return Task.FromResult(Result<MedicineAvailability[]>.Failure(Error.Forbidden));
        if (visitId <= 0 || ids is not { Length: > 0 and <= 10 } || ids.Any(i => i <= 0) || ids.Distinct().Count() != ids.Length)
            return Task.FromResult(Result<MedicineAvailability[]>.Failure(Error.Validation));
        return repository.AvailabilityAsync(visitId, ids, actor.Value, ct);
    }
}
