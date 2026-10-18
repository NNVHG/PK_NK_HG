using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies=Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/invoices/{invoiceId:int}/refund")]
[Authorize(Policy=AppPolicies.AdminOnly)]
[ResponseCache(Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class InvoiceRefundsController(InvoiceRefundService service,ICurrentUser user):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Read(int invoiceId,CancellationToken ct)=>user.UserId is int actor?Respond(await service.ReadAsync(invoiceId,actor,user.RoleCode,ct)):Unauthorized();
    [HttpPost("preview")] public async Task<IActionResult> Preview(int invoiceId,InvoiceRefundPreviewRequest request,CancellationToken ct)=>user.UserId is int actor?Respond(await service.PreviewAsync(invoiceId,actor,user.RoleCode,request,ct)):Unauthorized();
    [HttpPost] public async Task<IActionResult> Refund(int invoiceId,InvoiceRefundRequest request,CancellationToken ct)=>user.UserId is int actor?Respond(await service.RefundAsync(invoiceId,actor,user.RoleCode,request,HttpContext.Connection.RemoteIpAddress?.ToString(),ct)):Unauthorized();
    [HttpPost("{refundId:int}/print")] public async Task<IActionResult> Print(int invoiceId,int refundId,CancellationToken ct)=>user.UserId is int actor?Respond(await service.PrintAsync(invoiceId,refundId,actor,user.RoleCode,HttpContext.Connection.RemoteIpAddress?.ToString(),ct)):Unauthorized();
    private IActionResult Respond<T>(Result<T> result)=>result.IsSuccess?Ok(result.Value):StatusCode(result.Error.Code switch{"GEN_001"=>404,"GEN_002"=>403,"GEN_003"=>400,_=>409},new{code=result.Error.Code,message=result.Error.Message});
}
