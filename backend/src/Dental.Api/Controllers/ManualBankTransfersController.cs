using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/invoices/{invoiceId:int}/bank-transfer/confirm")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.CashPaymentWrite)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ManualBankTransfersController(ManualBankTransferService service, ICurrentUser user) : ControllerBase
{
    [HttpPost, RequestSizeLimit(8192)]
    public async Task<IActionResult> Confirm(int invoiceId, ManualBankTransferRequest request, CancellationToken ct)
    {
        if (user.UserId is not int actor) return Unauthorized();
        var result = await service.ConfirmAsync(invoiceId, request, actor, user.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
        {
            "GEN_001" => 404, "GEN_002" => 403, "GEN_003" or "BIL_032" => 400, _ => 409
        }, new { code = result.Error.Code, message = result.Error.Message });
    }
}
