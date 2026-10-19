using Dental.Application.Common;
using Dental.Application.Features.MOD_RX;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController, Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MedicineStockController(MedicineStockService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("api/medicine-stock/{id:int}"), Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Read(int id, [FromQuery] int beforeId = 0, CancellationToken ct = default)
    {
        if (actor.UserId is not int userId) return Unauthorized();
        return Respond(await service.ReadAsync(id, beforeId, userId, actor.RoleCode, ct));
    }
    [HttpPost("api/medicine-stock/{id:int}/receipts"), Authorize(Policy = AppPolicies.AdminOnly), RequestSizeLimit(8192), MedicineStockBodyLimit]
    public async Task<IActionResult> Receive(int id, MedicineStockReceiptRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int userId) return Unauthorized();
        var result = await service.ReceiveAsync(id, request, userId, actor.RoleCode, ct);
        return result.IsSuccess ? StatusCode(result.Value!.Replayed ? 200 : 201, result.Value) : Respond(result);
    }
    [HttpGet("api/visits/{visitId:int}/prescription/stock"), Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public async Task<IActionResult> Availability(int visitId, [FromQuery] int[] medicineIds, CancellationToken ct)
    {
        if (actor.UserId is not int userId) return Unauthorized();
        return Respond(await service.AvailabilityAsync(visitId, medicineIds, userId, actor.RoleCode, ct));
    }
    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
    { "GEN_001" => 404, "GEN_002" => 403, "RX_LOCKED" or "MED_STOCK_CONFLICT" or "MED_STOCK_STALE" or "MED_STOCK_LIMIT" => 409, _ => 400 }, new { code = result.Error.Code, message = result.Error.Message });
}

public sealed class MedicineStockBodyLimitAttribute : Attribute, IAsyncResourceFilter
{
    private static object Body => new { code = "MED_STOCK_BODY_LIMIT", message = "Phiếu nhập thuốc vượt dung lượng cho phép." };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > 8192)
        {
            Headers(context.HttpContext); context.Result = new ObjectResult(Body) { StatusCode = 413 }; return;
        }
        try
        {
            var executed = await next();
            if (executed.Exception is BadHttpRequestException { StatusCode: 413 } && !context.HttpContext.Response.HasStarted)
            { executed.ExceptionHandled = true; await Reject(context.HttpContext); }
        }
        catch (BadHttpRequestException e) when (e.StatusCode == 413 && !context.HttpContext.Response.HasStarted) { await Reject(context.HttpContext); }
    }
    private static void Headers(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Protocol is "HTTP/1.0" or "HTTP/1.1") context.Response.Headers.Connection = "close";
    }
    private static Task Reject(HttpContext context)
    { context.Response.StatusCode = 413; Headers(context); return context.Response.WriteAsJsonAsync(Body, context.RequestAborted); }
}
