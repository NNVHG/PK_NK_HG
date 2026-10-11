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
    QueueService queue, ICurrentUser user, InvoiceVisitRequestValidator validator, VisitReopenService reopen,
    IVisitReopenRepository reopenRepository) : ControllerBase
{
    [HttpGet("api/visits/{visitId:int}/invoice-draft")]
    [HttpGet("api/invoices/by-visit/{visitId:int}")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Policy = AppPolicies.InvoiceDraftView)]
    public async Task<IActionResult> Get(int visitId, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        if (!(await validator.ValidateAsync(new InvoiceVisitRequest(visitId), ct)).IsValid) return BadRequest(new { message = "Mã lần khám không hợp lệ." });
        var result = await service.GetAsync(visitId, userId, user.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("api/visits/{visitId:int}/complete")]
    [HttpPost("api/visits/{visitId:int}/lock")]
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

    [HttpPost("api/visits/{visitId:int}/unlock")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Unlock(int visitId, UnlockVisitRequest request, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await reopen.ReopenAsync(visitId, userId, user.RoleCode, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : NoContent();
    }

    [HttpGet("api/visits/{visitId:int}/unlock-history")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> UnlockHistory(int visitId, CancellationToken ct)
    {
        if (user.UserId is not int) return Unauthorized();
        if (!(await validator.ValidateAsync(new InvoiceVisitRequest(visitId), ct)).IsValid) return BadRequest();
        var rows = await reopenRepository.GetHistoryAsync(visitId, ct);
        return Ok(rows.Select(x => new { x.Id, x.ActorUserId, x.CancelledInvoiceId, x.Reason, x.CreatedAt }));
    }

    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    {
        "GEN_001" => 404, "GEN_002" => 403, "GEN_003" => 400, _ => 409
    }, new { code = error.Code, message = error.Message });
}
