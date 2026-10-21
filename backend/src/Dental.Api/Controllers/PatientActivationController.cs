using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/auth/activation")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class PatientActivationController(PatientActivationService service) : ControllerBase
{
    [HttpPost("request")][AllowAnonymous][EnableRateLimiting(PatientActivationRateLimits.Request)]
    public async Task<IActionResult> RequestCode(ActivationRequest request, CancellationToken ct)
    { var r = await service.RequestAsync(request, ct); return r.IsSuccess ? Accepted(r.Value) : Failure(r.Error); }
    [HttpPost("verify")][AllowAnonymous][EnableRateLimiting(PatientActivationRateLimits.Verify)]
    public async Task<IActionResult> Verify(ActivationVerification request, CancellationToken ct)
    { var r = await service.VerifyAsync(request, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPost("complete")][AllowAnonymous][EnableRateLimiting(PatientActivationRateLimits.Complete)]
    public async Task<IActionResult> Complete(ActivationCompletion request, CancellationToken ct)
    { var r = await service.CompleteAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return r.IsSuccess ? Ok(new { completed = true }) : Failure(r.Error); }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch {
        "AUTH_ACTIVATION_RATE" => 429, "AUTH_ACTIVATION_UNAVAILABLE" => 503, _ => 400
    }, new { code = error.Code, message = error.Message });
}
