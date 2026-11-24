using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
namespace Dental.Application.Features.MOD_MST.Services;

// MOD_MST / DL163,214,261,263,361,362,367. Caller owns the serializable transaction, save and required audit.
public sealed class VisitChairService(IStaffWorkspaceRepository workspaces, IDentalChairRepository chairs,
    IVisitRepository visits, IUserRepository users, VietnamClock clock) : IVisitChairCoordinator
{
    public static readonly Error Unavailable = new("MST065", "Chưa có phòng điều trị sẵn sàng cho nha sĩ. Kiểm tra phòng làm việc trước khi gọi hoặc bắt đầu khám.");
    public static readonly Error Inconsistent = new("MST066", "Vị trí hoặc trạng thái ghế đã thay đổi. Tải lại và đối chiếu trước khi tiếp tục.");
    public Task<Result> CheckReadyAsync(Visit visit, int dentistId, CancellationToken ct = default)
        => AllocateCoreAsync(visit, dentistId, 0, false, ct);
    public Task<Result> AcquireAsync(Visit visit, int dentistId, int actorId, CancellationToken ct = default)
        => AllocateCoreAsync(visit, dentistId, actorId, true, ct);

    public async Task<Result> TransferAsync(Visit visit, int dentistId, int actorId, CancellationToken ct = default)
    {
        if (visit.Status != VisitStatuses.InProgress || visit.RoomId is not int sourceRoomId ||
            dentistId <= 0 || actorId <= 0)
            return Result.Failure(Inconsistent);

        var dentist = await users.FindByIdAsync(dentistId, ct);
        if (dentist is not { IsActive: true } || dentist.Role?.RoleCode != RoleCodes.Dentist)
            return Result.Failure(Unavailable);
        if (await visits.HasActiveVisitForDentistAsync(dentistId, ct, visit.VisitId))
            return Result.Failure(Error.DentistHasActiveVisit);

        var workspace = await workspaces.FindAsync(dentistId, ct);
        var targetRoom = workspace is { RoomId: int targetRoomId } && workspace.WorkDate == clock.Today
            ? await chairs.FindAsync(targetRoomId, ct)
            : null;
        if (targetRoom is not { IsActive: true } ||
            await visits.HasActiveVisitForRoomAsync(targetRoom.Id, visit.VisitId, ct))
            return Result.Failure(Unavailable);

        var sourceRoom = await chairs.FindAsync(sourceRoomId, ct);
        if (sourceRoom is not { IsActive: true, Status: DentalChairStatus.Occupied } ||
            await visits.HasActiveVisitForRoomAsync(sourceRoomId, visit.VisitId, ct))
            return Result.Failure(Inconsistent);

        if (targetRoom.Status != DentalChairStatus.Available)
            return Result.Failure(Unavailable);

        var now = clock.UtcNow;
        sourceRoom.Status = DentalChairStatus.Available;
        sourceRoom.Version++;
        sourceRoom.UpdatedAt = now;
        sourceRoom.UpdatedByUserId = actorId;

        targetRoom.Status = DentalChairStatus.Occupied;
        targetRoom.Version++;
        targetRoom.UpdatedAt = now;
        targetRoom.UpdatedByUserId = actorId;
        visit.RoomId = targetRoom.Id;
        return Result.Success();
    }

    private async Task<Result> AllocateCoreAsync(Visit visit, int dentistId, int actorId, bool allocate, CancellationToken ct)
    {
        if (visit.Status is not (VisitStatuses.Created or VisitStatuses.InProgress)) return Result.Failure(Inconsistent);
        var dentist = await users.FindByIdAsync(dentistId, ct);
        if (dentist is not { IsActive: true } || dentist.Role?.RoleCode != RoleCodes.Dentist) return Result.Failure(Unavailable);
        if (await visits.HasActiveVisitForDentistAsync(dentistId, ct, visit.VisitId)) return Result.Failure(Error.DentistHasActiveVisit);
        var workspace = await workspaces.FindAsync(dentistId, ct);
        var room = workspace is { RoomId: int id } && workspace.WorkDate == clock.Today ? await chairs.FindAsync(id, ct) : null;
        if (room is not { IsActive: true } ||
            await visits.HasActiveVisitForRoomAsync(room.Id, visit.VisitId, ct))
            return Result.Failure(Unavailable);
        if (visit.Status == VisitStatuses.InProgress && visit.RoomId.HasValue)
            return visit.RoomId == room.Id && room.Status == DentalChairStatus.Occupied ? Result.Success() : Result.Failure(Inconsistent);
        if (room.Status != DentalChairStatus.Available) return Result.Failure(Unavailable);
        if (!allocate) return Result.Success();
        visit.RoomId = room.Id;
        room.Status = DentalChairStatus.Occupied; room.Version++; room.UpdatedAt = clock.UtcNow; room.UpdatedByUserId = actorId;
        return Result.Success();
    }
    public async Task<Result> ReleaseAsync(Visit visit, int actorId, CancellationToken ct = default)
    {
        // DL263 corrections never acquire/release the historical room. Null is reserved for pre-allocation visits.
        if (visit.Status == VisitStatuses.Correcting || visit.RoomId is not int roomId) return Result.Success();
        if (visit.Status != VisitStatuses.InProgress) return Result.Failure(Inconsistent);
        var room = await chairs.FindAsync(roomId, ct);
        if (room is not { IsActive: true, Status: DentalChairStatus.Occupied } ||
            await visits.HasActiveVisitForRoomAsync(roomId, visit.VisitId, ct)) return Result.Failure(Inconsistent);
        room.Status = DentalChairStatus.Available; room.Version++; room.UpdatedAt = clock.UtcNow; room.UpdatedByUserId = actorId;
        return Result.Success();
    }
}
