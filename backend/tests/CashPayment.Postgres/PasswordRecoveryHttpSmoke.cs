using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class PasswordRecoveryHttpSmoke
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Sms : IPasswordRecoveryDelivery
    {
        public bool IsConfigured { get; set; }
        public bool Deliver { get; set; } = true;
        public Dictionary<string, string> Codes { get; } = [];
        public bool Enqueue(string phone, RecoveryCode code) { Codes[phone] = code.Code; return Deliver; }
    }
    private sealed class Audit(IAuditLogger inner, AuditFailure failure) : IAuditLogger
    {
        public Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? detail = null, string? ipAddress = null, CancellationToken ct = default)
            => inner.LogAsync(action, entityType, entityId, userId, detail, ipAddress, ct);
        public Task LogRequiredAsync(string action, string entityType, int? entityId = null, int? userId = null, string? detail = null, string? ipAddress = null, CancellationToken ct = default)
            => failure.Fail ? throw new InvalidOperationException("Synthetic audit failure") : inner.LogRequiredAsync(action, entityType, entityId, userId, detail, ipAddress, ct);
    }
    private sealed class AuditFailure { public bool Fail { get; set; } }

    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool,string> check)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Secret"] = secret;
        builder.Configuration["RateLimiting:Auth:RecoveryRequest:PermitLimit"] = "100";
        builder.Configuration["RateLimiting:Auth:RecoveryReset:PermitLimit"] = "100";
        var clock = new Clock(); var sms = new Sms(); var failure = new AuditFailure();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<IPasswordRecoveryDelivery>(sms);
        builder.Services.AddSingleton<IPasswordRecoveryCodes, PasswordRecoveryCodes>();
        builder.Services.AddScoped(_ => new DentalDbContext(options));
        builder.Services.AddScoped<AuditLogger>();
        builder.Services.AddScoped<IAuditLogger>(sp => new Audit(sp.GetRequiredService<AuditLogger>(), failure));
        builder.Services.AddScoped<IPasswordRecoveryRepository, PasswordRecoveryRepository>();
        builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddScoped<PasswordRecoveryService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(PasswordRecoveryController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true,
                ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero };
            jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o => AuthRateLimitPolicies.Configure(o, builder.Configuration));
        await using var app = builder.Build();
        app.Use(async (context, next) => { try { await next(context); } catch { context.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers();
        app.MapGet("/probe", () => "Authorized").RequireAuthorization();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        await using var db = new DentalDbContext(options);
        var hasher = new BCryptPasswordHasher(); var oldHash = hasher.Hash("Oldpass1");
        var users = new List<User>();
        foreach (var roleCode in new[] { "ADMIN", "RECEPTIONIST", "DENTIST", "ASSISTANT", "PATIENT" })
        {
            var role = new Role { RoleCode = roleCode, RoleName = "Recovery " + roleCode };
            var user = new User { Role = role, FullName = "Synthetic recovery account", Phone = "097000000" + users.Count, PasswordHash = oldHash };
            users.Add(user); db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        const string root = "/api/auth/password-recovery/";
        async Task<RecoveryResponse> Issue(string phone)
        {
            using var response = await client.PostAsJsonAsync(root + "request", new RecoveryRequest(phone));
            check(response.StatusCode == HttpStatusCode.Accepted && response.Headers.CacheControl?.NoStore == true, "Recovery request accepted/no-store");
            var json = await response.Content.ReadAsStringAsync();
            check(!json.Contains(phone) && !json.Contains("passwordHash", StringComparison.OrdinalIgnoreCase) && !json.Contains("\"code\"", StringComparison.OrdinalIgnoreCase), "Recovery response contains only challenge metadata");
            return (await response.Content.ReadFromJsonAsync<RecoveryResponse>())!;
        }
        async Task<HttpStatusCode> Reset(RecoveryResponse challenge, User user, string? code = null)
        {
            using var response = await client.PostAsJsonAsync(root + "reset", new ResetPasswordRequest(challenge.ChallengeId, user.Phone, code ?? sms.Codes[user.Phone], "Newpass2"));
            return response.StatusCode;
        }
        using (var response = await client.PostAsJsonAsync(root + "request", new RecoveryRequest(users[0].Phone)))
            check(response.StatusCode == HttpStatusCode.ServiceUnavailable && sms.Codes.Count == 0, "SMS disabled fails closed without provider calls");
        sms.IsConfigured = true;
        foreach (var user in users)
        {
            await using var staleDb = new DentalDbContext(options);
            var staleActor = await staleDb.Users.SingleAsync(x => x.UserId == user.UserId);
            var oldToken = new JwtTokenService(builder.Configuration).GenerateAccessToken(user);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
            using (var response = await client.GetAsync("/probe")) check(response.IsSuccessStatusCode, "Existing access token accepted before recovery");
            client.DefaultRequestHeaders.Authorization = null;
            var challenge = await Issue(user.Phone);
            using (var response = await client.PostAsJsonAsync(root + "request", new RecoveryRequest(user.Phone)))
                check(response.StatusCode == HttpStatusCode.TooManyRequests, "Per-phone 60 second cooldown enforced");
            var results = await Task.WhenAll(Reset(challenge, user), Reset(challenge, user));
            check(results.Count(x => x == HttpStatusCode.OK) == 1 && results.Count(x => x == HttpStatusCode.BadRequest) == 1, "Concurrent reset: exactly one winner for " + user.Role.RoleCode);
            await db.Entry(user).ReloadAsync();
            check(user.TokenVersion == 1 && hasher.Verify("Newpass2", user.PasswordHash) && !hasher.Verify("Oldpass1", user.PasswordHash), "Password persisted with bcrypt and token version incremented");
            staleActor.PasswordHash = hasher.Hash("Stale3");
            var staleWriteBlocked = false;
            try { await staleDb.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { staleWriteBlocked = true; }
            check(staleWriteBlocked, "Already-running request cannot overwrite a recovered password");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
            using (var response = await client.GetAsync("/probe")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Old JWT revoked after reset");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenService(builder.Configuration).GenerateAccessToken(user));
            using (var response = await client.GetAsync("/probe")) check(response.IsSuccessStatusCode, "New JWT accepted after reset");
            client.DefaultRequestHeaders.Authorization = null;
        }
        var account = users[0]; clock.Now = clock.Now.AddMinutes(6);
        var expired = await Issue(account.Phone); clock.Now = clock.Now.AddMinutes(5);
        check(await Reset(expired, account) == HttpStatusCode.BadRequest, "Exactly five minutes expires OTP");
        var locked = await Issue(account.Phone);
        var wrong = sms.Codes[account.Phone] == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++) check(await Reset(locked, account, wrong) == HttpStatusCode.BadRequest, "Wrong OTP rejected");
        check(await Reset(locked, account) == HttpStatusCode.BadRequest, "Correct OTP denied after fifth wrong attempt");
        clock.Now = clock.Now.AddSeconds(60);
        var beforeChange = await Issue(account.Phone); clock.Now = clock.Now.AddSeconds(60);
        var replacement = await Issue(account.Phone);
        check(await Reset(beforeChange, account) == HttpStatusCode.BadRequest, "Resend invalidates previous challenge");
        await db.Users.Where(x => x.UserId == account.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
        check(await Reset(replacement, account) == HttpStatusCode.BadRequest, "Account locked after SMS cannot be reset/unlocked");
        await db.Entry(account).ReloadAsync(); check(!account.IsActive && account.TokenVersion == 1, "Admin account lock preserved");
        var count = sms.Codes.Count; var unknown = await Issue("0970000099");
        check(sms.Codes.Count == count, "Unknown account never sends SMS");
        clock.Now = clock.Now.AddSeconds(60);
        var inactive = await Issue(account.Phone);
        check(sms.Codes.Count == count && await Reset(inactive, account) == HttpStatusCode.BadRequest, "Inactive request remains generic and cannot reset");
        var other = users[1]; var hashBeforeFailure = other.PasswordHash; var auditChallenge = await Issue(other.Phone); failure.Fail = true;
        check(await Reset(auditChallenge, other) == HttpStatusCode.InternalServerError, "Required audit failure aborts password change"); failure.Fail = false;
        await db.Entry(other).ReloadAsync(); check(other.TokenVersion == 1 && other.PasswordHash == hashBeforeFailure, "Audit rollback preserves password and credential version");
        check(await db.AuditLogs.CountAsync(x => x.Action == "MOD_AUTH_PASSWORD_RECOVERED") == 5, "Exactly one required audit per committed recovery");
        check(await db.AuditLogs.AllAsync(x => x.Detail == null || !x.Detail.Contains("097000000") && !x.Detail.Contains("Newpass2")), "Audit excludes phone/password/OTP");
        var staleUser = users[2]; var stale = await Issue(staleUser.Phone);
        await db.Users.Where(x => x.UserId == staleUser.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, hasher.Hash("Changed3")));
        check(await Reset(stale, staleUser) == HttpStatusCode.BadRequest, "Password change after SMS invalidates pending recovery grant");
        using (var invalid = await client.PostAsJsonAsync(root + "request", new RecoveryRequest("invalid")))
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Invalid phone rejected at HTTP boundary");
        var throttled = false;
        for (var i = 0; i < 105; i++)
        {
            using var response = await client.PostAsJsonAsync(root + "request", new RecoveryRequest("09600" + i.ToString("D5")));
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throttled = true;
        }
        check(throttled, "IP request rate limiter bounds requests across different phone numbers");
        await app.StopAsync();
    }
}
