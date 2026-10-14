using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/invoices/lookup")]
[Authorize(Policy = AppPolicies.CashPaymentWrite)]
public sealed class InvoiceLookupController(InvoiceLookupService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Get([FromQuery] InvoiceLookupRequest request, CancellationToken ct)
    {
        if (user.UserId is null) return Unauthorized();
        var result = await service.GetAsync(request, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
        { "GEN_001" => 404, "GEN_002" => 403, _ => 400 }, new { code = result.Error.Code, message = result.Error.Message });
    }
}
