using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
namespace Dental.Application.Interfaces;
public interface IPrescriptionHandoverRepository
{
    Task<Result<PrescriptionHandoverStatus>> ReadAsync(int id, int actorId, string role, CancellationToken ct);
    Task<Result<PrescriptionHandoverStatus>> DispenseAsync(int id, int actorId, string role, PrescriptionDispenseRequest request, string? ip, CancellationToken ct);
    Task<Result<PrescriptionHandoverStatus>> UndoAsync(int id, int actorId, string role, PrescriptionUndoDispenseRequest request, string? ip, CancellationToken ct);
}
