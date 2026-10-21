using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.Staff;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.StaffAny)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class StaffPhoneChangeController(StaffPhoneChangeService service, ICurrentUser current) : ControllerBase
{
    [HttpPost("api/staff/{id:int}/phone-change")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [EnableRateLimiting(StaffPhoneChangeRateLimits.Request)]
    public async Task<IActionResult> RequestChange(int id, StaffPhoneChangeRequest request, CancellationToken ct)
    {
        if (current.UserId is not int actor) return Unauthorized();
        var result = await service.RequestAsync(actor, id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Accepted(result.Value);
    }
    [HttpGet("api/auth/staff-phone-change")]
    [EnableRateLimiting(StaffPhoneChangeRateLimits.Read)]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.PendingAsync(userId, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(new { pending = result.Value });
    }
    [HttpPost("api/auth/staff-phone-change/confirm")]
    [EnableRateLimiting(StaffPhoneChangeRateLimits.Confirm)]
    public async Task<IActionResult> Confirm(StaffPhoneChangeConfirmation request, CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.ConfirmAsync(userId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(new { completed = true });
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch {
        "STAFF_PHONE_SMS_UNAVAILABLE" => 503, "STAFF_PHONE_OTP_RATE" => 429,
        var c when c == Error.Forbidden.Code => 403,
        var c when c == Error.NotFound.Code => 404,
        var c when c == Error.Conflict.Code || c == Error.StaffPhoneExists.Code => 409,
        _ => 400
    }, new { code = error.Code, message = error.Message });
}
