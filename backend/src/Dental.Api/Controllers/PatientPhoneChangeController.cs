using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/auth/patient-phone-change")]
[Authorize(Policy = AppPolicies.PatientPhoneChange)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class PatientPhoneChangeController(PatientPhoneChangeService service, ICurrentUser current) : ControllerBase
{
    [HttpPost("preview")]
    [EnableRateLimiting(PatientPhoneChangeRateLimits.Read)]
    public async Task<IActionResult> Preview(PatientPhonePreviewRequest request, CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.PreviewAsync(userId, request, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }
    [HttpPost("request")]
    [EnableRateLimiting(PatientPhoneChangeRateLimits.Request)]
    public async Task<IActionResult> RequestChange(PatientPhoneRequest request, CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.RequestAsync(userId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Accepted(result.Value);
    }
    [HttpPost("confirm")]
    [EnableRateLimiting(PatientPhoneChangeRateLimits.Confirm)]
    public async Task<IActionResult> Confirm(PatientPhoneConfirmation request, CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.ConfirmAsync(userId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch {
        "PAT_PHONE_SMS_UNAVAILABLE" => 503, "PAT_PHONE_RATE" => 429,
        "PAT_PHONE_STALE" or "PAT_PHONE_NUMBER" => 409,
        var c when c == Error.Forbidden.Code => 403,
        var c when c == Error.Conflict.Code => 409,
        _ => 400
    }, new { code = error.Code, message = error.Message });
}
