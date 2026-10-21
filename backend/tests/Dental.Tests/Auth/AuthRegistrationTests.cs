using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Common;
using Dental.Application.Interfaces;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Auth;
public sealed class AuthRegistrationTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IUnitOfWork unit = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher passwords = Substitute.For<IPasswordHasher>();
    private readonly ITokenService tokens = Substitute.For<ITokenService>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private AuthService Service() => new(users, unit, passwords, tokens, audit, patients);
    private static RegisterRequest Request(string phone = "0967000000") => new("Synthetic", phone, "Strong123", new(1990,1,1), "Other", null);
    [Fact] public async Task RegisterAsync_IsDisabledBeforeAnyLookupOrWrite()
    {
        Assert.Equal(PatientActivationService.RegistrationDisabled, (await Service().RegisterAsync(Request(),null)).Error);
        Assert.Empty(users.ReceivedCalls()); Assert.Empty(patients.ReceivedCalls()); Assert.Empty(unit.ReceivedCalls());
    }
    [Fact] public async Task RegisterAsync_CannotHashOrIssueTokens()
    {
        await Service().RegisterAsync(Request(),null); Assert.Empty(passwords.ReceivedCalls()); Assert.Empty(tokens.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task RegisterAsync_DoesNotDistinguishExistingPhones()
    {
        var service=Service();var a=await service.RegisterAsync(Request(),null);var b=await service.RegisterAsync(Request("0967000001"),null);
        Assert.Equal(a.Error,b.Error);Assert.Empty(users.ReceivedCalls());
    }
    [Fact] public async Task RegisterRequestValidator_StillRejectsWeakLegacyPayload()
    {
        var result=await new RegisterRequestValidator(new VietnamClock(TimeProvider.System)).ValidateAsync(Request() with{Password="weak"});
        Assert.False(result.IsValid);
    }
    [Fact] public async Task RegisterAsync_DoesNotStartTransactionForMalformedLegacyCalls()
    {
        Assert.Equal(PatientActivationService.RegistrationDisabled,(await Service().RegisterAsync(null!,null)).Error);
        Assert.Empty(unit.ReceivedCalls());Assert.DoesNotContain(typeof(RegisterRequest).GetProperties(),p=>p.Name.Contains("Role"));
    }
}
