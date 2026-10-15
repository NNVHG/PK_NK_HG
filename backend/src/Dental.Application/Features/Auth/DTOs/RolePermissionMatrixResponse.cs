namespace Dental.Application.Features.Auth.DTOs;

public sealed record PermissionRoleResponse(string Code, string Name);
public sealed record RolePermissionResponse(string Code, string Name, string Module,
    IReadOnlyList<string> AllowedRoles, string Conditions);
public sealed record RolePermissionMatrixResponse(IReadOnlyList<PermissionRoleResponse> Roles,
    IReadOnlyList<RolePermissionResponse> Permissions)
{
    public bool IsReadOnly => true;
}
