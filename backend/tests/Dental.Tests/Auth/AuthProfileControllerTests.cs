using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuthProfileControllerTests
{
    [Fact]
    public async Task UpdateProfileAsync_UpdatesOnlyAuthenticatedUsersProfile()
    {
        var currentUserRecord = new User
        {
            UserId = 1,
            Phone = "0912345678",
            FullName = "Người dùng hiện tại",
            Role = new Role { RoleCode = "ADMIN", RoleName = "Quản trị viên" },
        };
        var otherUserRecord = new User
        {
            UserId = 2,
            Phone = "0987654321",
            FullName = "Người dùng khác",
            Role = new Role { RoleCode = "DENTIST", RoleName = "Nha sĩ" },
        };

        var userRepository = Substitute.For<IUserRepository>();
        userRepository.FindByIdAsync(1, Arg.Any<CancellationToken>()).Returns(currentUserRecord);
        userRepository.FindByIdAsync(2, Arg.Any<CancellationToken>()).Returns(otherUserRecord);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(1);

        var authService = new AuthService(
            userRepository,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<ITokenService>(),
            Substitute.For<IAuditLogger>());
        var controller = new AuthController(authService, new LoginRequestValidator(), currentUser);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest("Tên đã cập nhật", currentUserRecord.Phone, null, null, null),
            new UpdateProfileRequestValidator(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Tên đã cập nhật", currentUserRecord.FullName);
        Assert.Equal("Người dùng khác", otherUserRecord.FullName);
        await userRepository.Received(1).FindByIdAsync(1, Arg.Any<CancellationToken>());
        await userRepository.DidNotReceive().FindByIdAsync(2, Arg.Any<CancellationToken>());
    }
}
