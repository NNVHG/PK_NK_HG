using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuthRegistrationTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private AuthService CreateService() => new(
        _userRepository, _unitOfWork, _passwordHasher, _tokenService, _auditLogger, _patientRepository);

    private void UseTransactionCallback()
    {
        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<RegisterResponse>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<RegisterResponse>>>>()(call.Arg<CancellationToken>()));
    }

    private static RegisterRequest ValidRequest(string phone = "0912345678") =>
        new("Nguyễn An", phone, "Strong1", new DateOnly(1995, 5, 12), "Female", "an@example.com");

    [Fact]
    public async Task RegisterAsync_CreatesPatientUserWithPatientRoleAndAuditWithoutPersonalData()
    {
        UseTransactionCallback();
        _userRepository.PhoneExistsAsync("0912345678", null, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.FindRoleByCodeAsync(RoleCodes.Patient, Arg.Any<CancellationToken>())
            .Returns(new Role { RoleId = 5, RoleCode = RoleCodes.Patient, RoleName = "Bệnh nhân" });
        _passwordHasher.Hash("Strong1").Returns("hashed-value");
        _userRepository.AddAsync(Arg.Do<User>(user => user.UserId = 42), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await CreateService().RegisterAsync(ValidRequest(), "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value!.UserId);
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(user =>
            user.Role.RoleCode == RoleCodes.Patient && user.PasswordHash == "hashed-value"), Arg.Any<CancellationToken>());
        await _patientRepository.Received(1).AddAsync(Arg.Is<Patient>(patient =>
            patient.UserId == 42 && patient.Phone == "0912345678"), Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            AuditActions.UserRegistered, "User", 42, 42,
            Arg.Is<string?>(detail => detail != null && detail.Contains("42") && !detail.Contains("Nguyễn An") && !detail.Contains("0912345678")),
            "127.0.0.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicatePhoneReturnsConflictWithoutAddingRecords()
    {
        UseTransactionCallback();
        _userRepository.PhoneExistsAsync("0912345678", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().RegisterAsync(ValidRequest(), null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.RegistrationPhoneExists.Code, result.Error.Code);
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _patientRepository.DidNotReceive().AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterRequestValidator_RejectsWeakPassword()
    {
        var request = ValidRequest() with { Password = "weak" };

        var result = await new RegisterRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task RegisterAsync_AlwaysUsesPatientRoleAndRequestDoesNotContainRole()
    {
        UseTransactionCallback();
        _userRepository.PhoneExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.FindRoleByCodeAsync(RoleCodes.Patient, Arg.Any<CancellationToken>())
            .Returns(new Role { RoleId = 5, RoleCode = RoleCodes.Patient, RoleName = "Bệnh nhân" });
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hash");
        _userRepository.AddAsync(Arg.Do<User>(user => user.UserId = 7), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await CreateService().RegisterAsync(ValidRequest("0987654321"), null);

        await _userRepository.Received(1).FindRoleByCodeAsync(RoleCodes.Patient, Arg.Any<CancellationToken>());
        Assert.DoesNotContain(typeof(RegisterRequest).GetProperties(), property => property.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterAsync_SaveFailureAfterAddingPatientPropagatesInsideTransactionWithoutAuditOrSuccess()
    {
        var pendingUsers = new List<User>();
        var pendingPatients = new List<Patient>();
        var persistedUsers = new List<User>();
        var persistedPatients = new List<Patient>();
        _userRepository.PhoneExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.FindRoleByCodeAsync(RoleCodes.Patient, Arg.Any<CancellationToken>())
            .Returns(new Role { RoleId = 5, RoleCode = RoleCodes.Patient, RoleName = "Bệnh nhân" });
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hash");
        _userRepository.AddAsync(Arg.Do<User>(pendingUsers.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _patientRepository.AddAsync(Arg.Do<Patient>(pendingPatients.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1), Task.FromException<int>(new InvalidOperationException("Không thể lưu hồ sơ.")));
        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<RegisterResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                try
                {
                    var result = await call.Arg<Func<CancellationToken, Task<Result<RegisterResponse>>>>()(call.Arg<CancellationToken>());
                    persistedUsers.AddRange(pendingUsers);
                    persistedPatients.AddRange(pendingPatients);
                    return result;
                }
                catch
                {
                    pendingUsers.Clear();
                    pendingPatients.Clear();
                    throw;
                }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().RegisterAsync(ValidRequest(), null));

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<RegisterResponse>>>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _patientRepository.Received(1).AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>());
        Assert.Empty(pendingUsers);
        Assert.Empty(pendingPatients);
        Assert.Empty(persistedUsers);
        Assert.Empty(persistedPatients);
        await _auditLogger.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
