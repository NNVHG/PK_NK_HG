using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Application.Interfaces;
using Dental.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AppPolicies=Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController]
[Route("api/appointment-requests")]
[Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class BookingRequestsController(BookingRequestService service,ICurrentUser actor):ControllerBase
{
    [HttpGet("catalog")][AllowAnonymous][EnableRateLimiting(BookingRequestRateLimits.Read)]
    public async Task<IActionResult> Catalog(CancellationToken ct)=>Ok(await service.CatalogAsync(ct));
    [HttpPost][AllowAnonymous][EnableRateLimiting(BookingRequestRateLimits.Submit)]
    public async Task<IActionResult> Submit(SubmitBookingRequest request,CancellationToken ct)
    {
        var r=await service.SubmitAsync(request,actor.UserId,actor.RoleCode,HttpContext.Connection.RemoteIpAddress?.ToString(),ct);return r.IsSuccess?Accepted(r.Value):Failure(r.Error);
    }
    [HttpGet][Authorize(Policy=AppPolicies.AppointmentRequestReview)]
    public async Task<IActionResult> List([FromQuery]int page=1,CancellationToken ct=default)
    {
        if(actor.UserId is not int id)return Unauthorized();var r=await service.ListAsync(id,actor.RoleCode,page,ct);return r.IsSuccess?Ok(r.Value):Failure(r.Error);
    }
    [HttpPost("{id:int}/reject")][Authorize(Policy=AppPolicies.AppointmentRequestReview)]
    public async Task<IActionResult> Reject(int id,RejectBookingRequest request,CancellationToken ct)
    {
        if(actor.UserId is not int user)return Unauthorized();var r=await service.RejectAsync(id,user,actor.RoleCode,request,HttpContext.Connection.RemoteIpAddress?.ToString(),ct);return r.IsSuccess?NoContent():Failure(r.Error);
    }
    private IActionResult Failure(Error e)=>StatusCode(e.Code switch{"GEN_001"=>404,"GEN_002"=>403,"APP_REQUEST_CONFLICT" or "APP_REQUEST_HELD"=>409,"APP_REQUEST_RATE"=>429,"APP_REQUEST_UNAVAILABLE"=>503,_=>400},new{code=e.Code,message=e.Message});
}
