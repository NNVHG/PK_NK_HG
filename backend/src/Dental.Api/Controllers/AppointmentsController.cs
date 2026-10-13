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
    private readonly SelfCancellationEligibilityRequestValidator _eligibilityValidator;

    public AppointmentsController(
        AppointmentService appointmentService,
        ICurrentUser currentUser,
        CreateAppointmentRequestValidator createValidator,
        AppointmentQueryRequestValidator queryValidator,
        RescheduleAppointmentRequestValidator rescheduleValidator,
        CancelAppointmentRequestValidator cancelValidator,
        SelfCancellationEligibilityRequestValidator eligibilityValidator)
    {
        _appointmentService = appointmentService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _queryValidator = queryValidator;
        _rescheduleValidator = rescheduleValidator;
        _cancelValidator = cancelValidator;
        _eligibilityValidator = eligibilityValidator;
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
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Policy = AppPolicies.AppointmentView)]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppointments([FromQuery] AppointmentQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int userId) return Unauthorized();
        var result = await _appointmentService.GetAccessibleAppointmentsAsync(request, userId, _currentUser.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("{id:int}")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Policy = AppPolicies.AppointmentView)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(int id, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId) return Unauthorized();
        var result = await _appointmentService.GetAccessibleAppointmentByIdAsync(id, userId, _currentUser.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("desk")]
    [Authorize(Policy = AppPolicies.AppointmentManage)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetDeskAppointments([FromQuery] AppointmentQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ToValidationProblem(validation);
        var result = await _appointmentService.GetAppointmentsAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:int}/cancellation-eligibility")]
    [Authorize(Roles = Dental.Domain.Constants.RoleCodes.Patient)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetSelfCancellationEligibility(int id, CancellationToken ct)
    {
        var validation = await _eligibilityValidator.ValidateAsync(id, ct);
        if (!validation.IsValid) return ToValidationProblem(validation);
        if (_currentUser.UserId is not int userId) return Unauthorized();
        var result = await _appointmentService.GetSelfCancellationEligibilityAsync(id, userId, ct);
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
            _currentUser.RoleCode,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:int}/reschedule")]
    [Authorize(Policy = AppPolicies.AppointmentReschedule)]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleAppointment(int id, [FromBody] RescheduleAppointmentRequest request, CancellationToken ct)
    {
        var idValidation = await _eligibilityValidator.ValidateAsync(id, ct);
        if (!idValidation.IsValid) return ToValidationProblem(idValidation);
        var validation = await _rescheduleValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _appointmentService.RescheduleAppointmentAsync(
            id,
            actorUserId,
            _currentUser.RoleCode,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("{id:int}/rescheduling-eligibility")]
    [Authorize(Roles = Dental.Domain.Constants.RoleCodes.Patient)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetSelfReschedulingEligibility(int id, CancellationToken ct)
    {
        var validation = await _eligibilityValidator.ValidateAsync(id, ct);
        if (!validation.IsValid) return ToValidationProblem(validation);
        if (_currentUser.UserId is not int userId) return Unauthorized();
        var result = await _appointmentService.GetSelfReschedulingEligibilityAsync(id, userId, ct);
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
            "GEN_002" => StatusCodes.Status403Forbidden,
            "GEN_001" or "APP_001" => StatusCodes.Status404NotFound,
            "APP_002" or "APP_005" or "APP_009" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
