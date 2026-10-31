using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_MST.Services;

public sealed class StaffWorkspaceService(IStaffWorkspaceRepository workspaces, IDentalChairRepository chairs,
    IUserRepository users, IVisitRepository visits, IQueueRepository queues, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock)
{
    public static readonly Error Changed = new("MST_061", "Vị trí đã thay đổi hoặc đang có thao tác đồng thời. Tải lại rồi xác nhận lại.");
    public static readonly Error Busy = new("MST_062", "Nha sĩ đang có ca khám hoặc lời mời chưa xử lý; chưa thể đổi vị trí.");
    public static readonly Error Unavailable = new("MST_063", "Ghế/phòng chưa sẵn sàng hoặc đã có nha sĩ khác chọn.");
    public static readonly Error InvalidTarget = new("MST_064", "Nhân sự không hoạt động hoặc không đúng vai trò để chọn vị trí này.");

    private bool IsConfirmedToday(StaffWorkspace? workspace)
        => workspace is { ChairId: not null } && workspace.WorkDate == clock.Today;

    private async Task<bool> ActorAllowed(int actorId, string? role, CancellationToken ct)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant)) return false;
        var actor = await users.FindByIdAsync(actorId, ct);
        return actor is { IsActive: true } && actor.Role?.RoleCode == role;
    }
    private async Task<bool> IsBusy(int dentistId, CancellationToken ct)
        => await visits.HasActiveVisitForDentistAsync(dentistId, ct) || await queues.HasCallingForDentistAsync(clock.Today, dentistId, ct);

    public async Task<Result<WorkspaceSnapshot>> GetAsync(int actorId, string? role, int targetId, CancellationToken ct = default)
    {
        if (!await ActorAllowed(actorId, role, ct) || (role != RoleCodes.Admin && actorId != targetId)) return Result<WorkspaceSnapshot>.Failure(Error.Forbidden);
        var target = await users.FindByIdAsync(targetId, ct);
        if (target is null) return Result<WorkspaceSnapshot>.Failure(Error.NotFound);
        if (target.Role?.RoleCode == RoleCodes.Patient) return Result<WorkspaceSnapshot>.Failure(Error.Forbidden);
        return Result<WorkspaceSnapshot>.Success(await SnapshotAsync(target, ct));
    }
    public async Task<Result<PagedResult<WorkspaceTarget>>> GetTargetsAsync(int actorId, string? role, WorkspaceTargetQuery request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin || !await ActorAllowed(actorId, role, ct)) return Result<PagedResult<WorkspaceTarget>>.Failure(Error.Forbidden);
        if (!(await new WorkspaceTargetQueryValidator().ValidateAsync(request, ct)).IsValid) return Result<PagedResult<WorkspaceTarget>>.Failure(Error.Validation);
        var (items, total) = await users.GetStaffPageAsync(null, null, request.Keyword?.Trim(), request.Page, 20, ct);
        return Result<PagedResult<WorkspaceTarget>>.Success(PagedResult<WorkspaceTarget>.Create(
            items.Select(x => new WorkspaceTarget(x.UserId, x.FullName, x.Role.RoleCode, x.IsActive)).ToArray(), total, request.Page, 20));
    }
    public async Task<Result<WorkspaceOptions>> GetOptionsAsync(int actorId, string? role, CancellationToken ct = default)
    {
        if (!await ActorAllowed(actorId, role, ct)) return Result<WorkspaceOptions>.Failure(Error.Forbidden);
        var today = clock.Today;
        var allChairs = await chairs.GetAllAsync(ct); var claims = await workspaces.GetAllAsync(ct);
        var dentists = await users.GetActiveDentistsAsync(ct);
        var options = new List<WorkspaceDentistOption>();
        foreach (var dentist in dentists)
        {
            var workspace = claims.FirstOrDefault(x => x.UserId == dentist.UserId);
            var confirmedToday = workspace is { ChairId: not null } && workspace.WorkDate == today;
            var chair = confirmedToday ? allChairs.FirstOrDefault(x => x.Id == workspace!.ChairId) : null;
            var assistantId = claims.FirstOrDefault(x => x.DentistId == dentist.UserId)?.UserId;
            var assistantCount = claims.Count(x => x.DentistId == dentist.UserId);
            options.Add(new(dentist.UserId, dentist.FullName, chair?.Id, chair?.ChairNumber, chair?.RoomName, assistantId, true, assistantCount));
        }
        return Result<WorkspaceOptions>.Success(new(allChairs.Select(c => new WorkspaceChairOption(c.Id, c.ChairNumber, c.RoomName,
            (int)c.Status, c.IsActive, claims.FirstOrDefault(w => w.ChairId == c.Id && w.WorkDate == today)?.UserId)).ToArray(), options));
    }

    public async Task<Result<WorkspaceSnapshot>> ChangeAsync(int actorId, string? role, int targetId,
        ChangeWorkspaceRequest request, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant) || (role != RoleCodes.Admin && actorId != targetId))
            return Result<WorkspaceSnapshot>.Failure(Error.Forbidden);
        if (targetId <= 0 || !(await new ChangeWorkspaceRequestValidator().ValidateAsync(request, ct)).IsValid) return Result<WorkspaceSnapshot>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                // Recheck staff and role within the same snapshot used for the assignment.
                if (!await ActorAllowed(actorId, role, transactionCt)) return Result<WorkspaceSnapshot>.Failure(Error.Forbidden);
                var target = await users.FindByIdAsync(targetId, transactionCt);
                if (target is null) return Result<WorkspaceSnapshot>.Failure(Error.NotFound);
                var targetRole = target.Role?.RoleCode;
                var workspace = await workspaces.FindAsync(targetId, transactionCt);
                if ((workspace?.Version ?? 0) != request.ExpectedVersion) return Result<WorkspaceSnapshot>.Failure(Changed);
                var clearing = request.ChairId is null && request.DentistId is null;
                if (targetRole == RoleCodes.Patient || (!clearing && (!target.IsActive || targetRole is not (RoleCodes.Dentist or RoleCodes.Assistant))))
                    return Result<WorkspaceSnapshot>.Failure(InvalidTarget);
                if (!clearing && ((targetRole == RoleCodes.Dentist && request.DentistId.HasValue) || (targetRole == RoleCodes.Assistant && request.ChairId.HasValue)))
                    return Result<WorkspaceSnapshot>.Failure(Error.Validation);

                var today = clock.Today;
                var sameConfirmedSelection = workspace is not null &&
                    workspace.ChairId == request.ChairId &&
                    workspace.DentistId == request.DentistId &&
                    (!request.ChairId.HasValue || workspace.WorkDate == today);
                if (sameConfirmedSelection)
                    return Result<WorkspaceSnapshot>.Success(await SnapshotAsync(target, transactionCt));

                if (targetRole == RoleCodes.Dentist && await IsBusy(targetId, transactionCt))
                    return Result<WorkspaceSnapshot>.Failure(Busy);

                DentalChair? selectedChair = null;
                if (request.ChairId.HasValue)
                {
                    if (await workspaces.ReleaseStaleChairClaimsAsync(request.ChairId.Value, targetId, today, actorId, clock.UtcNow, transactionCt))
                        await uow.SaveChangesAsync(transactionCt);

                    selectedChair = await chairs.FindAsync(request.ChairId.Value, transactionCt);
                    if (selectedChair is not { IsActive: true, Status: DentalChairStatus.Available } ||
                        await workspaces.ChairClaimedAsync(selectedChair.Id, targetId, transactionCt))
                        return Result<WorkspaceSnapshot>.Failure(Unavailable);
                }
                if (request.DentistId.HasValue)
                {
                    var dentist = await users.FindByIdAsync(request.DentistId.Value, transactionCt);
                    if (dentist is not { IsActive: true } || dentist.Role?.RoleCode != RoleCodes.Dentist) return Result<WorkspaceSnapshot>.Failure(InvalidTarget);
                }
                var oldChair = workspace?.ChairId;
                var oldWorkDate = workspace?.WorkDate;
                var previousDentist = workspace?.DentistId;
                if (workspace is null)
                {
                    workspace = new StaffWorkspace { UserId = targetId, CreatedAt = clock.UtcNow };
                    await workspaces.AddAsync(workspace, transactionCt);
                }
                else workspace.Version++;
                workspace.ChairId = request.ChairId;
                workspace.RoomId = selectedChair?.RoomId;
                workspace.WorkDate = request.ChairId.HasValue ? today : null;
                workspace.DentistId = request.DentistId;
                workspace.UpdatedAt = clock.UtcNow; workspace.UpdatedByUserId = actorId;
                await uow.SaveChangesAsync(transactionCt);
                await audit.LogRequiredAsync("MOD_MST_WORKSPACE_CHANGED", "StaffWorkspace", targetId, actorId,
                    JsonSerializer.Serialize(new { oldChairId = oldChair, chairId = workspace.ChairId, roomId = workspace.RoomId,
                        oldWorkDate, workDate = workspace.WorkDate, oldDentistId = previousDentist,
                        dentistId = workspace.DentistId, version = workspace.Version }), ct: transactionCt);
                return Result<WorkspaceSnapshot>.Success(await SnapshotAsync(target, transactionCt));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<WorkspaceSnapshot>.Failure(Changed); }
    }

    private async Task<WorkspaceSnapshot> SnapshotAsync(User target, CancellationToken ct)
    {
        var workspace = await workspaces.FindAsync(target.UserId, ct);
        var isDentist = target.Role?.RoleCode == RoleCodes.Dentist;
        var dentistId = isDentist ? target.UserId : workspace?.DentistId;
        var dentist = isDentist ? target : dentistId.HasValue ? await users.FindByIdAsync(dentistId.Value, ct) : null;
        var dentistWorkspace = isDentist || workspace?.ChairId.HasValue == true ? workspace : dentistId.HasValue ? await workspaces.FindAsync(dentistId.Value, ct) : null;
        var confirmedToday = IsConfirmedToday(dentistWorkspace);
        var chair = confirmedToday && dentistWorkspace?.ChairId is int chairId ? await chairs.FindAsync(chairId, ct) : null;
        var ownerId = workspace?.ChairId.HasValue == true ? target.UserId : dentistId;
        var busy = ownerId.HasValue && await IsBusy(ownerId.Value, ct);
        return new(target.UserId, target.FullName, target.Role?.RoleCode ?? string.Empty, target.IsActive, workspace?.Version ?? 0,
            dentistId, dentist?.FullName, chair?.Id, chair?.ChairNumber, chair?.RoomName,
            target.IsActive && target.Role?.RoleCode is (RoleCodes.Dentist or RoleCodes.Assistant) &&
            dentist is { IsActive: true } && dentist.Role?.RoleCode == RoleCodes.Dentist &&
            confirmedToday && chair is { IsActive: true, Status: DentalChairStatus.Available }, !isDentist || !busy);
    }
}
