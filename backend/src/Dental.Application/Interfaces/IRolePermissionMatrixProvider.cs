using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;

namespace Dental.Application.Interfaces;

public interface IRolePermissionMatrixProvider
{
    Task<Result<RolePermissionMatrixResponse>> GetAsync(CancellationToken ct = default);
}
