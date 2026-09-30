using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Features.Staff.Services;
using Dental.Application.Features.Staff.Validators;
using Dental.Application.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class StaffController : ControllerBase
{
    private readonly StaffService _staffService;
    private readonly ICurrentUser _currentUser;
    private readonly StaffQueryRequestValidator _queryValidator;
    private readonly CreateStaffRequestValidator _createValidator;
    private readonly UpdateStaffRequestValidator _updateValidator;

    public StaffController(
        StaffService staffService,
        ICurrentUser currentUser,
        StaffQueryRequestValidator queryValidator,
        CreateStaffRequestValidator createValidator,
        UpdateStaffRequestValidator updateValidator)
    {
        _staffService = staffService;
        _currentUser = currentUser;
        _queryValidator = queryValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StaffAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStaff([FromQuery] StaffQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _staffService.GetStaffAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StaffAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _staffService.CreateStaffAsync(
            actorUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(StaffAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStaff(int id, [FromBody] UpdateStaffRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _staffService.UpdateStaffAsync(
            actorUserId,
            id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("{id:int}/lock")]
    [ProducesResponseType(typeof(StaffAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LockStaff(int id, CancellationToken ct)
    {
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _staffService.LockStaffAsync(
            actorUserId,
            id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("{id:int}/unlock")]
    [ProducesResponseType(typeof(StaffAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockStaff(int id, CancellationToken ct)
    {
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _staffService.UnlockStaffAsync(
            actorUserId,
            id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult ToValidationProblem(ValidationResult validation)
        => BadRequest(new
        {
            code = Error.Validation.Code,
            message = Error.Validation.Message,
            errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
        });

    private IActionResult Failure(Error error)
    {
        var statusCode = error.Code switch
        {
            var code when code == Error.NotFound.Code => StatusCodes.Status404NotFound,
            var code when code == Error.CannotLockSelf.Code => StatusCodes.Status403Forbidden,
            var code when code == Error.Conflict.Code ||
                          code == Error.StaffPhoneExists.Code ||
                          code == Error.LastActiveAdmin.Code => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
