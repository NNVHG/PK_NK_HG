using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize] // Mọi endpoint mặc định cần đăng nhập (AGENTS.md mục 6)
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly LoginRequestValidator _validator;
    private readonly ICurrentUser _currentUser;

    public AuthController(
        AuthService authService,
        LoginRequestValidator validator,
        ICurrentUser currentUser)
    {
        _authService = authService;
        _validator = validator;
        _currentUser = currentUser;
    }

    /// <summary>Đăng nhập bằng SĐT + mật khẩu. Trả về JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // Validate đầu vào
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                code = "GEN_003",
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = validationResult.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ipAddress, ct);

        if (result.IsFailure)
        {
            // AUTH_002 = tài khoản bị khóa → 403; các lỗi khác → 401
            var statusCode = result.Error.Code == "AUTH_002"
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;

            return StatusCode(statusCode, new { code = result.Error.Code, message = result.Error.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>Lấy thông tin người dùng đang đăng nhập.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized(new { code = "AUTH_003", message = "Bạn chưa đăng nhập." });

        var result = await _authService.GetMeAsync(userId.Value, ct);

        if (result.IsFailure)
            return NotFound(new { code = result.Error.Code, message = result.Error.Message });

        return Ok(result.Value);
    }

    /// <summary>Đổi mật khẩu người dùng hiện tại.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] ChangePasswordRequestValidator changePasswordValidator,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized(new { code = "AUTH_003", message = "Bạn chưa đăng nhập." });

        var validationResult = await changePasswordValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                code = "GEN_003",
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = validationResult.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.ChangePasswordAsync(userId.Value, request, ipAddress, ct);

        if (result.IsFailure)
        {
            return BadRequest(new { code = result.Error.Code, message = result.Error.Message });
        }

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    /// <summary>Đăng xuất phiên làm việc hiện tại.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized(new { code = "AUTH_003", message = "Bạn chưa đăng nhập." });

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authService.LogoutAsync(userId.Value, ipAddress, ct);

        return Ok(new { message = "Đăng xuất thành công." });
    }

}
