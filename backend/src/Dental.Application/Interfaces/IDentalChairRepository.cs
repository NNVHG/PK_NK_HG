using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IDentalChairRepository
{
    Task<IReadOnlyList<ClinicRoom>> GetAllAsync(CancellationToken ct = default);
    Task<bool> IsAssignedAsync(int roomId, CancellationToken ct = default);
    Task<bool> HasActiveVisitAsync(int roomId, CancellationToken ct = default);
    Task<ClinicRoom?> FindAsync(int id, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(string normalizedNumber, CancellationToken ct = default);
    Task<bool> RoomNameExistsAsync(string normalizedRoomName, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(ClinicRoom room, CancellationToken ct = default);
    Task<PagedResult<ClinicRoom>> GetPageAsync(DentalChairQuery query, CancellationToken ct = default);
}
