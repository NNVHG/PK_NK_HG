using Dental.Application.Features.MOD_RPT.DTOs;
using Dental.Application.Features.MOD_RPT.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AppPolicies.AdminOnly)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(RevenueReportService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] RevenueReportRequest request, CancellationToken ct)
    {
        var result = await service.GetAsync(actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpGet("revenue/export")]
    public async Task<IActionResult> Export([FromQuery] RevenueReportRequest request, CancellationToken ct)
    {
        var result = await service.GetAsync(actor.RoleCode, request, ct);
        return result.IsSuccess ? File(RevenueReportCsv.Create(result.Value!), "text/csv; charset=utf-8", RevenueReportCsv.FileName(result.Value!)) : Failure(result.Error);
    }
    private IActionResult Failure(Dental.Application.Common.Error error) => StatusCode(error.Code == "GEN_002" ? 403 : 400, new { code = error.Code, message = error.Message });
}
