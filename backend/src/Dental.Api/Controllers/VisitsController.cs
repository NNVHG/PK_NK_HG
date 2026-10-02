using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/visits")]
public sealed class VisitsController : ControllerBase
{
    private readonly VisitService _visitService;
    private readonly ICurrentUser _currentUser;
    private readonly VisitQueryRequestValidator _queryValidator;

    public VisitsController(
        VisitService visitService,
        ICurrentUser currentUser,
        VisitQueryRequestValidator queryValidator)
    {
        _visitService = visitService;
        _currentUser = currentUser;
        _queryValidator = queryValidator;
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.PatientManage)]
    [ProducesResponseType(typeof(VisitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateVisit(int patientId, CancellationToken ct)
    {
        if (_currentUser.UserId is not int createdByUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _visitService.CreateVisitAsync(
            patientId,
            createdByUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(PagedResult<VisitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVisits(
        int patientId,
        [FromQuery] VisitQueryRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _visitService.GetPatientVisitsAsync(
            patientId,
            currentUserId,
            _currentUser.RoleCode,
            request,
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult ToValidationProblem(ValidationResult validation)
        => BadRequest(new
        {
            code = Error.Validation.Code,
            message = "Dữ liệu đầu vào không hợp lệ.",
            errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
        });

    private IActionResult Failure(Error error)
    {
        var statusCode = error.Code switch
        {
            "GEN_001" => StatusCodes.Status404NotFound,
            "PAT_090" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
