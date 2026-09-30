using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;
using ApiPolicies = Dental.Api.Policies.Policies;

namespace Dental.Tests.Auth;

public class RbacPolicyTests
{
    private readonly IAuthorizationService _authService;

    public RbacPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(opts => Dental.Api.Policies.Policies.AddApplicationPolicies(opts));
        var sp = services.BuildServiceProvider();
        _authService = sp.GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public void StaffRoles_ShouldContainOnlyInternalClinicRoles()
    {
        // Assert
        Assert.Contains(RoleCodes.Admin, RoleCodes.StaffRoles);
        Assert.Contains(RoleCodes.Receptionist, RoleCodes.StaffRoles);
        Assert.Contains(RoleCodes.Dentist, RoleCodes.StaffRoles);
        Assert.Contains(RoleCodes.Assistant, RoleCodes.StaffRoles);
        Assert.DoesNotContain(RoleCodes.Patient, RoleCodes.StaffRoles);
        Assert.Equal(4, RoleCodes.StaffRoles.Length);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, false)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Assistant, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task AdminOnlyPolicy_ShouldAllowOnlyAdmin(string roleCode, bool expectedAllowed)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        // Act
        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.AdminOnly);

        // Assert
        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task StaffAnyPolicy_ShouldAllowStaffAndRejectPatient(string roleCode, bool expectedAllowed)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        // Act
        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.StaffAny);

        // Assert
        Assert.Equal(expectedAllowed, result.Succeeded);
    }
}

