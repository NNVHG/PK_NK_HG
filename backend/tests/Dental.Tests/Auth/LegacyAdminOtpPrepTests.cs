using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class LegacyAdminOtpPrepTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IKeyedIdentityHasher _identityHasher = new KeyedIdentityHasher(new byte[32]);

    [Fact]
    public void LegacyPhoneVerificationPreflight_GuardRejectsProductionDatabase()
    {
        using var prodDb = new DentalDbContext(
            new DbContextOptionsBuilder<DentalDbContext>()
                .UseNpgsql("Host=localhost;Database=dental_db;Username=postgres")
                .Options);
        Assert.Throws<InvalidOperationException>(() =>
            LegacyPhoneVerificationPreflight.AssertNotProductionDatabase(prodDb));

        using var cloneDb = new DentalDbContext(
            new DbContextOptionsBuilder<DentalDbContext>()
                .UseNpgsql("Host=localhost;Database=dental_cash_test_clone;Username=postgres")
                .Options);
        LegacyPhoneVerificationPreflight.AssertNotProductionDatabase(cloneDb);
    }

    [Fact]
    public void LegacyPhoneVerificationInventory_ComputesCandidatesAndReadinessSafely()
    {
        var ready = new LegacyPhoneVerificationInventory(
            TotalUsers: 12,
            InactiveUsers: 1,
            VerifiedActive: 3,
            PendingInitialActivation: 2,
            PendingPhoneChange: 1,
            PendingLegacyVerification: 0,
            LegacyUnverifiedAdmins: 1,
            LegacyUnverifiedStaffNonAdmin: 2,
            LegacyUnverifiedPatientsEligible: 2,
            LegacyUnverifiedPatientsBlocked: 0,
            InvalidPhoneActiveUsers: 0,
            InconsistentVerifiedPendingUsers: 0,
            ReachableActiveAdmins: 1);

        Assert.Equal(5, ready.TotalLegacyCandidates);
        Assert.True(ready.CanTransitionSafely);

        Assert.False((ready with { ReachableActiveAdmins = 0 }).CanTransitionSafely);
        Assert.False((ready with { LegacyUnverifiedPatientsBlocked = 1 }).CanTransitionSafely);
        Assert.False((ready with { InvalidPhoneActiveUsers = 1 }).CanTransitionSafely);
        Assert.False((ready with { InconsistentVerifiedPendingUsers = 1 }).CanTransitionSafely);
    }

    [Fact]
    public async Task AuthService_ReturnsAuthActivationRequired_WhenAdminIsMarkedLegacyVerification()
    {
        var svc = new AuthService(
            _userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository,
            lockoutService: new LoginLockoutService(TimeProvider.System),
            identityHasher: _identityHasher);

        const string adminPhone = "0900000001";
        var admin = new User
        {
            UserId = 1,
            Phone = adminPhone,
            PasswordHash = "admin_bcrypt_hash",
            FullName = "Admin Legacy Verification",
            IsActive = true,
            RequiresPhoneActivation = true,
            PhoneActivationReason = PhoneActivationReason.LegacyVerification,
            PhoneVerifiedAtUtc = null,
            Role = new Role { RoleCode = RoleCodes.Admin, RoleName = "Quản trị viên" }
        };

        _userRepo.FindByPhoneAsync(adminPhone, Arg.Any<CancellationToken>()).Returns(admin);
        _passwordHasher.Verify("AdminSecret123", "admin_bcrypt_hash").Returns(true);

        var result = await svc.LoginAsync(new LoginRequest(adminPhone, "AdminSecret123"), null);
        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_ACTIVATION_REQUIRED", result.Error.Code);
        Assert.Null(admin.PhoneVerifiedAtUtc);
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }
}
