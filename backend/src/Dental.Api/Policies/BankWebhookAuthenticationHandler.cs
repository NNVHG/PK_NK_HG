using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dental.Api.Policies;

public sealed class BankWebhookAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration, DentalDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MOD_BIL_BankWebhook";
    public static bool KeyMatches(string? expected, string? header)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 || header is null || header.Length > 1024 ||
            !header.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase)) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(header[7..])));
    }
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!KeyMatches(configuration["Payment:WebhookSecretKey"], Request.Headers.Authorization.ToString()))
            return AuthenticateResult.Fail("Webhook authentication failed.");
        if (!int.TryParse(configuration["Payment:WebhookActorUserId"], out var actorId))
            return AuthenticateResult.Fail("Webhook actor is not configured.");
        var actor = await db.Users.AsNoTracking().Where(x => x.UserId == actorId && x.IsActive)
            .Select(x => new { x.UserId, x.Role.RoleCode }).FirstOrDefaultAsync(Context.RequestAborted);
        if (actor is null || actor.RoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return AuthenticateResult.Fail("Webhook actor is unavailable.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.UserId.ToString()),
            new Claim(ClaimTypes.Role, actor.RoleCode)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
