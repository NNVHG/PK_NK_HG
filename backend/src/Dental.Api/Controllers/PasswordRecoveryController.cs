using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/auth/password-recovery")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordRecoveryController(PasswordRecoveryService service) : ControllerBase
{
    [HttpPost("request")]
    [AllowAnonymous] // Q-326a/DL-305: recovering access cannot require an existing account session.
    [EnableRateLimiting(AuthRateLimitPolicies.RecoveryRequest)]
    public async Task<IActionResult> RequestCode(RecoveryRequest request, CancellationToken ct)
    {
        var result = await service.RequestAsync(request, ct);
        return result.IsSuccess ? Accepted(result.Value) : Failure(result.Error);
    }

    [HttpPost("reset")]
    [AllowAnonymous] // Possession of the single-use SMS OTP authorizes this operation only.
    [EnableRateLimiting(AuthRateLimitPolicies.RecoveryReset)]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await service.ResetAsync(request, ct);
        return result.IsSuccess ? Ok(new { message = "Đã đặt mật khẩu mới. Vui lòng đăng nhập lại." }) : Failure(result.Error);
    }
    private IActionResult Failure(Dental.Application.Common.Error error) => StatusCode(error.Code switch
    { "AUTH_060" => 503, "AUTH_062" => 429, _ => 400 }, new { code = error.Code, message = error.Message });
}
