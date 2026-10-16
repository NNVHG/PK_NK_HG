using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.InvoiceDiscountWrite)]
public sealed class InvoiceDiscountsController(InvoiceDiscountService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("api/invoices/discount-presets")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Presets(CancellationToken ct)
    {
        if (user.UserId is null) return Unauthorized();
        var result = await service.GetPresetsAsync(user.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("api/invoices/discount-presets")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public Task<IActionResult> Create(DiscountPresetRequest request, CancellationToken ct) => Save(null, request, ct);

    [HttpPut("api/invoices/discount-presets/{id:int}")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public Task<IActionResult> Update(int id, DiscountPresetRequest request, CancellationToken ct) => Save(id, request, ct);

    [HttpPost("api/invoices/{invoiceId:int}/discounts")]
    public async Task<IActionResult> Apply(int invoiceId, ApplyInvoiceDiscountRequest request, CancellationToken ct)
    {
        if (user.UserId is not int actorId) return Unauthorized();
        var result = await service.ApplyAsync(invoiceId, request, actorId, user.RoleCode, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private async Task<IActionResult> Save(int? id, DiscountPresetRequest request, CancellationToken ct)
    {
        if (user.UserId is not int actorId) return Unauthorized();
        var result = await service.SavePresetAsync(id, request, actorId, user.RoleCode, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    {
        "GEN_001" => 404, "GEN_002" or "BIL_023" => 403,
        "GEN_003" or "BIL_025" => 400, _ => 409
    }, new { code = error.Code, message = error.Message });
}
