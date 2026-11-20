using System.Security.Claims;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Dental.Api.Policies;

public static class AccessTokenVersionValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var id = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var version = context.Principal?.FindFirst("token_version")?.Value ?? "0";
        if (!int.TryParse(id, out var userId) || !int.TryParse(version, out var tokenVersion) || tokenVersion < 0)
        {
            context.Fail("Phiên đăng nhập không còn hiệu lực.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var clock = services.GetService<VietnamClock>() ?? new VietnamClock(TimeProvider.System);
        var cutoff = PatientAccountAge.LatestEligibleBirthDate(clock.Today);
        var valid = await services.GetRequiredService<DentalDbContext>().Users.AnyAsync(x =>
            x.UserId == userId && x.IsActive && x.TokenVersion == tokenVersion && !x.RequiresPhoneActivation &&
            (x.Role.RoleCode != RoleCodes.Patient ||
             (x.Patient != null ? x.Patient.DateOfBirth <= cutoff : x.DateOfBirth == null || x.DateOfBirth <= cutoff)),
            context.HttpContext.RequestAborted);
        if (!valid)
            context.Fail("Phiên đăng nhập không còn hiệu lực.");
    }
}
