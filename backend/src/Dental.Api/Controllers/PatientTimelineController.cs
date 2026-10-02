using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/timeline")]
[Authorize(Policy = AppPolicies.PatientView)]
public sealed class PatientTimelineController : ControllerBase
{
    private readonly PatientTimelineService _service;
    private readonly ICurrentUser _currentUser;
    private readonly PatientTimelineQueryRequestValidator _validator;

    public PatientTimelineController(
        PatientTimelineService service,
        ICurrentUser currentUser,
        PatientTimelineQueryRequestValidator validator)
    {
        _service = service;
        _currentUser = currentUser;
        _validator = validator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PatientTimelineItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int patientId,
        [FromQuery] PatientTimelineQueryRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new
            {
                code = Error.Validation.Code,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
            });

        var result = await _service.GetAsync(
            patientId, currentUserId, _currentUser.RoleCode, request, ct);
        return result.IsFailure
            ? StatusCode(StatusCodes.Status404NotFound, new { code = result.Error.Code, message = result.Error.Message })
            : Ok(result.Value);
    }
}
