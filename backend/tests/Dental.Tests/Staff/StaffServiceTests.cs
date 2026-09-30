using Dental.Application.Common;
using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Features.Staff.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Staff;

public sealed class StaffServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private StaffService CreateService()
        => new(_userRepository, _unitOfWork, _passwordHasher, _auditLogger);

    private static Role MakeRole(string roleCode = RoleCodes.Dentist, int roleId = 2)
        => new()
        {
            RoleId = roleId,
            RoleCode = roleCode,
            RoleName = roleCode == RoleCodes.Admin ? "Quản trị viên" : "Nha sĩ",
        };

    private static User MakeUser(int userId = 22, string roleCode = RoleCodes.Dentist, bool isActive = true)
    {
        var role = MakeRole(roleCode, roleCode == RoleCodes.Admin ? 1 : 2);
        return new User
        {
            UserId = userId,
            Phone = "0912345678",
            PasswordHash = "stored_hash",
            FullName = "Nhân viên thử nghiệm",
            Role = role,
            RoleId = role.RoleId,
            IsActive = isActive,
        };
    }

    [Fact]
    public async Task CreateStaffAsync_ValidRequest_CreatesHashedAccountAndAuditsSafeDetails()
    {
        var role = MakeRole();
        _userRepository.PhoneExistsAsync("0912345678", null, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.FindRoleByCodeAsync(RoleCodes.Dentist, Arg.Any<CancellationToken>()).Returns(role);
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<User>().UserId = 22;
            return Task.CompletedTask;
        });
        _passwordHasher.Hash("InitialPass1").Returns("hashed_initial_password");

        var result = await CreateService().CreateStaffAsync(
            actorUserId: 7,
            new CreateStaffRequest(" Nha sĩ A ", "0912345678", "InitialPass1", RoleCodes.Dentist, "a@example.com", null, null),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(22, result.Value!.UserId);
        Assert.Equal("Nha sĩ A", result.Value.FullName);
        Assert.Equal(RoleCodes.Dentist, result.Value.RoleCode);
        Assert.True(result.Value.IsActive);
        _passwordHasher.Received(1).Hash("InitialPass1");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Create,
            entityType: "User",
            entityId: 22,
            userId: 7,
            detail: Arg.Is<string?>(detail =>
                detail != null &&
                detail.Contains("actorUserId") && detail.Contains("targetUserId") &&
                detail.Contains("roleAfter") && detail.Contains(RoleCodes.Dentist) &&
                detail.Contains("isActiveAfter") && !detail.Contains("InitialPass1") && !detail.Contains("hashed_initial_password")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateStaffAsync_DuplicatePhone_ReturnsConflictWithoutSaving()
    {
        _userRepository.PhoneExistsAsync("0912345678", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateStaffAsync(
            7,
            new CreateStaffRequest("Nha sĩ A", "0912345678", "InitialPass1", RoleCodes.Dentist, null, null, null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.StaffPhoneExists, result.Error);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _passwordHasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task CreateStaffAsync_PatientRole_ReturnsInvalidRole()
    {
        var result = await CreateService().CreateStaffAsync(
            7,
            new CreateStaffRequest("Bệnh nhân", "0912345678", "InitialPass1", RoleCodes.Patient, null, null, null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.InvalidStaffRole, result.Error);
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockStaffAsync_AdminCannotLockOwnAccount()
    {
        var admin = MakeUser(userId: 7, roleCode: RoleCodes.Admin);
        _userRepository.FindByIdAsync(7, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await CreateService().LockStaffAsync(7, 7, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.CannotLockSelf, result.Error);
        Assert.True(admin.IsActive);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockStaffAsync_CannotLockLastActiveAdmin()
    {
        var admin = MakeUser(userId: 22, roleCode: RoleCodes.Admin);
        _userRepository.FindByIdAsync(22, Arg.Any<CancellationToken>()).Returns(admin);
        _userRepository.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateService().LockStaffAsync(7, 22, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.LastActiveAdmin, result.Error);
        Assert.True(admin.IsActive);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStaffAsync_CannotDemoteLastActiveAdmin()
    {
        var admin = MakeUser(userId: 22, roleCode: RoleCodes.Admin);
        _userRepository.FindByIdAsync(22, Arg.Any<CancellationToken>()).Returns(admin);
        _userRepository.PhoneExistsAsync("0912345678", 22, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.FindRoleByCodeAsync(RoleCodes.Dentist, Arg.Any<CancellationToken>()).Returns(MakeRole());
        _userRepository.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateService().UpdateStaffAsync(
            7,
            22,
            new UpdateStaffRequest("Nhân viên", "0912345678", null, null, null, RoleCodes.Dentist),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.LastActiveAdmin, result.Error);
        Assert.Equal(RoleCodes.Admin, admin.Role.RoleCode);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlockStaffAsync_UpdatesStatusAndAuditsBeforeAndAfterValues()
    {
        var staff = MakeUser(isActive: false);
        _userRepository.FindByIdAsync(22, Arg.Any<CancellationToken>()).Returns(staff);

        var result = await CreateService().UnlockStaffAsync(7, 22, null);

        Assert.True(result.IsSuccess);
        Assert.True(staff.IsActive);
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Update,
            entityType: "User",
            entityId: 22,
            userId: 7,
            detail: Arg.Is<string?>(detail =>
                detail != null && detail.Contains("unlock_staff") &&
                detail.Contains("roleBefore") && detail.Contains("roleAfter") &&
                detail.Contains("isActiveBefore") && detail.Contains("isActiveAfter") &&
                detail.Contains("false") && detail.Contains("true")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }
}
