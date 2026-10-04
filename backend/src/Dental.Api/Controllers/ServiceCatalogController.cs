using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Features.ServiceCatalog.Services;
using Dental.Application.Features.ServiceCatalog.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize]
public sealed class ServiceCatalogController : ControllerBase
{
    private readonly ServiceCatalogService _serviceCatalog;
    private readonly ICurrentUser _currentUser;
    private readonly ServiceCatalogQueryRequestValidator _queryValidator;
    private readonly CreateDentalServiceRequestValidator _createValidator;
    private readonly UpdateDentalServiceRequestValidator _updateValidator;
    private readonly CreateServicePriceRequestValidator _priceValidator;
    private readonly ServiceIdRequestValidator _idValidator;

    public ServiceCatalogController(
        ServiceCatalogService serviceCatalog,
        ICurrentUser currentUser,
        ServiceCatalogQueryRequestValidator queryValidator,
        CreateDentalServiceRequestValidator createValidator,
        UpdateDentalServiceRequestValidator updateValidator,
        CreateServicePriceRequestValidator priceValidator,
        ServiceIdRequestValidator idValidator)
    {
        _serviceCatalog = serviceCatalog;
        _currentUser = currentUser;
        _queryValidator = queryValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _priceValidator = priceValidator;
        _idValidator = idValidator;
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ServiceCatalogView)]
    [ProducesResponseType(typeof(PagedResult<ServiceCatalogListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPage([FromQuery] ServiceCatalogQueryRequest request, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        return Ok(await _serviceCatalog.GetPageAsync(request, ct));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = AppPolicies.ServiceCatalogView)]
    [ProducesResponseType(typeof(ServiceCatalogDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var invalidId = ValidateId(id);
        if (invalidId is not null)
            return invalidId;

        var result = await _serviceCatalog.GetByIdAsync(
            id,
            includePriceHistory: _currentUser.RoleCode == Dental.Domain.Constants.RoleCodes.Admin,
            ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServiceCatalogDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDentalServiceRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _serviceCatalog.CreateAsync(
            actorUserId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.IsFailure)
            return Failure(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.DentalServiceId }, result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServiceCatalogDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDentalServiceRequest request, CancellationToken ct)
    {
        var invalidId = ValidateId(id);
        if (invalidId is not null)
            return invalidId;
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _serviceCatalog.UpdateAsync(
            actorUserId, id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost("{id:int}/prices")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServicePriceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPrice(int id, [FromBody] CreateServicePriceRequest request, CancellationToken ct)
    {
        var invalidId = ValidateId(id);
        if (invalidId is not null)
            return invalidId;
        var validation = await _priceValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _serviceCatalog.AddPriceAsync(
            actorUserId, id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServiceCatalogDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(int id, CancellationToken ct) => SetActive(id, false, ct);

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServiceCatalogDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Activate(int id, CancellationToken ct) => SetActive(id, true, ct);

    private async Task<IActionResult> SetActive(int id, bool isActive, CancellationToken ct)
    {
        var invalidId = ValidateId(id);
        if (invalidId is not null)
            return invalidId;
        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _serviceCatalog.SetActiveAsync(
            actorUserId, id, isActive, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult ToValidationProblem(ValidationResult validation)
        => BadRequest(new
        {
            code = Error.Validation.Code,
            message = Error.Validation.Message,
            errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
        });

    private IActionResult? ValidateId(int id)
    {
        var validation = _idValidator.Validate(new ServiceIdRequest(id));
        return validation.IsValid ? null : ToValidationProblem(validation);
    }

    private IActionResult Failure(Error error)
    {
        var statusCode = error.Code switch
        {
            var code when code == Error.NotFound.Code => StatusCodes.Status404NotFound,
            var code when code == Error.Conflict.Code || code == Error.ServiceCodeExists.Code ||
                          code == Error.ServicePriceEffectiveDateInvalid.Code => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
