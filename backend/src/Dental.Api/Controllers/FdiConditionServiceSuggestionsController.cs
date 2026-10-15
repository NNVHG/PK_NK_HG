using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/fdi/condition-service-suggestions")]
[Authorize(Policy = AppPolicies.FdiServiceAssign)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FdiConditionServiceSuggestionsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] string conditionCode)
    {
        if (!FdiCodes.Conditions.Contains(conditionCode, StringComparer.Ordinal))
            return BadRequest(new { code = "FDI_044", message = "Mã tình trạng răng không hợp lệ." });

        DentalConditionServiceMappings.TryGet(conditionCode, out var suggestions);
        return Ok(suggestions);
    }
}
