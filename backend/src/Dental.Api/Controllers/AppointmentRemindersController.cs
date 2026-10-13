using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = AppPolicies.AppointmentManage)]
public sealed class AppointmentRemindersController(AppointmentReminderService service, ICurrentUser user,
    SimulateAppointmentRemindersValidator simulateValidator, ReminderLogQueryValidator queryValidator) : ControllerBase
{
    [HttpPost("simulate-reminder")]
    public async Task<IActionResult> Simulate([FromBody] SimulateAppointmentRemindersRequest request, CancellationToken ct)
    {
        var validation = await simulateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return BadRequest(new { code = Error.Validation.Code, message = "Cần xác nhận mô phỏng nhắc hẹn." });
        if (user.UserId is not int id) return Unauthorized();
        var result = await service.SimulateAsync(id, user.RoleCode, request.Confirmed, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("reminder-logs")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetLogs([FromQuery] ReminderLogQueryRequest query, CancellationToken ct)
    {
        var validation = await queryValidator.ValidateAsync(query, ct);
        if (!validation.IsValid) return BadRequest(new { code = Error.Validation.Code, message = "Bộ lọc nhật ký không hợp lệ." });
        var result = await service.GetLogsAsync(user.RoleCode, query, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    { "GEN_002" => 403, "APP_010" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
