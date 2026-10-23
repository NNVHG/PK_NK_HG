using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/portal/invoices")]
[Authorize(Policy = AppPolicies.InvoicePortalRead)]
public sealed class InvoicePortalController(InvoicePortalService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("profiles")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Profiles(CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.ProfilesAsync(userId, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(403, new { code = result.Error.Code, message = result.Error.Message });
    }
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> List([FromQuery] InvoicePortalListQuery request, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.ListAsync(request, userId, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code == "GEN_003" ? 400 : 403, new { code = result.Error.Code, message = result.Error.Message });
    }
    [HttpGet("{invoiceId:int}/payments")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Payments(int invoiceId, [FromQuery] InvoicePortalQuery request, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.PaymentsAsync(invoiceId, request, userId, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code == "GEN_003" ? 400 : 403, new { code = result.Error.Code, message = result.Error.Message });
    }
    [HttpGet("{invoiceId:int}/items")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Items(int invoiceId, [FromQuery] InvoicePortalQuery request, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.ItemsAsync(invoiceId, request, userId, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code == "GEN_003" ? 400 : 403, new { code = result.Error.Code, message = result.Error.Message });
    }
}
