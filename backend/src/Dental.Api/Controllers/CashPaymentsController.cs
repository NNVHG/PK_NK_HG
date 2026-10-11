using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/invoices/{invoiceId:int}/payments")]
[Authorize(Policy = AppPolicies.CashPaymentWrite)]
public sealed class CashPaymentsController(CashPaymentService service, ICashPaymentRepository repository, ICurrentUser user) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(int invoiceId, CashPaymentRequest request, CancellationToken ct)
    {
        if (user.UserId is not int cashierId) return Unauthorized();
        var result = await service.ReceiveAsync(invoiceId, cashierId, user.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.IsSuccess) return Ok(result.Value);
        return StatusCode(result.Error.Code switch { "GEN_001" => 404, "GEN_002" => 403,
            "GEN_003" or "BIL_032" => 400, _ => 409 }, new { code = result.Error.Code, message = result.Error.Message });
    }

    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> History(int invoiceId, CancellationToken ct)
    {
        if (user.UserId is not int) return Unauthorized();
        if (invoiceId <= 0) return BadRequest();
        return Ok((await repository.GetAsync(invoiceId, ct)).Select(CashPaymentService.Map));
    }
}
