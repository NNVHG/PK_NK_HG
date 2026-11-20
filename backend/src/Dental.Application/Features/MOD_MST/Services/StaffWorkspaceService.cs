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
    public static readonly Error Unavailable = new("MST_063", "Phòng điều trị chưa sẵn sàng hoặc đã được phân công cho nha sĩ khác.");
    public static readonly Error InvalidTarget = new("MST_064", "Nhân sự không hoạt động hoặc không đúng vai trò để chọn vị trí này.");
    public static readonly Error DentistNotWorking = new("MST_067", "Nha sĩ chưa được phân công phòng điều trị sẵn sàng hôm nay.");

    private bool IsConfirmedToday(StaffWorkspace? workspace)
        => workspace is { ChairId: not null } && workspace.WorkDate == clock.Today;

    private static bool IsRoomOperational(DentalChair? chair)
        => chair is { IsActive: true, Status: DentalChairStatus.Available or DentalChairStatus.Occupied };

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
        var snapshot = await SnapshotAsync(target, ct);
        return Result<WorkspaceSnapshot>.Success(role == RoleCodes.Admin ? snapshot : snapshot with { CanChange = false });
    }
    public async Task<Result<PagedResult<WorkspaceTarget>>> GetTargetsAsync(int actorId, string? role, WorkspaceTargetQuery request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin || !await ActorAllowed(actorId, role, ct)) return Result<PagedResult<WorkspaceTarget>>.Failure(Error.Forbidden);
        if (!(await new WorkspaceTargetQueryValidator().ValidateAsync(request, ct)).IsValid) return Result<PagedResult<WorkspaceTarget>>.Failure(Error.Validation);
        var (items, total) = await users.GetStaffPageAsync(null, null, request.Keyword?.Trim(), request.Page, 20, ct);
        return Result<PagedResult<WorkspaceTarget>>.Success(PagedResult<WorkspaceTarget>.Create(
            items.Select(x => new WorkspaceTarget(x.UserId, x.FullName, x.Role.RoleCode, x.IsActive)).ToArray(), total, request.Page, 20));
    }
    public async Task<Result<WorkspaceOptions>> GetOptionsAsync(int actorId, string? role, CancellationToken ct = default, int? targetId = null)
    {
        if (role != RoleCodes.Admin || !await ActorAllowed(actorId, role, ct)) return Result<WorkspaceOptions>.Failure(Error.Forbidden);
        var today = clock.Today;
        var allChairs = await chairs.GetAllAsync(ct);
        var dailyAssignments = (await workspaces.GetAllAsync(ct)).Where(x => x.WorkDate == today).ToArray();
        var roomByChair = allChairs.ToDictionary(x => x.Id);
        var dentistAssignments = dailyAssignments.Where(x => x.ChairId.HasValue).ToDictionary(x => x.UserId);
        var assistantsByDentist = dailyAssignments.ToLookup(x => x.DentistId);
        var dentists = await users.GetActiveDentistsAsync(ct);
        var options = new List<WorkspaceDentistOption>();
        foreach (var dentist in dentists)
        {
            var workspace = dentistAssignments.GetValueOrDefault(dentist.UserId);
            if (!IsConfirmedToday(workspace) || workspace?.ChairId is not int chairId ||
                !roomByChair.TryGetValue(chairId, out var chair) || !IsRoomOperational(chair))
                continue;

            var assistants = assistantsByDentist[dentist.UserId];
            options.Add(new(dentist.UserId, dentist.FullName, chair.Id, chair.ChairNumber, chair.RoomName,
                assistants.FirstOrDefault()?.UserId, true, assistants.Count()));
        }
        var currentVisitChairId = targetId is int id
            ? await visits.GetActiveChairIdForDentistAsync(id, ct)
            : null;
        var chairOptions = allChairs.Select(chair => new WorkspaceChairOption(chair.Id, chair.ChairNumber,
            chair.RoomName, (int)chair.Status, chair.IsActive,
            dentistAssignments.Values.FirstOrDefault(w => w.ChairId == chair.Id ||
                (chair.RoomId.HasValue && w.RoomId == chair.RoomId))?.UserId)
        {
            IsCurrentVisitRoom = currentVisitChairId == chair.Id && chair.Status == DentalChairStatus.Occupied
        }).ToArray();
        return Result<WorkspaceOptions>.Success(new(chairOptions, options));
    }

    public async Task<Result<WorkspaceSnapshot>> ChangeAsync(int actorId, string? role, int targetId,
        ChangeWorkspaceRequest request, CancellationToken ct = default)
    {
        if (actorId <= 0 || role != RoleCodes.Admin)
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
                if (request.DentistId is int dentistId)
                {
                    var dentist = await users.FindByIdAsync(dentistId, transactionCt);
                    if (dentist is not { IsActive: true } || dentist.Role?.RoleCode != RoleCodes.Dentist)
                        return Result<WorkspaceSnapshot>.Failure(InvalidTarget);
                    var dentistWorkspace = await workspaces.FindAsync(dentistId, transactionCt);
                    if (!IsConfirmedToday(dentistWorkspace) || dentistWorkspace?.ChairId is not int dentistChairId ||
                        !IsRoomOperational(await chairs.FindAsync(dentistChairId, transactionCt)))
                        return Result<WorkspaceSnapshot>.Failure(DentistNotWorking);
                }
                DateOnly? workDate = clearing ? null : today;
                var sameSelection = workspace is not null &&
                    workspace.ChairId == request.ChairId &&
                    workspace.DentistId == request.DentistId &&
                    workspace.WorkDate == workDate;
                if (sameSelection)
                    return Result<WorkspaceSnapshot>.Success(await SnapshotAsync(target, transactionCt));

                if (targetRole == RoleCodes.Dentist &&
                    IsConfirmedToday(workspace) && await IsBusy(targetId, transactionCt))
                    return Result<WorkspaceSnapshot>.Failure(Busy);

                DentalChair? selectedChair = null;
                if (request.ChairId is int selectedChairId)
                {
                    selectedChair = await chairs.FindAsync(selectedChairId, transactionCt);
                    var visitChairId = await visits.GetActiveChairIdForDentistAsync(targetId, transactionCt);
                    if (visitChairId.HasValue && visitChairId != selectedChairId)
                        return Result<WorkspaceSnapshot>.Failure(Busy);
                    var reclaimingActiveRoom = visitChairId == selectedChairId;
                    if (selectedChair is not { IsActive: true } ||
                        selectedChair.Status != (reclaimingActiveRoom ? DentalChairStatus.Occupied : DentalChairStatus.Available) ||
                        (!reclaimingActiveRoom && await IsServingVisitAsync(selectedChair, transactionCt)))
                        return Result<WorkspaceSnapshot>.Failure(Unavailable);

                    if (await workspaces.ReleaseStaleChairClaimsAsync(selectedChairId, targetId, today, actorId, clock.UtcNow, transactionCt))
                        await uow.SaveChangesAsync(transactionCt);

                    if (await workspaces.ChairClaimedAsync(selectedChairId, targetId, transactionCt))
                        return Result<WorkspaceSnapshot>.Failure(Unavailable);
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
                workspace.WorkDate = workDate;
                workspace.DentistId = request.DentistId;
                workspace.UpdatedAt = clock.UtcNow; workspace.UpdatedByUserId = actorId;
                var releasedAssistants = oldChair.HasValue && !request.ChairId.HasValue
                    ? await ReleaseAssistantLinksAsync(targetId, actorId, transactionCt)
                    : 0;
                await uow.SaveChangesAsync(transactionCt);
                await audit.LogRequiredAsync("MOD_MST_WORKSPACE_CHANGED", "StaffWorkspace", targetId, actorId,
                    JsonSerializer.Serialize(new { oldChairId = oldChair, chairId = workspace.ChairId, roomId = workspace.RoomId,
                        oldWorkDate, workDate = workspace.WorkDate, oldDentistId = previousDentist,
                        dentistId = workspace.DentistId, releasedAssistants, version = workspace.Version }), ct: transactionCt);
                return Result<WorkspaceSnapshot>.Success(await SnapshotAsync(target, transactionCt));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<WorkspaceSnapshot>.Failure(Changed); }
    }

    private async Task<bool> IsServingVisitAsync(DentalChair chair, CancellationToken ct)
        => await visits.HasActiveVisitForChairAsync(chair.Id, 0, ct) ||
            (chair.RoomId is int roomId && await visits.HasActiveVisitForRoomAsync(roomId, 0, ct));

    private async Task<int> ReleaseAssistantLinksAsync(int dentistId, int actorId, CancellationToken ct)
    {
        var assistants = await workspaces.GetAssistantLinksForUpdateAsync(dentistId, ct);
        foreach (var assistant in assistants)
        {
            assistant.DentistId = null;
            assistant.WorkDate = null;
            assistant.Version++;
            assistant.UpdatedAt = clock.UtcNow;
            assistant.UpdatedByUserId = actorId;
        }
        return assistants.Count;
    }

    private async Task<WorkspaceSnapshot> SnapshotAsync(User target, CancellationToken ct)
    {
        var workspace = await workspaces.FindAsync(target.UserId, ct);
        var role = target.Role?.RoleCode;
        var isDentist = role == RoleCodes.Dentist;
        var assistantConfirmed = role == RoleCodes.Assistant && workspace?.WorkDate == clock.Today;
        var dentistId = isDentist ? target.UserId : assistantConfirmed ? workspace?.DentistId : null;
        var dentist = isDentist ? target : dentistId.HasValue ? await users.FindByIdAsync(dentistId.Value, ct) : null;
        var dentistWorkspace = isDentist ? workspace : dentistId.HasValue ? await workspaces.FindAsync(dentistId.Value, ct) : null;
        var confirmedToday = IsConfirmedToday(dentistWorkspace);
        var chair = confirmedToday && dentistWorkspace?.ChairId is int chairId ? await chairs.FindAsync(chairId, ct) : null;
        var canChange = !isDentist || !confirmedToday || !await IsBusy(target.UserId, ct);
        return new(target.UserId, target.FullName, role ?? string.Empty, target.IsActive, workspace?.Version ?? 0,
            dentistId, dentist?.FullName, chair?.Id, chair?.ChairNumber, chair?.RoomName,
            target.IsActive &&
            dentist is { IsActive: true } && dentist.Role?.RoleCode == RoleCodes.Dentist &&
            confirmedToday && IsRoomOperational(chair), canChange)
        {
            HasConfirmedToday = isDentist ? confirmedToday : assistantConfirmed
        };
    }
}
