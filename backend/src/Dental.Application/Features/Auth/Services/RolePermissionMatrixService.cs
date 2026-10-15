using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.Auth.Services;

public sealed class RolePermissionMatrixService(IRolePermissionMatrixProvider provider)
{
    public Task<Result<RolePermissionMatrixResponse>> GetAsync(int userId, string? role, CancellationToken ct = default)
    {
        if (userId <= 0) return Task.FromResult(Result<RolePermissionMatrixResponse>.Failure(Error.Unauthorized));
        if (role != RoleCodes.Admin) return Task.FromResult(Result<RolePermissionMatrixResponse>.Failure(Error.Forbidden));
        return provider.GetAsync(ct);
    }
}
