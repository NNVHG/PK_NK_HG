namespace Dental.Application.Features.MOD_MST.DTOs;
public sealed record ChangeWorkspaceRequest(int? ChairId, int? DentistId, int ExpectedVersion, bool Confirmed);
public sealed record WorkspaceTargetQuery(int Page = 1, string? Keyword = null);
public sealed record WorkspaceTarget(int UserId, string FullName, string RoleCode, bool IsActive);
public sealed record WorkspaceSnapshot(int UserId, string FullName, string RoleCode, bool IsActive, int Version,
    int? WorkingDentistId, string? DentistName, int? ChairId, string? ChairNumber, string? RoomName, bool IsLocationUsable, bool CanChange);
public sealed record WorkspaceChairOption(int Id, string ChairNumber, string RoomName, int Status, bool IsActive, int? ClaimedByUserId);
public sealed record WorkspaceDentistOption(int DentistId, string DentistName, int? ChairId, string? ChairNumber, string? RoomName, int AssistantCount, bool IsReady);
public sealed record WorkspaceOptions(IReadOnlyList<WorkspaceChairOption> Chairs, IReadOnlyList<WorkspaceDentistOption> Dentists);
