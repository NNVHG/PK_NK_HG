using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Dental.Api.Policies;

/// <summary>
/// Authorization policies được đặt tên rõ ràng — dùng [Authorize(Policy = Policies.AdminOnly)].
/// Tránh dùng [Authorize(Roles = "ADMIN")] string thô.
/// </summary>
public static class Policies
{
    public const string AdminOnly  = "AdminOnly";
    public const string StaffAny   = "StaffAny";    // Admin, Lễ tân, Nha sĩ, Phụ tá

    public static void AddApplicationPolicies(this AuthorizationOptions opts)
    {
        opts.AddPolicy(AdminOnly, policy =>
            policy.RequireRole(RoleCodes.Admin));

        opts.AddPolicy(StaffAny, policy =>
            policy.RequireRole(RoleCodes.StaffRoles));
    }
}
