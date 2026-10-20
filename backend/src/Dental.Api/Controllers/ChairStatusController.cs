using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Application.Features.MOD_ASS.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/chair-status")]
[Authorize(Policy = AppPolicies.ChairStatusManage)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ChairStatusController(ChairStatusService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] ChairStatusQuery query, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var result = await service.GetPageAsync(id, actor.RoleCode, query, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpPut("{id:int}")]
    [RequestSizeLimit(4096)]
    [ChairStatusBodyLimit]
    public async Task<IActionResult> Change(int id, [FromBody] ChangeChairStatusRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int actorId) return Unauthorized();
        var result = await service.ChangeAsync(id, actorId, actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    { "GEN_001" => 404, "GEN_002" => 403, "ASS_061" or "ASS_062" or "ASS_063" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}

// Keep oversize errors local to this flow; the global exception handler maps exceptions to500.
public sealed class ChairStatusBodyLimitAttribute : Attribute, IAsyncResourceFilter
{
    private static object ErrorBody => new { code = "ASS_064", message = "Yêu cầu đổi trạng thái vượt dung lượng cho phép." };
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > 4096)
        {
            Headers(context.HttpContext);
            context.Result = new ObjectResult(ErrorBody) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            return;
        }
        try
        {
            var executed = await next();
            if (executed.Exception is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } && !context.HttpContext.Response.HasStarted)
            {
                executed.ExceptionHandled = true;
                await TooLarge(context.HttpContext);
            }
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.HttpContext.Response.HasStarted)
        { await TooLarge(context.HttpContext); }
    }
    private static Task TooLarge(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        Headers(context);
        return context.Response.WriteAsJsonAsync(ErrorBody, context.RequestAborted);
    }
    private static void Headers(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Protocol is "HTTP/1.0" or "HTTP/1.1") context.Response.Headers.Connection = "close";
    }
}
