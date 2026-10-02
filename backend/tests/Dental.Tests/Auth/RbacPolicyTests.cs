using Dental.Domain.Constants;
using Dental.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
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

    [Fact]
    public void StaffController_RequiresAdminOnlyPolicy_AndHasNoDeleteEndpoint()
    {
        var authorize = typeof(StaffController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(ApiPolicies.AdminOnly, authorize!.Policy);
        Assert.DoesNotContain(typeof(StaffController).GetMethods(), method =>
            method.GetCustomAttributes<HttpDeleteAttribute>().Any());
    }

    [Fact]
    public void AuditLogsController_RequiresAdminOnlyPolicy_AndOnlyExposesGet()
    {
        var authorize = typeof(AuditLogsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(ApiPolicies.AdminOnly, authorize!.Policy);

        var actionMethods = typeof(AuditLogsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(AuditLogsController));
        Assert.Single(actionMethods, method => method.GetCustomAttributes<HttpGetAttribute>().Any());
        Assert.DoesNotContain(actionMethods, method =>
            method.GetCustomAttributes<HttpPostAttribute>().Any() ||
            method.GetCustomAttributes<HttpPutAttribute>().Any() ||
            method.GetCustomAttributes<HttpPatchAttribute>().Any() ||
            method.GetCustomAttributes<HttpDeleteAttribute>().Any());
    }

    [Fact]
    public void PatientsController_UsesSpecifiedPolicies_AndHasNoDeleteEndpoint()
    {
        var methods = typeof(PatientsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PatientsController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.PatientManage, Assert.Single(methods[nameof(PatientsController.CreatePatient)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientManage, Assert.Single(methods[nameof(PatientsController.CheckDuplicate)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientView, Assert.Single(methods[nameof(PatientsController.GetPatient)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientView, Assert.Single(methods[nameof(PatientsController.GetPatients)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientEdit, Assert.Single(methods[nameof(PatientsController.UpdatePatient)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.DoesNotContain(methods.Values, method => method.GetCustomAttributes<HttpDeleteAttribute>().Any());
    }

    [Fact]
    public void VisitsController_UsesPatientManageForCreateAndPatientViewForHistory()
    {
        var methods = typeof(VisitsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(VisitsController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.PatientManage, Assert.Single(methods[nameof(VisitsController.CreateVisit)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientView, Assert.Single(methods[nameof(VisitsController.GetVisits)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.DoesNotContain(methods.Values, method =>
            method.GetCustomAttributes<HttpPutAttribute>().Any() ||
            method.GetCustomAttributes<HttpPatchAttribute>().Any() ||
            method.GetCustomAttributes<HttpDeleteAttribute>().Any());
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

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task PatientManagePolicy_ShouldAllowOnlyConfiguredRoles(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.PatientManage);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Patient, true)]
    public async Task PatientViewPolicy_ShouldAllowConfiguredRoles(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.PatientView);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Assistant, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task PatientEditPolicy_ShouldAllowOnlyReceptionistAndAdmin(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.PatientEdit);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }
}

