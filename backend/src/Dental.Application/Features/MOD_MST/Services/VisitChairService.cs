using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
namespace Dental.Application.Features.MOD_MST.Services;

// MOD_MST / DL163,214,261,263. Caller owns the serializable transaction, save and required audit.
public sealed class VisitChairService(IStaffWorkspaceRepository workspaces, IDentalChairRepository chairs,
    IVisitRepository visits, IUserRepository users, VietnamClock clock) : IVisitChairCoordinator
{
    public static readonly Error Unavailable = new("MST065", "Chưa có ghế/phòng sẵn sàng cho nha sĩ. Kiểm tra vị trí làm việc trước khi gọi hoặc bắt đầu khám.");
    public static readonly Error Inconsistent = new("MST066", "Vị trí hoặc trạng thái ghế đã thay đổi. Tải lại và đối chiếu trước khi tiếp tục.");
    public Task<Result> CheckReadyAsync(Visit visit, int dentistId, CancellationToken ct = default)
        => AllocateCoreAsync(visit, dentistId, 0, false, ct);
    public Task<Result> AcquireAsync(Visit visit, int dentistId, int actorId, CancellationToken ct = default)
        => AllocateCoreAsync(visit, dentistId, actorId, true, ct);
    private async Task<Result> AllocateCoreAsync(Visit visit, int dentistId, int actorId, bool allocate, CancellationToken ct)
    {
        if (visit.Status is not (VisitStatuses.Created or VisitStatuses.InProgress)) return Result.Failure(Inconsistent);
        var dentist = await users.FindByIdAsync(dentistId, ct);
        if (dentist is not { IsActive: true } || dentist.Role?.RoleCode != RoleCodes.Dentist) return Result.Failure(Unavailable);
        if (await visits.HasActiveVisitForDentistAsync(dentistId, ct, visit.VisitId)) return Result.Failure(Error.DentistHasActiveVisit);
        var workspace = await workspaces.FindAsync(dentistId, ct);
        var chair = workspace?.ChairId is int id ? await chairs.FindAsync(id, ct) : null;
        if (chair is not { IsActive: true } || await visits.HasActiveVisitForChairAsync(chair.Id, visit.VisitId, ct))
            return Result.Failure(Unavailable);
        if (visit.Status == VisitStatuses.InProgress && visit.ChairId.HasValue)
            return visit.ChairId == chair.Id && chair.Status == DentalChairStatus.Occupied ? Result.Success() : Result.Failure(Inconsistent);
        if (chair.Status != DentalChairStatus.Available) return Result.Failure(Unavailable);
        if (!allocate) return Result.Success();
        visit.ChairId = chair.Id;
        chair.Status = DentalChairStatus.Occupied; chair.Version++; chair.UpdatedAt = clock.UtcNow; chair.UpdatedByUserId = actorId;
        return Result.Success();
    }
    public async Task<Result> ReleaseAsync(Visit visit, int actorId, CancellationToken ct = default)
    {
        // DL263 corrections never acquire/release the historical chair. Null is reserved for pre-allocation visits.
        if (visit.Status == VisitStatuses.Correcting || visit.ChairId is not int chairId) return Result.Success();
        if (visit.Status != VisitStatuses.InProgress) return Result.Failure(Inconsistent);
        var chair = await chairs.FindAsync(chairId, ct);
        if (chair is not { IsActive: true, Status: DentalChairStatus.Occupied } ||
            await visits.HasActiveVisitForChairAsync(chairId, visit.VisitId, ct)) return Result.Failure(Inconsistent);
        chair.Status = DentalChairStatus.Available; chair.Version++; chair.UpdatedAt = clock.UtcNow; chair.UpdatedByUserId = actorId;
        return Result.Success();
    }
}
