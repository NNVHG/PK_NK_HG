using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.CashPaymentWrite)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class InvoiceRefundProposalsController(InvoiceRefundProposalService service, ICurrentUser user) : ControllerBase
{
    [HttpPost("invoices/{invoiceId:int}/refund-proposals")]
    public async Task<IActionResult> Create(int invoiceId, InvoiceRefundProposalCreateRequest request, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.CreateAsync(invoiceId, actor, user.RoleCode, request, ct)) : Unauthorized();

    [HttpGet("invoices/{invoiceId:int}/refund-proposals")]
    public async Task<IActionResult> List(int invoiceId, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.ListPendingAsync(actor, user.RoleCode, invoiceId, ct)) : Unauthorized();

    [HttpGet("refund-proposals/{proposalId:int}")]
    public async Task<IActionResult> Read(int proposalId, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.ReadAsync(proposalId, actor, user.RoleCode, ct)) : Unauthorized();

    [HttpPost("refund-proposals/{proposalId:int}/preview")]
    public async Task<IActionResult> Preview(int proposalId, InvoiceRefundProposalPreviewRequest request, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.PreviewAsync(proposalId, actor, user.RoleCode, request, ct)) : Unauthorized();

    [HttpPost("refund-proposals/{proposalId:int}/approve")]
    public async Task<IActionResult> Approve(int proposalId, InvoiceRefundProposalDecisionRequest request, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.ApproveAsync(proposalId, actor, user.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct)) : Unauthorized();

    [HttpPost("refund-proposals/{proposalId:int}/reject")]
    public async Task<IActionResult> Reject(int proposalId, InvoiceRefundProposalRejectRequest request, CancellationToken ct) =>
        user.UserId is int actor ? Respond(await service.RejectAsync(proposalId, actor, user.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct)) : Unauthorized();

    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) :
        StatusCode(result.Error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "GEN_003" => 400, _ => 409 },
            new { code = result.Error.Code, message = result.Error.Message });
}