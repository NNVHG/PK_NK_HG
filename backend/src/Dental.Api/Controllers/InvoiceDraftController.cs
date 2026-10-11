using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Authorize]
public sealed class InvoiceDraftController(InvoiceDraftService service, IInvoiceRepository invoices,
    QueueService queue, ICurrentUser user, InvoiceVisitRequestValidator validator) : ControllerBase
{
    [HttpGet("api/visits/{visitId:int}/invoice-draft")]
    [Authorize(Policy = AppPolicies.InvoiceDraftView)]
    public async Task<IActionResult> Get(int visitId, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        if (!(await validator.ValidateAsync(new InvoiceVisitRequest(visitId), ct)).IsValid) return BadRequest(new { message = "Mã lần khám không hợp lệ." });
        var result = await service.GetAsync(visitId, userId, user.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("api/visits/{visitId:int}/complete")]
    [Authorize(Policy = AppPolicies.FdiServiceAssign)]
    public async Task<IActionResult> Complete(int visitId, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        if (!(await validator.ValidateAsync(new InvoiceVisitRequest(visitId), ct)).IsValid) return BadRequest(new { message = "Mã lần khám không hợp lệ." });
        var queueId = await invoices.GetQueueIdAsync(visitId, ct);
        if (queueId is null) return NotFound(new { message = "Không tìm thấy lượt khám trong hàng đợi." });
        var result = await queue.UpdateQueueStatusAsync(queueId.Value, userId, user.RoleCode!,
            new UpdateQueueStatusRequest(3), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    {
        "GEN_001" => 404, "GEN_002" => 403, _ => 409
    }, new { code = error.Code, message = error.Message });
}
