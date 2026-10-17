using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_RX;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/public/prescriptions/{code}")]
[AllowAnonymous]
[EnableRateLimiting(PublicPrescriptionRateLimits.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicPrescriptionsController(PublicPrescriptionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Summary(string code, CancellationToken ct)
    {
        var r = await service.SummaryAsync(code, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code, r.Error.Message);
    }
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(string code, PublicPrescriptionVerifyRequest request, CancellationToken ct)
    {
        var r = await service.VerifyAsync(code, request, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code, r.Error.Message);
    }
    [HttpGet("details")]
    public async Task<IActionResult> Details(string code, CancellationToken ct)
    {
        var r = await service.DetailsAsync(code, Request.Headers["X-Prescription-View-Token"].ToString(), ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code, r.Error.Message);
    }
    private IActionResult Failure(string code, string message) => StatusCode(code switch {
        "GEN_001" => 404, "GEN_002" => 403, "RX_PUBLIC_LOCKED" => 429, _ => 400 }, new { code, message });
}
