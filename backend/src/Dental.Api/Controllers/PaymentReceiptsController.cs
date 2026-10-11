using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/invoices/{invoiceId:int}/payments/{paymentId:int}")]
[Authorize(Policy = AppPolicies.CashPaymentWrite)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PaymentReceiptsController(ReceiptPrintService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("receipt")]
    public Task<IActionResult> Preview(int invoiceId, int paymentId, CancellationToken ct) => Get(invoiceId, paymentId, false, ct);
    [HttpPost("reprint")]
    public Task<IActionResult> Print(int invoiceId, int paymentId, CancellationToken ct) => Get(invoiceId, paymentId, true, ct);
    private async Task<IActionResult> Get(int invoiceId, int paymentId, bool print, CancellationToken ct)
    {
        if (user.UserId is not int actor) return Unauthorized();
        var result = await service.GetAsync(new(invoiceId, paymentId), actor, user.RoleCode, print, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch {
            "GEN_001" => 404, "GEN_002" => 403, "GEN_003" => 400, _ => 409 }, new { code = result.Error.Code, message = result.Error.Message });
    }
}
