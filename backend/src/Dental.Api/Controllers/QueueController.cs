using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Features.Queue.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/queue")]
[Authorize]
public sealed class QueueController : ControllerBase
{
    private readonly QueueService _queueService;
    private readonly ICurrentUser _currentUser;
    private readonly CheckInRequestValidator _checkInValidator;
    private readonly QueueQueryRequestValidator _queryValidator;
    private readonly UpdateQueueStatusRequestValidator _statusValidator;
    private readonly CallNextRequestValidator _callNextValidator;

    public QueueController(
        QueueService queueService,
        ICurrentUser currentUser,
        CheckInRequestValidator checkInValidator,
        QueueQueryRequestValidator queryValidator,
        UpdateQueueStatusRequestValidator statusValidator,
        CallNextRequestValidator callNextValidator)
    {
        _queueService = queueService;
        _currentUser = currentUser;
        _checkInValidator = checkInValidator;
        _queryValidator = queryValidator;
        _statusValidator = statusValidator;
        _callNextValidator = callNextValidator;
    }

    [HttpPost("check-in")]
    [Authorize(Policy = AppPolicies.QueueCheckIn)]
    [ProducesResponseType(typeof(QueueEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var validation = await _checkInValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _queueService.CheckInAsync(
            actorUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Policy = AppPolicies.QueueView)]
    [ProducesResponseType(typeof(PagedResult<QueueEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQueue([FromQuery] QueueQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _queueService.GetTodayQueueAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:int}")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Policy = AppPolicies.QueueView)]
    [ProducesResponseType(typeof(QueueEntryDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQueueDetail(int id, CancellationToken ct)
    {
        var result = await _queueService.GetQueueEntryDetailAsync(id, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = AppPolicies.QueueStatusUpdate)]
    [ProducesResponseType(typeof(QueueEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateQueueStatusRequest request, CancellationToken ct)
    {
        var validation = await _statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var actorRole = _currentUser.RoleCode ?? string.Empty;

        var result = await _queueService.UpdateQueueStatusAsync(
            id,
            actorUserId,
            actorRole,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("call-next")]
    [Authorize(Policy = AppPolicies.QueueStatusUpdate)]
    [ProducesResponseType(typeof(QueueEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CallNext([FromBody] CallNextRequest? request, CancellationToken ct)
    {
        request ??= new CallNextRequest(null);
        var validation = await _callNextValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var actorRole = _currentUser.RoleCode ?? string.Empty;

        var result = await _queueService.CallNextAsync(
            actorUserId,
            actorRole,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("display")]
    [HttpGet("public-monitor")]
    [AllowAnonymous]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [ProducesResponseType(typeof(PublicQueueMonitorResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicDisplay(CancellationToken ct)
    {
        var result = await _queueService.GetPublicQueueDisplayAsync(ct);
        return Ok(result.Value);
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
            "GEN_001" or "CHK_001" or "CHK_008" or "APP_001" => StatusCodes.Status404NotFound,
            "CHK_004" => StatusCodes.Status403Forbidden,
            "CHK_002" or "CHK_009" or "PAT_090" or "PAT_091" or "APP_008" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
