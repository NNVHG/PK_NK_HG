using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Tests.Auth;

public sealed class RolePermissionMatrixTests
{
    private static RolePermissionMatrixProvider Provider(Action<AuthorizationOptions>? change = null)
    {
        var options = new AuthorizationOptions(); options.AddApplicationPolicies(); change?.Invoke(options);
        return new(new DefaultAuthorizationPolicyProvider(Options.Create(options)));
    }

    [Fact]
    public async Task Matrix_CoversEveryNamedPolicyAndMatchesActualAuthorizationForAllFiveRoles()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuthorization(x => x.AddApplicationPolicies());
        using var scope = services.BuildServiceProvider();
        var matrix = (await new RolePermissionMatrixProvider(scope.GetRequiredService<IAuthorizationPolicyProvider>()).GetAsync()).Value!;
        var names = typeof(AppPolicies).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral && x.FieldType == typeof(string)).Select(x => (string)x.GetRawConstantValue()!).Order().ToArray();
        Assert.Equal(names, matrix.Permissions.Select(x => x.Code).Order()); Assert.True(matrix.IsReadOnly);
        Assert.Equal(new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient }, matrix.Roles.Select(x => x.Code));
        var authorization = scope.GetRequiredService<IAuthorizationService>();
        foreach (var permission in matrix.Permissions)
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Name)); Assert.False(string.IsNullOrWhiteSpace(permission.Conditions));
            foreach (var role in matrix.Roles)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role.Code)], "Test"));
                Assert.Equal((await authorization.AuthorizeAsync(principal, null, permission.Code)).Succeeded, permission.AllowedRoles.Contains(role.Code));
            }
        }
    }

    [Fact]
    public async Task ImageDeletionDescribesUploaderScopeWithoutGrantingAssistantImageRead()
    {
        var matrix = (await Provider().GetAsync()).Value!;
        var deletion = matrix.Permissions.Single(x => x.Code == AppPolicies.DentalImageDelete);
        Assert.Equal(new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant }, deletion.AllowedRoles);
        Assert.Contains("DL-323", deletion.Conditions);
        Assert.DoesNotContain(RoleCodes.Assistant, matrix.Permissions.Single(x => x.Code == AppPolicies.DentalImageRead).AllowedRoles);
    }

    [Fact]
    public async Task Matrix_ReadsRuntimeRoleRequirementsInsteadOfCopyingAnIndependentGrantTable()
    {
        var matrix = (await Provider(x => x.AddPolicy(AppPolicies.PatientManage, p => p.RequireRole(RoleCodes.Dentist))).GetAsync()).Value!;
        Assert.Equal(new[] { RoleCodes.Dentist }, matrix.Permissions.Single(x => x.Code == AppPolicies.PatientManage).AllowedRoles);
    }

    [Fact]
    public async Task MultipleRoleRequirements_AreIntersectedForASingleRoleAccount()
    {
        var matrix = (await Provider(x => x.AddPolicy(AppPolicies.PatientView, p => p.RequireRole(RoleCodes.Admin, RoleCodes.Dentist)
            .RequireRole(RoleCodes.Dentist, RoleCodes.Assistant))).GetAsync()).Value!;
        Assert.Equal(new[] { RoleCodes.Dentist }, matrix.Permissions.Single(x => x.Code == AppPolicies.PatientView).AllowedRoles);
    }

    [Fact]
    public async Task UnsupportedRoleOrRequirement_FailsWithoutAdvertisingAFalseMatrix()
    {
        foreach (var provider in new[] {
            Provider(x => x.AddPolicy(AppPolicies.StaffAny, p => p.RequireRole("UNKNOWN_ROLE"))),
            Provider(x => x.AddPolicy(AppPolicies.StaffAny, p => p.RequireAuthenticatedUser())),
            Provider(x => x.AddPolicy(AppPolicies.StaffAny, p => p.RequireRole(RoleCodes.Admin).RequireAssertion(_ => true))) })
        {
            var result = await provider.GetAsync(); Assert.True(result.IsFailure); Assert.Equal("AUTH_050", result.Error.Code); Assert.Null(result.Value);
        }
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task Service_RejectsNonAdminWithoutReadingConfiguration(string? role)
    {
        var provider = Substitute.For<IRolePermissionMatrixProvider>();
        Assert.Equal(Error.Forbidden, (await new RolePermissionMatrixService(provider).GetAsync(1, role)).Error);
        Assert.Empty(provider.ReceivedCalls());
    }

    [Fact]
    public async Task Service_RequiresPositiveAuthenticatedActor()
    {
        var provider = Substitute.For<IRolePermissionMatrixProvider>();
        Assert.Equal(Error.Unauthorized, (await new RolePermissionMatrixService(provider).GetAsync(0, RoleCodes.Admin)).Error);
        Assert.Empty(provider.ReceivedCalls());
    }

    [Fact]
    public async Task ConfigurationFailure_IsReturnedAsUnavailableRatherThanMockPermissions()
    {
        var provider = Substitute.For<IRolePermissionMatrixProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(Result<RolePermissionMatrixResponse>.Failure(new Error("AUTH_050", "Configuration unavailable")));
        var user = Substitute.For<ICurrentUser>(); user.UserId.Returns(1); user.RoleCode.Returns(RoleCodes.Admin);
        var result = Assert.IsType<ObjectResult>(await new RolePermissionsController(new(provider), user).Get(default));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void Endpoint_IsAdminOnlyReadOnlyAndNoStore()
    {
        var type = typeof(RolePermissionsController);
        Assert.Equal(AppPolicies.AdminOnly, type.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
        Assert.True(method.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    [Fact]
    public async Task Http_RequiresValidJwtAndAdmin_ReturnsLiveReadOnlyMatrixWithoutADatabase()
    {
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(RolePermissionsController).Assembly);
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IRolePermissionMatrixProvider, RolePermissionMatrixProvider>();
        builder.Services.AddScoped<RolePermissionMatrixService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
        { ValidateIssuerSigningKey = true, IssuerSigningKey = key, ValidateIssuer = true, ValidIssuer = "permissions-test",
            ValidateAudience = true, ValidAudience = "permissions-test", ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
        builder.Services.AddAuthorization(o => o.AddApplicationPolicies());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            const string route = "/api/admin/role-permissions";
            using (var response = await client.GetAsync(route)) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
            using (var response = await client.GetAsync(route)) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient, RoleCodes.Admin })
            {
                var token = new JwtSecurityToken("permissions-test", "permissions-test",
                    [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
                using var response = await client.GetAsync(route);
                Assert.Equal(role == RoleCodes.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
                if (role != RoleCodes.Admin) continue;
                Assert.True(response.Headers.CacheControl?.NoStore);
                var matrix = (await response.Content.ReadFromJsonAsync<RolePermissionMatrixResponse>())!;
                Assert.True(matrix.IsReadOnly); Assert.Equal(5, matrix.Roles.Count); Assert.NotEmpty(matrix.Permissions);
                Assert.Equal(new[] { RoleCodes.Admin, RoleCodes.Receptionist }, matrix.Permissions.Single(x => x.Code == AppPolicies.PatientManage).AllowedRoles);
            }
            using (var response = await client.PostAsJsonAsync(route, new { role = RoleCodes.Patient, permissions = new[] { AppPolicies.AdminOnly } }))
                Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
        finally { await app.StopAsync(); }
    }
}
