using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IInvoiceDiscountRepository
{
    Task<IReadOnlyList<DiscountPreset>> GetPresetsAsync(bool includeInactive, CancellationToken ct = default);
    Task<Result<DiscountPreset>> SavePresetAsync(int? id, DiscountPresetRequest request, int actorId, string? ip, CancellationToken ct = default);
    Task<Result<InvoiceDiscountResponse>> ApplyAsync(int invoiceId, ApplyInvoiceDiscountRequest request, int actorId, string role, string? ip, CancellationToken ct = default);
}
