using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

// Synthetic accounts and loopback HTTP against a disposable database only.
internal static class PasswordSessionPolicyHttpSmoke
{
    private sealed class Failure : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Detail!.Contains("change_password_success")))
                throw new InvalidOperationException("Synthetic required password audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var failure = new Failure();
        var dbOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        b.Logging.ClearProviders(); b.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); b.Configuration["Jwt:Secret"] = secret;
        b.Services.AddScoped(_ => new DentalDbContext(dbOptions));
        b.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        b.Services.AddScoped<IUserRepository, UserRepository>();
        b.Services.AddScoped<IPatientRepository, PatientRepository>();
        b.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        b.Services.AddScoped<IAuditLogger, AuditLogger>();
        b.Services.AddScoped<ITokenService, JwtTokenService>();
        b.Services.AddScoped<AuthService>(); b.Services.AddScoped<LoginRequestValidator>();
        b.Services.AddScoped<ChangePasswordRequestValidator>();
        b.Services.AddHttpContextAccessor(); b.Services.AddScoped<ICurrentUser, CurrentUser>();
        b.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => {
            jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic",
                ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero };
            jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync };
        });
        b.Services.AddAuthorization(o => o.AddApplicationPolicies());
        b.Services.AddRateLimiter(o => AuthRateLimitPolicies.Configure(o, b.Configuration));
        await using var app = b.Build(); var ip = 0;
        app.Use(async (c, next) => { var n = Interlocked.Increment(ref ip); c.Connection.RemoteIpAddress = IPAddress.Parse($"10.3.{n/250}.{n%250+1}"); try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        await using var db = new DentalDbContext(dbOptions); var hasher = new BCryptPasswordHasher(); var seq = 0;
        async Task<HttpResponseMessage> Send(string path, object? data = null, string? token = null, string method = "POST")
        { using var req = new HttpRequestMessage(new HttpMethod(method), path); if (data is not null) req.Content = JsonContent.Create(data); if (token is not null) req.Headers.Authorization = new("Bearer", token); return await client.SendAsync(req); }
        async Task<bool> Session(string token) { using var response = await Send("/api/auth/me", token: token, method: "GET"); return response.IsSuccessStatusCode; }
        async Task<string> Login(User user, string password)
        { using var response = await Send("/api/auth/login", new LoginRequest(user.Phone, password)); check(response.StatusCode == HttpStatusCode.OK, "Credential login succeeds with selected password"); return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken; }
        using (var response = await Send("/api/auth/change-password", new ChangePasswordRequest("OldPass1", "NewPass2")))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot self-change password");
        foreach (var code in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            var role = new Role { RoleCode = code, RoleName = "Synthetic " + code };
            var user = new User { Role = role, Phone = "096760" + (++seq).ToString("D4"), FullName = "Synthetic " + code,
                PasswordHash = hasher.Hash("OldPass1"), TokenVersion = 7, PhoneVerifiedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
            db.Users.Add(user); await db.SaveChangesAsync(); var hashBefore = user.PasswordHash; var verifiedBefore = user.PhoneVerifiedAtUtc;
            var first = await Login(user, "OldPass1"); var second = await Login(user, "OldPass1");
            var reader = new JwtSecurityTokenHandler(); var firstToken = reader.ReadJwtToken(first); var secondToken = reader.ReadJwtToken(second);
            check(first != second && firstToken.Id != secondToken.Id, "Two independently issued sessions: " + code);
            check(firstToken.ValidTo <= DateTime.UtcNow.AddMinutes(30) && firstToken.ValidTo > DateTime.UtcNow.AddMinutes(29), "Original access token uses DL031 thirty minute lifetime");
            var expired = new JwtSecurityToken("DentalClinic", "DentalClinicClient", firstToken.Claims.Where(c => c.Type is not ("exp" or "nbf" or "iat")),
                expires: DateTime.UtcNow.AddMinutes(-1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
            var expiredText = reader.WriteToken(expired); check(!await Session(expiredText), "Expired JWT rejected before password change");
            using (var response = await Send("/api/auth/change-password", new ChangePasswordRequest("WrongPass1", "NewPass2"), first))
                check(response.StatusCode == HttpStatusCode.BadRequest && response.Headers.CacheControl?.NoStore == true, "Wrong current password rejected with NoStore");
            using (var response = await Send("/api/auth/change-password", new ChangePasswordRequest("OldPass1", "Aa1234"), first))
                check(response.StatusCode == HttpStatusCode.BadRequest && response.Headers.CacheControl?.NoStore == true, "New password policy enforced before mutation");
            await db.Entry(user).ReloadAsync(); check(user.PasswordHash == hashBefore && user.TokenVersion == 7, "Failed attempts preserve hash and all session versions");
            check(await Session(first) && await Session(second), "Both sessions remain usable after failed attempts");
            using (var response = await Send("/api/auth/change-password", new ChangePasswordRequest("OldPass1", "NewPass2"), first))
            {
                check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "Self-change succeeds with NoStore: " + code);
                var text = await response.Content.ReadAsStringAsync(); var payload = JsonDocument.Parse(text).RootElement;
                check(payload.GetProperty("sessionPolicy").GetString() == "KeepUntilExpiry" && payload.GetProperty("message").GetString()!.Contains("thời hạn cũ"), "API explicitly confirms original expiry policy");
                check(!text.Contains(first) && !text.Contains(second) && !text.Contains("OldPass1") && !text.Contains("NewPass2") && !text.Contains("accessToken") && !text.Contains("refreshToken"), "Success issues no token and echoes no credential");
                check(!response.Headers.Contains("Set-Cookie") && !response.Headers.Contains("Authorization"), "No silent session refresh headers");
            }
            await db.Entry(user).ReloadAsync();
            check(user.TokenVersion == 7 && user.PhoneVerifiedAtUtc == verifiedBefore && !user.RequiresPhoneActivation && user.RoleId == role.RoleId, "Self-change preserves version, phone verification and role");
            check(user.PasswordHash != hashBefore && hasher.Verify("NewPass2", user.PasswordHash) && !hasher.Verify("OldPass1", user.PasswordHash), "Only the new password works against persisted bcrypt hash");
            check(await Session(first) && await Session(second), "Current and other original JWTs remain authorized: " + code);
            check(reader.ReadJwtToken(first).ValidTo == firstToken.ValidTo && reader.ReadJwtToken(second).ValidTo == secondToken.ValidTo, "Original JWT expiration is never extended");
            check(!await Session(expiredText), "Self-change never reactivates an expired JWT");
            using (var response = await Send("/api/auth/login", new LoginRequest(user.Phone, "OldPass1")))
                check(response.StatusCode == HttpStatusCode.Unauthorized, "Old password cannot create a new session");
            var newSession = await Login(user, "NewPass2"); check(await Session(newSession), "New password login produces a usable independent session");
            var audit = await db.AuditLogs.SingleAsync(a => a.UserId == user.UserId && a.Detail!.Contains("change_password_success"));
            check(!audit.Detail!.Contains("OldPass1") && !audit.Detail.Contains("NewPass2") && !audit.Detail.Contains(user.PasswordHash) && !audit.Detail.Contains(first), "Password success audit contains no credential or token");
            var successfulHash = user.PasswordHash;
            failure.Enabled = true;
            using (var response = await Send("/api/auth/change-password", new ChangePasswordRequest("NewPass2", "ThirdPass3"), first))
                check(response.StatusCode == HttpStatusCode.InternalServerError, "Required audit failure aborts self-change: " + code);
            failure.Enabled = false; await db.Entry(user).ReloadAsync();
            check(user.PasswordHash == successfulHash && user.TokenVersion == 7, "Failed audit rolls back password and leaves JWT policy unchanged");
            check(await Session(first) && await Session(second), "Existing sessions survive failed transaction without renewal");
            check(await db.AuditLogs.CountAsync(a => a.UserId == user.UserId && a.Detail!.Contains("change_password_success")) == 1, "Failed change commits no success audit");
            var parallel = await Task.WhenAll(
                Send("/api/auth/change-password", new ChangePasswordRequest("NewPass2", "ParallelA3"), first),
                Send("/api/auth/change-password", new ChangePasswordRequest("NewPass2", "ParallelB3"), second));
            check(parallel.Count(r => r.IsSuccessStatusCode) == 1 && parallel.Count(r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest) == 1,
                "Concurrent self-change has one winner, no silent overwrite");
            foreach (var response in parallel) response.Dispose();
            check(await Session(first) && await Session(second), "Concurrent password changes do not revoke or renew original JWTs");
            await db.Users.Where(u => u.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, 8));
            check(!await Session(first) && !await Session(second) && !await Session(newSession), "Independent version revocation still invalidates every session");
        }
        await app.StopAsync();
    }
}
