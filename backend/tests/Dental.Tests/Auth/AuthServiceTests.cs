using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuthServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private AuthService CreateService() =>
        new(_userRepo, _unitOfWork, _passwordHasher, _tokenService, _auditLogger);


    private static User MakeUser(bool isActive = true) => new()
    {
        UserId       = 1,
        Phone        = "0912345678",
        PasswordHash = "hashed",
        FullName     = "Test User",
        IsActive     = isActive,
        Role         = new Role { RoleCode = RoleCodes.Admin, RoleName = "Quản trị viên" },
    };

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsSuccess()
    {
        // Arrange
        var user = MakeUser();
        _userRepo.FindByPhoneAsync("0912345678").Returns(user);
        _passwordHasher.Verify("correct", "hashed").Returns(true);
        _tokenService.GenerateAccessToken(user).Returns("jwt_token");

        var svc = CreateService();

        // Act
        var result = await svc.LoginAsync(new LoginRequest("0912345678", "correct"), ipAddress: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("jwt_token", result.Value!.AccessToken);
        Assert.Equal(RoleCodes.Admin, result.Value.RoleCode);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = MakeUser();
        _userRepo.FindByPhoneAsync("0912345678").Returns(user);
        _passwordHasher.Verify("wrong", "hashed").Returns(false);

        var svc = CreateService();
        var result = await svc.LoginAsync(new LoginRequest("0912345678", "wrong"), ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_001", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_PhoneNotFound_ReturnsInvalidCredentials()
    {
        _userRepo.FindByPhoneAsync(Arg.Any<string>()).Returns((User?)null);

        var svc = CreateService();
        var result = await svc.LoginAsync(new LoginRequest("0999999999", "any"), ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_001", result.Error.Code); // cùng lỗi chung — chống dò tài khoản
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ReturnsAccountLocked()
    {
        // Mật khẩu đúng nhưng IsActive = false → AUTH_002
        var user = MakeUser(isActive: false);
        _userRepo.FindByPhoneAsync("0912345678").Returns(user);
        _passwordHasher.Verify("correct", "hashed").Returns(true);

        var svc = CreateService();
        var result = await svc.LoginAsync(new LoginRequest("0912345678", "correct"), ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_002", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_WritesAuditLog()
    {
        var user = MakeUser();
        _userRepo.FindByPhoneAsync("0912345678").Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var svc = CreateService();
        await svc.LoginAsync(new LoginRequest("0912345678", "wrong"), ipAddress: "127.0.0.1");

        // Kiểm tra AuditLogger được gọi với action LOGIN_FAILED
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.LoginFailed,
            entityType: Arg.Any<string>(),
            entityId: Arg.Any<int?>(),
            userId: Arg.Any<int?>(),
            detail: Arg.Any<string?>(),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_AuditLogNotContainPassword()
    {
        var user = MakeUser();
        _userRepo.FindByPhoneAsync("0912345678").Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _tokenService.GenerateAccessToken(user).Returns("tok");

        var svc = CreateService();
        await svc.LoginAsync(new LoginRequest("0912345678", "myPassword"), ipAddress: null);

        // Detail trong AuditLog không được chứa password
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Login,
            entityType: Arg.Any<string>(),
            entityId: Arg.Any<int?>(),
            userId: Arg.Any<int?>(),
            detail: Arg.Is<string?>(d => d == null || !d.Contains("myPassword")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task ChangePasswordAsync_ValidOldPassword_UpdatesHashAndSaves()
    {
        var user = MakeUser();
        _userRepo.FindByIdAsync(1).Returns(user);
        _passwordHasher.Verify("oldPass", "hashed").Returns(true);
        _passwordHasher.Verify("newPass123", "hashed").Returns(false);
        _passwordHasher.Hash("newPass123").Returns("newHashed");

        var svc = CreateService();
        var result = await svc.ChangePasswordAsync(1, new ChangePasswordRequest("oldPass", "newPass123"), ipAddress: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("newHashed", user.PasswordHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongOldPassword_ReturnsError()
    {
        var user = MakeUser();
        _userRepo.FindByIdAsync(1).Returns(user);
        _passwordHasher.Verify("wrongOldPass", "hashed").Returns(false);

        var svc = CreateService();
        var result = await svc.ChangePasswordAsync(1, new ChangePasswordRequest("wrongOldPass", "newPass123"), ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_004", result.Error.Code);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_SameNewPassword_ReturnsError()
    {
        var user = MakeUser();
        _userRepo.FindByIdAsync(1).Returns(user);
        _passwordHasher.Verify("samePass", "hashed").Returns(true);

        var svc = CreateService();
        var result = await svc.ChangePasswordAsync(1, new ChangePasswordRequest("samePass", "samePass"), ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_005", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAsync_ValidUser_WritesLogoutAuditLog()
    {
        var user = MakeUser();
        _userRepo.FindByIdAsync(1).Returns(user);

        var svc = CreateService();
        var result = await svc.LogoutAsync(1, ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Logout,
            entityType: "User",
            entityId: 1,
            userId: 1,
            detail: Arg.Any<string?>(),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }
}
