using System.Security.Claims;
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
        if (!int.TryParse(id, out var userId) || !int.TryParse(version, out var tokenVersion) || tokenVersion < 0 ||
            !await context.HttpContext.RequestServices.GetRequiredService<DentalDbContext>().Users
                .AnyAsync(x => x.UserId == userId && x.TokenVersion == tokenVersion && !x.RequiresPhoneActivation, context.HttpContext.RequestAborted))
            context.Fail("Phiên đăng nhập không còn hiệu lực.");
    }
}
