using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Api.RateLimiting;
using AppPolicies = Dental.Api.Policies.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Dental.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class CounterPatientPhoneChangeController(CounterPatientPhoneService service, ICurrentUser current) : ControllerBase
{
    [HttpPost("api/appointments/{id:int}/patient-phone-change")]
    [Authorize(Policy = AppPolicies.AppointmentManage)]
    [EnableRateLimiting(PatientPhoneChangeRateLimits.Request)]
    public async Task<IActionResult> Create(int id, CounterPhoneRequest request, CancellationToken ct)
    {
        if (current.UserId is not int actorId) return Unauthorized();
        var result = await service.CreateAsync(actorId, id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Accepted(result.Value);
    }
    [HttpGet("api/auth/patient-phone-change/counter-request")]
    [Authorize(Policy = AppPolicies.PatientPhoneChange)]
    [EnableRateLimiting(PatientPhoneChangeRateLimits.Read)]
    public async Task<IActionResult> Own(CancellationToken ct)
    {
        if (current.UserId is not int userId) return Unauthorized();
        var result = await service.GetOwnAsync(userId, ct);
        // An explicit envelope prevents a no-pending result being changed to HTTP204.
        return result.IsFailure ? Failure(result.Error) : Ok(new { request = result.Value });
    }
    private IActionResult Failure(Error e) => StatusCode(e.Code == Error.Forbidden.Code ? 403 :
        e.Code == Error.NotFound.Code ? 404 : e.Code is "PAT_PHONE_NUMBER" or "PAT_PHONE_PENDING" || e.Code == Error.Conflict.Code ? 409 : 400,
        new { code = e.Code, message = e.Message });
}
