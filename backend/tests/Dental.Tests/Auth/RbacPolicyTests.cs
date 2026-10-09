using Dental.Domain.Constants;
using Dental.Api.Controllers;
using Dental.Api.RateLimiting;
using Dental.Application.Features.ServiceCatalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    public void AuthController_RequiresAuthenticationExceptRegisterAndLogin()
    {
        var controllerAuthorize = typeof(AuthController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(controllerAuthorize);

        var methods = typeof(AuthController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(AuthController))
            .ToDictionary(method => method.Name);

        foreach (var methodName in new[] { nameof(AuthController.Register), nameof(AuthController.Login) })
        {
            Assert.NotNull(methods[methodName].GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(methods[methodName].GetCustomAttribute<EnableRateLimitingAttribute>());
        }

        foreach (var methodName in new[]
                 {
                     nameof(AuthController.GetMe), nameof(AuthController.UpdateProfile),
                     nameof(AuthController.ChangePassword), nameof(AuthController.Logout),
                 })
        {
            Assert.Null(methods[methodName].GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(methods[methodName].GetCustomAttribute<EnableRateLimitingAttribute>());
        }
    }

    [Fact]
    public void DevController_RequiresAuthenticationAndExpectedPolicies()
    {
        Assert.NotNull(typeof(DevController).GetCustomAttribute<AuthorizeAttribute>());

        var methods = typeof(DevController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(DevController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.AdminOnly,
            methods[nameof(DevController.AdminOnly)].GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(ApiPolicies.StaffAny,
            methods[nameof(DevController.StaffOnly)].GetCustomAttribute<AuthorizeAttribute>()?.Policy);
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

    [Fact]
    public void MedicalHistoryController_UsesWriteAndViewPolicies_AndHasNoMutationEndpoint()
    {
        var methods = typeof(MedicalHistoryController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(MedicalHistoryController))
            .ToArray();

        Assert.Equal(ApiPolicies.MedicalHistoryWrite, Assert.Single(methods
            .Single(method => method.Name == nameof(MedicalHistoryController.Record))
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(2, methods.Count(method => method.GetCustomAttributes<AuthorizeAttribute>()
            .Any(attribute => attribute.Policy == ApiPolicies.PatientView)));
        Assert.DoesNotContain(methods, method =>
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

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task MedicalHistoryWritePolicy_ShouldAllowOnlyReceptionistAssistantAndAdmin(
        string roleCode,
        bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.MedicalHistoryWrite);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Fact]
    public void VitalSignsController_UsesWriteAndViewPolicies_AndHasNoMutationEndpoint()
    {
        var methods = typeof(VitalSignsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(VitalSignsController))
            .ToArray();

        Assert.Equal(ApiPolicies.VitalSignsWrite, Assert.Single(methods
            .Single(method => method.Name == nameof(VitalSignsController.Record))
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.PatientView, Assert.Single(methods
            .Single(method => method.Name == nameof(VitalSignsController.GetPatientVitalSigns))
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.DoesNotContain(methods, method =>
            method.GetCustomAttributes<HttpPutAttribute>().Any() ||
            method.GetCustomAttributes<HttpPatchAttribute>().Any() ||
            method.GetCustomAttributes<HttpDeleteAttribute>().Any());
    }

    [Fact]
    public void ServiceCatalogController_UsesViewAndAdminPoliciesForEndpoints()
    {
        var methods = typeof(ServiceCatalogController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ServiceCatalogController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.ServiceCatalogView, Assert.Single(methods[nameof(ServiceCatalogController.GetPage)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.ServiceCatalogView, Assert.Single(methods[nameof(ServiceCatalogController.GetById)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        foreach (var methodName in new[]
                 {
                     nameof(ServiceCatalogController.Create), nameof(ServiceCatalogController.Update),
                     nameof(ServiceCatalogController.AddPrice), nameof(ServiceCatalogController.Deactivate),
                     nameof(ServiceCatalogController.Activate),
                 })
        {
            Assert.Equal(ApiPolicies.AdminOnly, Assert.Single(methods[methodName]
                .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        }
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Receptionist, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task ServiceCatalogViewPolicy_AllowsOnlyAdminDentistAndAssistant(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.ServiceCatalogView);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task VitalSignsWritePolicy_ShouldAllowOnlyConfiguredRoles(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.VitalSignsWrite);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Fact]
    public void PatientSafetyAlertsController_RequiresStaffOnlyPolicy()
    {
        var authorize = typeof(PatientSafetyAlertsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(ApiPolicies.PatientSafetyAlertsView, authorize!.Policy);
        Assert.Contains(typeof(PatientSafetyAlertsController).GetMethods(), method =>
            method.Name == nameof(PatientSafetyAlertsController.Get) &&
            method.GetCustomAttributes<HttpGetAttribute>().Any());
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task PatientSafetyAlertsViewPolicy_ShouldAllowStaffAndRejectPatient(
        string roleCode,
        bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.PatientSafetyAlertsView);

        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Fact]
    public void PatientTimelineController_UsesPatientViewPolicy()
    {
        var authorize = typeof(PatientTimelineController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(ApiPolicies.PatientView, authorize!.Policy);
    }
    [Fact]
    public void AppointmentsController_UsesExpectedPolicies()
    {
        var methods = typeof(AppointmentsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(AppointmentsController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.AppointmentCreate, Assert.Single(methods[nameof(AppointmentsController.CreateAppointment)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.AppointmentView, Assert.Single(methods[nameof(AppointmentsController.GetAppointments)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.AppointmentView, Assert.Single(methods[nameof(AppointmentsController.GetAppointmentById)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.AppointmentView, Assert.Single(methods[nameof(AppointmentsController.CancelAppointment)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.AppointmentManage, Assert.Single(methods[nameof(AppointmentsController.RescheduleAppointment)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
    }

    [Fact]
    public void QueueController_UsesExpectedPolicies()
    {
        var methods = typeof(QueueController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(QueueController))
            .ToDictionary(method => method.Name);

        Assert.Equal(ApiPolicies.QueueCheckIn, Assert.Single(methods[nameof(QueueController.CheckIn)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.QueueView, Assert.Single(methods[nameof(QueueController.GetQueue)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.QueueView, Assert.Single(methods[nameof(QueueController.GetQueueDetail)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
        Assert.Equal(ApiPolicies.QueueStatusUpdate, Assert.Single(methods[nameof(QueueController.UpdateStatus)]
            .GetCustomAttributes<AuthorizeAttribute>()).Policy);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, false)]
    [InlineData(RoleCodes.Assistant, false)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task QueueCheckInPolicy_AllowsOnlyAdminAndReceptionist(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.QueueCheckIn);
        Assert.Equal(expectedAllowed, result.Succeeded);
    }

    [Theory]
    [InlineData(RoleCodes.Admin, true)]
    [InlineData(RoleCodes.Receptionist, true)]
    [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, true)]
    [InlineData(RoleCodes.Patient, false)]
    public async Task QueueViewPolicy_AllowsStaffAndRejectsPatient(string roleCode, bool expectedAllowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleCode)
        }, "TestAuth"));

        var result = await _authService.AuthorizeAsync(user, null, ApiPolicies.QueueView);
        Assert.Equal(expectedAllowed, result.Succeeded);
    }

}

