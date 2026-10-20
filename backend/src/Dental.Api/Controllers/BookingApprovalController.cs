using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/appointment-requests")]
[Authorize(Policy = AppPolicies.AppointmentRequestReview)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class BookingApprovalController(BookingApprovalService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("{id:int}/approval")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.ReadAsync(id, user, actor.RoleCode, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ApproveBookingRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.ApproveAsync(id, user, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }
    private IActionResult Failure(Error e) => StatusCode(e.Code switch {
        "GEN_001" => 404, "GEN_002" => 403, "APP_APPROVAL_CONFLICT" => 409, _ => 400
    }, new { code = e.Code, message = e.Message });
}
