using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Dental.Api.RateLimiting;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController]
[Route("api/dental-images")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DentalImagesController(DentalImageService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("{id:int}/file")]
    [Authorize(Policy = AppPolicies.DentalImageRead)]
    public async Task<IActionResult> ImageFile(int id, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var result = await service.FileAsync(id, user, actor.RoleCode!, ct);
        if (result.IsFailure) return Failure(result.Error);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = "inline";
        Response.RegisterForDispose(result.Value!.Content);
        return File(result.Value.Content, result.Value.ContentType);
    }
    public sealed class UploadForm
    {
        public int PatientId { get; set; }
        public int? VisitId { get; set; }
        public int? ToothNumber { get; set; }
        public string Category { get; set; } = "Intraoral";
        public string? Notes { get; set; }
        public IFormFile? File { get; set; }
    }
    [HttpGet]
    [Authorize(Policy = AppPolicies.DentalImageRead)]
    public async Task<IActionResult> List([FromQuery] int patientId, [FromQuery] int? visitId, [FromQuery] int? toothNumber, [FromQuery] int beforeId, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.ListAsync(patientId, visitId, toothNumber, beforeId, user, actor.RoleCode!, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }
    [HttpGet("targets")]
    [Authorize(Policy = AppPolicies.DentalImageUpload)]
    public async Task<IActionResult> Targets([FromQuery] int patientId, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.TargetsAsync(patientId, user, actor.RoleCode!, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }
    [HttpPost("upload")]
    [EnableRateLimiting(DentalImageRateLimits.Upload)]
    [Authorize(Policy = AppPolicies.DentalImageUpload)]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = DentalImageFile.MaxBytes, MemoryBufferThreshold = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        if (form.File is not { Length: >= 12 and <= DentalImageFile.MaxBytes } file) return Failure(Error.Validation);
        await using var stream = file.OpenReadStream();
        var prefix = new byte[12]; await stream.ReadExactlyAsync(prefix, ct);
        if (!DentalImageFile.Magic(prefix, file.ContentType)) return Failure(Error.Validation);
        // Q-255: reject invalid magic in memory before creating any application storage file.
        stream.Seek(0, SeekOrigin.Begin);
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, ct);
        if (memory.Length != file.Length) return Failure(Error.Validation);
        var r = await service.UploadAsync(new(form.PatientId, form.VisitId, form.ToothNumber, form.Category, form.Notes), file.FileName,
            file.ContentType, memory.ToArray(), user, actor.RoleCode!, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? StatusCode(201, r.Value) : Failure(r.Error);
    }
    [HttpPost("{id:int}/attach")]
    [Authorize(Policy = AppPolicies.DentalImageManage)]
    public async Task<IActionResult> Attach(int id, AttachDentalImageRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.AttachAsync(id, request, user, actor.RoleCode!, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "GEN_004" => 409, "GEN_500" => 500, _ => 400 }, new { code = error.Code, message = error.Message });
}
