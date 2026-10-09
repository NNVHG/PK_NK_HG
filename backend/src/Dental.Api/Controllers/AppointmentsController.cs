using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public sealed class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _appointmentService;
    private readonly ICurrentUser _currentUser;
    private readonly CreateAppointmentRequestValidator _createValidator;
    private readonly AppointmentQueryRequestValidator _queryValidator;
    private readonly RescheduleAppointmentRequestValidator _rescheduleValidator;
    private readonly CancelAppointmentRequestValidator _cancelValidator;

    public AppointmentsController(
        AppointmentService appointmentService,
        ICurrentUser currentUser,
        CreateAppointmentRequestValidator createValidator,
        AppointmentQueryRequestValidator queryValidator,
        RescheduleAppointmentRequestValidator rescheduleValidator,
        CancelAppointmentRequestValidator cancelValidator)
    {
        _appointmentService = appointmentService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _queryValidator = queryValidator;
        _rescheduleValidator = rescheduleValidator;
        _cancelValidator = cancelValidator;
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.AppointmentCreate)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _appointmentService.CreateAppointmentAsync(
            actorUserId,
            _currentUser.RoleCode,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.AppointmentView)]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppointments([FromQuery] AppointmentQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _appointmentService.GetAppointmentsAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = AppPolicies.AppointmentView)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(int id, CancellationToken ct)
    {
        var result = await _appointmentService.GetAppointmentByIdAsync(id, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:int}/cancel")]
    [Authorize(Policy = AppPolicies.AppointmentView)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(int id, [FromBody] CancelAppointmentRequest request, CancellationToken ct)
    {
        var validation = await _cancelValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _appointmentService.CancelAppointmentAsync(
            id,
            actorUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:int}/reschedule")]
    [Authorize(Policy = AppPolicies.AppointmentManage)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleAppointment(int id, [FromBody] RescheduleAppointmentRequest request, CancellationToken ct)
    {
        var validation = await _rescheduleValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _appointmentService.RescheduleAppointmentAsync(
            id,
            actorUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
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
            "GEN_001" or "APP_001" => StatusCodes.Status404NotFound,
            "APP_002" or "APP_005" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
