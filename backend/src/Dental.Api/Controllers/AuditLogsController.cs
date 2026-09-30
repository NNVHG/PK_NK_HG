using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;
    private readonly AuditLogQueryRequestValidator _validator;

    public AuditLogsController(AuditLogService auditLogService, AuditLogQueryRequestValidator validator)
    {
        _auditLogService = auditLogService;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _auditLogService.GetPageAsync(request, ct);
        return Ok(result);
    }

    private IActionResult ToValidationProblem(ValidationResult validation)
        => BadRequest(new
        {
            code = "VALIDATION_ERROR",
            message = "Dữ liệu lọc không hợp lệ.",
            errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
        });
}
