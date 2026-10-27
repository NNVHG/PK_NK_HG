using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class IdentityAndLockoutSecurityTests
{
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IKeyedIdentityHasher _identityHasher;

    public IdentityAndLockoutSecurityTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySecurity:HmacKey"] = "SuperSecretHmacKeyForUnitTestingPurposes_2026!#*"
        }).Build();

        _identityHasher = new KeyedIdentityHasher(config);
    }

    [Fact]
    public void KeyedHasher_Produces64Hex_AndDistinguishesDocumentTypes()
    {
        var cccdHash = _identityHasher.Hash("001200000001", IdentityDocumentType.CitizenId);
        Assert.Equal(64, cccdHash.Length);

        var passportHash = _identityHasher.Hash("001200000001", IdentityDocumentType.Passport, "VN");
        Assert.NotEqual(cccdHash, passportHash);

        var passportUsHash = _identityHasher.Hash("001200000001", IdentityDocumentType.Passport, "US");
        Assert.NotEqual(passportHash, passportUsHash);

        Assert.True(_identityHasher.Matches(cccdHash, "001200000001", IdentityDocumentType.CitizenId));
        Assert.True(_identityHasher.Matches(passportHash, "001200000001", IdentityDocumentType.Passport, "VN"));
        Assert.False(_identityHasher.Matches(passportHash, "001200000001", IdentityDocumentType.Passport, "US"));
    }

    [Fact]
    public void KeyedHasher_BackwardCompatibleWithLegacySha256()
    {
        const string cccd = "079090000001";
        var legacySha256 = Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(cccd);

        Assert.True(_identityHasher.Matches(legacySha256, cccd, IdentityDocumentType.CitizenId));
        Assert.False(_identityHasher.Matches(legacySha256, "079090000002", IdentityDocumentType.CitizenId));
    }

    [Fact]
    public void KeyedHasher_RequiresConfiguredSecret()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Throws<InvalidOperationException>(() => new KeyedIdentityHasher(config));
        Assert.Throws<ArgumentException>(() => new KeyedIdentityHasher(new byte[31]));
    }

    [Fact]
    public void LoginRequestValidator_AcceptsCitizenIdAndPassport()
    {
        var validator = new LoginRequestValidator();

        Assert.True(validator.Validate(new LoginRequest("0900000001", "Password@123")).IsValid);
        Assert.True(validator.Validate(new LoginRequest("001200000001", "Password@123", IdentityDocumentType.CitizenId)).IsValid);
        Assert.True(validator.Validate(new LoginRequest("B1234567", "Password@123", IdentityDocumentType.Passport, "USA")).IsValid);

        var invalidPassport = validator.Validate(new LoginRequest("B1234567", "Password@123", IdentityDocumentType.Passport, null));
        Assert.False(invalidPassport.IsValid);
        Assert.Contains(invalidPassport.Errors, e => e.PropertyName == nameof(LoginRequest.NationalityCode));
    }

    [Fact]
    public void LockoutService_LocksAccountAfter5FailedAttempts()
    {
        var lockout = new LoginLockoutService(TimeProvider.System);
        const string phone = "0988776655";

        for (int i = 1; i <= 4; i++)
        {
            var recorded = lockout.RecordFailedAttempt(phone, out var nowLocked, out _);
            Assert.True(recorded);
            Assert.False(nowLocked);
            Assert.False(lockout.IsLocked(phone, out _));
        }

        lockout.RecordFailedAttempt(phone, out var lockedFifth, out var duration);
        Assert.True(lockedFifth);
        Assert.True(duration.TotalMinutes >= 15);
        Assert.True(lockout.IsLocked(phone, out var remaining));
        Assert.True(remaining.TotalMinutes > 0);

        lockout.ResetAttempts(phone);
        Assert.False(lockout.IsLocked(phone, out _));
    }

    [Fact]
    public void LockoutService_ExpiredLockoutStartsFreshWindow()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero));
        using var lockout = new LoginLockoutService(clock);

        for (var i = 0; i < LoginLockoutService.MaxAttempts; i++)
            lockout.RecordFailedAttempt("CCCD:001200000001", out _, out _);

        Assert.True(lockout.IsLocked("CCCD:001200000001", out _));
        clock.Now = clock.Now.Add(LoginLockoutService.LockoutDuration).AddSeconds(1);
        Assert.False(lockout.IsLocked("CCCD:001200000001", out _));

        lockout.RecordFailedAttempt("CCCD:001200000001", out var locked, out _);
        Assert.False(locked);
        Assert.False(lockout.IsLocked("CCCD:001200000001", out _));
    }

    [Fact]
    public async Task AuthService_UnknownAccountHasSameLockoutResponse()
    {
        using var lockout = new LoginLockoutService(TimeProvider.System);
        var svc = new AuthService(
            _userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository,
            lockoutService: lockout, identityHasher: _identityHasher);
        var request = new LoginRequest("0911223344", "wrong_pass");

        for (var i = 0; i < 4; i++)
        {
            var result = await svc.LoginAsync(request, null);
            Assert.Equal("AUTH_001", result.Error.Code);
        }

        var fifth = await svc.LoginAsync(request, null);
        Assert.Equal("AUTH_010", fifth.Error.Code);
        var lockedAttempt = await svc.LoginAsync(request, null);
        Assert.Equal("AUTH_010", lockedAttempt.Error.Code);
    }

    [Fact]
    public async Task AuthService_PassportLockoutUsesNationality()
    {
        using var lockout = new LoginLockoutService(TimeProvider.System);
        var svc = new AuthService(
            _userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository,
            lockoutService: lockout, identityHasher: _identityHasher);
        var vn = new LoginRequest("B1234567", "wrong_pass", IdentityDocumentType.Passport, "VN");
        var us = new LoginRequest("B1234567", "wrong_pass", IdentityDocumentType.Passport, "US");

        for (var i = 0; i < 5; i++)
            await svc.LoginAsync(vn, null);

        var usFirst = await svc.LoginAsync(us, null);
        Assert.Equal("AUTH_001", usFirst.Error.Code);
    }

    [Fact]
    public async Task AuthService_EnforcesTemporaryLockout_WhenMaxAttemptsExceeded()
    {
        var lockout = new LoginLockoutService(TimeProvider.System);
        var svc = new AuthService(
            _userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository,
            lockoutService: lockout,
            identityHasher: _identityHasher
        );

        const string targetPhone = "0911223344";
        var user = new User
        {
            UserId = 10,
            Phone = targetPhone,
            PasswordHash = "correct_hash",
            FullName = "Lockout Test",
            IsActive = true,
            Role = new Role { RoleCode = RoleCodes.Patient, RoleName = "Bệnh nhân" }
        };

        _userRepo.FindByPhoneAsync(targetPhone, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong_pass", "correct_hash").Returns(false);

        for (int i = 0; i < 4; i++)
        {
            var failResult = await svc.LoginAsync(new LoginRequest(targetPhone, "wrong_pass"), null);
            Assert.True(failResult.IsFailure);
            Assert.Equal("AUTH_001", failResult.Error.Code);
        }

        var fifthResult = await svc.LoginAsync(new LoginRequest(targetPhone, "wrong_pass"), null);
        Assert.True(fifthResult.IsFailure);
        Assert.Equal("AUTH_010", fifthResult.Error.Code);

        _passwordHasher.Verify("correct_pass", "correct_hash").Returns(true);
        var lockedAttempt = await svc.LoginAsync(new LoginRequest(targetPhone, "correct_pass"), null);
        Assert.True(lockedAttempt.IsFailure);
        Assert.Equal("AUTH_010", lockedAttempt.Error.Code);
    }

    [Fact]
    public async Task AuthService_ReturnsAuthActivationRequired_WhenUserRequiresPhoneActivation()
    {
        var svc = new AuthService(
            _userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository,
            lockoutService: new LoginLockoutService(TimeProvider.System),
            identityHasher: _identityHasher
        );

        const string targetPhone = "0988776655";
        var user = new User
        {
            UserId = 12,
            Phone = targetPhone,
            PasswordHash = "correct_hash",
            FullName = "Activation Pending User",
            IsActive = true,
            RequiresPhoneActivation = true,
            Role = new Role { RoleCode = RoleCodes.Patient, RoleName = "Bệnh nhân" }
        };

        _userRepo.FindByPhoneAsync(targetPhone, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct_pass", "correct_hash").Returns(true);

        var result = await svc.LoginAsync(new LoginRequest(targetPhone, "correct_pass"), null);
        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_ACTIVATION_REQUIRED", result.Error.Code);
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }

    [Fact]
    public void ActivationRequestValidator_AcceptsPassportWithNationality()
    {
        var validator = new ActivationRequestValidator();
        var validPassport = new ActivationRequest("B1234567", "Default123", IdentityDocumentType.Passport, "VN");
        Assert.True(validator.Validate(validPassport).IsValid);

        var missingCountry = new ActivationRequest("B1234567", "Default123", IdentityDocumentType.Passport, null);
        Assert.False(validator.Validate(missingCountry).IsValid);

        var validCccd = new ActivationRequest("001200000001", "Default123", IdentityDocumentType.CitizenId);
        Assert.True(validator.Validate(validCccd).IsValid);
    }
}
